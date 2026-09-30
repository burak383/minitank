using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MiniTank
{
    /// <summary>
    /// Botların yol bulması için arena yüklenirken NavMesh'i oluşturur.
    /// Ek paket gerektirmez; arena geometrisi değişirse otomatik uyum sağlar.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ArenaNavMesh : MonoBehaviour
    {
        public Vector3 size = new Vector3(160f, 20f, 160f);
        public float agentRadius = 1.8f;
        public float agentHeight = 2.5f;
        public float maxSlope = 35f;
        public float stepHeight = 0.5f;
        public LayerMask includeLayers = ~0;

        NavMeshDataInstance instance;

        void Awake()
        {
            var bounds = new Bounds(transform.position, size);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, includeLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                                          new List<NavMeshBuildMarkup>(), sources);

            // Tankların kendisini engel olarak sayma
            sources.RemoveAll(s => s.component != null && s.component.GetComponentInParent<TankController>() != null);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentSlope = maxSlope;
            settings.agentClimb = stepHeight;

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null) instance = NavMesh.AddNavMeshData(data);
            else Debug.LogWarning("[MiniTank] NavMesh oluşturulamadı; botlar düz çizgide hareket edecek.");
        }

        void OnDestroy()
        {
            if (instance.valid) instance.Remove();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
