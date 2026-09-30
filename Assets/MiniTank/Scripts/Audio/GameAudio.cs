using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniTank
{
    /// <summary>
    /// Tüm oyun sesleri: atış, isabet, patlama, motor, arayüz, menü müziği ve arena ambiyansı.
    /// Oyun açılınca kendiliğinden oluşur, sahneler arasında yaşar.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        public AudioClip EngineClip { get; private set; }
        public AudioClip TracksClip { get; private set; }
        AudioClip whoosh, whistle, shieldUp, repairUp, pickup, flyby, reload;
        AudioClip click, killChime, assistChime, promoteChime, wind, menuMusic;
        // Aynı ses art arda tekrar etmesin diye birden fazla varyasyon
        AudioClip[] cannon, cannonFar, explosion, hitMetal, ricochet, impactGround;

        /// <summary>Bu mesafeden uzaktaki atışlar boğuk "uzak top" sesiyle çalar.</summary>
        const float FarDistance = 70f;
        /// <summary>Ses hızı (m/sn): uzaktaki patlamalar gecikmeli duyulur.</summary>
        const float SpeedOfSound = 343f;

        AudioListener listener;

        readonly List<AudioSource> pool = new List<AudioSource>();
        int poolIndex;
        AudioSource musicSource, ambienceSource, uiSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("Oyun Sesleri");
            DontDestroyOnLoad(go);
            go.AddComponent<GameAudio>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            cannon = new[] { SoundSynth.Cannon(0), SoundSynth.Cannon(1), SoundSynth.Cannon(2) };
            cannonFar = new[] { SoundSynth.Cannon(0, true), SoundSynth.Cannon(1, true) };
            explosion = new[] { SoundSynth.Explosion(0), SoundSynth.Explosion(1) };
            hitMetal = new[] { SoundSynth.HitMetal(0), SoundSynth.HitMetal(1), SoundSynth.HitMetal(2) };
            ricochet = new[] { SoundSynth.Ricochet(0), SoundSynth.Ricochet(1) };
            impactGround = new[] { SoundSynth.ImpactGround(0), SoundSynth.ImpactGround(1) };
            flyby = SoundSynth.Flyby();
            reload = SoundSynth.Reload();
            EngineClip = SoundSynth.Engine();
            TracksClip = SoundSynth.Tracks();
            click = SoundSynth.Click();
            killChime = SoundSynth.Chime("Kill", 880f, 1318.5f);
            assistChime = SoundSynth.Chime("Assist", 987.8f);
            promoteChime = SoundSynth.Chime("Promote", 659.3f, 880f, 1318.5f);
            wind = SoundSynth.Wind();
            menuMusic = SoundSynth.MenuMusic();
            whoosh = SoundSynth.Whoosh();
            whistle = SoundSynth.Whistle();
            shieldUp = SoundSynth.Chime("Shield", 392f, 523.3f);
            repairUp = SoundSynth.Chime("Repair", 523.3f, 659.3f, 784f);
            pickup = SoundSynth.Chime("Pickup", 784f, 1046.5f);

            for (int i = 0; i < 24; i++)
            {
                // Her 3B kaynak ayrı objede: konumları birbirinden bağımsız olmalı
                var child = new GameObject("Ses " + i);
                child.transform.SetParent(transform, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                // Gerçek hayattaki gibi: yakında yüksek, uzaklaştıkça logaritmik azalır
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 8f;
                s.maxDistance = 400f;
                s.dopplerLevel = 0f;
                s.spread = 60f;
                // Uzaklık arttıkça tizler söner (hava sesin tizini yutar)
                var lp = child.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = 22000f;
                pool.Add(s);
            }
            musicSource = Make2D(true);
            ambienceSource = Make2D(true);
            uiSource = Make2D(false);
            uiSource.ignoreListenerPause = true; // duraklatma menüsünde de tık sesi çalsın

            TankController.AnyFired += OnFired;
            TankController.AnyDamaged += OnDamaged;
            TankController.AnyDestroyed += OnDestroyedTank;
            TankController.AnyAbility += OnAbility;
            TankController.AnyPowerUp += OnPowerUp;
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameSettings.Changed += ApplyVolumes;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            TankController.AnyFired -= OnFired;
            TankController.AnyDamaged -= OnDamaged;
            TankController.AnyDestroyed -= OnDestroyedTank;
            TankController.AnyAbility -= OnAbility;
            TankController.AnyPowerUp -= OnPowerUp;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameSettings.Changed -= ApplyVolumes;
            Instance = null;
        }

        AudioSource Make2D(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool inMatch = FindAnyObjectByType<MatchManager>() != null;
            if (inMatch)
            {
                musicSource.Stop();
                ambienceSource.clip = wind;
                if (!ambienceSource.isPlaying) ambienceSource.Play();
            }
            else
            {
                ambienceSource.Stop();
                musicSource.clip = menuMusic;
                if (!musicSource.isPlaying) musicSource.Play();
            }
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            musicSource.volume = GameSettings.MusicVolume * 0.6f;
            ambienceSource.volume = GameSettings.SfxVolume * 0.35f;
            uiSource.volume = GameSettings.SfxVolume;
        }

        Vector3 ListenerPosition
        {
            get
            {
                if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
                return listener != null ? listener.transform.position : Vector3.zero;
            }
        }

        public float DistanceToListener(Vector3 p) => Vector3.Distance(p, ListenerPosition);

        static AudioClip Pick(AudioClip[] clips) => clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];

        /// <summary>Dünyada bir noktada ses çalar (3B). Uzaktaki sesler boğuklaşır ve ses hızıyla gecikir.</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch = 1f, bool delayByDistance = false)
        {
            if (clip == null) return;
            var s = pool[poolIndex];
            poolIndex = (poolIndex + 1) % pool.Count;
            s.Stop();
            s.transform.position = position;
            s.clip = clip;
            s.pitch = pitch * Random.Range(0.95f, 1.05f);
            s.volume = volume * GameSettings.SfxVolume;

            float dist = DistanceToListener(position);
            var lp = s.GetComponent<AudioLowPassFilter>();
            if (lp != null) lp.cutoffFrequency = Mathf.Lerp(22000f, 1800f, Mathf.Clamp01((dist - 15f) / 160f));

            if (delayByDistance && dist > 35f) s.PlayDelayed(dist / SpeedOfSound);
            else s.Play();
        }

        static bool IsPlayer(TankController t) =>
            t != null && MatchManager.Instance != null && MatchManager.Instance.PlayerTank == t;

        void OnFired(TankController tank)
        {
            float pitch = 1f;
            if (tank.ClassData != null)
            {
                // Büyük toplar daha kalın ses
                pitch = Mathf.Lerp(1.25f, 0.7f, Mathf.InverseLerp(80f, 320f, tank.ClassData.damage));
            }
            Vector3 pos = tank.muzzle != null ? tank.muzzle.position : tank.transform.position;
            bool far = DistanceToListener(pos) > FarDistance;
            PlayAt(far ? Pick(cannonFar) : Pick(cannon), pos, IsPlayer(tank) ? 1f : far ? 1f : 0.85f, pitch, true);
            if (IsPlayer(tank)) { pendingReload = tank; pendingReloadSince = Time.time; }
        }

        // Oyuncunun topu yeniden dolunca mekanik "şak-çunk"
        TankController pendingReload;
        float pendingReloadSince;

        void Update()
        {
            var t = pendingReload;
            if (t == null) return;
            if (t.IsDead) { pendingReload = null; return; }
            if (Time.time - pendingReloadSince > 0.4f && t.ReloadProgress >= 1f)
            {
                pendingReload = null;
                uiSource.PlayOneShot(reload, 0.35f);
            }
        }

        /// <summary>Kameranın yakınından geçen düşman mermisinin vızıltısı.</summary>
        public void PlayFlyby(Vector3 position) => PlayAt(flyby, position, 0.9f, Random.Range(0.85f, 1.15f));

        void OnDamaged(DamageInfo info)
        {
            if (info.zone == HitZone.Splash) return;
            float vol = IsPlayer(info.victim) ? 1f : 0.75f;
            // Ön zırha düşük hasarlı isabet: mermi seker
            bool bounce = info.zone == HitZone.Front && !info.killed && Random.value < 0.6f;
            PlayAt(Pick(hitMetal), info.point, vol, info.zone == HitZone.Front ? 1.1f : 0.95f, true);
            if (bounce) PlayAt(Pick(ricochet), info.point, vol * 0.7f, Random.Range(0.9f, 1.1f), true);
        }

        void OnDestroyedTank(TankController tank) => PlayAt(Pick(explosion), tank.transform.position, 1f, Random.Range(0.85f, 1f), true);

        void OnAbility(TankController tank, AbilityType type)
        {
            float vol = IsPlayer(tank) ? 1f : 0.7f;
            switch (type)
            {
                case AbilityType.Nitro: PlayAt(whoosh, tank.transform.position, vol); break;
                case AbilityType.Shield: PlayAt(shieldUp, tank.transform.position, vol); break;
                case AbilityType.Repair: PlayAt(repairUp, tank.transform.position, vol); break;
                case AbilityType.Barrage: PlayAt(whistle, tank.transform.position, vol * 0.8f); break;
            }
        }

        void OnPowerUp(TankController tank, PowerUpType type) =>
            PlayAt(pickup, tank.transform.position, IsPlayer(tank) ? 1f : 0.6f);

        public void PlayImpact(Vector3 point, bool hitTank, bool big)
        {
            if (hitTank && !big) return; // metal çınlaması zaten OnDamaged'da çalınıyor
            PlayAt(big ? Pick(explosion) : Pick(impactGround), point, big ? 0.8f : 0.65f, big ? 1.15f : 1f, true);
        }

        public void PlayClick() => uiSource.PlayOneShot(click, 0.6f);
        public void PlayKill() => uiSource.PlayOneShot(killChime, 0.7f);
        public void PlayAssist() => uiSource.PlayOneShot(assistChime, 0.6f);
        public void PlayPromote() => uiSource.PlayOneShot(promoteChime, 0.8f);
    }
}
