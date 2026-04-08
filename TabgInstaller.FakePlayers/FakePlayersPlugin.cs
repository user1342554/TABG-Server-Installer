using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
            Citrus.AddCommand("spawndummy", (string[] prms, TABGPlayerServer player) =>
            {
                if (ServerRef == null) { Citrus.SelfParrot(player, "Server not ready."); return; }

                int count = 1;
                if (prms.Length > 0) int.TryParse(prms[0], out count);
                count = Math.Max(1, Math.Min(count, 200));

                int spawned = SpawnFakePlayers(ServerRef, count);
                Citrus.SelfParrot(player, $"Spawned {spawned} fake player(s). Total: {FakeIndices.Count}");
            }, "FakePlayers", "Spawn fake players", "[count]", 0);

            Citrus.AddCommand("removedummy", (string[] prms, TABGPlayerServer player) =>
            {
                if (ServerRef == null) { Citrus.SelfParrot(player, "Server not ready."); return; }

                int count = 0; // 0 = all
                if (prms.Length > 0) int.TryParse(prms[0], out count);

                int removed = RemoveFakePlayers(ServerRef, count);
                Citrus.SelfParrot(player, $"Removed {removed}. Remaining: {FakeIndices.Count}");
            }, "FakePlayers", "Remove fake players", "[count]", 0);

            Citrus.AddCommand("dummycount", (string[] prms, TABGPlayerServer player) =>
            {
                Citrus.SelfParrot(player, $"Active fake players: {FakeIndices.Count}");
            }, "FakePlayers", "Show fake player count", "", 0);

            Logger.LogInfo("[FakePlayers] Commands registered: /spawndummy, /removedummy, /dummycount");
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

        public static int SpawnFakePlayers(ServerClient server, int count)
        {
            var room = server.GameRoomReference;
            if (room == null) return 0;

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                byte idx = SpawnOne(server, room, _nextNumber);
                if (idx == byte.MaxValue) break;
                FakeIndices.Add(idx);
                _nextNumber++;
                spawned++;
            }
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

                TABGPlayerServer player = room.FindPlayer(idx);
                if (player == null) continue;

                try { room.KillPlayer(player); }
                catch (Exception ex) { Log($"Error removing player {idx}: {ex.Message}"); }
                removed++;
            }

            Log($"Removed {removed}. Remaining: {FakeIndices.Count}");
            return removed;
        }

        private static byte SpawnOne(ServerClient server, GameRoom room, int number)
        {
            byte playerIndex = room.GetNewPlayerIndex();
            if (playerIndex == byte.MaxValue) return byte.MaxValue;

            ulong loginKey = 0uL;
            byte groupIndex = room.GetNewGroupIndex(loginKey, playerIndex);
            string name = "Player" + number;
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

            BroadcastLogin(server, player);

            player.SetInited();
            player.Dropped();
            player.IsReady();

            Vector3 pos = GetSpawnPosition(room, player);
            player.UpdatePosition(pos);
            CurrentGameWorldCommand.InitNewServerPlayer(server, player);

            return playerIndex;
        }

        private static void BroadcastLogin(ServerClient server, TABGPlayerServer player)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write(player.GroupIndex);
                bw.Write(player.PlayerName);
                bw.Write(string.Empty);
                bw.Write(player.GearData.Length);
                for (int i = 0; i < player.GearData.Length; i++)
                    bw.Write(player.GearData[i]);
                bw.Write(false);

                server.SendMessageToClients(
                    EventCode.Login, ms.ToArray(),
                    byte.MaxValue, reliable: true, alsoSendToTeamates: true);
            }
        }

        private static Vector3 GetSpawnPosition(GameRoom room, TABGPlayerServer player)
        {
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
}
