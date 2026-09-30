using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MiniTank.EditorTools
{
    /// <summary>Giriş ekranı sahnesini (misafir / e-posta / kayıt / şifremi unuttum) oluşturur.</summary>
    public static partial class MiniTankBuilder
    {
        const string LoginSceneName = "Login";

        [MenuItem("MiniTank/Sadece Giriş ve Menü Sahnelerini Kur", priority = 1)]
        public static void BuildLoginAndMenuOnly()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var classes = AssetDatabase.FindAssets("t:TankClassData", new[] { Root + "/Classes" })
                .Select(g => AssetDatabase.LoadAssetAtPath<TankClassData>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(c => ClassOrder(c.name)).ToArray();
            if (classes.Length == 0)
            {
                EditorUtility.DisplayDialog("MiniTank", "Önce 'MiniTank > Her Şeyi Kur' çalıştırılmalı.", "Tamam");
                return;
            }

            var a = new SharedAssets
            {
                classes = classes,
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
            };

            var scenes = new List<string> { BuildLoginScene(a), BuildMenuScene(a) };
            foreach (var def in ArenaDefinitions())
            {
                string path = $"{ScenesFolder}/{def.sceneName}.unity";
                if (File.Exists(path)) scenes.Add(path);
            }
            EditorBuildSettings.scenes = scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            EditorSceneManager.OpenScene(scenes[0]);
            EditorUtility.DisplayDialog("MiniTank", "Giriş ve menü sahneleri kuruldu. Giriş sahnesi açıldı, Play'e basarak deneyebilirsin.", "Tamam");
        }

        static int ClassOrder(string id)
        {
            switch (id) { case "Hafif": return 0; case "Orta": return 1; case "Agir": return 2; case "Topcu": return 3; default: return 9; }
        }

        static string BuildLoginScene(SharedAssets a)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.07f, 0.09f, 0.11f);
            }

            var canvasGo = new GameObject("Giriş Arayüzü");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            // Arka plan: koyu degrade hissi için iki katman
            var bg = UIImage("Arka Plan", root, a.uiSprite, new Color(0.1f, 0.13f, 0.16f));
            bg.raycastTarget = false;
            Stretch(bg.rectTransform);
            var band = UIImage("Şerit", root, a.uiSprite, new Color(0.9f, 0.55f, 0.15f, 0.9f));
            band.raycastTarget = false;
            Place(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(520f, 6f));

            var title = UIText("Başlık", root, a.font, "MİNİ TANK 5v5", 96, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1400f, 140f));
            var subtitle = UIText("Alt Başlık", root, a.font, "Takımını topla, arenaya çık", 32, TextAnchor.MiddleCenter);
            subtitle.color = new Color(0.75f, 0.8f, 0.85f);
            Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(1200f, 50f));

            var login = canvasGo.AddComponent<LoginScreen>();
            login.menuSceneName = "MainMenu";

            // Yükleniyor
            var loading = Panel("Yükleniyor", root);
            var loadingText = UIText("Yazı", loading, a.font, "Yükleniyor...", 40, TextAnchor.MiddleCenter);
            Place(loadingText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 80f));
            login.loadingPanel = loading.gameObject;

            // Seçim paneli
            var choice = Panel("Seçim", root);
            login.guestButton = UIButton("Misafir", choice, a, "Misafir olarak oyna", new Vector2(0f, 120f), new Vector2(640f, 110f), new Color(0.9f, 0.55f, 0.15f));
            login.openLoginButton = UIButton("Giriş", choice, a, "E-posta ile giriş yap", new Vector2(0f, -10f), new Vector2(640f, 100f), new Color(0.22f, 0.45f, 0.8f));
            login.openRegisterButton = UIButton("Kayıt", choice, a, "Hesap oluştur", new Vector2(0f, -130f), new Vector2(640f, 100f), new Color(0.25f, 0.3f, 0.36f));
            var hint = UIText("İpucu", choice, a.font, "Misafir hesabını daha sonra ana menüden e-postana bağlayabilirsin.", 24, TextAnchor.MiddleCenter);
            hint.color = new Color(0.65f, 0.7f, 0.75f);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(1200f, 40f));
            login.choicePanel = choice.gameObject;

            // Giriş paneli
            var loginPanel = Panel("E-posta Girişi", root);
            var loginTitle = UIText("Başlık", loginPanel, a.font, "E-posta ile giriş", 44, TextAnchor.MiddleCenter);
            Place(loginTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(900f, 70f));
            login.loginEmail = UIInput("E-posta", loginPanel, a, "E-posta", InputField.ContentType.EmailAddress, new Vector2(0f, 100f));
            login.loginPassword = UIInput("Şifre", loginPanel, a, "Şifre", InputField.ContentType.Password, new Vector2(0f, -10f));
            login.loginButton = UIButton("Giriş Yap", loginPanel, a, "Giriş yap", new Vector2(0f, -130f), new Vector2(640f, 100f), new Color(0.22f, 0.45f, 0.8f));
            login.forgotButton = UIButton("Şifremi Unuttum", loginPanel, a, "Şifremi unuttum", new Vector2(170f, -245f), new Vector2(300f, 70f), new Color(0.2f, 0.24f, 0.28f), 26);
            login.loginBackButton = UIButton("Geri", loginPanel, a, "Geri", new Vector2(-170f, -245f), new Vector2(300f, 70f), new Color(0.2f, 0.24f, 0.28f), 26);
            login.loginPanel = loginPanel.gameObject;

            // Kayıt paneli
            var reg = Panel("Kayıt", root);
            login.registerTitle = UIText("Başlık", reg, a.font, "Hesap oluştur", 44, TextAnchor.MiddleCenter);
            Place(login.registerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 110f));
            login.registerName = UIInput("Oyuncu Adı", reg, a, "Oyuncu adı (3-16 karakter)", InputField.ContentType.Standard, new Vector2(0f, 150f));
            login.registerName.characterLimit = 16;
            login.registerEmail = UIInput("E-posta", reg, a, "E-posta", InputField.ContentType.EmailAddress, new Vector2(0f, 60f));
            login.registerPassword = UIInput("Şifre", reg, a, "Şifre (en az 6 karakter)", InputField.ContentType.Password, new Vector2(0f, -30f));
            login.registerPassword2 = UIInput("Şifre Tekrar", reg, a, "Şifre (tekrar)", InputField.ContentType.Password, new Vector2(0f, -120f));
            login.registerButton = UIButton("Kayıt Ol", reg, a, "Kayıt ol", new Vector2(170f, -235f), new Vector2(300f, 90f), new Color(0.9f, 0.55f, 0.15f));
            login.registerBackButton = UIButton("Geri", reg, a, "Geri", new Vector2(-170f, -235f), new Vector2(300f, 90f), new Color(0.2f, 0.24f, 0.28f));
            login.registerPanel = reg.gameObject;

            // Durum ve servis bilgisi
            login.statusText = UIText("Durum", root, a.font, "", 30, TextAnchor.MiddleCenter);
            Place(login.statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1600f, 50f));
            login.backendText = UIText("Servis", root, a.font, "", 20, TextAnchor.LowerRight);
            login.backendText.color = new Color(0.5f, 0.55f, 0.6f);
            var brt = login.backendText.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 0f);
            brt.anchoredPosition = new Vector2(-20f, 15f);
            brt.sizeDelta = new Vector2(900f, 30f);

            loginPanel.gameObject.SetActive(false);
            reg.gameObject.SetActive(false);
            choice.gameObject.SetActive(false);

            CreateEventSystem();

            string path = $"{ScenesFolder}/{LoginSceneName}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static RectTransform Panel(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1400f, 700f));
            return rt;
        }

        static Button UIButton(string name, RectTransform parent, SharedAssets a, string label, Vector2 pos, Vector2 size, Color color, int fontSize = 36)
        {
            var img = UIImage(name, parent, a.uiSprite, color);
            img.type = Image.Type.Sliced;
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            button.colors = colors;
            var text = UIText("Yazı", img.rectTransform, a.font, label, fontSize, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform);
            return button;
        }

        static InputField UIInput(string name, RectTransform parent, SharedAssets a, string placeholder, InputField.ContentType type, Vector2 pos)
        {
            var bg = UIImage(name, parent, a.uiSprite, new Color(0.95f, 0.96f, 0.98f));
            bg.type = Image.Type.Sliced;
            Place(bg.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(640f, 80f));

            var text = UIText("Metin", bg.rectTransform, a.font, "", 34, TextAnchor.MiddleLeft);
            text.color = new Color(0.1f, 0.12f, 0.15f);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Object.DestroyImmediate(text.GetComponent<Outline>());
            Inset(text.rectTransform);

            var ph = UIText("Yer Tutucu", bg.rectTransform, a.font, placeholder, 34, TextAnchor.MiddleLeft);
            ph.color = new Color(0.45f, 0.48f, 0.52f);
            ph.fontStyle = FontStyle.Italic;
            ph.horizontalOverflow = HorizontalWrapMode.Wrap;
            Object.DestroyImmediate(ph.GetComponent<Outline>());
            Inset(ph.rectTransform);

            var input = bg.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = ph;
            input.contentType = type;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        static void Inset(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(24f, 6f);
            rt.offsetMax = new Vector2(-24f, -6f);
        }
    }
}
