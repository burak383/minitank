using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MiniTank.EditorTools
{
    /// <summary>
    /// Tek tıkla oynanabilir prototip kurar:
    /// 4 tank sınıfı, tank ve mermi prefab'ları, efektler, ana menü ve 4 arena sahnesi.
    /// Menü: MiniTank > Her Şeyi Kur
    /// </summary>
    public static partial class MiniTankBuilder
    {
        const string Root = "Assets/MiniTank/Generated";
        const string ScenesFolder = "Assets/MiniTank/Scenes";

        // ------------------------------------------------------------------ Menü

        [MenuItem("MiniTank/Her Şeyi Kur (Menü + 4 Arena)", priority = 0)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                EditorUtility.DisplayProgressBar("MiniTank", "Görüntü kalitesi ayarlanıyor...", 0.05f);
                ConfigureRenderPipelineAssets();
                EditorUtility.DisplayProgressBar("MiniTank", "Ortak varlıklar oluşturuluyor...", 0.1f);
                var assets = BuildSharedAssets();

                var scenePaths = new List<string>();
                EditorUtility.DisplayProgressBar("MiniTank", "Ana menü...", 0.25f);
                scenePaths.Add(BuildLoginScene(assets));
                scenePaths.Add(BuildMenuScene(assets));

                var arenas = ArenaDefinitions();
                for (int i = 0; i < arenas.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("MiniTank", $"Arena: {arenas[i].displayName}", 0.3f + 0.15f * i);
                    scenePaths.Add(BuildArenaScene(arenas[i], assets));
                }

                EditorBuildSettings.scenes = scenePaths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();

                EditorUtility.DisplayProgressBar("MiniTank", "Online (Photon) nesnesi...", 0.95f);
                OnlineSetup.EnsureNetMatchPrefab(false);
                IconSetup.Apply();

                // Mobil için yatay ekran
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                EditorSceneManager.OpenScene(scenePaths[0]);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("MiniTank",
                    "Kurulum tamam!\n\nGiriş sahnesi açıldı. Play'e basıp misafir olarak veya e-postayla giriş yap, sonra mod, tank ve arena seç.\n\n" +
                    "Editörde: WASD hareket, sağ tık basılı + fare ile bakış, Space ateş.", "Harika");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ Ortak varlıklar

        class SharedAssets
        {
            public TankClassData[] classes;
            public Material ground, zone, pole;
            public Sprite knob, uiSprite;
            public Font font;
        }

        class TankSpec
        {
            public string id, displayName, description;
            public Vector3 hull, turret;
            public float barrelLength, barrelRadius, mass;
            public float hp, speed, reverse, accel, hullTurn, turretTurn, minPitch, maxPitch;
            public float damage, cooldown, projSpeed, projLife, splash;
            public bool gravity;
            public float engage, preferred;
            public float front = 0.75f, side = 1f, rear = 1.35f;
            public AbilityType ability;
            public string abilityName;
            public float abilityCooldown, abilityDuration, abilityPower;
        }

        static List<TankSpec> TankSpecs() => new List<TankSpec>
        {
            new TankSpec {
                id = "Hafif", displayName = "Hafif Tank",
                description = "Çok hızlı, az canlı. Ele geçirme noktalarını kapmak ve düşmanın arkasından dolaşmak için ideal.",
                hull = new Vector3(2.2f, 0.7f, 3.4f), turret = new Vector3(1.4f, 0.55f, 1.6f), barrelLength = 2.2f, barrelRadius = 0.09f, mass = 25f,
                hp = 650f, speed = 15f, reverse = 7f, accel = 20f, hullTurn = 110f, turretTurn = 160f, minPitch = -8f, maxPitch = 18f,
                damage = 90f, cooldown = 0.8f, projSpeed = 110f, projLife = 2f, engage = 55f, preferred = 25f, front = 0.85f, side = 1f, rear = 1.4f,
                ability = AbilityType.Nitro, abilityName = "Nitro", abilityCooldown = 12f, abilityDuration = 3f, abilityPower = 1.7f },
            new TankSpec {
                id = "Orta", displayName = "Orta Tank",
                description = "Dengeli hız, zırh ve ateş gücü. Her duruma uyum sağlar.",
                hull = new Vector3(2.8f, 0.9f, 4.2f), turret = new Vector3(1.9f, 0.7f, 2.2f), barrelLength = 3.2f, barrelRadius = 0.12f, mass = 40f,
                hp = 1000f, speed = 11f, reverse = 5.5f, accel = 14f, hullTurn = 80f, turretTurn = 100f, minPitch = -8f, maxPitch = 15f,
                damage = 180f, cooldown = 2f, projSpeed = 100f, projLife = 2.5f, engage = 65f, preferred = 35f, front = 0.75f, side = 1f, rear = 1.35f,
                ability = AbilityType.Repair, abilityName = "Onarım", abilityCooldown = 20f, abilityDuration = 0f, abilityPower = 0.3f },
            new TankSpec {
                id = "Agir", displayName = "Ağır Tank",
                description = "Yavaş ama çok dayanıklı, tek atışta büyük hasar. Cephe hattını tutar.",
                hull = new Vector3(3.4f, 1.1f, 5.0f), turret = new Vector3(2.4f, 0.85f, 2.6f), barrelLength = 3.8f, barrelRadius = 0.16f, mass = 70f,
                hp = 1700f, speed = 7.5f, reverse = 4f, accel = 9f, hullTurn = 55f, turretTurn = 65f, minPitch = -6f, maxPitch = 12f,
                damage = 320f, cooldown = 3.5f, projSpeed = 95f, projLife = 3f, engage = 60f, preferred = 25f, front = 0.6f, side = 0.9f, rear = 1.3f,
                ability = AbilityType.Shield, abilityName = "Kalkan", abilityCooldown = 18f, abilityDuration = 4f, abilityPower = 0.6f },
            new TankSpec {
                id = "Topcu", displayName = "Topçu",
                description = "Uzaktan kavisli atış yapar, alan hasarı verir. Namlu açısı nişan noktasına göre otomatik ayarlanır. Yakın dövüşte zayıf.",
                hull = new Vector3(2.8f, 0.8f, 4.4f), turret = new Vector3(2.0f, 1.0f, 2.4f), barrelLength = 4.2f, barrelRadius = 0.18f, mass = 45f,
                hp = 700f, speed = 8.5f, reverse = 4.5f, accel = 12f, hullTurn = 65f, turretTurn = 50f, minPitch = -5f, maxPitch = 55f,
                damage = 280f, cooldown = 5f, projSpeed = 60f, projLife = 6f, splash = 7f, gravity = true, engage = 100f, preferred = 70f, front = 0.85f, side = 1.05f, rear = 1.4f,
                ability = AbilityType.Barrage, abilityName = "Topçu Atışı", abilityCooldown = 25f, abilityDuration = 0f, abilityPower = 130f },
        };

        static SharedAssets BuildSharedAssets()
        {
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Classes");
            EnsureFolder(ScenesFolder);

            var a = new SharedAssets
            {
                ground = CreateMaterial("Ground", new Color(0.55f, 0.55f, 0.5f)),
                zone = CreateMaterial("CaptureZone", Color.white),
                pole = CreateMaterial("Pole", new Color(0.85f, 0.85f, 0.85f)),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
            };

            PrepareShapes();
            grainTex = TextureFactory.Create("Grain", TextureFactory.Kind.Grain, 5, 256);
            concreteTex = TextureFactory.Create("Concrete", TextureFactory.Kind.Concrete, 9, 256);
            var shellMat = CreateMaterial("Shell", new Color(1f, 0.85f, 0.3f));
            if (shellMat.HasProperty("_EmissionColor"))
            {
                shellMat.EnableKeyword("_EMISSION");
                shellMat.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.1f) * 2f);
            }

            var particleMat = ParticleMaterial();
            var impact = CreateBurstEffect("FX_Impact", new Color(1f, 0.6f, 0.2f), 0.9f, 20, 7f, particleMat);
            var explosion = CreateBurstEffect("FX_Explosion", new Color(1f, 0.45f, 0.1f), 2.2f, 60, 12f, particleMat);
            var bigBoom = CreateBurstEffect("FX_ArtilleryImpact", new Color(1f, 0.5f, 0.15f), 2.5f, 50, 14f, particleMat);
            var flash = CreateBurstEffect("FX_MuzzleFlash", new Color(1f, 0.85f, 0.4f), 0.7f, 10, 5f, particleMat);

            var shell = CreateProjectilePrefab("Shell", 0.35f, shellMat, impact);
            var artilleryShell = CreateProjectilePrefab("ArtilleryShell", 0.6f, shellMat, bigBoom);

            var tankMats = CreateTankMaterials(particleMat);
            var specs = TankSpecs();
            a.classes = new TankClassData[specs.Count];
            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                var existing = AssetDatabase.LoadAssetAtPath<TankClassData>($"{Root}/Classes/{s.id}.asset");
                var prefab = existing != null && existing.customModel != null
                    ? CreateCustomTankPrefab(s, existing, tankMats, flash, explosion)
                    : CreateTankPrefab(s, tankMats, flash, explosion);
                a.classes[i] = CreateOrUpdateClass(s, prefab, s.gravity ? artilleryShell : shell);
            }

            AssetDatabase.SaveAssets();
            return a;
        }

        /// <summary>Sonradan eklenen alanları (yetenek) mevcut sınıflara bir kez yazar.</summary>
        static void ConfigureLateFields(TankClassData data, TankSpec s)
        {
            if (!data.abilityConfigured)
            {
                data.ability = s.ability;
                data.abilityName = s.abilityName;
                data.abilityCooldown = s.abilityCooldown;
                data.abilityDuration = s.abilityDuration;
                data.abilityPower = s.abilityPower;
                data.abilityConfigured = true;
            }
            if (!data.armorConfigured)
            {
                data.frontArmor = s.front;
                data.sideArmor = s.side;
                data.rearArmor = s.rear;
                data.armorConfigured = true;
            }
            EditorUtility.SetDirty(data);
        }

        [MenuItem("MiniTank/Tank Sınıflarını Güncelle (zırh + yetenek)", priority = 3)]
        public static void UpdateClassesMenu()
        {
            int n = 0;
            foreach (var s in TankSpecs())
            {
                var data = AssetDatabase.LoadAssetAtPath<TankClassData>($"{Root}/Classes/{s.id}.asset");
                if (data == null) continue;
                ConfigureLateFields(data, s);
                n++;
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("MiniTank", n > 0
                ? $"{n} tank sınıfı güncellendi: zırh değerleri ve yetenekler eklendi."
                : "Tank sınıfı bulunamadı. Önce 'Her Şeyi Kur' çalıştırılmalı.", "Tamam");
        }

        static TankClassData CreateOrUpdateClass(TankSpec s, GameObject prefab, Projectile projectile)
        {
            string path = $"{Root}/Classes/{s.id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<TankClassData>(path);
            bool isNew = data == null;
            if (isNew)
            {
                data = ScriptableObject.CreateInstance<TankClassData>();
                data.displayName = s.displayName;
                data.description = s.description;
                data.maxHealth = s.hp;
                data.moveSpeed = s.speed;
                data.reverseSpeed = s.reverse;
                data.acceleration = s.accel;
                data.hullTurnSpeed = s.hullTurn;
                data.turretTurnSpeed = s.turretTurn;
                data.minGunPitch = s.minPitch;
                data.maxGunPitch = s.maxPitch;
                data.damage = s.damage;
                data.fireCooldown = s.cooldown;
                data.projectileSpeed = s.projSpeed;
                data.projectileLifetime = s.projLife;
                data.projectileUsesGravity = s.gravity;
                data.splashRadius = s.splash;
                data.botEngageRange = s.engage;
                data.botPreferredRange = s.preferred;
                AssetDatabase.CreateAsset(data, path);
            }
            // Sonradan eklenen zırh ve yetenek değerleri: bir kez ayarlanır, sonra senin değişikliklerine dokunulmaz
            ConfigureLateFields(data, s);
            // Mevcut sınıfların değerlerine dokunma (dengeleme ayarların korunur), sadece referansları güncelle
            data.prefab = prefab;
            data.projectilePrefab = projectile;
            EditorUtility.SetDirty(data);
            return data;
        }

        static Projectile CreateProjectilePrefab(string name, float size, Material mat, GameObject impact)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var p = go.AddComponent<Projectile>();
            p.impactEffectPrefab = impact;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = size * 0.8f;
            trail.endWidth = 0f;
            trail.sharedMaterial = mat;
            trail.shadowCastingMode = ShadowCastingMode.Off;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Projectile>();
        }

        static GameObject CreateBurstEffect(string name, Color color, float size, int count, float speed, Material mat)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.gray, 0.6f));
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = size * 0.2f;

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.gray, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = fade;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// Saydam (alfa karışımlı) parçacık materyali. URP'nin hazır materyali toplamalı (additive)
        /// çalıştığı için toz ve dumanlar bloom ile parlıyordu; bu yüzden kendi materyalimizi kuruyoruz.
        /// </summary>
        static Material ParticleMaterial()
        {
            string path = $"{Root}/Materials/Particle.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(shader);
            m.shader = shader;

            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);

            // Saydam + alfa karışımı (toplamalı değil)
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ Ana menü

        static string BuildMenuScene(SharedAssets a)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.12f, 0.15f);
            }

            var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
            menu.classes = a.classes;
            var arenas = ArenaDefinitions();
            menu.arenaScenes = arenas.Select(x => x.sceneName).ToArray();
            menu.arenaNames = arenas.Select(x => x.displayName).ToArray();

            string path = $"{ScenesFolder}/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ Arenalar

        class ArenaDef
        {
            public string sceneName, displayName;
            public float width, depth;
            public Color groundColor, obstacleColor, skyColor;
            public bool fog;
            public float fogDensity;
            public MatchMode defaultMode;
            public int seed;
            public Vector3[] capturePoints;
            public Action<ArenaContext> build;
            public TextureFactory.Kind groundKind;
            public Vector3 sunEuler = new Vector3(50f, -30f, 0f);
            public Color sunColor = Color.white;
            public float sunIntensity = 1.2f;
            public Color skyTint = new Color(0.5f, 0.5f, 0.5f);
            public float atmosphere = 1f;
            /// <summary>Arena büyütme çarpanı: boyut, ele geçirme noktaları ve obje yerleşimi bununla ölçeklenir.</summary>
            public float scale = 1f;
        }

        class ArenaContext
        {
            public ArenaDef def;
            public Transform parent;
            public Material obstacle;
            public System.Random rng;
            public List<Vector3> capturePoints = new List<Vector3>();
            public Func<string, Color, Material> mat;

            public float S => def.scale;
            public float S2 => def.scale * def.scale;
            public float HalfW => def.width / 2f;
            public float HalfD => def.depth / 2f;

            public float Range(float min, float max) => (float)(min + rng.NextDouble() * (max - min));

            /// <summary>Doğma bölgeleri ve ele geçirme noktaları boş kalsın.</summary>
            public bool IsFree(float x, float z, float margin)
            {
                if (Mathf.Abs(x) > HalfW - 4f - margin || Mathf.Abs(z) > HalfD - 4f - margin) return false;
                if (Mathf.Abs(z) > HalfD - 24f - margin && Mathf.Abs(x) < 30f + margin) return false;
                foreach (var cp in capturePoints)
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(cp.x, cp.z)) < 11f + margin) return false;
                return true;
            }

            public GameObject Box(Vector3 center, Vector3 size, Material m = null, float rotY = 0f)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Engel";
                go.transform.SetParent(parent, false);
                go.transform.localPosition = center;
                go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m != null ? m : obstacle;
                go.isStatic = true;
                return go;
            }

            public GameObject Cylinder(Vector3 bottom, float radius, float height, Material m = null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Silindir";
                go.transform.SetParent(parent, false);
                go.transform.localPosition = bottom + Vector3.up * height / 2f;
                go.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
                go.GetComponent<Renderer>().sharedMaterial = m != null ? m : obstacle;
                go.isStatic = true;
                return go;
            }

            public GameObject Decor(PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = "Dekor";
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.isStatic = true;
                return go;
            }
        }

        static Texture2D grainTex, concreteTex;

        static Material M(ArenaContext c, string n, Color col, float smooth = 0.2f, float metal = 0f) =>
            CreateMaterial($"{c.def.sceneName}/{n}", col, smooth, metal);

        static Material MT(ArenaContext c, string n, Color col, Texture2D tex, float tiling, float smooth = 0.15f) =>
            CreateTexturedMaterial($"{c.def.sceneName}/{n}", col, tex, new Vector2(tiling, tiling), smooth);

        static List<ArenaDef> ArenaDefinitions() => new List<ArenaDef>
        {
            new ArenaDef {
                sceneName = "Arena1_ColKasabasi", displayName = "Çöl Kasabası",
                width = 130f, depth = 130f, seed = 11, scale = 1.4f, defaultMode = MatchMode.TeamDeathmatch,
                groundColor = new Color(0.86f, 0.74f, 0.54f), obstacleColor = new Color(0.8f, 0.68f, 0.5f),
                skyColor = new Color(0.78f, 0.84f, 0.9f), groundKind = TextureFactory.Kind.Sand,
                sunEuler = new Vector3(42f, -35f, 0f), sunColor = new Color(1f, 0.93f, 0.8f), sunIntensity = 1.5f,
                skyTint = new Color(0.55f, 0.6f, 0.7f), atmosphere = 1.1f,
                capturePoints = new[] { new Vector3(0f, 0f, 0f), new Vector3(-40f, 0f, 8f), new Vector3(40f, 0f, -8f) },
                build = BuildDesertTown },
            new ArenaDef {
                sceneName = "Arena2_KarliOrman", displayName = "Karlı Orman",
                width = 150f, depth = 150f, seed = 22, scale = 1.4f, defaultMode = MatchMode.Capture,
                groundColor = new Color(0.93f, 0.95f, 0.98f), obstacleColor = new Color(0.45f, 0.33f, 0.22f),
                skyColor = new Color(0.8f, 0.84f, 0.9f), fog = true, fogDensity = 0.005f, groundKind = TextureFactory.Kind.Snow,
                sunEuler = new Vector3(25f, 30f, 0f), sunColor = new Color(0.9f, 0.93f, 1f), sunIntensity = 1.1f,
                skyTint = new Color(0.6f, 0.65f, 0.72f), atmosphere = 0.8f,
                capturePoints = new[] { new Vector3(-45f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(45f, 0f, 0f) },
                build = BuildSnowForest },
            new ArenaDef {
                sceneName = "Arena3_Liman", displayName = "Liman",
                width = 180f, depth = 120f, seed = 33, scale = 1.35f, defaultMode = MatchMode.Capture,
                groundColor = new Color(0.5f, 0.5f, 0.52f), obstacleColor = new Color(0.6f, 0.3f, 0.2f),
                skyColor = new Color(0.62f, 0.74f, 0.88f), groundKind = TextureFactory.Kind.Asphalt,
                sunEuler = new Vector3(50f, 120f, 0f), sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.3f,
                skyTint = new Color(0.45f, 0.55f, 0.7f), atmosphere = 1f,
                capturePoints = new[] { new Vector3(-55f, 0f, 0f), new Vector3(55f, 0f, 0f) },
                build = BuildHarbor },
            new ArenaDef {
                sceneName = "Arena4_Fabrika", displayName = "Fabrika",
                width = 120f, depth = 120f, seed = 44, scale = 1.4f, defaultMode = MatchMode.TeamDeathmatch,
                groundColor = new Color(0.42f, 0.42f, 0.43f), obstacleColor = new Color(0.62f, 0.6f, 0.56f),
                skyColor = new Color(0.62f, 0.62f, 0.64f), fog = true, fogDensity = 0.005f, groundKind = TextureFactory.Kind.Asphalt,
                sunEuler = new Vector3(35f, -140f, 0f), sunColor = new Color(1f, 0.88f, 0.75f), sunIntensity = 1.2f,
                skyTint = new Color(0.55f, 0.52f, 0.5f), atmosphere = 1.6f,
                capturePoints = new[] { new Vector3(-35f, 0f, 12f), new Vector3(0f, 0f, 0f), new Vector3(35f, 0f, -12f) },
                build = BuildFactory },
        };

        // Arena 1: kerpiç evler, kum torbası siperler, palmiyeler, enkazlar
        static void BuildDesertTown(ArenaContext c)
        {
            Color[] plaster = { new Color(0.86f, 0.76f, 0.6f), new Color(0.8f, 0.7f, 0.55f), new Color(0.9f, 0.84f, 0.72f), new Color(0.76f, 0.62f, 0.48f) };
            var walls = plaster.Select((col, i) => MT(c, "Plaster" + i, col, grainTex, 1f)).ToArray();
            var trim = M(c, "Trim", new Color(0.7f, 0.6f, 0.46f));
            var window = M(c, "Window", new Color(0.12f, 0.12f, 0.14f), 0.7f);
            var bag = M(c, "Sandbag", new Color(0.72f, 0.64f, 0.46f), 0.05f);
            var wood = M(c, "Wood", new Color(0.55f, 0.4f, 0.25f), 0.1f);
            var woodDark = M(c, "WoodDark", new Color(0.35f, 0.25f, 0.15f), 0.1f);
            var burnt = M(c, "Burnt", new Color(0.22f, 0.18f, 0.15f), 0.3f, 0.4f);
            var tyre = M(c, "Tyre", new Color(0.08f, 0.08f, 0.08f), 0.2f);
            var palmTrunk = M(c, "PalmTrunk", new Color(0.5f, 0.4f, 0.28f), 0.1f);
            var palmLeaf = M(c, "PalmLeaf", new Color(0.3f, 0.45f, 0.18f), 0.2f);
            var rock = M(c, "Sandstone", new Color(0.72f, 0.58f, 0.42f), 0.1f);
            PreparePalms(palmTrunk, palmLeaf);

            for (float x = -48f * c.S; x <= 48f * c.S; x += 16f)
            for (float z = -40f * c.S; z <= 40f * c.S; z += 16f)
            {
                float px = x + c.Range(-3f, 3f), pz = z + c.Range(-3f, 3f);
                if (c.rng.NextDouble() < 0.35 || !c.IsFree(px, pz, 5f)) continue;
                Vector3 size = new Vector3(Mathf.Round(c.Range(6f, 10f)), c.rng.NextDouble() < 0.3 ? 6f : 3.2f, Mathf.Round(c.Range(6f, 10f)));
                Building(c, new Vector3(px, 0f, pz), size, c.rng.Next(4) * 90f, walls[c.rng.Next(walls.Length)], trim, window);
            }
            for (int i = 0; i < (int)(16 * c.S2); i++)
            {
                float x = c.Range(-55f * c.S, 55f * c.S), z = c.Range(-45f * c.S, 45f * c.S);
                if (!c.IsFree(x, z, 3f)) continue;
                SandbagWall(c, new Vector3(x, 0f, z), c.Range(4f, 7f), c.Range(0f, 180f), bag);
            }
            for (int i = 0; i < (int)(40 * c.S2); i++)
            {
                float x = c.Range(-60f * c.S, 60f * c.S), z = c.Range(-55f * c.S, 55f * c.S);
                if (!c.IsFree(x, z, 2f)) continue;
                Palm(c, new Vector3(x, 0f, z));
                if (i > 24 * c.S2) break;
            }
            for (int i = 0; i < (int)(10 * c.S2); i++)
            {
                float x = c.Range(-55f * c.S, 55f * c.S), z = c.Range(-45f * c.S, 45f * c.S);
                if (!c.IsFree(x, z, 3f)) continue;
                if (i % 2 == 0) CrateStack(c, new Vector3(x, 0f, z), c.Range(0f, 360f), wood, woodDark);
                else CarWreck(c, new Vector3(x, 0f, z), c.Range(0f, 360f), burnt, tyre);
            }
            for (int i = 0; i < (int)(8 * c.S2); i++)
            {
                float x = c.Range(-60f * c.S, 60f * c.S), z = c.Range(-50f * c.S, 50f * c.S);
                if (!c.IsFree(x, z, 3f)) continue;
                Rock(c, new Vector3(x, 0f, z), c.Range(1.5f, 3f), rock, null);
            }
        }

        // Arena 2: karlı çamlar, kayalar, kütükler, kulübeler
        static void BuildSnowForest(ArenaContext c)
        {
            var bark = M(c, "Bark", new Color(0.33f, 0.24f, 0.17f), 0.1f);
            var needles = M(c, "Needles", new Color(0.13f, 0.28f, 0.18f), 0.15f);
            var snow = M(c, "Snow", new Color(0.95f, 0.97f, 1f), 0.35f);
            var rock = MT(c, "Rock", new Color(0.52f, 0.54f, 0.58f), concreteTex, 0.5f);
            var cut = M(c, "LogCut", new Color(0.78f, 0.64f, 0.45f), 0.1f);
            var cabinWood = MT(c, "CabinWood", new Color(0.45f, 0.32f, 0.2f), grainTex, 1f);
            var window = M(c, "Window", new Color(0.15f, 0.18f, 0.22f), 0.8f);
            PreparePines(bark, needles, snow);

            var placed = new List<Vector2>();

            for (int i = 0; i < 5; i++)
            {
                float x = c.Range(-55f * c.S, 55f * c.S), z = c.Range(-40f * c.S, 40f * c.S);
                if (!c.IsFree(x, z, 6f)) continue;
                Cabin(c, new Vector3(x, 0f, z), c.Range(0f, 360f), cabinWood, snow, window);
                placed.Add(new Vector2(x, z));
            }
            int trees = 0;
            for (int i = 0; i < 1000 && trees < (int)(110 * c.S2); i++)
            {
                float x = c.Range(-70f * c.S, 70f * c.S), z = c.Range(-60f * c.S, 60f * c.S);
                if (!c.IsFree(x, z, 1f)) continue;
                var p = new Vector2(x, z);
                if (placed.Any(o => Vector2.Distance(o, p) < 7f)) continue;
                placed.Add(p);
                Pine(c, new Vector3(x, 0f, z));
                trees++;
            }
            for (int i = 0; i < (int)(22 * c.S2); i++)
            {
                float x = c.Range(-65f * c.S, 65f * c.S), z = c.Range(-55f * c.S, 55f * c.S);
                if (!c.IsFree(x, z, 3f)) continue;
                Rock(c, new Vector3(x, 0f, z), c.Range(2f, 4.5f), rock, snow);
            }
            for (int i = 0; i < (int)(10 * c.S2); i++)
            {
                float x = c.Range(-60f * c.S, 60f * c.S), z = c.Range(-50f * c.S, 50f * c.S);
                if (!c.IsFree(x, z, 4f)) continue;
                Log(c, new Vector3(x, 0f, z), c.Range(0f, 180f), bark, cut);
            }
        }

        // Arena 3: oluklu konteyner sıraları, vinçler, depolar
        static void BuildHarbor(ArenaContext c)
        {
            Color[] colors = {
                new Color(0.7f, 0.2f, 0.14f), new Color(0.12f, 0.32f, 0.58f), new Color(0.18f, 0.45f, 0.28f),
                new Color(0.85f, 0.5f, 0.1f), new Color(0.55f, 0.55f, 0.55f), new Color(0.45f, 0.15f, 0.35f) };
            var paints = colors.Select((col, i) => M(c, "ContainerPaint" + i, col, 0.35f, 0.3f)).ToArray();
            var frame = M(c, "ContainerFrame", new Color(0.2f, 0.2f, 0.2f), 0.3f, 0.5f);
            var craneSteel = M(c, "CraneSteel", new Color(0.95f, 0.72f, 0.08f), 0.4f, 0.4f);
            var dark = M(c, "DarkSteel", new Color(0.18f, 0.18f, 0.2f), 0.4f, 0.6f);
            var glass = M(c, "Glass", new Color(0.3f, 0.45f, 0.55f), 0.9f);
            var shed = MT(c, "ShedWall", new Color(0.62f, 0.64f, 0.66f), grainTex, 1f, 0.3f);
            var shedRoof = M(c, "ShedRoof", new Color(0.35f, 0.38f, 0.42f), 0.3f, 0.3f);
            var yellow = M(c, "LinePaint", new Color(0.95f, 0.8f, 0.2f), 0.2f);
            PrepareContainer(paints[0], frame);

            for (float x = -75f * c.S; x <= 75f * c.S; x += 14f)
            for (float z = -35f * c.S; z <= 35f * c.S; z += 17f)
            {
                if (c.rng.NextDouble() < 0.3) continue;
                bool alongZ = c.rng.NextDouble() < 0.5;
                float px = x + c.Range(-2f, 2f), pz = z + c.Range(-2f, 2f);
                if (!c.IsFree(px, pz, 6f)) continue;
                Container(c, new Vector3(px, 0f, pz), alongZ, paints[c.rng.Next(paints.Length)], frame);
                if (c.rng.NextDouble() < 0.4)
                    Container(c, new Vector3(px, 2.6f, pz), alongZ, paints[c.rng.Next(paints.Length)], frame);
            }

            foreach (float x in new[] { -25f * c.S, 25f * c.S })
                if (c.IsFree(x, 0f, 0f)) Crane(c, new Vector3(x, 0f, 0f), craneSteel, dark, glass);

            foreach (var p in new[] { new Vector3(-72f, 0f, -18f) * c.S, new Vector3(72f, 0f, 18f) * c.S })
                if (c.IsFree(p.x, p.z, 0f)) Warehouse(c, p, new Vector3(14f, 7f, 18f), 0f, shed, shedRoof, dark);

            // Rıhtım kenarı: babalar ve sarı çizgiler
            for (float x = -c.HalfW + 6f; x < c.HalfW - 6f; x += 12f)
                if (Mathf.Abs(x) > 32f * c.S) Bollard(c, new Vector3(x, 0f, c.HalfD - 3f), dark);
            for (float x = -c.HalfW + 10f; x < c.HalfW - 10f; x += 6f)
            {
                GroundLine(c, new Vector3(x, 0f, 9f), new Vector3(3f, 0.02f, 0.25f), 0f, yellow);
                GroundLine(c, new Vector3(x, 0f, -9f), new Vector3(3f, 0.02f, 0.25f), 0f, yellow);
            }

            // Arena dışında deniz
            var water = M(c, "Water", new Color(0.08f, 0.28f, 0.42f), 0.92f);
            c.Decor(PrimitiveType.Cube, new Vector3(0f, -1.2f, c.HalfD + 60f), new Vector3(c.def.width + 300f, 1f, 120f), water);
        }

        // Arena 4: fabrika binaları, beton duvarlar, depolama tankları, boru hatları, bacalar
        static void BuildFactory(ArenaContext c)
        {
            var concrete = MT(c, "Concrete", new Color(0.66f, 0.64f, 0.6f), concreteTex, 1f);
            var capMat = M(c, "ConcreteCap", new Color(0.55f, 0.54f, 0.52f), 0.15f);
            var brick = MT(c, "Brick", new Color(0.55f, 0.32f, 0.25f), grainTex, 1f);
            var trim = M(c, "Trim", new Color(0.45f, 0.44f, 0.42f), 0.2f);
            var window = M(c, "Window", new Color(0.2f, 0.26f, 0.3f), 0.85f, 0.2f);
            var metal = M(c, "TankMetal", new Color(0.72f, 0.74f, 0.76f), 0.5f, 0.6f);
            var dark = M(c, "DarkSteel", new Color(0.2f, 0.2f, 0.22f), 0.4f, 0.6f);
            var pipe = M(c, "Pipe", new Color(0.35f, 0.5f, 0.35f), 0.4f, 0.4f);
            var red = M(c, "ChimneyRed", new Color(0.65f, 0.15f, 0.12f), 0.2f);
            var white = M(c, "ChimneyWhite", new Color(0.85f, 0.85f, 0.82f), 0.2f);
            var hazard = M(c, "Hazard", new Color(0.95f, 0.75f, 0.1f), 0.2f);

            // Merkez binanın dört köşesi
            foreach (var p in new[] { new Vector2(-16f, -16f), new Vector2(16f, -16f), new Vector2(-16f, 16f), new Vector2(16f, 16f) })
                Building(c, new Vector3(p.x, 0f, p.y) * c.S, new Vector3(9f, 7f, 9f), 0f, brick, trim, window, true);

            var wallDefs = new[]
            {
                (new Vector3(-38f, 0f, -20f), new Vector3(20f, 5f, 1.5f)),
                (new Vector3(38f, 0f, 20f), new Vector3(20f, 5f, 1.5f)),
                (new Vector3(-50f, 0f, 5f), new Vector3(1.5f, 5f, 18f)),
                (new Vector3(50f, 0f, -5f), new Vector3(1.5f, 5f, 18f)),
                (new Vector3(-20f, 0f, 30f), new Vector3(1.5f, 5f, 14f)),
                (new Vector3(20f, 0f, -30f), new Vector3(1.5f, 5f, 14f)),
                (new Vector3(0f, 0f, 32f), new Vector3(14f, 5f, 1.5f)),
                (new Vector3(0f, 0f, -32f), new Vector3(14f, 5f, 1.5f)),
            };
            foreach (var wall in wallDefs)
            {
                Vector3 pos = wall.Item1 * c.S, size = wall.Item2;
                if (c.IsFree(pos.x, pos.z, -Mathf.Max(size.x, size.z) / 2f + 1f))
                    ConcreteWall(c, pos, size, concrete, capMat);
            }

            for (int i = 0; i < (int)(9 * c.S2); i++)
            {
                float x = c.Range(-50f * c.S, 50f * c.S), z = c.Range(-45f * c.S, 45f * c.S);
                if (!c.IsFree(x, z, 4f)) continue;
                StorageTank(c, new Vector3(x, 0f, z), c.Range(2f, 3.2f), c.Range(5f, 8f), metal, dark);
            }
            for (int i = 0; i < (int)(4 * c.S2); i++)
            {
                float x = c.Range(-40f * c.S, 40f * c.S), z = c.Range(-40f * c.S, 40f * c.S);
                if (!c.IsFree(x, z, 6f)) continue;
                PipeRack(c, new Vector3(x, 0f, z), 24f, c.rng.NextDouble() < 0.5 ? 0f : 90f, pipe, dark);
            }
            for (int i = 0; i < (int)(8 * c.S2); i++)
            {
                float x = c.Range(-50f * c.S, 50f * c.S), z = c.Range(-45f * c.S, 45f * c.S);
                if (!c.IsFree(x, z, 3f)) continue;
                ConcreteBarrier(c, new Vector3(x, 0f, z), c.Range(4f, 8f), c.rng.NextDouble() < 0.5 ? 0f : 90f, capMat);
            }
            foreach (var p in new[] { new Vector3(-52f, 0f, -35f) * c.S, new Vector3(52f, 0f, 35f) * c.S })
                if (c.IsFree(p.x, p.z, 0f)) Chimney(c, p, 22f, concrete, red, white);

            // Sarı-siyah tehlike şeritleri
            for (int i = -3; i <= 3; i++)
                GroundLine(c, new Vector3(i * 2.4f, 0f, 0f), new Vector3(1.2f, 0.02f, 16f), 45f, hazard);
        }

        static string BuildArenaScene(ArenaDef def, SharedAssets a)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            propCounter = 0;

            // Arena büyütme
            def.width *= def.scale;
            def.depth *= def.scale;
            def.capturePoints = def.capturePoints.Select(cp => cp * def.scale).ToArray();

            // Güneş
            var sun = new GameObject("Güneş").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = def.sunIntensity;
            sun.color = def.sunColor;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(def.sunEuler);

            // Gökyüzü ve ortam ışığı
            var sky = CreateSkybox($"{def.sceneName}/Sky", def.skyTint, def.groundColor * 0.6f, def.atmosphere);
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(def.skyColor, Color.white, 0.1f) * 0.9f;
            RenderSettings.ambientEquatorColor = Color.Lerp(def.skyColor, def.groundColor, 0.5f) * 0.75f;
            RenderSettings.ambientGroundColor = def.groundColor * 0.45f;
            RenderSettings.fog = def.fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = def.fogDensity;
            RenderSettings.fogColor = def.skyColor;

            EnsureFolder($"{Root}/Materials/{def.sceneName}");
            Func<string, Color, Material> mat = (n, col) => CreateMaterial($"{def.sceneName}/{n}", col);

            var geometry = new GameObject("Arena").transform;

            // Dokulu zemin
            var groundTex = TextureFactory.Create($"Ground_{def.groundKind}", def.groundKind, def.seed);
            var groundMat = CreateTexturedMaterial($"{def.sceneName}/Ground", def.groundColor, groundTex,
                                                   new Vector2(def.width / 12f, def.depth / 12f),
                                                   def.groundKind == TextureFactory.Kind.Snow ? 0.3f : 0.1f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Zemin";
            ground.transform.SetParent(geometry, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(def.width, 1f, def.depth);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            ground.isStatic = true;

            // Arena dışını da zeminle doldur (ufuk boş görünmesin)
            var outer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outer.name = "Dış Zemin";
            Object.DestroyImmediate(outer.GetComponent<Collider>());
            outer.transform.SetParent(geometry, false);
            outer.transform.localPosition = new Vector3(0f, -0.52f, 0f);
            outer.transform.localScale = new Vector3(def.width + 400f, 1f, def.depth + 400f);
            outer.GetComponent<Renderer>().sharedMaterial = CreateTexturedMaterial($"{def.sceneName}/GroundOuter", def.groundColor * 0.95f,
                                                                                  groundTex, new Vector2((def.width + 400f) / 12f, (def.depth + 400f) / 12f), 0.1f);
            outer.isStatic = true;

            // Sınır duvarları (dokulu beton)
            var wallMat = CreateTexturedMaterial($"{def.sceneName}/Wall", Color.Lerp(def.obstacleColor, new Color(0.6f, 0.6f, 0.6f), 0.6f),
                                                 concreteTex, new Vector2(8f, 1f));
            float hw = def.width / 2f, hd = def.depth / 2f;
            CreateWall(geometry, new Vector3(0f, 2.5f, hd + 1f), new Vector3(def.width + 4f, 5f, 2f), wallMat);
            CreateWall(geometry, new Vector3(0f, 2.5f, -hd - 1f), new Vector3(def.width + 4f, 5f, 2f), wallMat);
            CreateWall(geometry, new Vector3(hw + 1f, 2.5f, 0f), new Vector3(2f, 5f, def.depth + 4f), wallMat);
            CreateWall(geometry, new Vector3(-hw - 1f, 2.5f, 0f), new Vector3(2f, 5f, def.depth + 4f), wallMat);

            // Engeller ve dekor
            var ctx = new ArenaContext
            {
                def = def,
                parent = geometry,
                obstacle = mat("Obstacle", def.obstacleColor),
                rng = new System.Random(def.seed),
                mat = mat,
            };
            ctx.capturePoints.AddRange(def.capturePoints);
            def.build(ctx);

            // Doğma noktaları: 5 ön sıra + 3 arka sıra
            var blue = CreateSpawns("Mavi Doğma", new Vector3(0f, 0.2f, -hd + 12f), 0f);
            var red = CreateSpawns("Kırmızı Doğma", new Vector3(0f, 0.2f, hd - 12f), 180f);

            // Ele geçirme noktaları
            var points = new List<CapturePoint>();
            string[] names = { "A", "B", "C", "D" };
            for (int i = 0; i < def.capturePoints.Length; i++)
                points.Add(CreateCapturePoint(names[i], def.capturePoints[i], a));

            // Botlar için NavMesh
            var nav = new GameObject("NavMesh").AddComponent<ArenaNavMesh>();
            nav.size = new Vector3(def.width + 10f, 30f, def.depth + 10f);

            // Kamera
            var camGo = new GameObject("Oyuncu Kamerası");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 500f;
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<ThirdPersonCamera>();
            camGo.transform.position = new Vector3(0f, 8f, -hd + 2f);
            SetupPostProcessing(cam);

            // Arayüz ve girdi
            var ui = BuildUI(a, cam);
            var inputGo = new GameObject("Oyuncu Girdisi");
            var input = inputGo.AddComponent<PlayerInputSource>();
            input.moveJoystick = ui.joystick;
            input.lookArea = ui.look;
            input.fireButton = ui.fire;
            input.cameraRig = rig;

            // Maç yöneticisi
            var match = new GameObject("Maç Yöneticisi").AddComponent<MatchManager>();
            match.arenaName = def.displayName;
            match.mode = def.defaultMode;
            match.blueSpawns = blue;
            match.redSpawns = red;
            match.availableClasses = a.classes;
            match.playerInput = input;
            match.playerCamera = rig;
            match.capturePoints = points.ToArray();

            string path = $"{ScenesFolder}/{def.sceneName}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static void CreateWall(Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "Sınır";
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;
            w.transform.localScale = size;
            w.GetComponent<Renderer>().sharedMaterial = m;
            w.isStatic = true;
        }

        static Transform[] CreateSpawns(string name, Vector3 center, float yaw)
        {
            var root = new GameObject(name).transform;
            var list = new List<Transform>();
            float forward = yaw == 0f ? 1f : -1f;
            for (int i = 0; i < 5; i++)
                list.Add(Spawn(root, center + new Vector3(-20f + i * 10f, 0f, 0f), yaw, i));
            for (int i = 0; i < 3; i++)
                list.Add(Spawn(root, center + new Vector3(-10f + i * 10f, 0f, -7f * forward), yaw, 5 + i));
            return list.ToArray();
        }

        static Transform Spawn(Transform parent, Vector3 pos, float yaw, int index)
        {
            var t = new GameObject("Spawn " + (index + 1)).transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static CapturePoint CreateCapturePoint(string name, Vector3 pos, SharedAssets a)
        {
            var go = new GameObject("Nokta " + name);
            go.transform.position = pos;
            var cp = go.AddComponent<CapturePoint>();
            cp.pointName = name;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Alan";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(go.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            disc.transform.localScale = new Vector3(cp.radius * 2f, 0.03f, cp.radius * 2f);
            disc.GetComponent<Renderer>().sharedMaterial = a.zone;
            disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            cp.zoneRenderer = disc.GetComponent<Renderer>();

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Direk";
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(go.transform, false);
            pole.transform.localPosition = new Vector3(0f, 3f, 0f);
            pole.transform.localScale = new Vector3(0.2f, 3f, 0.2f);
            pole.GetComponent<Renderer>().sharedMaterial = a.pole;

            return cp;
        }

        // ------------------------------------------------------------------ Arayüz

        class UIRefs
        {
            public VirtualJoystick joystick;
            public TouchLookArea look;
            public FireButton fire;
        }

        static UIRefs BuildUI(SharedAssets a, Camera worldCamera)
        {
            var refs = new UIRefs();

            var canvasGo = new GameObject("Arayüz");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)canvasGo.transform;

            // Kamera döndürme alanı (tüm ekran, en arkada)
            var lookImg = UIImage("Bakış Alanı", rt, null, new Color(0f, 0f, 0f, 0f));
            Stretch(lookImg.rectTransform);
            refs.look = lookImg.gameObject.AddComponent<TouchLookArea>();

            // Nişangah
            var cross = UIText("Nişangah", rt, a.font, "+", 64, TextAnchor.MiddleCenter);
            Place(cross.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));

            // Hareket joystick'i
            var joyBg = UIImage("Joystick", rt, a.knob, new Color(1f, 1f, 1f, 0.22f));
            Place(joyBg.rectTransform, new Vector2(0f, 0f), new Vector2(250f, 250f), new Vector2(320f, 320f));
            var handle = UIImage("Tutamak", joyBg.rectTransform, a.knob, new Color(1f, 1f, 1f, 0.65f));
            handle.raycastTarget = false;
            Place(handle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            refs.joystick = joyBg.gameObject.AddComponent<VirtualJoystick>();
            refs.joystick.handle = handle.rectTransform;
            refs.joystick.radius = 120f;

            // Ateş butonu ve dolum halkası
            var fireImg = UIImage("Ateş", rt, a.knob, new Color(0.95f, 0.3f, 0.2f, 0.75f));
            Place(fireImg.rectTransform, new Vector2(1f, 0f), new Vector2(-240f, 240f), new Vector2(250f, 250f));
            refs.fire = fireImg.gameObject.AddComponent<FireButton>();
            var reload = UIImage("Dolum", fireImg.rectTransform, a.knob, new Color(1f, 1f, 1f, 0.35f));
            reload.raycastTarget = false;
            reload.type = Image.Type.Filled;
            reload.fillMethod = Image.FillMethod.Radial360;
            reload.fillOrigin = (int)Image.Origin360.Top;
            Stretch(reload.rectTransform);
            var fireLabel = UIText("Yazı", fireImg.rectTransform, a.font, "ATEŞ", 40, TextAnchor.MiddleCenter);
            Stretch(fireLabel.rectTransform);

            // Üst bilgi
            var timer = UIText("Süre", rt, a.font, "5:00", 56, TextAnchor.MiddleCenter);
            Place(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(220f, 80f));
            var blueScore = UIText("Mavi Skor", rt, a.font, "0", 60, TextAnchor.MiddleCenter);
            blueScore.color = TeamColors.Blue;
            Place(blueScore.rectTransform, new Vector2(0.5f, 1f), new Vector2(-170f, -55f), new Vector2(140f, 80f));
            var redScore = UIText("Kırmızı Skor", rt, a.font, "0", 60, TextAnchor.MiddleCenter);
            redScore.color = TeamColors.Red;
            Place(redScore.rectTransform, new Vector2(0.5f, 1f), new Vector2(170f, -55f), new Vector2(140f, 80f));
            var modeText = UIText("Mod", rt, a.font, "", 24, TextAnchor.MiddleCenter);
            Place(modeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(700f, 40f));
            var captureText = UIText("Noktalar", rt, a.font, "", 38, TextAnchor.MiddleCenter);
            Place(captureText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(700f, 50f));

            // Can barı
            var hpBg = UIImage("Can Arka", rt, a.uiSprite, new Color(0f, 0f, 0f, 0.55f));
            hpBg.raycastTarget = false;
            Place(hpBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(620f, 30f));
            var hpFill = UIImage("Can", hpBg.rectTransform, a.uiSprite, new Color(0.3f, 0.85f, 0.35f));
            hpFill.raycastTarget = false;
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            Stretch(hpFill.rectTransform);
            var hpText = UIText("Can Yazı", rt, a.font, "", 26, TextAnchor.MiddleCenter);
            Place(hpText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 105f), new Vector2(1000f, 40f));

            // Orta mesaj ve öldürme akışı
            var center = UIText("Mesaj", rt, a.font, "", 80, TextAnchor.MiddleCenter);
            center.fontStyle = FontStyle.Bold;
            Place(center.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1200f, 220f));
            var feed = UIText("Öldürme Akışı", rt, a.font, "", 26, TextAnchor.UpperRight);
            var feedRt = feed.rectTransform;
            feedRt.anchorMin = feedRt.anchorMax = feedRt.pivot = new Vector2(1f, 1f);
            feedRt.anchoredPosition = new Vector2(-30f, -30f);
            feedRt.sizeDelta = new Vector2(620f, 240f);

            var hud = canvasGo.AddComponent<MatchHUD>();
            hud.timerText = timer;
            hud.blueScoreText = blueScore;
            hud.redScoreText = redScore;
            hud.modeText = modeText;
            hud.captureText = captureText;
            hud.healthText = hpText;
            hud.healthFill = hpFill;
            hud.reloadFill = reload;
            hud.centerText = center;
            hud.killFeedText = feed;
            hud.worldCamera = worldCamera;

            CreateEventSystem();
            return refs;
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            var moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) go.AddComponent(moduleType);
            else go.AddComponent<StandaloneInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static Image UIImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        static Text UIText(string name, RectTransform parent, Font font, string text, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ Yardımcılar

        static GameObject Visual(PrimitiveType type, string name, Transform parent, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static Material CreateMaterial(string name, Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            string path = $"{Root}/Materials/{name}.mat";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            // URP projelerinde URP/Lit, değilse Standard. (Yanlış shader = pembe materyal)
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // Çok parlak (pürüzsüz) yüzeyler güneşte aşırı yüksek parlaklık üretip bloom ile
            // dev ışık topları oluşturuyordu; bu yüzden pürüzsüzlüğü sınırlıyoruz.
            smoothness = Mathf.Min(smoothness, 0.6f);

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(shader);
            else if (m.shader != shader) m.shader = shader;

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;

            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Dokulu materyal (zemin, beton, duvar). Doku renkle çarpılır.</summary>
        static Material CreateTexturedMaterial(string name, Color color, Texture2D texture, Vector2 tiling, float smoothness = 0.15f)
        {
            var m = CreateMaterial(name, color, smoothness);
            if (texture != null)
            {
                if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", texture); m.SetTextureScale("_BaseMap", tiling); }
                if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", texture); m.SetTextureScale("_MainTex", tiling); }
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
