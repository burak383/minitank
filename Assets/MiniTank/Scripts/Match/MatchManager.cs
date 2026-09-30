using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniTank
{
    /// <summary>
    /// Maçı yönetir: 5v5 tank doğurma, süre, skor, yeniden doğma ve kazanan.
    /// İki mod: Takım Ölüm Maçı (öldürme sayısı) ve Ele Geçirme (nokta tutma puanı).
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Arena")]
        public string arenaName = "Arena";

        [Header("Mod")]
        public MatchMode mode = MatchMode.TeamDeathmatch;
        public float matchDuration = 300f;
        [Tooltip("Takım Ölüm Maçı: bu kadar öldürmeye ulaşan takım kazanır.")]
        public int killLimit = 30;
        [Tooltip("Ele Geçirme: bu puana ulaşan takım kazanır.")]
        public int captureScoreLimit = 200;

        [Header("Takımlar")]
        public int teamSize = 5;
        public Team playerTeam = Team.Blue;
        public Transform[] blueSpawns;
        public Transform[] redSpawns;
        public float respawnDelay = 5f;

        [Header("Tank sınıfları")]
        public TankClassData[] availableClasses;
        public int playerClassIndex;

        [Header("Oyuncu")]
        public PlayerInputSource playerInput;
        public ThirdPersonCamera playerCamera;

        [Header("Ele geçirme noktaları")]
        public CapturePoint[] capturePoints;

        [Header("Maç sonu")]
        public float returnToMenuDelay = 6f;
        public string menuSceneName = "MainMenu";

        public MatchMode Mode => mode;
        public float TimeLeft { get; private set; }
        public bool IsOver { get; private set; }
        public bool IsDraw { get; private set; }
        public Team Winner { get; private set; }
        public TankController PlayerTank { get; private set; }
        public float PlayerRespawnAt { get; private set; }
        public IReadOnlyList<TankController> Tanks => tanks;

        /// <summary>Takım skoru. Son Tank modunda hayatta kalan tank sayısıdır.</summary>
        public int GetScore(Team team) => mode == MatchMode.Elimination ? AliveCount(team) : Mathf.FloorToInt(scores[(int)team]);

        public int AliveCount(Team team)
        {
            int n = 0;
            foreach (var t in tanks) if (t != null && t.Team == team && !t.IsDead) n++;
            return n;
        }

        float TeamHealth(Team team)
        {
            float h = 0f;
            foreach (var t in tanks) if (t != null && t.Team == team && !t.IsDead) h += t.Health;
            return h;
        }

        /// <summary>Son Tank modunda oyuncu öldüyse izlenen takım arkadaşı.</summary>
        public TankController SpectateTarget { get; private set; }

        public event Action<TankController, TankController> Kill; // ölen, öldüren
        public event Action MatchEnded;
        /// <summary>Sadece oyun mantığını çalıştıran cihazda (çevrimdışı/host) maç bitince.</summary>
        public event Action MatchEndedHost;

        readonly float[] scores = new float[2];
        readonly List<TankController> tanks = new List<TankController>();

        // ------------------------------------------------------------------ Online
        /// <summary>Online maç mı (Photon Fusion)?</summary>
        public bool IsOnline { get; private set; }
        /// <summary>Online maçta bu cihaz host değilse true: oyun mantığı çalışmaz, durum ağdan gelir.</summary>
        public bool IsOnlineClient { get; private set; }
        /// <summary>Online maçta oyuncular bekleniyorsa false (tanklar hareket edemez, süre işlemez).</summary>
        public bool Started { get; private set; } = true;
        /// <summary>Oyun mantığı bu cihazda mı çalışıyor (çevrimdışı veya host) ve maç başladı mı?</summary>
        public bool IsSimulating => !IsOnlineClient && Started && !IsOver;

        void Awake()
        {
            Instance = this;
            TankController.AnyDamaged += OnAnyDamaged;
            TankController.AnyAbility += OnAnyAbility;
            TankController.AnyPowerUp += OnAnyPowerUp;
            if (MatchSettings.HasSelection)
            {
                mode = MatchSettings.Mode;
                playerClassIndex = MatchSettings.PlayerClassIndex;
            }
        }

        void OnDestroy()
        {
            TankController.AnyDamaged -= OnAnyDamaged;
            TankController.AnyAbility -= OnAnyAbility;
            TankController.AnyPowerUp -= OnAnyPowerUp;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            TimeLeft = matchDuration;

            bool capture = mode == MatchMode.Capture;
            if (capturePoints != null)
                foreach (var cp in capturePoints)
                    if (cp != null) cp.gameObject.SetActive(capture);

            if (MatchSettings.Online)
            {
                // Tankları ağ oturumu kurar (OnlineSession → NetMatch)
                IsOnline = true;
                Started = false;
                CapturePoint.NetDriven = false;
                gameObject.AddComponent<OnlineSession>();
                return;
            }
            CapturePoint.NetDriven = false;

            if (MatchSettings.Tutorial)
            {
                SetupTutorial();
                return;
            }

            SpawnTeam(Team.Blue);
            SpawnTeam(Team.Red);

            // Haritaya güçlendirme bırakan sistem
            if (GetComponent<PowerUpSpawner>() == null) gameObject.AddComponent<PowerUpSpawner>();
        }

        void SpawnTeam(Team team)
        {
            for (int i = 0; i < teamSize; i++)
            {
                bool isPlayer = team == playerTeam && i == 0 && playerInput != null;
                TankClassData cls = isPlayer
                    ? availableClasses[Mathf.Clamp(playerClassIndex, 0, availableClasses.Length - 1)]
                    : availableClasses[i % availableClasses.Length]; // botlar sınıfları dengeli dağıtır
                SpawnTank(cls, team, i, isPlayer);
            }
        }

        void SpawnTank(TankClassData cls, Team team, int index, bool isPlayer)
        {
            var tank = CreateTank(cls, team, index, isPlayer, null, null, false);
            if (tank != null) tanks.Add(tank);
        }

        /// <summary>
        /// Tankı oluşturur. Online'da NetMatch çağırır: source verilirse (uzak oyuncu) o kullanılır,
        /// puppet=true ise tank ağdan gelen durumla hareket eden kukladır.
        /// </summary>
        TankController CreateTank(TankClassData cls, Team team, int index, bool isPlayer,
                                  ITankInputSource remoteSource, string nameOverride, bool puppet)
        {
            if (cls == null || cls.prefab == null)
            {
                Debug.LogError($"[MiniTank] '{(cls != null ? cls.name : "null")}' sınıfının prefab'ı yok.");
                return null;
            }

            Transform sp = GetSpawnPoint(team, index);
            GameObject go = Instantiate(cls.prefab, sp.position, sp.rotation);
            var tank = go.GetComponent<TankController>();
            if (puppet) tank.MakePuppet();

            ITankInputSource source;
            string displayName;
            if (remoteSource != null || (puppet && !isPlayer))
            {
                source = remoteSource;
                displayName = nameOverride ?? $"{cls.displayName} {index + 1}";
                var bot = go.GetComponent<BotInputSource>();
                if (bot != null) bot.enabled = false;
            }
            else if (isPlayer)
            {
                playerInput.Bind(tank);
                source = playerInput;
                displayName = nameOverride ?? (PlayerSession.IsSignedIn ? PlayerSession.DisplayName : "Sen");
                PlayerTank = tank;
                playerTeam = team;
                if (playerCamera != null)
                {
                    playerCamera.target = tank.transform;
                    playerCamera.SnapBehind();
                }
                var bot = go.GetComponent<BotInputSource>();
                if (bot != null) bot.enabled = false;
            }
            else
            {
                var bot = go.GetComponent<BotInputSource>();
                if (bot == null) bot = go.AddComponent<BotInputSource>();
                source = bot;
                displayName = nameOverride ?? $"{cls.displayName} {index + 1}";
            }

            go.name = $"{TeamColors.Name(team)} - {displayName}";
            tank.Init(cls, team, displayName, !isPlayer && remoteSource == null, source);
            if (!IsOnline) ApplyMeta(tank, cls, isPlayer);
            tank.Died += OnTankDied;
            if (!Started) tank.InputEnabled = false;
            return tank;
        }

        // ------------------------------------------------------------------ Eğitim

        public bool IsTutorial { get; private set; }

        /// <summary>Oyuncu tek başına; önünde hareketsiz 3 hedef tank. Süre işlemez, lig etkilenmez.</summary>
        void SetupTutorial()
        {
            IsTutorial = true;
            mode = MatchMode.TeamDeathmatch;
            if (capturePoints != null)
                foreach (var cp in capturePoints) if (cp != null) cp.gameObject.SetActive(false);

            var cls = availableClasses[Mathf.Clamp(playerClassIndex, 0, availableClasses.Length - 1)];
            SpawnTank(cls, Team.Blue, 0, playerInput != null);
            var player = PlayerTank;
            if (player == null) return;

            var dummies = new List<TankController>();
            Vector3 fwd = player.transform.forward;
            Vector3 right = player.transform.right;
            float[] dist = { 32f, 44f, 56f };
            float[] side = { -9f, 7f, -2f };
            for (int i = 0; i < 3; i++)
            {
                var dcls = availableClasses[(i + 1) % availableClasses.Length];
                Vector3 pos = player.transform.position + fwd * dist[i] + right * side[i];
                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up * 40f, Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point;
                var go = Instantiate(dcls.prefab, pos, Quaternion.LookRotation(i == 1 ? -right : right)); // yan dönük: yan zırh dersi
                var t = go.GetComponent<TankController>();
                var bot = go.GetComponent<BotInputSource>();
                if (bot != null) bot.enabled = false;
                go.name = "Hedef " + (i + 1);
                t.Init(dcls, Team.Red, "Hedef " + (i + 1), true, null);
                t.InputEnabled = false;
                t.SetHealthFraction(0.5f);
                t.Died += OnTankDied;
                tanks.Add(t);
                dummies.Add(t);
            }

            gameObject.AddComponent<TutorialDirector>().Init(this, dummies);
        }

        // ------------------------------------------------------------------ Online API (NetMatch kullanır)

        /// <summary>Online: bir koltuğun tankını (yeniden) oluşturur. Eski tank varsa yok edilir.</summary>
        public TankController SetSeatTank(int seat, TankClassData cls, Team team, int teamIndex, bool isLocalPlayer,
                                          ITankInputSource remoteSource, string displayName, bool puppet,
                                          int engine, int armor, int gun, string camo)
        {
            while (tanks.Count <= seat) tanks.Add(null);
            var old = tanks[seat];
            if (old != null)
            {
                if (old == PlayerTank) PlayerTank = null;
                old.Died -= OnTankDied;
                Destroy(old.gameObject);
            }
            var tank = CreateTank(cls, team, teamIndex, isLocalPlayer, remoteSource, displayName, puppet);
            tanks[seat] = tank;
            if (tank != null)
            {
                tank.ApplyUpgrades(engine, armor, gun);
                tank.ApplyCamo(camo);
                // Maç sırasında katılan oyuncu güvenli bir noktada doğar
                if (Started && !puppet)
                {
                    Transform sp = GetSafeSpawnPoint(team);
                    tank.transform.SetPositionAndRotation(sp.position, sp.rotation);
                }
            }
            return tank;
        }

        public TankController GetSeatTank(int seat) => seat >= 0 && seat < tanks.Count ? tanks[seat] : null;
        public int SeatOf(TankController tank) => tank != null ? tanks.IndexOf(tank) : -1;
        public int SeatCount => tanks.Count;

        /// <summary>Online: bu cihaz istemci (host değil) olarak ayarlanır.</summary>
        public void SetOnlineRole(bool isClient)
        {
            IsOnlineClient = isClient;
            CapturePoint.NetDriven = isClient;
            // Güçlendirmeleri sadece host bırakır
            if (!isClient && GetComponent<PowerUpSpawner>() == null) gameObject.AddComponent<PowerUpSpawner>();
        }

        /// <summary>Online: bekleme bitti, maç başlıyor.</summary>
        public void BeginOnlineMatch()
        {
            if (Started) return;
            Started = true;
            TimeLeft = matchDuration;
            foreach (var t in tanks) if (t != null) t.InputEnabled = true;
            if (GameAudio.Instance != null) GameAudio.Instance.PlayPromote();
        }

        /// <summary>Online istemci: skor ve süre host'tan gelir.</summary>
        public void ApplyNetScore(float blue, float red, float timeLeft)
        {
            scores[0] = blue; scores[1] = red;
            TimeLeft = timeLeft;
        }

        public float RawScore(Team team) => scores[(int)team];

        /// <summary>Online istemci: host maçı bitirdi.</summary>
        public void EndMatchFromNet(Team winner, bool draw)
        {
            if (IsOver) return;
            IsOver = true;
            IsDraw = draw;
            Winner = winner;
            foreach (var t in tanks) if (t != null) t.InputEnabled = false;
            MatchEnded?.Invoke();
            ReportLeagueResult();
            StartCoroutine(ReturnToMenu());
        }

        /// <summary>Online: bağlantı koptu. Lig sonucu yazılmadan menüye dönülür.</summary>
        public void AbortOnline()
        {
            if (forfeiting) return;
            forfeiting = true;
            IsOver = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (Application.CanStreamedLevelBeLoaded(menuSceneName)) SceneManager.LoadScene(menuSceneName);
        }

        /// <summary>Online host: kamerayı ve oyuncu girdisini bu tanka bağlar.</summary>
        public void FocusCamera(TankController tank)
        {
            if (playerCamera == null || tank == null) return;
            playerCamera.target = tank.transform;
            playerCamera.SnapBehind();
        }

        /// <summary>
        /// Garaj geliştirmeleri ve kamuflaj. Botlar, dengeli olsun diye oyuncunun ortalama
        /// geliştirme seviyesine yakın (±1) seviyeler alır.
        /// </summary>
        void ApplyMeta(TankController tank, TankClassData cls, bool isPlayer)
        {
            if (!ProgressService.IsLoaded) return;
            if (isPlayer)
            {
                var u = ProgressService.GetUpgrade(cls.name);
                tank.ApplyUpgrades(u.engine, u.armor, u.gun);
                tank.ApplyCamo(ProgressService.Current.equippedCamo);
                return;
            }
            int total = 0, count = 0;
            foreach (var up in ProgressService.Current.upgrades) { total += up.engine + up.armor + up.gun; count += 3; }
            int avg = count > 0 ? Mathf.RoundToInt((float)total / count) : 0;
            tank.ApplyUpgrades(BotLevel(avg), BotLevel(avg), BotLevel(avg));
        }

        static int BotLevel(int avg) => Mathf.Clamp(avg + UnityEngine.Random.Range(-1, 2), 0, Economy.MaxUpgradeLevel);

        // Oyuncunun maç içi istatistikleri (görevler için)
        int playerDamage, playerAbilities, playerPowerUps;

        void OnAnyDamaged(DamageInfo info)
        {
            if (info.attacker != null && info.attacker == PlayerTank && info.victim != PlayerTank)
                playerDamage += Mathf.RoundToInt(info.amount);
        }

        void OnAnyAbility(TankController t, AbilityType a) { if (t == PlayerTank) playerAbilities++; }
        void OnAnyPowerUp(TankController t, PowerUpType p) { if (t == PlayerTank) playerPowerUps++; }

        Transform GetSpawnPoint(Team team, int index)
        {
            Transform[] list = team == Team.Blue ? blueSpawns : redSpawns;
            if (list == null || list.Length == 0) return transform;
            return list[index % list.Length];
        }

        /// <summary>Yeniden doğarken düşmanlardan en uzak doğma noktasını seçer.</summary>
        Transform GetSafeSpawnPoint(Team team)
        {
            Transform[] list = team == Team.Blue ? blueSpawns : redSpawns;
            if (list == null || list.Length == 0) return transform;

            Transform best = list[0];
            float bestScore = float.MinValue;
            foreach (var sp in list)
            {
                float nearestEnemy = float.MaxValue;
                bool blocked = false;
                foreach (var t in TankController.All)
                {
                    float d = Vector3.Distance(sp.position, t.transform.position);
                    if (d < 4f) blocked = true;
                    if (t.Team != team) nearestEnemy = Mathf.Min(nearestEnemy, d);
                }
                float score = (blocked ? -1000f : 0f) + Mathf.Min(nearestEnemy, 200f) + UnityEngine.Random.value * 5f;
                if (score > bestScore) { bestScore = score; best = sp; }
            }
            return best;
        }

        void OnTankDied(TankController victim, TankController killer)
        {
            if (IsOnlineClient)
            {
                // İstemci: skor ve yeniden doğma host'ta. Burada sadece arayüz olayları.
                Kill?.Invoke(victim, killer);
                if (mode == MatchMode.Elimination)
                {
                    if (victim == PlayerTank || SpectateTarget == victim) SpectateNextAlly();
                }
                else if (victim == PlayerTank) PlayerRespawnAt = Time.time + respawnDelay;
                return;
            }

            victim.Deaths++;
            if (killer != null && killer != victim) killer.Kills++;

            if (IsTutorial)
            {
                Kill?.Invoke(victim, killer);
                // Eğitimde hedefler yeniden doğmaz, oyuncu doğar
                if (victim == PlayerTank)
                {
                    PlayerRespawnAt = Time.time + respawnDelay;
                    StartCoroutine(RespawnAfterDelay(victim));
                }
                return;
            }
            foreach (var assister in victim.LastAssisters) assister.Assists++;

            Kill?.Invoke(victim, killer);
            if (IsOver) return;

            if (mode == MatchMode.TeamDeathmatch && killer != null && killer.Team != victim.Team)
            {
                scores[(int)killer.Team] += 1f;
                if (GetScore(killer.Team) >= killLimit) EndMatch();
            }

            if (mode == MatchMode.Elimination)
            {
                // Tek can: yeniden doğma yok. Bir takım tamamen yok olunca maç biter.
                if (victim == PlayerTank) SpectateNextAlly();
                else if (SpectateTarget == victim) SpectateNextAlly();
                if (AliveCount(Team.Blue) == 0 || AliveCount(Team.Red) == 0) EndMatch();
                return;
            }

            if (victim == PlayerTank) PlayerRespawnAt = Time.time + respawnDelay;
            StartCoroutine(RespawnAfterDelay(victim));
        }

        void SpectateNextAlly()
        {
            SpectateTarget = null;
            if (PlayerTank == null || playerCamera == null) return;
            foreach (var t in tanks)
            {
                if (t != null && t != PlayerTank && t.Team == PlayerTank.Team && !t.IsDead)
                {
                    SpectateTarget = t;
                    playerCamera.target = t.transform;
                    playerCamera.SnapBehind();
                    return;
                }
            }
        }

        IEnumerator RespawnAfterDelay(TankController tank)
        {
            yield return new WaitForSeconds(respawnDelay);
            if (IsOver || tank == null) yield break;

            Transform sp = GetSafeSpawnPoint(tank.Team);
            tank.Respawn(sp.position, sp.rotation);
            if (tank == PlayerTank && playerCamera != null) playerCamera.SnapBehind();
        }

        public void AddCaptureScore(Team team, float amount)
        {
            if (IsOver || mode != MatchMode.Capture) return;
            scores[(int)team] += amount;
            if (GetScore(team) >= captureScoreLimit) EndMatch();
        }

        void Update()
        {
            if (IsOver || !Started || IsOnlineClient || IsTutorial) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                EndMatch();
            }
        }

        void EndMatch()
        {
            if (IsOver) return;
            IsOver = true;

            float blue = scores[(int)Team.Blue];
            float red = scores[(int)Team.Red];
            if (mode == MatchMode.Elimination)
            {
                // Önce hayatta kalan sayısı, eşitse toplam can
                blue = AliveCount(Team.Blue) * 100000f + TeamHealth(Team.Blue);
                red = AliveCount(Team.Red) * 100000f + TeamHealth(Team.Red);
            }
            IsDraw = Mathf.FloorToInt(blue) == Mathf.FloorToInt(red);
            Winner = blue > red ? Team.Blue : Team.Red;

            foreach (var t in tanks)
                if (t != null) t.InputEnabled = false;

            MatchEnded?.Invoke();
            if (MatchEndedHost != null) MatchEndedHost();
            ReportLeagueResult();
            StartCoroutine(ReturnToMenu());
        }

        bool forfeiting;

        /// <summary>Maçtan çık: oyuncu yenilmiş sayılır, ana menüye dönülür.</summary>
        public async void Forfeit()
        {
            if (forfeiting) return;
            forfeiting = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            var player = PlayerTank;
            MatchSettings.Tutorial = false;
            if (!IsOver && !IsTutorial && player != null && PlayerSession.IsSignedIn)
            {
                try
                {
                    await ProgressService.ReportMatchAsync(new MatchOutcome
                    {
                        won = false, draw = false, mvp = false,
                        kills = player.Kills, assists = player.Assists, deaths = player.Deaths,
                        damage = playerDamage, abilitiesUsed = playerAbilities, powerUps = playerPowerUps,
                    });
                }
                catch (Exception e) { Debug.LogWarning("[MiniTank] Çıkış sonucu kaydedilemedi: " + e.Message); }
            }
            IsOver = true;
            OnlineSession.ShutdownCurrent();
            if (Application.CanStreamedLevelBeLoaded(menuSceneName)) SceneManager.LoadScene(menuSceneName);
        }

        /// <summary>Oyuncunun maç sonucu ve lig kupası değişimi (arayüzde gösterilir).</summary>
        public LeagueChange PlayerLeagueChange { get; private set; }

        async void ReportLeagueResult()
        {
            var player = PlayerTank;
            if (player == null || !PlayerSession.IsSignedIn) return;

            // MVP: takımında en çok öldürme (eşitlikte asist) yapan
            bool mvp = player.Kills > 0 && !tanks.Any(t => t != null && t != player && t.Team == player.Team &&
                                                             (t.Kills > player.Kills || (t.Kills == player.Kills && t.Assists > player.Assists)));
            var outcome = new MatchOutcome
            {
                draw = IsDraw,
                won = !IsDraw && Winner == player.Team,
                mvp = mvp,
                kills = player.Kills,
                assists = player.Assists,
                deaths = player.Deaths,
                damage = playerDamage,
                abilitiesUsed = playerAbilities,
                powerUps = playerPowerUps,
            };
            try
            {
                PlayerLeagueChange = await ProgressService.ReportMatchAsync(outcome);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MiniTank] Lig sonucu kaydedilemedi: " + e.Message);
            }
        }

        IEnumerator ReturnToMenu()
        {
            float endedAt = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(Mathf.Max(returnToMenuDelay, 9f)); // lig sonucunu okumaya zaman tanı
            // Oyuncu 2x altın reklamını izliyorsa bitmesini bekle
            while (Ads.Busy) yield return null;
            OnlineSession.ShutdownCurrent();
            // Birkaç maçta bir geçiş reklamı (ödüllü reklam izlendiyse gösterilmez)
            if (Ads.ShouldShowInterstitial(Ads.LastRewardedAt >= endedAt - 1f))
            {
                var t = Ads.Service.ShowInterstitialAsync();
                float timeout = Time.realtimeSinceStartup + 90f;
                while (!t.IsCompleted && Time.realtimeSinceStartup < timeout) yield return null;
            }
            if (Application.CanStreamedLevelBeLoaded(menuSceneName))
                SceneManager.LoadScene(menuSceneName);
        }

        /// <summary>Hedefi olmayan botların gideceği yer.</summary>
        public Vector3 GetBotObjective(TankController bot)
        {
            if (mode == MatchMode.Capture && capturePoints != null && capturePoints.Length > 0)
            {
                // Takımına ait olmayan en yakın noktaya git; hepsi bizimse rastgele birini koru
                CapturePoint best = null;
                float bestDist = float.MaxValue;
                foreach (var cp in capturePoints)
                {
                    if (cp == null || !cp.isActiveAndEnabled) continue;
                    if (cp.HasOwner && cp.Owner == bot.Team && !cp.Contested) continue;
                    float d = Vector3.Distance(bot.transform.position, cp.transform.position);
                    // Botlar hep aynı noktaya yığılmasın
                    d += Mathf.Abs(bot.GetHashCode() % 7) * 6f;
                    if (d < bestDist) { bestDist = d; best = cp; }
                }
                if (best == null) best = capturePoints[Mathf.Abs(bot.GetHashCode()) % capturePoints.Length];
                if (best != null) return best.transform.position;
            }

            // Ölüm maçı: en yakın düşmana, yoksa karşı doğma bölgesine
            TankController nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var t in TankController.All)
            {
                if (t.Team == bot.Team || t.IsDead) continue;
                float d = Vector3.Distance(bot.transform.position, t.transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = t; }
            }
            if (nearest != null) return nearest.transform.position;

            Transform enemySpawn = GetSpawnPoint(bot.Team == Team.Blue ? Team.Red : Team.Blue, 0);
            return enemySpawn.position;
        }
    }
}
