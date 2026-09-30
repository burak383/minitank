using System.Collections.Generic;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Oyunun arayüz teması: yuvarlak köşeli paneller ve butonlar (kodla üretilen dokular),
    /// renk paleti ve küçük animasyon yardımcıları (üzerine gelince büyüme, basınca küçülme,
    /// ekrana kayarak giriş). GUI.skin'e uygulanınca tüm IMGUI ekranları yeni görünümü alır.
    /// </summary>
    public static class UITheme
    {
        // ---------------------------------------------------------------- Renkler
        public static readonly Color Accent = new Color(1f, 0.72f, 0.18f);        // altın sarısı
        public static readonly Color AccentDark = new Color(0.75f, 0.45f, 0.05f);
        public static readonly Color Panel = new Color(0.07f, 0.09f, 0.12f, 0.82f);
        public static readonly Color PanelLight = new Color(0.14f, 0.17f, 0.22f, 0.92f);
        public static readonly Color Ink = new Color(0.93f, 0.95f, 0.97f);
        public static readonly Color TextDim = new Color(0.62f, 0.68f, 0.75f);
        public static readonly Color Good = new Color(0.45f, 0.95f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f);

        static GUISkin skin;
        static Texture2D panelTex, panelLightTex, buttonTex, buttonHoverTex, buttonActiveTex, selectedTex, accentTex, fieldTex, glowTex;
        static readonly Dictionary<int, float> hover = new Dictionary<int, float>();
        static readonly Dictionary<int, float> press = new Dictionary<int, float>();

        public static Texture2D PanelTex { get { Ensure(); return panelTex; } }
        public static Texture2D PanelLightTex { get { Ensure(); return panelLightTex; } }
        public static Texture2D AccentTex { get { Ensure(); return accentTex; } }
        public static Texture2D GlowTex { get { Ensure(); return glowTex; } }

        /// <summary>Temalı GUISkin. Sadece OnGUI içinde çağrılmalı (GUI.skin'i kopyalar).</summary>
        public static GUISkin Skin
        {
            get
            {
                if (skin != null) return skin;
                Ensure();
                skin = Object.Instantiate(GUI.skin);
                skin.name = "MiniTankSkin";

                var b = skin.button;
                SetBackgrounds(b, buttonTex, buttonHoverTex, buttonActiveTex);
                b.border = new RectOffset(18, 18, 18, 18);
                b.fontStyle = FontStyle.Bold;
                b.richText = true;
                b.wordWrap = true;
                b.normal.textColor = Ink;
                b.hover.textColor = Color.white;
                b.active.textColor = Accent;
                b.focused.textColor = Ink;
                b.onNormal.textColor = b.onHover.textColor = Accent;

                var box = skin.box;
                SetBackgrounds(box, panelTex, panelTex, panelTex);
                box.border = new RectOffset(18, 18, 18, 18);
                box.normal.textColor = Ink;

                var tf = skin.textField;
                SetBackgrounds(tf, fieldTex, fieldTex, fieldTex);
                tf.focused.background = fieldTex;
                tf.border = new RectOffset(14, 14, 14, 14);
                tf.padding = new RectOffset(18, 18, 10, 10);
                tf.normal.textColor = tf.hover.textColor = tf.focused.textColor = Color.white;
                tf.alignment = TextAnchor.MiddleLeft;

                skin.label.normal.textColor = Ink;
                skin.label.richText = true;
                skin.toggle.normal.textColor = Ink;

                var sb = skin.verticalScrollbar;
                SetBackgrounds(sb, fieldTex, fieldTex, fieldTex);
                SetBackgrounds(skin.verticalScrollbarThumb, accentTex, accentTex, accentTex);
                SetBackgrounds(skin.horizontalSlider, fieldTex, fieldTex, fieldTex);
                SetBackgrounds(skin.horizontalSliderThumb, accentTex, accentTex, accentTex);
                return skin;
            }
        }

        static void SetBackgrounds(GUIStyle s, Texture2D normal, Texture2D hoverT, Texture2D active)
        {
            s.normal.background = normal;
            s.hover.background = hoverT;
            s.active.background = active;
            s.focused.background = normal;
            s.onNormal.background = active;
            s.onHover.background = active;
            s.onActive.background = active;
        }

        static void Ensure()
        {
            if (panelTex != null) return;
            panelTex = Rounded(64, 18, new Color(0.06f, 0.08f, 0.11f, 0.86f), new Color(1f, 1f, 1f, 0.08f), 2, 0.12f);
            panelLightTex = Rounded(64, 18, new Color(0.13f, 0.16f, 0.21f, 0.94f), new Color(1f, 1f, 1f, 0.1f), 2, 0.1f);
            buttonTex = Rounded(64, 18, new Color(0.16f, 0.2f, 0.27f, 0.95f), new Color(1f, 1f, 1f, 0.12f), 2, 0.18f);
            buttonHoverTex = Rounded(64, 18, new Color(0.22f, 0.27f, 0.36f, 0.97f), new Color(1f, 0.85f, 0.5f, 0.35f), 2, 0.2f);
            buttonActiveTex = Rounded(64, 18, new Color(0.1f, 0.13f, 0.18f, 1f), new Color(1f, 0.75f, 0.25f, 0.8f), 3, 0.1f);
            selectedTex = Rounded(64, 18, new Color(0.3f, 0.22f, 0.07f, 0.97f), Accent, 4, 0.2f);
            accentTex = Rounded(64, 18, Accent, new Color(1f, 0.95f, 0.7f, 0.8f), 2, 0.28f);
            fieldTex = Rounded(48, 14, new Color(0.03f, 0.04f, 0.06f, 0.9f), new Color(1f, 1f, 1f, 0.18f), 2, 0f);
            glowTex = RadialGlow(64);
        }

        /// <summary>Yuvarlak köşeli, kenarlıklı, üstten hafif parlak (degrade) doku. 9 dilimli çizilir.</summary>
        static Texture2D Rounded(int size, int radius, Color fill, Color border, int borderWidth, float topShine)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Köşeye olan uzaklık (işaretli mesafe alanı)
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float inside = Mathf.Clamp01(radius - d + 0.5f);          // kenar yumuşatma
                    float edge = Mathf.Clamp01(radius - d + 0.5f - borderWidth);
                    float v = (float)y / (size - 1);
                    Color c = fill;
                    c.r += topShine * v * 0.5f; c.g += topShine * v * 0.5f; c.b += topShine * v * 0.5f;
                    c = Color.Lerp(border, c, edge);
                    c.a *= inside;
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D RadialGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Dikey degrade doku (arena kartları vb.).</summary>
        public static Texture2D Gradient(Color top, Color bottom, int height = 64)
        {
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, (float)y / (height - 1)));
            tex.Apply(false, true);
            return tex;
        }

        // ---------------------------------------------------------------- Çizim yardımcıları

        public static void DrawPanel(Rect r, bool light = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            Ensure();
            var style = GUI.skin.box;
            var old = style.normal.background;
            style.normal.background = light ? panelLightTex : panelTex;
            style.Draw(r, GUIContent.none, false, false, false, false);
            style.normal.background = old;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c * old;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Glow(Rect r, Color c)
        {
            Ensure();
            var old = GUI.color;
            GUI.color = c * old;
            GUI.DrawTexture(r, glowTex);
            GUI.color = old;
        }

        public static GUIStyle Text(int size, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, Color? color = null)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, richText = true, wordWrap = true, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            s.normal.textColor = color ?? Ink;
            return s;
        }

        /// <summary>
        /// Animasyonlu buton: üzerine gelince hafifçe büyür, basınca içe çöker, seçiliyse altın çerçeve.
        /// id her buton için benzersiz olmalı (ör. "mode".GetHashCode() + i).
        /// </summary>
        public static bool Button(int id, Rect r, string text, bool selected = false, int fontSize = 28, bool accent = false)
        {
            Ensure();
            var e = Event.current;
            bool over = r.Contains(e.mousePosition);
            float h, p;
            hover.TryGetValue(id, out h);
            press.TryGetValue(id, out p);
            if (e.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                h = Mathf.MoveTowards(h, over ? 1f : 0f, dt * 6f);
                p = Mathf.MoveTowards(p, 0f, dt * 5f);
                hover[id] = h;
                press[id] = p;
            }
            if (e.type == EventType.MouseDown && over) press[id] = 1f;

            float scale = 1f + 0.04f * Ease(h) - 0.05f * p;
            Rect sr = Scale(r, scale);

            var style = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
            if (accent)
            {
                style.normal.background = style.hover.background = accentTex;
                style.normal.textColor = style.hover.textColor = new Color(0.12f, 0.08f, 0.02f);
            }
            else if (selected)
            {
                style.normal.background = style.hover.background = selectedTex;
                style.normal.textColor = style.hover.textColor = Accent;
            }
            if (selected && e.type == EventType.Repaint)
                Glow(new Rect(sr.x - 20, sr.y - 20, sr.width + 40, sr.height + 40), new Color(Accent.r, Accent.g, Accent.b, 0.18f));
            return GUI.Button(sr, text, style);
        }

        public static Rect Scale(Rect r, float s)
        {
            float w = r.width * s, hh = r.height * s;
            return new Rect(r.center.x - w / 2f, r.center.y - hh / 2f, w, hh);
        }

        public static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>Ekran açılış animasyonu: 0→1 (gecikme ile sıralı giriş için).</summary>
        public static float Intro(float openedAt, float delay, float duration = 0.45f) =>
            Ease(Mathf.Clamp01((Time.unscaledTime - openedAt - delay) / duration));

        /// <summary>Soldan/sağdan kayarak ve belirerek girme: rect'i kaydırır, GUI.color alfasını ayarlar.</summary>
        public static Rect SlideIn(Rect r, float t, float dx, float dy = 0f)
        {
            var c = GUI.color;
            c.a = t;
            GUI.color = c;
            return new Rect(r.x + dx * (1f - t), r.y + dy * (1f - t), r.width, r.height);
        }

        public static void ResetColor() => GUI.color = Color.white;

        /// <summary>Yatay değer çubuğu (tank özellikleri).</summary>
        public static void StatBar(Rect r, string name, float value01, Color color)
        {
            GUI.Label(new Rect(r.x, r.y, r.width * 0.34f, r.height), name, Text(20, TextAnchor.MiddleLeft, false, TextDim));
            var bar = new Rect(r.x + r.width * 0.36f, r.y + r.height * 0.32f, r.width * 0.64f, r.height * 0.36f);
            Fill(bar, new Color(1f, 1f, 1f, 0.08f));
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value01), bar.height), color);
        }
    }
}
