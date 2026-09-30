using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniTank
{
    // ======================================================================= Reklam

    /// <summary>Reklam servisi: ödüllü (oyuncu ister) ve geçiş (maç arası) reklamları.</summary>
    public interface IAdService
    {
        bool RewardedReady { get; }
        bool InterstitialReady { get; }
        /// <summary>Ödüllü reklamı gösterir; oyuncu ödülü hak ederse true döner.</summary>
        Task<bool> ShowRewardedAsync(string placement);
        /// <summary>Geçiş reklamını gösterir; kapanınca (veya gösterilemezse) tamamlanır.</summary>
        Task ShowInterstitialAsync();
        /// <summary>Gizlilik seçenekleri (AB kullanıcıları için onay formu) gösterilmeli mi?</summary>
        bool PrivacyOptionsRequired { get; }
        void ShowPrivacyOptions();
    }

    public static class Ads
    {
        // AdMob kimlikleri (Mini Tank, Android)
        public const string AndroidAppId = "ca-app-pub-9017194698663463~5639094588";
        public const string AndroidRewardedUnitId = "ca-app-pub-9017194698663463/7328589936";
        public const string AndroidInterstitialUnitId = "ca-app-pub-9017194698663463/3831188899";
        // AdMob kimlikleri (Mini Tank, iOS) — AdMob'daki iOS uygulamasından
        public const string IosAppId = "";
        public const string IosRewardedUnitId = "";
        public const string IosInterstitialUnitId = "";

#if UNITY_IOS
        public static string RewardedUnitId => IosRewardedUnitId;
        public static string InterstitialUnitId => IosInterstitialUnitId;
        // Google'ın resmi iOS test birimleri
        public const string TestRewardedUnitId = "ca-app-pub-3940256099942544/1712485313";
        public const string TestInterstitialUnitId = "ca-app-pub-3940256099942544/4411468910";
#else
        public static string RewardedUnitId => AndroidRewardedUnitId;
        public static string InterstitialUnitId => AndroidInterstitialUnitId;
        // Google'ın resmi test birimleri: geliştirme build'lerinde bunlar kullanılır
        // (kendi reklamına tıklamak AdMob hesabını askıya aldırabilir)
        public const string TestRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        public const string TestInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
#endif

        /// <summary>Geliştirme build'i veya editör: test reklamları.</summary>
        public static bool UseTestUnits => Application.isEditor || Debug.isDebugBuild;

        static IAdService service;
        public static IAdService Service => service ?? (service = new TestAdService());
        public static bool IsTestMode => Service is TestAdService;
        public static void SetService(IAdService s) => service = s;

        /// <summary>Test reklamı gösterilirken ekrana çizilir (MatchHUD).</summary>
        public static bool ShowingTestAd { get; internal set; }
        /// <summary>Şu an tam ekran reklam açık mı (maç sonu menü dönüşü bekler).</summary>
        public static bool Busy { get; internal set; }

        /// <summary>Reklam sisteminin durumu (geliştirme build'inde menüde gösterilir).</summary>
        public static string Status { get; set; } = "Reklam servisi yok (editör/test)";

        /// <summary>Oyuncunun en son ödüllü reklam izlediği an (gerçek zaman).</summary>
        public static float LastRewardedAt { get; private set; } = -1000f;
        public static void MarkRewarded() => LastRewardedAt = Time.realtimeSinceStartup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if MINITANK_ADMOB && !UNITY_EDITOR
#if UNITY_IOS
            if (string.IsNullOrEmpty(IosRewardedUnitId)) { Status = "iOS reklam kimlikleri girilmedi"; return; }
#endif
            var s = new AdMobService();
            SetService(s);
            s.Initialize();
#endif
        }

        // ------------------------------------------------------------------ Sıklık kuralları

        const string MatchesKey = "mt_ads_matches";
        const string LastInterstitialKey = "mt_ads_last_inter";
        const string DailyKey = "mt_ads_daily";
        const string DailyCountKey = "mt_ads_daily_count";

        /// <summary>Ücretsiz ödüllü reklam: günde en fazla bu kadar.</summary>
        public const int DailyRewardLimit = 5;
        public const int DailyRewardGold = 50;

        /// <summary>
        /// Maç bitince çağrılır. Geçiş reklamı gösterilmeli mi? Kural: ilk 2 maçta asla, sonra her 3 maçta bir;
        /// eğitimde asla; oyuncu o maçta ödüllü reklam izlediyse gösterme (zaten reklam izledi).
        /// </summary>
        public static bool ShouldShowInterstitial(bool watchedRewardedThisMatch)
        {
            int matches = PlayerPrefs.GetInt(MatchesKey, 0) + 1;
            PlayerPrefs.SetInt(MatchesKey, matches);
            if (MatchSettings.Tutorial || watchedRewardedThisMatch || matches <= 2) return false;
            int last = PlayerPrefs.GetInt(LastInterstitialKey, 0);
            if (matches - last < 3) return false;
            if (!Service.InterstitialReady) return false;
            PlayerPrefs.SetInt(LastInterstitialKey, matches);
            return true;
        }

        static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");

        public static int DailyRewardsLeft
        {
            get
            {
                if (PlayerPrefs.GetString(DailyKey, "") != Today) return DailyRewardLimit;
                return Mathf.Max(0, DailyRewardLimit - PlayerPrefs.GetInt(DailyCountKey, 0));
            }
        }

        public static void CountDailyReward()
        {
            if (PlayerPrefs.GetString(DailyKey, "") != Today)
            {
                PlayerPrefs.SetString(DailyKey, Today);
                PlayerPrefs.SetInt(DailyCountKey, 0);
            }
            PlayerPrefs.SetInt(DailyCountKey, PlayerPrefs.GetInt(DailyCountKey, 0) + 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Sadece editörde çalışan sahte reklam: 2 saniye bekler ve ödül verir. Telefonda reklam yok.</summary>
    public class TestAdService : IAdService
    {
        public bool RewardedReady => Application.isEditor;
        public bool InterstitialReady => Application.isEditor;
        public bool PrivacyOptionsRequired => false;
        public void ShowPrivacyOptions() { }

        public async Task<bool> ShowRewardedAsync(string placement)
        {
            if (!Application.isEditor) return false;
            Debug.Log($"[MiniTank] Test ödüllü reklam: {placement}");
            Ads.ShowingTestAd = true;
            Ads.Busy = true;
            await Task.Delay(2000);
            Ads.ShowingTestAd = false;
            Ads.Busy = false;
            return true;
        }

        public async Task ShowInterstitialAsync()
        {
            if (!Application.isEditor) return;
            Debug.Log("[MiniTank] Test geçiş reklamı");
            Ads.ShowingTestAd = true;
            Ads.Busy = true;
            await Task.Delay(1500);
            Ads.ShowingTestAd = false;
            Ads.Busy = false;
        }
    }

    // ======================================================================= Mağaza (uygulama içi satın alma)

    public class GoldPack
    {
        public string productId;   // Google Play ürün kimliği
        public string name;
        public int gold;
        public string priceLabel;  // mağaza bağlanınca gerçek (yerel para birimli) fiyatla değişir
        public bool bestValue;
    }

    public interface IStoreService
    {
        bool Ready { get; }
        string PriceOf(GoldPack pack);
        /// <summary>Satın alma: ödeme tamamlanıp altın verilince true.</summary>
        Task<bool> BuyAsync(GoldPack pack);
    }

    /// <summary>Altın paketleri. Telefonda Google Play (Unity IAP), editörde ücretsiz test satın alması.</summary>
    public static class Store
    {
        public static readonly GoldPack[] Packs =
        {
            new GoldPack { productId = "minitank.gold.small",  name = "Bir avuç altın", gold = 1000,  priceLabel = "₺29,99" },
            new GoldPack { productId = "minitank.gold.medium", name = "Altın kesesi",   gold = 3000,  priceLabel = "₺79,99" },
            new GoldPack { productId = "minitank.gold.large",  name = "Altın sandığı",  gold = 7500,  priceLabel = "₺179,99", bestValue = true },
        };

        public static GoldPack Find(string productId)
        {
            foreach (var p in Packs) if (p.productId == productId) return p;
            return null;
        }

        static IStoreService service;
        public static void SetService(IStoreService s) => service = s;

        /// <summary>Gerçek mağaza bağlı değilse (editör veya IAP paketi yok) test modundayız.</summary>
        public static bool IsTestMode => service == null;
        public static bool Ready => service != null ? service.Ready : Application.isEditor;

        public static string PriceOf(GoldPack pack) => service != null ? service.PriceOf(pack) : pack.priceLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if MINITANK_IAP && !UNITY_EDITOR
            var s = new IapStoreService();
            SetService(s);
            s.Initialize();
#endif
        }

        public static async Task<bool> BuyAsync(GoldPack pack)
        {
            if (service != null) return await service.BuyAsync(pack);
            if (!Application.isEditor) return false;
            Debug.Log($"[MiniTank] Test satın alma: {pack.productId}");
            await Task.Delay(500);
            return await ProgressService.AddGoldAsync(pack.gold);
        }
    }

    /// <summary>Başka iş parçacıklarından gelen işleri Unity ana iş parçacığında çalıştırır.</summary>
    public class MainThread : MonoBehaviour
    {
        static MainThread instance;
        static readonly System.Collections.Generic.Queue<Action> queue = new System.Collections.Generic.Queue<Action>();
        static readonly System.Collections.Generic.List<(float, Action)> timers = new System.Collections.Generic.List<(float, Action)>();

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("Ana İş Parçacığı");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MainThread>();
        }

        public static void Post(Action a)
        {
            if (a == null) return;
            lock (queue) queue.Enqueue(a);
        }

        /// <summary>Ana iş parçacığında, gerçek zamanla saniye sonra çalıştırır.</summary>
        public static void After(float seconds, Action a)
        {
            Post(() => timers.Add((Time.realtimeSinceStartup + seconds, a)));
        }

        void Update()
        {
            while (true)
            {
                Action a;
                lock (queue)
                {
                    if (queue.Count == 0) break;
                    a = queue.Dequeue();
                }
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                if (Time.realtimeSinceStartup < timers[i].Item1) continue;
                var a = timers[i].Item2;
                timers.RemoveAt(i);
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
