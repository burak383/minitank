using UnityEngine;
using UnityEngine.AI;

namespace MiniTank
{
    /// <summary>
    /// Basit tank yapay zekası. Oyuncu gibi TankInputData üretir, yani tank için
    /// oyuncu ile bot arasında fark yoktur. Online modda boş kalan yerleri doldurmak
    /// için de aynı script kullanılacak.
    /// </summary>
    [RequireComponent(typeof(TankController))]
    public class BotInputSource : MonoBehaviour, ITankInputSource
    {
        [Tooltip("Botun karar verme sıklığı (saniye).")]
        public float thinkInterval = 0.3f;
        public float repathInterval = 1f;
        [Tooltip("Nişan hatası çarpanı. 0 = kusursuz nişancı.")]
        public float aimErrorMultiplier = 1f;

        TankController tank;
        TankController target;
        bool targetVisible;
        Vector3 aimError;
        Vector3 destination;
        bool hasDestination;

        NavMeshPath path;
        int cornerIndex;
        bool hasPath;
        float nextThink, nextRepath;

        Vector3 lastStuckCheckPos;
        float nextStuckCheck, reverseUntil;
        float reverseTurn;

        static readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            tank = GetComponent<TankController>();
            path = new NavMeshPath();
        }

        void OnEnable()
        {
            target = null;
            hasPath = false;
            nextThink = nextRepath = 0f;
            reverseUntil = 0f;
        }

        public TankInputData GetInput()
        {
            var input = new TankInputData();
            var d = tank.ClassData;
            if (d == null) return input;

            if (Time.time >= nextThink)
            {
                nextThink = Time.time + thinkInterval * Random.Range(0.8f, 1.2f);
                Think(d);
            }

            // Nişan ve ateş
            if (target != null && target.isActiveAndEnabled)
            {
                Vector3 aimPoint = target.transform.position + Vector3.up * 1f + aimError;
                input.AimPoint = aimPoint;

                float dist = Vector3.Distance(transform.position, target.transform.position);
                input.Fire = targetVisible && dist <= d.botEngageRange && IsTurretAligned(aimPoint, 5f);
            }
            else
            {
                input.AimPoint = transform.position + transform.forward * 30f + Vector3.up;
            }

            input.Move = ComputeMove();
            input.Ability = ShouldUseAbility(d);
            return input;
        }

        void Think(TankClassData d)
        {
            target = FindTarget(d.botEngageRange * 1.5f, out targetVisible);

            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                aimError = Random.insideUnitSphere * (0.4f + dist * 0.03f) * aimErrorMultiplier;
            }

            // Nereye gideceğine karar ver
            Vector3 newDest;
            Vector3 powerUpPos;
            if (target != null && targetVisible &&
                Vector3.Distance(transform.position, target.transform.position) <= d.botPreferredRange)
            {
                newDest = transform.position; // iyi mesafede: dur ve ateş et
            }
            else if ((target == null || !targetVisible) && TryGetNearbyPowerUp(45f, out powerUpPos))
            {
                newDest = powerUpPos; // yakında güçlendirme var: önce onu al
            }
            else if (target != null)
            {
                newDest = target.transform.position;
            }
            else if (MatchManager.Instance != null)
            {
                newDest = MatchManager.Instance.GetBotObjective(tank);
            }
            else
            {
                newDest = transform.position;
            }

            bool changed = !hasDestination || (newDest - destination).sqrMagnitude > 16f;
            destination = newDest;
            hasDestination = true;

            if (changed || Time.time >= nextRepath) Repath();
        }

        bool ShouldUseAbility(TankClassData d)
        {
            if (d.ability == AbilityType.None || tank.AbilityReady01 < 1f) return false;
            float hp = tank.Health / tank.MaxHealth;
            bool hasTarget = target != null && target.isActiveAndEnabled;
            float dist = hasTarget ? Vector3.Distance(transform.position, target.transform.position) : float.MaxValue;
            switch (d.ability)
            {
                case AbilityType.Nitro:
                    return hasDestination && FlatDistance(transform.position, destination) > 40f && (!hasTarget || dist > d.botEngageRange);
                case AbilityType.Repair:
                    return hp < 0.45f;
                case AbilityType.Shield:
                    return hp < 0.7f && hasTarget && targetVisible && dist < d.botEngageRange;
                case AbilityType.Barrage:
                    return hasTarget && targetVisible && dist > 25f && dist < 120f;
            }
            return false;
        }

        /// <summary>Yakında güçlendirme varsa onu hedefle.</summary>
        bool TryGetNearbyPowerUp(float range, out Vector3 position)
        {
            position = Vector3.zero;
            float best = range;
            bool found = false;
            var list = PowerUp.All;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                float dd = Vector3.Distance(transform.position, list[i].transform.position);
                // Canı azsa onarım kitini daha uzaktan bile ister
                if (list[i].type == PowerUpType.Health && tank.Health < tank.MaxHealth * 0.6f) dd *= 0.5f;
                if (dd < best) { best = dd; position = list[i].transform.position; found = true; }
            }
            return found;
        }

        TankController FindTarget(float range, out bool visible)
        {
            TankController best = null;
            float bestScore = float.MaxValue;
            visible = false;

            var all = TankController.All;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == tank || other.IsDead || other.Team == tank.Team) continue;
                float dist = Vector3.Distance(transform.position, other.transform.position);
                if (dist > range) continue;

                bool los = HasLineOfSight(other);
                float score = dist * (los ? 1f : 2.5f); // görünen hedefleri tercih et
                if (score < bestScore) { bestScore = score; best = other; visible = los; }
            }

            // Menzilde kimse yoksa en yakın düşmana doğru git (ama ateş etme)
            if (best == null)
            {
                float bestDist = float.MaxValue;
                for (int i = 0; i < all.Count; i++)
                {
                    var other = all[i];
                    if (other == tank || other.IsDead || other.Team == tank.Team) continue;
                    float dist = Vector3.Distance(transform.position, other.transform.position);
                    if (dist < bestDist) { bestDist = dist; best = other; }
                }
                visible = false;
                if (MatchManager.Instance != null && MatchManager.Instance.Mode == MatchMode.Capture)
                    best = null; // ele geçirme modunda hedefe odaklan
            }
            return best;
        }

        bool HasLineOfSight(TankController other)
        {
            Vector3 from = tank.muzzle != null ? tank.muzzle.position : transform.position + Vector3.up * 1.5f;
            Vector3 to = other.transform.position + Vector3.up * 1f;
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.1f) return true;

            int count = Physics.RaycastNonAlloc(from, dir / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            TankController hitTank = null;
            bool hitSomething = false;
            for (int i = 0; i < count; i++)
            {
                var t = hits[i].collider.GetComponentInParent<TankController>();
                if (t == tank) continue;
                if (hits[i].distance < best) { best = hits[i].distance; hitTank = t; hitSomething = true; }
            }
            return !hitSomething || hitTank == other;
        }

        bool IsTurretAligned(Vector3 point, float maxAngle)
        {
            Transform t = tank.turret != null ? tank.turret : transform;
            Vector3 dir = point - t.position;
            dir.y = 0f;
            Vector3 fwd = t.forward;
            fwd.y = 0f;
            return Vector3.Angle(fwd, dir) <= maxAngle;
        }

        void Repath()
        {
            nextRepath = Time.time + repathInterval;
            hasPath = false;
            cornerIndex = 1;

            if (!NavMesh.SamplePosition(transform.position, out var fromHit, 4f, NavMesh.AllAreas)) return;
            if (!NavMesh.SamplePosition(destination, out var toHit, 8f, NavMesh.AllAreas)) return;
            if (NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path) &&
                path.status != NavMeshPathStatus.PathInvalid && path.corners.Length > 1)
            {
                hasPath = true;
            }
        }

        Vector2 ComputeMove()
        {
            // Sıkışma kontrolü: gitmek isteyip ilerleyemiyorsa kısa süre geri vites
            if (Time.time < reverseUntil) return new Vector2(reverseTurn, -1f);

            Vector3 next;
            if (hasPath)
            {
                var corners = path.corners;
                while (cornerIndex < corners.Length && FlatDistance(transform.position, corners[cornerIndex]) < 3f)
                    cornerIndex++;
                if (cornerIndex >= corners.Length) return Vector2.zero;
                next = corners[cornerIndex];
            }
            else if (hasDestination && FlatDistance(transform.position, destination) > 3f)
            {
                next = destination; // NavMesh yoksa düz git
            }
            else
            {
                return Vector2.zero;
            }

            Vector3 local = transform.InverseTransformPoint(next);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float turn = Mathf.Clamp(angle / 35f, -1f, 1f);
            float abs = Mathf.Abs(angle);
            float forward = abs < 50f ? 1f : (abs < 100f ? 0.3f : 0f);

            if (Time.time >= nextStuckCheck)
            {
                if (forward > 0f && nextStuckCheck > 0f && FlatDistance(transform.position, lastStuckCheckPos) < 1f)
                {
                    reverseUntil = Time.time + 1.2f;
                    reverseTurn = Random.value < 0.5f ? -1f : 1f;
                    nextRepath = 0f;
                }
                lastStuckCheckPos = transform.position;
                nextStuckCheck = Time.time + 1.5f;
            }

            return new Vector2(turn, forward);
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
