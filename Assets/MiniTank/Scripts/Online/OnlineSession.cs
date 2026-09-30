using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Online maç bağlantısı (Photon Fusion 2, Host modu).
    /// Aynı mod + arenayı seçen oyuncuları aynı odaya toplar; oda yoksa bu cihaz host olur.
    /// Host, maç durumunu tutan NetMatch nesnesini oluşturur. Bekleme ekranını da burası çizer.
    /// </summary>
    public class OnlineSession : MonoBehaviour
    {
        /// <summary>Eşleştirme sürümü: uyumsuz oyun sürümleri aynı odaya düşmesin.</summary>
        public const int ProtocolVersion = 1;
        public const string NetMatchResource = "NetMatch";

        static OnlineSession current;

        NetworkRunner runner;
        string status = "Sunucuya bağlanılıyor…";
        bool failed;
        bool shuttingDownOnPurpose;
        float leaveAt = -1f;
        GUIStyle big, small, btn;
        Texture2D white;

        public static NetworkRunner Runner => current != null ? current.runner : null;

        /// <summary>Kodla açılan özel oda mı?</summary>
        public static bool IsPrivate => !string.IsNullOrEmpty(MatchSettings.RoomCode);

        /// <summary>Maç bitince veya maçtan çıkınca bağlantıyı kapatır.</summary>
        public static void ShutdownCurrent()
        {
            if (current == null) return;
            current.shuttingDownOnPurpose = true;
            if (current.runner != null && current.runner.IsRunning)
                current.runner.Shutdown(true, ShutdownReason.Ok, false);
        }

        void Awake() { current = this; }

        void OnDestroy()
        {
            if (current == this)
            {
                ShutdownCurrent();
                current = null;
            }
        }

        async void Start()
        {
            var go = new GameObject("NetworkRunner");
            runner = go.AddComponent<NetworkRunner>();
            runner.ProvideInput = true;

            var events = go.AddComponent<NetworkEvents>();
            events.OnInput.AddListener(OnInput);
            events.PlayerLeft.AddListener(OnPlayerLeft);
            events.OnShutdown.AddListener(OnShutdown);
            events.OnDisconnectedFromServer.AddListener(OnDisconnected);

            var provider = go.AddComponent<NetworkObjectProviderDefault>();
            var sceneManager = go.AddComponent<NetworkSceneManagerDefault>();

            var props = new Dictionary<string, SessionProperty>
            {
                { "v", (SessionProperty)ProtocolVersion },
                { "m", (SessionProperty)(int)MatchSettings.Mode },
                { "a", (SessionProperty)MatchSettings.ArenaIndex },
            };

            var args = new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                PlayerCount = NetMatch.SeatCount,
                SessionProperties = props,
                MatchmakingMode = Photon.Realtime.MatchmakingMode.FillRoom,
                ObjectProvider = provider,
                SceneManager = sceneManager,
            };
            if (IsPrivate)
            {
                // Özel oda: kodla açılır, rastgele eşleştirmede görünmez
                args.SessionName = "MT" + ProtocolVersion + "_" + MatchSettings.RoomCode;
                args.GameMode = MatchSettings.IsRoomHost ? GameMode.Host : GameMode.Client;
                args.IsVisible = false;
                args.MatchmakingMode = null;
                status = MatchSettings.IsRoomHost ? "Oda kuruluyor…" : "Odaya bağlanılıyor…";
            }

            StartGameResult result;
            try
            {
                result = await runner.StartGame(args);
            }
            catch (System.Exception e)
            {
                Fail("Bağlantı hatası: " + e.Message);
                return;
            }

            if (this == null) return; // sahne değişti
            if (!result.Ok)
            {
                if (IsPrivate && !MatchSettings.IsRoomHost && result.ShutdownReason == ShutdownReason.GameNotFound)
                {
                    Fail("Oda bulunamadı. Kodu kontrol et; oda kapanmış veya maç bitmiş olabilir.");
                    return;
                }
                if (IsPrivate && result.ShutdownReason == ShutdownReason.GameIsFull)
                {
                    Fail("Oda dolu (10/10).");
                    return;
                }
                Fail("Bağlanılamadı: " + result.ShutdownReason + (string.IsNullOrEmpty(result.ErrorMessage) ? "" : " (" + result.ErrorMessage + ")"));
                return;
            }

            if (runner.IsServer)
            {
                var prefab = Resources.Load<NetworkObject>(NetMatchResource);
                if (prefab == null)
                {
                    Fail("NetMatch prefab'ı yok. Unity'de MiniTank → Online Kurulumu menüsünü çalıştır.");
                    return;
                }
                runner.Spawn(prefab, Vector3.zero, Quaternion.identity, PlayerRef.None);
            }
            status = null;
        }

        void Fail(string message)
        {
            failed = true;
            status = message;
            Debug.LogWarning("[MiniTank] Online: " + message);
            leaveAt = Time.time + 6f;
        }

        void Update()
        {
            if (leaveAt > 0f && Time.time >= leaveAt)
            {
                leaveAt = -1f;
                LeaveToMenu();
            }
        }

        void LeaveToMenu()
        {
            shuttingDownOnPurpose = true;
            var mm = MatchManager.Instance;
            if (mm != null) mm.AbortOnline();
        }

        // ------------------------------------------------------------------ Fusion olayları

        void OnInput(NetworkRunner r, NetworkInput input)
        {
            // Host kendi tankını doğrudan sürer; girdi sadece istemcilerden gönderilir
            if (r.IsServer) return;
            var mm = MatchManager.Instance;
            if (mm == null || mm.playerInput == null || mm.PlayerTank == null) return;

            var d = mm.playerInput.GetInput();
            var data = new NetInput
            {
                Move = d.Move,
                Aim = d.AimPoint,
                Buttons = (d.Fire ? NetInput.FireBit : 0) | (d.Ability ? NetInput.AbilityBit : 0),
            };
            input.Set(data);
        }

        void OnPlayerLeft(NetworkRunner r, PlayerRef player)
        {
            if (NetMatch.Instance != null) NetMatch.Instance.OnPlayerLeft(player);
        }

        void OnShutdown(NetworkRunner r, ShutdownReason reason)
        {
            if (shuttingDownOnPurpose) return;
            var mm = MatchManager.Instance;
            if (mm != null && mm.IsOver) return;
            Fail(reason == ShutdownReason.DisconnectedByPluginLogic || reason == ShutdownReason.Ok
                ? "Host oyundan ayrıldı. Maç sona erdi."
                : "Bağlantı koptu: " + reason);
        }

        void OnDisconnected(NetworkRunner r, NetDisconnectReason reason)
        {
            if (shuttingDownOnPurpose) return;
            Fail("Sunucuyla bağlantı koptu.");
        }

        // ------------------------------------------------------------------ Bekleme / durum ekranı

        void OnGUI()
        {
            GUI.skin = UITheme.Skin;
            var net = NetMatch.Instance;
            bool waiting = net != null && net.Object != null && net.Phase == NetMatch.MatchPhase.Waiting;
            if (status == null && !waiting) return;

            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                btn = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold };
                white = Texture2D.whiteTexture;
            }

            var old = GUI.matrix;
            float s = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920f * s) / 2f, (Screen.height - 1080f * s) / 2f, 0f),
                                       Quaternion.identity, new Vector3(s, s, 1f));

            if (status != null)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.DrawTexture(new Rect(-2000, -2000, 6000, 6000), white);
                GUI.color = Color.white;
                GUI.Label(new Rect(160, 380, 1600, 140), failed ? "<color=#FF7A6A>" + status + "</color>" : status, big);
                if (!failed)
                    GUI.Label(new Rect(160, 530, 1600, 60), IsPrivate ? "Oda kodu: <b>" + MatchSettings.RoomCode + "</b>" : "Aynı mod ve arenayı seçen oyuncular aranıyor…", small);
                if (GUI.Button(new Rect(760, 640, 400, 90), failed ? "Menüye dön" : "Vazgeç", btn))
                {
                    ShutdownCurrent();
                    LeaveToMenu();
                }
            }
            else
            {
                // Bekleme: üstte şerit, arka plan görünür (oyuncu arenayı görebilsin)
                bool host = runner != null && runner.IsServer;
                float h = IsPrivate ? 300f : 190f;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(-2000, 150, 6000, h), white);
                GUI.color = Color.white;
                int humans = net.HumanCount;
                int secs = Mathf.CeilToInt(net.WaitRemaining);
                if (IsPrivate)
                {
                    GUI.Label(new Rect(0, 160, 1920, 80), $"Oda kodu:  <color=#FFD24A>{MatchSettings.RoomCode}</color>", big);
                    GUI.Label(new Rect(0, 240, 1920, 60),
                        $"Oyuncular: <b>{humans}/{NetMatch.SeatCount}</b>   Arkadaşların ana menüde <b>Koda katıl</b> ile bu kodu girsin. Boş yerleri botlar doldurur.", small);
                    if (host)
                    {
                        if (GUI.Button(new Rect(760, 320, 400, 100), "MAÇI BAŞLAT", btn)) net.StartNow();
                        GUI.Label(new Rect(0, 420, 1920, 30), $"<size=22>(Başlatmazsan {secs} sn sonra kendiliğinden başlar)</size>", small);
                    }
                    else
                        GUI.Label(new Rect(0, 330, 1920, 60), "Oda sahibinin maçı başlatması bekleniyor…", small);
                }
                else
                {
                    GUI.Label(new Rect(0, 165, 1920, 80), $"Oyuncular bekleniyor  <color=#FFD24A>{humans}/{NetMatch.SeatCount}</color>", big);
                    GUI.Label(new Rect(0, 250, 1920, 60),
                        $"Maç {secs} sn içinde başlıyor. Boş yerleri botlar dolduracak." +
                        (host ? "  <color=#9FD8FF>(Host sensin)</color>" : ""), small);
                }
            }

            GUI.matrix = old;
        }
    }
}
