using System;
using UnityEngine;

namespace MiniTank
{
    /// <summary>Bir lig kademesi ve kuralları.</summary>
    public class LeagueTier
    {
        public string name;
        public int minPoints;       // bu lige girmek için gereken kupa
        public Color color;
        public int winPoints;       // galibiyette kazanılan temel kupa
        public int lossPoints;      // yenilgide kaybedilen temel kupa
        public int seasonResetTo;   // sezon sonunda düşülecek kupa (yüksek lig = daha büyük düşüş)
    }

    /// <summary>Lig tablosu, puan hesabı ve aylık sezon kuralları.</summary>
    public static class Leagues
    {
        public static readonly LeagueTier[] Tiers =
        {
            new LeagueTier { name = "Bronz",  minPoints = 0,    color = new Color(0.80f, 0.50f, 0.25f), winPoints = 35, lossPoints = 10, seasonResetTo = 0 },
            new LeagueTier { name = "Gümüş",  minPoints = 300,  color = new Color(0.78f, 0.80f, 0.84f), winPoints = 32, lossPoints = 15, seasonResetTo = 150 },
            new LeagueTier { name = "Altın",  minPoints = 700,  color = new Color(1.00f, 0.80f, 0.20f), winPoints = 30, lossPoints = 20, seasonResetTo = 300 },
            new LeagueTier { name = "Platin", minPoints = 1200, color = new Color(0.40f, 0.85f, 0.80f), winPoints = 28, lossPoints = 22, seasonResetTo = 700 },
            new LeagueTier { name = "Elmas",  minPoints = 1800, color = new Color(0.45f, 0.65f, 1.00f), winPoints = 26, lossPoints = 25, seasonResetTo = 1000 },
            new LeagueTier { name = "Usta",   minPoints = 2500, color = new Color(0.85f, 0.40f, 1.00f), winPoints = 25, lossPoints = 30, seasonResetTo = 1200 },
        };

        public const int DrawPoints = 5;
        public const int MvpBonus = 5;
        public const int StreakBonus = 5;      // 3 ve üzeri galibiyet serisinde
        public const int MaxPerformanceBonus = 8;

        public static int IndexOf(int points)
        {
            for (int i = Tiers.Length - 1; i >= 0; i--)
                if (points >= Tiers[i].minPoints) return i;
            return 0;
        }

        public static LeagueTier Get(int points) => Tiers[IndexOf(points)];

        public static LeagueTier Next(int points)
        {
            int i = IndexOf(points);
            return i + 1 < Tiers.Length ? Tiers[i + 1] : null;
        }

        /// <summary>Bulunulan lig içindeki ilerleme (0-1). Son ligde her zaman 1.</summary>
        public static float Progress01(int points)
        {
            var tier = Get(points);
            var next = Next(points);
            if (next == null) return 1f;
            return Mathf.Clamp01((points - tier.minPoints) / (float)(next.minPoints - tier.minPoints));
        }

        /// <summary>Sezon sonunda kupanın düşeceği değer. Kupa asla artmaz.</summary>
        public static int SeasonResetPoints(int points) => Math.Min(points, Get(points).seasonResetTo);

        // ------------------------------------------------------------------ Sezon (Türkiye saatiyle ay başı)

        static DateTime TurkeyNow(DateTime utcNow) => utcNow.AddHours(3);

        public static string SeasonId(DateTime utcNow) => TurkeyNow(utcNow).ToString("yyyy-MM");

        public static string SeasonName(string seasonId)
        {
            string[] months = { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
            if (string.IsNullOrEmpty(seasonId) || seasonId.Length < 7) return seasonId;
            int year, month;
            if (!int.TryParse(seasonId.Substring(0, 4), out year) || !int.TryParse(seasonId.Substring(5, 2), out month)) return seasonId;
            return $"{months[Mathf.Clamp(month, 1, 12) - 1]} {year}";
        }

        public static TimeSpan TimeUntilSeasonEnd(DateTime utcNow)
        {
            var tr = TurkeyNow(utcNow);
            var nextMonth = new DateTime(tr.Year, tr.Month, 1).AddMonths(1);
            return nextMonth - tr;
        }

        /// <summary>İki sezon arasında kaç ay geçtiği (örn. "2026-09" → "2026-11" = 2).</summary>
        public static int MonthsBetween(string fromSeason, string toSeason)
        {
            int fy, fm, ty, tm;
            if (!TryParse(fromSeason, out fy, out fm) || !TryParse(toSeason, out ty, out tm)) return 0;
            return (ty - fy) * 12 + (tm - fm);
        }

        static bool TryParse(string s, out int year, out int month)
        {
            year = month = 0;
            return !string.IsNullOrEmpty(s) && s.Length >= 7 &&
                   int.TryParse(s.Substring(0, 4), out year) && int.TryParse(s.Substring(5, 2), out month);
        }

        // ------------------------------------------------------------------ Maç sonucu

        /// <summary>Maç sonucuna göre kupa değişimi.</summary>
        public static int CalculateDelta(int points, MatchOutcome o, int winStreakBefore)
        {
            var tier = Get(points);
            int performance = Mathf.Min(MaxPerformanceBonus, o.kills + o.assists / 2);

            int delta;
            if (o.draw)
            {
                delta = DrawPoints + performance / 2;
            }
            else if (o.won)
            {
                delta = tier.winPoints + performance;
                if (o.mvp) delta += MvpBonus;
                if (winStreakBefore + 1 >= 3) delta += StreakBonus;
            }
            else
            {
                // İyi oynayan kaybeden daha az kupa kaybeder (en fazla yarısı affedilir)
                int forgiven = Mathf.Min(performance + (o.mvp ? MvpBonus : 0), tier.lossPoints / 2);
                delta = -(tier.lossPoints - forgiven);
            }
            return delta;
        }
    }

    public struct MatchOutcome
    {
        public bool won;
        public bool draw;
        public bool mvp;
        public int kills;
        public int assists;
        public int deaths;
        public int damage;
        public int abilitiesUsed;
        public int powerUps;
    }

    /// <summary>Bir maç sonrası lig değişimi (arayüzde gösterilir).</summary>
    public class LeagueChange
    {
        public int before;
        public int after;
        public int Delta => after - before;
        public LeagueTier fromTier;
        public LeagueTier toTier;
        public int goldEarned;
        public int missionsCompleted;
        public bool Promoted => Array.IndexOf(Leagues.Tiers, toTier) > Array.IndexOf(Leagues.Tiers, fromTier);
        public bool Demoted => Array.IndexOf(Leagues.Tiers, toTier) < Array.IndexOf(Leagues.Tiers, fromTier);
    }
}
