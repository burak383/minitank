using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Sadece görsel canlılık: palet tekerleklerinin dönmesi, atışta namlu geri tepmesi,
    /// hareket ederken toz ve anten sallanması. Oyun mantığını etkilemez.
    /// </summary>
    [RequireComponent(typeof(TankController))]
    public class TankVisuals : MonoBehaviour
    {
        [Header("Tekerlekler")]
        public Transform[] leftWheels;
        public Transform[] rightWheels;
        public float wheelRadius = 0.35f;
        public float trackHalfWidth = 1.5f;

        [Header("Geri tepme")]
        public Transform recoilPart;
        public float recoilDistance = 0.4f;
        public float recoilReturnSpeed = 3f;

        [Header("Efektler")]
        public ParticleSystem dust;
        public float dustPerSpeed = 1.5f;
        public Transform antenna;

        TankController tank;
        Rigidbody rb;
        Vector3 recoilRest;
        float recoil;
        float lastYaw;
        float antennaSwing;
        float antennaVelocity;
        float lastSpeed;
        Vector3 lastPos;
        AudioSource engine, tracks;
        GameObject bubble;
        Material bubbleMat;

        void Awake()
        {
            tank = GetComponent<TankController>();
            rb = GetComponent<Rigidbody>();
            if (recoilPart != null) recoilRest = recoilPart.localPosition;
            tank.Fired += OnFired;
        }

        void OnDestroy()
        {
            if (tank != null) tank.Fired -= OnFired;
        }

        void OnEnable()
        {
            lastYaw = transform.eulerAngles.y;
            recoil = 0f;
            lastPos = transform.position;
        }

        void OnFired(TankController t)
        {
            recoil = 1f;
            antennaVelocity -= 60f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Online kukla tanklar kinematik: hızı konum farkından hesapla
            Vector3 vel = rb != null && !rb.isKinematic ? rb.linearVelocity : (transform.position - lastPos) / dt;
            lastPos = transform.position;
            float forward = transform.InverseTransformDirection(vel).z;
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt * Mathf.Deg2Rad;
            lastYaw = yaw;

            // Sağa dönerken sol palet daha hızlı döner
            float leftSpeed = forward + yawRate * trackHalfWidth;
            float rightSpeed = forward - yawRate * trackHalfWidth;
            SpinWheels(leftWheels, leftSpeed, dt);
            SpinWheels(rightWheels, rightSpeed, dt);

            if (recoilPart != null)
            {
                recoil = Mathf.MoveTowards(recoil, 0f, recoilReturnSpeed * dt);
                float eased = recoil * recoil; // hızlı geri tep, yavaş dön
                recoilPart.localPosition = recoilRest - Vector3.forward * (eased * recoilDistance);
            }

            if (dust != null)
            {
                var emission = dust.emission;
                float speed = Mathf.Abs(forward) + Mathf.Abs(yawRate) * trackHalfWidth * 0.5f;
                emission.rateOverTime = tank.IsBoosting ? 30f : speed > 2f ? Mathf.Min(speed * dustPerSpeed, 25f) : 0f;
            }

            if (antenna != null)
            {
                // Hızlanma ve frenlemede yaylanan anten
                float accel = (forward - lastSpeed) / dt;
                antennaVelocity += (-accel * 4f - antennaSwing * 60f - antennaVelocity * 6f) * dt;
                antennaSwing = Mathf.Clamp(antennaSwing + antennaVelocity * dt, -25f, 25f);
                antenna.localRotation = Quaternion.Euler(antennaSwing, 0f, 0f);
            }
            lastSpeed = forward;

            UpdateEngineSound(forward, yawRate);
            UpdateAbilityVisual();
        }

        /// <summary>Kalkan (takım renginde) ve onarım (yeşil) için tankı saran yarı saydam küre.</summary>
        void UpdateAbilityVisual()
        {
            var d = tank.ClassData;
            bool show = d != null && tank.AbilityActive && (d.ability == AbilityType.Shield || d.ability == AbilityType.Repair);
            if (!show)
            {
                if (bubble != null && bubble.activeSelf) bubble.SetActive(false);
                return;
            }
            if (bubble == null)
            {
                var ps = GetComponentInChildren<ParticleSystemRenderer>(true);
                if (ps == null || ps.sharedMaterial == null) return;
                bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bubble.name = "Yetenek Kalkanı";
                Destroy(bubble.GetComponent<Collider>());
                bubble.transform.SetParent(transform, false);
                var col = GetComponent<BoxCollider>();
                Vector3 size = col != null ? col.size : new Vector3(3f, 2.5f, 5f);
                bubble.transform.localPosition = col != null ? col.center : Vector3.up * 1.2f;
                bubble.transform.localScale = new Vector3(size.x, size.y, size.z) * 1.35f;
                bubbleMat = new Material(ps.sharedMaterial);
                if (bubbleMat.HasProperty("_BaseMap")) bubbleMat.SetTexture("_BaseMap", null);
                bubble.GetComponent<Renderer>().sharedMaterial = bubbleMat;
                bubble.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!bubble.activeSelf) bubble.SetActive(true);
            Color c = d.ability == AbilityType.Repair ? new Color(0.3f, 1f, 0.4f) : TeamColors.Get(tank.Team);
            c.a = 0.18f + 0.07f * Mathf.Sin(Time.time * 8f);
            bubbleMat.SetColor("_BaseColor", c);
        }

        void UpdateEngineSound(float forward, float yawRate)
        {
            if (engine == null)
            {
                if (GameAudio.Instance == null || GameAudio.Instance.EngineClip == null) return;
                engine = gameObject.AddComponent<AudioSource>();
                engine.clip = GameAudio.Instance.EngineClip;
                engine.loop = true;
                engine.spatialBlend = 1f;
                engine.rolloffMode = AudioRolloffMode.Linear;
                engine.minDistance = 4f;
                engine.maxDistance = 70f;
                engine.dopplerLevel = 0f;
                engine.time = Random.Range(0f, 1.9f);
                engine.Play();

                // Palet şıkırtısı: hızla artar
                tracks = gameObject.AddComponent<AudioSource>();
                tracks.clip = GameAudio.Instance.TracksClip;
                tracks.loop = true;
                tracks.spatialBlend = 1f;
                tracks.rolloffMode = AudioRolloffMode.Linear;
                tracks.minDistance = 3f;
                tracks.maxDistance = 55f;
                tracks.dopplerLevel = 0f;
                tracks.volume = 0f;
                tracks.time = Random.Range(0f, 1.9f);
                tracks.Play();
            }
            if (tank.ClassData == null) return;

            float load = Mathf.Clamp01(Mathf.Abs(forward) / Mathf.Max(1f, tank.ClassData.moveSpeed) + Mathf.Abs(yawRate) * 0.15f);
            // Ağır tanklar daha kalın motor sesi
            float classPitch = Mathf.Lerp(1.2f, 0.8f, Mathf.InverseLerp(600f, 1700f, tank.ClassData.maxHealth));
            engine.pitch = Mathf.Lerp(0.55f, 1.15f, load) * classPitch;
            bool isPlayer = MatchManager.Instance != null && MatchManager.Instance.PlayerTank == tank;
            engine.volume = (0.2f + 0.4f * load) * GameSettings.SfxVolume * (isPlayer ? 0.9f : 0.6f);

            if (tracks != null)
            {
                float move = Mathf.Clamp01(Mathf.Abs(forward) / Mathf.Max(1f, tank.ClassData.moveSpeed) + Mathf.Abs(yawRate) * 0.1f);
                tracks.pitch = Mathf.Lerp(0.6f, 1.3f, move);
                tracks.volume = move * move * 0.55f * GameSettings.SfxVolume * (isPlayer ? 0.8f : 0.6f);
            }
        }

        void SpinWheels(Transform[] wheels, float speed, float dt)
        {
            if (wheels == null) return;
            float angle = speed / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg * dt;
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].Rotate(angle, 0f, 0f, Space.Self);
        }
    }
}
