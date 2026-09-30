using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MiniTank.EditorTools
{
    /// <summary>Gökyüzü ve URP görüntü efektleri (ton eşleme, bloom, renk ayarı, vinyet, kenar yumuşatma).</summary>
    public static partial class MiniTankBuilder
    {
        static Material CreateSkybox(string name, Color tint, Color ground, float atmosphere)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return null;

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(shader);
            m.shader = shader;
            m.SetFloat("_SunSize", 0.035f);
            m.SetFloat("_SunSizeConvergence", 6f);
            m.SetFloat("_AtmosphereThickness", atmosphere);
            m.SetColor("_SkyTint", tint);
            m.SetColor("_GroundColor", ground);
            m.SetFloat("_Exposure", 1.15f);
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        static VolumeProfile postProfile;

        static VolumeProfile GetPostProfile()
        {
            string path = $"{Root}/PostFX.asset";
            if (postProfile == null) postProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (postProfile == null)
            {
                postProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(postProfile, path);
            }

            // Her kurulumda değerleri güncelle
            var tone = GetOrAdd<Tonemapping>(postProfile);
            tone.mode.value = TonemappingMode.ACES;

            var color = GetOrAdd<ColorAdjustments>(postProfile);
            color.postExposure.value = 0.2f;
            color.contrast.value = 10f;
            color.saturation.value = 6f;

            // Kar gibi parlak yüzeyler parlamasın diye yüksek eşik, düşük şiddet
            var bloom = GetOrAdd<Bloom>(postProfile);
            bloom.threshold.value = 1.4f;
            bloom.intensity.value = 0.2f;
            bloom.scatter.value = 0.5f;
            bloom.clamp.value = 3f; // tek bir aşırı parlak pikselin ekranı kaplamasını engeller

            var vignette = GetOrAdd<Vignette>(postProfile);
            vignette.intensity.value = 0.2f;
            vignette.smoothness.value = 0.45f;

            EditorUtility.SetDirty(postProfile);
            AssetDatabase.SaveAssets();
            return postProfile;
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component;
            if (!profile.TryGet(out component))
            {
                component = profile.Add<T>(true);
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            return component;
        }

        static void SetupPostProcessing(Camera cam)
        {
            var profile = GetPostProfile();
            var volumeGo = new GameObject("Görüntü Efektleri");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.stopNaN = true; // bozuk (NaN) pikseller bloom ile yayılıp beyaz leke yapmasın
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            cam.allowMSAA = true;
        }
    }
}
