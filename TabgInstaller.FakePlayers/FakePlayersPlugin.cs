using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using CitrusLib;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.FakePlayers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.cyrusthelesser.citruslib", BepInDependency.DependencyFlags.SoftDependency)]
    public class FakePlayersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tabginstaller.fakeplayers";
        public const string PluginName = "TABG Fake Players";
        public const string PluginVersion = "1.0.0";

        public static FakePlayersPlugin Instance { get; private set; }
        public static ServerClient ServerRef { get; set; }

        internal static readonly List<byte> FakeIndices = new List<byte>();
        internal static readonly List<byte> AiIndices = new List<byte>();
        private static readonly Dictionary<byte, int> PendingAiLevels = new Dictionary<byte, int>();
        private static int _nextAiThrownItemIndex = 50000;
        private static int _nextNumber = 1;

        private void Awake()
        {
            Instance = this;
            new Harmony(PluginGuid).PatchAll();
            Logger.LogInfo("[FakePlayers] Loaded.");
        }

        /// <summary>
        /// Start() runs after all plugins are initialized, so Citruslib is ready.
        /// </summary>
        private void Start()
        {
            try
            {
                RegisterCommands();
                PatchPermissions();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[FakePlayers] Citruslib integration failed: {ex.Message}");
            }
        }

        private void RegisterCommands()
        {
            Action<string[], TABGPlayerServer> spawnAiCommand = (string[] prms, TABGPlayerServer player) =>
            {
                var server = ResolveServer();
                if (server == null) { Citrus.SelfParrot(player, "Server not ready."); return; }

                int count = 1;
                if (prms.Length > 0 && !int.TryParse(prms[0], out count))
                    count = 1;
                count = Math.Max(1, Math.Min(count, 32));

                int level = ParseAiLevel(prms.Length > 1 ? prms[1] : null);
                int spawned = SpawnFakePlayers(server, count, player, aiControlled: true, aiLevel: level);
                Citrus.SelfParrot(player, $"Spawned {spawned} AI dummy player(s), level {level}. Total dummies: {FakeIndices.Count}");
            };

            Citrus.AddCommand("spawndummy", (string[] prms, TABGPlayerServer player) =>
            {
                var server = ResolveServer();
                if (server == null) { Citrus.SelfParrot(player, "Server not ready."); return; }

                int count = 1;
                if (prms.Length > 0 && !int.TryParse(prms[0], out count))
                    count = 1;
                count = Math.Max(1, Math.Min(count, 200));

                int spawned = SpawnFakePlayers(server, count, player);
                Citrus.SelfParrot(player, $"Spawned {spawned} fake player(s). Total: {FakeIndices.Count}");
            }, "FakePlayers", "Spawn fake players", "[count]", 0);

            Citrus.AddCommand("spawnaidummy", spawnAiCommand, "FakePlayers", "Spawn AI dummy players", "[count] [level 1-5]", 0);
            Citrus.AddCommand("aidummy", spawnAiCommand, "FakePlayers", "Spawn AI dummy players", "[count] [level 1-5]", 0);
            Citrus.AddCommand("spawnai", spawnAiCommand, "FakePlayers", "Spawn AI dummy players", "[count] [level 1-5]", 0);

            Citrus.AddCommand("removedummy", (string[] prms, TABGPlayerServer player) =>
            {
                var server = ResolveServer();
                if (server == null) { Citrus.SelfParrot(player, "Server not ready."); return; }

                int count = 0; // 0 = all
                if (prms.Length > 0 && !int.TryParse(prms[0], out count))
                    count = 0;

                int removed = RemoveFakePlayers(server, count);
                Citrus.SelfParrot(player, $"Removed {removed}. Remaining: {FakeIndices.Count}");
            }, "FakePlayers", "Remove fake players", "[count]", 0);

            Citrus.AddCommand("dummycount", (string[] prms, TABGPlayerServer player) =>
            {
                Citrus.SelfParrot(player, $"Active fake players: {FakeIndices.Count}");
            }, "FakePlayers", "Show fake player count", "", 0);

            Logger.LogInfo("[FakePlayers] Commands registered: /spawndummy, /spawnaidummy, /aidummy, /spawnai, /removedummy, /dummycount");
        }

        /// <summary>
        /// Patches Citruslib's internal Command.Run to skip the permission check,
        /// so every player can use every command (not just our commands — ALL commands).
        /// </summary>
        private void PatchPermissions()
        {
            var commandType = AccessTools.TypeByName("CitrusLib.Command");
            if (commandType == null)
            {
                Logger.LogWarning("[FakePlayers] Could not find CitrusLib.Command type for perm bypass.");
                return;
            }

            var runMethod = AccessTools.Method(commandType, "Run");
            if (runMethod == null)
            {
                Logger.LogWarning("[FakePlayers] Could not find Command.Run method for perm bypass.");
                return;
            }

            var prefix = new HarmonyMethod(typeof(PermBypassPatch), nameof(PermBypassPatch.Prefix));
            new Harmony(PluginGuid + ".perms").Patch(runMethod, prefix: prefix);
            Logger.LogInfo("[FakePlayers] Permission bypass applied — all commands usable by everyone.");
        }

        // -----------------------------------------------------------------
        // Spawning / Removing
        // -----------------------------------------------------------------

        internal static ServerClient ResolveServer()
        {
            if (ServerRef != null && ServerRef.GameRoomReference != null)
                return ServerRef;

            try
            {
                ServerRef = Citrus.World;
            }
            catch
            {
                ServerRef = null;
            }

            if (ServerRef != null && ServerRef.GameRoomReference != null)
                return ServerRef;

            ServerRef = UnityEngine.Object.FindObjectOfType<ServerClient>();
            return ServerRef != null && ServerRef.GameRoomReference != null ? ServerRef : null;
        }

        public static int SpawnFakePlayers(ServerClient server, int count, TABGPlayerServer anchorPlayer = null, bool aiControlled = false, int aiLevel = 1)
        {
            var room = server.GameRoomReference;
            if (room == null) return 0;

            PruneMissingFakePlayers(room);

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                byte idx = SpawnOne(server, room, _nextNumber, anchorPlayer, spawned, aiControlled, aiLevel);
                if (idx == byte.MaxValue) break;
                FakeIndices.Add(idx);
                if (aiControlled)
                    AiIndices.Add(idx);
                _nextNumber++;
                spawned++;
            }

            if (spawned > 0)
                room.CheckGameState();

            Log($"Spawned {spawned} fake player(s). Total: {FakeIndices.Count}");
            return spawned;
        }

        public static int RemoveFakePlayers(ServerClient server, int count)
        {
            var room = server.GameRoomReference;
            if (room == null || FakeIndices.Count == 0) return 0;

            int toRemove = count <= 0 ? FakeIndices.Count : Math.Min(count, FakeIndices.Count);
            int removed = 0;

            for (int i = 0; i < toRemove; i++)
            {
                byte idx = FakeIndices[FakeIndices.Count - 1];
                FakeIndices.RemoveAt(FakeIndices.Count - 1);
                AiIndices.Remove(idx);

                TABGPlayerServer player = room.FindPlayer(idx);
                if (player == null) continue;

                try
                {
                    BroadcastLeave(server, idx);
                    room.KillPlayer(player);
                }
                catch (Exception ex) { Log($"Error removing player {idx}: {ex.Message}"); }
                removed++;
            }

            room.CheckGameState();
            Log($"Removed {removed}. Remaining: {FakeIndices.Count}");
            return removed;
        }

        internal static void PruneMissingFakePlayers(GameRoom room)
        {
            for (int i = FakeIndices.Count - 1; i >= 0; i--)
            {
                if (room.FindPlayer(FakeIndices[i]) == null)
                {
                    AiIndices.Remove(FakeIndices[i]);
                    FakeIndices.RemoveAt(i);
                }
            }
        }

        private static byte SpawnOne(ServerClient server, GameRoom room, int number, TABGPlayerServer anchorPlayer, int spawnOffset, bool aiControlled, int aiLevel)
        {
            byte playerIndex = room.GetNewPlayerIndex();
            if (playerIndex == byte.MaxValue) return byte.MaxValue;

            ulong loginKey = 0uL;
            byte groupIndex = room.GetNewGroupIndex(loginKey, playerIndex);
            string name = (aiControlled ? "AIPlayer" : "Player") + number;
            int[] gearData = { 2 };

            var player = new TABGPlayerServer(
                name, playerIndex, groupIndex, loginKey,
                null, 0, gearData,
                room.CurrentGameSettings.MaxPlayers,
                admin: false, bot: true);

            room.AddPlayer(player, wantsToBeAlone: true);
            player.WasAccepted();
            server.CheckForMaxCapaciy();
            room.DecrementReservedSquadSlots(loginKey);

            player.SetInited();
            if (!aiControlled)
                player.Dropped();
            player.IsReady();
            player.AntiCheatAuthorized();

            Vector3 pos = GetSpawnPosition(room, player, anchorPlayer, spawnOffset);
            player.UpdatePosition(pos);
            CurrentGameWorldCommand.InitNewServerPlayer(server, player);

            BroadcastLogin(server, player);
            if (!aiControlled)
            {
                BroadcastRespawn(server, player, pos);
                BroadcastPlayerUpdate(server, player, pos);
                QueueDelayedUpdate(server, room, playerIndex, 0.5f);
                QueueDelayedUpdate(server, room, playerIndex, 1.0f);
                QueueDelayedUpdate(server, room, playerIndex, 2.0f);
            }
            if (aiControlled)
            {
                PendingAiLevels[playerIndex] = Mathf.Clamp(aiLevel, 1, 5);
                QueueAiInit(server, room, playerIndex, 0.75f);
                Log($"AI dummy {name} level {PendingAiLevels[playerIndex]} queued at {pos}.");
            }

            return playerIndex;
        }

        private static void BroadcastLogin(ServerClient server, TABGPlayerServer player)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(player.PlayerName);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write(player.GroupIndex);
                bw.Write(nameBytes.Length);
                bw.Write(nameBytes);
                bw.Write(player.GearData.Length);
                for (int i = 0; i < player.GearData.Length; i++)
                    bw.Write(player.GearData[i]);
                bw.Write(false);

                SendToRealClients(server, EventCode.Login, ms.ToArray(), reliable: true, alsoSendToTeamates: true);
            }
        }

        internal static void BroadcastRespawn(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)1);
                bw.Write(player.PlayerIndex);
                bw.Write(player.PlayerIndex);
                bw.Write(player.Health);
                bw.Write(pos.x);
                bw.Write(pos.y);
                bw.Write(pos.z);
                bw.Write(player.PlayerRotation.y);
                bw.Write(byte.MaxValue);

                SendToRealClients(server, EventCode.PlayerRespawn, ms.ToArray(), reliable: true, alsoSendToTeamates: true);
            }
        }

        internal static void RespawnWithVanillaPacket(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            if (server == null || player == null)
                return;

            byte[] packet = RespawnEntityCommand.MakeCommand(server, player, pos, byte.MaxValue);
            SendToRealClients(server, EventCode.PlayerRespawn, packet, reliable: true, alsoSendToTeamates: true);
            BroadcastPlayerUpdate(server, player, pos);
            QueueDelayedUpdate(server, server.GameRoomReference, player.PlayerIndex, 0.15f);
            QueueDelayedUpdate(server, server.GameRoomReference, player.PlayerIndex, 0.5f);
            QueueDelayedUpdate(server, server.GameRoomReference, player.PlayerIndex, 1.25f);
        }

        internal static void BroadcastPlayerUpdate(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            byte[] direction = NetworkOptimizationHelper.OptimizeDirection(player.MovementDirection);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Time.unscaledTime);
                bw.Write((byte)1);
                bw.Write(player.PlayerIndex);
                bw.Write((byte)PacketContainerFlags.All);
                bw.Write((byte)DrivingState.None);
                bw.Write(pos.x);
                bw.Write(pos.y);
                bw.Write(pos.z);
                bw.Write(player.PlayerRotation.x);
                bw.Write(player.PlayerRotation.y);
                bw.Write(player.IsADS);
                bw.Write(direction);
                bw.Write(player.MovementType);
                bw.Write((byte)0);

                SendToRealClients(server, EventCode.PlayerUpdate, ms.ToArray(), reliable: false, alsoSendToTeamates: true);
            }
        }

        internal static void BroadcastWeaponChanged(ServerClient server, TABGPlayerServer player)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write((byte)player.Equipment[5]);
                bw.Write(player.Equipment[0]);
                bw.Write(player.Equipment[1]);
                bw.Write(player.Equipment[2]);
                bw.Write(player.Equipment[3]);
                bw.Write(player.Equipment[4]);
                bw.Write((byte)player.Attachments.Length);
                for (int i = 0; i < player.Attachments.Length; i++)
                    bw.Write(player.Attachments[i]);
                bw.Write((short)-1);

                SendToRealClients(server, EventCode.WeaponChanged, ms.ToArray(), reliable: true);
            }
        }

        internal static void BroadcastFire(ServerClient server, TABGPlayerServer player, Vector3 target)
        {
            Vector3 dir = target - player.PlayerPosition;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();

            Quaternion rot = Quaternion.LookRotation(dir);
            byte[] rotBytes = NetworkOptimizationHelper.OptimizeQuaternion(rot);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write((byte)(FiringMode.Semi | FiringMode.ContainsDirection));
                bw.Write(-1);
                bw.Write(player.PlayerPosition.x);
                bw.Write(player.PlayerPosition.y + 1.3f);
                bw.Write(player.PlayerPosition.z);
                bw.Write(rotBytes);

                SendToRealClients(server, EventCode.PlayerFire, ms.ToArray(), reliable: true);
            }
        }

        internal static void BroadcastFullAutoStart(ServerClient server, TABGPlayerServer player, Vector3 target)
        {
            Vector3 dir = target - player.PlayerPosition;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();

            Quaternion rot = Quaternion.LookRotation(dir);
            byte[] rotBytes = NetworkOptimizationHelper.OptimizeQuaternion(rot);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write((byte)(FiringMode.FullAutoStart | FiringMode.ContainsDirection));
                bw.Write(-1);
                bw.Write(player.PlayerPosition.x);
                bw.Write(player.PlayerPosition.y + 1.3f);
                bw.Write(player.PlayerPosition.z);
                bw.Write(rotBytes);

                SendToRealClients(server, EventCode.PlayerFire, ms.ToArray(), reliable: true);
            }
        }

        internal static void BroadcastFullAutoStop(ServerClient server, TABGPlayerServer player, int bulletsFired)
        {
            if (server == null || player == null)
                return;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write((byte)FiringMode.FullAutoStop);
                bw.Write(-1);
                bw.Write(Math.Max(1, bulletsFired));

                SendToRealClients(server, EventCode.PlayerFire, ms.ToArray(), reliable: true);
            }
        }

        internal static void BroadcastGrenadeThrow(ServerClient server, TABGPlayerServer player, int itemIdentifier, int quantity, Vector3 position, Vector3 direction, bool sync)
        {
            if (server == null || player == null)
                return;

            int networkIndex = _nextAiThrownItemIndex++;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write(networkIndex);
                bw.Write(itemIdentifier);
                bw.Write(Math.Max(1, quantity));
                bw.Write(position.x);
                bw.Write(position.y);
                bw.Write(position.z);
                bw.Write(direction.x);
                bw.Write(direction.y);
                bw.Write(direction.z);
                bw.Write(sync);

                SendToRealClients(server, EventCode.ItemThrown, ms.ToArray(), reliable: true);
            }
        }

        internal static void BroadcastAirplaneDrop(ServerClient server, TABGPlayerServer player, Vector3 position, Vector3 forward)
        {
            if (server == null || player == null)
                return;

            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write(position.x);
                bw.Write(position.y);
                bw.Write(position.z);
                bw.Write(forward.x);
                bw.Write(forward.y);
                bw.Write(forward.z);

                SendToRealClients(server, EventCode.PlayerAirplaneDropped, ms.ToArray(), reliable: true, alsoSendToTeamates: true);
            }
        }

        internal static void ApplyDamage(ServerClient server, TABGPlayerServer attacker, TABGPlayerServer target, float damage)
        {
            if (target == null || attacker == null || target.IsDead || target.IsDowned)
                return;

            Vector3 dir = target.PlayerPosition - attacker.PlayerPosition;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();

            float newHealth = Mathf.Max(0f, target.Health - damage);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(attacker.PlayerIndex);
                bw.Write(target.PlayerIndex);
                bw.Write(newHealth);
                bw.Write(dir.x);
                bw.Write(dir.y);
                bw.Write(dir.z);
                bw.Write(false);
                bw.Write(false);

                PlayerDamageCommand.Run(ms.ToArray(), server, attacker.PlayerIndex);
            }
        }

        internal static void ApplyDirectDamage(ServerClient server, TABGPlayerServer attacker, TABGPlayerServer target, float damage)
        {
            if (server == null || attacker == null || target == null || target.IsDead || target.IsDowned)
                return;

            Vector3 dir = target.PlayerPosition - attacker.PlayerPosition;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();

            target.UpdateLastAttacker(attacker.PlayerIndex);
            target.TakeDamage(Mathf.Max(0f, damage));

            byte flags = 0;
            flags = flags.SetBit(2);
            byte[] dirBytes = NetworkOptimizationHelper.OptimizeDirection(dir);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(target.PlayerIndex);
                bw.Write(attacker.PlayerIndex);
                bw.Write(target.Health);
                bw.Write(flags);
                bw.Write(dirBytes);
                SendToRealClients(server, EventCode.PlayerDamaged, ms.ToArray(), reliable: true, alsoSendToTeamates: true);
            }

            if (target.Health <= 0f && !target.IsDead)
                server.KillPlayer(target);
        }

        private static void BroadcastLeave(ServerClient server, byte playerIndex)
        {
            SendToRealClients(
                server,
                EventCode.PlayerLeft,
                new[] { playerIndex, (byte)1 },
                reliable: true,
                alsoSendToTeamates: true);
        }

        private static void SendToRealClients(ServerClient server, EventCode eventCode, byte[] data, bool reliable, bool alsoSendToTeamates = false)
        {
            byte[] recipients = GetRealRecipients(server);
            if (recipients.Length == 0)
                return;

            server.SendMessageToClients(eventCode, data, recipients, reliable, alsoSendToTeamates);
        }

        private static byte[] GetRealRecipients(ServerClient server)
        {
            var room = server != null ? server.GameRoomReference : null;
            if (room == null)
                return Array.Empty<byte>();

            var recipients = new List<byte>();
            for (int i = 0; i < room.Players.Count; i++)
            {
                TABGPlayerServer player = room.Players[i];
                if (player != null && !player.Bot && !recipients.Contains(player.PlayerIndex))
                    recipients.Add(player.PlayerIndex);
            }

            for (int i = 0; i < room.Spectators.Count; i++)
            {
                TABGPlayerServer player = room.Spectators[i];
                if (player != null && !player.Bot && !recipients.Contains(player.PlayerIndex))
                    recipients.Add(player.PlayerIndex);
            }

            return recipients.ToArray();
        }

        private static void QueueDelayedUpdate(ServerClient server, GameRoom room, byte playerIndex, float delay)
        {
            server.WaitThenDoAction(delay, () =>
            {
                TABGPlayerServer current = room.FindPlayer(playerIndex);
                if (current != null)
                    BroadcastPlayerUpdate(server, current, current.PlayerPosition);
            });
        }

        private static void QueueAiInit(ServerClient server, GameRoom room, byte playerIndex, float delay)
        {
            server.WaitThenDoAction(delay, () =>
            {
                TABGPlayerServer current = room.FindPlayer(playerIndex);
                if (current == null || current.PlayerObject == null)
                    return;

                if (current.PlayerObject.GetComponent<AiDummyController>() == null)
                {
                    int level;
                    if (!PendingAiLevels.TryGetValue(playerIndex, out level))
                        level = 1;
                    PendingAiLevels.Remove(playerIndex);

                    current.PlayerObject.AddComponent<AiDummyController>().Init(server, current, level);
                    Log($"AI dummy {current.PlayerName} initialized at level {level}.");
                }
            });
        }

        private static int ParseAiLevel(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 1;

            switch (value.ToLowerInvariant())
            {
                case "easy": return 1;
                case "normal": return 3;
                case "hard": return 5;
            }

            int level;
            if (!int.TryParse(value, out level))
                level = 1;
            return Mathf.Clamp(level, 1, 5);
        }

        private static Vector3 GetSpawnPosition(GameRoom room, TABGPlayerServer player, TABGPlayerServer anchorPlayer, int spawnOffset)
        {
            if (anchorPlayer != null && !anchorPlayer.IsDead && anchorPlayer.PlayerObject != null)
            {
                Vector3 anchor = anchorPlayer.PlayerPosition;
                float angle = spawnOffset * 1.5707964f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * 2.5f, 0f, Mathf.Sin(angle) * 2.5f);
                return anchor + offset;
            }

            try
            {
                object spawnPoint = room.GetNewPlayerSpawnPoint(player);
                if (spawnPoint == null) return Vector3.zero;

                var type = spawnPoint.GetType();
                var prop = type.GetProperty("Position", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null) return (Vector3)prop.GetValue(spawnPoint);

                var field = type.GetField("Position", BindingFlags.Public | BindingFlags.Instance);
                if (field != null) return (Vector3)field.GetValue(spawnPoint);
            }
            catch (Exception ex) { Log($"Error getting spawn position: {ex.Message}"); }
            return Vector3.zero;
        }

        internal static void Log(string msg)
        {
            if (Instance != null)
                Instance.Logger.LogInfo($"[FakePlayers] {msg}");
        }
    }

    /// <summary>
    /// Prefix patch for CitrusLib.Command.Run — skips the permission check
    /// so every player can run every command regardless of perm level.
    /// </summary>
    internal static class PermBypassPatch
    {
        public static bool Prefix(object __instance, ref bool __result, string[] prms, TABGPlayerServer player)
        {
            try
            {
                var funcField = AccessTools.Field(__instance.GetType(), "func");
                if (funcField != null)
                {
                    var func = funcField.GetValue(__instance) as Action<string[], TABGPlayerServer>;
                    if (func != null)
                    {
                        func.Invoke(prms, player);
                        __result = true;
                        return false; // Skip original (which has perm check)
                    }
                }
            }
            catch (Exception ex)
            {
                FakePlayersPlugin.Log($"Perm bypass error: {ex.Message}");
            }
            return true; // Fallback to original
        }
    }

    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.SendMessageToClients), new[] { typeof(EventCode), typeof(byte[]), typeof(byte[]), typeof(bool), typeof(bool) })]
    internal static class FilterFakeRecipientsPatch
    {
        public static bool Prefix(ref byte[] recipents)
        {
            if (recipents == null || recipents.Length == 0 || FakePlayersPlugin.FakeIndices.Count == 0)
                return true;

            if (recipents.Length == 1 && recipents[0] == byte.MaxValue)
                return true;

            int write = 0;
            for (int i = 0; i < recipents.Length; i++)
            {
                if (!FakePlayersPlugin.FakeIndices.Contains(recipents[i]))
                    recipents[write++] = recipents[i];
            }

            if (write == 0)
                return false;

            if (write != recipents.Length)
            {
                byte[] filtered = new byte[write];
                Array.Copy(recipents, filtered, write);
                recipents = filtered;
            }

            return true;
        }
    }
}
