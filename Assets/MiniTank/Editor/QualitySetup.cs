using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MiniTank.EditorTools
{
    /// <summary>
    /// Pikselli/tırtıklı görüntüyü düzeltir: tüm kalite seviyelerinde tam çözünürlük, 4x MSAA,
    /// daha keskin gölgeler; kameralarda SMAA; dokularda anizotropik filtreleme.
    /// </summary>
    public static partial class MiniTankBuilder
    {
        [MenuItem("MiniTank/Görüntü Kalitesini Düzelt (Pikselleşme)", priority = 2)]
        public static void FixImageQualityMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string activeScene = EditorSceneManager.GetActiveScene().path;

            int assets = ConfigureRenderPipelineAssets();
            int textures = ConfigureTextures();
            int cameras = 0;
            foreach (var def in ArenaDefinitions())
            {
                string path = $"{ScenesFolder}/{def.sceneName}.unity";
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    ConfigureCamera(cam);
                    cameras++;
                }
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(activeScene) && File.Exists(activeScene)) EditorSceneManager.OpenScene(activeScene);

            EditorUtility.DisplayDialog("MiniTank",
                $"Görüntü kalitesi ayarlandı.\n\n• {assets} render ayarı: tam çözünürlük, 4x MSAA, keskin gölgeler\n• {cameras} kamera: SMAA kenar yumuşatma\n• {textures} doku: anizotropik filtreleme\n\n" +
                "Game penceresinde 'Scale' 1x olmalı. Hâlâ bulanıksa Game penceresindeki çözünürlük menüsünden 1920x1080 seç.", "Tamam");
        }

        /// <summary>Projedeki tüm URP ayar dosyalarını (her kalite seviyesi için ayrı olabilir) günceller.</summary>
        static int ConfigureRenderPipelineAssets()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;

                // Alan adları sürümden sürüme değişebildiği için SerializedObject ile güvenli ayar
                var so = new SerializedObject(asset);
                SetInt(so, "m_MSAA", 4);
                SetFloat(so, "m_RenderScale", 1f);
                SetInt(so, "m_UpscalingFilter", 0);
                SetBool(so, "m_SupportsHDR", true);
                SetFloat(so, "m_ShadowDistance", 90f);
                SetInt(so, "m_ShadowCascadeCount", 2);
                SetInt(so, "m_MainLightShadowmapResolution", 2048);
                SetBool(so, "m_SoftShadowsSupported", true);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                count++;
            }
            AssetDatabase.SaveAssets();
            return count;
        }

        static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Enum) p.enumValueFlag = value; // enum'un gerçek değeri (örn. 4x MSAA = 4)
            else if (p.propertyType == SerializedPropertyType.Integer) p.intValue = value;
        }

        static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
        }

        static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value;
        }

        static void ConfigureCamera(Camera cam)
        {
            cam.allowMSAA = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderPostProcessing = true;
            data.stopNaN = true;
            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(data);
        }

        /// <summary>Zemin dokuları eğik açıdan bakınca bulanıklaşmasın/pikselleşmesin.</summary>
        static int ConfigureTextures()
        {
            if (!AssetDatabase.IsValidFolder(TextureFactory.Folder)) return 0;
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFactory.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
                count++;
            }
            return count;
        }
    }
}
