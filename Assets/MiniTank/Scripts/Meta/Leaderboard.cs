using System.Collections.Generic;
using System.Threading.Tasks;
#if MINITANK_FIRESTORE
using Firebase.Firestore;
#endif

namespace MiniTank
{
    public class LeaderboardEntry
    {
        public string userId;
        public string name;
        public string league;
        public int points;
    }

    /// <summary>
    /// Kupa sıralaması. Firestore'daki "players" koleksiyonundan en yüksek kupalı oyuncuları okur.
    /// Firestore yoksa kullanılamaz.
    /// </summary>
    public static class Leaderboard
    {
#if MINITANK_FIRESTORE
        public static bool Available => true;
#else
        public static bool Available => false;
#endif

        public static async Task<List<LeaderboardEntry>> TopAsync(int limit)
        {
            var list = new List<LeaderboardEntry>();
#if MINITANK_FIRESTORE
            var query = FirebaseFirestore.DefaultInstance.Collection("players")
                .OrderByDescending("points")
                .Limit(limit);
            var snapshot = await query.GetSnapshotAsync();
            foreach (var doc in snapshot.Documents)
            {
                string name, league;
                long points;
                if (!doc.TryGetValue("name", out name)) name = "?";
                if (!doc.TryGetValue("league", out league)) league = "";
                if (!doc.TryGetValue("points", out points)) points = 0;
                list.Add(new LeaderboardEntry { userId = doc.Id, name = name, league = league, points = (int)points });
            }
#else
            await Task.CompletedTask;
#endif
            return list;
        }
    }
}
