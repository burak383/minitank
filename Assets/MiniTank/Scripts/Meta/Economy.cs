using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniTank
{
    // ====================================================================== Kayıt yapıları

    /// <summary>Bir tank sınıfının geliştirme seviyeleri (0-5).</summary>
    [Serializable]
    public class ClassUpgrade
    {
        public string classId;
        public int engine;
        public int armor;
        public int gun;
    }

    public enum UpgradeStat { Engine = 0, Armor = 1, Gun = 2 }

    public enum MissionType { Play = 0, Win = 1, Kills = 2, Assists = 3, Damage = 4, Abilities = 5, PowerUps = 6 }

    [Serializable]
    public class MissionState
    {
        public int type;
        public int target;
        public int progress;
        public int reward;
        public bool claimed;

        public MissionType Type => (MissionType)type;
        public bool Complete => progress >= target;
    }

    /// <summary>Kamuflaj (görünüm) tanımı.</summary>
    public class CamoDef
    {
        public string id;
        public string name;
        public int price;
        public Color a, b, c;       // desen renkleri
        public bool pattern = true; // false: düz renk
        public float metallic;
        public float smoothness = 0.3f;
    }

    // ====================================================================== Kurallar

    /// <summary>Altın kazancı, geliştirme fiyatları/etkileri, kamuflaj kataloğu, görev havuzu, sezon ödülleri.</summary>
    public static class Economy
    {
        // ------------------------------------------------------------------ Altın

        public const int GoldWin = 100, GoldDraw = 60, GoldLoss = 40, GoldPerKill = 10, GoldPerAssist = 5, GoldMvp = 25;

        public static int MatchGold(MatchOutcome o)
        {
            int g = o.won ? GoldWin : o.draw ? GoldDraw : GoldLoss;
            g += o.kills * GoldPerKill + o.assists * GoldPerAssist;
            if (o.mvp) g += GoldMvp;
            return g;
        }

        /// <summary>Sezon sonunda bitirilen lige göre altın ödülü.</summary>
        public static int SeasonRewardGold(int points)
        {
            int[] rewards = { 200, 400, 700, 1000, 1500, 2500 };
            return rewards[Mathf.Clamp(Leagues.IndexOf(points), 0, rewards.Length - 1)];
        }

        // ------------------------------------------------------------------ Tank geliştirme

        public const int MaxUpgradeLevel = 5;
        static readonly int[] upgradeCosts = { 200, 400, 700, 1100, 1600 };

        /// <summary>Bir sonraki seviyenin fiyatı. Maksimumdaysa -1.</summary>
        public static int UpgradeCost(int currentLevel) =>
            currentLevel >= MaxUpgradeLevel ? -1 : upgradeCosts[Mathf.Clamp(currentLevel, 0, upgradeCosts.Length - 1)];

        public static string StatName(UpgradeStat s) => s == UpgradeStat.Engine ? "Motor" : s == UpgradeStat.Armor ? "Zırh" : "Top";

        public static string StatEffect(UpgradeStat s) =>
            s == UpgradeStat.Engine ? "Seviye başına +%4 hız" :
            s == UpgradeStat.Armor ? "Seviye başına +%5 can" : "Seviye başına +%4 hasar, -%3 dolum süresi";

        public static float SpeedMultiplier(int engine) => 1f + 0.04f * engine;
        public static float HealthMultiplier(int armor) => 1f + 0.05f * armor;
        public static float DamageMultiplier(int gun) => 1f + 0.04f * gun;
        public static float ReloadMultiplier(int gun) => 1f - 0.03f * gun;

        // ------------------------------------------------------------------ Kamuflaj

        public const string DefaultCamo = "standart";

        public static readonly CamoDef[] Camos =
        {
            new CamoDef { id = DefaultCamo, name = "Standart", price = 0, pattern = false },
            new CamoDef { id = "orman", name = "Orman", price = 300,
                a = new Color(0.27f, 0.33f, 0.2f), b = new Color(0.4f, 0.35f, 0.22f), c = new Color(0.14f, 0.16f, 0.12f) },
            new CamoDef { id = "col", name = "Çöl", price = 300,
                a = new Color(0.76f, 0.66f, 0.46f), b = new Color(0.6f, 0.48f, 0.32f), c = new Color(0.86f, 0.8f, 0.64f) },
            new CamoDef { id = "kis", name = "Kış", price = 400,
                a = new Color(0.88f, 0.9f, 0.92f), b = new Color(0.6f, 0.63f, 0.66f), c = new Color(0.35f, 0.37f, 0.4f) },
            new CamoDef { id = "sehir", name = "Şehir", price = 500,
                a = new Color(0.45f, 0.47f, 0.5f), b = new Color(0.28f, 0.3f, 0.33f), c = new Color(0.66f, 0.68f, 0.7f) },
            new CamoDef { id = "gece", name = "Gece", price = 700,
                a = new Color(0.1f, 0.12f, 0.2f), b = new Color(0.18f, 0.2f, 0.3f), c = new Color(0.05f, 0.05f, 0.08f) },
            new CamoDef { id = "kizil", name = "Kızıl", price = 900,
                a = new Color(0.45f, 0.08f, 0.08f), b = new Color(0.25f, 0.05f, 0.05f), c = new Color(0.08f, 0.08f, 0.08f) },
            new CamoDef { id = "altin", name = "Altın", price = 2500, pattern = false,
                a = new Color(0.95f, 0.72f, 0.25f), metallic = 0.85f, smoothness = 0.55f },
        };

        public static CamoDef GetCamo(string id)
        {
            foreach (var c in Camos) if (c.id == id) return c;
            return Camos[0];
        }

        // ------------------------------------------------------------------ Görevler

        struct MissionTemplate
        {
            public MissionType type;
            public int daily, weekly;           // hedef
            public int dailyReward, weeklyReward;
        }

        static readonly MissionTemplate[] templates =
        {
            new MissionTemplate { type = MissionType.Play,      daily = 3,    weekly = 15,    dailyReward = 80,  weeklyReward = 400 },
            new MissionTemplate { type = MissionType.Win,       daily = 2,    weekly = 10,    dailyReward = 120, weeklyReward = 600 },
            new MissionTemplate { type = MissionType.Kills,     daily = 8,    weekly = 50,    dailyReward = 100, weeklyReward = 500 },
            new MissionTemplate { type = MissionType.Assists,   daily = 6,    weekly = 35,    dailyReward = 90,  weeklyReward = 450 },
            new MissionTemplate { type = MissionType.Damage,    daily = 3000, weekly = 20000, dailyReward = 110, weeklyReward = 550 },
            new MissionTemplate { type = MissionType.Abilities, daily = 5,    weekly = 30,    dailyReward = 80,  weeklyReward = 400 },
            new MissionTemplate { type = MissionType.PowerUps,  daily = 3,    weekly = 15,    dailyReward = 80,  weeklyReward = 400 },
        };

        /// <summary>Gün/hafta anahtarı ve kullanıcıya göre her zaman aynı 3 görevi üretir.</summary>
        public static List<MissionState> GenerateMissions(string periodKey, string userId, bool weekly)
        {
            int seed = 17;
            foreach (char ch in periodKey + "|" + userId + (weekly ? "W" : "D")) seed = unchecked(seed * 31 + ch);
            var rng = new System.Random(seed);

            var pool = new List<int>();
            for (int i = 0; i < templates.Length; i++) pool.Add(i);
            var result = new List<MissionState>();
            for (int k = 0; k < 3; k++)
            {
                int pick = rng.Next(pool.Count);
                var t = templates[pool[pick]];
                pool.RemoveAt(pick);
                result.Add(new MissionState
                {
                    type = (int)t.type,
                    target = weekly ? t.weekly : t.daily,
                    reward = weekly ? t.weeklyReward : t.dailyReward,
                });
            }
            return result;
        }

        public static string MissionText(MissionState m)
        {
            switch (m.Type)
            {
                case MissionType.Play: return $"{m.target} maç oyna";
                case MissionType.Win: return $"{m.target} maç kazan";
                case MissionType.Kills: return $"{m.target} düşman imha et";
                case MissionType.Assists: return $"{m.target} asist yap";
                case MissionType.Damage: return $"{m.target} hasar ver";
                case MissionType.Abilities: return $"{m.target} kez yetenek kullan";
                default: return $"{m.target} güçlendirme topla";
            }
        }

        public static int MissionIncrement(MissionType type, MatchOutcome o)
        {
            switch (type)
            {
                case MissionType.Play: return 1;
                case MissionType.Win: return o.won ? 1 : 0;
                case MissionType.Kills: return o.kills;
                case MissionType.Assists: return o.assists;
                case MissionType.Damage: return o.damage;
                case MissionType.Abilities: return o.abilitiesUsed;
                default: return o.powerUps;
            }
        }

        // ------------------------------------------------------------------ Tarih anahtarları (Türkiye saati)

        static DateTime TurkeyNow => DateTime.UtcNow.AddHours(3);

        public static string DayKey() => TurkeyNow.ToString("yyyy-MM-dd");

        /// <summary>Haftanın pazartesi günü (hafta anahtarı).</summary>
        public static string WeekKey()
        {
            var d = TurkeyNow.Date;
            int offset = ((int)d.DayOfWeek + 6) % 7; // pazartesi = 0
            return d.AddDays(-offset).ToString("yyyy-MM-dd");
        }

        public static TimeSpan UntilNextDay()
        {
            var now = TurkeyNow;
            return now.Date.AddDays(1) - now;
        }

        public static TimeSpan UntilNextWeek()
        {
            var now = TurkeyNow;
            int offset = ((int)now.DayOfWeek + 6) % 7;
            return now.Date.AddDays(7 - offset) - now;
        }
    }
}
