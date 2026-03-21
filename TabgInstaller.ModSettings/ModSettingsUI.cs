using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace TabgInstaller.ModSettings
{
    /// <summary>
    /// In-game mod settings menu. Press # to open.
    /// Blocks player input while open. Clean dark UI.
    /// Auto-discovers config entries from all TabgInstaller plugins.
    /// </summary>
    [BepInPlugin("tabginstaller.modsettings", "TABG Mod Settings", "1.0.0")]
    [BepInDependency("tabginstaller.flyingcontrols", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("tabginstaller.coordsdisplay", BepInDependency.DependencyFlags.SoftDependency)]
    public class ModSettingsUI : BaseUnityPlugin
    {
        private ConfigEntry<KeyCode> _menuKey;
        private bool _isOpen = false;
        private bool _waitingForKey = false;
        private string _waitingForKeyId = null;
        private Vector2 _scrollPos;

        // All registered settings
        private static readonly List<SettingEntry> _settings = new List<SettingEntry>();

        private Rect _windowRect;
        private bool _dragging = false;

        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _sliderStyle;
        private GUIStyle _sliderThumbStyle;
        private Texture2D _bgTex;
        private Texture2D _headerTex;
        private Texture2D _btnTex;
        private Texture2D _btnHoverTex;
        private bool _stylesInit = false;

        private class SettingEntry
        {
            public string Category;
            public string Name;
            public string Description;
            public string Id;
            public ConfigEntryBase Entry;
        }

        private void Awake()
        {
            _menuKey = Config.Bind("Menu", "OpenKey", KeyCode.F9,
                "Key to open/close mod settings");
            _windowRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 250, 500, 500);
            Logger.LogInfo($"[ModSettings] Press {_menuKey.Value} for in-game settings");
        }

        /// <summary>Register a config entry to show in the settings menu.</summary>
        public static void Register(string category, string name, string description, ConfigEntryBase entry)
        {
            _settings.Add(new SettingEntry
            {
                Category = category,
                Name = name,
                Description = description,
                Id = category + "." + name,
                Entry = entry
            });
        }

        private void Update()
        {
            if (_waitingForKey)
            {
                // Listen for any key press
                foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
                {
                    if (kc == KeyCode.None || kc == KeyCode.Mouse0 || kc == KeyCode.Escape) continue;
                    if (Input.GetKeyDown(kc))
                    {
                        // Find the setting and update it
                        foreach (var s in _settings)
                        {
                            if (s.Id == _waitingForKeyId && s.Entry is ConfigEntry<KeyCode> keyEntry)
                            {
                                keyEntry.Value = kc;
                                break;
                            }
                        }
                        _waitingForKey = false;
                        _waitingForKeyId = null;
                        return;
                    }
                }
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _waitingForKey = false;
                    _waitingForKeyId = null;
                }
                return;
            }

            if (Input.GetKeyDown(_menuKey.Value))
            {
                _isOpen = !_isOpen;
                // Use Player.usingInterface — this is how the game itself
                // blocks input and shows cursor (inventory, map, blessings menu, etc.)
                Player.usingInterface = _isOpen;
            }
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _bgTex = MakeTex(1, 1, new Color(0.12f, 0.12f, 0.15f, 0.95f));
            _headerTex = MakeTex(1, 1, new Color(0.18f, 0.22f, 0.30f, 1f));
            _btnTex = MakeTex(1, 1, new Color(0.25f, 0.28f, 0.35f, 1f));
            _btnHoverTex = MakeTex(1, 1, new Color(0.35f, 0.40f, 0.50f, 1f));

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _bgTex;
            _windowStyle.onNormal.background = _bgTex;
            _windowStyle.padding = new RectOffset(10, 10, 10, 10);

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _headerStyle.normal.textColor = new Color(0.5f, 0.8f, 1f);
            _headerStyle.normal.background = _headerTex;
            _headerStyle.padding = new RectOffset(8, 8, 4, 4);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight };
            _valueStyle.normal.textColor = new Color(1f, 0.9f, 0.5f);

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            _buttonStyle.normal.background = _btnTex;
            _buttonStyle.hover.background = _btnHoverTex;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.active.background = _btnHoverTex;
            _buttonStyle.padding = new RectOffset(8, 8, 4, 4);
        }

        private Texture2D MakeTex(int w, int h, Color c)
        {
            var t = new Texture2D(w, h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    t.SetPixel(x, y, c);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (!_isOpen) return;
            InitStyles();

            _windowRect = GUI.Window(98765, _windowRect, DrawWindow, "", _windowStyle);
        }

        private void DrawWindow(int id)
        {
            // Title bar
            GUILayout.BeginHorizontal();
            GUILayout.Label("MOD SETTINGS", _headerStyle, GUILayout.Height(30));
            if (GUILayout.Button("X", _buttonStyle, GUILayout.Width(30), GUILayout.Height(30)))
            {
                _isOpen = false;
                Player.usingInterface = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            string currentCategory = "";
            foreach (var s in _settings)
            {
                if (s.Category != currentCategory)
                {
                    if (currentCategory != "") GUILayout.Space(8);
                    currentCategory = s.Category;
                    GUILayout.Label(currentCategory, _headerStyle, GUILayout.Height(26));
                    GUILayout.Space(4);
                }

                GUILayout.BeginHorizontal();

                if (s.Entry is ConfigEntry<KeyCode> keyEntry)
                {
                    GUILayout.Label(s.Name, _labelStyle, GUILayout.Width(200));
                    bool isWaiting = _waitingForKey && _waitingForKeyId == s.Id;
                    string btnText = isWaiting ? ">> Press a key <<" : keyEntry.Value.ToString();
                    if (GUILayout.Button(btnText, _buttonStyle, GUILayout.Width(160)))
                    {
                        _waitingForKey = true;
                        _waitingForKeyId = s.Id;
                    }
                }
                else if (s.Entry is ConfigEntry<float> floatEntry)
                {
                    GUILayout.Label(s.Name, _labelStyle, GUILayout.Width(160));
                    float min = 0f, max = 100f;
                    if (s.Name.Contains("Speed")) max = 120f;
                    else if (s.Name.Contains("Force") || s.Name.Contains("Stabilization")) max = 60f;
                    else if (s.Name.Contains("Font")) { min = 8; max = 40; }
                    float newVal = GUILayout.HorizontalSlider(floatEntry.Value, min, max, GUILayout.Width(140));
                    if (Mathf.Abs(newVal - floatEntry.Value) > 0.1f)
                        floatEntry.Value = Mathf.Round(newVal * 10f) / 10f;
                    GUILayout.Label(floatEntry.Value.ToString("F1"), _valueStyle, GUILayout.Width(50));
                }
                else if (s.Entry is ConfigEntry<int> intEntry)
                {
                    GUILayout.Label(s.Name, _labelStyle, GUILayout.Width(160));
                    float newVal = GUILayout.HorizontalSlider(intEntry.Value, 8, 40, GUILayout.Width(140));
                    intEntry.Value = Mathf.RoundToInt(newVal);
                    GUILayout.Label(intEntry.Value.ToString(), _valueStyle, GUILayout.Width(50));
                }
                else if (s.Entry is ConfigEntry<bool> boolEntry)
                {
                    GUILayout.Label(s.Name, _labelStyle, GUILayout.Width(200));
                    bool newVal = GUILayout.Toggle(boolEntry.Value, boolEntry.Value ? "ON" : "OFF", _buttonStyle, GUILayout.Width(60));
                    boolEntry.Value = newVal;
                }

                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(s.Description))
                {
                    var descStyle = new GUIStyle(_labelStyle) { fontSize = 10 };
                    descStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                    GUILayout.Label("  " + s.Description, descStyle);
                }

                GUILayout.Space(2);
            }

            GUILayout.Space(10);
            var footerStyle = new GUIStyle(_labelStyle) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            footerStyle.normal.textColor = new Color(0.4f, 0.4f, 0.4f);
            GUILayout.Label($"Press {_menuKey.Value} to close  |  Settings save automatically", footerStyle);

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
    }
}
