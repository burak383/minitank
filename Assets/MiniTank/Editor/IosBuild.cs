using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace MiniTank.EditorTools
{
    /// <summary>
    /// iOS Xcode projesini üretir. Codemagic bunu komut satırından çağırır:
    ///   Unity -batchmode -executeMethod MiniTank.EditorTools.IosBuild.Build
    /// Çıktı: proje kökünde "ios" klasörü.
    /// </summary>
    public static class IosBuild
    {
        public const string BundleId = "com.burak.minitank";
        public const string OutputDir = "ios";
        const string MinIosVersion = "15.0";

        [MenuItem("MiniTank/iOS Xcode Projesi Üret")]
        public static void BuildFromMenu() => Run(false);

        /// <summary>Codemagic (komut satırı) giriş noktası.</summary>
        public static void Build() => Run(true);

        static void Run(bool batch)
        {
            string error = Check();
            if (error != null) { Fail(error, batch); return; }

            // Android'deki derleme sembollerini (Firebase, reklam, mağaza) iOS'a da uygula
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, defines);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = MinIosVersion;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false; // imzayı Codemagic yapar
            // Sadece iPhone: iPad ekran görüntüsü zorunluluğu olmaz; yatay oyun için tam ekran şart
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.requiresFullScreen = true;

            // Derleme numarası: Codemagic her build'de artırır
            string buildNumber = Environment.GetEnvironmentVariable("PROJECT_BUILD_NUMBER")
                                 ?? Environment.GetEnvironmentVariable("BUILD_NUMBER");
            if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Fail("Build Settings'te sahne yok. Önce MiniTank → Her Şeyi Kur'u çalıştır.", batch); return; }

            Debug.Log($"[MiniTank] iOS build başlıyor: {PlayerSettings.bundleVersion} ({PlayerSettings.iOS.buildNumber}), sahneler: {string.Join(", ", scenes)}");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            });

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Fail("iOS build başarısız: " + report.summary.result + " (" + report.summary.totalErrors + " hata)", batch);
                return;
            }
            Debug.Log("[MiniTank] iOS Xcode projesi hazır: " + Path.GetFullPath(OutputDir));
            if (batch) EditorApplication.Exit(0);
        }

        /// <summary>iOS için gerekli dosya ve kimlikler eksik mi?</summary>
        static string Check()
        {
            if (!File.Exists("Assets/GoogleService-Info.plist"))
                return "Assets/GoogleService-Info.plist yok. Firebase konsolundaki iOS uygulamasından indirip Assets klasörüne koy.";

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var p = so.FindProperty("adMobIOSAppId");
                if (p != null && string.IsNullOrEmpty(p.stringValue))
                    return "AdMob iOS uygulama kimliği boş (Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings). Boşken iOS'ta oyun açılışta çöker.";
            }
            return null;
        }

        static void Fail(string message, bool batch)
        {
            Debug.LogError("[MiniTank] " + message);
            if (batch) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("MiniTank iOS", message, "Tamam");
        }

#if UNITY_IOS
        /// <summary>Xcode projesine App Store için gereken ayarları ekler.</summary>
        [PostProcessBuild(1000)]
        static void OnPostprocess(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            // Sadece standart HTTPS şifrelemesi: App Store'daki "ihracat uyumu" sorusunu atlar
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetString("CFBundleDisplayName", "Mini Tank");
            plist.WriteToFile(plistPath);

            // Firebase / reklam SDK'ları bitcode desteklemiyor
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            foreach (var guid in new[] { proj.GetUnityMainTargetGuid(), proj.GetUnityFrameworkTargetGuid() })
                proj.SetBuildProperty(guid, "ENABLE_BITCODE", "NO");
            proj.WriteToFile(projPath);
        }
#endif
    }
}
