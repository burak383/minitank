using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Ele geçirme noktası. Alanda sadece bir takımın tankı varsa ilerleme o takıma kayar.
    /// İki takım da varsa nokta çekişmeli kalır. Sahibi olan takım saniye başına puan kazanır.
    /// </summary>
    public class CapturePoint : MonoBehaviour
    {
        public string pointName = "A";
        public float radius = 9f;
        [Tooltip("Tek tankla nötr noktayı almak için gereken süre (saniye).")]
        public float captureTime = 8f;
        [Tooltip("Aynı anda en fazla kaç tankın hızı toplanır.")]
        public int maxCapturers = 3;
        public float pointsPerSecond = 1f;

        [Header("Görsel")]
        public Renderer zoneRenderer;

        /// <summary>-1 = Mavi'nin, 0 = nötr, +1 = Kırmızı'nın.</summary>
        public float Progress { get; private set; }
        public bool HasOwner { get; private set; }
        public Team Owner { get; private set; }
        public bool Contested { get; private set; }

        MaterialPropertyBlock block;

        /// <summary>Online istemcide true: durum host'tan gelir, burada hesaplanmaz.</summary>
        public static bool NetDriven;

        public void SetNetState(float progress, bool hasOwner, Team owner, bool contested)
        {
            Progress = progress; HasOwner = hasOwner; Owner = owner; Contested = contested;
        }

        void Update()
        {
            if (NetDriven) { UpdateVisual(); return; }
            var mm = MatchManager.Instance;
            if (mm != null && !mm.IsSimulating) { UpdateVisual(); return; }
            int blue = 0, red = 0;
            var all = TankController.All;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t.IsDead) continue;
                Vector3 offset = t.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius) continue;
                if (t.Team == Team.Blue) blue++; else red++;
            }

            Contested = blue > 0 && red > 0;
            if (!Contested && (blue > 0 || red > 0))
            {
                int count = Mathf.Min(blue + red, maxCapturers);
                float direction = blue > 0 ? -1f : 1f;
                Progress = Mathf.Clamp(Progress + direction * count * Time.deltaTime / captureTime, -1f, 1f);
            }

            if (Progress <= -1f) { HasOwner = true; Owner = Team.Blue; }
            else if (Progress >= 1f) { HasOwner = true; Owner = Team.Red; }
            else if (HasOwner)
            {
                // Sahip takım, ilerleme nötre dönünce noktayı kaybeder
                if ((Owner == Team.Blue && Progress >= 0f) || (Owner == Team.Red && Progress <= 0f))
                    HasOwner = false;
            }

            if (HasOwner && MatchManager.Instance != null)
                MatchManager.Instance.AddCaptureScore(Owner, pointsPerSecond * Time.deltaTime);

            UpdateVisual();
        }

        void UpdateVisual()
        {
            if (zoneRenderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color c = Progress < 0f
                ? Color.Lerp(TeamColors.Neutral, TeamColors.Blue, -Progress)
                : Color.Lerp(TeamColors.Neutral, TeamColors.Red, Progress);
            if (Contested) c = Color.Lerp(c, Color.yellow, Mathf.PingPong(Time.time * 2f, 1f) * 0.6f);
            zoneRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            zoneRenderer.SetPropertyBlock(block);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
