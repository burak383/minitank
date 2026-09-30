using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MiniTank
{
    /// <summary>Oyuncu ayarları (cihazda saklanır).</summary>
    public static class GameSettings
    {
        const string Prefix = "mt_set_";

        public static float SfxVolume { get => Get("sfx", 0.8f); set => Set("sfx", value); }
        public static float MusicVolume { get => Get("music", 0.5f); set => Set("music", value); }
        /// <summary>Kamera dönüş hassasiyeti çarpanı (0.3 - 2.5).</summary>
        public static float Sensitivity { get => Get("sens", 1f); set => Set("sens", value); }
        /// <summary>0 = Düşük, 1 = Orta, 2 = Yüksek</summary>
        public static int Quality { get => (int)Get("quality", 2f); set { Set("quality", value); ApplyGraphics(); } }
        /// <summary>Joystick sağda, ateş butonu solda.</summary>
        public static bool LeftHanded { get => Get("left", 0f) > 0.5f; set => Set("left", value ? 1f : 0f); }
        public static bool ShowFps { get => Get("fps", 0f) > 0.5f; set => Set("fps", value ? 1f : 0f); }

        public static event Action Changed;

        public static readonly string[] QualityNames = { "Düşük", "Orta", "Yüksek" };

        static float Get(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);

        static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() => ApplyGraphics();

        /// <summary>
        /// Grafik kalitesini uygular. Render ayarları sadece telefonda/derlenmiş oyunda değiştirilir;
        /// editörde değiştirilirse projedeki ayar dosyası kalıcı olarak bozulurdu.
        /// </summary>
        public static void ApplyGraphics()
        {
            int q = Mathf.Clamp(Quality, 0, 2);
            Application.targetFrameRate = q == 0 ? 30 : 60; // Android varsayılanı 30'dur
#if !UNITY_EDITOR
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = q == 0 ? 0.75f : q == 1 ? 0.9f : 1f;
                urp.msaaSampleCount = q == 0 ? 1 : q == 1 ? 2 : 4;
                urp.shadowDistance = q == 0 ? 0f : q == 1 ? 50f : 90f;
            }
#endif
        }
    }

    /// <summary>Ana menü ve duraklatma menüsünde ortak kullanılan ayarlar paneli (IMGUI).</summary>
    public static class SettingsPanel
    {
        static GUIStyle label, value, header, button, selected;

        static void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleLeft };
            value = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            header = new GUIStyle(label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 26 };
            selected = new GUIStyle(button) { fontStyle = FontStyle.Bold };
            selected.normal.textColor = selected.hover.textColor = new Color(1f, 0.85f, 0.2f);
        }

        /// <summary>Paneli çizer. "Kapat"a basılırsa true döner. Koordinatlar 1920x1080 sanal ekrandadır.</summary>
        public static bool Draw(Rect r)
        {
            Styles();
            var old = GUI.color;
            GUI.color = new Color(0.1f, 0.12f, 0.15f, 0.98f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(r.x, r.y + 20, r.width, 60), "Ayarlar", header);
            float x = r.x + 70, w = r.width - 140, y = r.y + 110, row = 78;

            float sfx = Slider("Ses efektleri", GameSettings.SfxVolume, 0f, 1f, x, y, w, v => $"%{Mathf.RoundToInt(v * 100)}"); y += row;
            if (sfx != GameSettings.SfxVolume) GameSettings.SfxVolume = sfx;
            float music = Slider("Müzik", GameSettings.MusicVolume, 0f, 1f, x, y, w, v => $"%{Mathf.RoundToInt(v * 100)}"); y += row;
            if (music != GameSettings.MusicVolume) GameSettings.MusicVolume = music;
            float sens = Slider("Kamera hassasiyeti", GameSettings.Sensitivity, 0.3f, 2.5f, x, y, w, v => $"{v:0.0}x"); y += row;
            if (sens != GameSettings.Sensitivity) GameSettings.Sensitivity = sens;

            GUI.Label(new Rect(x, y, 360, 60), "Grafik kalitesi", label);
            for (int i = 0; i < 3; i++)
                if (GUI.Button(new Rect(x + 380 + i * 170, y + 5, 160, 55), GameSettings.QualityNames[i], GameSettings.Quality == i ? selected : button))
                    GameSettings.Quality = i;
            y += row;

            bool left = Toggle("Sol el düzeni (joystick sağda)", GameSettings.LeftHanded, x, y, w); y += row;
            if (left != GameSettings.LeftHanded) GameSettings.LeftHanded = left;
            bool fps = Toggle("FPS göster", GameSettings.ShowFps, x, y, w); y += row;
            if (fps != GameSettings.ShowFps) GameSettings.ShowFps = fps;

#if UNITY_EDITOR
            GUI.Label(new Rect(x, y, w, 40), "<size=20>Not: Grafik kalitesi editörde değil, telefonda uygulanır.</size>",
                      new GUIStyle(label) { richText = true, fontSize = 20 });
#endif
            // AB/İngiltere kullanıcıları reklam onayını buradan değiştirebilir (Google zorunluluğu)
            if (Ads.Service.PrivacyOptionsRequired &&
                GUI.Button(new Rect(r.x + 40, r.yMax - 90, 300, 65), "Gizlilik ayarları"))
                Ads.Service.ShowPrivacyOptions();
            bool close = GUI.Button(new Rect(r.center.x - 130, r.yMax - 90, 260, 65), "Kapat", selected);
            if (close && GameAudio.Instance != null) GameAudio.Instance.PlayClick();
            return close;
        }

        static float Slider(string title, float current, float min, float max, float x, float y, float w, Func<float, string> format)
        {
            GUI.Label(new Rect(x, y, 360, 60), title, label);
            float v = GUI.HorizontalSlider(new Rect(x + 380, y + 25, w - 520, 30), current, min, max);
            GUI.Label(new Rect(x + w - 130, y, 130, 60), format(v), value);
            return Mathf.Abs(v - current) > 0.001f ? v : current;
        }

        static bool Toggle(string title, bool current, float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w - 200, 60), title, label);
            if (GUI.Button(new Rect(x + w - 180, y + 5, 180, 55), current ? "Açık" : "Kapalı", current ? selected : button))
            {
                if (GameAudio.Instance != null) GameAudio.Instance.PlayClick();
                return !current;
            }
            return current;
        }
    }
}
