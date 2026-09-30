#if MINITANK_FIREBASE
using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Firebase Authentication ile misafir ve e-posta/şifre girişi.
    /// Bu dosya sadece Firebase Auth SDK projeye eklendiğinde derlenir
    /// (MINITANK_FIREBASE sembolü otomatik eklenir).
    /// </summary>
    public class FirebaseAuthService : IAuthService
    {
        FirebaseAuth auth;
        bool ready;

        public string BackendName => "Firebase";
        public bool IsSignedIn => ready && auth.CurrentUser != null;
        public bool IsGuest => IsSignedIn && auth.CurrentUser.IsAnonymous;
        public string UserId => IsSignedIn ? auth.CurrentUser.UserId : null;
        public string DisplayName => IsSignedIn ? auth.CurrentUser.DisplayName : null;
        public string Email => IsSignedIn ? auth.CurrentUser.Email : null;

        public async Task InitializeAsync()
        {
            if (ready) return;
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
                throw new AuthException("Firebase başlatılamadı: " + status);
            auth = FirebaseAuth.DefaultInstance;
            ready = true;
        }

        public async Task SignInAsGuestAsync()
        {
            await EnsureReady();
            await Run(auth.SignInAnonymouslyAsync());
        }

        public async Task SignInWithEmailAsync(string email, string password)
        {
            AuthValidation.Email(email);
            AuthValidation.Password(password);
            await EnsureReady();
            await Run(auth.SignInWithEmailAndPasswordAsync(email.Trim(), password));
        }

        public async Task RegisterWithEmailAsync(string displayName, string email, string password)
        {
            AuthValidation.Nickname(displayName);
            AuthValidation.Email(email);
            AuthValidation.Password(password);
            await EnsureReady();

            if (auth.CurrentUser != null && auth.CurrentUser.IsAnonymous)
            {
                // Misafir hesabını e-postaya bağla: kullanıcı kimliği ve ilerleme korunur
                var credential = EmailAuthProvider.GetCredential(email.Trim(), password);
                await Run(auth.CurrentUser.LinkWithCredentialAsync(credential));
            }
            else
            {
                await Run(auth.CreateUserWithEmailAndPasswordAsync(email.Trim(), password));
            }

            await Run(auth.CurrentUser.UpdateUserProfileAsync(new UserProfile { DisplayName = displayName.Trim() }));
        }

        public async Task SendPasswordResetAsync(string email)
        {
            AuthValidation.Email(email);
            await EnsureReady();
            await Run(auth.SendPasswordResetEmailAsync(email.Trim()));
        }

        public void SignOut()
        {
            if (ready) auth.SignOut();
        }

        async Task EnsureReady()
        {
            if (!ready) await InitializeAsync();
        }

        /// <summary>Firebase hatalarını Türkçe mesaja çevirir.</summary>
        static async Task Run(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                var baseEx = e.GetBaseException();
                var fe = baseEx as FirebaseException;
                Debug.LogWarning("[MiniTank] Firebase giriş hatası: " + (fe != null ? "kod " + fe.ErrorCode + " (" + (AuthError)fe.ErrorCode + ") " : "") + baseEx.Message);
                if (fe == null) throw new AuthException("Beklenmeyen bir hata oluştu: " + baseEx.Message);
                throw new AuthException(Translate(((AuthError)fe.ErrorCode).ToString(), fe.Message));
            }
        }

        static string Translate(string code, string message)
        {
            // Yeni Firebase projelerinde "e-posta numaralandırma koruması" açık: yanlış şifre ve
            // olmayan hesap için ayrı kod yerine genel bir hata döner; mesaj metninden anlaşılır.
            string m = (message ?? "").ToUpperInvariant();
            if (m.Contains("INVALID_LOGIN_CREDENTIALS") || m.Contains("INVALID_PASSWORD") || m.Contains("EMAIL_NOT_FOUND") ||
                m.Contains("INVALID_CREDENTIAL") || m.Contains("CREDENTIAL IS INCORRECT") || m.Contains("MALFORMED OR HAS EXPIRED"))
                return "E-posta veya şifre hatalı ya da bu e-postayla hesap yok. Hesabın yoksa önce 'Kayıt ol'u kullan.";
            if (m.Contains("OPERATION_NOT_ALLOWED") || m.Contains("PASSWORD SIGN-IN IS DISABLED"))
                return "E-posta ile giriş Firebase konsolunda açık değil.";
            if (m.Contains("EMAIL_EXISTS")) return "Bu e-posta ile zaten bir hesap var.";
            if (m.Contains("NETWORK")) return "İnternet bağlantısı yok. Bağlantını kontrol et.";

            switch (code)
            {
                case "InvalidEmail": return "Geçerli bir e-posta adresi gir.";
                case "WrongPassword":
                case "UserNotFound":
                case "InvalidCredential": return "E-posta veya şifre hatalı.";
                case "EmailAlreadyInUse":
                case "CredentialAlreadyInUse": return "Bu e-posta ile zaten bir hesap var.";
                case "WeakPassword": return "Şifre çok zayıf. En az 6 karakter kullan.";
                case "NetworkRequestFailed": return "İnternet bağlantısı yok. Bağlantını kontrol et.";
                case "TooManyRequests": return "Çok fazla deneme yapıldı. Biraz bekleyip tekrar dene.";
                case "UserDisabled": return "Bu hesap devre dışı bırakılmış.";
                case "OperationNotAllowed": return "Bu giriş yöntemi Firebase konsolunda açık değil.";
                case "MissingEmail": return "E-posta adresini gir.";
                case "MissingPassword": return "Şifreni gir.";
                default: return "Giriş yapılamadı (" + code + "). Ayrıntı Console'da.";
            }
        }
    }
}
#endif
