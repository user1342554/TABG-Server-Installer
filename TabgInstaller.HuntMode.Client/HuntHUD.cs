using TabgInstaller.HuntMode.Shared;
using UnityEngine;

namespace TabgInstaller.HuntMode.Client
{
    /// <summary>
    /// OnGUI-based HUD for Hunt Mode. Renders survivor/killer overlays and end screen.
    /// </summary>
    public class HuntHUD
    {
        // Style cache
        private bool _stylesInit;
        private GUIStyle _boldStyle;
        private GUIStyle _bigStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _timerStyle;
        private GUIStyle _timerRedStyle;
        private GUIStyle _resultStyle;

        private Texture2D _darkBg;
        private Texture2D _redBg;
        private Texture2D _greenBg;
        private Texture2D _resultBg;

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var t = new Texture2D(w, h);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            t.SetPixels(pixels);
            t.Apply();
            return t;
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _darkBg = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.13f, 0.85f));
            _redBg = MakeTex(1, 1, new Color(0.5f, 0.05f, 0.05f, 0.85f));
            _greenBg = MakeTex(1, 1, new Color(0.05f, 0.4f, 0.05f, 0.85f));
            _resultBg = MakeTex(1, 1, new Color(0.08f, 0.08f, 0.10f, 0.92f));

            _boldStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            _boldStyle.normal.textColor = Color.white;

            _bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _bigStyle.normal.textColor = Color.white;

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };
            _smallStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _darkBg;
            _boxStyle.padding = new RectOffset(8, 8, 6, 6);

            _timerStyle = new GUIStyle(_boldStyle)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            _timerStyle.normal.textColor = Color.white;

            _timerRedStyle = new GUIStyle(_timerStyle);
            _timerRedStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);

            _resultStyle = new GUIStyle(_bigStyle)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter
            };
            _resultStyle.normal.textColor = Color.white;
        }

        public void Draw(HuntClientState state)
        {
            InitStyles();

            if (state.IsKiller)
                DrawKillerHUD(state);
            else
                DrawSurvivorHUD(state);
        }

        // ---- Shared elements ----

        private void DrawCrateProgress(HuntClientState state)
        {
            int total = state.CratesTotal > 0 ? state.CratesTotal : 5;
            int looted = Mathf.Clamp(state.CratesLooted, 0, total);

            // Build icon string
            string icons = "";
            for (int i = 0; i < total; i++)
                icons += (i < looted) ? "[#] " : "[ ] ";
            icons = icons.TrimEnd();

            string label = icons + "  " + looted + "/" + total;

            float w = 200f;
            float h = 36f;
            float x = (Screen.width - w) * 0.5f;
            float y = 10f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(x + 8, y + 4, w - 16, h - 8), label, _boldStyle);
        }

        private void DrawTimer(HuntClientState state)
        {
            float t = Mathf.Max(0, state.TimeRemaining);
            int minutes = (int)(t / 60f);
            int seconds = (int)(t % 60f);
            string timeStr = minutes + ":" + seconds.ToString("D2");

            float w = 90f;
            float h = 36f;
            float x = Screen.width - w - 12f;
            float y = 10f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            var style = (t < 60f) ? _timerRedStyle : _timerStyle;
            GUI.Label(new Rect(x, y, w, h), timeStr, style);
        }

        private void DrawPerks(HuntClientState state)
        {
            float w = 160f;
            float lineH = 22f;
            float h = lineH * 2 + 12f;
            float x = 12f;
            float y = Screen.height - h - 12f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            y += 6f;

            if (state.IsKiller)
            {
                string p1name = (state.MyPerks[0] > 0)
                    ? PerkInfo.GetName((KillerPerk)state.MyPerks[0]) : "-";
                string p2name = (state.MyPerks[1] > 0)
                    ? PerkInfo.GetName((KillerPerk)state.MyPerks[1]) : "-";
                GUI.Label(new Rect(x + 8, y, w - 16, lineH), p1name, _boldStyle);
                GUI.Label(new Rect(x + 8, y + lineH, w - 16, lineH), p2name, _boldStyle);
            }
            else
            {
                string p1name = (state.MyPerks[0] > 0)
                    ? PerkInfo.GetName((SurvivorPerk)state.MyPerks[0]) : "-";
                string p2name = (state.MyPerks[1] > 0)
                    ? PerkInfo.GetName((SurvivorPerk)state.MyPerks[1]) : "-";
                GUI.Label(new Rect(x + 8, y, w - 16, lineH), p1name, _boldStyle);
                GUI.Label(new Rect(x + 8, y + lineH, w - 16, lineH), p2name, _boldStyle);
            }
        }

        // ---- Survivor HUD ----

        private void DrawSurvivorHUD(HuntClientState state)
        {
            DrawCrateProgress(state);
            DrawTimer(state);
            DrawPerks(state);
            DrawSurvivorTeamPanel(state);
            DrawVehicleIndicator(state);
        }

        private void DrawSurvivorTeamPanel(HuntClientState state)
        {
            // Collect non-killer player indices that have names
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 256; i++)
            {
                if (i == state.KillerIndex) continue;
                if (string.IsNullOrEmpty(state.PlayerNames[i])) continue;
                indices.Add(i);
            }

            if (indices.Count == 0) return;

            float lineH = 24f;
            float w = 200f;
            float h = (indices.Count + 1) * lineH + 12f;
            float x = 12f;
            float y = 56f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            y += 6f;

            // Title
            var titleStyle = new GUIStyle(_boldStyle);
            titleStyle.normal.textColor = new Color(0.5f, 0.8f, 1f);
            GUI.Label(new Rect(x + 8, y, w - 16, lineH), "SURVIVORS", titleStyle);
            y += lineH;

            foreach (int idx in indices)
            {
                bool isDead = state.PlayerDead[idx];
                int downs = state.PlayerDownCounts[idx];
                int maxDowns = state.PlayerMaxDowns[idx] > 0 ? state.PlayerMaxDowns[idx] : 3;

                string downIcons = "";
                for (int d = 0; d < maxDowns; d++)
                    downIcons += (d < downs) ? "\u2620 " : "\u25cb ";  // skull / circle

                string name = state.PlayerNames[idx];
                if (name.Length > 14) name = name.Substring(0, 14);

                var nameStyle = new GUIStyle(_smallStyle);
                if (isDead) nameStyle.normal.textColor = new Color(0.4f, 0.4f, 0.4f);

                string line = name.PadRight(14) + "  " + downIcons;
                GUI.Label(new Rect(x + 8, y, w - 16, lineH), line, nameStyle);
                y += lineH;
            }
        }

        private void DrawVehicleIndicator(HuntClientState state)
        {
            if (!state.VehicleRevealed) return;

            float w = 180f;
            float h = 36f;
            float x = Screen.width - w - 12f;
            float y = Screen.height - h - 12f;

            if (state.VehicleUnlocked)
            {
                var origBg = _boxStyle.normal.background;
                _boxStyle.normal.background = _greenBg;
                GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
                _boxStyle.normal.background = origBg;

                var style = new GUIStyle(_boldStyle) { alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(0.6f, 1f, 0.6f);
                GUI.Label(new Rect(x, y, w, h), "VEHICLE: READY", style);
            }
            else
            {
                var origBg = _boxStyle.normal.background;
                _boxStyle.normal.background = _redBg;
                GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
                _boxStyle.normal.background = origBg;

                var style = new GUIStyle(_boldStyle) { alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(1f, 0.4f, 0.4f);
                GUI.Label(new Rect(x, y, w, h), "VEHICLE: LOCKED", style);
            }
        }

        // ---- Killer HUD ----

        private void DrawKillerHUD(HuntClientState state)
        {
            DrawCrateProgress(state);
            DrawTimer(state);
            DrawPerks(state);
            DrawKillerTargetPanel(state);
            DrawTrackerPing(state);
        }

        private void DrawKillerTargetPanel(HuntClientState state)
        {
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 256; i++)
            {
                if (i == state.KillerIndex) continue;
                if (string.IsNullOrEmpty(state.PlayerNames[i])) continue;
                indices.Add(i);
            }

            if (indices.Count == 0) return;

            float lineH = 24f;
            float w = 200f;
            float h = (indices.Count + 1) * lineH + 12f;
            float x = 12f;
            float y = 56f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            y += 6f;

            var titleStyle = new GUIStyle(_boldStyle);
            titleStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
            GUI.Label(new Rect(x + 8, y, w - 16, lineH), "TARGETS", titleStyle);
            y += lineH;

            foreach (int idx in indices)
            {
                bool isDead = state.PlayerDead[idx];
                int downs = state.PlayerDownCounts[idx];
                int maxDowns = state.PlayerMaxDowns[idx] > 0 ? state.PlayerMaxDowns[idx] : 3;

                string downIcons = "";
                for (int d = 0; d < maxDowns; d++)
                    downIcons += (d < downs) ? "\u2620 " : "\u25cb ";

                string name = state.PlayerNames[idx];
                if (name.Length > 14) name = name.Substring(0, 14);

                var nameStyle = new GUIStyle(_smallStyle);
                if (isDead) nameStyle.normal.textColor = new Color(0.35f, 0.35f, 0.35f);

                string line = name.PadRight(14) + "  " + downIcons;
                GUI.Label(new Rect(x + 8, y, w - 16, lineH), line, nameStyle);
                y += lineH;
            }
        }

        private void DrawTrackerPing(HuntClientState state)
        {
            // Only show if tracker perk equipped and ping is live
            bool hasTracker = (state.MyPerks[0] == (byte)KillerPerk.Tracker
                            || state.MyPerks[1] == (byte)KillerPerk.Tracker);
            if (!hasTracker) return;
            if (Time.time >= state.TrackerPingEndTime) return;

            float w = 160f;
            float h = 36f;
            float x = Screen.width - w - 12f;
            float y = Screen.height - 56f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            var style = new GUIStyle(_boldStyle) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 0.95f, 0.2f);
            GUI.Label(new Rect(x, y, w, h), "TRACKING...", style);
        }

        // ---- End Screen ----

        public void DrawResult(HuntClientState state)
        {
            InitStyles();
            if (string.IsNullOrEmpty(state.ResultText)) return;

            float w = 500f;
            float h = 120f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // Dark banner
            var origBg = _boxStyle.normal.background;
            _boxStyle.normal.background = _resultBg;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            _boxStyle.normal.background = origBg;

            GUI.Label(new Rect(x, y, w, h), state.ResultText, _resultStyle);
        }
    }
}
