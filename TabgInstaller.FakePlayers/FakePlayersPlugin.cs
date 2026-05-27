using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
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

        internal struct GunshotSoundEvent
        {
            public int Sequence;
            public byte ShooterIndex;
            public Vector3 Position;
            public FiringMode Mode;
            public float Time;

            public GunshotSoundEvent(int sequence, byte shooterIndex, Vector3 position, FiringMode mode, float time)
            {
                Sequence = sequence;
                ShooterIndex = shooterIndex;
                Position = position;
                Mode = mode;
                Time = time;
            }
        }

        internal static readonly List<byte> FakeIndices = new List<byte>();
        internal static readonly List<byte> AiIndices = new List<byte>();
        internal static readonly List<GunshotSoundEvent> GunshotSounds = new List<GunshotSoundEvent>();
        private static readonly Dictionary<byte, int> PendingAiLevels = new Dictionary<byte, int>();
        private static int _nextNumber = 1;
        internal static int GunshotSoundSequence { get; private set; }
        internal static ConfigEntry<int> MaxFakeSpawnCount;
        internal static ConfigEntry<int> MaxAiSpawnCount;
        internal static ConfigEntry<bool> CommandsUsableByEveryone;

        private void Awake()
        {
            MaxFakeSpawnCount = Config.Bind("Commands", "MaxFakeSpawnCount", 200, "Maximum fake players spawned by one /spawndummy command.");
            MaxAiSpawnCount = Config.Bind("Commands", "MaxAiSpawnCount", 32, "Maximum AI dummy players spawned by one /spawnaidummy command.");
            CommandsUsableByEveryone = Config.Bind("Commands", "CommandsUsableByEveryone", true, "Bypass Citrus permissions for FakePlayers commands.");

            Instance = this;
            new Harmony(PluginGuid).PatchAll();
            Logger.LogInfo("[FakePlayers] Loaded.");
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this))
            {
                ResetStaticMatchState();
                ServerRef = null;
                Instance = null;
            }
        }

        /// <summary>
        /// Start() runs after all plugins are initialized, so Citruslib is ready.
        /// </summary>
        private void Start()
        {
            try
            {
                RegisterCommands();
                if (CommandsUsableByEveryone.Value)
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
                count = Math.Max(1, Math.Min(count, Math.Max(1, MaxAiSpawnCount.Value)));

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
                count = Math.Max(1, Math.Min(count, Math.Max(1, MaxFakeSpawnCount.Value)));

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

            Citrus.AddCommand("inspectbot", (string[] prms, TABGPlayerServer player) =>
            {
                var server = ResolveServer();
                if (server == null || server.GameRoomReference == null) { Citrus.SelfParrot(player, "Server not ready."); return; }

                AiDummyController controller = FindAiController(server.GameRoomReference, prms.Length > 0 ? prms[0] : null);
                if (controller == null)
                {
                    Citrus.SelfParrot(player, "No matching AI dummy found. Use /inspectbot [index|name].");
                    return;
                }

                Citrus.SelfParrot(player, controller.GetDebugSummary());
            }, "FakePlayers", "Inspect one AI dummy", "[index|name]", 0);

            Logger.LogInfo("[FakePlayers] Commands registered: /spawndummy, /spawnaidummy, /aidummy, /spawnai, /removedummy, /dummycount, /inspectbot");
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
                TrackFakePlayer(idx, aiControlled);
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
                ForgetFakePlayer(idx);

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
            if (room == null)
                return;

            for (int i = FakeIndices.Count - 1; i >= 0; i--)
            {
                if (room.FindPlayer(FakeIndices[i]) == null)
                {
                    ForgetFakePlayer(FakeIndices[i]);
                }
            }
        }

        internal static void ResetStaticMatchState()
        {
            FakeIndices.Clear();
            AiIndices.Clear();
            PendingAiLevels.Clear();
            GunshotSounds.Clear();
            GunshotSoundSequence = 0;
            _nextNumber = 1;
            ServerMessages.ResetTransientState();
        }

        private static void TrackFakePlayer(byte playerIndex, bool aiControlled)
        {
            if (!FakeIndices.Contains(playerIndex))
                FakeIndices.Add(playerIndex);
            if (aiControlled && !AiIndices.Contains(playerIndex))
                AiIndices.Add(playerIndex);
        }

        private static void ForgetFakePlayer(byte playerIndex)
        {
            FakeIndices.Remove(playerIndex);
            AiIndices.Remove(playerIndex);
            PendingAiLevels.Remove(playerIndex);
        }

        private static AiDummyController FindAiController(GameRoom room, string query)
        {
            if (room == null || room.Players == null)
                return null;

            byte requestedIndex = 0;
            bool hasIndex = !string.IsNullOrWhiteSpace(query) && byte.TryParse(query, out requestedIndex);
            string requestedName = query ?? string.Empty;

            for (int i = 0; i < room.Players.Count; i++)
            {
                TABGPlayerServer candidate = room.Players[i];
                if (candidate == null || candidate.PlayerObject == null)
                    continue;

                if (hasIndex && candidate.PlayerIndex != requestedIndex)
                    continue;
                if (!hasIndex && !string.IsNullOrWhiteSpace(requestedName) && candidate.PlayerName.IndexOf(requestedName, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                AiDummyController controller = candidate.PlayerObject.GetComponent<AiDummyController>();
                if (controller != null)
                    return controller;
            }

            return null;
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
            ServerMessages.SendLogin(server, player);
        }

        internal static void BroadcastRespawn(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            ServerMessages.SendRespawn(server, player, pos);
        }

        internal static void RespawnWithVanillaPacket(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            if (server == null || player == null)
                return;

            ServerMessages.SendVanillaRespawn(server, player, pos);
            BroadcastPlayerUpdate(server, player, pos);
            QueueDelayedUpdate(server, server.GameRoomReference, player.PlayerIndex, 0.15f);
            QueueDelayedUpdate(server, server.GameRoomReference, player.PlayerIndex, 0.5f);
            QueueDelayedUpdate(server, server.GameRoomReference, player.PlayerIndex, 1.25f);
        }

        internal static void BroadcastPlayerUpdate(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            ServerMessages.SendPlayerUpdate(server, player, pos);
        }

        internal static void BroadcastSeatAccepted(ServerClient server, TABGPlayerServer player, TABGCarServer car, TABGCarServerSeat seat, bool getIn)
        {
            ServerMessages.SendSeatAccepted(server, player, car, seat, getIn);
        }

        internal static void BroadcastWeaponChanged(ServerClient server, TABGPlayerServer player)
        {
            ServerMessages.SendWeaponChanged(server, player);
        }

        internal static void BroadcastPickupAccepted(ServerClient server, TABGPlayerServer player, NetworkGun loot, byte slot)
        {
            ServerMessages.SendPickupAccepted(server, player, loot, slot);
        }

        internal static void BroadcastFire(ServerClient server, TABGPlayerServer player, Vector3 target)
        {
            ServerMessages.SendFire(server, player, target, FiringMode.Semi);
        }

        internal static void BroadcastFullAutoStart(ServerClient server, TABGPlayerServer player, Vector3 target)
        {
            ServerMessages.SendFire(server, player, target, FiringMode.FullAutoStart);
        }

        internal static void BroadcastFullAutoStop(ServerClient server, TABGPlayerServer player, int bulletsFired)
        {
            ServerMessages.SendFullAutoStop(server, player, bulletsFired);
        }

        internal static void BroadcastGrenadeThrow(ServerClient server, TABGPlayerServer player, int itemIdentifier, int quantity, Vector3 position, Vector3 direction, bool sync)
        {
            ServerMessages.SendGrenadeThrow(server, player, itemIdentifier, quantity, position, direction, sync);
        }

        internal static void ApplyHeal(ServerClient server, TABGPlayerServer player, float newHealth)
        {
            if (server == null || player == null || player.IsDead)
                return;

            newHealth = Mathf.Clamp(newHealth, player.Health, 100f);
            if (newHealth <= player.Health)
                return;

            player.UpdateHealth(newHealth);
            ServerMessages.SendHealthStateChanged(server, player, newHealth);
        }

        internal static void BroadcastAirplaneDrop(ServerClient server, TABGPlayerServer player, Vector3 position, Vector3 forward)
        {
            ServerMessages.SendAirplaneDrop(server, player, position, forward);
        }

        internal static void ApplyDamage(ServerClient server, TABGPlayerServer attacker, TABGPlayerServer target, float damage)
        {
            if (target == null || attacker == null || target.IsDead || target.IsDowned)
                return;

            float newHealth = Mathf.Max(0f, target.Health - damage);
            byte[] damageCommand = ServerMessages.MakeDamageCommand(attacker, target, newHealth);

            // Server-side fake attackers are not real chunk watchers, so report through the victim path.
            PlayerDamageCommand.Run(damageCommand, server, target.PlayerIndex);
        }

        internal static void ApplyDirectDamage(ServerClient server, TABGPlayerServer attacker, TABGPlayerServer target, float damage)
        {
            if (server == null || attacker == null || target == null || target.IsDead || target.IsDowned)
                return;

            target.UpdateLastAttacker(attacker.PlayerIndex);
            target.TakeDamage(Mathf.Max(0f, damage));
            ServerMessages.SendDirectDamage(server, attacker, target);

            if (target.Health <= 0f && !target.IsDead)
                server.KillPlayer(target);
        }

        private static void BroadcastLeave(ServerClient server, byte playerIndex)
        {
            ServerMessages.SendLeave(server, playerIndex);
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

        internal static void RecordGunshot(TABGPlayerServer shooter, FiringMode mode)
        {
            if (shooter == null || shooter.Bot || FakeIndices.Contains(shooter.PlayerIndex))
                return;

            FiringMode audibleModes = FiringMode.Semi | FiringMode.Burst | FiringMode.FullAutoStart;
            if ((mode & audibleModes) == FiringMode.None)
                return;

            GunshotSoundSequence++;
            GunshotSounds.Add(new GunshotSoundEvent(GunshotSoundSequence, shooter.PlayerIndex, shooter.PlayerPosition, mode, Time.unscaledTime));

            float cutoff = Time.unscaledTime - 2.5f;
            while (GunshotSounds.Count > 0 && (GunshotSounds[0].Time < cutoff || GunshotSounds.Count > 96))
                GunshotSounds.RemoveAt(0);
        }
    }

    [HarmonyPatch(typeof(PlayerFireCommand), nameof(PlayerFireCommand.Run))]
    internal static class PlayerFireSoundPatch
    {
        public static void Postfix(byte[] msgData, ServerClient world, byte senderIndex)
        {
            try
            {
                if (msgData == null || msgData.Length < 2 || world == null || world.GameRoomReference == null)
                    return;

                FiringMode mode = (FiringMode)msgData[1];
                TABGPlayerServer shooter = world.GameRoomReference.FindPlayer(senderIndex);
                FakePlayersPlugin.RecordGunshot(shooter, mode);
            }
            catch (Exception ex)
            {
                FakePlayersPlugin.Log($"Gunshot sound patch error: {ex.Message}");
            }
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
