using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Ana menünün arkasındaki vitrin: seçili tank döner bir platformun üstünde, spot ışıklar altında durur.
    /// Parmakla / fareyle sürükleyerek çevrilebilir. Sahne kurulumu gerektirmez, çalışırken oluşur.
    /// </summary>
    public class MenuShowroom : MonoBehaviour
    {
        public static MenuShowroom Instance { get; private set; }

        Transform turntable;
        GameObject current;
        TankClassData currentClass;
        string currentCamo;
        float spin = 18f;       // derece/sn
        float dragVelocity;
        Camera cam;
        float swapTime = -10f;

        public static MenuShowroom Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Menü Vitrini");
            return go.AddComponent<MenuShowroom>();
        }

        void Awake()
        {
            Instance = this;
            BuildStage();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildStage()
        {
            cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
                cam.fieldOfView = 34f;
                cam.transform.position = new Vector3(0f, 3.4f, -11.5f);
                cam.transform.LookAt(new Vector3(0f, 1.1f, 0f));
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.25f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.08f);
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 40f;

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            // Zemin: koyu beton ve üstünde platform
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Zemin";
            floor.transform.SetParent(transform, false);
            floor.transform.localScale = new Vector3(8f, 1f, 8f);
            floor.GetComponent<Renderer>().sharedMaterial = Mat(lit, new Color(0.1f, 0.11f, 0.13f), 0.35f, 0f);
            Destroy(floor.GetComponent<Collider>());

            turntable = new GameObject("Platform").transform;
            turntable.SetParent(transform, false);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.transform.SetParent(turntable, false);
            disc.transform.localScale = new Vector3(8.5f, 0.12f, 8.5f);
            disc.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            disc.GetComponent<Renderer>().sharedMaterial = Mat(lit, new Color(0.18f, 0.19f, 0.21f), 0.5f, 0.6f);
            Destroy(disc.GetComponent<Collider>());
            // Platform kenarındaki ışık şeridi
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.transform.SetParent(turntable, false);
            ring.transform.localScale = new Vector3(8.7f, 0.05f, 8.7f);
            ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var ringMat = Mat(lit, new Color(1f, 0.7f, 0.2f), 0.5f, 0f);
            if (ringMat.HasProperty("_EmissionColor"))
            {
                ringMat.EnableKeyword("_EMISSION");
                ringMat.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.15f) * 1.2f);
            }
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            Destroy(ring.GetComponent<Collider>());

            // Işıklar: ana spot (üst ön), soğuk arka ışık (siluet), sıcak yan dolgu
            AddLight(LightType.Spot, new Vector3(3f, 9f, -6f), new Color(1f, 0.96f, 0.88f), 120f, 38f, true);
            AddLight(LightType.Spot, new Vector3(-4f, 5f, 7f), new Color(0.45f, 0.65f, 1f), 45f, 45f, false);
            AddLight(LightType.Point, new Vector3(-6f, 2f, -3f), new Color(1f, 0.6f, 0.3f), 15f, 0f, false);

            // Sahnede başka yönlü ışık varsa kıs (vitrin ışığı baskın olsun)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.intensity = 0.35f;
        }

        void AddLight(LightType type, Vector3 pos, Color color, float intensity, float angle, bool shadows)
        {
            var go = new GameObject(type + " Işık");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.LookAt(new Vector3(0f, 1f, 0f));
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = 30f;
            if (type == LightType.Spot) l.spotAngle = angle;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        static Material Mat(Shader shader, Color c, float smooth, float metal)
        {
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        /// <summary>Vitrindeki tankı değiştirir (sınıf veya kamuflaj değişince).</summary>
        public void Show(TankClassData cls, string camo)
        {
            if (cls == null || cls.prefab == null) return;
            if (cls == currentClass && camo == currentCamo && current != null) return;
            bool classChanged = cls != currentClass;
            currentClass = cls;
            currentCamo = camo;
            if (current != null) Destroy(current);

            current = Instantiate(cls.prefab, turntable);
            current.name = "Vitrin Tankı";
            current.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            current.transform.localRotation = Quaternion.identity;

            // Sadece görüntü: oyun mantığı, fizik, ses ve toz kapalı
            var rb = current.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }
            var tank = current.GetComponent<TankController>();
            if (tank != null)
            {
                tank.ApplyCamo(camo);
                tank.enabled = false;
            }
            foreach (var b in current.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && !(b is TankController)) b.enabled = false;
            foreach (var ps in current.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var a in current.GetComponentsInChildren<AudioSource>(true))
                a.enabled = false;

            if (classChanged) swapTime = Time.unscaledTime;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Sürükleme (MainMenu, IMGUI olaylarından Drag çağırır: Input System ile de çalışır)
            bool dragging = Time.unscaledTime - lastDrag < 0.08f;
            if (!dragging)
            {
                dragVelocity = Mathf.Lerp(dragVelocity, spin, dt * 1.5f);   // bırakınca savrulur, sonra yavaş döner
                if (turntable != null) turntable.Rotate(0f, dragVelocity * dt, 0f, Space.World);
            }

            // Yeni tank gelince küçük "zıplama" animasyonu
            if (current != null)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - swapTime) / 0.45f);
                float s = t >= 1f ? 1f : 0.85f + 0.15f * UITheme.Ease(t) + Mathf.Sin(t * Mathf.PI) * 0.05f;
                current.transform.localScale = Vector3.one * s;
            }
        }

        float lastDrag = -10f;

        /// <summary>Sanal ekran pikseli cinsinden yatay sürükleme: tankı döndürür.</summary>
        public void Drag(float dx)
        {
            if (turntable != null) turntable.Rotate(0f, -dx * 0.35f, 0f, Space.World);
            dragVelocity = Mathf.Clamp(-dx * 0.35f / Mathf.Max(0.016f, Time.unscaledDeltaTime), -540f, 540f);
            lastDrag = Time.unscaledTime;
        }
    }
}
