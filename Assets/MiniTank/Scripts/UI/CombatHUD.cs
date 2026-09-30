using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiniTank
{
    /// <summary>
    /// Savaş geri bildirimleri: hasar sayıları, isabet işareti, vurulma yönü oku,
    /// düşük can uyarısı, FPS sayacı, sol el düzeni ve duraklatma menüsü.
    /// MatchHUD tarafından otomatik eklenir.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        class FloatingNumber
        {
            public Vector3 world;
            public string text;
            public Color color;
            public float born;
            public float size;
        }

        class HitDirection
        {
            public Vector3 source;
            public float born;
        }

        readonly List<FloatingNumber> numbers = new List<FloatingNumber>();
        readonly List<HitDirection> directions = new List<HitDirection>();
        float hitMarkerUntil;
        bool hitMarkerKill;
        float fps = 60f;

        MatchManager match;
        Camera cam;
        Texture2D white, vignette;
        GUIStyle numberStyle, fpsStyle, pauseTitle, pauseButton;

        bool paused;
        bool showSettings;

        RectTransform abilityRect;
        Image abilityFill;
        Image abilityBackground;
        GUIStyle buffStyle;

        void Start()
        {
            match = MatchManager.Instance;
            cam = Camera.main;
            white = Texture2D.whiteTexture;
            vignette = MakeVignette();
            TankController.AnyDamaged += OnDamaged;
            ApplyHandedness();
            GameSettings.Changed += ApplyHandedness;
        }

        void OnDestroy()
        {
            TankController.AnyDamaged -= OnDamaged;
            GameSettings.Changed -= ApplyHandedness;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        // ------------------------------------------------------------------ Olaylar

        void OnDamaged(DamageInfo info)
        {
            if (match == null) return;
            var player = match.PlayerTank;
            if (player == null) return;

            if (info.attacker == player && info.victim != player)
            {
                Color c;
                string tag = "";
                switch (info.zone)
                {
                    case HitZone.Front: c = new Color(0.8f, 0.85f, 0.9f); tag = " (zırh)"; break;
                    case HitZone.Rear: c = new Color(1f, 0.45f, 0.2f); tag = "  ARKADAN!"; break;
                    case HitZone.Side: c = new Color(1f, 0.85f, 0.3f); break;
                    default: c = new Color(1f, 0.65f, 0.35f); break;
                }
                numbers.Add(new FloatingNumber
                {
                    world = info.point + Vector3.up * 1.5f,
                    text = $"-{Mathf.RoundToInt(info.amount)}{tag}",
                    color = c,
                    born = Time.time,
                    size = info.killed ? 44f : info.zone == HitZone.Rear ? 38f : 32f,
                });
                hitMarkerUntil = Time.time + (info.killed ? 0.5f : 0.25f);
                hitMarkerKill = info.killed;
            }

            if (info.victim == player && info.attacker != null)
                directions.Add(new HitDirection { source = info.attacker.transform.position, born = Time.time });
        }

        void Update()
        {
            UpdateAbilityButton();
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            numbers.RemoveAll(n => Time.time - n.born > 1.3f);
            directions.RemoveAll(d => Time.time - d.born > 1.6f);
        }

        // ------------------------------------------------------------------ Yetenek butonu

        void UpdateAbilityButton()
        {
            var player = match != null ? match.PlayerTank : null;
            if (player == null || player.ClassData == null || player.ClassData.ability == AbilityType.None) return;

            if (abilityRect == null)
            {
                var fire = FindAnyObjectByType<FireButton>();
                if (fire == null) return;
                // Ateş butonunu kopyalayıp yetenek butonuna çevir
                var go = Instantiate(fire.gameObject, fire.transform.parent);
                go.name = "Yetenek";
                Destroy(go.GetComponent<FireButton>());
                go.AddComponent<AbilityButton>();
                abilityRect = (RectTransform)go.transform;
                abilityRect.sizeDelta = ((RectTransform)fire.transform).sizeDelta * 0.68f;
                abilityBackground = go.GetComponent<Image>();
                abilityBackground.color = new Color(0.25f, 0.55f, 0.95f, 0.8f);
                var fillTr = go.transform.Find("Dolum");
                if (fillTr != null) abilityFill = fillTr.GetComponent<Image>();
                var label = go.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = player.ClassData.abilityName.ToUpper();
                    label.fontSize = 24;
                }
                ApplyHandedness();
            }

            float ready = player.AbilityReady01;
            if (abilityFill != null) abilityFill.fillAmount = ready;
            if (abilityBackground != null)
            {
                Color c = player.AbilityActive ? new Color(1f, 0.8f, 0.25f, 0.9f)
                        : ready >= 1f ? new Color(0.25f, 0.55f, 0.95f, 0.85f)
                        : new Color(0.25f, 0.3f, 0.4f, 0.7f);
                abilityBackground.color = c;
            }
        }

        /// <summary>Sol el düzeni: joystick sağa, ateş butonu sola.</summary>
        void ApplyHandedness()
        {
            var joystick = FindAnyObjectByType<VirtualJoystick>();
            var fire = FindAnyObjectByType<FireButton>();
            if (joystick == null || fire == null) return;
            PlaceSide((RectTransform)joystick.transform, GameSettings.LeftHanded, 250f);
            PlaceSide((RectTransform)fire.transform, !GameSettings.LeftHanded, 240f);
            if (abilityRect != null)
            {
                bool right = !GameSettings.LeftHanded;
                abilityRect.anchorMin = abilityRect.anchorMax = new Vector2(right ? 1f : 0f, 0f);
                abilityRect.anchoredPosition = new Vector2(right ? -470f : 470f, 170f);
            }
        }

        static void PlaceSide(RectTransform rt, bool right, float offset)
        {
            float y = rt.anchoredPosition.y;
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            rt.anchoredPosition = new Vector2(right ? -offset : offset, y);
        }

        // ------------------------------------------------------------------ Çizim

        void OnGUI()
        {
            GUI.skin = UITheme.Skin;
            if (match == null) return;
            float scale = Screen.height / 1080f;
            GUI.depth = -5;

            DrawLowHealth();
            DrawNumbers(scale);
            DrawHitMarker(scale);
            DrawDirections(scale);

            if (GameSettings.ShowFps)
            {
                if (fpsStyle == null) fpsStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                fpsStyle.fontSize = Mathf.RoundToInt(20 * scale);
                GUI.Label(new Rect(20 * scale, 430 * scale, 200 * scale, 30 * scale), $"{Mathf.RoundToInt(fps)} FPS", fpsStyle);
            }

            DrawBuffs(scale);
            DrawPause(scale);
        }

        /// <summary>Aktif güçlendirmeler (can barının üstünde).</summary>
        void DrawBuffs(float scale)
        {
            var p = match.PlayerTank;
            if (p == null || p.IsDead) return;
            if (buffStyle == null) buffStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
            buffStyle.fontSize = Mathf.RoundToInt(24 * scale);

            var sb = new System.Text.StringBuilder();
            for (int i = 1; i < 4; i++)
            {
                var type = (PowerUpType)i;
                float left = p.BuffRemaining(type);
                if (left <= 0f) continue;
                string hex = ColorUtility.ToHtmlStringRGB(PowerUp.ColorOf(type));
                sb.Append($"<color=#{hex}>{PowerUp.Name(type)} {Mathf.CeilToInt(left)}s</color>    ");
            }
            if (p.IsShielded) sb.Append("<color=#7FB8FF>KALKAN</color>    ");
            if (p.IsBoosting) sb.Append("<color=#7FE8FF>NİTRO</color>    ");
            if (sb.Length == 0) return;
            GUI.Label(new Rect(0, Screen.height - 175 * scale, Screen.width, 40 * scale), sb.ToString(), buffStyle);
        }

        void DrawNumbers(float scale)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            if (numberStyle == null)
                numberStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };

            foreach (var n in numbers)
            {
                float age = Time.time - n.born;
                Vector3 sp = cam.WorldToScreenPoint(n.world + Vector3.up * age * 2f);
                if (sp.z <= 0f) continue;
                float alpha = Mathf.Clamp01(1.3f - age) ;
                numberStyle.fontSize = Mathf.RoundToInt(n.size * scale * (1f + Mathf.Max(0f, 0.25f - age)));
                var rect = new Rect(sp.x - 200 * scale, Screen.height - sp.y - 30 * scale, 400 * scale, 60 * scale);
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.7f);
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), n.text, numberStyle);
                GUI.color = new Color(n.color.r, n.color.g, n.color.b, alpha);
                GUI.Label(rect, n.text, numberStyle);
            }
            GUI.color = Color.white;
        }

        void DrawHitMarker(float scale)
        {
            if (Time.time > hitMarkerUntil) return;
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float len = 14f * scale, gap = 10f * scale, thick = 3f * scale;
            GUI.color = hitMarkerKill ? new Color(1f, 0.3f, 0.25f) : Color.white;
            for (int i = 0; i < 4; i++)
            {
                var old = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f + 90f * i, c);
                GUI.DrawTexture(new Rect(c.x - thick / 2f, c.y - gap - len, thick, len), white);
                GUI.matrix = old;
            }
            GUI.color = Color.white;
        }

        /// <summary>Vurulduğun yönü gösteren kırmızı oklar (ekran ortası etrafında).</summary>
        void DrawDirections(float scale)
        {
            if (cam == null || directions.Count == 0) return;
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            Vector3 origin = match.PlayerTank != null ? match.PlayerTank.transform.position : cam.transform.position;
            foreach (var d in directions)
            {
                Vector3 to = d.source - origin; to.y = 0f;
                if (to.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.01f) continue;
                float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
                float alpha = Mathf.Clamp01(1.6f - (Time.time - d.born));
                var old = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, c);
                GUI.color = new Color(1f, 0.15f, 0.1f, alpha * 0.85f);
                float w = 120f * scale, h = 14f * scale, radius = 200f * scale;
                GUI.DrawTexture(new Rect(c.x - w / 2f, c.y - radius, w, h), white);
                GUI.DrawTexture(new Rect(c.x - w * 0.25f, c.y - radius - h, w * 0.5f, h), white);
                GUI.matrix = old;
            }
            GUI.color = Color.white;
        }

        void DrawLowHealth()
        {
            var p = match.PlayerTank;
            if (p == null || p.IsDead) return;
            float hp = p.Health / p.MaxHealth;
            float recentHit = directions.Count > 0 ? Mathf.Clamp01(0.6f - (Time.time - directions[directions.Count - 1].born)) : 0f;
            float strength = Mathf.Max(hp < 0.3f ? (0.35f + 0.25f * Mathf.Sin(Time.time * 6f)) * (1f - hp / 0.3f * 0.5f) : 0f, recentHit * 0.6f);
            if (strength <= 0.01f) return;
            GUI.color = new Color(0.9f, 0.05f, 0.05f, strength);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill);
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------ Duraklatma

        void DrawPause(float scale)
        {
            if (pauseButton == null)
            {
                pauseButton = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                pauseTitle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 64 };
            }
            pauseButton.fontSize = Mathf.RoundToInt(30 * scale);

            if (!paused)
            {
                if (!match.IsOver && GUI.Button(new Rect(20 * scale, 20 * scale, 80 * scale, 72 * scale), "II", pauseButton))
                    SetPaused(true);
                return;
            }

            // 1920x1080 sanal ekrana göre ölçekli menü
            var oldMatrix = GUI.matrix;
            float s = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920f * s) / 2f, (Screen.height - 1080f * s) / 2f, 0f), Quaternion.identity, new Vector3(s, s, 1f));
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(-2000, -2000, 6000, 6000), white);
            GUI.color = Color.white;

            if (showSettings)
            {
                if (SettingsPanel.Draw(new Rect(460, 140, 1000, 800))) showSettings = false;
            }
            else
            {
                var big = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(0, 230, 1920, 100), match.IsOnline ? "MENÜ (maç devam ediyor)" : "DURAKLATILDI", pauseTitle);
                if (GUI.Button(new Rect(710, 380, 500, 100), "Devam et", big)) SetPaused(false);
                if (GUI.Button(new Rect(710, 500, 500, 100), "Ayarlar", big)) { showSettings = true; Click(); }
                if (GUI.Button(new Rect(710, 620, 500, 100), "Maçtan çık", big)) { Click(); match.Forfeit(); }
                GUI.Label(new Rect(0, 740, 1920, 50), "<size=24>Maçtan çıkarsan yenilmiş sayılırsın.</size>",
                          new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });
            }
            GUI.matrix = oldMatrix;
        }

        void SetPaused(bool value)
        {
            paused = value;
            showSettings = false;
            // Çevrimdışı maçta oyun durur; online'da sadece menü açılacak
            bool online = match != null && match.IsOnline;
            Time.timeScale = value && !online ? 0f : 1f;
            AudioListener.pause = value && !online;
            Click();
        }

        static void Click()
        {
            if (GameAudio.Instance != null) GameAudio.Instance.PlayClick();
        }

        static Texture2D MakeVignette()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (n - 1f)) * 2f - 1f, dy = (y / (n - 1f)) * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
