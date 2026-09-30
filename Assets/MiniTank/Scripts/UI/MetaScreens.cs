using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Ana menüdeki Garaj (geliştirme + kamuflaj), Görevler ve Sıralama ekranları.
    /// 1920x1080 sanal ekran koordinatlarında IMGUI ile çizilir.
    /// </summary>
    public class MetaScreens
    {
        public enum Screen { None, Garage, Missions, Leaderboard, Store }
        public Screen Current = Screen.None;

        TankClassData[] classes;
        int classIndex;
        bool busy;
        string toast;
        float toastUntil;

        List<LeaderboardEntry> board;
        bool boardLoading;
        string boardError;

        GUIStyle title, text, small, button, selected, bold;

        public MetaScreens(TankClassData[] classes) { this.classes = classes; }

        public void Open(Screen s)
        {
            Current = s;
            toast = null;
            if (s == Screen.Missions) _ = ProgressService.RefreshMissionsAsync();
            if (s == Screen.Leaderboard) LoadBoard();
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            text = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true };
            small = new GUIStyle(text) { fontSize = 22 };
            bold = new GUIStyle(text) { fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 26, richText = true };
            selected = new GUIStyle(button) { fontStyle = FontStyle.Bold };
            selected.normal.textColor = selected.hover.textColor = new Color(1f, 0.85f, 0.2f);
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        static void Click()
        {
            if (GameAudio.Instance != null) GameAudio.Instance.PlayClick();
        }

        void Toast(string message) { toast = message; toastUntil = Time.time + 2.5f; }

        async void Run(Func<Task<bool>> action, string ok, string fail)
        {
            if (busy) return;
            busy = true;
            try
            {
                bool success = await action();
                Toast(success ? ok : fail);
                if (success && GameAudio.Instance != null) GameAudio.Instance.PlayKill();
            }
            catch (Exception e) { Toast("Hata: " + e.Message); }
            finally { busy = false; }
        }

        /// <summary>Açık ekranı çizer. Ekran açıksa true döner (menünün geri kalanı çizilmez).</summary>
        public bool Draw()
        {
            if (Current == Screen.None) return false;
            Styles();
            Fill(new Rect(0, 0, 1920, 1080), new Color(0f, 0f, 0f, 0.8f));
            var r = new Rect(160, 60, 1600, 960);
            Fill(r, new Color(0.1f, 0.12f, 0.15f, 0.98f));

            var p = ProgressService.Current;
            if (p != null)
                GUI.Label(new Rect(r.xMax - 420, r.y + 20, 390, 60), $"<color=#FFD24A><b>{p.gold}</b> altın</color>",
                          new GUIStyle(text) { alignment = TextAnchor.MiddleRight, fontSize = 32 });

            if (p == null)
                GUI.Label(new Rect(r.x, r.center.y - 30, r.width, 60), "Hesap bilgisi yükleniyor...", new GUIStyle(text) { alignment = TextAnchor.MiddleCenter });
            else if (Current == Screen.Garage) DrawGarage(r, p);
            else if (Current == Screen.Missions) DrawMissions(r, p);
            else if (Current == Screen.Store) DrawStore(r);
            else DrawLeaderboard(r);

            if (!string.IsNullOrEmpty(toast) && Time.time < toastUntil)
                GUI.Label(new Rect(r.x, r.yMax - 150, r.width, 50), $"<color=#FFE08A>{toast}</color>", new GUIStyle(text) { alignment = TextAnchor.MiddleCenter });

            if (GUI.Button(new Rect(r.center.x - 130, r.yMax - 90, 260, 65), "Kapat", selected)) { Current = Screen.None; Click(); }
            return true;
        }

        // ================================================================== Garaj

        void DrawGarage(Rect r, PlayerProgress p)
        {
            GUI.Label(new Rect(r.x, r.y + 20, r.width, 60), "Garaj", title);
            if (classes == null || classes.Length == 0) return;
            classIndex = Mathf.Clamp(classIndex, 0, classes.Length - 1);

            // Sınıf sekmeleri
            float tx = r.x + 60, ty = r.y + 100;
            for (int i = 0; i < classes.Length; i++)
                if (classes[i] != null && GUI.Button(new Rect(tx + i * 230, ty, 215, 70), classes[i].displayName, i == classIndex ? selected : button))
                { classIndex = i; Click(); }

            var cls = classes[classIndex];
            var u = ProgressService.GetUpgrade(cls.name);

            // Güncel değerler
            float hp = cls.maxHealth * Economy.HealthMultiplier(u.armor);
            float spd = cls.moveSpeed * Economy.SpeedMultiplier(u.engine);
            float dmg = cls.damage * Economy.DamageMultiplier(u.gun);
            float cd = cls.fireCooldown * Economy.ReloadMultiplier(u.gun);
            GUI.Label(new Rect(tx, ty + 90, 900, 40),
                $"Can <b>{hp:0}</b>   •   Hız <b>{spd:0.0}</b>   •   Hasar <b>{dmg:0}</b>   •   Dolum <b>{cd:0.00} sn</b>   •   Yetenek: <b>{cls.abilityName}</b>", small);

            // Geliştirmeler
            float y = ty + 150;
            foreach (UpgradeStat stat in new[] { UpgradeStat.Engine, UpgradeStat.Armor, UpgradeStat.Gun })
            {
                int level = ProgressService.GetLevel(u, stat);
                int cost = Economy.UpgradeCost(level);
                Fill(new Rect(tx, y, 900, 90), new Color(1f, 1f, 1f, 0.05f));
                GUI.Label(new Rect(tx + 20, y + 5, 300, 45), $"<b>{Economy.StatName(stat)}</b>", text);
                GUI.Label(new Rect(tx + 20, y + 45, 420, 40), Economy.StatEffect(stat), small);
                for (int i = 0; i < Economy.MaxUpgradeLevel; i++)
                    Fill(new Rect(tx + 450 + i * 42, y + 30, 34, 30), i < level ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 1f, 1f, 0.15f));
                if (cost < 0)
                    GUI.Label(new Rect(tx + 690, y + 20, 200, 50), "<color=#9CF7A0>Maksimum</color>", bold);
                else
                {
                    bool canBuy = p.gold >= cost && !busy;
                    GUI.enabled = canBuy;
                    var st = stat;
                    if (GUI.Button(new Rect(tx + 680, y + 15, 210, 60), $"Geliştir  {cost}", button))
                    {
                        Click();
                        Run(() => ProgressService.TryBuyUpgradeAsync(cls.name, st), $"{Economy.StatName(st)} geliştirildi!", "Yeterli altın yok.");
                    }
                    GUI.enabled = true;
                }
                y += 105;
            }

            // Kamuflaj
            float cx = r.x + 1010, cy = r.y + 100;
            GUI.Label(new Rect(cx, cy, 540, 50), "<b>Kamuflaj</b>  <size=20>(tüm tanklarında geçerli)</size>", text);
            cy += 60;
            for (int i = 0; i < Economy.Camos.Length; i++)
            {
                var camo = Economy.Camos[i];
                float bx = cx + (i % 2) * 275, by = cy + (i / 2) * 150;
                var card = new Rect(bx, by, 260, 138);
                bool owned = ProgressService.OwnsCamo(camo.id);
                bool equipped = p.equippedCamo == camo.id || (camo.id == Economy.DefaultCamo && string.IsNullOrEmpty(p.equippedCamo));
                Fill(card, equipped ? new Color(1f, 0.8f, 0.25f, 0.25f) : new Color(1f, 1f, 1f, 0.06f));

                var swatch = new Rect(bx + 10, by + 10, 70, 70);
                var tex = CamoTextures.Get(camo);
                if (tex != null) GUI.DrawTexture(swatch, tex);
                else Fill(swatch, camo.id == Economy.DefaultCamo ? new Color(0.35f, 0.4f, 0.32f) : camo.a);

                GUI.Label(new Rect(bx + 90, by + 8, 165, 40), $"<b>{camo.name}</b>", small);
                string status = equipped ? "<color=#FFD24A>Kullanılıyor</color>" : owned ? "Sahipsin" : $"<color=#FFD24A>{camo.price}</color> altın";
                GUI.Label(new Rect(bx + 90, by + 45, 165, 36), status, small);

                if (!equipped)
                {
                    var c = camo;
                    if (owned)
                    {
                        if (GUI.Button(new Rect(bx + 10, by + 88, 240, 42), "Kullan", button)) { Click(); _ = ProgressService.EquipCamoAsync(c.id); }
                    }
                    else
                    {
                        GUI.enabled = p.gold >= camo.price && !busy;
                        if (GUI.Button(new Rect(bx + 10, by + 88, 240, 42), "Satın al", button))
                        {
                            Click();
                            Run(() => ProgressService.TryBuyCamoAsync(c.id), $"{c.name} kamuflajı alındı!", "Yeterli altın yok.");
                        }
                        GUI.enabled = true;
                    }
                }
            }

            GUI.Label(new Rect(tx, r.yMax - 210, 900, 60),
                "Altın kazanmak için maç oyna ve görevleri tamamla. Botlar dengeli olsun diye senin ortalama geliştirme seviyene yakın seviyede olur.", small);
        }

        // ================================================================== Görevler

        void DrawMissions(Rect r, PlayerProgress p)
        {
            GUI.Label(new Rect(r.x, r.y + 20, r.width, 60), "Görevler", title);
            float y = r.y + 110;
            y = DrawMissionList(r, "Günlük görevler", $"yenilenmesine {Format(Economy.UntilNextDay())}", p.daily, false, y);
            y += 30;
            DrawMissionList(r, "Haftalık görevler", $"yenilenmesine {Format(Economy.UntilNextWeek())}", p.weekly, true, y);
        }

        float DrawMissionList(Rect r, string header, string timer, List<MissionState> list, bool weekly, float y)
        {
            float x = r.x + 100, w = r.width - 200;
            GUI.Label(new Rect(x, y, w, 50), $"<b>{header}</b>   <size=22>{timer}</size>", text);
            y += 55;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                Fill(new Rect(x, y, w, 80), new Color(1f, 1f, 1f, 0.05f));
                GUI.Label(new Rect(x + 20, y + 5, 600, 40), Economy.MissionText(m), text);
                var bar = new Rect(x + 20, y + 50, 600, 16);
                Fill(bar, new Color(1f, 1f, 1f, 0.12f));
                Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01((float)m.progress / Mathf.Max(1, m.target)), bar.height),
                     m.Complete ? new Color(0.4f, 0.9f, 0.45f) : new Color(1f, 0.8f, 0.25f));
                GUI.Label(new Rect(x + 640, y + 20, 200, 40), $"{m.progress} / {m.target}", small);
                GUI.Label(new Rect(x + 850, y + 20, 220, 40), $"<color=#FFD24A>+{m.reward} altın</color>", small);

                var btn = new Rect(x + w - 230, y + 12, 210, 56);
                if (m.claimed) GUI.Label(btn, "<color=#9CF7A0>Alındı</color>", new GUIStyle(bold) { alignment = TextAnchor.MiddleCenter });
                else
                {
                    GUI.enabled = m.Complete && !busy;
                    int index = i;
                    if (GUI.Button(btn, m.Complete ? "Ödülü al" : "Devam ediyor", m.Complete ? selected : button))
                    {
                        Click();
                        Run(() => ProgressService.ClaimMissionAsync(weekly, index), $"+{m.reward} altın!", "Alınamadı.");
                    }
                    GUI.enabled = true;
                }
                y += 92;
            }
            return y;
        }

        static string Format(TimeSpan t) =>
            t.TotalDays >= 1 ? $"{(int)t.TotalDays} gün {t.Hours} saat" : t.TotalHours >= 1 ? $"{(int)t.TotalHours} saat {t.Minutes} dk" : $"{Mathf.Max(1, t.Minutes)} dk";

        // ================================================================== Mağaza

        void DrawStore(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y + 20, r.width, 60), "Mağaza", title);
            float w = 420, h = 420, gap = 60;
            float x0 = r.center.x - (w * 3 + gap * 2) / 2f, y = r.y + 160;
            for (int i = 0; i < Store.Packs.Length; i++)
            {
                var pack = Store.Packs[i];
                var card = new Rect(x0 + i * (w + gap), y, w, h);
                Fill(card, pack.bestValue ? new Color(1f, 0.8f, 0.25f, 0.18f) : new Color(1f, 1f, 1f, 0.06f));
                if (pack.bestValue)
                    GUI.Label(new Rect(card.x, card.y + 10, w, 40), "<color=#FFD24A><b>EN AVANTAJLI</b></color>", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(card.x, card.y + 70, w, 50), $"<b>{pack.name}</b>", new GUIStyle(text) { alignment = TextAnchor.MiddleCenter });
                // Altın yığını çizimi
                int coins = 3 + i * 3;
                for (int k = 0; k < coins; k++)
                    Fill(new Rect(card.center.x - 60 + (k % 4) * 30, card.y + 210 - (k / 4) * 22, 26, 18), new Color(1f, 0.78f, 0.2f));
                GUI.Label(new Rect(card.x, card.y + 250, w, 50), $"<color=#FFD24A><b>{pack.gold}</b> altın</color>", new GUIStyle(text) { alignment = TextAnchor.MiddleCenter, fontSize = 34 });
                GUI.enabled = !busy;
                var pk = pack;
                GUI.enabled = !busy && Store.Ready;
                if (GUI.Button(new Rect(card.x + 40, card.yMax - 100, w - 80, 70), Store.PriceOf(pack), selected))
                {
                    Click();
                    Run(() => Store.BuyAsync(pk), $"+{pk.gold} altın!", "Satın alma tamamlanmadı.");
                }
                GUI.enabled = true;
            }
            GUI.Label(new Rect(r.x + 100, y + h + 40, r.width - 200, 90),
                (Store.IsTestMode
                    ? "<size=22>Test modu: editörde satın alma ücretsizdir, gerçek ödeme alınmaz. "
                    : Store.Ready ? "<size=22>Ödemeler Google Play üzerinden güvenle yapılır. "
                                  : "<size=22>Mağazaya bağlanılıyor… (Google Play hesabı ve internet gerekli) ") +
                "Oyunda güç satılmaz; altın sadece geliştirmeyi hızlandırır ve kamuflaj alır.</size>", new GUIStyle(small) { alignment = TextAnchor.UpperCenter });
        }

        // ================================================================== Sıralama

        async void LoadBoard()
        {
            board = null;
            boardError = null;
            if (!Leaderboard.Available || boardLoading) return;
            boardLoading = true;
            try { board = await Leaderboard.TopAsync(50); }
            catch (Exception e) { boardError = e.Message; }
            finally { boardLoading = false; }
        }

        void DrawLeaderboard(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y + 20, r.width, 60), "Sıralama", title);
            float x = r.x + 160, w = r.width - 320, y = r.y + 110;

            if (!Leaderboard.Available)
            {
                GUI.Label(new Rect(x, y + 100, w, 200),
                    "Sıralama tablosu için Firebase <b>Firestore</b> kurulmalı.\n\nKurulunca bütün oyuncuların kupaları burada listelenir. Adımlar KURULUM.md dosyasının 'Ligler ve sezonlar' bölümünde.",
                    new GUIStyle(text) { alignment = TextAnchor.UpperCenter });
                return;
            }
            if (boardLoading) { GUI.Label(new Rect(x, y + 100, w, 60), "Yükleniyor...", new GUIStyle(text) { alignment = TextAnchor.MiddleCenter }); return; }
            if (boardError != null)
            {
                GUI.Label(new Rect(x, y + 100, w, 120), $"Sıralama yüklenemedi:\n<size=20>{boardError}</size>", new GUIStyle(text) { alignment = TextAnchor.MiddleCenter });
                if (GUI.Button(new Rect(r.center.x - 120, y + 240, 240, 60), "Tekrar dene", button)) LoadBoard();
                return;
            }
            if (board == null || board.Count == 0) { GUI.Label(new Rect(x, y + 100, w, 60), "Henüz kimse yok.", new GUIStyle(text) { alignment = TextAnchor.MiddleCenter }); return; }

            GUI.Label(new Rect(x, y, 120, 40), "<b>#</b>", small);
            GUI.Label(new Rect(x + 100, y, 600, 40), "<b>Oyuncu</b>", small);
            GUI.Label(new Rect(x + 800, y, 250, 40), "<b>Lig</b>", small);
            GUI.Label(new Rect(x + 1050, y, 200, 40), "<b>Kupa</b>", small);
            y += 45;

            string me = AuthManager.Service.UserId;
            int rows = Mathf.Min(board.Count, 14);
            int myRank = board.FindIndex(e => e.userId == me);
            for (int i = 0; i < rows; i++) DrawBoardRow(board[i], i, x, ref y, w, board[i].userId == me);
            if (myRank >= rows)
            {
                y += 10;
                DrawBoardRow(board[myRank], myRank, x, ref y, w, true);
            }
            else if (myRank < 0 && ProgressService.Current != null)
            {
                GUI.Label(new Rect(x, y + 10, w, 40), $"Sen ilk 50'de değilsin: {ProgressService.Current.points} kupa", small);
            }
        }

        void DrawBoardRow(LeaderboardEntry e, int index, float x, ref float y, float w, bool me)
        {
            if (me) Fill(new Rect(x - 20, y - 2, w + 40, 44), new Color(1f, 0.8f, 0.25f, 0.2f));
            else if (index % 2 == 0) Fill(new Rect(x - 20, y - 2, w + 40, 44), new Color(1f, 1f, 1f, 0.04f));
            var tier = Leagues.Get(e.points);
            string medal = index == 0 ? "<color=#FFD24A>1</color>" : index == 1 ? "<color=#C8CDD6>2</color>" : index == 2 ? "<color=#CD8540>3</color>" : (index + 1).ToString();
            GUI.Label(new Rect(x, y, 100, 40), $"<b>{medal}</b>", small);
            GUI.Label(new Rect(x + 100, y, 680, 40), me ? $"<b>{e.name} (sen)</b>" : e.name, small);
            GUI.Label(new Rect(x + 800, y, 250, 40), $"<color=#{Hex(tier.color)}>{tier.name}</color>", small);
            GUI.Label(new Rect(x + 1050, y, 200, 40), e.points.ToString(), small);
            y += 46;
        }
    }
}
