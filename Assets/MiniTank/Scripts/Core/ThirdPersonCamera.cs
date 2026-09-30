using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Tankın arkasından takip eden, sağ ekranda parmak kaydırarak döndürülen kamera.
    /// Ekranın ortası nişangahtır; kule kameranın baktığı noktaya döner.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;

        [Header("Konum")]
        public float distance = 10f;
        public float height = 3f;
        public float followSharpness = 15f;

        [Header("Dönüş")]
        public float sensitivity = 0.15f; // piksel başına derece
        public float minPitch = -15f;
        public float maxPitch = 35f;

        [Header("Çarpışma ve nişan")]
        [Tooltip("Kameranın içinden geçemeyeceği katmanlar (tankları hariç tutun).")]
        public LayerMask obstacleMask = ~0;
        public float aimRange = 300f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 5f;

        static readonly RaycastHit[] hits = new RaycastHit[16];

        public void AddLook(Vector2 delta)
        {
            Yaw += delta.x * sensitivity;
            Pitch = Mathf.Clamp(Pitch - delta.y * sensitivity, minPitch, maxPitch);
        }

        public void SnapBehind()
        {
            if (target == null) return;
            Yaw = target.eulerAngles.y;
            Pitch = 5f;
            transform.SetPositionAndRotation(DesiredPosition(out var rot), rot);
        }

        Vector3 Pivot => target.position + Vector3.up * height;

        Vector3 DesiredPosition(out Quaternion rotation)
        {
            rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 pivot = Pivot;
            Vector3 desired = pivot - rotation * Vector3.forward * distance;

            Vector3 dir = desired - pivot;
            float len = dir.magnitude;
            if (len > 0.01f && Physics.SphereCast(pivot, 0.3f, dir / len, out RaycastHit hit, len,
                                                  obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<TankController>() == null)
                    desired = pivot + dir / len * Mathf.Max(0.5f, hit.distance);
            }
            return desired;
        }

        void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            Vector3 desired = DesiredPosition(out Quaternion rot);
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = rot;
        }

        /// <summary>Ekran ortasındaki nişangahın dünyada denk geldiği nokta.</summary>
        public Vector3 GetAimPoint(TankController self)
        {
            var ray = new Ray(transform.position, transform.forward);
            int count = Physics.RaycastNonAlloc(ray, hits, aimRange, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = ray.GetPoint(aimRange);
            for (int i = 0; i < count; i++)
            {
                if (self != null && hits[i].collider.GetComponentInParent<TankController>() == self) continue;
                // kameranın arkasında kalan (tank ile kamera arası) engelleri yok say
                if (target != null && hits[i].distance < Vector3.Distance(transform.position, Pivot)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; point = hits[i].point; }
            }
            return point;
        }
    }
}
