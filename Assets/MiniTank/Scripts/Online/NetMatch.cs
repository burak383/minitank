using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Online maçın ağ durumu (Photon Fusion, Host modu).
    ///
    /// Oyunu sadece host simüle eder: tank hareketi, mermiler, hasar, botlar, skor ve süre.
    /// Host her ağ adımında 10 tankın durumunu bu nesneye yazar; diğer oyuncular bu durumu
    /// yumuşatarak (interpolasyon) gösterir ve sadece girdilerini host'a yollar.
    /// Mermi, hasar, öldürme, yetenek ve güçlendirme olayları RPC ile herkese duyurulur.
    ///
    /// Boş koltukları botlar doldurur; maç sırasında gelen oyuncu bir botun yerini alır,
    /// çıkan oyuncunun tankını bot devralır.
    /// </summary>
    public class NetMatch : NetworkBehaviour
    {
        public const int SeatCount = 10;
        public const int TeamSize = 5;
        /// <summary>Oyuncular için bekleme süresi (saniye). Dolunca boş koltuklar botla başlar.</summary>
        public const float WaitSeconds = 20f;
        /// <summary>Özel odada oda sahibi "Başlat"a basana kadar en fazla bu kadar beklenir.</summary>
        public const float PrivateWaitSeconds = 300f;
        /// <summary>İstemcide gösterim gecikmesi (akıcı hareket için).</summary>
        const float InterpDelay = 0.1f;

        public static NetMatch Instance { get; private set; }

        public enum MatchPhase { Waiting = 0, Playing = 1, Ended = 2 }

        [Networked, Capacity(SeatCount)] public NetworkArray<SeatData> Seats => default;
        [Networked, Capacity(SeatCount)] public NetworkArray<TankNet> TankStates => default;
        [Networked, Capacity(5)] public NetworkArray<CaptureNet> Captures => default;
        [Networked, Capacity(8)] public NetworkArray<PowerUpNet> PowerUps => default;

        [Networked] public int PhaseValue { get; set; }
        [Networked] public TickTimer WaitTimer { get; set; }
        [Networked] public float TimeLeft { get; set; }
        [Networked] public float ScoreBlue { get; set; }
        [Networked] public float ScoreRed { get; set; }
        [Networked] public int WinnerTeam { get; set; }
        [Networked] public NetworkBool Draw { get; set; }

        public MatchPhase Phase => (MatchPhase)PhaseValue;

        public int HumanCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < SeatCount; i++) if (Seats[i].Player != PlayerRef.None) n++;
                return n;
            }
        }

        public float WaitRemaining
        {
            get
            {
                if (Runner == null) return 0f;
                var r = WaitTimer.RemainingTime(Runner);
                return r.HasValue ? r.Value : 0f;
            }
        }

        MatchManager mm;
        readonly int[] builtVersion = new int[SeatCount];
        readonly RemoteInputSource[] remoteInputs = new RemoteInputSource[SeatCount];
        bool hooked;

        // ================================================================== Yaşam döngüsü

        public override void Spawned()
        {
            Instance = this;
            mm = MatchManager.Instance;
            if (mm == null) { Debug.LogError("[MiniTank] Online: MatchManager bulunamadı."); return; }
            mm.SetOnlineRole(!HasStateAuthority);

            if (HasStateAuthority)
            {
                InitSeats();
                PhaseValue = (int)MatchPhase.Waiting;
                WaitTimer = TickTimer.CreateFromSeconds(Runner, OnlineSession.IsPrivate ? PrivateWaitSeconds : WaitSeconds);
                TimeLeft = mm.matchDuration;

                // Host'un kendisi de bir oyuncu
                var info = LocalRegistration();
                AssignSeat(Runner.LocalPlayer, info.classIndex, info.name, info.engine, info.armor, info.gun, info.camo);

                Projectile.Launched += OnProjectileLaunched;
                TankController.AnyDamaged += OnAnyDamaged;
                TankController.AnyAbility += OnAnyAbility;
                TankController.AnyPowerUp += OnAnyPowerUp;
                mm.Kill += OnKill;
                mm.MatchEndedHost += OnMatchEndedHost;
                hooked = true;
            }
            else
            {
                var info = LocalRegistration();
                RpcRegister(info.classIndex, info.name, info.engine, info.armor, info.gun, info.camo);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (hooked)
            {
                Projectile.Launched -= OnProjectileLaunched;
                TankController.AnyDamaged -= OnAnyDamaged;
                TankController.AnyAbility -= OnAnyAbility;
                TankController.AnyPowerUp -= OnAnyPowerUp;
                if (mm != null) { mm.Kill -= OnKill; mm.MatchEndedHost -= OnMatchEndedHost; }
                hooked = false;
            }
            if (Instance == this) Instance = null;
        }

        void OnDestroy()
        {
            if (hooked)
            {
                Projectile.Launched -= OnProjectileLaunched;
                TankController.AnyDamaged -= OnAnyDamaged;
                TankController.AnyAbility -= OnAnyAbility;
                TankController.AnyPowerUp -= OnAnyPowerUp;
                hooked = false;
            }
            if (Instance == this) Instance = null;
        }

        struct Registration
        {
            public int classIndex, engine, armor, gun;
            public string name, camo;
        }

        Registration LocalRegistration()
        {
            var r = new Registration
            {
                classIndex = MatchSettings.PlayerClassIndex,
                name = PlayerSession.IsSignedIn ? PlayerSession.DisplayName : "Oyuncu",
                camo = Economy.DefaultCamo,
            };
            if (mm != null && mm.availableClasses != null && mm.availableClasses.Length > 0)
                r.classIndex = Mathf.Clamp(r.classIndex, 0, mm.availableClasses.Length - 1);
            if (ProgressService.IsLoaded && mm != null && mm.availableClasses != null && r.classIndex < mm.availableClasses.Length)
            {
                var cls = mm.availableClasses[r.classIndex];
                if (cls != null)
                {
                    var u = ProgressService.GetUpgrade(cls.name);
                    r.engine = u.engine; r.armor = u.armor; r.gun = u.gun;
                }
                r.camo = ProgressService.Current.equippedCamo;
            }
            if (string.IsNullOrEmpty(r.name)) r.name = "Oyuncu";
            if (r.name.Length > 16) r.name = r.name.Substring(0, 16);
            return r;
        }

        // ================================================================== Koltuklar (host)

        int ClassCount => mm != null && mm.availableClasses != null ? mm.availableClasses.Length : 1;

        void InitSeats()
        {
            int botLevel = 0;
            if (ProgressService.IsLoaded && ProgressService.Current.upgrades != null)
            {
                int total = 0, count = 0;
                foreach (var up in ProgressService.Current.upgrades) { total += up.engine + up.armor + up.gun; count += 3; }
                botLevel = count > 0 ? Mathf.RoundToInt((float)total / count) : 0;
            }
            for (int i = 0; i < SeatCount; i++)
            {
                int cls = (i % TeamSize) % Mathf.Max(1, ClassCount);
                var seat = new SeatData
                {
                    Player = PlayerRef.None,
                    ClassIndex = cls,
                    Version = 1,
                    Engine = botLevel, Armor = botLevel, Gun = botLevel,
                    Camo = 0,
                };
                seat.Name = BotName(cls, i);
                Seats.Set(i, seat);
            }
        }

        string BotName(int classIndex, int seat)
        {
            var c = mm != null && mm.availableClasses != null && classIndex < mm.availableClasses.Length ? mm.availableClasses[classIndex] : null;
            return (c != null ? c.displayName : "Tank") + " (Bot " + (seat % TeamSize + 1) + ")";
        }

        static Team TeamOf(int seat) => seat < TeamSize ? Team.Blue : Team.Red;

        static int CamoIndex(string id)
        {
            for (int i = 0; i < Economy.Camos.Length; i++) if (Economy.Camos[i].id == id) return i;
            return 0;
        }

        int SeatOfPlayer(PlayerRef player)
        {
            for (int i = 0; i < SeatCount; i++) if (Seats[i].Player == player) return i;
            return -1;
        }

        /// <summary>Oyuncuyu insan sayısı az olan takımda bir botun yerine oturtur.</summary>
        void AssignSeat(PlayerRef player, int classIndex, string name, int engine, int armor, int gun, string camo)
        {
            int seat = SeatOfPlayer(player);
            if (seat < 0)
            {
                int blueHumans = 0, redHumans = 0;
                for (int i = 0; i < SeatCount; i++)
                    if (Seats[i].Player != PlayerRef.None) { if (TeamOf(i) == Team.Blue) blueHumans++; else redHumans++; }
                Team preferred = blueHumans <= redHumans ? Team.Blue : Team.Red;
                seat = FindBotSeat(preferred);
                if (seat < 0) seat = FindBotSeat(preferred == Team.Blue ? Team.Red : Team.Blue);
                if (seat < 0) { Debug.LogWarning("[MiniTank] Online: boş koltuk yok."); return; }
            }

            var s = Seats[seat];
            s.Player = player;
            s.ClassIndex = Mathf.Clamp(classIndex, 0, Mathf.Max(0, ClassCount - 1));
            s.Engine = Mathf.Clamp(engine, 0, Economy.MaxUpgradeLevel);
            s.Armor = Mathf.Clamp(armor, 0, Economy.MaxUpgradeLevel);
            s.Gun = Mathf.Clamp(gun, 0, Economy.MaxUpgradeLevel);
            s.Camo = CamoIndex(camo);
            s.Name = string.IsNullOrEmpty(name) ? "Oyuncu" : (name.Length > 16 ? name.Substring(0, 16) : name);
            s.Version++;
            Seats.Set(seat, s);
        }

        int FindBotSeat(Team team)
        {
            int start = team == Team.Blue ? 0 : TeamSize;
            // Önce ölü olmayan botlar değil, sıradaki ilk bot koltuğu (sabit sıra herkes için tutarlı)
            for (int i = start; i < start + TeamSize; i++)
                if (Seats[i].Player == PlayerRef.None) return i;
            return -1;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcRegister(int classIndex, string name, int engine, int armor, int gun, string camo, RpcInfo info = default)
        {
            var player = info.Source;
            if (player == PlayerRef.None) return;
            // Son Tank modunda maç başladıktan sonra katılım yok (ölü botun yerine canlı tank olmasın)
            if (Phase != MatchPhase.Waiting && mm != null && mm.Mode == MatchMode.Elimination) return;
            if (Phase == MatchPhase.Ended) return;
            AssignSeat(player, classIndex, name, engine, armor, gun, camo);
        }

        /// <summary>Host: oyuncu çıktı, tankını bot devralır.</summary>
        public void OnPlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            int seat = SeatOfPlayer(player);
            if (seat < 0) return;
            var s = Seats[seat];
            s.Player = PlayerRef.None;
            s.Name = BotName(s.ClassIndex, seat);
            Seats.Set(seat, s);
            remoteInputs[seat] = null;

            var tank = mm != null ? mm.GetSeatTank(seat) : null;
            if (tank != null)
            {
                var bot = tank.GetComponent<BotInputSource>();
                if (bot == null) bot = tank.gameObject.AddComponent<BotInputSource>();
                bot.enabled = true;
                tank.SetControl(s.Name.ToString(), true, bot);
            }
        }

        // ================================================================== Tankları kur (herkes)

        void SyncSeatTanks()
        {
            if (mm == null || mm.availableClasses == null || mm.availableClasses.Length == 0) return;
            bool isHost = HasStateAuthority;
            for (int i = 0; i < SeatCount; i++)
            {
                var s = Seats[i];
                if (s.Version == 0 || s.Version == builtVersion[i]) continue;
                builtVersion[i] = s.Version;

                var cls = mm.availableClasses[Mathf.Clamp(s.ClassIndex, 0, mm.availableClasses.Length - 1)];
                bool local = s.Player != PlayerRef.None && s.Player == Runner.LocalPlayer;
                ITankInputSource remote = null;
                if (isHost && s.Player != PlayerRef.None && !local)
                {
                    remoteInputs[i] = new RemoteInputSource();
                    remote = remoteInputs[i];
                }
                string camo = s.Camo >= 0 && s.Camo < Economy.Camos.Length ? Economy.Camos[s.Camo].id : Economy.DefaultCamo;
                var tank = mm.SetSeatTank(i, cls, TeamOf(i), i % TeamSize, local, remote, s.Name.ToString(),
                                          !isHost, s.Engine, s.Armor, s.Gun, camo);
                if (!isHost) ResetInterpolation(i);
                if (local && tank != null) mm.FocusCamera(tank);
            }

            // İstemci: kendi tankımız yokken (kayıt bekleniyor) kamera bir takım arkadaşını izlesin
            if (!isHost && mm.PlayerTank == null)
            {
                var any = mm.GetSeatTank(0);
                if (any != null && mm.playerCamera != null && mm.playerCamera.target == null) mm.FocusCamera(any);
            }
        }

        // ================================================================== Host simülasyon adımı

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || mm == null) return;

            SyncSeatTanks();

            // Uzak oyuncuların girdileri
            for (int i = 0; i < SeatCount; i++)
            {
                var p = Seats[i].Player;
                if (p == PlayerRef.None || p == Runner.LocalPlayer || remoteInputs[i] == null) continue;
                NetInput input;
                if (Runner.TryGetInputForPlayer<NetInput>(p, out input)) remoteInputs[i].Push(input);
            }

            if (Phase == MatchPhase.Waiting && (WaitTimer.Expired(Runner) || HumanCount >= SeatCount))
            {
                PhaseValue = (int)MatchPhase.Playing;
                mm.BeginOnlineMatch();
                // Son Tank modunda maç başlayınca yeni oyuncu alınmaz
                if (mm.Mode == MatchMode.Elimination && Runner.SessionInfo.IsValid) Runner.SessionInfo.IsOpen = false;
            }

            WriteState();
        }

        void WriteState()
        {
            for (int i = 0; i < SeatCount; i++)
            {
                var t = mm.GetSeatTank(i);
                if (t == null) continue;
                var tr = t.transform;
                var st = new TankNet
                {
                    Pos = tr.position,
                    Yaw = tr.eulerAngles.y,
                    TurretYaw = t.TurretYaw,
                    GunPitch = t.GunPitch,
                    Health = t.Health,
                    Speed = t.CurrentSpeed,
                    Reload = t.ReloadRemaining,
                    AbilityCooldown = t.AbilityCooldownRemaining,
                    AbilityActive = t.AbilityActiveRemaining,
                    BuffHealth = t.BuffRemaining(PowerUpType.Health),
                    BuffSpeed = t.BuffRemaining(PowerUpType.Speed),
                    BuffDamage = t.BuffRemaining(PowerUpType.Damage),
                    BuffReload = t.BuffRemaining(PowerUpType.Reload),
                    Kills = t.Kills, Deaths = t.Deaths, Assists = t.Assists,
                    SpawnCount = t.SpawnCount,
                    Dead = t.IsDead || !t.gameObject.activeSelf,
                };
                TankStates.Set(i, st);
            }

            TimeLeft = mm.TimeLeft;
            ScoreBlue = mm.RawScore(Team.Blue);
            ScoreRed = mm.RawScore(Team.Red);

            var cps = mm.capturePoints;
            if (cps != null)
            {
                for (int i = 0; i < cps.Length && i < Captures.Length; i++)
                {
                    var cp = cps[i];
                    if (cp == null) continue;
                    Captures.Set(i, new CaptureNet
                    {
                        Progress = cp.Progress,
                        Owner = cp.HasOwner ? (int)cp.Owner : -1,
                        Contested = cp.Contested,
                    });
                }
            }

            var list = PowerUp.All;
            for (int i = 0; i < PowerUps.Length; i++)
            {
                if (i < list.Count && list[i] != null)
                    PowerUps.Set(i, new PowerUpNet { Id = list[i].netId, Pos = list[i].BasePosition - Vector3.up * 1.2f, Type = (int)list[i].type });
                else
                    PowerUps.Set(i, default(PowerUpNet));
            }
        }

        /// <summary>Host: özel odada maçı hemen başlat.</summary>
        public void StartNow()
        {
            if (!HasStateAuthority || Phase != MatchPhase.Waiting) return;
            WaitTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
        }

        void OnMatchEndedHost()
        {
            if (!HasStateAuthority) return;
            WinnerTeam = (int)mm.Winner;
            Draw = mm.IsDraw;
            PhaseValue = (int)MatchPhase.Ended;
            TimeLeft = mm.TimeLeft;
            ScoreBlue = mm.RawScore(Team.Blue);
            ScoreRed = mm.RawScore(Team.Red);
            if (Runner.SessionInfo.IsValid) Runner.SessionInfo.IsOpen = false;
        }

        // ================================================================== Olaylar (host → herkes)

        int Seat(TankController t) => mm != null ? mm.SeatOf(t) : -1;

        void OnProjectileLaunched(Projectile p, TankController owner, Vector3 pos, Vector3 vel, bool custom, float splash, bool gravity, float life)
        {
            int seat = Seat(owner);
            if (seat >= 0) RpcShot(seat, pos, vel, custom, splash, gravity, life);
        }

        void OnAnyDamaged(DamageInfo info)
        {
            int victim = Seat(info.victim);
            if (victim < 0) return;
            RpcDamage(victim, Seat(info.attacker), info.amount, info.point, info.direction, (int)info.zone, info.killed);
        }

        void OnKill(TankController victim, TankController killer)
        {
            int v = Seat(victim);
            if (v < 0) return;
            int mask = 0;
            foreach (var a in victim.LastAssisters)
            {
                int s = Seat(a);
                if (s >= 0) mask |= 1 << s;
            }
            RpcKill(v, Seat(killer), mask);
        }

        void OnAnyAbility(TankController t, AbilityType a)
        {
            int seat = Seat(t);
            if (seat >= 0) RpcAbility(seat, (int)a, t.LastAbilityTarget);
        }

        void OnAnyPowerUp(TankController t, PowerUpType type)
        {
            int seat = Seat(t);
            if (seat >= 0) RpcPowerUp(seat, (int)type);
        }

        TankController ClientTank(int seat)
        {
            if (HasStateAuthority || mm == null) return null; // host olayları zaten yerelde yaşadı
            return mm.GetSeatTank(seat);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcShot(int seat, Vector3 pos, Vector3 vel, NetworkBool custom, float splash, NetworkBool gravity, float life)
        {
            var t = ClientTank(seat);
            if (t == null) return;
            // Mermiyi, ekranda görünen (biraz geriden gelen) namludan çıkar ki tanktan ayrı durmasın
            Vector3 start = pos;
            if (!custom && t.muzzle != null && t.gameObject.activeInHierarchy && (t.muzzle.position - pos).sqrMagnitude < 36f)
                start = t.muzzle.position;
            t.PlayShotVisual(start, vel, custom, splash, gravity, life);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcDamage(int victim, int attacker, float amount, Vector3 point, Vector3 dir, int zone, NetworkBool killed)
        {
            var v = ClientTank(victim);
            if (v == null) return;
            var a = attacker >= 0 ? mm.GetSeatTank(attacker) : null;
            v.RaiseDamageRemote(a, amount, point, dir, (HitZone)zone, killed);
        }

        readonly List<TankController> assistBuffer = new List<TankController>();
        readonly float[] killRpcTime = new float[SeatCount];

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcKill(int victim, int killer, int assistMask)
        {
            var v = ClientTank(victim);
            if (v == null) return;
            killRpcTime[victim] = Time.time;
            assistBuffer.Clear();
            for (int i = 0; i < SeatCount; i++)
            {
                if ((assistMask & (1 << i)) == 0) continue;
                var a = mm.GetSeatTank(i);
                if (a != null) assistBuffer.Add(a);
            }
            v.PuppetDie(killer >= 0 ? mm.GetSeatTank(killer) : null, assistBuffer, true);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcAbility(int seat, int ability, Vector3 target)
        {
            var t = ClientTank(seat);
            if (t != null) t.PlayAbilityVisual((AbilityType)ability, target);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcPowerUp(int seat, int type)
        {
            var t = ClientTank(seat);
            if (t != null) t.PlayPowerUpVisual((PowerUpType)type);
        }

        // ================================================================== İstemci: gösterim

        class Frame
        {
            public float time;
            public readonly Vector3[] pos = new Vector3[SeatCount];
            public readonly float[] yaw = new float[SeatCount];
            public readonly float[] turret = new float[SeatCount];
            public readonly float[] gun = new float[SeatCount];
        }

        readonly List<Frame> frames = new List<Frame>();
        readonly Stack<Frame> framePool = new Stack<Frame>();
        readonly bool[] snapSeat = new bool[SeatCount];
        readonly float[] deadSince = new float[SeatCount];
        int lastServerTick = -1;
        float renderTime;
        bool clockReady;

        void ResetInterpolation(int seat) { snapSeat[seat] = true; }

        public override void Render()
        {
            if (mm == null) return;

            if (HasStateAuthority)
            {
                // Host: sadece kendi kamerası/arayüzü; tanklar gerçek fizikle hareket ediyor
                return;
            }

            SyncSeatTanks();

            RecordFrame();
            AdvanceClock();
            ApplyTanks();
            RefreshNames();
            ApplyCaptures();
            ApplyPowerUps();

            if (Phase != MatchPhase.Waiting && !mm.Started) mm.BeginOnlineMatch();
            mm.ApplyNetScore(ScoreBlue, ScoreRed, TimeLeft);
            if (Phase == MatchPhase.Ended && !mm.IsOver)
                mm.EndMatchFromNet((Team)Mathf.Clamp(WinnerTeam, 0, 1), Draw);
        }

        void RecordFrame()
        {
            int tick = (int)Runner.LatestServerTick;
            if (tick == lastServerTick) return;
            lastServerTick = tick;

            var f = framePool.Count > 0 ? framePool.Pop() : new Frame();
            f.time = tick * Runner.DeltaTime;
            for (int i = 0; i < SeatCount; i++)
            {
                var st = TankStates[i];
                f.pos[i] = st.Pos;
                f.yaw[i] = st.Yaw;
                f.turret[i] = st.TurretYaw;
                f.gun[i] = st.GunPitch;
            }
            frames.Add(f);
            while (frames.Count > 12) { framePool.Push(frames[0]); frames.RemoveAt(0); }
        }

        void AdvanceClock()
        {
            if (frames.Count == 0) return;
            float target = frames[frames.Count - 1].time - InterpDelay;
            if (!clockReady) { renderTime = target; clockReady = true; return; }
            renderTime += Time.unscaledDeltaTime;
            float err = target - renderTime;
            if (Mathf.Abs(err) > 0.35f) renderTime = target;
            else renderTime += err * Mathf.Clamp01(Time.unscaledDeltaTime * 2f);
        }

        bool Sample(int seat, out Vector3 pos, out float yaw, out float turret, out float gun)
        {
            pos = Vector3.zero; yaw = turret = gun = 0f;
            if (frames.Count == 0) return false;
            Frame a = frames[0], b = frames[frames.Count - 1];
            if (renderTime <= a.time) b = a;
            else if (renderTime >= b.time) a = b;
            else
            {
                for (int i = 0; i < frames.Count - 1; i++)
                {
                    if (frames[i].time <= renderTime && frames[i + 1].time >= renderTime) { a = frames[i]; b = frames[i + 1]; break; }
                }
            }
            float t = b.time > a.time ? Mathf.Clamp01((renderTime - a.time) / (b.time - a.time)) : 1f;
            pos = Vector3.Lerp(a.pos[seat], b.pos[seat], t);
            yaw = Mathf.LerpAngle(a.yaw[seat], b.yaw[seat], t);
            turret = Mathf.LerpAngle(a.turret[seat], b.turret[seat], t);
            gun = Mathf.Lerp(a.gun[seat], b.gun[seat], t);
            return true;
        }

        void ApplyTanks()
        {
            var input = mm.playerInput;
            for (int i = 0; i < SeatCount; i++)
            {
                var tank = mm.GetSeatTank(i);
                if (tank == null) continue;
                var st = TankStates[i];

                tank.ApplyNetState(st.Health, st.Reload, st.AbilityCooldown, st.AbilityActive, st.Speed,
                                   st.Kills, st.Deaths, st.Assists,
                                   st.BuffHealth, st.BuffSpeed, st.BuffDamage, st.BuffReload);

                // Yeniden doğma
                if (st.SpawnCount > tank.SpawnCount)
                {
                    tank.PuppetRespawn(st.Pos, Quaternion.Euler(0f, st.Yaw, 0f), st.SpawnCount);
                    snapSeat[i] = true;
                    deadSince[i] = 0f;
                    if (tank == mm.PlayerTank) mm.FocusCamera(tank);
                    continue;
                }

                // Ölüm: normalde RPC ile gelir; gelmezse (geç katılma) sessizce gizle
                if (st.Dead)
                {
                    if (deadSince[i] <= 0f) deadSince[i] = Time.time;
                    if (tank.gameObject.activeSelf && Time.time - deadSince[i] > 0.6f && Time.time - killRpcTime[i] > 0.6f)
                        tank.PuppetDie(null, null, false);
                    continue;
                }
                deadSince[i] = 0f;
                if (!tank.gameObject.activeSelf) continue;

                Vector3 pos; float yaw, turret, gun;
                bool mine = tank == mm.PlayerTank;
                if (snapSeat[i])
                {
                    pos = st.Pos; yaw = st.Yaw; turret = st.TurretYaw; gun = st.GunPitch;
                    snapSeat[i] = false;
                }
                else if (mine)
                {
                    // Kendi tankımız: gösterim gecikmesi eklemeden en son duruma yumuşakça yaklaş
                    float k = 1f - Mathf.Exp(-18f * Time.deltaTime);
                    pos = Vector3.Lerp(tank.transform.position, st.Pos, k);
                    if ((pos - st.Pos).sqrMagnitude > 25f) pos = st.Pos;
                    yaw = Mathf.LerpAngle(tank.transform.eulerAngles.y, st.Yaw, k);
                    turret = st.TurretYaw; gun = st.GunPitch;
                }
                else if (!Sample(i, out pos, out yaw, out turret, out gun)) continue;

                tank.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

                // Kendi tankımızın kulesi gecikmesiz döner (tahmin); diğerleri ağdan
                if (mine && input != null && mm.Started && !mm.IsOver)
                    tank.AimVisual(input.CurrentAimPoint(), Time.deltaTime);
                else
                    tank.SetTurretAngles(turret, gun);
            }
        }

        float nextNameCheck;

        /// <summary>Oyuncu çıkıp yerine bot geçince isimler güncellensin.</summary>
        void RefreshNames()
        {
            if (Time.time < nextNameCheck) return;
            nextNameCheck = Time.time + 1f;
            for (int i = 0; i < SeatCount; i++)
            {
                var tank = mm.GetSeatTank(i);
                if (tank == null) continue;
                var s = Seats[i];
                string name = s.Name.ToString();
                if (tank.DisplayName != name) tank.SetControl(name, s.Player == PlayerRef.None, null);
            }
        }

        void ApplyCaptures()
        {
            var cps = mm.capturePoints;
            if (cps == null) return;
            for (int i = 0; i < cps.Length && i < Captures.Length; i++)
            {
                if (cps[i] == null) continue;
                var c = Captures[i];
                cps[i].SetNetState(c.Progress, c.Owner >= 0, c.Owner == 1 ? Team.Red : Team.Blue, c.Contested);
            }
        }

        readonly Dictionary<int, PowerUp> clientPowerUps = new Dictionary<int, PowerUp>();
        readonly HashSet<int> seenIds = new HashSet<int>();
        readonly List<int> removeIds = new List<int>();

        void ApplyPowerUps()
        {
            seenIds.Clear();
            for (int i = 0; i < PowerUps.Length; i++)
            {
                var p = PowerUps[i];
                if (p.Id == 0) continue;
                seenIds.Add(p.Id);
                PowerUp existing;
                if (clientPowerUps.TryGetValue(p.Id, out existing) && existing != null) continue;
                var pu = PowerUpSpawner.Create(p.Pos, (PowerUpType)Mathf.Clamp(p.Type, 0, 3), null);
                pu.netId = p.Id;
                pu.visualOnly = true;
                clientPowerUps[p.Id] = pu;
            }
            removeIds.Clear();
            foreach (var pair in clientPowerUps)
                if (!seenIds.Contains(pair.Key)) removeIds.Add(pair.Key);
            foreach (var id in removeIds)
            {
                var pu = clientPowerUps[id];
                if (pu != null) Destroy(pu.gameObject);
                clientPowerUps.Remove(id);
            }
        }
    }
}
