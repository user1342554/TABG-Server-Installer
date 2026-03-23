using System.Collections.Generic;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    public class SpeakerIcon
    {
        private readonly Dictionary<int, float> _talkingTimers = new Dictionary<int, float>();
        private readonly Dictionary<int, string> _playerNames = new Dictionary<int, string>();
        private const float FadeOutDuration = 1.0f;
        private GUIStyle _hudStyle;
        private GUIStyle _hudBgStyle;
        private GUIStyle _worldStyle;
        private Texture2D _bgTexture;

        private readonly Dictionary<int, Transform> _playerTransformCache = new Dictionary<int, Transform>();

        public void SetTalking(int playerId, bool isTalking)
        {
            if (isTalking)
                _talkingTimers[playerId] = FadeOutDuration;
        }

        public void UpdatePlayerCache(Dictionary<int, Transform> cache)
        {
            _playerTransformCache.Clear();
            foreach (var kvp in cache)
                _playerTransformCache[kvp.Key] = kvp.Value;

            // Try to get player names
            try
            {
                var handler = Landfall.Network.PhotonServerHandler.instance;
                if (handler != null && handler.AllPlayers != null)
                {
                    foreach (var player in handler.AllPlayers)
                    {
                        if (player != null)
                            _playerNames[(int)player.PlayerIndex] = player.PlayerName ?? $"Player {player.PlayerIndex}";
                    }
                }
            }
            catch { }
        }

        public void OnGUI()
        {
            InitStyles();

            // === HUD OVERLAY (top-left corner) ===
            float hudX = 10f;
            float hudY = Screen.height / 2f - 60f; // Middle-left
            float hudWidth = 220f;
            float hudHeight = 28f;
            float padding = 4f;

            var toRemove = new List<int>();
            int displayCount = 0;

            foreach (var kvp in new Dictionary<int, float>(_talkingTimers))
            {
                int playerId = kvp.Key;
                float timer = kvp.Value;
                float alpha = Mathf.Clamp01(timer / FadeOutDuration);

                string name = _playerNames.ContainsKey(playerId) ? _playerNames[playerId] : $"Player {playerId}";

                // Background box
                var oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
                GUI.DrawTexture(new Rect(hudX, hudY + displayCount * (hudHeight + padding), hudWidth, hudHeight), _bgTexture);

                // Speaker icon + name
                GUI.color = new Color(0.3f, 1f, 0.3f, alpha); // Green like Discord
                GUI.Label(
                    new Rect(hudX + 8, hudY + displayCount * (hudHeight + padding), hudWidth - 16, hudHeight),
                    $"\u266B {name}",
                    _hudStyle
                );
                GUI.color = oldColor;

                displayCount++;

                // Count down timer
                timer -= Time.deltaTime;
                if (timer <= 0)
                    toRemove.Add(playerId);
                else
                    _talkingTimers[playerId] = timer;
            }

            foreach (int id in toRemove)
                _talkingTimers.Remove(id);

            // === 3D WORLD ICON (above player head) ===
            if (Camera.main == null) return;

            foreach (var kvp in new Dictionary<int, float>(_talkingTimers))
            {
                int playerId = kvp.Key;
                float timer = kvp.Value;

                if (!_playerTransformCache.TryGetValue(playerId, out var playerTransform) || playerTransform == null)
                    continue;

                Vector3 worldPos = playerTransform.position + Vector3.up * 2.5f;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                if (screenPos.z <= 0) continue;

                float guiY = Screen.height - screenPos.y;
                float alpha = Mathf.Clamp01(timer / FadeOutDuration);

                var oldColor = GUI.color;
                // Pulsing green glow
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 6f);
                GUI.color = new Color(0.3f, 1f, 0.3f, alpha * pulse);
                GUI.Label(new Rect(screenPos.x - 12, guiY - 30, 24, 24), "\u266B", _worldStyle);
                GUI.color = oldColor;
            }
        }

        private void InitStyles()
        {
            if (_hudStyle != null) return;

            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, Color.white);
            _bgTexture.Apply();

            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _worldStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }
    }
}
