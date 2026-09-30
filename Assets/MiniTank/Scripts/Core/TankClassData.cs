using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Tank sınıfı (Hafif, Orta, Ağır, Topçu...). Yeni sınıf eklemek için:
    /// Project penceresinde sağ tık > Create > MiniTank > Tank Class.
    /// </summary>
    [CreateAssetMenu(fileName = "TankClass", menuName = "MiniTank/Tank Class")]
    public class TankClassData : ScriptableObject
    {
        [Header("Genel")]
        public string displayName = "Orta Tank";
        [TextArea] public string description;
        public GameObject prefab;

        [Header("Dayanıklılık")]
        public float maxHealth = 1000f;

        [Header("Hareket")]
        public float moveSpeed = 10f;
        public float reverseSpeed = 5f;
        public float acceleration = 14f;
        public float hullTurnSpeed = 80f;

        [Header("Kule ve namlu")]
        public float turretTurnSpeed = 100f;
        public float minGunPitch = -8f;
        public float maxGunPitch = 15f;

        [Header("Silah")]
        public Projectile projectilePrefab;
        public float damage = 180f;
        public float fireCooldown = 2f;
        public float projectileSpeed = 95f;
        public float projectileLifetime = 2.5f;
        [Tooltip("Topçu gibi kavisli atış yapan sınıflar için açın. Namlu açısı otomatik hesaplanır.")]
        public bool projectileUsesGravity;
        [Tooltip("0 ise sadece doğrudan isabet hasar verir. 0'dan büyükse alan hasarı.")]
        public float splashRadius;

        [Header("Zırh (hasar çarpanı: küçük = dayanıklı)")]
        [Tooltip("Önden gelen isabetlerde hasar çarpanı.")]
        public float frontArmor = 0.75f;
        public float sideArmor = 1f;
        [Tooltip("Arkadan gelen isabetlerde hasar çarpanı.")]
        public float rearArmor = 1.35f;
        [HideInInspector] public bool armorConfigured;

        [Header("Yetenek")]
        public AbilityType ability = AbilityType.None;
        public string abilityName = "Yetenek";
        public float abilityCooldown = 15f;
        [Tooltip("Etki süresi (saniye). Onarım ve topçu atışında kullanılmaz.")]
        public float abilityDuration = 4f;
        [Tooltip("Nitro: hız çarpanı, Onarım: iyileşme oranı, Kalkan: hasar azaltma oranı, Topçu atışı: mermi başı hasar.")]
        public float abilityPower = 0.5f;
        [HideInInspector] public bool abilityConfigured;

        [Header("Özel 3D model (isteğe bağlı)")]
        [Tooltip("Asset Store'dan veya kendi yaptığın bir tank modeli. Boşsa kodla üretilen model kullanılır. " +
                 "Modelde adı 'Turret' (taret), 'Gun'/'Barrel' (namlu) ve isteğe bağlı 'Muzzle' (namlu ağzı) içeren parçalar varsa otomatik bulunur.")]
        public GameObject customModel;
        public float customModelScale = 1f;
        public Vector3 customModelOffset;
        public Vector3 customModelRotation;

        [Header("Bot davranışı")]
        public float botEngageRange = 60f;
        public float botPreferredRange = 30f;
    }
}
