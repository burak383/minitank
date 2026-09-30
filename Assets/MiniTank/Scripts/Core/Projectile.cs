using System.Collections.Generic;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Mermi. Her karede bir ışın atarak ilerler (hızlı mermiler duvarın içinden geçmez).
    /// Yerçekimli (topçu) ve alan hasarlı atışları destekler.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public GameObject impactEffectPrefab;
        public LayerMask hitMask = ~0;

        /// <summary>Host'ta bir mermi fırlatıldığında (online istemcilere göndermek için).
        /// Parametreler: mermi, sahibi, başlangıç, hız, özel atış mı, alan yarıçapı, yerçekimi, ömür.</summary>
        public static event System.Action<Projectile, TankController, Vector3, Vector3, bool, float, bool, float> Launched;

        TankController owner;
        bool visualOnly;
        Vector3 velocity;
        float damage;
        float splashRadius;
        bool useGravity;
        float dieAt;
        bool launched;

        static readonly RaycastHit[] hits = new RaycastHit[8];
        static readonly Collider[] overlaps = new Collider[32];

        public void Launch(TankController shooter, Vector3 initialVelocity)
        {
            owner = shooter;
            velocity = initialVelocity;
            var d = shooter.ClassData;
            damage = d.damage * shooter.OutgoingDamageMultiplier;
            splashRadius = d.splashRadius;
            useGravity = d.projectileUsesGravity;
            dieAt = Time.time + d.projectileLifetime;
            launched = true;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
            if (Launched != null) Launched(this, shooter, transform.position, velocity, false, splashRadius, useGravity, d.projectileLifetime);
        }

        /// <summary>Online istemcide sadece görüntü: çarpınca patlar ama hasar vermez.</summary>
        public void LaunchVisual(TankController shooter, Vector3 initialVelocity, float splash, bool gravity, float lifetime)
        {
            owner = shooter;
            visualOnly = true;
            velocity = initialVelocity;
            damage = 0f;
            splashRadius = splash;
            useGravity = gravity;
            dieAt = Time.time + lifetime;
            launched = true;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        /// <summary>Özel değerlerle fırlatma (topçu atışı yeteneği gibi).</summary>
        public void LaunchCustom(TankController shooter, Vector3 initialVelocity, float customDamage, float splash, bool gravity, float lifetime)
        {
            owner = shooter;
            velocity = initialVelocity;
            damage = customDamage * (shooter != null ? shooter.OutgoingDamageMultiplier : 1f);
            splashRadius = splash;
            useGravity = gravity;
            dieAt = Time.time + lifetime;
            launched = true;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
            if (Launched != null) Launched(this, shooter, transform.position, velocity, true, splash, gravity, lifetime);
        }

        void Update()
        {
            if (!launched) return;

            float dt = Time.deltaTime;
            if (useGravity) velocity += Physics.gravity * dt;

            Vector3 step = velocity * dt;
            float distance = step.magnitude;

            if (distance > 0f)
            {
                int count = Physics.RaycastNonAlloc(transform.position, step / distance, hits, distance,
                                                    hitMask, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                int bestIndex = -1;
                for (int i = 0; i < count; i++)
                {
                    var tank = hits[i].collider.GetComponentInParent<TankController>();
                    if (tank != null && tank == owner) continue; // kendi tankını vurma
                    if (hits[i].distance < best) { best = hits[i].distance; bestIndex = i; }
                }

                if (bestIndex >= 0)
                {
                    var h = hits[bestIndex];
                    Explode(h.point, h.normal, h.collider.GetComponentInParent<TankController>());
                    return;
                }
            }

            CheckFlyby(step);
            transform.position += step;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);

            if (Time.time >= dieAt)
            {
                if (useGravity && splashRadius > 0f) Explode(transform.position, Vector3.up, null);
                else Destroy(gameObject);
            }
        }

        bool flybyPlayed;

        /// <summary>Düşman mermisi kameranın yakınından geçerken vızıltı.</summary>
        void CheckFlyby(Vector3 step)
        {
            if (flybyPlayed || GameAudio.Instance == null) return;
            var mm = MatchManager.Instance;
            if (mm == null || owner == null || owner == mm.PlayerTank) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            // Kameranın bu adım içinde merminin yoluna en yakın noktası
            Vector3 a = transform.position, ab = step;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(c - a, ab) / ab.sqrMagnitude) : 0f;
            Vector3 closest = a + ab * t;
            if ((closest - c).sqrMagnitude < 6f * 6f)
            {
                flybyPlayed = true;
                GameAudio.Instance.PlayFlyby(closest);
            }
        }

        void Explode(Vector3 point, Vector3 normal, TankController directHit)
        {
            if (visualOnly) { }
            else if (splashRadius > 0f)
            {
                var damaged = new HashSet<TankController>();
                int count = Physics.OverlapSphereNonAlloc(point, splashRadius, overlaps, hitMask,
                                                          QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var tank = overlaps[i].GetComponentInParent<TankController>();
                    if (tank == null || !damaged.Add(tank)) continue;

                    if (tank == directHit)
                    {
                        tank.TakeHit(damage, owner, point, velocity.normalized, false);
                        continue;
                    }
                    float dist = Vector3.Distance(point, overlaps[i].ClosestPoint(point));
                    float falloff = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(dist / splashRadius));
                    tank.TakeHit(damage * falloff, owner, point, velocity.normalized, true);
                }
            }
            else if (directHit != null)
            {
                directHit.TakeHit(damage, owner, point, velocity.normalized, false);
            }

            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayImpact(point, directHit != null, splashRadius > 0f);

            if (impactEffectPrefab != null)
                Destroy(Instantiate(impactEffectPrefab, point, Quaternion.LookRotation(normal)), 3f);

            Destroy(gameObject);
        }
    }
}
