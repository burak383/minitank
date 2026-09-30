using Fusion;
using UnityEngine;

namespace MiniTank
{
    /// <summary>Online oyuncunun her ağ adımında host'a gönderdiği girdi.</summary>
    public struct NetInput : INetworkInput
    {
        public const int FireBit = 1;
        public const int AbilityBit = 2;

        public Vector2 Move;
        public Vector3 Aim;
        public int Buttons;
    }

    /// <summary>Maçtaki 10 koltuktan biri: gerçek oyuncu veya bot.</summary>
    public struct SeatData : INetworkStruct
    {
        public PlayerRef Player;      // PlayerRef.None = bot
        public int ClassIndex;
        public int Version;           // değişince tank yeniden kurulur
        public int Engine, Armor, Gun;
        public int Camo;              // Economy.Camos içindeki sıra
        public NetworkString<_32> Name;
    }

    /// <summary>Bir tankın host'tan gelen anlık durumu.</summary>
    public struct TankNet : INetworkStruct
    {
        public Vector3 Pos;
        public float Yaw, TurretYaw, GunPitch;
        public float Health, Speed;
        public float Reload, AbilityCooldown, AbilityActive;
        public float BuffHealth, BuffSpeed, BuffDamage, BuffReload;
        public int Kills, Deaths, Assists, SpawnCount;
        public NetworkBool Dead;
    }

    public struct CaptureNet : INetworkStruct
    {
        public float Progress;
        public int Owner;             // -1 = sahipsiz, 0 = Mavi, 1 = Kırmızı
        public NetworkBool Contested;
    }

    public struct PowerUpNet : INetworkStruct
    {
        public int Id;                // 0 = boş
        public Vector3 Pos;
        public int Type;
    }

    /// <summary>Host'ta uzak oyuncunun tankını süren girdi kaynağı.</summary>
    public class RemoteInputSource : ITankInputSource
    {
        TankInputData latest;
        bool fireLatched, abilityLatched;

        public void Push(NetInput input)
        {
            latest.Move = Vector2.ClampMagnitude(input.Move, 1f);
            latest.AimPoint = input.Aim;
            latest.Fire = (input.Buttons & NetInput.FireBit) != 0;
            // Kısa basışlar fizik adımına kadar kaybolmasın
            if (latest.Fire) fireLatched = true;
            if ((input.Buttons & NetInput.AbilityBit) != 0) abilityLatched = true;
        }

        public TankInputData GetInput()
        {
            var d = latest;
            d.Fire = latest.Fire || fireLatched;
            d.Ability = abilityLatched;
            fireLatched = abilityLatched = false;
            return d;
        }
    }
}
