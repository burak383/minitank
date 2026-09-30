using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiniTank
{
    /// <summary>
    /// Giriş ekranı: misafir olarak oyna, e-posta ile giriş, hesap oluştur, şifremi unuttum.
    /// Daha önce giriş yapılmışsa doğrudan ana menüye geçer.
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        [Header("Paneller")]
        public GameObject loadingPanel;
        public GameObject choicePanel;
        public GameObject loginPanel;
        public GameObject registerPanel;

        [Header("Seçim paneli")]
        public Button guestButton;
        public Button openLoginButton;
        public Button openRegisterButton;

        [Header("Giriş paneli")]
        public InputField loginEmail;
        public InputField loginPassword;
        public Button loginButton;
        public Button forgotButton;
        public Button loginBackButton;

        [Header("Kayıt paneli")]
        public Text registerTitle;
        public InputField registerName;
        public InputField registerEmail;
        public InputField registerPassword;
        public InputField registerPassword2;
        public Button registerButton;
        public Button registerBackButton;

        [Header("Genel")]
        public Text statusText;
        public Text backendText;
        public string menuSceneName = "MainMenu";

        /// <summary>Ana menüdeki "Hesabını kaydet" butonu bunu açar: misafir hesabı e-postaya bağlanır.</summary>
        public static bool OpenRegisterForLinking;

        bool busy;

        void Awake()
        {
            guestButton.onClick.AddListener(() => Run(PlayAsGuest));
            openLoginButton.onClick.AddListener(() => Show(loginPanel));
            openRegisterButton.onClick.AddListener(() => ShowRegister(false));
            loginButton.onClick.AddListener(() => Run(Login));
            forgotButton.onClick.AddListener(() => Run(ForgotPassword));
            loginBackButton.onClick.AddListener(() => Show(choicePanel));
            registerButton.onClick.AddListener(() => Run(Register));
            registerBackButton.onClick.AddListener(BackFromRegister);
        }

        async void Start()
        {
            Show(loadingPanel);
            SetStatus("Bağlanıyor...", false);
            var auth = AuthManager.Service;
            if (backendText != null) backendText.text = "Giriş servisi: " + auth.BackendName;

            try
            {
                await auth.InitializeAsync();
            }
            catch (Exception e)
            {
                Show(choicePanel);
                SetStatus(e is AuthException ? e.Message : "Giriş servisi başlatılamadı: " + e.Message, true);
                return;
            }

            if (OpenRegisterForLinking && auth.IsSignedIn && auth.IsGuest)
            {
                ShowRegister(true);
                return;
            }
            OpenRegisterForLinking = false;

            if (auth.IsSignedIn)
            {
                GoToMenu();
                return;
            }
            Show(choicePanel);
            SetStatus("", false);
        }

        // ------------------------------------------------------------------ Akışlar

        async Task PlayAsGuest()
        {
            SetStatus("Misafir girişi yapılıyor...", false);
            await AuthManager.Service.SignInAsGuestAsync();
            GoToMenu();
        }

        async Task Login()
        {
            SetStatus("Giriş yapılıyor...", false);
            await AuthManager.Service.SignInWithEmailAsync(loginEmail.text, loginPassword.text);
            GoToMenu();
        }

        async Task ForgotPassword()
        {
            SetStatus("Gönderiliyor...", false);
            await AuthManager.Service.SendPasswordResetAsync(loginEmail.text);
            SetStatus("Şifre sıfırlama bağlantısı e-posta adresine gönderildi.", false);
        }

        async Task Register()
        {
            if (registerPassword.text != registerPassword2.text)
                throw new AuthException("Şifreler aynı değil.");
            SetStatus("Hesap oluşturuluyor...", false);
            await AuthManager.Service.RegisterWithEmailAsync(registerName.text, registerEmail.text, registerPassword.text);
            OpenRegisterForLinking = false;
            GoToMenu();
        }

        void BackFromRegister()
        {
            if (OpenRegisterForLinking)
            {
                OpenRegisterForLinking = false;
                GoToMenu();
            }
            else Show(choicePanel);
        }

        // ------------------------------------------------------------------ Yardımcılar

        async void Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            SetInteractable(false);
            try
            {
                await action();
            }
            catch (AuthException e)
            {
                SetStatus(e.Message, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("Bir hata oluştu: " + e.Message, true);
            }
            finally
            {
                busy = false;
                SetInteractable(true);
            }
        }

        void ShowRegister(bool linking)
        {
            OpenRegisterForLinking = linking;
            if (registerTitle != null)
                registerTitle.text = linking
                    ? "Misafir hesabını kaydet\n<size=26>İlerlemen korunur, başka cihazlarda da giriş yapabilirsin.</size>"
                    : "Hesap oluştur";
            Show(registerPanel);
            SetStatus("", false);
        }

        void Show(GameObject panel)
        {
            loadingPanel.SetActive(panel == loadingPanel);
            choicePanel.SetActive(panel == choicePanel);
            loginPanel.SetActive(panel == loginPanel);
            registerPanel.SetActive(panel == registerPanel);
            SetStatus("", false);
        }

        void SetStatus(string message, bool isError)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = isError ? new Color(1f, 0.45f, 0.4f) : new Color(0.85f, 0.9f, 1f);
        }

        void SetInteractable(bool value)
        {
            foreach (var b in GetComponentsInChildren<Button>(true)) b.interactable = value;
            foreach (var f in GetComponentsInChildren<InputField>(true)) f.interactable = value;
        }

        void GoToMenu()
        {
            if (Application.CanStreamedLevelBeLoaded(menuSceneName)) SceneManager.LoadScene(menuSceneName);
            else SetStatus($"'{menuSceneName}' sahnesi Build Settings'te yok.", true);
        }
    }
}
