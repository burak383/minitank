using UnityEngine;

namespace MiniTank
{
    public enum Team { Blue = 0, Red = 1 }

    public enum MatchMode { TeamDeathmatch = 0, Capture = 1, Elimination = 2 }

    /// <summary>Sınıfa özel yetenek.</summary>
    public enum AbilityType { None, Nitro, Repair, Shield, Barrage }

    /// <summary>Haritada çıkan güçlendirmeler.</summary>
    public enum PowerUpType { Health = 0, Speed = 1, Damage = 2, Reload = 3 }

    /// <summary>İsabetin tankın hangi bölgesine geldiği (zırh hesabı).</summary>
    public enum HitZone { Front, Side, Rear, Splash }

    public struct DamageInfo
    {
        public TankController victim;
        public TankController attacker;
        public float amount;
        public Vector3 point;
        public Vector3 direction;   // merminin gidiş yönü
        public HitZone zone;
        public bool killed;
    }

    /// <summary>
    /// Bir tankın tek bir fizik adımındaki girdisi.
    /// Online'a geçerken bu yapı Photon Fusion'daki INetworkInput'a dönüşecek;
    /// tank mantığı sadece bu veriyle çalıştığı için oyun kodu değişmeyecek.
    /// </summary>
    public struct TankInputData
    {
        public Vector2 Move;      // x: sağa/sola dönüş, y: ileri/geri
        public Vector3 AimPoint;  // dünyada nişan alınan nokta
        public bool Fire;
        public bool Ability;
    }

    /// <summary>Oyuncu, bot veya (ileride) ağ girdisi bu arayüzü uygular.</summary>
    public interface ITankInputSource
    {
        TankInputData GetInput();
    }

    public static class TeamColors
    {
        public static readonly Color Blue = new Color(0.20f, 0.45f, 0.95f);
        public static readonly Color Red = new Color(0.90f, 0.25f, 0.20f);
        public static readonly Color Neutral = new Color(0.75f, 0.75f, 0.75f);

        // Tank gövde boyaları: mavi takım koyu yeşil-gri, kırmızı takım çöl kumu.
        // Takımı asıl belli eden, parlak renkli işaretler (şerit, bayrak, tavan paneli).
        public static readonly Color BlueBody = new Color(0.30f, 0.34f, 0.28f);
        public static readonly Color RedBody = new Color(0.60f, 0.50f, 0.35f);
        public static Color Body(Team team) => team == Team.Blue ? BlueBody : RedBody;

        public static Color Get(Team team) => team == Team.Blue ? Blue : Red;
        public static string Name(Team team) => team == Team.Blue ? "Mavi" : "Kırmızı";
    }

    /// <summary>Menüden arena sahnesine taşınan seçimler.</summary>
    public static class MatchSettings
    {
        public static bool HasSelection;
        public static MatchMode Mode = MatchMode.TeamDeathmatch;
        public static int PlayerClassIndex;
        /// <summary>Gerçek oyunculara karşı online maç (Photon Fusion).</summary>
        public static bool Online;
        /// <summary>Seçilen arenanın sırası (online eşleştirme için).</summary>
        public static int ArenaIndex;
        /// <summary>Özel oda kodu (arkadaşla oynama). null ise rastgele eşleştirme.</summary>
        public static string RoomCode;
        /// <summary>Özel odayı bu cihaz mı kurdu?</summary>
        public static bool IsRoomHost;
        /// <summary>Eğitim maçı (ilk giriş).</summary>
        public static bool Tutorial;
    }

    /// <summary>
    /// Özel oda kodları. İlk harf mod + arenayı taşır (katılan oyuncu doğru arenayı kendiliğinden yükler),
    /// kalan 4 karakter rastgeledir. Karışan karakterler (I, O, 0, 1) kullanılmaz.
    /// </summary>
    public static class RoomCodes
    {
        const string Head = "ABCDEFGHJKLM";                 // 3 mod x 4 arena = 12
        const string Body = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int Length = 5;

        public static string Create(MatchMode mode, int arenaIndex)
        {
            int idx = Mathf.Clamp((int)mode, 0, 2) * 4 + Mathf.Clamp(arenaIndex, 0, 3);
            var sb = new System.Text.StringBuilder();
            sb.Append(Head[idx]);
            for (int i = 1; i < Length; i++) sb.Append(Body[Random.Range(0, Body.Length)]);
            return sb.ToString();
        }

        /// <summary>Kodu temizler (boşluk, tire, küçük harf).</summary>
        public static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in code.ToUpperInvariant())
                if (Body.IndexOf(c) >= 0) sb.Append(c);
            return sb.ToString();
        }

        public static bool TryParse(string code, out MatchMode mode, out int arenaIndex)
        {
            mode = MatchMode.TeamDeathmatch; arenaIndex = 0;
            code = Normalize(code);
            if (code.Length != Length) return false;
            int idx = Head.IndexOf(code[0]);
            if (idx < 0) return false;
            for (int i = 1; i < code.Length; i++) if (Body.IndexOf(code[i]) < 0) return false;
            mode = (MatchMode)(idx / 4);
            arenaIndex = idx % 4;
            return true;
        }
    }
}
