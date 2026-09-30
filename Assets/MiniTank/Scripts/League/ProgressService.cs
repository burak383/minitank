using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniTank
{
    /// <summary>Oyuncunun kalıcı ilerlemesi: kupa, lig, sezon ve istatistikler.</summary>
    [Serializable]
    public class PlayerProgress
    {
        public int points;
        public string season;
        public int seasonHighPoints;
        public int winStreak;

        // Tüm zamanlar istatistikleri
        public int matches, wins, losses, draws;
        public int kills, assists, deaths;
        public int bestPointsEver;

        // Sezon bitişi bildirimi
        public bool seasonNoticePending;
        public string lastSeason;
        public int lastSeasonPoints;
        public int lastSeasonHighPoints;
        public int lastSeasonResetTo;
        public int seasonRewardGold;     // alınmayı bekleyen sezon ödülü

        // Ekonomi
        public int gold;
        public int totalDamage;
        public List<ClassUpgrade> upgrades = new List<ClassUpgrade>();
        public List<string> ownedCamos = new List<string>();
        public string equippedCamo = Economy.DefaultCamo;

        // Görevler
        public string dailyKey, weeklyKey;
        public List<MissionState> daily = new List<MissionState>();
        public List<MissionState> weekly = new List<MissionState>();
    }

    public interface IProgressStore
    {
        Task<PlayerProgress> LoadAsync(string userId);
        Task SaveAsync(string userId, PlayerProgress progress, string displayName);
    }

    /// <summary>Cihazda saklama (PlayerPrefs). Çevrimdışı yedek olarak her zaman kullanılır.</summary>
    public class LocalProgressStore : IProgressStore
    {
        static string Key(string userId) => "mt_progress_" + userId;

        public Task<PlayerProgress> LoadAsync(string userId)
        {
            string json = PlayerPrefs.GetString(Key(userId), "");
            return Task.FromResult(string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PlayerProgress>(json));
        }

        public Task SaveAsync(string userId, PlayerProgress progress, string displayName)
        {
            PlayerPrefs.SetString(Key(userId), JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// İlerlemeyi yükler, aylık sezon sıfırlamasını uygular ve maç sonuçlarını işler.
    /// Firestore kuruluysa bulutta, değilse cihazda saklar.
    /// </summary>
    public static class ProgressService
    {
        static readonly LocalProgressStore local = new LocalProgressStore();
        static IProgressStore remote;
        static string loadedFor;

        public static PlayerProgress Current { get; private set; }
        public static bool IsLoaded => Current != null && loadedFor == AuthManager.Service.UserId;
        public static bool UsesCloud => Remote != null;
        public static LeagueChange LastChange { get; private set; }

        static IProgressStore Remote
        {
            get
            {
#if MINITANK_FIRESTORE
                if (remote == null) remote = new FirestoreProgressStore();
#endif
                return remote;
            }
        }

        public static async Task LoadAsync()
        {
            var auth = AuthManager.Service;
            if (!auth.IsSignedIn) { Current = null; loadedFor = null; return; }
            string uid = auth.UserId;

            PlayerProgress progress = null;
            if (Remote != null)
            {
                try { progress = await Remote.LoadAsync(uid); }
                catch (Exception e) { Debug.LogWarning("[MiniTank] Bulut ilerlemesi okunamadı, cihazdaki kullanılıyor: " + e.Message); }
            }
            if (progress == null) progress = await local.LoadAsync(uid);
            if (progress == null) progress = new PlayerProgress();

            Current = progress;
            loadedFor = uid;

            bool changed = ApplySeasonReset(progress);
            changed |= EnsureMissions(progress, uid);
            if (changed) await SaveAsync();
        }

        public static void Clear()
        {
            Current = null;
            loadedFor = null;
            LastChange = null;
        }

        /// <summary>Ay değiştiyse ligi düşürür. Birden fazla ay geçtiyse her ay için bir kez uygular.</summary>
        static bool ApplySeasonReset(PlayerProgress p)
        {
            string now = Leagues.SeasonId(DateTime.UtcNow);
            if (string.IsNullOrEmpty(p.season))
            {
                p.season = now;
                p.seasonHighPoints = p.points;
                return true;
            }
            if (p.season == now) return false;

            int months = Mathf.Clamp(Leagues.MonthsBetween(p.season, now), 1, 12);
            p.seasonRewardGold += Economy.SeasonRewardGold(p.points);
            p.lastSeason = p.season;
            p.lastSeasonPoints = p.points;
            p.lastSeasonHighPoints = p.seasonHighPoints;

            int points = p.points;
            for (int i = 0; i < months; i++) points = Leagues.SeasonResetPoints(points);

            p.points = points;
            p.lastSeasonResetTo = points;
            p.season = now;
            p.seasonHighPoints = points;
            p.winStreak = 0;
            p.seasonNoticePending = true;
            return true;
        }

        /// <summary>Maç sonucunu işler, kupayı günceller ve kaydeder.</summary>
        public static async Task<LeagueChange> ReportMatchAsync(MatchOutcome o)
        {
            if (!IsLoaded) await LoadAsync();
            var p = Current;
            if (p == null) return null;
            ApplySeasonReset(p);

            int before = p.points;
            int delta = Leagues.CalculateDelta(before, o, p.winStreak);
            p.points = Mathf.Max(0, before + delta);

            p.matches++;
            if (o.draw) { p.draws++; p.winStreak = 0; }
            else if (o.won) { p.wins++; p.winStreak++; }
            else { p.losses++; p.winStreak = 0; }
            p.kills += o.kills;
            p.assists += o.assists;
            p.deaths += o.deaths;
            p.seasonHighPoints = Mathf.Max(p.seasonHighPoints, p.points);
            p.bestPointsEver = Mathf.Max(p.bestPointsEver, p.points);
            p.totalDamage += o.damage;

            int gold = Economy.MatchGold(o);
            p.gold += gold;

            // Görev ilerlemesi
            EnsureMissions(p, AuthManager.Service.UserId);
            int completed = 0;
            foreach (var list in new[] { p.daily, p.weekly })
                foreach (var m in list)
                {
                    if (m.claimed || m.Complete) continue;
                    m.progress = Mathf.Min(m.target, m.progress + Economy.MissionIncrement(m.Type, o));
                    if (m.Complete) completed++;
                }

            LastChange = new LeagueChange
            {
                before = before,
                after = p.points,
                fromTier = Leagues.Get(before),
                toTier = Leagues.Get(p.points),
                goldEarned = gold,
                missionsCompleted = completed,
            };

            await SaveAsync();
            return LastChange;
        }

        /// <summary>Sezon özetini kapatır ve bekleyen sezon ödülünü hesaba ekler.</summary>
        public static async Task DismissSeasonNoticeAsync()
        {
            if (Current == null || !Current.seasonNoticePending) return;
            Current.seasonNoticePending = false;
            Current.gold += Current.seasonRewardGold;
            Current.seasonRewardGold = 0;
            await SaveAsync();
        }

        // ================================================================== Görevler

        /// <summary>Gün veya hafta değiştiyse yeni görevleri üretir.</summary>
        static bool EnsureMissions(PlayerProgress p, string uid)
        {
            bool changed = false;
            string day = Economy.DayKey(), week = Economy.WeekKey();
            if (p.dailyKey != day || p.daily == null || p.daily.Count == 0)
            {
                p.dailyKey = day;
                p.daily = Economy.GenerateMissions(day, uid ?? "", false);
                changed = true;
            }
            if (p.weeklyKey != week || p.weekly == null || p.weekly.Count == 0)
            {
                p.weeklyKey = week;
                p.weekly = Economy.GenerateMissions(week, uid ?? "", true);
                changed = true;
            }
            return changed;
        }

        /// <summary>Görevler ekranı açıldığında gün değişmiş olabilir.</summary>
        public static async Task RefreshMissionsAsync()
        {
            if (Current != null && EnsureMissions(Current, AuthManager.Service.UserId)) await SaveAsync();
        }

        public static async Task<bool> ClaimMissionAsync(bool weekly, int index)
        {
            var p = Current;
            if (p == null) return false;
            var list = weekly ? p.weekly : p.daily;
            if (index < 0 || index >= list.Count) return false;
            var m = list[index];
            if (!m.Complete || m.claimed) return false;
            m.claimed = true;
            p.gold += m.reward;
            await SaveAsync();
            return true;
        }

        public static int ClaimableMissionCount
        {
            get
            {
                if (Current == null) return 0;
                int n = 0;
                foreach (var m in Current.daily) if (m.Complete && !m.claimed) n++;
                foreach (var m in Current.weekly) if (m.Complete && !m.claimed) n++;
                return n;
            }
        }

        // ================================================================== Geliştirme ve kamuflaj

        public static ClassUpgrade GetUpgrade(string classId)
        {
            if (Current == null) return new ClassUpgrade { classId = classId };
            foreach (var u in Current.upgrades) if (u.classId == classId) return u;
            var created = new ClassUpgrade { classId = classId };
            Current.upgrades.Add(created);
            return created;
        }

        public static int GetLevel(ClassUpgrade u, UpgradeStat s) => s == UpgradeStat.Engine ? u.engine : s == UpgradeStat.Armor ? u.armor : u.gun;

        public static async Task<bool> TryBuyUpgradeAsync(string classId, UpgradeStat stat)
        {
            var p = Current;
            if (p == null) return false;
            var u = GetUpgrade(classId);
            int level = GetLevel(u, stat);
            int cost = Economy.UpgradeCost(level);
            if (cost < 0 || p.gold < cost) return false;
            p.gold -= cost;
            if (stat == UpgradeStat.Engine) u.engine++;
            else if (stat == UpgradeStat.Armor) u.armor++;
            else u.gun++;
            await SaveAsync();
            return true;
        }

        /// <summary>Ödüllü reklam veya satın alma ile altın ekler.</summary>
        public static async Task<bool> AddGoldAsync(int amount)
        {
            if (Current == null || amount <= 0) return false;
            Current.gold += amount;
            await SaveAsync();
            return true;
        }

        public static bool OwnsCamo(string id) =>
            id == Economy.DefaultCamo || (Current != null && Current.ownedCamos.Contains(id));

        public static async Task<bool> TryBuyCamoAsync(string id)
        {
            var p = Current;
            var camo = Economy.GetCamo(id);
            if (p == null || OwnsCamo(id) || p.gold < camo.price) return false;
            p.gold -= camo.price;
            p.ownedCamos.Add(id);
            p.equippedCamo = id;
            await SaveAsync();
            return true;
        }

        public static async Task EquipCamoAsync(string id)
        {
            if (Current == null || !OwnsCamo(id)) return;
            Current.equippedCamo = id;
            await SaveAsync();
        }

        static async Task SaveAsync()
        {
            var auth = AuthManager.Service;
            if (Current == null || !auth.IsSignedIn) return;
            string uid = auth.UserId;

            await local.SaveAsync(uid, Current, PlayerSession.DisplayName);
            if (Remote != null)
            {
                try { await Remote.SaveAsync(uid, Current, PlayerSession.DisplayName); }
                catch (Exception e) { Debug.LogWarning("[MiniTank] İlerleme buluta kaydedilemedi (cihazda duruyor): " + e.Message); }
            }
        }
    }
}
