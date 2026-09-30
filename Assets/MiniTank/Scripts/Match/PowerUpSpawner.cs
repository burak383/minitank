using UnityEngine;
using UnityEngine.AI;

namespace MiniTank
{
    /// <summary>Belirli aralıklarla haritanın rastgele yerlerine güçlendirme bırakır.</summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        public float interval = 20f;
        public int maxActive = 4;
        public float firstDelay = 15f;

        float nextSpawn;
        Bounds area;
        Shader litShader;

        static int nextNetId = 1;

        void Start()
        {
            nextSpawn = Time.time + firstDelay;
            var nav = FindAnyObjectByType<ArenaNavMesh>();
            area = nav != null ? new Bounds(nav.transform.position, nav.size - new Vector3(30f, 0f, 30f)) : new Bounds(Vector3.zero, new Vector3(150f, 10f, 150f));
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
        }

        void Update()
        {
            var match = MatchManager.Instance;
            if (match == null || match.IsOver || !match.IsSimulating || Time.time < nextSpawn) return;
            nextSpawn = Time.time + interval;
            if (PowerUp.All.Count >= maxActive) return;

            for (int attempt = 0; attempt < 15; attempt++)
            {
                Vector3 p = new Vector3(Random.Range(area.min.x, area.max.x), 0f, Random.Range(area.min.z, area.max.z));
                // Doğma bölgelerinden uzak dursun
                if (Mathf.Abs(p.z) > area.extents.z - 20f) continue;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(p, out hit, 4f, NavMesh.AllAreas)) continue;
                Spawn(hit.position, (PowerUpType)Random.Range(0, 4));
                return;
            }
        }

        void Spawn(Vector3 position, PowerUpType type)
        {
            var pu = Create(position, type, litShader);
            pu.netId = nextNetId++;
        }

        /// <summary>Güçlendirme objesini oluşturur (online istemciler de görsel kopya için kullanır).</summary>
        public static PowerUp Create(Vector3 position, PowerUpType type, Shader litShader)
        {
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            var root = new GameObject("Güçlendirme: " + PowerUp.Name(type));
            root.transform.position = position + Vector3.up * 1.2f;

            Color c = PowerUp.ColorOf(type);
            var mat = new Material(litShader);
            mat.SetColor("_BaseColor", c);
            mat.SetColor("_Color", c);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 0.6f);

            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.9f;
            core.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            core.GetComponent<Renderer>().sharedMaterial = mat;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = Vector3.down * 1.1f;
            ring.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
            ring.GetComponent<Renderer>().sharedMaterial = mat;

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.2f;

            var pu = root.AddComponent<PowerUp>();
            pu.type = type;
            return pu;
        }
    }
}
