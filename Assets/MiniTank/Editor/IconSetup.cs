using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MiniTank.EditorTools
{
    /// <summary>
    /// Uygulama ikonunu ayarlar: varsayılan ikon (tüm platformlar) + Android uyarlanabilir ikon
    /// (arka plan + ön plan katmanı; telefon hangi şekilde keserse kessin tank ortada kalır).
    /// </summary>
    public static class IconSetup
    {
        const string Folder = "Assets/MiniTank/Icons";

        [MenuItem("MiniTank/Uygulama İkonunu Ayarla", priority = 6)]
        public static void Menu()
        {
            bool ok = Apply();
            EditorUtility.DisplayDialog("MiniTank",
                ok ? "Uygulama ikonu ayarlandı.\n\nProject Settings → Player → Icon bölümünden görebilirsin. Telefonda görmek için yeni build al."
                   : "İkon dosyaları bulunamadı: " + Folder, "Tamam");
        }

        public static bool Apply()
        {
            var icon = Prepare("icon_1024.png");
            var bg = Prepare("adaptive_bg.png");
            var fg = Prepare("adaptive_fg.png");
            if (icon == null) return false;

            // Varsayılan ikon (her platform ve Android'in eski tip ikonu buna düşer)
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            // Android: uyarlanabilir, yuvarlak ve eski tip ikonlar
            try
            {
                var kindType = Type.GetType("UnityEditor.Android.AndroidPlatformIconKind, UnityEditor.Android.Extensions");
                if (kindType != null)
                {
                    SetAndroid(kindType, "Adaptive", bg != null && fg != null ? new[] { bg, fg } : null);
                    SetAndroid(kindType, "Round", new[] { icon });
                    SetAndroid(kindType, "Legacy", new[] { icon });
                }
                else Debug.Log("[MiniTank] Android modülü bulunamadı; sadece varsayılan ikon ayarlandı.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MiniTank] Android ikonları ayarlanamadı (varsayılan ikon kullanılacak): " + e.Message);
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        static void SetAndroid(Type kindType, string kindName, Texture2D[] layers)
        {
            if (layers == null) return;
            var prop = kindType.GetProperty(kindName, BindingFlags.Public | BindingFlags.Static);
            var kind = prop != null ? prop.GetValue(null) as PlatformIconKind : null;
            if (kind == null) return;
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var pi in icons)
            {
                int n = Mathf.Min(pi.maxLayerCount, layers.Length);
                if (n <= 0) continue;
                var arr = new Texture2D[n];
                Array.Copy(layers, arr, n);
                pi.SetTextures(arr);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }

        static Texture2D Prepare(string file)
        {
            string path = Folder + "/" + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool changed = false;
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.maxTextureSize < 1024) { importer.maxTextureSize = 1024; changed = true; }
            if (!importer.alphaIsTransparency && file.Contains("fg")) { importer.alphaIsTransparency = true; changed = true; }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
