using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Tankın tüm oyun mantığı: hareket, kule, atış, can, ölüm ve yeniden doğma.
    /// Girdi bir ITankInputSource'tan gelir (oyuncu veya bot). Online'a geçişte
    /// Simulate() metodu Fusion'ın FixedUpdateNetwork()'ünden çağrılacak.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TankController : MonoBehaviour
    {
        [Header("Parçalar")]
        public Transform turret;
        public Transform gun;
        public Transform muzzle;

        [Header("Takım rengi alacak parçalar")]
        public Renderer[] teamColorRenderers;

        [Header("Efektler (opsiyonel)")]
        public GameObject muzzleFlashPrefab;
        public GameObject deathEffectPrefab;

        static readonly List<TankController> active = new List<TankController>();
        /// <summary>Sahnedeki canlı tüm tanklar.</summary>
        public static IReadOnlyList<TankController> All => active;

        public TankClassData ClassData { get; private set; }
        public Team Team { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsBot { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth => ClassData != null ? ClassData.maxHealth * upgradeHealth : 1f;
        public bool IsDead => Health <= 0f;
        public int Kills { get; set; }
        public int Assists { get; set; }

        /// <summary>Ölmeden önceki bu kadar saniye içinde hasar veren düşmanlar asist alır.</summary>
        public const float AssistWindow = 15f;

        /// <summary>Son ölümde asist alan tanklar (öldüren hariç).</summary>
        public IReadOnlyList<TankController> LastAssisters => lastAssisters;
        readonly List<TankController> lastAssisters = new List<TankController>();
        readonly Dictionary<TankController, float> recentAttackers = new Dictionary<TankController, float>();
        public int Deaths { get; set; }
        public bool InputEnabled { get; set; } = true;
        public float CurrentSpeed => currentSpeed;

        /// <summary>0 = yeni ateşlendi, 1 = atışa hazır.</summary>
        public float ReloadProgress
        {
            get
            {
                if (ClassData == null || ClassData.fireCooldown <= 0f) return 1f;
                return Mathf.Clamp01(1f - (nextFireTime - Time.time) / ClassData.fireCooldown);
            }
        }

        public event Action<TankController, float> Damaged;           // tank, hasar
        public event Action<TankController, TankController> Died;     // ölen, öldüren
        public event Action<TankController> Fired;

        /// <summary>Herhangi bir tank hasar aldığında (arayüz ve sesler için).</summary>
        public static event Action<DamageInfo> AnyDamaged;
        /// <summary>Herhangi bir tank ateş ettiğinde.</summary>
        public static event Action<TankController> AnyFired;
        /// <summary>Herhangi bir tank imha olduğunda.</summary>
        public static event Action<TankController> AnyDestroyed;

        Rigidbody rb;
        ITankInputSource inputSource;
        float currentSpeed;
        float nextFireTime;
        MaterialPropertyBlock propertyBlock;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() { active.Remove(this); }

        public void Init(TankClassData data, Team team, string displayName, bool isBot, ITankInputSource source)
        {
            ClassData = data;
            Team = team;
            DisplayName = displayName;
            IsBot = isBot;
            inputSource = source;
            upgradeSpeed = upgradeHealth = upgradeDamage = upgradeReload = 1f;
            camo = null;
            Health = MaxHealth;
            nextFireTime = 0f;
            ApplyTeamColor();
        }

        /// <summary>
        /// Adı "TankBody" olan materyaller takım gövde boyasını, "TankMarker" olanlar parlak takım rengini alır.
        /// </summary>
        void ApplyTeamColor()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            Color body = TeamColors.Body(Team);
            Color marker = TeamColors.Get(Team);
            Texture2D camoTex = camo != null ? CamoTextures.Get(camo) : null;
            if (camo != null) body = camoTex != null ? Color.white : camo.a;

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    Color? c = null;
                    if (mats[i].name.StartsWith("TankBody")) c = body;
                    else if (mats[i].name.StartsWith("TankMarker")) c = marker;
                    if (c == null) continue;
                    r.GetPropertyBlock(propertyBlock, i);
                    propertyBlock.SetColor("_BaseColor", c.Value);
                    propertyBlock.SetColor("_Color", c.Value);
                    if (camo != null && mats[i].name.StartsWith("TankBody"))
                    {
                        if (camoTex != null) { propertyBlock.SetTexture("_BaseMap", camoTex); propertyBlock.SetTexture("_MainTex", camoTex); }
                        propertyBlock.SetFloat("_Metallic", camo.metallic);
                        propertyBlock.SetFloat("_Smoothness", camo.smoothness);
                    }
                    r.SetPropertyBlock(propertyBlock, i);
                }
            }

            // Eski yöntem: tüm renderer'ı takım rengine boya
            if (teamColorRenderers == null) return;
            foreach (var r in teamColorRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", marker);
                propertyBlock.SetColor("_Color", marker);
                r.SetPropertyBlock(propertyBlock);
            }
        }

        void FixedUpdate()
        {
            if (IsDead || ClassData == null || IsPuppet) return;
            TankInputData input = (InputEnabled && inputSource != null) ? inputSource.GetInput() : default(TankInputData);
            if (!InputEnabled) input.AimPoint = muzzle ? muzzle.position + muzzle.forward * 20f : transform.position + transform.forward * 20f;
            Simulate(input, Time.fixedDeltaTime);
        }

        public void Simulate(TankInputData input, float dt)
        {
            var d = ClassData;

            // Gövde: ileri/geri + kendi ekseninde dönüş
            float targetSpeed = (input.Move.y >= 0f ? input.Move.y * d.moveSpeed : input.Move.y * d.reverseSpeed) * SpeedMultiplier;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, d.acceleration * dt);

            float turn = input.Move.x * d.hullTurnSpeed * dt * (SpeedMultiplier > 1f ? 1.25f : 1f);
            if (Mathf.Abs(turn) > 0.0001f)
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));

            Vector3 velocity = transform.forward * currentSpeed;
            velocity.y = rb.linearVelocity.y;
            rb.linearVelocity = velocity;

            AimAt(input.AimPoint, dt);

            if (input.Fire) TryFire();
            if (input.Ability) TryAbility(input.AimPoint);
        }

        void AimAt(Vector3 point, float dt)
        {
            var d = ClassData;

            if (turret != null)
            {
                Transform parent = turret.parent != null ? turret.parent : transform;
                Vector3 local = parent.InverseTransformPoint(point) - turret.localPosition;
                if (local.sqrMagnitude > 0.01f)
                {
                    float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    turret.localRotation = Quaternion.RotateTowards(
                        turret.localRotation, Quaternion.Euler(0f, yaw, 0f), d.turretTurnSpeed * dt);
                }
            }

            if (gun != null)
            {
                Transform parent = gun.parent != null ? gun.parent : transform;
                Vector3 local = parent.InverseTransformDirection(point - gun.position);
                float horizontal = new Vector2(local.x, local.z).magnitude;
                float elevation;

                if (d.projectileUsesGravity)
                    elevation = BallisticAngle(horizontal, local.y, d.projectileSpeed);
                else
                    elevation = Mathf.Atan2(local.y, Mathf.Max(horizontal, 0.01f)) * Mathf.Rad2Deg;

                elevation = Mathf.Clamp(elevation, d.minGunPitch, d.maxGunPitch);
                // Unity'de X ekseninde negatif açı namluyu yukarı kaldırır
                gun.localRotation = Quaternion.RotateTowards(
                    gun.localRotation, Quaternion.Euler(-elevation, 0f, 0f), d.turretTurnSpeed * dt);
            }
        }

        /// <summary>Hedefe ulaşmak için gereken alçak kavis açısı (derece).</summary>
        static float BallisticAngle(float x, float y, float v)
        {
            float g = Mathf.Abs(Physics.gravity.y);
            if (x < 0.1f) return 45f;
            float v2 = v * v;
            float root = v2 * v2 - g * (g * x * x + 2f * y * v2);
            if (root < 0f) return 45f; // menzil dışı: en uzağa at
            return Mathf.Atan((v2 - Mathf.Sqrt(root)) / (g * x)) * Mathf.Rad2Deg;
        }

        void TryFire()
        {
            var d = ClassData;
            if (Time.time < nextFireTime || d.projectilePrefab == null || muzzle == null) return;
            nextFireTime = Time.time + d.fireCooldown * ReloadMultiplier;

            Projectile p = Instantiate(d.projectilePrefab, muzzle.position, muzzle.rotation);
            p.Launch(this, muzzle.forward * d.projectileSpeed);
            PlayFireEffects();
        }

        void PlayFireEffects()
        {
            Fired?.Invoke(this);
            AnyFired?.Invoke(this);
            if (muzzleFlashPrefab != null && muzzle != null)
                Destroy(Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation, muzzle), 1f);
        }

        // ================================================================== Yetenek ve güçlendirmeler

        float abilityReadyAt;
        float abilityActiveUntil;
        readonly float[] buffUntil = new float[4];

        public const float PowerUpDuration = 10f;

        public static event Action<TankController, AbilityType> AnyAbility;
        public static event Action<TankController, PowerUpType> AnyPowerUp;

        /// <summary>0 = yeni kullanıldı, 1 = hazır.</summary>
        public float AbilityReady01
        {
            get
            {
                if (ClassData == null || ClassData.ability == AbilityType.None || ClassData.abilityCooldown <= 0f) return 1f;
                return Mathf.Clamp01(1f - (abilityReadyAt - Time.time) / ClassData.abilityCooldown);
            }
        }
        public bool AbilityActive => Time.time < abilityActiveUntil;
        public bool IsShielded => AbilityActive && ClassData != null && ClassData.ability == AbilityType.Shield;
        public bool IsBoosting => AbilityActive && ClassData != null && ClassData.ability == AbilityType.Nitro;
        public float BuffRemaining(PowerUpType type) => Mathf.Max(0f, buffUntil[(int)type] - Time.time);

        float upgradeSpeed = 1f, upgradeHealth = 1f, upgradeDamage = 1f, upgradeReload = 1f;
        CamoDef camo;

        /// <summary>Garajdaki geliştirme seviyelerini uygular (Init'ten sonra çağrılır).</summary>
        public void ApplyUpgrades(int engine, int armor, int gun)
        {
            upgradeSpeed = Economy.SpeedMultiplier(engine);
            upgradeHealth = Economy.HealthMultiplier(armor);
            upgradeDamage = Economy.DamageMultiplier(gun);
            upgradeReload = Economy.ReloadMultiplier(gun);
            Health = MaxHealth;
        }

        /// <summary>Kamuflajı sadece bu tankın gövdesine uygular (takım işaretleri değişmez).</summary>
        public void ApplyCamo(string camoId)
        {
            camo = string.IsNullOrEmpty(camoId) || camoId == Economy.DefaultCamo ? null : Economy.GetCamo(camoId);
            ApplyTeamColor();
        }

        float SpeedMultiplier => upgradeSpeed *
            (IsBoosting ? Mathf.Max(1f, ClassData.abilityPower) : 1f) * (BuffRemaining(PowerUpType.Speed) > 0f ? 1.35f : 1f);
        float ReloadMultiplier => upgradeReload * (BuffRemaining(PowerUpType.Reload) > 0f ? 0.6f : 1f);
        /// <summary>Giden hasar çarpanı (hasar güçlendirmesi).</summary>
        public float OutgoingDamageMultiplier => upgradeDamage * (BuffRemaining(PowerUpType.Damage) > 0f ? 1.35f : 1f);

        void TryAbility(Vector3 aimPoint)
        {
            var d = ClassData;
            if (d == null || d.ability == AbilityType.None || IsDead || Time.time < abilityReadyAt) return;
            abilityReadyAt = Time.time + d.abilityCooldown;

            switch (d.ability)
            {
                case AbilityType.Nitro:
                case AbilityType.Shield:
                    abilityActiveUntil = Time.time + d.abilityDuration;
                    break;
                case AbilityType.Repair:
                    Health = Mathf.Min(MaxHealth, Health + MaxHealth * Mathf.Clamp01(d.abilityPower));
                    abilityActiveUntil = Time.time + 1f;
                    break;
                case AbilityType.Barrage:
                    abilityActiveUntil = Time.time + 2.5f;
                    StartCoroutine(Barrage(aimPoint));
                    break;
            }
            LastAbilityTarget = aimPoint;
            AnyAbility?.Invoke(this, d.ability);
        }

        /// <summary>Topçu atışı: hedef noktaya kısa bir uyarıdan sonra 6 mermi yağar.</summary>
        IEnumerator Barrage(Vector3 target)
        {
            RaycastHit ground;
            if (Physics.Raycast(target + Vector3.up * 60f, Vector3.down, out ground, 120f, ~0, QueryTriggerInteraction.Ignore))
                target = ground.point;

            LastAbilityTarget = target;
            var marker = CreateZoneMarker(target, 8f, new Color(1f, 0.2f, 0.1f, 0.35f));
            yield return new WaitForSeconds(1.5f);

            var d = ClassData;
            var prefab = d != null ? d.projectilePrefab : null;
            for (int i = 0; i < 6 && prefab != null; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * 7f;
                Vector3 start = target + new Vector3(r.x, 45f, r.y);
                var p = Instantiate(prefab, start, Quaternion.LookRotation(Vector3.down));
                p.LaunchCustom(this, Vector3.down * 50f + new Vector3(r.x, 0f, r.y) * 0.2f, d.abilityPower, 5f, true, 4f);
                yield return new WaitForSeconds(0.18f);
            }
            if (marker != null) Destroy(marker, 1f);
        }

        /// <summary>Yerde yarı saydam uyarı dairesi (topçu atışı alanı).</summary>
        GameObject CreateZoneMarker(Vector3 position, float radius, Color color)
        {
            var baseMat = GetComponentInChildren<ParticleSystemRenderer>(true);
            if (baseMat == null || baseMat.sharedMaterial == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.name = "Topçu Uyarısı";
            go.transform.position = position + Vector3.up * 0.05f;
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            var mat = new Material(baseMat.sharedMaterial);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Güçlendirme toplandığında.</summary>
        public void ApplyPowerUp(PowerUpType type)
        {
            if (IsDead) return;
            if (type == PowerUpType.Health) Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.35f);
            else buffUntil[(int)type] = Time.time + PowerUpDuration;
            AnyPowerUp?.Invoke(this, type);
        }

        /// <summary>
        /// Mermi isabeti: merminin geldiği yöne göre zırh çarpanı uygulanır.
        /// Önden gelen isabet az, yandan normal, arkadan çok hasar verir. Alan hasarında zırh yoktur.
        /// </summary>
        public void TakeHit(float baseDamage, TankController attacker, Vector3 point, Vector3 travelDirection, bool splash)
        {
            if (IsDead || ClassData == null) return;
            HitZone zone = HitZone.Splash;
            float multiplier = 1f;
            if (!splash && travelDirection.sqrMagnitude > 0.0001f)
            {
                // Tankın kendi ekseninde, mermiyi atan tarafa doğru yön
                Vector3 local = transform.InverseTransformDirection(-travelDirection);
                float angle = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
                if (angle <= 45f) { zone = HitZone.Front; multiplier = ClassData.frontArmor; }
                else if (angle >= 135f) { zone = HitZone.Rear; multiplier = ClassData.rearArmor; }
                else { zone = HitZone.Side; multiplier = ClassData.sideArmor; }
            }
            ApplyDamage(baseDamage * multiplier, attacker, point, travelDirection, zone);
        }

        public void TakeDamage(float amount, TankController attacker) =>
            ApplyDamage(amount, attacker, transform.position, Vector3.zero, HitZone.Splash);

        void ApplyDamage(float amount, TankController attacker, Vector3 point, Vector3 direction, HitZone zone)
        {
            if (IsDead || amount <= 0f) return;
            // Dost ateşi ve kendine hasar kapalı
            if (attacker != null && attacker.Team == Team) return;

            if (IsShielded) amount *= 1f - Mathf.Clamp01(ClassData.abilityPower);
            Health = Mathf.Max(0f, Health - amount);
            if (attacker != null) recentAttackers[attacker] = Time.time;
            Damaged?.Invoke(this, amount);
            AnyDamaged?.Invoke(new DamageInfo
            {
                victim = this, attacker = attacker, amount = amount,
                point = point, direction = direction, zone = zone, killed = Health <= 0f,
            });
            if (Health <= 0f) Die(attacker);
        }

        void Die(TankController killer)
        {
            currentSpeed = 0f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (deathEffectPrefab != null)
                Destroy(Instantiate(deathEffectPrefab, transform.position, Quaternion.identity), 5f);

            lastAssisters.Clear();
            foreach (var pair in recentAttackers)
            {
                var attacker = pair.Key;
                if (attacker == null || attacker == killer || attacker.Team == Team) continue;
                if (Time.time - pair.Value <= AssistWindow) lastAssisters.Add(attacker);
            }
            recentAttackers.Clear();

            Died?.Invoke(this, killer);
            AnyDestroyed?.Invoke(this);
            gameObject.SetActive(false); // MatchManager yeniden doğurur
        }

        // ================================================================== Online (Photon Fusion)
        // Online maçta oyunu sadece host simüle eder. Diğer cihazlardaki tanklar "kukla"dır:
        // konumları ve durumları ağdan gelir, sadece görüntü ve ses üretirler.

        /// <summary>Bu tank ağdan gelen durumla hareket eden bir kukla mı (online istemci)?</summary>
        public bool IsPuppet { get; private set; }
        /// <summary>Kaçıncı kez doğduğu (host artırır, istemciler yeniden doğuşu buradan anlar).</summary>
        public int SpawnCount { get; private set; }
        /// <summary>Son yetenek kullanımının hedef noktası (topçu atışı için).</summary>
        public Vector3 LastAbilityTarget { get; private set; }

        public void MakePuppet()
        {
            IsPuppet = true;
            if (rb == null) rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            var bot = GetComponent<BotInputSource>();
            if (bot != null) bot.enabled = false;
        }

        /// <summary>Kendi tankımızın kulesini ağ gecikmesi olmadan çevirir (istemci tahmini).</summary>
        public void AimVisual(Vector3 point, float dt)
        {
            if (ClassData != null) AimAt(point, dt);
        }

        public void SetTurretAngles(float turretYaw, float gunPitch)
        {
            if (turret != null) turret.localRotation = Quaternion.Euler(0f, turretYaw, 0f);
            if (gun != null) gun.localRotation = Quaternion.Euler(gunPitch, 0f, 0f);
        }

        public float TurretYaw => turret != null ? turret.localEulerAngles.y : 0f;
        public float GunPitch
        {
            get
            {
                if (gun == null) return 0f;
                float x = gun.localEulerAngles.x;
                return x > 180f ? x - 360f : x;
            }
        }

        /// <summary>Host tarafında: ağa gönderilecek zamanlayıcılar (kalan saniye).</summary>
        public float ReloadRemaining => Mathf.Max(0f, nextFireTime - Time.time);
        public float AbilityCooldownRemaining => Mathf.Max(0f, abilityReadyAt - Time.time);
        public float AbilityActiveRemaining => Mathf.Max(0f, abilityActiveUntil - Time.time);

        /// <summary>İstemci tarafında: host'tan gelen durumu uygular.</summary>
        public void ApplyNetState(float health, float reloadRemaining, float abilityRemaining, float abilityActiveRemaining,
                                  float speed, int kills, int deaths, int assists,
                                  float buffHealth, float buffSpeed, float buffDamage, float buffReload)
        {
            Health = health;
            nextFireTime = Time.time + reloadRemaining;
            abilityReadyAt = Time.time + abilityRemaining;
            abilityActiveUntil = Time.time + abilityActiveRemaining;
            currentSpeed = speed;
            Kills = kills; Deaths = deaths; Assists = assists;
            buffUntil[0] = Time.time + buffHealth;
            buffUntil[1] = Time.time + buffSpeed;
            buffUntil[2] = Time.time + buffDamage;
            buffUntil[3] = Time.time + buffReload;
        }

        /// <summary>İstemci: host'ta atılan merminin görsel kopyası (hasar vermez).</summary>
        public void PlayShotVisual(Vector3 position, Vector3 velocity, bool custom, float splash, bool gravity, float lifetime)
        {
            var d = ClassData;
            if (d == null || d.projectilePrefab == null) return;
            Projectile p = Instantiate(d.projectilePrefab, position, Quaternion.LookRotation(velocity.sqrMagnitude > 0.01f ? velocity : Vector3.forward));
            p.LaunchVisual(this, velocity, splash, gravity, lifetime);
            if (!custom) PlayFireEffects();
        }

        /// <summary>İstemci: yetenek kullanımının ses/görüntüsü.</summary>
        public void PlayAbilityVisual(AbilityType ability, Vector3 target)
        {
            LastAbilityTarget = target;
            if (ability == AbilityType.Barrage && gameObject.activeInHierarchy)
            {
                RaycastHit ground;
                if (Physics.Raycast(target + Vector3.up * 60f, Vector3.down, out ground, 120f, ~0, QueryTriggerInteraction.Ignore))
                    target = ground.point;
                var marker = CreateZoneMarker(target, 8f, new Color(1f, 0.2f, 0.1f, 0.35f));
                if (marker != null) Destroy(marker, 2.8f);
            }
            AnyAbility?.Invoke(this, ability);
        }

        /// <summary>İstemci: güçlendirme toplama bildirimi.</summary>
        public void PlayPowerUpVisual(PowerUpType type) => AnyPowerUp?.Invoke(this, type);

        /// <summary>İstemci: host'ta gerçekleşen hasarın olaylarını tetikler (hasar sayıları, sesler).</summary>
        public void RaiseDamageRemote(TankController attacker, float amount, Vector3 point, Vector3 direction, HitZone zone, bool killed)
        {
            Damaged?.Invoke(this, amount);
            AnyDamaged?.Invoke(new DamageInfo
            {
                victim = this, attacker = attacker, amount = amount,
                point = point, direction = direction, zone = zone, killed = killed,
            });
        }

        bool puppetDeathShown;

        /// <summary>İstemci: tankın ölümü. raiseEvents=false ise sessizce gizlenir (geç katılma).</summary>
        public void PuppetDie(TankController killer, List<TankController> assisters, bool raiseEvents)
        {
            Health = 0f;
            if (!puppetDeathShown && gameObject.activeSelf && deathEffectPrefab != null && raiseEvents)
                Destroy(Instantiate(deathEffectPrefab, transform.position, Quaternion.identity), 5f);
            if (raiseEvents)
            {
                lastAssisters.Clear();
                if (assisters != null) lastAssisters.AddRange(assisters);
                Died?.Invoke(this, killer);
                AnyDestroyed?.Invoke(this);
            }
            puppetDeathShown = true;
            gameObject.SetActive(false);
        }

        /// <summary>İstemci: yeniden doğma.</summary>
        public void PuppetRespawn(Vector3 position, Quaternion rotation, int spawnCount)
        {
            SpawnCount = spawnCount;
            puppetDeathShown = false;
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(position, rotation);
            if (rb != null) { rb.position = position; rb.rotation = rotation; }
            Health = MaxHealth;
        }

        /// <summary>Canı maksimumun belli bir oranına ayarlar (eğitim hedefleri).</summary>
        public void SetHealthFraction(float fraction) => Health = Mathf.Max(1f, MaxHealth * Mathf.Clamp01(fraction));

        /// <summary>Host: oyuncu adı değişince (oyuncu çıkıp yerine bot geçince).</summary>
        public void SetControl(string displayName, bool isBot, ITankInputSource source)
        {
            DisplayName = displayName;
            IsBot = isBot;
            inputSource = source;
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            SpawnCount++;
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(position, rotation);
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (turret != null) turret.localRotation = Quaternion.identity;
            if (gun != null) gun.localRotation = Quaternion.identity;

            Health = MaxHealth;
            recentAttackers.Clear();
            currentSpeed = 0f;
            nextFireTime = Time.time + 0.5f;
            abilityActiveUntil = 0f;
            for (int i = 0; i < buffUntil.Length; i++) buffUntil[i] = 0f;
        }
    }
}
