using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MiniTank.EditorSetup
{
    /// <summary>
    /// Firebase Auth SDK projeye eklendiğinde MINITANK_FIREBASE sembolünü otomatik ekler,
    /// SDK kaldırılırsa sembolü siler. Böylece proje Firebase olsun olmasın derlenir.
    /// Bu script ayrı bir assembly'de olduğu için oyun kodunda derleme hatası olsa bile çalışır.
    /// </summary>
    [InitializeOnLoad]
    static class FirebaseDefineSetup
    {
        static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS,
        };

        static FirebaseDefineSetup()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("MiniTank/Servis Durumunu Kontrol Et (Firebase, Reklam, Mağaza)", priority = 20)]
        static void CheckFromMenu()
        {
            Apply();
            EditorUtility.DisplayDialog("MiniTank",
                (HasFirebaseAuth()
                    ? "✔ Firebase Auth bulundu: giriş ekranı Firebase ile çalışıyor."
                    : "✘ Firebase Auth bulunamadı: giriş ekranı cihaz içi test modunda.") + "\n\n" +
                (HasAssembly("Firebase.Firestore")
                    ? "✔ Firestore bulundu: lig ve istatistikler bulutta saklanıyor."
                    : "✘ Firestore bulunamadı: lig ve istatistikler sadece bu cihazda saklanıyor.") + "\n\n" +
                (HasType("GoogleMobileAds.Api.MobileAds")
                    ? "✔ Google Mobile Ads bulundu: telefonda gerçek reklamlar."
                    : "✘ Google Mobile Ads bulunamadı: reklamlar sadece editörde test modunda.") + "\n\n" +
                (HasType("UnityEngine.Purchasing.StoreController")
                    ? "✔ Unity IAP 5 bulundu: telefonda Google Play satın alma."
                    : "✘ Unity IAP 5 bulunamadı: mağaza sadece editörde test modunda.") +
                "\n\nKurulum adımları KURULUM.md dosyasında.",
                "Tamam");
        }

        static bool HasAssembly(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == name);

        static bool HasFirebaseAuth() => HasAssembly("Firebase.Auth");

        /// <summary>Tam adıyla bir tür yüklü assembly'lerden birinde var mı (paket assembly adı sürüme göre değişebilir).</summary>
        static bool HasType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => { try { return a.GetType(fullName, false) != null; } catch { return false; } });

        static bool Has(string requirement) =>
            requirement.StartsWith("type:") ? HasType(requirement.Substring(5)) : HasAssembly(requirement);

        // Sembol -> gerektirdiği Firebase assembly'si
        static readonly (string symbol, string assembly)[] Features =
        {
            ("MINITANK_FIREBASE", "Firebase.Auth"),
            ("MINITANK_FIRESTORE", "Firebase.Firestore"),
            ("MINITANK_ADMOB", "type:GoogleMobileAds.Api.MobileAds"),
            ("MINITANK_IAP", "type:UnityEngine.Purchasing.StoreController"),
        };

        static void Apply()
        {
            foreach (var target in Targets)
            {
                string current;
                try { current = PlayerSettings.GetScriptingDefineSymbols(target); }
                catch { continue; } // platform modülü kurulu değil

                var symbols = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                bool changed = false;
                foreach (var f in Features)
                {
                    bool has = Has(f.assembly);
                    bool present = symbols.Contains(f.symbol);
                    if (has && !present) { symbols.Add(f.symbol); changed = true; Debug.Log($"[MiniTank] {target.TargetName}: {f.symbol} eklendi."); }
                    else if (!has && present) { symbols.Remove(f.symbol); changed = true; Debug.Log($"[MiniTank] {target.TargetName}: {f.symbol} kaldırıldı."); }
                }
                if (changed) PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            }
        }
    }
}
