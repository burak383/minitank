using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Sol üstte mini harita. Arka plan, maç başında arenanın üstten çekilmiş fotoğrafıdır.
    /// Takım arkadaşları her zaman, düşmanlar sadece görüldüklerinde (takımından biri görüyorsa
    /// veya yakın zamanda ateş ettiyse) gösterilir.
    /// </summary>
    public class MinimapHUD : MonoBehaviour
    {
        public float sizePixels = 300f;          // 1080p ekrana göre
        public float spotRange = 60f;
        public float firingRevealTime = 3f;

        RenderTexture mapTexture;
        Rect worldRect;                           // x,z düzleminde arena sınırları
        Texture2D white, arrow;
        readonly HashSet<TankController> spotted = new HashSet<TankController>();
        readonly Dictionary<TankController, float> lastFired = new Dictionary<TankController, float>();
        float nextSpotUpdate;
        MatchManager match;
        GUIStyle letterStyle;

        public Rect ScreenRect { get; private set; }

        static readonly RaycastHit[] hits = new RaycastHit[8];

        void Start()
        {
            match = MatchManager.Instance;
            white = Texture2D.whiteTexture;
            arrow = MakeArrowTexture();
            TankController.AnyFired += OnAnyFired;

            var nav = FindAnyObjectByType<ArenaNavMesh>();
            Vector3 center = nav != null ? nav.transform.position : Vector3.zero;
            Vector3 size = nav != null ? nav.size : new Vector3(200f, 0f, 200f);
            worldRect = new Rect(center.x - size.x / 2f, center.z - size.z / 2f, size.x, size.z);
            StartCoroutine(CaptureMap());
        }

        void OnDestroy()
        {
            TankController.AnyFired -= OnAnyFired;
            if (mapTexture != null) mapTexture.Release();
        }

        void OnAnyFired(TankController t) => lastFired[t] = Time.time;

        /// <summary>Arenayı tanklar gizliyken tepeden bir kez fotoğraflar.</summary>
        IEnumerator CaptureMap()
        {
            yield return null; // tanklar doğsun
            int w = 512;
            int h = Mathf.Max(64, Mathf.RoundToInt(512f * worldRect.height / worldRect.width));
            mapTexture = new RenderTexture(w, h, 16);

            var go = new GameObject("Mini Harita Kamerası");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = worldRect.height / 2f;
            cam.aspect = worldRect.width / worldRect.height;
            cam.transform.position = new Vector3(worldRect.center.x, 250f, worldRect.center.y);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.14f);
            cam.targetTexture = mapTexture;
            cam.depth = -50;

            // Bir kare boyunca tankları ve sisi gizle
            var hidden = new List<Renderer>();
            foreach (var t in TankController.All)
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;

            yield return new WaitForEndOfFrame();

            RenderSettings.fog = fog;
            foreach (var r in hidden) if (r != null) r.enabled = true;
            cam.targetTexture = null;
            Destroy(go);
        }

        void Update()
        {
            if (match == null || Time.time < nextSpotUpdate) return;
            nextSpotUpdate = Time.time + 0.3f;
            UpdateSpotted();
        }

        void UpdateSpotted()
        {
            spotted.Clear();
            var player = match.PlayerTank;
            if (player == null) return;
            Team myTeam = player.Team;

            foreach (var enemy in TankController.All)
            {
                if (enemy.Team == myTeam || enemy.IsDead) continue;
                float fired;
                if (lastFired.TryGetValue(enemy, out fired) && Time.time - fired < firingRevealTime) { spotted.Add(enemy); continue; }

                foreach (var ally in TankController.All)
                {
                    if (ally.Team != myTeam || ally.IsDead) continue;
                    if (Vector3.Distance(ally.transform.position, enemy.transform.position) > spotRange) continue;
                    if (HasLineOfSight(ally, enemy)) { spotted.Add(enemy); break; }
                }
            }
        }

        static bool HasLineOfSight(TankController from, TankController to)
        {
            Vector3 a = from.transform.position + Vector3.up * 2f;
            Vector3 b = to.transform.position + Vector3.up * 1.5f;
            Vector3 dir = b - a;
            float len = dir.magnitude;
            int count = Physics.RaycastNonAlloc(a, dir / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var t = hits[i].collider.GetComponentInParent<TankController>();
                if (t == from || t == to) continue;
                if (t == null) return false; // arada engel var
            }
            return true;
        }

        Vector2 ToMap(Vector3 world, Rect r)
        {
            float u = (world.x - worldRect.x) / worldRect.width;
            float v = (world.z - worldRect.y) / worldRect.height;
            return new Vector2(r.x + u * r.width, r.y + (1f - v) * r.height);
        }

        void OnGUI()
        {
            if (match == null) return;
            float scale = Screen.height / 1080f;
            float size = sizePixels * scale;
            float aspect = worldRect.width / Mathf.Max(1f, worldRect.height);
            float mw = aspect >= 1f ? size : size * aspect;
            float mh = aspect >= 1f ? size / aspect : size;
            var r = new Rect(20f * scale, 110f * scale, mw, mh);
            ScreenRect = r;

            // Çerçeve ve arka plan
            Draw(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0f, 0f, 0f, 0.6f));
            if (mapTexture != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.DrawTexture(r, mapTexture, ScaleMode.StretchToFill, false);
                GUI.color = Color.white;
            }
            else Draw(r, new Color(0.15f, 0.16f, 0.17f, 0.8f));

            // Ele geçirme noktaları
            if (match.Mode == MatchMode.Capture && match.capturePoints != null)
            {
                if (letterStyle == null) letterStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                letterStyle.fontSize = Mathf.RoundToInt(16 * scale);
                foreach (var cp in match.capturePoints)
                {
                    if (cp == null) continue;
                    Vector2 p = ToMap(cp.transform.position, r);
                    float s = 22f * scale;
                    Color c = cp.Contested ? new Color(1f, 0.82f, 0.3f) : cp.HasOwner ? TeamColors.Get(cp.Owner) : TeamColors.Neutral;
                    Draw(new Rect(p.x - s / 2, p.y - s / 2, s, s), new Color(0f, 0f, 0f, 0.7f));
                    Draw(new Rect(p.x - s / 2 + 2, p.y - s / 2 + 2, s - 4, s - 4), c);
                    GUI.Label(new Rect(p.x - s / 2, p.y - s / 2, s, s), cp.pointName, letterStyle);
                }
            }

            // Güçlendirmeler
            foreach (var pu in PowerUp.All)
            {
                if (pu == null) continue;
                Vector2 pp = ToMap(pu.transform.position, r);
                float ps = 9f * scale;
                var oldM = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, pp);
                Draw(new Rect(pp.x - ps / 2 - 1, pp.y - ps / 2 - 1, ps + 2, ps + 2), Color.black);
                Draw(new Rect(pp.x - ps / 2, pp.y - ps / 2, ps, ps), PowerUp.ColorOf(pu.type));
                GUI.matrix = oldM;
            }

            var player = match.PlayerTank;
            foreach (var t in TankController.All)
            {
                if (t == player || t.IsDead) continue;
                bool ally = player != null && t.Team == player.Team;
                if (!ally && !spotted.Contains(t)) continue;
                Vector2 p = ToMap(t.transform.position, r);
                float s = 10f * scale;
                Draw(new Rect(p.x - s / 2 - 1, p.y - s / 2 - 1, s + 2, s + 2), Color.black);
                Draw(new Rect(p.x - s / 2, p.y - s / 2, s, s), TeamColors.Get(t.Team));
            }

            // Oyuncu: gövde yönünü gösteren ok
            if (player != null && !player.IsDead)
            {
                Vector2 p = ToMap(player.transform.position, r);
                float s = 22f * scale;
                var old = GUI.matrix;
                GUIUtility.RotateAroundPivot(player.transform.eulerAngles.y, p);
                GUI.color = new Color(1f, 0.9f, 0.3f);
                GUI.DrawTexture(new Rect(p.x - s / 2, p.y - s / 2, s, s), arrow);
                GUI.color = Color.white;
                GUI.matrix = old;
            }
        }

        void Draw(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
        }

        static Texture2D MakeArrowTexture()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Yukarıyı gösteren üçgen (GUI'de y aşağı doğru artar; dokuda y=0 alttadır)
                    float fy = (float)y / (n - 1);          // 0 alt, 1 üst
                    float half = (1f - fy) * 0.5f;          // üstte sivri
                    float fx = Mathf.Abs((float)x / (n - 1) - 0.5f);
                    bool inside = fx <= half && fy >= 0.05f;
                    bool notch = fy < 0.35f && fx < (0.35f - fy) * 0.6f; // arka çentik
                    px[y * n + x] = inside && !notch ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
