#if MINITANK_FIRESTORE
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// İlerlemeyi Firestore'da "players/{kullanıcıId}" belgesinde saklar.
    /// "points", "league" ve "name" alanları ileride sıralama tablosu için ayrıca yazılır.
    /// Sadece Firebase Firestore SDK eklendiğinde derlenir (MINITANK_FIRESTORE otomatik eklenir).
    /// </summary>
    public class FirestoreProgressStore : IProgressStore
    {
        static DocumentReference Doc(string userId) =>
            FirebaseFirestore.DefaultInstance.Collection("players").Document(userId);

        public async Task<PlayerProgress> LoadAsync(string userId)
        {
            var snapshot = await Doc(userId).GetSnapshotAsync();
            if (!snapshot.Exists) return null;
            string json;
            if (!snapshot.TryGetValue("progress", out json) || string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<PlayerProgress>(json);
        }

        public Task SaveAsync(string userId, PlayerProgress progress, string displayName)
        {
            var data = new Dictionary<string, object>
            {
                { "progress", JsonUtility.ToJson(progress) },
                { "points", progress.points },
                { "league", Leagues.Get(progress.points).name },
                { "season", progress.season },
                { "name", displayName ?? "" },
                { "updatedAt", FieldValue.ServerTimestamp },
            };
            return Doc(userId).SetAsync(data, SetOptions.MergeAll);
        }
    }
}
#endif
