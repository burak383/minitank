using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Firebase kurulmadan önce test için kullanılan cihaz içi giriş servisi.
    /// Hesaplar sadece bu cihazda (PlayerPrefs) tutulur; gerçek oyun için Firebase kullanılır.
    /// </summary>
    public class LocalAuthService : IAuthService
    {
        const string SessionKey = "mt_session";
        const string AccountPrefix = "mt_acc_";

        public string BackendName => "Cihaz içi (test) – Firebase henüz kurulmadı";
        public bool IsSignedIn => !string.IsNullOrEmpty(UserId);
        public bool IsGuest { get; private set; }
        public string UserId { get; private set; }
        public string DisplayName { get; private set; }
        public string Email { get; private set; }

        public Task InitializeAsync()
        {
            string session = PlayerPrefs.GetString(SessionKey, "");
            if (!string.IsNullOrEmpty(session))
            {
                var parts = session.Split('|');
                if (parts.Length == 4)
                {
                    UserId = parts[0];
                    IsGuest = parts[1] == "1";
                    Email = parts[2];
                    DisplayName = parts[3];
                }
            }
            return Task.CompletedTask;
        }

        public async Task SignInAsGuestAsync()
        {
            await Task.Delay(300);
            UserId = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            IsGuest = true;
            Email = "";
            DisplayName = PlayerSession.GuestName(UserId);
            SaveSession();
        }

        public async Task SignInWithEmailAsync(string email, string password)
        {
            AuthValidation.Email(email);
            AuthValidation.Password(password);
            await Task.Delay(300);

            email = email.Trim().ToLowerInvariant();
            string stored = PlayerPrefs.GetString(AccountPrefix + email, "");
            if (string.IsNullOrEmpty(stored)) throw new AuthException("E-posta veya şifre hatalı.");
            var parts = stored.Split('|');
            if (parts[0] != Hash(email, password)) throw new AuthException("E-posta veya şifre hatalı.");

            UserId = parts.Length > 2 ? parts[2] : "user_" + email;
            IsGuest = false;
            Email = email;
            DisplayName = parts.Length > 1 ? parts[1] : email;
            SaveSession();
        }

        public async Task RegisterWithEmailAsync(string displayName, string email, string password)
        {
            AuthValidation.Nickname(displayName);
            AuthValidation.Email(email);
            AuthValidation.Password(password);
            await Task.Delay(300);

            email = email.Trim().ToLowerInvariant();
            if (PlayerPrefs.HasKey(AccountPrefix + email))
                throw new AuthException("Bu e-posta ile zaten bir hesap var.");

            // Misafirse aynı kullanıcı kimliği korunur (hesap bağlama)
            string id = IsSignedIn && IsGuest ? UserId : "user_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            PlayerPrefs.SetString(AccountPrefix + email, $"{Hash(email, password)}|{displayName.Trim()}|{id}");

            UserId = id;
            IsGuest = false;
            Email = email;
            DisplayName = displayName.Trim();
            SaveSession();
        }

        public async Task SendPasswordResetAsync(string email)
        {
            AuthValidation.Email(email);
            await Task.Delay(300);
            throw new AuthException("Test modunda şifre sıfırlama e-postası gönderilemez. Firebase kurulunca çalışacak.");
        }

        public void SignOut()
        {
            UserId = DisplayName = Email = null;
            IsGuest = false;
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.Save();
        }

        void SaveSession()
        {
            PlayerPrefs.SetString(SessionKey, $"{UserId}|{(IsGuest ? "1" : "0")}|{Email}|{DisplayName}");
            PlayerPrefs.Save();
        }

        static string Hash(string email, string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("minitank:" + email + ":" + password));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
