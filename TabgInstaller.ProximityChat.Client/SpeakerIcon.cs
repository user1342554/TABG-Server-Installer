using System.Collections.Generic;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    public class SpeakerIcon
    {
        private readonly Dictionary<int, float> _talkingTimers = new Dictionary<int, float>();
        private const float FadeOutDuration = 0.3f;
        private const float IconSize = 24f;
        private const float IconOffsetY = 0.5f;
        private GUIStyle _iconStyle;

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
        }

        public void OnGUI()
        {
            if (Camera.main == null) return;
            if (_iconStyle == null)
            {
                _iconStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }

            var toRemove = new List<int>();

            foreach (var kvp in new Dictionary<int, float>(_talkingTimers))
            {
                int playerId = kvp.Key;
                float timer = kvp.Value;

                if (!_playerTransformCache.TryGetValue(playerId, out var playerTransform) || playerTransform == null)
                {
                    toRemove.Add(playerId);
                    continue;
                }

                Vector3 worldPos = playerTransform.position + Vector3.up * (2f + IconOffsetY);
                Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                if (screenPos.z <= 0) continue;

                float guiY = Screen.height - screenPos.y;
                float alpha = Mathf.Clamp01(timer / FadeOutDuration);

                var oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(screenPos.x - IconSize / 2, guiY - IconSize, IconSize, IconSize), "\u266A", _iconStyle);
                GUI.color = oldColor;

                timer -= Time.deltaTime;
                if (timer <= 0)
                    toRemove.Add(playerId);
                else
                    _talkingTimers[playerId] = timer;
            }

            foreach (int id in toRemove)
                _talkingTimers.Remove(id);
        }
    }
}
