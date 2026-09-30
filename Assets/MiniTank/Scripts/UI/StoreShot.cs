using System.Collections;
using System.IO;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Play Store ekran görüntüsü aracı (editörde, oyun çalışırken): Game görünümünü yakalar,
    /// ortadan 16:9 kırpar ve tam 1920x1080 PNG olarak Yayin/ekran-goruntuleri klasörüne kaydeder.
    /// Menü: MiniTank/Ekran Görüntüsü Al  (kısayol Ctrl+Shift+K)
    /// </summary>
    public class StoreShot : MonoBehaviour
    {
        /// <summary>true iken geliştirme yazıları (reklam durumu vb.) gizlenir.</summary>
        public static bool Clean { get; set; }

        public const int Width = 1920, Height = 1080;
        static StoreShot runner;

        public static string Folder =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "Yayin", "ekran-goruntuleri");

        public static void Take()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[MiniTank] Ekran görüntüsü için önce Play'e bas."); return; }
            if (runner == null)
            {
                var go = new GameObject("Ekran Görüntüsü");
                go.hideFlags = HideFlags.HideAndDontSave;
                runner = go.AddComponent<StoreShot>();
            }
            runner.StartCoroutine(runner.Capture());
        }

        IEnumerator Capture()
        {
            Clean = true;
            yield return null;                    // yazılar gizlenmiş kareyi çiz
            yield return new WaitForEndOfFrame();
            var src = ScreenCapture.CaptureScreenshotAsTexture();
            Clean = false;

            // Ortadan 16:9 kırp
            int w = src.width, h = src.height;
            int cw = w, ch = h;
            if ((float)w / h > 16f / 9f) cw = Mathf.RoundToInt(h * 16f / 9f);
            else ch = Mathf.RoundToInt(w * 9f / 16f);
            var scale = new Vector2((float)cw / w, (float)ch / h);
            var offset = new Vector2((w - cw) * 0.5f / w, (h - ch) * 0.5f / h);

            var rt = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt, scale, offset);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var outTex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            outTex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            outTex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Directory.CreateDirectory(Folder);
            string file = Path.Combine(Folder, "ekran_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            File.WriteAllBytes(file, outTex.EncodeToPNG());
            Destroy(src);
            Destroy(outTex);
            Debug.Log("[MiniTank] Ekran görüntüsü kaydedildi: " + file +
                      (w < Width ? "  (Uyarı: Game görünümü küçük, Game penceresinde 1920x1080 seçersen daha net olur)" : ""));
        }
    }
}
