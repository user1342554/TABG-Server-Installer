using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.FakePlayers
{
    /// <summary>
    /// Server plugin that lets you spawn/remove fake players via admin commands.
    ///
    /// Admin commands (type in admin console):
    ///   spawndummy       — spawn 1 fake player
    ///   spawndummy 10    — spawn 10 fake players
    ///   removedummy      — remove all fake players
    ///   removedummy 3    — remove 3 fake players
    ///
    /// Fake players are treated as real by the server: they count toward
    /// start thresholds, receive loadouts, and appear in the player list.
    /// They stand still and do not move.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class FakePlayersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tabginstaller.fakeplayers";
        public const string PluginName = "TABG Fake Players";
        public const string PluginVersion = "1.0.0";

        public static FakePlayersPlugin Instance { get; private set; }
        public static ServerClient ServerRef { get; set; }

        public static ConfigEntry<string> NamePrefix;

        internal static readonly List<byte> FakeIndices = new List<byte>();
        private static int _nextNumber = 1;

        private void Awake()
        {
            Instance = this;

            NamePrefix = Config.Bind("General", "NamePrefix", "Player",
                "Name prefix for fake players (e.g. Player1, Player2...)");

            new Harmony(PluginGuid).PatchAll();

            Logger.LogInfo("[FakePlayers] Loaded. Use admin commands: spawndummy [count], removedummy [count]");
        }

        /// <summary>
        /// Spawns N fake players into the current game room.
        /// </summary>
        public static int SpawnFakePlayers(ServerClient server, int count)
        {
            var room = server.GameRoomReference;
            if (room == null)
            {
                Log("No game room available.");
                return 0;
            }

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

        /// <summary>
        /// Removes fake players from the game room.
        /// Pass 0 to remove all.
        /// </summary>
        public static int RemoveFakePlayers(ServerClient server, int count)
        {
            var room = server.GameRoomReference;
            if (room == null || FakeIndices.Count == 0)
            {
                Log("No fake players to remove.");
                return 0;
            }

            int toRemove = count <= 0 ? FakeIndices.Count : Math.Min(count, FakeIndices.Count);
            int removed = 0;

            for (int i = 0; i < toRemove; i++)
            {
                byte idx = FakeIndices[FakeIndices.Count - 1];
                FakeIndices.RemoveAt(FakeIndices.Count - 1);

                TABGPlayerServer player = room.FindPlayer(idx);
                if (player == null) continue;

                // Kill the fake player so the server cleans up game state
                try
                {
                    room.KillPlayer(player);
                }
                catch (Exception ex)
                {
                    Log($"Error removing player {idx}: {ex.Message}");
                }

                removed++;
            }

            Log($"Removed {removed} fake player(s). Remaining: {FakeIndices.Count}");
            return removed;
        }

        /// <summary>
        /// Creates a single fake player using the same flow as SpawnBotCommand.
        /// </summary>
        private static byte SpawnOne(ServerClient server, GameRoom room, int number)
        {
            byte playerIndex = room.GetNewPlayerIndex();
            if (playerIndex == byte.MaxValue)
            {
                Log("No player index available, server full.");
                return byte.MaxValue;
            }

            ulong loginKey = 0uL;
            byte groupIndex = room.GetNewGroupIndex(loginKey, playerIndex);
            string name = (NamePrefix?.Value ?? "Player") + number;
            int[] gearData = { 2 };

            // bot=true prevents creating a network message queue (no real connection)
            var player = new TABGPlayerServer(
                name, playerIndex, groupIndex, loginKey,
                null, 0, gearData,
                room.CurrentGameSettings.MaxPlayers,
                admin: false, bot: true);

            room.AddPlayer(player, wantsToBeAlone: true);
            player.WasAccepted();
            server.CheckForMaxCapaciy();
            room.DecrementReservedSquadSlots(loginKey);

            // Broadcast Login to all real clients so they see the player
            BroadcastLogin(server, player);

            player.SetInited();
            player.Dropped();
            player.IsReady();

            // Place at a valid spawn point
            Vector3 pos = GetSpawnPosition(room, player);
            player.UpdatePosition(pos);

            // Create the server-side GameObject (physics, chunk registration)
            CurrentGameWorldCommand.InitNewServerPlayer(server, player);

            return playerIndex;
        }

        /// <summary>
        /// Sends the EventCode.Login packet so real clients create a
        /// TABGPlayerClient for this fake player.
        /// </summary>
        private static void BroadcastLogin(ServerClient server, TABGPlayerServer player)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(player.PlayerIndex);
                bw.Write(player.GroupIndex);
                bw.Write(player.PlayerName);
                bw.Write(string.Empty); // death phrase
                bw.Write(player.GearData.Length);
                for (int i = 0; i < player.GearData.Length; i++)
                    bw.Write(player.GearData[i]);
                bw.Write(false); // admin flag

                server.SendMessageToClients(
                    EventCode.Login, ms.ToArray(),
                    byte.MaxValue, reliable: true, alsoSendToTeamates: true);
            }
        }

        /// <summary>
        /// Gets a spawn position from the game room via reflection,
        /// since the SpawnPoint return type may not be directly accessible.
        /// </summary>
        private static Vector3 GetSpawnPosition(GameRoom room, TABGPlayerServer player)
        {
            try
            {
                object spawnPoint = room.GetNewPlayerSpawnPoint(player);
                if (spawnPoint == null) return Vector3.zero;

                var type = spawnPoint.GetType();
                var prop = type.GetProperty("Position",
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                    return (Vector3)prop.GetValue(spawnPoint);

                var field = type.GetField("Position",
                    BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                    return (Vector3)field.GetValue(spawnPoint);
            }
            catch (Exception ex)
            {
                Log($"Could not get spawn position: {ex.Message}");
            }

            return Vector3.zero;
        }

        public static bool IsFakePlayer(byte playerIndex) => FakeIndices.Contains(playerIndex);

        internal static void Log(string msg)
        {
            if (Instance != null)
                Instance.Logger.LogInfo($"[FakePlayers] {msg}");
            else
                LandLog.Log("[FakePlayers] " + msg);
        }
    }
}
