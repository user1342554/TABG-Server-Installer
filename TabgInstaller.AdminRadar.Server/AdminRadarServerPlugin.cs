using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;
using UnityEngine;

namespace TabgInstaller.AdminRadar.Server
{
    [BepInPlugin("tabginstaller.adminradar.server", "Admin Radar Server", "1.0.0")]
    public class AdminRadarServerPlugin : BaseUnityPlugin
    {
        internal const byte RadarEventCode = 241;

        private static AdminRadarServerPlugin _instance;
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<float> _broadcastInterval;
        private static ConfigEntry<string> _recipients;
        private static ConfigEntry<bool> _includeDeadPlayers;

        private Harmony _harmony;

        private void Awake()
        {
            _instance = this;
            _enabled = Config.Bind("Radar", "Enabled", true, "Broadcast server-authorized radar positions.");
            _broadcastInterval = Config.Bind("Radar", "BroadcastIntervalSeconds", 0.5f, "How often to send radar updates.");
            _recipients = Config.Bind("Radar", "Recipients", "*", "Comma-separated player indexes that receive radar, or * for everyone.");
            _includeDeadPlayers = Config.Bind("Radar", "IncludeDeadPlayers", false, "Include dead players in radar updates.");

            _harmony = new Harmony("tabginstaller.adminradar.server");
            _harmony.PatchAll(typeof(RadarBroadcastPatch));

            Logger.LogInfo("[AdminRadar.Server] Loaded.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            RadarBroadcastPatch.TryBroadcast(null);
        }

        [HarmonyPatch(typeof(BattleRoyaleGameMode), "Run")]
        internal static class RadarBroadcastPatch
        {
            private static float _lastBroadcast;
            private static bool _resolved;
            private static PropertyInfo _playerIndexProp;
            private static PropertyInfo _playerNameProp;
            private static PropertyInfo _playerPositionProp;
            private static FieldInfo _isAliveField;
            private static MethodInfo _getPositionMethod;

            public static void Postfix(BattleRoyaleGameMode __instance)
            {
                TryBroadcast(__instance);
            }

            public static void TryBroadcast(BattleRoyaleGameMode gameMode)
            {
                if (_enabled == null || !_enabled.Value) return;
                if (Time.time - _lastBroadcast < Mathf.Max(0.1f, _broadcastInterval.Value)) return;

                _lastBroadcast = Time.time;

                try
                {
                    var server = FindServerClient(gameMode);
                    if (server == null || server.GameRoomReference == null || server.GameRoomReference.Players == null)
                        return;

                    var payload = BuildPayload(server);
                    if (payload == null || payload.Length == 0) return;

                    var recipientIndexes = ParseRecipients();
                    if (recipientIndexes == null)
                    {
                        server.SendMessageToClients((EventCode)RadarEventCode, payload, byte.MaxValue, false, false);
                    }
                    else if (recipientIndexes.Length > 0)
                    {
                        server.SendMessageToClients((EventCode)RadarEventCode, payload, recipientIndexes, false, false);
                    }
                }
                catch (Exception ex)
                {
                    _instance?.Logger.LogWarning("[AdminRadar.Server] Broadcast failed: " + ex.Message);
                }
            }

            private static ServerClient FindServerClient(BattleRoyaleGameMode gameMode)
            {
                var server = UnityEngine.Object.FindObjectOfType<ServerClient>();
                if (server != null) return server;

                var room = gameMode?.GetType().GetProperty("GameRoomReference", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(gameMode, null);
                return room?.GetType().GetProperty("ServerClient", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(room, null) as ServerClient;
            }

            private static byte[] BuildPayload(ServerClient server)
            {
                var entries = new List<PlayerRadarEntry>();
                foreach (var player in server.GameRoomReference.Players)
                {
                    if (player == null) continue;

                    bool alive = IsAlive(player);
                    if (!alive && !_includeDeadPlayers.Value) continue;

                    byte index = GetPlayerIndex(player);
                    if (index == byte.MaxValue) continue;

                    entries.Add(new PlayerRadarEntry
                    {
                        Index = index,
                        Name = GetPlayerName(player, index),
                        Position = GetPlayerPosition(player),
                        Alive = alive
                    });
                }

                if (entries.Count == 0) return null;

                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write((byte)Math.Min(entries.Count, 255));
                    for (int i = 0; i < entries.Count && i < 255; i++)
                    {
                        var entry = entries[i];
                        bw.Write(entry.Index);
                        bw.Write(entry.Name ?? string.Empty);
                        bw.Write(entry.Position.x);
                        bw.Write(entry.Position.y);
                        bw.Write(entry.Position.z);
                        bw.Write(entry.Alive);
                    }

                    return ms.ToArray();
                }
            }

            private static void EnsureResolved(object player)
            {
                if (_resolved || player == null) return;
                _resolved = true;

                var playerType = player.GetType();
                _playerIndexProp = AccessTools.Property(playerType, "PlayerIndex");
                _playerNameProp = AccessTools.Property(playerType, "PlayerName");
                _playerPositionProp = AccessTools.Property(playerType, "PlayerPosition");
                _isAliveField = AccessTools.Field(playerType, "IsAlive") ?? AccessTools.Field(playerType, "isAlive");
                _getPositionMethod = AccessTools.Method(playerType, "GetPosition");
            }

            private static byte GetPlayerIndex(object player)
            {
                EnsureResolved(player);
                try
                {
                    var value = _playerIndexProp?.GetValue(player, null);
                    if (value is byte b) return b;
                    if (value is int i && i >= 0 && i <= 255) return (byte)i;
                }
                catch { }

                return byte.MaxValue;
            }

            private static string GetPlayerName(object player, byte index)
            {
                EnsureResolved(player);
                try
                {
                    var value = _playerNameProp?.GetValue(player, null)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
                catch { }

                return "Player " + index;
            }

            private static Vector3 GetPlayerPosition(object player)
            {
                EnsureResolved(player);
                try
                {
                    if (_playerPositionProp?.GetValue(player, null) is Vector3 propertyPosition)
                        return propertyPosition;
                }
                catch { }

                try
                {
                    if (_getPositionMethod?.Invoke(player, null) is Vector3 methodPosition)
                        return methodPosition;
                }
                catch { }

                var component = player as Component;
                return component != null ? component.transform.position : Vector3.zero;
            }

            private static bool IsAlive(object player)
            {
                EnsureResolved(player);
                try
                {
                    var value = _isAliveField?.GetValue(player);
                    if (value is bool alive) return alive;
                }
                catch { }

                return true;
            }

            private static byte[] ParseRecipients()
            {
                var raw = _recipients.Value?.Trim();
                if (string.IsNullOrWhiteSpace(raw) || raw == "*") return null;

                var result = new List<byte>();
                foreach (var part in raw.Split(','))
                {
                    if (byte.TryParse(part.Trim(), out byte playerIndex))
                        result.Add(playerIndex);
                }

                return result.ToArray();
            }

            private struct PlayerRadarEntry
            {
                public byte Index;
                public string Name;
                public Vector3 Position;
                public bool Alive;
            }
        }
    }
}
