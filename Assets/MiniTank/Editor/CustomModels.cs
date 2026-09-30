using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MiniTank.EditorTools
{
    /// <summary>
    /// Tank sınıfına "Custom Model" atanmışsa kodla üretilen model yerine onu kullanır.
    /// Taret, namlu ve namlu ağzı isimlerinden otomatik bulunur; dönme için ayrı pivotlar eklenir
    /// (modelin kendi eksenleri bozulmaz).
    /// </summary>
    public static partial class MiniTankBuilder
    {
        static readonly string[] TurretNames = { "turret", "taret", "tower", "kule" };
        static readonly string[] GunNames = { "gun", "barrel", "cannon", "namlu", "mantlet" };
        static readonly string[] MuzzleNames = { "muzzle", "firepoint", "fire_point", "shootpoint", "namluagzi" };

        [MenuItem("MiniTank/Tankları Yeniden Kur (model değiştirince)", priority = 4)]
        public static void RebuildTanksMenu()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorUtility.DisplayProgressBar("MiniTank", "Tanklar yeniden oluşturuluyor...", 0.5f);
                BuildSharedAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }
            EditorUtility.DisplayDialog("MiniTank", "Tank prefab'ları yeniden oluşturuldu. Sahneleri kurmana gerek yok, Play'e basabilirsin.", "Tamam");
        }

        static GameObject CreateCustomTankPrefab(TankSpec s, TankClassData data, TankMaterials m, GameObject flash, GameObject explosion)
        {
            var root = new GameObject("Tank_" + s.id);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(data.customModel);
            if (model == null) model = Object.Instantiate(data.customModel);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = data.customModelOffset;
            model.transform.localRotation = Quaternion.Euler(data.customModelRotation);
            model.transform.localScale = Vector3.one * Mathf.Max(0.001f, data.customModelScale);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Modelin kendi çarpıştırıcıları kaldırılır; tek bir kutu kullanılır
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            Bounds bounds = LocalBounds(root.transform, model);

            // ---------------------------------------------------------------- Taret ve namlu
            Transform turretPart = FindByName(model.transform, TurretNames);
            Transform gunPart = FindByName(turretPart != null ? turretPart : model.transform, GunNames) ?? FindByName(model.transform, GunNames);
            Transform muzzlePart = FindByName(model.transform, MuzzleNames);

            Transform turretPivot;
            if (turretPart != null) turretPivot = InsertPivot(turretPart, "TurretPivot", root.transform);
            else
            {
                turretPivot = new GameObject("TurretPivot").transform;
                turretPivot.SetParent(root.transform, false);
                turretPivot.localPosition = new Vector3(0f, bounds.max.y * 0.75f, bounds.center.z);
                Debug.LogWarning($"[MiniTank] {s.id}: modelde 'Turret' adlı parça bulunamadı; taret görsel olarak dönmeyecek.");
            }

            Transform gunPivot;
            if (gunPart != null)
            {
                gunPivot = InsertPivot(gunPart, "GunPivot", turretPivot);
            }
            else
            {
                gunPivot = new GameObject("GunPivot").transform;
                gunPivot.SetParent(turretPivot, false);
                gunPivot.position = root.transform.TransformPoint(new Vector3(0f, bounds.max.y * 0.8f, bounds.center.z));
                Debug.LogWarning($"[MiniTank] {s.id}: modelde 'Gun'/'Barrel' adlı parça bulunamadı; namlu yukarı-aşağı hareket etmeyecek.");
            }

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gunPivot, false);
            if (muzzlePart != null) muzzle.position = muzzlePart.position;
            else
            {
                // Namlunun en öndeki noktası
                float front = bounds.max.z;
                if (gunPart != null)
                {
                    var gb = LocalBounds(root.transform, gunPart.gameObject);
                    front = gb.max.z;
                }
                Vector3 gunLocal = root.transform.InverseTransformPoint(gunPivot.position);
                muzzle.position = root.transform.TransformPoint(new Vector3(0f, gunLocal.y, front + 0.4f));
            }
            muzzle.rotation = root.transform.rotation;

            // ---------------------------------------------------------------- Takım işareti (anten + bayrak)
            var antennaPivot = new GameObject("Anten").transform;
            antennaPivot.SetParent(turretPivot, false);
            antennaPivot.position = root.transform.TransformPoint(new Vector3(-bounds.extents.x * 0.5f, bounds.max.y, bounds.center.z - bounds.extents.z * 0.4f));
            var antenna = new MeshKit();
            antenna.CylinderY(m.steel, new Vector3(0f, 1.1f, 0f), 0.015f, 2.2f);
            antenna.Box(m.marker, new Vector3(0f, 2f, -0.18f), new Vector3(0.02f, 0.22f, 0.34f));
            antenna.Build($"Tank_{s.id}_CustomAntenna", "AntenMesh", antennaPivot, Vector3.zero, Quaternion.identity);

            // ---------------------------------------------------------------- Fizik ve bileşenler
            var dust = CreateDust(root.transform, new Vector3(0f, 0.15f, bounds.min.z), bounds.size.x, m.dust);

            var col = root.AddComponent<BoxCollider>();
            col.center = bounds.center;
            col.size = bounds.size;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = s.mass;
            rb.linearDamping = 0f;
            rb.angularDamping = 2f;

            var tank = root.AddComponent<TankController>();
            tank.turret = turretPivot;
            tank.gun = gunPivot;
            tank.muzzle = muzzle;
            tank.teamColorRenderers = new Renderer[0];
            tank.muzzleFlashPrefab = flash;
            tank.deathEffectPrefab = explosion;

            var visuals = root.AddComponent<TankVisuals>();
            visuals.leftWheels = new Transform[0];
            visuals.rightWheels = new Transform[0];
            visuals.recoilPart = gunPart;
            visuals.recoilDistance = 0.35f;
            visuals.dust = dust;
            visuals.antenna = antennaPivot;
            visuals.trackHalfWidth = bounds.extents.x;

            root.AddComponent<BotInputSource>();

            string path = $"{Root}/Prefabs/Tank_{s.id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[MiniTank] {s.id}: özel model kullanıldı (taret: {(turretPart != null ? turretPart.name : "yok")}, namlu: {(gunPart != null ? gunPart.name : "yok")}).");
            return prefab;
        }

        /// <summary>Parçanın yerine, kimlik (identity) dönüşlü bir pivot koyar ve parçayı altına taşır.</summary>
        static Transform InsertPivot(Transform part, string name, Transform newParent)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(newParent, false);
            pivot.position = part.position;
            pivot.rotation = newParent.rotation;
            part.SetParent(pivot, true);
            return pivot;
        }

        static Transform FindByName(Transform root, string[] keywords)
        {
            if (root == null) return null;
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                string n = t.name.ToLowerInvariant().Replace(" ", "");
                foreach (var k in keywords)
                    if (t != root && n.Contains(k)) return t;
                foreach (Transform child in t) queue.Enqueue(child);
            }
            return null;
        }

        /// <summary>Objenin tüm görsellerinin, verilen köke göre sınırları.</summary>
        static Bounds LocalBounds(Transform space, GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            bool has = false;
            var b = new Bounds();
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = space.InverseTransformPoint(corner);
                    if (!has) { b = new Bounds(local, Vector3.zero); has = true; }
                    else b.Encapsulate(local);
                }
            }
            if (!has) b = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(3f, 2f, 5f));
            return b;
        }
    }
}
