using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniTank
{
    /// <summary>
    /// Ana menü: arkada dönen tank vitrini, solda mod ve rakip seçimi, ortada tank sınıfı ve özellikleri,
    /// sağda arena kartları ve "Savaşa gir" butonu. Paneller açılışta kayarak gelir, butonlar animasyonludur.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public TankClassData[] classes;
        public string[] arenaScenes = { "Arena1_ColKasabasi", "Arena2_KarliOrman", "Arena3_Liman", "Arena4_Fabrika" };
        public string[] arenaNames = { "Çöl Kasabası", "Karlı Orman", "Liman", "Fabrika" };
        public string loginSceneName = "Login";

        enum Opponents { Bots, Online, Friends }

        MatchMode mode = MatchMode.TeamDeathmatch;
        int classIndex;
        int arenaIndex;
        Opponents opponents;
        string joinCode = "";
        string joinError;
        float openedAt;
        bool showTutorialOffer;

        public const string TutorialDoneKey = "mt_tutorial_done";

        async void Start()
        {
            openedAt = Time.unscaledTime;
            mode = (MatchMode)Mathf.Clamp(PlayerPrefs.GetInt("mt_menu_mode", 0), 0, 2);
            classIndex = PlayerPrefs.GetInt("mt_menu_class", 0);
            arenaIndex = PlayerPrefs.GetInt("mt_menu_arena", 0);
            opponents = MatchSettings.Online ? (string.IsNullOrEmpty(MatchSettings.RoomCode) ? Opponents.Online : Opponents.Friends) : Opponents.Bots;
            if (classes != null && classes.Length > 0) classIndex = Mathf.Clamp(classIndex, 0, classes.Length - 1);
            arenaIndex = Mathf.Clamp(arenaIndex, 0, Mathf.Max(0, arenaScenes.Length - 1));
            MenuShowroom.Ensure();

            // Giriş yapılmadıysa giriş ekranına dön
            try { await AuthManager.Service.InitializeAsync(); }
            catch (System.Exception e) { Debug.LogWarning("[MiniTank] Giriş servisi başlatılamadı: " + e.Message); }
            if (!AuthManager.Service.IsSignedIn) { LoadLogin(); return; }

            try { await ProgressService.LoadAsync(); }
            catch (System.Exception e) { Debug.LogWarning("[MiniTank] İlerleme yüklenemedi: " + e.Message); }

            showTutorialOffer = PlayerPrefs.GetInt(TutorialDoneKey, 0) == 0;
        }

        bool showLeagueInfo;
        bool showSettings;
        MetaScreens meta;
        Texture2D[] arenaThumbs;

        static readonly Color[,] ArenaColors =
        {
            { new Color(0.95f, 0.78f, 0.5f), new Color(0.55f, 0.36f, 0.18f) },   // çöl
            { new Color(0.9f, 0.95f, 1f), new Color(0.25f, 0.4f, 0.35f) },       // karlı orman
            { new Color(0.45f, 0.7f, 0.95f), new Color(0.2f, 0.26f, 0.32f) },    // liman
            { new Color(0.75f, 0.6f, 0.45f), new Color(0.22f, 0.2f, 0.2f) },     // fabrika
        };
        static readonly string[] ArenaHints = { "Açık alan, dar sokaklar", "Sis, ağaçlar, tepeler", "Konteynerler, vinçler", "Kapalı alan, yakın dövüş" };

        void OnGUI()
        {
            GUI.skin = UITheme.Skin;

            // 1920x1080 sanal ekrana göre ölçekle
            float s = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920f * s) / 2f, (Screen.height - 1080f * s) / 2f, 0f),
                                       Quaternion.identity, new Vector3(s, s, 1f));

            UpdateShowroom();

            // Açılır pencereler varken arkadaki butonlar tıklanmasın
            if (showSettings)
            {
                UITheme.Fill(new Rect(-2000, -2000, 6000, 6000), new Color(0f, 0f, 0f, 0.75f));
                if (SettingsPanel.Draw(new Rect(460, 140, 1000, 800))) showSettings = false;
                return;
            }
            if (meta == null) meta = new MetaScreens(classes);
            if (meta.Draw()) return;
            if (showLeagueInfo) { DrawLeagueInfo(); return; }
            if (ProgressService.IsLoaded && ProgressService.Current.seasonNoticePending) { DrawSeasonNotice(); return; }

            HandleShowroomDrag();
            DrawTopBar();
            DrawLeftPanel();
            DrawClassPanel();
            DrawRightPanel();
            DrawLeagueCard();
            UITheme.ResetColor();

            // Geliştirme build'inde reklam durumunu göster (sorun giderme)
            if (Debug.isDebugBuild && !StoreShot.Clean)
                GUI.Label(new Rect(580, 1050, 760, 28), "<color=#8899AA>Reklam: " + Ads.Status +
                          (Ads.Service.RewardedReady ? " ✔" : "") + "</color>", UITheme.Text(18, TextAnchor.MiddleCenter));

            if (showTutorialOffer && ProgressService.IsLoaded) DrawTutorialOffer();
        }

        // ------------------------------------------------------------------ Vitrin

        void UpdateShowroom()
        {
            var room = MenuShowroom.Instance;
            if (room == null || classes == null || classes.Length == 0) return;
            string camo = ProgressService.IsLoaded ? ProgressService.Current.equippedCamo : Economy.DefaultCamo;
            room.Show(classes[Mathf.Clamp(classIndex, 0, classes.Length - 1)], camo);
        }

        void HandleShowroomDrag()
        {
            var e = Event.current;
            var area = new Rect(580, 120, 760, 600);
            if (e.type == EventType.MouseDrag && area.Contains(e.mousePosition) && MenuShowroom.Instance != null)
            {
                MenuShowroom.Instance.Drag(e.delta.x);
                e.Use();
            }
        }

        // ------------------------------------------------------------------ Üst çubuk

        void DrawTopBar()
        {
            float t = UITheme.Intro(openedAt, 0f);
            var bar = UITheme.SlideIn(new Rect(0, 0, 1920, 110), t, 0f, -40f);
            UITheme.Fill(new Rect(bar.x, bar.y, bar.width, bar.height), new Color(0f, 0f, 0f, 0.45f));
            UITheme.Fill(new Rect(bar.x, bar.yMax - 3, bar.width, 3), new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.6f));

            GUI.Label(new Rect(40, bar.y + 10, 700, 90), "MİNİ TANK <color=#FFB82E>5v5</color>", UITheme.Text(58, TextAnchor.MiddleLeft, true));

            if (ProgressService.IsLoaded)
            {
                var chip = new Rect(640, bar.y + 28, 260, 56);
                UITheme.DrawPanel(chip, true);
                GUI.Label(chip, $"<color=#FFD24A>●</color>  <b>{ProgressService.Current.gold}</b> altın", UITheme.Text(28, TextAnchor.MiddleCenter));

                // Günlük ücretsiz altın (ödüllü reklam)
                int left = Ads.DailyRewardsLeft;
                if (left > 0 && Ads.Service.RewardedReady && !rewardBusy)
                {
                    if (UITheme.Button(9010, new Rect(912, bar.y + 28, 200, 56), $"▶ +{Ads.DailyRewardGold} <size=16>({left})</size>", false, 24, true))
                        WatchDailyReward();
                }
            }

            var auth = AuthManager.Service;
            if (auth != null && auth.IsSignedIn)
            {
                string who = auth.IsGuest ? $"{PlayerSession.DisplayName}  <size=18><color=#9AA8B8>misafir</color></size>"
                                          : $"{PlayerSession.DisplayName}  <size=18><color=#9AA8B8>{auth.Email}</color></size>";
                GUI.Label(new Rect(1130, bar.y + 8, 330, 50), who, UITheme.Text(24, TextAnchor.MiddleRight, true));

                float x = 1880f;
                if (UITheme.Button(9001, new Rect(x - 150, bar.y + 22, 150, 64), "Çıkış", false, 24))
                {
                    auth.SignOut();
                    ProgressService.Clear();
                    LoadLogin();
                }
                if (UITheme.Button(9002, new Rect(x - 320, bar.y + 22, 160, 64), "Ayarlar", false, 24)) { showSettings = true; Click(); }
                if (UITheme.Button(9003, new Rect(x - 490, bar.y + 22, 160, 64), "Eğitim", false, 24)) { Click(); StartTutorial(); }
                if (auth.IsGuest && UITheme.Button(9004, new Rect(920 + 540 - 290, bar.y + 58, 290, 44), "Hesabını kaydet", true, 20))
                {
                    LoginScreen.OpenRegisterForLinking = true;
                    LoadLogin();
                }
            }
            UITheme.ResetColor();
        }

        bool rewardBusy;

        async void WatchDailyReward()
        {
            rewardBusy = true;
            Click();
            try
            {
                if (await Ads.Service.ShowRewardedAsync("daily_gold"))
                {
                    Ads.MarkRewarded();
                    Ads.CountDailyReward();
                    if (await ProgressService.AddGoldAsync(Ads.DailyRewardGold) && GameAudio.Instance != null)
                        GameAudio.Instance.PlayPromote();
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[MiniTank] Günlük ödül: " + e.Message); }
            finally { rewardBusy = false; }
        }

        // ------------------------------------------------------------------ Sol panel: mod ve rakipler

        static readonly string[] ModeNames = { "Takım Ölüm Maçı", "Ele Geçirme", "Son Tank" };
        static readonly string[] ModeHints = { "30 imhaya ulaşan takım kazanır", "Noktaları tut, puan topla", "Tek can: son kalan kazanır" };

        void DrawLeftPanel()
        {
            float t = UITheme.Intro(openedAt, 0.08f);
            var r = UITheme.SlideIn(new Rect(40, 130, 520, 560), t, -80f);
            UITheme.DrawPanel(r);

            GUI.Label(new Rect(r.x + 28, r.y + 14, 400, 44), "MOD", UITheme.Text(24, TextAnchor.MiddleLeft, true, UITheme.TextDim));
            for (int i = 0; i < 3; i++)
            {
                var b = new Rect(r.x + 24, r.y + 62 + i * 92, r.width - 48, 82);
                string text = $"{ModeNames[i]}\n<size=18><color=#9AA8B8>{ModeHints[i]}</color></size>";
                if (UITheme.Button(100 + i, b, text, (int)mode == i, 26))
                {
                    mode = (MatchMode)i;
                    PlayerPrefs.SetInt("mt_menu_mode", i);
                    Click();
                }
            }

            float y = r.y + 350;
            GUI.Label(new Rect(r.x + 28, y, 400, 40), "RAKİPLER", UITheme.Text(24, TextAnchor.MiddleLeft, true, UITheme.TextDim));
            string[] names = { "Botlar", "Çevrimiçi", "Arkadaşla" };
            float bw = (r.width - 48 - 20) / 3f;
            for (int i = 0; i < 3; i++)
            {
                if (UITheme.Button(200 + i, new Rect(r.x + 24 + i * (bw + 10), y + 44, bw, 70), names[i], (int)opponents == i, 24))
                {
                    opponents = (Opponents)i;
                    joinError = null;
                    Click();
                }
            }

            string hint;
            switch (opponents)
            {
                case Opponents.Online: hint = "Aynı mod ve arenayı seçenlerle eşleşirsin. 20 sn'de dolmayan yerlere bot gelir."; break;
                case Opponents.Friends: hint = "Oda kur ve kodu arkadaşına gönder, ya da aşağıya arkadaşının kodunu yaz."; break;
                default: hint = "Botlara karşı çevrimdışı maç. İnternet gerekmez."; break;
            }
            GUI.Label(new Rect(r.x + 28, y + 122, r.width - 56, 70), hint, UITheme.Text(19, TextAnchor.UpperLeft, false, UITheme.TextDim));
            UITheme.ResetColor();

            if (opponents == Opponents.Friends) DrawJoinPanel(t);
        }

        void DrawJoinPanel(float t)
        {
            // Mod panelinin altından, lig kartının üzerine açılan küçük katılma paneli
            var r = UITheme.SlideIn(new Rect(40, 700, 520, 150), t, -80f);
            UITheme.DrawPanel(r, true);
            GUI.Label(new Rect(r.x + 24, r.y + 10, 400, 36), "KODA KATIL", UITheme.Text(22, TextAnchor.MiddleLeft, true, UITheme.TextDim));
            var fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            joinCode = GUI.TextField(new Rect(r.x + 24, r.y + 52, 290, 74), joinCode ?? "", 12, fieldStyle).ToUpperInvariant();
            if (UITheme.Button(300, new Rect(r.x + 330, r.y + 52, 166, 74), "Katıl", false, 28)) JoinByCode();
            if (!string.IsNullOrEmpty(joinError))
                GUI.Label(new Rect(r.x + 24, r.yMax + 2, r.width, 30), $"<color=#FF7A6E>{joinError}</color>", UITheme.Text(18));
            UITheme.ResetColor();
        }

        // ------------------------------------------------------------------ Orta: tank sınıfı

        void DrawClassPanel()
        {
            if (classes == null || classes.Length == 0) return;
            var cls = classes[Mathf.Clamp(classIndex, 0, classes.Length - 1)];
            if (cls == null) return;

            float t = UITheme.Intro(openedAt, 0.16f);
            // Tankın adı (vitrinin üstünde)
            var nameRect = UITheme.SlideIn(new Rect(580, 130, 760, 70), t, 0f, -30f);
            GUI.Label(nameRect, cls.displayName.ToUpperInvariant(), UITheme.Text(46, TextAnchor.MiddleCenter, true));
            if (cls.ability != AbilityType.None)
                GUI.Label(new Rect(nameRect.x, nameRect.yMax - 6, nameRect.width, 36),
                          $"<color=#FFB82E>Yetenek:</color> {cls.abilityName}", UITheme.Text(22, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(nameRect.x, nameRect.yMax + 30, nameRect.width, 30), "<color=#6F7D8C>← sürükleyerek çevir →</color>", UITheme.Text(18, TextAnchor.MiddleCenter));

            var r = UITheme.SlideIn(new Rect(580, 730, 760, 320), t, 0f, 60f);
            UITheme.DrawPanel(r);

            // Sınıf seçici
            float bw = (r.width - 48 - 10 * (classes.Length - 1)) / classes.Length;
            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == null) continue;
                if (UITheme.Button(400 + i, new Rect(r.x + 24 + i * (bw + 10), r.y + 20, bw, 64), classes[i].displayName, i == classIndex, 22))
                {
                    classIndex = i;
                    PlayerPrefs.SetInt("mt_menu_class", i);
                    Click();
                }
            }

            // Özellik çubukları (sınıflar arası karşılaştırmalı)
            float maxHp = 1f, maxSpd = 1f, maxDmg = 1f, maxRof = 0.01f;
            foreach (var c in classes)
            {
                if (c == null) continue;
                maxHp = Mathf.Max(maxHp, c.maxHealth);
                maxSpd = Mathf.Max(maxSpd, c.moveSpeed);
                maxDmg = Mathf.Max(maxDmg, c.damage);
                maxRof = Mathf.Max(maxRof, 1f / Mathf.Max(0.1f, c.fireCooldown));
            }
            float sx = r.x + 30, sw = 340, sy = r.y + 102;
            UITheme.StatBar(new Rect(sx, sy, sw, 34), "Dayanıklılık", cls.maxHealth / maxHp, UITheme.Good);
            UITheme.StatBar(new Rect(sx, sy + 40, sw, 34), "Hız", cls.moveSpeed / maxSpd, new Color(0.4f, 0.8f, 1f));
            UITheme.StatBar(new Rect(sx, sy + 80, sw, 34), "Hasar", cls.damage / maxDmg, UITheme.Bad);
            UITheme.StatBar(new Rect(sx, sy + 120, sw, 34), "Atış hızı", (1f / Mathf.Max(0.1f, cls.fireCooldown)) / maxRof, UITheme.Accent);
            GUI.Label(new Rect(r.x + 400, r.y + 98, r.width - 424, 200), cls.description ?? "", UITheme.Text(19, TextAnchor.UpperLeft, false, UITheme.TextDim));
            UITheme.ResetColor();
        }

        // ------------------------------------------------------------------ Sağ panel: arena ve oyna

        void DrawRightPanel()
        {
            if (arenaThumbs == null)
            {
                arenaThumbs = new Texture2D[4];
                for (int i = 0; i < 4; i++) arenaThumbs[i] = UITheme.Gradient(ArenaColors[i, 0], ArenaColors[i, 1]);
            }

            float t = UITheme.Intro(openedAt, 0.1f);
            var r = UITheme.SlideIn(new Rect(1360, 130, 520, 560), t, 80f);
            UITheme.DrawPanel(r);
            GUI.Label(new Rect(r.x + 28, r.y + 14, 400, 44), "ARENA", UITheme.Text(24, TextAnchor.MiddleLeft, true, UITheme.TextDim));

            for (int i = 0; i < arenaScenes.Length; i++)
            {
                var card = new Rect(r.x + 24, r.y + 62 + i * 120, r.width - 48, 108);
                bool sel = i == arenaIndex;
                if (UITheme.Button(500 + i, card, "", sel, 20))
                {
                    arenaIndex = i;
                    PlayerPrefs.SetInt("mt_menu_arena", i);
                    Click();
                }
                // Küçük resim + isim
                var thumb = new Rect(card.x + 12, card.y + 12, 130, card.height - 24);
                if (Event.current.type == EventType.Repaint && i < arenaThumbs.Length) GUI.DrawTexture(thumb, arenaThumbs[i]);
                string name = i < arenaNames.Length ? arenaNames[i] : arenaScenes[i];
                GUI.Label(new Rect(card.x + 160, card.y + 14, card.width - 170, 44), name, UITheme.Text(28, TextAnchor.MiddleLeft, true, sel ? UITheme.Accent : UITheme.Ink));
                GUI.Label(new Rect(card.x + 160, card.y + 56, card.width - 170, 36), i < ArenaHints.Length ? ArenaHints[i] : "", UITheme.Text(19, TextAnchor.MiddleLeft, false, UITheme.TextDim));
            }
            UITheme.ResetColor();

            // Oyna butonu: yavaşça "nefes alan" parıltı
            float tp = UITheme.Intro(openedAt, 0.25f);
            var play = UITheme.SlideIn(new Rect(1360, 710, 520, 120), tp, 80f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            UITheme.Glow(new Rect(play.x - 40, play.y - 30, play.width + 80, play.height + 60), new Color(1f, 0.7f, 0.2f, 0.15f + 0.15f * pulse));
            string playText = opponents == Opponents.Friends ? "ODA KUR" : opponents == Opponents.Online ? "EŞLEŞ" : "SAVAŞA GİR";
            if (UITheme.Button(600, play, playText, false, 44, true)) Play();
            UITheme.ResetColor();

            // Garaj, görevler, sıralama, mağaza
            float tm = UITheme.Intro(openedAt, 0.32f);
            var row = UITheme.SlideIn(new Rect(1360, 850, 520, 190), tm, 80f);
            int claimable = ProgressService.ClaimableMissionCount;
            float w = (row.width - 12) / 2f;
            if (UITheme.Button(700, new Rect(row.x, row.y, w, 86), "Garaj", false, 28)) { Click(); meta.Open(MetaScreens.Screen.Garage); }
            if (UITheme.Button(701, new Rect(row.x + w + 12, row.y, w, 86), claimable > 0 ? $"Görevler <color=#FFB82E>({claimable})</color>" : "Görevler", claimable > 0, 28))
            { Click(); meta.Open(MetaScreens.Screen.Missions); }
            if (UITheme.Button(702, new Rect(row.x, row.y + 98, w, 86), "Sıralama", false, 28)) { Click(); meta.Open(MetaScreens.Screen.Leaderboard); }
            if (UITheme.Button(703, new Rect(row.x + w + 12, row.y + 98, w, 86), "Mağaza", false, 28)) { Click(); meta.Open(MetaScreens.Screen.Store); }
            UITheme.ResetColor();
        }

        // ------------------------------------------------------------------ Eğitim teklifi

        void DrawTutorialOffer()
        {
            UITheme.Fill(new Rect(-2000, -2000, 6000, 6000), new Color(0f, 0f, 0f, 0.7f));
            var r = new Rect(560, 300, 800, 440);
            UITheme.DrawPanel(r, true);
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 70), "Hoş geldin, komutan!", UITheme.Text(46, TextAnchor.MiddleCenter, true));
            GUI.Label(new Rect(r.x + 60, r.y + 110, r.width - 120, 150),
                "Kısa eğitimde tankını sürmeyi, nişan almayı, ateş etmeyi ve yeteneğini kullanmayı öğren. " +
                "2 dakika sürer ve bitirince <color=#FFD24A><b>+200 altın</b></color> kazanırsın.",
                UITheme.Text(26, TextAnchor.MiddleCenter));
            if (UITheme.Button(800, new Rect(r.x + 60, r.yMax - 130, 400, 90), "Eğitime başla", false, 32, true)) { Click(); StartTutorial(); }
            if (UITheme.Button(801, new Rect(r.x + 480, r.yMax - 130, 260, 90), "Atla", false, 28))
            {
                Click();
                showTutorialOffer = false;
                PlayerPrefs.SetInt(TutorialDoneKey, 1);
            }
        }

        // ------------------------------------------------------------------ Lig kartı (kompakt)

        static Texture2D White => Texture2D.whiteTexture;

        static void Fill(Rect r, Color c) => UITheme.Fill(r, c);

        GUIStyle Rich(int size, TextAnchor anchor, bool bold = false) => UITheme.Text(size, anchor, bold);

        void DrawLeagueCard()
        {
            if (opponents == Opponents.Friends) return; // bu alanı katılma paneli kullanıyor
            float t = UITheme.Intro(openedAt, 0.2f);
            var r = UITheme.SlideIn(new Rect(40, 710, 520, 340), t, -80f);
            UITheme.DrawPanel(r);

            if (!ProgressService.IsLoaded)
            {
                GUI.Label(new Rect(r.x + 30, r.y, r.width, r.height), "Lig bilgisi yükleniyor...", Rich(24, TextAnchor.MiddleLeft));
                UITheme.ResetColor();
                return;
            }

            var p = ProgressService.Current;
            var tier = Leagues.Get(p.points);
            var next = Leagues.Next(p.points);

            // Lig rozeti
            var badge = new Rect(r.x + 24, r.y + 24, 150, 150);
            UITheme.Glow(new Rect(badge.x - 30, badge.y - 30, badge.width + 60, badge.height + 60), new Color(tier.color.r, tier.color.g, tier.color.b, 0.35f));
            Fill(badge, tier.color * 0.85f);
            Fill(new Rect(badge.x + 6, badge.y + 6, badge.width - 12, badge.height - 12), new Color(0.08f, 0.09f, 0.11f, 0.95f));
            GUI.Label(new Rect(badge.x, badge.y + 16, badge.width, 50), $"<color=#{Hex(tier.color)}>{tier.name.ToUpper()}</color>", Rich(30, TextAnchor.MiddleCenter, true));
            GUI.Label(new Rect(badge.x, badge.y + 64, badge.width, 50), $"{p.points}", Rich(40, TextAnchor.MiddleCenter, true));
            GUI.Label(new Rect(badge.x, badge.y + 108, badge.width, 30), "kupa", Rich(20, TextAnchor.MiddleCenter));

            float x = badge.xMax + 24, w = r.xMax - x - 24;
            GUI.Label(new Rect(x, r.y + 20, w, 36), $"<b>{Leagues.SeasonName(p.season)} Sezonu</b>", Rich(26, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(x, r.y + 54, w, 30), $"<color=#9AA8B8>bitimine {FormatRemaining(Leagues.TimeUntilSeasonEnd(System.DateTime.UtcNow))}</color>", Rich(20, TextAnchor.MiddleLeft));

            var bar = new Rect(x, r.y + 96, w, 20);
            Fill(bar, new Color(1f, 1f, 1f, 0.1f));
            Fill(new Rect(bar.x, bar.y, bar.width * Leagues.Progress01(p.points), bar.height), tier.color);
            string nextText = next != null ? $"{next.name} ligine {next.minPoints - p.points} kupa" : "En yüksek ligdesin!";
            GUI.Label(new Rect(x, r.y + 120, w, 34), nextText, Rich(20, TextAnchor.MiddleLeft));

            float winRate = p.matches > 0 ? 100f * p.wins / p.matches : 0f;
            string stats = $"{p.matches} maç  •  %{winRate:0} galibiyet" +
                           (p.winStreak >= 2 ? $"\n<color=#FFB347>{p.winStreak} galibiyet serisi</color>" : "") +
                           $"\n<size=18><color=#9AA8B8>{p.kills} imha • {p.assists} asist • {p.deaths} ölüm</color></size>";
            GUI.Label(new Rect(r.x + 24, r.y + 190, r.width - 230, 140), stats, Rich(22, TextAnchor.UpperLeft));

            if (UITheme.Button(900, new Rect(r.xMax - 190, r.yMax - 84, 166, 60), "Ligler", false, 24)) { showLeagueInfo = true; Click(); }
            UITheme.ResetColor();
        }

        void DrawLeagueInfo()
        {
            var screen = new Rect(0, 0, 1920, 1080);
            Fill(screen, new Color(0f, 0f, 0f, 0.75f));
            var r = new Rect(360, 110, 1200, 860);
            UITheme.DrawPanel(r, true);

            GUI.Label(new Rect(r.x, r.y + 20, r.width, 60), "Ligler", Rich(44, TextAnchor.MiddleCenter, true));
            GUI.Label(new Rect(r.x + 60, r.y + 85, r.width - 120, 70),
                "Kazanınca kupa kazanır, kaybedince kupa kaybedersin. Öldürme, asist ve MVP bonus verir; 3+ galibiyet serisi ek kupa kazandırır. " +
                "Her ayın 1'inde sezon biter ve ligine göre kupan düşer.", Rich(22, TextAnchor.UpperCenter));

            float y = r.y + 175;
            float[] cols = { r.x + 80, r.x + 330, r.x + 560, r.x + 740, r.x + 920 };
            string[] heads = { "Lig", "Kupa", "Galibiyet", "Yenilgi", "Sezon sonu" };
            for (int i = 0; i < heads.Length; i++)
                GUI.Label(new Rect(cols[i], y, 240, 40), $"<b>{heads[i]}</b>", Rich(24, TextAnchor.MiddleLeft));
            y += 50;

            int current = ProgressService.IsLoaded ? Leagues.IndexOf(ProgressService.Current.points) : -1;
            for (int i = Leagues.Tiers.Length - 1; i >= 0; i--)
            {
                var t = Leagues.Tiers[i];
                if (i == current) Fill(new Rect(r.x + 50, y - 4, r.width - 100, 72), new Color(1f, 1f, 1f, 0.08f));
                Fill(new Rect(r.x + 55, y + 8, 12, 48), t.color);
                string range = i + 1 < Leagues.Tiers.Length ? $"{t.minPoints} – {Leagues.Tiers[i + 1].minPoints - 1}" : $"{t.minPoints}+";
                GUI.Label(new Rect(cols[0], y, 240, 64), $"<color=#{Hex(t.color)}><b>{t.name}</b></color>{(i == current ? "  <size=18>(sen)</size>" : "")}", Rich(30, TextAnchor.MiddleLeft));
                GUI.Label(new Rect(cols[1], y, 240, 64), range, Rich(26, TextAnchor.MiddleLeft));
                GUI.Label(new Rect(cols[2], y, 180, 64), $"<color=#7CFF8A>+{t.winPoints}</color>", Rich(26, TextAnchor.MiddleLeft));
                GUI.Label(new Rect(cols[3], y, 180, 64), $"<color=#FF7A6E>-{t.lossPoints}</color>", Rich(26, TextAnchor.MiddleLeft));
                string reset = $"{t.seasonResetTo} kupa ({Leagues.Get(t.seasonResetTo).name})";
                GUI.Label(new Rect(cols[4], y, 300, 64), reset, Rich(24, TextAnchor.MiddleLeft));
                y += 80;
            }

            GUI.Label(new Rect(r.x + 60, r.yMax - 130, r.width - 120, 50),
                $"Beraberlik: +{Leagues.DrawPoints}   •   MVP: +{Leagues.MvpBonus}   •   Seri bonusu: +{Leagues.StreakBonus}   •   Performans bonusu: en fazla +{Leagues.MaxPerformanceBonus}",
                Rich(22, TextAnchor.MiddleCenter));
            if (UITheme.Button(910, new Rect(r.center.x - 120, r.yMax - 75, 240, 60), "Kapat", false, 26)) showLeagueInfo = false;
        }

        void DrawSeasonNotice()
        {
            var p = ProgressService.Current;
            Fill(new Rect(0, 0, 1920, 1080), new Color(0f, 0f, 0f, 0.75f));
            var r = new Rect(460, 260, 1000, 520);
            UITheme.DrawPanel(r, true);

            var oldTier = Leagues.Get(p.lastSeasonPoints);
            var newTier = Leagues.Get(p.lastSeasonResetTo);
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 70), $"{Leagues.SeasonName(p.lastSeason)} sezonu bitti!", Rich(44, TextAnchor.MiddleCenter, true));
            string body =
                $"Sezonu <color=#{Hex(oldTier.color)}><b>{oldTier.name}</b></color> liginde <b>{p.lastSeasonPoints}</b> kupa ile bitirdin.\n" +
                $"Sezonun en yüksek kupası: {p.lastSeasonHighPoints}\n\n" +
                $"Yeni sezona <color=#{Hex(newTier.color)}><b>{newTier.name}</b></color> liginde <b>{p.lastSeasonResetTo}</b> kupa ile başlıyorsun.";
            if (p.seasonRewardGold > 0)
                body += $"\n\nSezon ödülün: <color=#FFD24A><b>+{p.seasonRewardGold} altın</b></color>";
            GUI.Label(new Rect(r.x + 60, r.y + 110, r.width - 120, 300), body, Rich(30, TextAnchor.MiddleCenter));
            if (UITheme.Button(911, new Rect(r.center.x - 200, r.yMax - 100, 400, 70), p.seasonRewardGold > 0 ? "Ödülü al ve başla" : "Yeni sezona başla", false, 28, true))
                _ = ProgressService.DismissSeasonNoticeAsync();
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        static string FormatRemaining(System.TimeSpan t)
        {
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays} gün {t.Hours} saat";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours} saat {t.Minutes} dk";
            return $"{Mathf.Max(1, t.Minutes)} dk";
        }

        static void Click()
        {
            if (GameAudio.Instance != null) GameAudio.Instance.PlayClick();
        }

        void LoadLogin()
        {
            if (Application.CanStreamedLevelBeLoaded(loginSceneName)) SceneManager.LoadScene(loginSceneName);
        }

        void Play()
        {
            Click();
            if (opponents == Opponents.Friends)
            {
                MatchSettings.RoomCode = RoomCodes.Create(mode, arenaIndex);
                MatchSettings.IsRoomHost = true;
                LaunchArena(arenaIndex, true);
                return;
            }
            MatchSettings.RoomCode = null;
            MatchSettings.IsRoomHost = false;
            LaunchArena(arenaIndex, opponents == Opponents.Online);
        }

        void JoinByCode()
        {
            Click();
            string code = RoomCodes.Normalize(joinCode);
            MatchMode m;
            int arena;
            if (!RoomCodes.TryParse(code, out m, out arena) || arena >= arenaScenes.Length)
            {
                joinError = $"Geçersiz kod. Kod {RoomCodes.Length} karakterli olmalı (ör. B7KQ4).";
                return;
            }
            mode = m;
            MatchSettings.RoomCode = code;
            MatchSettings.IsRoomHost = false;
            LaunchArena(arena, true);
        }

        void StartTutorial()
        {
            showTutorialOffer = false;
            MatchSettings.Tutorial = true;
            MatchSettings.RoomCode = null;
            MatchSettings.IsRoomHost = false;
            mode = MatchMode.TeamDeathmatch;
            LaunchArena(0, false, true);
        }

        void LaunchArena(int arena, bool isOnline, bool tutorial = false)
        {
            MatchSettings.HasSelection = true;
            MatchSettings.Mode = mode;
            MatchSettings.PlayerClassIndex = classIndex;
            MatchSettings.Online = isOnline;
            MatchSettings.ArenaIndex = arena;
            MatchSettings.Tutorial = tutorial;

            string scene = arenaScenes[Mathf.Clamp(arena, 0, arenaScenes.Length - 1)];
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogError($"[MiniTank] '{scene}' sahnesi Build Settings'te yok.");
        }
    }
}
