#if MINITANK_ADMOB
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Google AdMob: önce gizlilik onayı (AB/İngiltere kullanıcılarına Google'ın zorunlu formu), sonra
    /// ödüllü ve geçiş reklamlarını önceden yükler. Reklam kapanınca bir sonrakini hemen yükler.
    /// Geliştirme build'lerinde Google'ın test reklamları gösterilir.
    /// </summary>
    public class AdMobService : IAdService
    {
        RewardedAd rewarded;
        InterstitialAd interstitial;
        bool initialized, loadingRewarded, loadingInterstitial;
        float retryRewardedAt, retryInterstitialAt;

        /// <summary>
        /// Reklam SDK'sının geri çağrılarını Unity ana iş parçacığına taşır (arayüz, PlayerPrefs, Task güvenliği).
        /// Kendi dağıtıcımızı kullanıyoruz: SDK'nınki, SDK başlamadan gelen onay (UMP) geri çağrılarını işlemiyordu.
        /// </summary>
        static void Main(System.Action a) => MainThread.Post(a);

        string RewardedId => Ads.UseTestUnits ? Ads.TestRewardedUnitId : Ads.RewardedUnitId;
        string InterstitialId => Ads.UseTestUnits ? Ads.TestInterstitialUnitId : Ads.InterstitialUnitId;

        public bool RewardedReady { get { Tick(); return rewarded != null && rewarded.CanShowAd(); } }
        public bool InterstitialReady { get { Tick(); return interstitial != null && interstitial.CanShowAd(); } }

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void ShowPrivacyOptions()
        {
            ConsentForm.ShowPrivacyOptionsForm(err => Main(() =>
            {
                if (err != null) Debug.LogWarning("[MiniTank] Gizlilik formu: " + err.Message);
            }));
        }

        public void Initialize()
        {
            MainThread.Ensure();
            Ads.Status = "Gizlilik onayı kontrol ediliyor";
            try
            {
                var request = new ConsentRequestParameters();
                ConsentInformation.Update(request, updateError => Main(() =>
                {
                    if (updateError != null)
                    {
                        // Onay bilgisi alınamadı (ör. AdMob'da gizlilik mesajı yok): Türkiye gibi AB dışı
                        // ülkelerde onay gerekmez, reklamlara yine de başla.
                        Debug.LogWarning("[MiniTank] Onay bilgisi alınamadı: " + updateError.Message);
                        StartAds();
                        return;
                    }
                    ConsentForm.LoadAndShowConsentFormIfRequired(formError => Main(() =>
                    {
                        if (formError != null) Debug.LogWarning("[MiniTank] Onay formu: " + formError.Message);
                        if (ConsentInformation.CanRequestAds() || formError != null) StartAds();
                        else Ads.Status = "Kullanıcı reklam onayı vermedi";
                    }));
                }));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MiniTank] Onay (UMP) başlatılamadı: " + e.Message);
                StartAds();
            }
            // Önceki oturumdan onay varsa beklemeden başla
            if (ConsentInformation.CanRequestAds()) StartAds();
            // Onay akışı takılırsa 8 sn sonra yine de başla
            MainThread.After(8f, () => { if (!initialized) { Debug.LogWarning("[MiniTank] Onay yanıtı gelmedi, reklamlar başlatılıyor."); StartAds(); } });
        }

        void StartAds()
        {
            if (initialized) return;
            initialized = true;
            Ads.Status = "Reklam SDK'sı başlatılıyor";
            MobileAds.Initialize(status => Main(() =>
            {
                Ads.Status = "Reklamlar yükleniyor";
                LoadRewarded();
                LoadInterstitial();
            }));
        }

        /// <summary>Yükleme başarısız olduysa bir süre sonra tekrar dener.</summary>
        void Tick()
        {
            if (!initialized) return;
            if (rewarded == null && !loadingRewarded && Time.realtimeSinceStartup >= retryRewardedAt) LoadRewarded();
            if (interstitial == null && !loadingInterstitial && Time.realtimeSinceStartup >= retryInterstitialAt) LoadInterstitial();
        }

        void LoadRewarded()
        {
            if (loadingRewarded) return;
            loadingRewarded = true;
            RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) => Main(() =>
            {
                loadingRewarded = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[MiniTank] Ödüllü reklam yüklenemedi: " + (error != null ? error.GetMessage() : "boş"));
                    Ads.Status = "Ödüllü reklam yüklenemedi: " + (error != null ? error.GetMessage() : "boş");
                    retryRewardedAt = Time.realtimeSinceStartup + 30f;
                    return;
                }
                rewarded = ad;
                Ads.Status = "Ödüllü reklam hazır";
            }));
        }

        void LoadInterstitial()
        {
            if (loadingInterstitial) return;
            loadingInterstitial = true;
            InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) => Main(() =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[MiniTank] Geçiş reklamı yüklenemedi: " + (error != null ? error.GetMessage() : "boş"));
                    retryInterstitialAt = Time.realtimeSinceStartup + 60f;
                    return;
                }
                interstitial = ad;
            }));
        }

        public Task<bool> ShowRewardedAsync(string placement)
        {
            var tcs = new TaskCompletionSource<bool>();
            var ad = rewarded;
            if (ad == null || !ad.CanShowAd()) { tcs.SetResult(false); return tcs.Task; }

            rewarded = null;
            bool earned = false;
            Ads.Busy = true;
            ad.OnAdFullScreenContentClosed += () => Main(() =>
            {
                Ads.Busy = false;
                ad.Destroy();
                LoadRewarded();
                tcs.TrySetResult(earned);
            });
            ad.OnAdFullScreenContentFailed += err => Main(() =>
            {
                Ads.Busy = false;
                Debug.LogWarning("[MiniTank] Ödüllü reklam açılamadı: " + err.GetMessage());
                ad.Destroy();
                LoadRewarded();
                tcs.TrySetResult(false);
            });
            ad.Show(reward => Main(() => { earned = true; }));
            return tcs.Task;
        }

        public Task ShowInterstitialAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var ad = interstitial;
            if (ad == null || !ad.CanShowAd()) { tcs.SetResult(false); return tcs.Task; }

            interstitial = null;
            Ads.Busy = true;
            ad.OnAdFullScreenContentClosed += () => Main(() =>
            {
                Ads.Busy = false;
                ad.Destroy();
                LoadInterstitial();
                tcs.TrySetResult(true);
            });
            ad.OnAdFullScreenContentFailed += err => Main(() =>
            {
                Ads.Busy = false;
                ad.Destroy();
                LoadInterstitial();
                tcs.TrySetResult(false);
            });
            ad.Show();
            return tcs.Task;
        }
    }
}
#endif
