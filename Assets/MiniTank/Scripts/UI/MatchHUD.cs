using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MiniTank
{
    /// <summary>Maç arayüzü: süre, skor, can, dolum, yeniden doğma sayacı, öldürme akışı, tank can barları.</summary>
    public class MatchHUD : MonoBehaviour
    {
        public Text timerText;
        public Text blueScoreText;
        public Text redScoreText;
        public Text modeText;
        public Text healthText;
        public Text centerText;
        public Text killFeedText;
        public Text captureText;
        public Image healthFill;
        public Image reloadFill;

        [Header("Tankların üstündeki can barları")]
        public bool showWorldHealthBars = true;
        public Camera worldCamera;

        readonly List<string> feed = new List<string>();
        bool leagueSoundPlayed;
        bool doubledGold;
        bool adBusy;
        string announceText;
        Color announceColor;
        float announceUntil;
        GUIStyle announceStyle;

        void Announce(string text, Color color)
        {
            announceText = text;
            announceColor = color;
            announceUntil = Time.time + 1.6f;
        }
        readonly StringBuilder sb = new StringBuilder();
        MatchManager match;
        Texture2D white;

        void Start()
        {
            match = MatchManager.Instance;
            if (match != null) match.Kill += OnKill;
            if (worldCamera == null) worldCamera = Camera.main;
            white = Texture2D.whiteTexture;

            // Yeni arayüz parçaları (sahneyi yeniden kurmaya gerek kalmadan eklenir)
            if (GetComponent<CombatHUD>() == null) gameObject.AddComponent<CombatHUD>();
            if (GetComponent<MinimapHUD>() == null) gameObject.AddComponent<MinimapHUD>();
        }

        void OnDestroy()
        {
            if (match != null) match.Kill -= OnKill;
        }

        void OnKill(TankController victim, TankController killer)
        {
            string v = Colored(victim.DisplayName, victim.Team);
            string assist = victim.LastAssisters.Count > 0
                ? " <size=20>+ " + string.Join(", ", victim.LastAssisters.Select(a => a.DisplayName)) + "</size>"
                : "";
            string line = killer != null && killer != victim
                ? $"{Colored(killer.DisplayName, killer.Team)}{assist}  ►  {v}"
                : $"{v} imha oldu";

            // Oyuncuya anlık bildirim
            var player = match != null ? match.PlayerTank : null;
            if (player != null)
            {
                if (killer == player && victim != player)
                {
                    Announce("İMHA  +1", new Color(1f, 0.85f, 0.3f));
                    if (GameAudio.Instance != null) GameAudio.Instance.PlayKill();
                }
                else if (victim.LastAssisters.Contains(player))
                {
                    Announce("ASİST  +1", new Color(0.6f, 0.9f, 1f));
                    if (GameAudio.Instance != null) GameAudio.Instance.PlayAssist();
                }
            }
            feed.Add(line);
            if (feed.Count > 5) feed.RemoveAt(0);
            if (killFeedText != null) killFeedText.text = string.Join("\n", feed);
        }

        static string LeagueLine(LeagueChange c)
        {
            if (c == null) return "";
            string hex = ColorUtility.ToHtmlStringRGB(c.toTier.color);
            string sign = c.Delta >= 0 ? "+" : "";
            string deltaColor = c.Delta >= 0 ? "7CFF8A" : "FF7A6E";
            string line = $"\n<size=36><color=#{deltaColor}>{sign}{c.Delta} kupa</color>   <color=#{hex}>{c.toTier.name} Lig</color> ({c.after})</size>";
            if (c.goldEarned > 0) line += $"\n<size=32><color=#FFD24A>+{c.goldEarned} altın</color></size>";
            if (c.missionsCompleted > 0) line += $"<size=28>   <color=#9CF7A0>{c.missionsCompleted} görev tamamlandı!</color></size>";
            if (c.Promoted) line += $"\n<size=44><color=#{hex}>LİG ATLADIN → {c.toTier.name.ToUpper()}</color></size>";
            else if (c.Demoted) line += $"\n<size=36><color=#FF7A6E>Lig düştün → {c.toTier.name}</color></size>";
            return line;
        }

        static string Colored(string text, Team team)
        {
            string hex = ColorUtility.ToHtmlStringRGB(TeamColors.Get(team));
            return $"<color=#{hex}>{text}</color>";
        }

        void Update()
        {
            if (match == null) return;

            int t = Mathf.CeilToInt(match.TimeLeft);
            if (timerText != null) timerText.text = $"{t / 60}:{t % 60:00}";
            if (blueScoreText != null) blueScoreText.text = match.GetScore(Team.Blue).ToString();
            if (redScoreText != null) redScoreText.text = match.GetScore(Team.Red).ToString();
            if (modeText != null)
                modeText.text = match.Mode == MatchMode.Capture
                    ? $"ELE GEÇİRME  •  hedef {match.captureScoreLimit}"
                    : match.Mode == MatchMode.Elimination
                        ? "SON TANK  •  tek can, hayatta kalan sayısı"
                        : $"TAKIM ÖLÜM MAÇI  •  hedef {match.killLimit}";

            var player = match.PlayerTank;
            if (player != null)
            {
                float hp01 = player.IsDead ? 0f : player.Health / player.MaxHealth;
                if (healthFill != null) healthFill.fillAmount = hp01;
                if (reloadFill != null) reloadFill.fillAmount = player.IsDead ? 0f : player.ReloadProgress;
                if (healthText != null)
                    healthText.text = player.ClassData != null
                        ? $"{player.ClassData.displayName}   {Mathf.CeilToInt(player.Health)} / {Mathf.CeilToInt(player.MaxHealth)}   •   {player.Kills} öldürme / {player.Assists} asist / {player.Deaths} ölüm"
                        : "";
            }

            if (centerText != null)
            {
                if (match.IsOver)
                {
                    string result = match.IsDraw
                        ? "BERABERE"
                        : (match.Winner == match.playerTeam ? "KAZANDIN!" : "KAYBETTİN") +
                          $"\n<size=40>{TeamColors.Name(match.Winner)} takım kazandı</size>";
                    centerText.text = result + LeagueLine(match.PlayerLeagueChange);
                    if (match.PlayerLeagueChange != null && !leagueSoundPlayed)
                    {
                        leagueSoundPlayed = true;
                        if (GameAudio.Instance != null)
                        {
                            if (match.PlayerLeagueChange.Promoted) GameAudio.Instance.PlayPromote();
                            else if (match.PlayerLeagueChange.Delta > 0) GameAudio.Instance.PlayKill();
                        }
                    }
                }
                else if (player != null && player.IsDead)
                {
                    if (match.Mode == MatchMode.Elimination)
                    {
                        var spec = match.SpectateTarget;
                        centerText.text = spec != null
                            ? $"İMHA EDİLDİN\n<size=34>{spec.DisplayName} izleniyor</size>"
                            : "İMHA EDİLDİN";
                    }
                    else
                    {
                        int left = Mathf.Max(0, Mathf.CeilToInt(match.PlayerRespawnAt - Time.time));
                        centerText.text = $"İMHA EDİLDİN\n<size=40>Yeniden doğuş: {left}</size>";
                    }
                }
                else centerText.text = "";
            }

            if (captureText != null)
            {
                if (match.Mode == MatchMode.Capture && match.capturePoints != null)
                {
                    sb.Clear();
                    foreach (var cp in match.capturePoints)
                    {
                        if (cp == null) continue;
                        string label = cp.pointName;
                        if (cp.Contested) sb.Append($"<color=#FFD24A>[{label}]</color>  ");
                        else if (cp.HasOwner) sb.Append(Colored($"[{label}]", cp.Owner)).Append("  ");
                        else sb.Append($"[{label}]  ");
                    }
                    captureText.text = sb.ToString();
                }
                else captureText.text = "";
            }
        }

        void OnGUI()
        {
            GUI.skin = UITheme.Skin;
            DrawRewardedAd();

            if (!showWorldHealthBars || match == null || worldCamera == null) return;

            float scale = Screen.height / 1080f;
            float w = 90f * scale, h = 10f * scale;

            foreach (var tank in TankController.All)
            {
                if (tank == match.PlayerTank || tank.IsDead) continue;
                Vector3 world = tank.transform.position + Vector3.up * 3.2f;
                Vector3 sp = worldCamera.WorldToScreenPoint(world);
                if (sp.z <= 0f || sp.z > 150f) continue;

                float x = sp.x - w / 2f;
                float y = Screen.height - sp.y;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, h + 2), white);
                GUI.color = TeamColors.Get(tank.Team);
                GUI.DrawTexture(new Rect(x, y, w * (tank.Health / tank.MaxHealth), h), white);
            }
            GUI.color = Color.white;

            // Maç sonunda (veya editörde Tab basılıyken) skor tablosu
            bool showBoard = match.IsOver;
#if ENABLE_INPUT_SYSTEM && (UNITY_EDITOR || UNITY_STANDALONE)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tabKey.isPressed) showBoard = true;
#endif
            if (showBoard) DrawScoreboard(scale);

            // İMHA / ASİST bildirimi (nişangahın biraz üstünde)
            if (Time.time < announceUntil && !string.IsNullOrEmpty(announceText))
            {
                if (announceStyle == null)
                    announceStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                announceStyle.fontSize = Mathf.RoundToInt(44f * scale);
                float alpha = Mathf.Clamp01((announceUntil - Time.time) / 0.4f);
                var rect = new Rect(0f, Screen.height * 0.36f, Screen.width, 60f * scale);
                GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), announceText, announceStyle);
                GUI.color = new Color(announceColor.r, announceColor.g, announceColor.b, alpha);
                GUI.Label(rect, announceText, announceStyle);
                GUI.color = Color.white;
            }
        }
    
        GUIStyle boardStyle, boardHeader, adStyle;

        /// <summary>Maç sonunda: reklam izleyerek kazanılan altını ikiye katla.</summary>
        void DrawRewardedAd()
        {
            if (match == null || !match.IsOver) return;
            float scale = Screen.height / 1080f;
            if (adStyle == null) adStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, richText = true };
            adStyle.fontSize = Mathf.RoundToInt(26 * scale);

            if (Ads.ShowingTestAd)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.9f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, Screen.height / 2f - 40 * scale, Screen.width, 80 * scale), "TEST REKLAMI (sadece editörde)",
                          new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(40 * scale) });
                return;
            }

            var change = match.PlayerLeagueChange;
            if (change == null || change.goldEarned <= 0 || doubledGold || !Ads.Service.RewardedReady) return;
            var r = new Rect(Screen.width / 2f - 260 * scale, Screen.height * 0.5f - 60 * scale, 520 * scale, 70 * scale);
            GUI.enabled = !adBusy;
            if (GUI.Button(r, $"Reklam izle: altını 2 katına çıkar (+{change.goldEarned})", adStyle)) WatchAd(change.goldEarned);
            GUI.enabled = true;
        }

        async void WatchAd(int bonus)
        {
            adBusy = true;
            try
            {
                if (await Ads.Service.ShowRewardedAsync("double_gold"))
                {
                    Ads.MarkRewarded();
                    doubledGold = await ProgressService.AddGoldAsync(bonus);
                    if (doubledGold) Announce($"+{bonus} ALTIN", new Color(1f, 0.85f, 0.3f));
                }
            }
            finally { adBusy = false; }
        }

        void DrawScoreboard(float scale)
        {
            if (boardStyle == null)
            {
                boardStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, richText = true };
                boardHeader = new GUIStyle(boardStyle) { fontStyle = FontStyle.Bold };
            }
            boardStyle.fontSize = Mathf.RoundToInt(24f * scale);
            boardHeader.fontSize = Mathf.RoundToInt(24f * scale);

            float rowH = 34f * scale;
            float colW = 520f * scale;
            float width = colW * 2f + 40f * scale;
            float height = rowH * (match.teamSize + 2) + 20f * scale;
            float x0 = (Screen.width - width) / 2f;
            float y0 = Screen.height * 0.58f;

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x0, y0, width, height), white);
            GUI.color = Color.white;

            DrawTeamColumn(Team.Blue, x0 + 15f * scale, y0 + 10f * scale, colW, rowH);
            DrawTeamColumn(Team.Red, x0 + colW + 25f * scale, y0 + 10f * scale, colW, rowH);
        }

        void DrawTeamColumn(Team team, float x, float y, float w, float rowH)
        {
            string hex = ColorUtility.ToHtmlStringRGB(TeamColors.Get(team));
            float c1 = w * 0.52f, c2 = w * 0.16f;
            GUI.Label(new Rect(x, y, c1, rowH), $"<color=#{hex}>{TeamColors.Name(team).ToUpper()} TAKIM</color>", boardHeader);
            GUI.Label(new Rect(x + c1, y, c2, rowH), "İmha", boardHeader);
            GUI.Label(new Rect(x + c1 + c2, y, c2, rowH), "Asist", boardHeader);
            GUI.Label(new Rect(x + c1 + c2 * 2f, y, c2, rowH), "Ölüm", boardHeader);

            var rows = match.Tanks.Where(t => t != null && t.Team == team)
                                  .OrderByDescending(t => t.Kills).ThenByDescending(t => t.Assists).ThenBy(t => t.Deaths);
            int i = 1;
            foreach (var t in rows)
            {
                float ry = y + rowH * i++;
                string name = t == match.PlayerTank ? $"<b><color=#FFD24A>{t.DisplayName}</color></b>" : t.DisplayName;
                GUI.Label(new Rect(x, ry, c1, rowH), name, boardStyle);
                GUI.Label(new Rect(x + c1, ry, c2, rowH), t.Kills.ToString(), boardStyle);
                GUI.Label(new Rect(x + c1 + c2, ry, c2, rowH), t.Assists.ToString(), boardStyle);
                GUI.Label(new Rect(x + c1 + c2 * 2f, ry, c2, rowH), t.Deaths.ToString(), boardStyle);
            }
        }
    }
}
