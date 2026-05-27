using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
        private const uint RadarPayloadMagic = 0x52445241; // "ARDR", little-endian
        private const byte RadarPayloadVersion = 1;
        private const byte PlayerSectionType = 1;
        private const byte BotDebugSectionType = 2;
        private const int MaxSerializedStringBytes = 512;

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
            private static BotDebugAccessor _botDebugAccessor;
            private static string _lastRecipientWarning;

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
                var debugEntries = BuildBotDebugEntries(server);

                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(RadarPayloadMagic);
                    bw.Write(RadarPayloadVersion);
                    bw.Write((byte)(debugEntries.Count > 0 ? 2 : 1));
                    WriteSection(bw, PlayerSectionType, sectionWriter => WritePlayerSection(sectionWriter, entries));
                    if (debugEntries.Count > 0)
                        WriteSection(bw, BotDebugSectionType, sectionWriter => WriteBotDebugSection(sectionWriter, debugEntries));

                    return ms.ToArray();
                }
            }

            private static void WriteSection(BinaryWriter payloadWriter, byte sectionType, Action<BinaryWriter> writeSection)
            {
                using (var sectionStream = new MemoryStream())
                using (var sectionWriter = new BinaryWriter(sectionStream))
                {
                    writeSection(sectionWriter);
                    sectionWriter.Flush();

                    long sectionLength = sectionStream.Length;
                    if (sectionLength > ushort.MaxValue)
                        throw new InvalidOperationException("Radar section exceeded " + ushort.MaxValue + " bytes.");

                    payloadWriter.Write(sectionType);
                    payloadWriter.Write((ushort)sectionLength);
                    payloadWriter.Write(sectionStream.ToArray());
                }
            }

            private static void WritePlayerSection(BinaryWriter bw, List<PlayerRadarEntry> entries)
            {
                bw.Write((byte)Math.Min(entries.Count, 255));
                for (int i = 0; i < entries.Count && i < 255; i++)
                {
                    var entry = entries[i];
                    bw.Write(entry.Index);
                    WriteString(bw, entry.Name);
                    bw.Write(entry.Position.x);
                    bw.Write(entry.Position.y);
                    bw.Write(entry.Position.z);
                    bw.Write(entry.Alive);
                }
            }

            private static void WriteBotDebugSection(BinaryWriter bw, List<BotDebugEntry> debugEntries)
            {
                bw.Write((byte)Math.Min(debugEntries.Count, 255));
                for (int i = 0; i < debugEntries.Count && i < 255; i++)
                {
                    var entry = debugEntries[i];
                    bw.Write(entry.Index);
                    WriteString(bw, entry.State);
                    WriteString(bw, entry.TargetName);
                    WriteString(bw, entry.WeaponName);
                    bw.Write(entry.HasLineOfSight);
                    bw.Write(entry.IsFiring);
                    bw.Write(entry.HasMoveGoal);
                    bw.Write(entry.MoveGoal.x);
                    bw.Write(entry.MoveGoal.y);
                    bw.Write(entry.MoveGoal.z);
                    bw.Write(entry.HasLootGoal);
                    bw.Write(entry.LootGoal.x);
                    bw.Write(entry.LootGoal.y);
                    bw.Write(entry.LootGoal.z);
                    WriteString(bw, entry.LootName);
                }
            }

            private static void WriteString(BinaryWriter bw, string value)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                int length = Math.Min(bytes.Length, MaxSerializedStringBytes);
                bw.Write((ushort)length);
                bw.Write(bytes, 0, length);
            }

            private static List<BotDebugEntry> BuildBotDebugEntries(ServerClient server)
            {
                var entries = new List<BotDebugEntry>();
                if (server == null || server.GameRoomReference == null || server.GameRoomReference.Players == null)
                    return entries;

                for (int i = 0; i < server.GameRoomReference.Players.Count; i++)
                {
                    TABGPlayerServer player = server.GameRoomReference.Players[i];
                    if (player == null || player.PlayerObject == null)
                        continue;

                    MonoBehaviour controller = FindAiController(player.PlayerObject);
                    if (controller == null)
                        continue;

                    var accessor = GetBotDebugAccessor(controller);
                    if (accessor == null)
                        continue;

                    entries.Add(new BotDebugEntry
                    {
                        Index = player.PlayerIndex,
                        State = accessor.ReadString(controller, accessor.DebugState),
                        TargetName = accessor.ReadString(controller, accessor.DebugTargetName),
                        WeaponName = accessor.ReadString(controller, accessor.DebugWeaponName),
                        HasLineOfSight = accessor.ReadBool(controller, accessor.DebugHasLineOfSight),
                        IsFiring = accessor.ReadBool(controller, accessor.DebugIsFiring),
                        HasMoveGoal = accessor.ReadBool(controller, accessor.DebugHasMoveGoal),
                        MoveGoal = accessor.ReadVector3(controller, accessor.DebugMoveGoal),
                        HasLootGoal = accessor.ReadBool(controller, accessor.DebugHasLootGoal),
                        LootGoal = accessor.ReadVector3(controller, accessor.DebugLootGoal),
                        LootName = accessor.ReadString(controller, accessor.DebugLootName)
                    });
                }

                return entries;
            }

            private static MonoBehaviour FindAiController(GameObject playerObject)
            {
                MonoBehaviour[] behaviours = playerObject.GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour == null)
                        continue;

                    Type type = behaviour.GetType();
                    if (type.FullName == "TabgInstaller.FakePlayers.AiDummyController" || type.Name == "AiDummyController")
                        return behaviour;
                }

                return null;
            }

            private static BotDebugAccessor GetBotDebugAccessor(Component component)
            {
                if (component == null)
                    return null;

                Type controllerType = component.GetType();
                if (_botDebugAccessor == null || _botDebugAccessor.ControllerType != controllerType)
                    _botDebugAccessor = new BotDebugAccessor(controllerType);

                return _botDebugAccessor;
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
                if (raw == "*")
                {
                    _lastRecipientWarning = null;
                    return null;
                }
                if (string.IsNullOrWhiteSpace(raw))
                {
                    WarnRecipientsOnce("empty", "[AdminRadar.Server] Radar.Recipients is empty; no radar packet recipients selected. Use * for everyone.");
                    return new byte[0];
                }

                var result = new List<byte>();
                foreach (var part in raw.Split(','))
                {
                    string token = part.Trim();
                    if (byte.TryParse(token, out byte playerIndex))
                    {
                        if (!result.Contains(playerIndex))
                            result.Add(playerIndex);
                    }
                    else if (!string.IsNullOrWhiteSpace(token))
                    {
                        WarnRecipientsOnce(raw, "[AdminRadar.Server] Ignoring invalid radar recipient '" + token + "'.");
                    }
                }

                if (result.Count == 0)
                    WarnRecipientsOnce(raw, "[AdminRadar.Server] Radar.Recipients did not contain any valid player indexes; no radar packet recipients selected.");
                else
                    _lastRecipientWarning = null;

                return result.ToArray();
            }

            private static void WarnRecipientsOnce(string warningKey, string message)
            {
                if (_lastRecipientWarning == warningKey)
                    return;

                _lastRecipientWarning = warningKey;
                _instance?.Logger.LogWarning(message);
            }

            private sealed class BotDebugAccessor
            {
                private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                public readonly Type ControllerType;
                public readonly PropertyInfo DebugState;
                public readonly PropertyInfo DebugTargetName;
                public readonly PropertyInfo DebugWeaponName;
                public readonly PropertyInfo DebugHasLineOfSight;
                public readonly PropertyInfo DebugIsFiring;
                public readonly PropertyInfo DebugHasMoveGoal;
                public readonly PropertyInfo DebugMoveGoal;
                public readonly PropertyInfo DebugHasLootGoal;
                public readonly PropertyInfo DebugLootGoal;
                public readonly PropertyInfo DebugLootName;

                public BotDebugAccessor(Type controllerType)
                {
                    ControllerType = controllerType;
                    DebugState = controllerType.GetProperty("DebugState", Flags);
                    DebugTargetName = controllerType.GetProperty("DebugTargetName", Flags);
                    DebugWeaponName = controllerType.GetProperty("DebugWeaponName", Flags);
                    DebugHasLineOfSight = controllerType.GetProperty("DebugHasLineOfSight", Flags);
                    DebugIsFiring = controllerType.GetProperty("DebugIsFiring", Flags);
                    DebugHasMoveGoal = controllerType.GetProperty("DebugHasMoveGoal", Flags);
                    DebugMoveGoal = controllerType.GetProperty("DebugMoveGoal", Flags);
                    DebugHasLootGoal = controllerType.GetProperty("DebugHasLootGoal", Flags);
                    DebugLootGoal = controllerType.GetProperty("DebugLootGoal", Flags);
                    DebugLootName = controllerType.GetProperty("DebugLootName", Flags);
                }

                public string ReadString(Component component, PropertyInfo property)
                {
                    try
                    {
                        object value = property?.GetValue(component, null);
                        return value != null ? value.ToString() : string.Empty;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }

                public bool ReadBool(Component component, PropertyInfo property)
                {
                    try
                    {
                        object value = property?.GetValue(component, null);
                        return value is bool b && b;
                    }
                    catch
                    {
                        return false;
                    }
                }

                public Vector3 ReadVector3(Component component, PropertyInfo property)
                {
                    try
                    {
                        object value = property?.GetValue(component, null);
                        if (value is Vector3 vector)
                            return vector;
                    }
                    catch
                    {
                    }

                    return Vector3.zero;
                }
            }

            private struct PlayerRadarEntry
            {
                public byte Index;
                public string Name;
                public Vector3 Position;
                public bool Alive;
            }

            private struct BotDebugEntry
            {
                public byte Index;
                public string State;
                public string TargetName;
                public string WeaponName;
                public bool HasLineOfSight;
                public bool IsFiring;
                public bool HasMoveGoal;
                public Vector3 MoveGoal;
                public bool HasLootGoal;
                public Vector3 LootGoal;
                public string LootName;
            }
        }
    }
}
