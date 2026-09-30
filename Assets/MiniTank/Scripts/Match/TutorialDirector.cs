using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniTank
{
    /// <summary>
    /// İlk giriş eğitimi: oyuncu tek başına, karşısında hareketsiz hedef tanklar var.
    /// Adım adım: sür → nişan al → ateş et → imha et → yetenek → güçlendirme → kalan hedefler.
    /// İlgili kontrolün etrafında yanıp sönen halka gösterilir. Bitince +200 altın.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public const int RewardGold = 200;
        const string RewardKey = "mt_tutorial_rewarded";
        bool rewardGiven;

        enum Step { Drive, Aim, Shoot, Destroy, Ability, PowerUp, Finish, Done }

        MatchManager match;
        Step step = Step.Drive;
        float stepStartedAt;
        Vector3 startPos;
        float lastTurretYaw, aimed;
        bool hitEnemy, usedAbility, pickedPowerUp;
        int killsAtStepStart;
        PowerUp spawnedPowerUp;
        readonly List<TankController> targets = new List<TankController>();
        float finishedAt = -1f;
        bool rewarded;
        GUIStyle titleStyle, bodyStyle, smallStyle;

        public void Init(MatchManager m, List<TankController> dummies)
        {
            match = m;
            targets.AddRange(dummies);
        }

        void Start()
        {
            TankController.AnyDamaged += OnDamaged;
            TankController.AnyAbility += OnAbility;
            TankController.AnyPowerUp += OnPowerUp;
            BeginStep(Step.Drive);
        }

        void OnDestroy()
        {
            TankController.AnyDamaged -= OnDamaged;
            TankController.AnyAbility -= OnAbility;
            TankController.AnyPowerUp -= OnPowerUp;
        }

        TankController Player => match != null ? match.PlayerTank : null;

        void OnDamaged(DamageInfo info) { if (info.attacker != null && info.attacker == Player && info.victim != Player) hitEnemy = true; }
        void OnAbility(TankController t, AbilityType a) { if (t == Player) usedAbility = true; }
        void OnPowerUp(TankController t, PowerUpType p) { if (t == Player) pickedPowerUp = true; }

        void BeginStep(Step s)
        {
            step = s;
            stepStartedAt = Time.time;
            var p = Player;
            if (p != null)
            {
                startPos = p.transform.position;
                lastTurretYaw = p.TurretYaw;
                killsAtStepStart = p.Kills;
            }
            aimed = 0f;
            hitEnemy = usedAbility = pickedPowerUp = false;

            if (s == Step.Ability && (p == null || p.ClassData == null || p.ClassData.ability == AbilityType.None))
            {
                BeginStep(Step.PowerUp); // yeteneği olmayan sınıf
                return;
            }
            if (s == Step.PowerUp && p != null)
            {
                // Oyuncunun önüne güçlendirme bırak
                Vector3 pos = p.transform.position + p.transform.forward * 14f + Vector3.up * 30f;
                RaycastHit hit;
                if (Physics.Raycast(pos, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point;
                else pos.y = p.transform.position.y;
                spawnedPowerUp = PowerUpSpawner.Create(pos, PowerUpType.Speed, null);
            }
            if (s == Step.Finish && AliveTargets() == 0) { Complete(); return; }
            if (GameAudio.Instance != null && s != Step.Drive) GameAudio.Instance.PlayAssist();
        }

        int AliveTargets()
        {
            int n = 0;
            foreach (var t in targets) if (t != null && !t.IsDead) n++;
            return n;
        }

        void Update()
        {
            if (finishedAt > 0f && Time.time - finishedAt > 8f) { ReturnToMenu(); return; }
            var p = Player;
            if (p == null || step == Step.Done) return;

            switch (step)
            {
                case Step.Drive:
                    if (Vector3.Distance(p.transform.position, startPos) > 8f) BeginStep(Step.Aim);
                    break;
                case Step.Aim:
                    aimed += Mathf.Abs(Mathf.DeltaAngle(lastTurretYaw, p.TurretYaw));
                    lastTurretYaw = p.TurretYaw;
                    if (aimed > 70f) BeginStep(Step.Shoot);
                    break;
                case Step.Shoot:
                    if (hitEnemy) BeginStep(Step.Destroy);
                    break;
                case Step.Destroy:
                    if (p.Kills > killsAtStepStart) BeginStep(Step.Ability);
                    break;
                case Step.Ability:
                    if (usedAbility) BeginStep(Step.PowerUp);
                    break;
                case Step.PowerUp:
                    if (pickedPowerUp || (spawnedPowerUp == null && Time.time - stepStartedAt > 1f)) BeginStep(Step.Finish);
                    break;
                case Step.Finish:
                    if (AliveTargets() == 0) Complete();
                    break;
            }
        }

        async void Complete()
        {
            step = Step.Done;
            finishedAt = Time.time;
            // Ödül sadece ilk tamamlamada (eğitim tekrar oynanabilir ama altın bir kez verilir)
            bool firstTime = PlayerPrefs.GetInt(RewardKey, 0) == 0;
            PlayerPrefs.SetInt(MainMenu.TutorialDoneKey, 1);
            PlayerPrefs.SetInt(RewardKey, 1);
            PlayerPrefs.Save();
            rewardGiven = firstTime;
            if (GameAudio.Instance != null) GameAudio.Instance.PlayPromote();
            if (firstTime && !rewarded && ProgressService.IsLoaded)
            {
                rewarded = true;
                try { await ProgressService.AddGoldAsync(RewardGold); }
                catch (System.Exception e) { Debug.LogWarning("[MiniTank] Eğitim ödülü verilemedi: " + e.Message); }
            }
        }

        void ReturnToMenu()
        {
            finishedAt = -1f;
            MatchSettings.Tutorial = false;
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(match != null ? match.menuSceneName : "MainMenu"))
                SceneManager.LoadScene(match != null ? match.menuSceneName : "MainMenu");
        }

        // ------------------------------------------------------------------ Arayüz

        void OnGUI()
        {
            GUI.skin = UITheme.Skin;
            if (titleStyle == null)
            {
                titleStyle = UITheme.Text(34, TextAnchor.MiddleLeft, true, UITheme.Accent);
                bodyStyle = UITheme.Text(28, TextAnchor.UpperLeft);
                smallStyle = UITheme.Text(20, TextAnchor.MiddleRight, false, UITheme.TextDim);
            }

            float s = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            Vector2 offset = new Vector2((Screen.width - 1920f * s) / 2f, (Screen.height - 1080f * s) / 2f);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1f));

            if (step == Step.Done)
            {
                UITheme.Fill(new Rect(-2000, -2000, 6000, 6000), new Color(0f, 0f, 0f, 0.55f));
                var r = new Rect(560, 330, 800, 380);
                UITheme.DrawPanel(r, true);
                GUI.Label(new Rect(r.x, r.y + 40, r.width, 80), "EĞİTİM TAMAMLANDI!", UITheme.Text(52, TextAnchor.MiddleCenter, true, UITheme.Accent));
                GUI.Label(new Rect(r.x + 40, r.y + 130, r.width - 80, 90),
                    rewardGiven ? $"Artık savaşa hazırsın. Ödülün: <color=#FFD24A><b>+{RewardGold} altın</b></color>" : "Artık savaşa hazırsın!",
                    UITheme.Text(30, TextAnchor.MiddleCenter));
                if (UITheme.Button(1200, new Rect(r.center.x - 200, r.yMax - 120, 400, 90), "Ana menüye dön", false, 32, true)) ReturnToMenu();
                return;
            }

            int index = (int)step + 1, total = 7;
            string title, body;
            Describe(step, out title, out body);

            // Görev kartı (üst orta), girişte aşağı kayar
            float t = UITheme.Ease(Mathf.Clamp01((Time.time - stepStartedAt) / 0.35f));
            var card = UITheme.SlideIn(new Rect(460, 120, 1000, 190), t, 0f, -40f);
            UITheme.DrawPanel(card, true);
            GUI.Label(new Rect(card.x + 30, card.y + 16, 700, 50), title, titleStyle);
            GUI.Label(new Rect(card.x + 700, card.y + 16, 270, 50), $"Adım {Mathf.Min(index, total)}/{total}", smallStyle);
            GUI.Label(new Rect(card.x + 30, card.y + 70, card.width - 60, 110), body, bodyStyle);
            UITheme.Fill(new Rect(card.x + 30, card.yMax - 14, card.width - 60, 6), new Color(1f, 1f, 1f, 0.1f));
            UITheme.Fill(new Rect(card.x + 30, card.yMax - 14, (card.width - 60) * (index - 1) / (float)total, 6), UITheme.Accent);
            UITheme.ResetColor();

            if (UITheme.Button(1201, new Rect(1640, 120, 240, 64), "Eğitimi geç", false, 22))
            {
                PlayerPrefs.SetInt(MainMenu.TutorialDoneKey, 1);
                ReturnToMenu();
            }

            // İlgili kontrolü vurgula
            Rect? target = null;
            switch (step)
            {
                case Step.Drive: target = ControlRect<VirtualJoystick>(offset, s); break;
                case Step.Aim: target = ControlRect<TouchLookArea>(offset, s, 0.35f); break;
                case Step.Shoot:
                case Step.Destroy:
                case Step.Finish: target = ControlRect<FireButton>(offset, s); break;
                case Step.Ability: target = ControlRect<AbilityButton>(offset, s); break;
            }
            if (target.HasValue)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                var r = target.Value;
                float grow = 30f + pulse * 25f;
                UITheme.Glow(new Rect(r.x - grow, r.y - grow, r.width + grow * 2f, r.height + grow * 2f), new Color(1f, 0.75f, 0.2f, 0.35f + 0.35f * pulse));
            }
        }

        void Describe(Step s, out string title, out string body)
        {
            bool touch = Application.isMobilePlatform;
            switch (s)
            {
                case Step.Drive:
                    title = "Tankını sür";
                    body = touch ? "Sol alttaki joystick ile ileri git ve dön." : "W/S ile ileri-geri git, A/D ile dön. (Telefonda sol alttaki joystick)";
                    break;
                case Step.Aim:
                    title = "Nişan al";
                    body = touch ? "Ekranın sağ yarısında parmağını kaydırarak kuleyi çevir." : "Sağ tıkı basılı tutup fareyi kaydırarak kuleyi çevir.";
                    break;
                case Step.Shoot:
                    title = "Ateş et";
                    body = (touch ? "Sağ alttaki ateş butonuna bas." : "Space ile ateş et.") + " Karşıdaki kırmızı hedef tanklardan birini vur!";
                    break;
                case Step.Destroy:
                    title = "Hedefi yok et";
                    body = "Önden vurmak az hasar verir. <b>Yandan</b> veya <b>arkadan</b> vurursan zırhı daha kolay delersin. Bir tankı yok et!";
                    break;
                case Step.Ability:
                    var cls = Player != null ? Player.ClassData : null;
                    title = "Yeteneğini kullan: " + (cls != null ? cls.abilityName : "");
                    body = (touch ? "Sağdaki yetenek butonuna bas." : "E tuşuna bas.") + " Her sınıfın farklı bir yeteneği var ve dolması biraz sürer.";
                    break;
                case Step.PowerUp:
                    title = "Güçlendirme topla";
                    body = "Önündeki dönen kutunun üstünden geç. Haritada rastgele çıkan kutular hız, hasar, onarım veya hızlı dolum verir.";
                    break;
                default:
                    title = "Son hedefler";
                    body = $"Kalan {AliveTargets()} hedefi de yok et. Gerçek maçta 5'e 5 savaşacaksın!";
                    break;
            }
        }

        /// <summary>uGUI kontrolünün ekran dikdörtgenini 1920x1080 sanal koordinata çevirir.</summary>
        static Rect? ControlRect<T>(Vector2 offset, float scale, float shrink = 1f) where T : Component
        {
            var c = Object.FindAnyObjectByType<T>();
            if (c == null) return null;
            var rt = c.transform as RectTransform;
            if (rt == null) return null;
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var w in corners)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, w);
                sp.y = Screen.height - sp.y;
                min = Vector2.Min(min, sp);
                max = Vector2.Max(max, sp);
            }
            var r = new Rect((min - offset) / scale, (max - min) / scale);
            if (shrink < 1f) r = UITheme.Scale(r, shrink);
            return r;
        }
    }
}
