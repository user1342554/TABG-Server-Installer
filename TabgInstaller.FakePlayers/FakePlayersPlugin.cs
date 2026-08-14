using System;
using System.Collections.Generic;
using System.IO;
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

        internal struct TeamMoveOrder
        {
            public int Sequence;
            public byte SenderIndex;
            public Vector3 Position;
            public bool IsPing;
            public float CreatedAt;

            public TeamMoveOrder(int sequence, byte senderIndex, Vector3 position, bool isPing, float createdAt)
            {
                Sequence = sequence;
                SenderIndex = senderIndex;
                Position = position;
                IsPing = isPing;
                CreatedAt = createdAt;
            }
        }

        internal static readonly List<byte> FakeIndices = new List<byte>();
        internal static readonly List<byte> AiIndices = new List<byte>();
        internal static readonly List<GunshotSoundEvent> GunshotSounds = new List<GunshotSoundEvent>();
        private static readonly Dictionary<byte, TeamMoveOrder> TeamMoveOrders = new Dictionary<byte, TeamMoveOrder>();
        private static readonly Dictionary<byte, int> PendingAiLevels = new Dictionary<byte, int>();
        private const float AutoSpawnInitialDelaySeconds = 25.0f;
        private const int AutoSpawnMaxReadinessAttempts = 60;
        private const float AutoSpawnRetryDelaySeconds = 1.0f;
        private static readonly string[] BotFirstNames =
        {
            "Alex", "Amelia", "Ben", "Charlotte", "Daniel", "Elias", "Emma", "Felix",
            "Finn", "Hannah", "Henry", "Isabella", "Jack", "Jannik", "Jonah", "Julia",
            "Kai", "Lara", "Laura", "Lea", "Leo", "Leon", "Liam", "Lina", "Luca",
            "Lucas", "Maja", "Marie", "Mia", "Mila", "Noah", "Nora", "Oliver", "Oscar",
            "Paul", "Sophie", "Theo", "Tom", "Victoria", "Zoe"
        };
        private static readonly string[] BotLastNames =
        {
            "Bauer", "Becker", "Fischer", "Hartmann", "Hoffmann", "Keller", "Klein",
            "Koch", "Krause", "Kruger", "Lehmann", "Meyer", "Neumann", "Richter",
            "Schmidt", "Schneider", "Schulz", "Vogel", "Wagner", "Weber", "Werner", "Wolf"
        };
        private static int _nextNumber = 1;
        private static int _nextTeamMoveOrderSequence = 1;
        private static bool _loggedCosmeticDatabase;
        internal static int GunshotSoundSequence { get; private set; }
        internal static ConfigEntry<int> MaxFakeSpawnCount;
        internal static ConfigEntry<int> MaxAiSpawnCount;
        internal static ConfigEntry<int> AutoSpawnAiCount;
        internal static ConfigEntry<int> AutoSpawnAiLevel;
        internal static ConfigEntry<int> CommandPermissionLevel;
        internal static ConfigEntry<bool> DevelopmentMode;
        internal static ConfigEntry<bool> CommandsUsableByEveryone;
        private static bool _autoSpawnQueued;
        private static bool _autoSpawnCompleted;
        private Harmony _harmony;
        private Harmony _permissionHarmony;

        private void Awake()
        {
            MaxFakeSpawnCount = Config.Bind("Commands", "MaxFakeSpawnCount", 200, "Maximum fake players spawned by one /spawndummy command.");
            MaxAiSpawnCount = Config.Bind("Commands", "MaxAiSpawnCount", 32, "Maximum AI dummy players spawned by one /spawnaidummy command.");
            AutoSpawnAiCount = Config.Bind("AutoSpawn", "AiCount", 0, "AI dummy players to spawn automatically when the game room is ready. Set to 0 to disable.");
            AutoSpawnAiLevel = Config.Bind("AutoSpawn", "AiLevel", 3, "Skill level used for automatically spawned AI dummy players, from 1 to 5.");
            CommandPermissionLevel = Config.Bind("Commands", "CommandPermissionLevel", 2, "Citrus permission level required for FakePlayers commands in normal release mode.");
            DevelopmentMode = Config.Bind("Safety", "DevelopmentMode", false, "Explicitly mark this server as a private development/test server. Required before test-only permission bypass can activate.");
            CommandsUsableByEveryone = Config.Bind("Safety", "CommandsUsableByEveryone", false, "Development-only: bypass Citrus permissions for FakePlayers commands. Ignored unless Safety.DevelopmentMode is true.");

            Instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Logger.LogInfo("[FakePlayers] Loaded.");
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this))
            {
                _permissionHarmony?.UnpatchSelf();
                _harmony?.UnpatchSelf();
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
                if (DevelopmentMode.Value && CommandsUsableByEveryone.Value)
                {
                    PatchPermissions();
                }
                else if (CommandsUsableByEveryone.Value)
                {
                    Logger.LogWarning("[FakePlayers] CommandsUsableByEveryone is ignored because Safety.DevelopmentMode is false.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[FakePlayers] Citruslib integration failed: {ex.Message}");
            }
        }

        private void RegisterCommands()
        {
            int commandPermission = Math.Max(0, CommandPermissionLevel.Value);

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
            }, "FakePlayers", "Spawn fake players", "[count]", commandPermission);

            Citrus.AddCommand("spawnaidummy", spawnAiCommand, "FakePlayers", "Spawn AI dummy players", "[count] [level 1-5]", commandPermission);
            Citrus.AddCommand("aidummy", spawnAiCommand, "FakePlayers", "Spawn AI dummy players", "[count] [level 1-5]", commandPermission);
            Citrus.AddCommand("spawnai", spawnAiCommand, "FakePlayers", "Spawn AI dummy players", "[count] [level 1-5]", commandPermission);

            Citrus.AddCommand("removedummy", (string[] prms, TABGPlayerServer player) =>
            {
                var server = ResolveServer();
                if (server == null) { Citrus.SelfParrot(player, "Server not ready."); return; }

                int count = 0; // 0 = all
                if (prms.Length > 0 && !int.TryParse(prms[0], out count))
                    count = 0;

                int removed = RemoveFakePlayers(server, count);
                Citrus.SelfParrot(player, $"Removed {removed}. Remaining: {FakeIndices.Count}");
            }, "FakePlayers", "Remove fake players", "[count]", commandPermission);

            Citrus.AddCommand("dummycount", (string[] prms, TABGPlayerServer player) =>
            {
                Citrus.SelfParrot(player, $"Active fake players: {FakeIndices.Count}");
            }, "FakePlayers", "Show fake player count", "", commandPermission);

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
            }, "FakePlayers", "Inspect one AI dummy", "[index|name]", commandPermission);

            Logger.LogInfo("[FakePlayers] Commands registered: /spawndummy, /spawnaidummy, /aidummy, /spawnai, /removedummy, /dummycount, /inspectbot");
            if (commandPermission <= 0)
                Logger.LogWarning("[FakePlayers] CommandPermissionLevel is 0; FakePlayers commands are available to everyone.");
        }

        /// <summary>
        /// Patches Citruslib's internal Command.Run to skip the permission check
        /// only for commands registered by this plugin, and only in explicit dev mode.
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
            _permissionHarmony = new Harmony(PluginGuid + ".perms");
            _permissionHarmony.Patch(runMethod, prefix: prefix);
            Logger.LogWarning("[FakePlayers] Development permission bypass applied for FakePlayers commands only.");
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
                TABGPlayerServer player = room.FindPlayer(idx);
                ForgetFakePlayer(idx);
                if (!LooksLikeTrackedFakePlayer(player)) continue;

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
                TABGPlayerServer player = room.FindPlayer(FakeIndices[i]);
                if (!LooksLikeTrackedFakePlayer(player))
                    ForgetFakePlayer(FakeIndices[i]);
            }
        }

        internal static bool IsTrackedFakePlayer(GameRoom room, byte playerIndex)
        {
            if (room == null || !FakeIndices.Contains(playerIndex))
                return false;

            return LooksLikeTrackedFakePlayer(room.FindPlayer(playerIndex));
        }

        internal static bool IsTrackedFakePlayer(TABGPlayerServer player)
        {
            return player != null &&
                FakeIndices.Contains(player.PlayerIndex) &&
                LooksLikeTrackedFakePlayer(player);
        }

        internal static bool IsTrackedAiPlayer(GameRoom room, byte playerIndex)
        {
            if (room == null || !AiIndices.Contains(playerIndex))
                return false;

            return LooksLikeTrackedFakePlayer(room.FindPlayer(playerIndex));
        }

        internal static bool IsTrackedAiPlayer(TABGPlayerServer player)
        {
            return player != null &&
                AiIndices.Contains(player.PlayerIndex) &&
                LooksLikeTrackedFakePlayer(player);
        }

        private static bool LooksLikeTrackedFakePlayer(TABGPlayerServer player)
        {
            return player != null && player.Bot;
        }

        internal static void ResetStaticMatchState()
        {
            FakeIndices.Clear();
            AiIndices.Clear();
            PendingAiLevels.Clear();
            GunshotSounds.Clear();
            TeamMoveOrders.Clear();
            GunshotSoundSequence = 0;
            _nextNumber = 1;
            _nextTeamMoveOrderSequence = 1;
            _loggedCosmeticDatabase = false;
            _autoSpawnQueued = false;
            _autoSpawnCompleted = false;
            ServerMessages.ResetTransientState();
        }

        internal static void QueueAutoSpawn(ServerClient server)
        {
            if (_autoSpawnQueued || _autoSpawnCompleted || server == null || AutoSpawnAiCount == null)
                return;

            int count = Math.Max(0, Math.Min(AutoSpawnAiCount.Value, MaxAiSpawnCount.Value));
            if (count <= 0)
                return;

            _autoSpawnQueued = true;
            int level = Mathf.Clamp(AutoSpawnAiLevel?.Value ?? 3, 1, 5);
            Log($"Auto-spawn AI queued: {count} AI dummy player(s), level {level}.");
            ScheduleAutoSpawnAttempt(server, count, level, 0, AutoSpawnInitialDelaySeconds);
        }

        private static void ScheduleAutoSpawnAttempt(ServerClient server, int count, int level, int attempt, float delaySeconds)
        {
            server.WaitThenDoAction(delaySeconds, () =>
            {
                RunAutoSpawnAttempt(server, count, level, attempt);
            });
        }

        private static void RunAutoSpawnAttempt(ServerClient queuedServer, int count, int level, int attempt)
        {
            try
            {
                var currentServer = ResolveServer() ?? queuedServer;
                if (currentServer == null || currentServer.GameRoomReference == null)
                {
                    if (attempt < AutoSpawnMaxReadinessAttempts)
                    {
                        ScheduleAutoSpawnAttempt(queuedServer, count, level, attempt + 1, AutoSpawnRetryDelaySeconds);
                        return;
                    }

                    _autoSpawnQueued = false;
                    Log("Auto-spawn AI skipped: server room not ready after waiting.");
                    return;
                }

                if (!IsRoomReadyForAutoSpawn(currentServer.GameRoomReference))
                {
                    if (attempt < AutoSpawnMaxReadinessAttempts)
                    {
                        ScheduleAutoSpawnAttempt(queuedServer, count, level, attempt + 1, AutoSpawnRetryDelaySeconds);
                        return;
                    }

                    _autoSpawnQueued = false;
                    Log("Auto-spawn AI skipped: game room did not finish initialization.");
                    return;
                }

                int spawned = SpawnFakePlayers(currentServer, count, aiControlled: true, aiLevel: level);
                _autoSpawnCompleted = spawned > 0;
                _autoSpawnQueued = false;
                Log($"Auto-spawned {spawned} AI dummy player(s), level {level}.");
            }
            catch (Exception ex)
            {
                _autoSpawnQueued = false;
                Log($"Auto-spawn AI failed: {ex.Message}");
            }
        }

        private static bool IsRoomReadyForAutoSpawn(GameRoom room)
        {
            return room != null &&
                room.Players != null &&
                room.CurrentGameSettings.MaxPlayers > 0 &&
                room.CurrentGameMode != null;
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
            bool joinsAnchorTeam = CanJoinAnchorTeam(room, anchorPlayer);
            byte groupIndex = joinsAnchorTeam
                ? anchorPlayer.GroupIndex
                : room.GetNewGroupIndex(loginKey, playerIndex);
            string name = CreateHumanBotName(room, number);
            int[] gearData = CreateRandomCosmeticLoadout();

            var player = new TABGPlayerServer(
                name, playerIndex, groupIndex, loginKey,
                null, 0, gearData,
                room.CurrentGameSettings.MaxPlayers,
                admin: false, bot: true);

            room.AddPlayer(player, wantsToBeAlone: !joinsAnchorTeam);
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
                string teamNote = joinsAnchorTeam ? $"; teammate of {anchorPlayer.PlayerName} in group {groupIndex}" : string.Empty;
                Log($"AI dummy {name} level {PendingAiLevels[playerIndex]} queued at {pos}{teamNote}; cosmetics [{string.Join(",", gearData)}].");
            }

            return playerIndex;
        }

        private static bool CanJoinAnchorTeam(GameRoom room, TABGPlayerServer anchorPlayer)
        {
            if (room == null || anchorPlayer == null || anchorPlayer.IsDead || anchorPlayer.Bot || room.CurrentGameStats == null)
                return false;

            TeamStanding team = room.CurrentGameStats.GetTeam(anchorPlayer.GroupIndex);
            return team != null &&
                team.GetNumberOfPlayersInTeam(withBookings: true) < room.CurrentGameSettings.MaxTeamSize;
        }

        private static string CreateHumanBotName(GameRoom room, int fallbackNumber)
        {
            for (int attempt = 0; attempt < 32; attempt++)
            {
                string candidate = BotFirstNames[UnityEngine.Random.Range(0, BotFirstNames.Length)] + " " +
                    BotLastNames[UnityEngine.Random.Range(0, BotLastNames.Length)];
                bool alreadyUsed = false;
                for (int i = 0; room.Players != null && i < room.Players.Count; i++)
                {
                    TABGPlayerServer existing = room.Players[i];
                    if (existing != null && string.Equals(existing.PlayerName, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyUsed = true;
                        break;
                    }
                }

                if (!alreadyUsed)
                    return candidate;
            }

            return BotFirstNames[fallbackNumber % BotFirstNames.Length] + " " + fallbackNumber;
        }

        private static int[] CreateRandomCosmeticLoadout()
        {
            try
            {
                GearDatabase database = GearDatabase.Instance;
                if (database == null)
                    throw new InvalidOperationException("Gear Database resource is unavailable");

                var head = database.GetGearList(Gear.GearType.HEAD);
                var torso = database.GetGearList(Gear.GearType.TORSO);
                var legs = database.GetGearList(Gear.GearType.LEGS);
                var feet = database.GetGearList(Gear.GearType.FEET);
                int colorCount = database.Colors != null ? database.Colors.Length : 0;

                if (!_loggedCosmeticDatabase)
                {
                    _loggedCosmeticDatabase = true;
                    Log($"Cosmetic database ready: head={head.Count}, torso={torso.Count}, legs={legs.Count}, feet={feet.Count}, colors={colorCount}.");
                }

                if (head.Count == 0 || torso.Count == 0 || legs.Count == 0 || feet.Count == 0)
                    throw new InvalidOperationException("one or more cosmetic categories are empty");

                return new[]
                {
                    RandomGearIndex(head), RandomColorIndex(database, Gear.GearType.HEAD, colorCount),
                    RandomGearIndex(torso), RandomColorIndex(database, Gear.GearType.TORSO, colorCount),
                    RandomGearIndex(legs), RandomColorIndex(database, Gear.GearType.LEGS, colorCount),
                    RandomGearIndex(feet), RandomColorIndex(database, Gear.GearType.FEET, colorCount)
                };
            }
            catch (Exception ex)
            {
                if (!_loggedCosmeticDatabase)
                {
                    _loggedCosmeticDatabase = true;
                    Log($"Random cosmetics unavailable; using the vanilla fallback item: {ex.GetType().Name}: {ex.Message}");
                }

                // Vanilla SpawnBotCommand uses gear index 2. Supplying its color pair
                // makes the normal client actually consume the item.
                return new[] { 2, -1 };
            }
        }

        private static int RandomGearIndex(List<GearDataEntry> entries)
        {
            Gear gear = entries[UnityEngine.Random.Range(0, entries.Count)].m_gear;
            return gear != null ? gear.Index : -1;
        }

        private static int RandomColorIndex(GearDatabase database, Gear.GearType gearType, int colorCount)
        {
            if (colorCount <= 0 || UnityEngine.Random.value < 0.2f)
                return -1;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                int color = UnityEngine.Random.Range(0, colorCount);
                if (gearType != Gear.GearType.HEAD || database.m_bannedHeadColors == null ||
                    Array.IndexOf(database.m_bannedHeadColors, color) < 0)
                    return color;
            }

            return -1;
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
            if (server == null || !IsCombatTargetAlive(attacker) || !IsCombatTargetAlive(target))
                return;

            damage = Mathf.Max(0f, damage);
            if (damage <= 0f)
                return;

            float newHealth = Mathf.Max(0f, target.Health - damage);
            if (newHealth <= 0f)
            {
                ApplyLethalDamage(server, attacker, target);
                return;
            }

            byte[] damageCommand = ServerMessages.MakeDamageCommand(attacker, target, newHealth);

            // Server-side fake attackers are not real chunk watchers, so report through the victim path.
            PlayerDamageCommand.Run(damageCommand, server, target.PlayerIndex);
        }

        internal static void ApplyEnvironmentDamage(ServerClient server, TABGPlayerServer target, float damage, string source)
        {
            GameRoom room = server?.GameRoomReference;
            if (room?.CurrentGameMode == null || !IsCombatTargetAlive(target))
                return;

            damage = Mathf.Max(0f, damage);
            if (damage <= 0f)
                return;

            float newHealth = Mathf.Max(0f, target.Health - damage);
            if (newHealth > 0f)
            {
                target.UpdateHealth(newHealth);
                ServerMessages.SendHealthStateChanged(server, target, newHealth);
                return;
            }

            try
            {
                // Bots have no client that can complete the Gulag. Consume that path
                // before killing them so they cannot remain alive as boss spectators.
                if (target.Bot && !target.HasDoneBossFight)
                {
                    target.EnterBoss();
                    target.ExitBoss();
                }

                target.UpdateLastAttacker(byte.MaxValue);
                target.UpdateHealth(0f);
                room.CurrentGameMode.KillPlayer(target, null);
                room.CheckGameState();
                Log($"AI dummy {target.PlayerName} died {source}.");
            }
            catch (Exception ex)
            {
                Log($"Error applying environmental damage to {target.PlayerName}: {ex.Message}");
            }
        }

        internal static bool IsCombatTargetAlive(TABGPlayerServer player)
        {
            return player != null &&
                !player.IsDead &&
                !player.IsDowned &&
                player.Health > 0f &&
                player.HasDropped;
        }

        private static void ApplyLethalDamage(ServerClient server, TABGPlayerServer attacker, TABGPlayerServer target)
        {
            GameRoom room = server?.GameRoomReference;
            if (room?.CurrentGameMode == null || attacker == null || target == null || target.IsDead)
                return;

            try
            {
                target.UpdateLastAttacker(attacker.PlayerIndex);
                target.UpdateHealth(0f);
                room.CurrentGameMode.KillPlayer(target, attacker);
                room.CheckGameState();
                Log($"AI dummy {attacker.PlayerName} killed {target.PlayerName}.");
            }
            catch (Exception ex)
            {
                Log($"Error applying lethal AI damage to {target.PlayerName}: {ex.Message}");
            }
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
            if (shooter == null)
                return;

            bool trackedAi = IsTrackedAiPlayer(shooter);
            if (shooter.Bot && !trackedAi)
                return;
            if (IsTrackedFakePlayer(shooter) && !trackedAi)
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

        internal static void RecordTeamMarker(ServerClient server, byte[] msgData)
        {
            GameRoom room = server != null ? server.GameRoomReference : null;
            if (room == null || msgData == null || msgData.Length < 2)
                return;

            TABGPlayerServer sender = room.FindPlayer(msgData[0]);
            if (sender == null || sender.Bot)
                return;

            byte action = msgData[1];
            TeamMoveOrder previous;
            if (action == 1)
            {
                if (TeamMoveOrders.TryGetValue(sender.GroupIndex, out previous) && previous.SenderIndex == sender.PlayerIndex)
                {
                    TeamMoveOrders.Remove(sender.GroupIndex);
                    Log($"Cleared teammate move order from {sender.PlayerName} for group {sender.GroupIndex}.");
                }
                return;
            }

            if (action != 0 || msgData.Length < 27)
                return;

            using (var stream = new MemoryStream(msgData, writable: false))
            using (var reader = new BinaryReader(stream))
            {
                reader.ReadByte();
                reader.ReadByte();
                Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                bool isPing = (MarkerType)reader.ReadByte() == MarkerType.Ping;

                if (!IsFinite(position))
                    return;

                var order = new TeamMoveOrder(
                    _nextTeamMoveOrderSequence++,
                    sender.PlayerIndex,
                    position,
                    isPing,
                    Time.unscaledTime);
                TeamMoveOrders[sender.GroupIndex] = order;
                Log($"Teammate {(isPing ? "ping" : "map marker")} from {sender.PlayerName} set group {sender.GroupIndex} move order at {position}.");
            }
        }

        internal static bool TryGetTeamMoveOrder(GameRoom room, byte groupIndex, out TeamMoveOrder order)
        {
            if (!TeamMoveOrders.TryGetValue(groupIndex, out order))
                return false;

            TABGPlayerServer sender = room != null ? room.FindPlayer(order.SenderIndex) : null;
            bool expiredPing = order.IsPing && Time.unscaledTime - order.CreatedAt > 18f;
            if (sender == null || sender.Bot || sender.GroupIndex != groupIndex || expiredPing)
            {
                TeamMoveOrders.Remove(groupIndex);
                order = default(TeamMoveOrder);
                return false;
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
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
    /// Prefix patch for CitrusLib.Command.Run. In explicit development mode it skips
    /// the permission check for commands owned by this plugin only.
    /// </summary>
    internal static class PermBypassPatch
    {
        public static bool Prefix(object __instance, ref bool __result, string[] prms, TABGPlayerServer player)
        {
            try
            {
                var modNameField = AccessTools.Field(__instance.GetType(), "modName");
                var modName = modNameField?.GetValue(__instance) as string;
                if (!string.Equals(modName, "FakePlayers", StringComparison.OrdinalIgnoreCase))
                    return true;

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
        public static bool Prefix(ServerClient __instance, ref byte[] recipents)
        {
            if (recipents == null || recipents.Length == 0 || FakePlayersPlugin.FakeIndices.Count == 0)
                return true;

            if (recipents.Length == 1 && recipents[0] == byte.MaxValue)
                return true;

            GameRoom room = __instance != null ? __instance.GameRoomReference : null;
            if (room == null)
                return true;

            FakePlayersPlugin.PruneMissingFakePlayers(room);
            if (FakePlayersPlugin.FakeIndices.Count == 0)
                return true;

            int write = 0;
            for (int i = 0; i < recipents.Length; i++)
            {
                if (!FakePlayersPlugin.IsTrackedFakePlayer(room, recipents[i]))
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

    [HarmonyPatch]
    internal static class FakePlayerDeathLootPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DropAllLootCommand), "Run", new[] { typeof(ServerClient), typeof(List<TABGPlayerServer>) });
        }

        private static void Prefix(List<TABGPlayerServer> players)
        {
            if (players == null)
                return;

            for (int i = 0; i < players.Count; i++)
            {
                TABGPlayerServer player = players[i];
                AiDummyController controller = player?.PlayerObject != null
                    ? player.PlayerObject.GetComponent<AiDummyController>()
                    : null;
                controller?.SyncDeathLoot();
            }
        }
    }
}
