using TabgInstaller.HuntMode.Shared;
using UnityEngine;

namespace TabgInstaller.HuntMode.Client
{
    /// <summary>
    /// Full-screen perk selection overlay. Shown between role assignment and match start.
    /// </summary>
    public class PerkSelectionUI
    {
        private bool _stylesInit;
        private GUIStyle _overlayStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _roleBannerKiller;
        private GUIStyle _roleBannerSurvivor;
        private GUIStyle _subtitleStyle;
        private GUIStyle _cardNormal;
        private GUIStyle _cardSelected;
        private GUIStyle _cardNameStyle;
        private GUIStyle _cardDescStyle;
        private GUIStyle _confirmEnabled;
        private GUIStyle _confirmDisabled;

        private Texture2D _overlayTex;
        private Texture2D _panelTex;
        private Texture2D _cardNormalTex;
        private Texture2D _cardSelectedTex;
        private Texture2D _confirmGreenTex;
        private Texture2D _confirmGrayTex;

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

            _overlayTex = MakeTex(1, 1, new Color(0.12f, 0.12f, 0.15f, 0.95f));
            _panelTex = MakeTex(1, 1, new Color(0.16f, 0.16f, 0.20f, 1f));
            _cardNormalTex = MakeTex(1, 1, new Color(0.22f, 0.22f, 0.27f, 1f));
            _cardSelectedTex = MakeTex(1, 1, new Color(0.15f, 0.30f, 0.60f, 1f));
            _confirmGreenTex = MakeTex(1, 1, new Color(0.10f, 0.48f, 0.15f, 1f));
            _confirmGrayTex = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.28f, 1f));

            _overlayStyle = new GUIStyle(GUI.skin.box);
            _overlayStyle.normal.background = _overlayTex;

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = _panelTex;
            _panelStyle.padding = new RectOffset(20, 20, 20, 20);

            _roleBannerKiller = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _roleBannerKiller.normal.textColor = new Color(1f, 0.25f, 0.25f);

            _roleBannerSurvivor = new GUIStyle(_roleBannerKiller);
            _roleBannerSurvivor.normal.textColor = new Color(0.3f, 0.6f, 1f);

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f);

            _cardNormal = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            _cardNormal.normal.background = _cardNormalTex;
            _cardNormal.hover.background = MakeTex(1, 1, new Color(0.28f, 0.28f, 0.34f, 1f));
            _cardNormal.normal.textColor = Color.white;
            _cardNormal.hover.textColor = Color.white;
            _cardNormal.active.background = _cardNormalTex;
            _cardNormal.padding = new RectOffset(6, 6, 8, 6);

            _cardSelected = new GUIStyle(_cardNormal);
            _cardSelected.normal.background = _cardSelectedTex;
            _cardSelected.hover.background = _cardSelectedTex;
            _cardSelected.active.background = _cardSelectedTex;
            _cardSelected.normal.textColor = Color.white;

            _cardNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            _cardNameStyle.normal.textColor = Color.white;

            _cardDescStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter
            };
            _cardDescStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);

            _confirmEnabled = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _confirmEnabled.normal.background = _confirmGreenTex;
            _confirmEnabled.hover.background = MakeTex(1, 1, new Color(0.12f, 0.58f, 0.18f, 1f));
            _confirmEnabled.normal.textColor = Color.white;
            _confirmEnabled.hover.textColor = Color.white;
            _confirmEnabled.active.background = _confirmGreenTex;

            _confirmDisabled = new GUIStyle(_confirmEnabled);
            _confirmDisabled.normal.background = _confirmGrayTex;
            _confirmDisabled.hover.background = _confirmGrayTex;
            _confirmDisabled.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            _confirmDisabled.hover.textColor = new Color(0.5f, 0.5f, 0.5f);
        }

        public void Draw(HuntClientState state)
        {
            InitStyles();

            // Full-screen overlay
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _overlayStyle);

            // Centered panel
            float panelW = 700f;
            float panelH = 500f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, _panelStyle);

            float cy = panelY + 24f;

            // Role banner
            string bannerText = state.IsKiller ? "YOU ARE THE KILLER" : "SURVIVOR";
            var bannerStyle = state.IsKiller ? _roleBannerKiller : _roleBannerSurvivor;
            GUI.Label(new Rect(panelX, cy, panelW, 42f), bannerText, bannerStyle);
            cy += 46f;

            // Subtitle
            GUI.Label(new Rect(panelX, cy, panelW, 24f), "Select 2 Perks", _subtitleStyle);
            cy += 30f;

            // Perk grid
            if (state.IsKiller)
                DrawKillerPerks(state, panelX + 20f, cy, panelW - 40f);
            else
                DrawSurvivorPerks(state, panelX + 20f, cy, panelW - 40f);

            cy += state.IsKiller ? 90f : 200f;

            // Confirm button
            float confirmW = 200f;
            float confirmH = 45f;
            float confirmX = panelX + (panelW - confirmW) * 0.5f;
            float confirmY = panelY + panelH - confirmH - 24f;

            bool canConfirm = (state.SelectedPerk1 != 0 && state.SelectedPerk2 != 0);
            var confirmStyle = canConfirm ? _confirmEnabled : _confirmDisabled;

            if (canConfirm)
            {
                if (GUI.Button(new Rect(confirmX, confirmY, confirmW, confirmH), "CONFIRM", confirmStyle))
                {
                    state.MyPerks[0] = state.SelectedPerk1;
                    state.MyPerks[1] = state.SelectedPerk2;
                    state.PerkSelectionConfirmed = true;
                    // Note: The actual perk selection is sent to server via HuntEventCodes.PerkSelect
                    // when PerkSelectionConfirmed is set. The HuntClientPlugin or a separate sender
                    // can watch this flag and fire the event.
                }
            }
            else
            {
                GUI.Label(new Rect(confirmX, confirmY, confirmW, confirmH), "CONFIRM", confirmStyle);
            }
        }

        // 5 killer perks in a single row
        private void DrawKillerPerks(HuntClientState state, float startX, float startY, float totalW)
        {
            KillerPerk[] perks = { KillerPerk.Tracker, KillerPerk.Ironhide,
                                    KillerPerk.Bloodhound, KillerPerk.Reload, KillerPerk.Locksmith };

            float cardW = 130f;
            float cardH = 70f;
            float descH = 40f;
            float spacing = 8f;
            float totalCards = perks.Length;
            float rowW = totalCards * cardW + (totalCards - 1) * spacing;
            float rowX = startX + (totalW - rowW) * 0.5f;

            for (int i = 0; i < perks.Length; i++)
            {
                byte perkByte = (byte)perks[i];
                string name = PerkInfo.GetName(perks[i]);
                string desc = PerkInfo.GetDescription(perks[i]);

                float cx = rowX + i * (cardW + spacing);
                bool isSelected = (state.SelectedPerk1 == perkByte || state.SelectedPerk2 == perkByte);
                var style = isSelected ? _cardSelected : _cardNormal;

                if (GUI.Button(new Rect(cx, startY, cardW, cardH), name, style))
                    TogglePerk(state, perkByte);

                GUI.Label(new Rect(cx, startY + cardH + 4f, cardW, descH), desc, _cardDescStyle);
            }
        }

        // 6 survivor perks in 2 rows of 3
        private void DrawSurvivorPerks(HuntClientState state, float startX, float startY, float totalW)
        {
            SurvivorPerk[] perks = { SurvivorPerk.Medic, SurvivorPerk.Scout, SurvivorPerk.Tough,
                                      SurvivorPerk.Sprinter, SurvivorPerk.Saboteur, SurvivorPerk.Smokescreen };

            float cardW = 130f;
            float cardH = 70f;
            float descH = 40f;
            float hSpacing = 8f;
            float vSpacing = 56f; // space for desc + gap
            int cols = 3;

            float rowW = cols * cardW + (cols - 1) * hSpacing;
            float rowX = startX + (totalW - rowW) * 0.5f;

            for (int i = 0; i < perks.Length; i++)
            {
                byte perkByte = (byte)perks[i];
                string name = PerkInfo.GetName(perks[i]);
                string desc = PerkInfo.GetDescription(perks[i]);

                int col = i % cols;
                int row = i / cols;
                float cx = rowX + col * (cardW + hSpacing);
                float cy = startY + row * (cardH + descH + vSpacing - descH + 4f);

                bool isSelected = (state.SelectedPerk1 == perkByte || state.SelectedPerk2 == perkByte);
                var style = isSelected ? _cardSelected : _cardNormal;

                if (GUI.Button(new Rect(cx, cy, cardW, cardH), name, style))
                    TogglePerk(state, perkByte);

                GUI.Label(new Rect(cx, cy + cardH + 4f, cardW, descH), desc, _cardDescStyle);
            }
        }

        private static void TogglePerk(HuntClientState state, byte perkByte)
        {
            if (perkByte == 0) return;

            // If already selected in slot 1, deselect
            if (state.SelectedPerk1 == perkByte) { state.SelectedPerk1 = 0; return; }
            // If already selected in slot 2, deselect
            if (state.SelectedPerk2 == perkByte) { state.SelectedPerk2 = 0; return; }

            // Add to first empty slot
            if (state.SelectedPerk1 == 0) { state.SelectedPerk1 = perkByte; return; }
            if (state.SelectedPerk2 == 0) { state.SelectedPerk2 = perkByte; return; }

            // Both full: replace slot 2
            state.SelectedPerk2 = perkByte;
        }
    }
}
