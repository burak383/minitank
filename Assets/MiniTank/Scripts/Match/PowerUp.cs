using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MiniTank
{
    /// <summary>Haritada dönen, üstünden geçen tanka güçlendirme veren obje.</summary>
    public class PowerUp : MonoBehaviour
    {
        public PowerUpType type;
        /// <summary>Online: ağ kimliği (host verir). visualOnly ise toplanamaz, sadece görüntüdür.</summary>
        public int netId;
        public bool visualOnly;
        static readonly List<PowerUp> active = new List<PowerUp>();
        public static IReadOnlyList<PowerUp> All => active;

        public static string Name(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Health: return "Onarım Kiti";
                case PowerUpType.Speed: return "Hız";
                case PowerUpType.Damage: return "Hasar";
                default: return "Hızlı Dolum";
            }
        }

        public static Color ColorOf(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Health: return new Color(0.3f, 0.95f, 0.4f);
                case PowerUpType.Speed: return new Color(0.3f, 0.8f, 1f);
                case PowerUpType.Damage: return new Color(1f, 0.35f, 0.25f);
                default: return new Color(1f, 0.85f, 0.25f);
            }
        }

        Vector3 basePos;
        public Vector3 BasePosition => basePos;

        void OnEnable() { active.Add(this); basePos = transform.position; }
        void OnDisable() { active.Remove(this); }

        void Update()
        {
            transform.position = basePos + Vector3.up * (0.25f * Mathf.Sin(Time.time * 2.5f));
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (visualOnly) return;
            var tank = other.GetComponentInParent<TankController>();
            if (tank == null || tank.IsDead || tank.IsPuppet) return;
            tank.ApplyPowerUp(type);
            Destroy(gameObject);
        }
    }
}
