using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniTank
{
    /// <summary>Giriş sisteminin ortak arayüzü. Firebase veya cihaz içi (test) servis bunu uygular.</summary>
    public interface IAuthService
    {
        /// <summary>Kullanıcıya gösterilecek kısa açıklama, örn. "Firebase" veya "Cihaz içi (test)".</summary>
        string BackendName { get; }
        bool IsSignedIn { get; }
        bool IsGuest { get; }
        string UserId { get; }
        string DisplayName { get; }
        string Email { get; }

        Task InitializeAsync();
        Task SignInAsGuestAsync();
        Task SignInWithEmailAsync(string email, string password);
        /// <summary>Yeni hesap açar. Kullanıcı misafirse misafir hesabı bu e-postaya bağlanır (ilerleme korunur).</summary>
        Task RegisterWithEmailAsync(string displayName, string email, string password);
        Task SendPasswordResetAsync(string email);
        void SignOut();
    }

    /// <summary>Kullanıcıya doğrudan gösterilebilecek Türkçe hata mesajı taşır.</summary>
    public class AuthException : Exception
    {
        public AuthException(string message) : base(message) { }
    }

    /// <summary>Aktif giriş servisini sağlar. Firebase SDK kuruluysa Firebase, değilse cihaz içi servis kullanılır.</summary>
    public static class AuthManager
    {
        static IAuthService service;

        public static IAuthService Service
        {
            get
            {
                if (service == null)
                {
#if MINITANK_FIREBASE
                    service = new FirebaseAuthService();
#else
                    service = new LocalAuthService();
#endif
                }
                return service;
            }
        }
    }

    /// <summary>Oyun içinde kullanılan oturum bilgileri (maçta oyuncu adı vb.).</summary>
    public static class PlayerSession
    {
        public static bool IsSignedIn => AuthManager.Service.IsSignedIn;
        public static string DisplayName
        {
            get
            {
                var s = AuthManager.Service;
                if (!s.IsSignedIn) return "Oyuncu";
                return string.IsNullOrEmpty(s.DisplayName) ? GuestName(s.UserId) : s.DisplayName;
            }
        }

        public static string GuestName(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return "Misafir";
            // Her cihazda aynı sonucu veren basit karma (string.GetHashCode platforma göre değişebilir)
            int hash = 17;
            foreach (char ch in userId) hash = unchecked(hash * 31 + ch);
            hash = Mathf.Abs(hash % 10000);
            return $"Misafir{hash:0000}";
        }
    }

    public static class AuthValidation
    {
        public static void Email(string email)
        {
            email = email?.Trim() ?? "";
            int at = email.IndexOf('@');
            if (at < 1 || email.LastIndexOf('.') < at + 2 || email.EndsWith("."))
                throw new AuthException("Geçerli bir e-posta adresi gir.");
        }

        public static void Password(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
                throw new AuthException("Şifre en az 6 karakter olmalı.");
        }

        public static void Nickname(string name)
        {
            name = name?.Trim() ?? "";
            if (name.Length < 3 || name.Length > 16)
                throw new AuthException("Oyuncu adı 3 ile 16 karakter arasında olmalı.");
        }
    }
}
