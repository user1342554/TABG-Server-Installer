using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;
using UnityEngine;

namespace TabgInstaller.DevTestMap.Server
{
    [BepInPlugin("tabginstaller.devtestmap.server", "DevTest Map Server", "1.0.0")]
    public sealed class DevTestMapServerPlugin : BaseUnityPlugin
    {
        internal static DevTestMapServerPlugin Instance;
        internal static ConfigEntry<string> RespawnItems;
        internal static ConfigEntry<bool> WaterDamageEnabled;
        internal static ConfigEntry<float> WaterHeight;
        internal static ConfigEntry<float> WaterDamagePerSecond;
        internal static ConfigEntry<float> WaterDamageTickSeconds;
        internal static ConfigEntry<bool> GunGameEnabled;
        internal static ConfigEntry<string> GunGameCastleSpawns;
        internal static ConfigEntry<int> GunGameKillsToWin;
        internal static ConfigEntry<float> SpawnProtectionSeconds;

        private Harmony _harmony;
        private float _lastMatchStartAttempt;
        private readonly HashSet<byte> _playersInWater = new HashSet<byte>();
        private readonly Dictionary<byte, float> _nextWaterDamageAt = new Dictionary<byte, float>();
        private readonly HashSet<byte> _respawnsQueued = new HashSet<byte>();
        private readonly HashSet<byte> _initialLoadoutsQueued = new HashSet<byte>();
        private readonly Dictionary<byte, int> _gunGameStagesGranted = new Dictionary<byte, int>();
        private readonly Dictionary<byte, float> _spawnProtectionEndsAt = new Dictionary<byte, float>();

        private static readonly GunGameLoadout[] GunGameLoadouts =
        {
            new GunGameLoadout("AK2K47", 151, 1, 6, 255, 6, 255, 38, 1),
            new GunGameLoadout("Crossbow", 169, 1, 2, 255, 38, 1),
            new GunGameLoadout("Barret", 322, 1, 1, 255, 38, 1),
            new GunGameLoadout("Mossberg5K", 298, 1, 8, 255),
            new GunGameLoadout("Glockinator", 305, 1, 9, 255, 9, 255),
            new GunGameLoadout("AA12", 292, 1, 8, 255, 38, 1),
            new GunGameLoadout("Burstgun", 155, 1, 6, 255, 6, 255, 38, 1),
            new GunGameLoadout("Missile", 231, 1),
            new GunGameLoadout("Blunder", 293, 1, 4, 255),
            new GunGameLoadout("H1", 158, 1, 6, 255, 6, 255, 38, 1),
            new GunGameLoadout("AutoCrossbow", 163, 1, 2, 255, 38, 1),
            new GunGameLoadout("Rainmaker", 300, 1, 8, 255, 38, 1),
            new GunGameLoadout("UMP", 314, 1, 9, 255, 38, 1),
            new GunGameLoadout("MG42", 220, 1, 6, 255, 6, 255, 38, 1),
            new GunGameLoadout("STG", 161, 1, 6, 255, 38, 1),
            new GunGameLoadout("AUG", 153, 1, 6, 255),
            new GunGameLoadout("M14", 289, 1, 6, 255, 38, 1),
            new GunGameLoadout("VSS", 328, 1, 9, 255, 9, 255, 38, 1),
            new GunGameLoadout("Garand", 287, 1, 1, 255, 38, 1),
            new GunGameLoadout("Minigun", 178, 1, 6, 255, 6, 255, 6, 255, 6, 255),
            new GunGameLoadout("MP40", 309, 1, 9, 255, 38, 1),
            new GunGameLoadout("M16", 160, 1, 6, 255, 38, 1),
            new GunGameLoadout("Kar98", 321, 1, 1, 255, 38, 1),
            new GunGameLoadout("CursedFamas", 157, 1, 6, 255, 38, 1),
            new GunGameLoadout("BeamAR", 154, 1, 9, 255, 9, 255),
            new GunGameLoadout("Z4", 316, 1, 9, 255, 38, 1),
            new GunGameLoadout("Deagle", 279, 1, 6, 255),
            new GunGameLoadout("WindUp", 270, 1, 9, 255),
            new GunGameLoadout("Flintlock", 267, 1, 4, 255),
            new GunGameLoadout("Beretta", 264, 1, 9, 255),
            new GunGameLoadout("Tec9", 313, 1, 9, 255, 9, 255),
            new GunGameLoadout("Holy Revolver", 281, 1, 6, 255),
            new GunGameLoadout("Mac", 304, 1, 9, 255),
            new GunGameLoadout("Luger", 276, 1, 9, 255),
            new GunGameLoadout("Fish", 248, 1),
            new GunGameLoadout("Money", 180, 1),
        };

        private void Awake()
        {
            Instance = this;
            RespawnItems = Config.Bind("DevTestMap", "RespawnItems", string.Empty,
                "Optional items granted after every respawn as itemId:quantity pairs. Leave empty and use F6 to choose items.");
            WaterDamageEnabled = Config.Bind("DevTestMap", "WaterDamageEnabled", true,
                "Apply server-authoritative damage to players inside DevTest water.");
            WaterHeight = Config.Bind("DevTestMap", "WaterHeight", 109f,
                "DevTest water surface Y coordinate. Damage begins one unit above this height, matching TABG swimming detection.");
            WaterDamagePerSecond = Config.Bind("DevTestMap", "WaterDamagePerSecond", 20f,
                "Health removed per second while a player is in water.");
            WaterDamageTickSeconds = Config.Bind("DevTestMap", "WaterDamageTickSeconds", 0.1f,
                "Seconds between water-damage ticks.");
            GunGameEnabled = Config.Bind("GunGame", "Enabled", true,
                "Use the Island Map Gun Game weapon progression.");
            GunGameCastleSpawns = Config.Bind("GunGame", "CastleSpawns",
                "-37,111,-11;6,112,21;-2,111,2",
                "Gun Game spawn points as exact x,y,z positions separated by semicolons. Legacy x,z pairs are ground-raycast.");
            GunGameKillsToWin = Config.Bind("GunGame", "KillsToWin", 32,
                "Individual kills required to win the Island Map Gun Game.");
            SpawnProtectionSeconds = Config.Bind("GunGame", "SpawnProtectionSeconds", 1f,
                "Seconds of server-authoritative damage immunity after each spawn or respawn.");

            _harmony = new Harmony("tabginstaller.devtestmap.server");
            _harmony.PatchAll();
            Logger.LogInfo("[DevTestMap] Server ready: Island Map Gun Game, native DevTest geometry, infinite respawns, water damage, and authoritative item requests.");
        }

        private void Update()
        {
            TickMatchStart();
            TickGunGameLoadouts();
            TickWaterDamage();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _playersInWater.Clear();
            _nextWaterDamageAt.Clear();
            _respawnsQueued.Clear();
            _initialLoadoutsQueued.Clear();
            _gunGameStagesGranted.Clear();
            _spawnProtectionEndsAt.Clear();
            DevTestNetworkPatch.ClearClients();
            Instance = null;
        }

        internal void LogDevTestInfo(string message) => Logger.LogInfo(message);
        internal void LogDevTestWarning(string message) => Logger.LogWarning(message);

        internal void ForgetPlayer(byte playerIndex)
        {
            _playersInWater.Remove(playerIndex);
            _nextWaterDamageAt.Remove(playerIndex);
            _respawnsQueued.Remove(playerIndex);
            _initialLoadoutsQueued.Remove(playerIndex);
            _gunGameStagesGranted.Remove(playerIndex);
            _spawnProtectionEndsAt.Remove(playerIndex);
        }

        internal void ResetEmptyRoomState()
        {
            _playersInWater.Clear();
            _nextWaterDamageAt.Clear();
            _respawnsQueued.Clear();
            _initialLoadoutsQueued.Clear();
            _gunGameStagesGranted.Clear();
            _spawnProtectionEndsAt.Clear();
            _lastMatchStartAttempt = 0f;
        }

        internal void StartSpawnProtection(TABGPlayerServer player)
        {
            if (player == null)
                return;

            var seconds = SpawnProtectionSeconds == null ? 1f : Mathf.Max(0f, SpawnProtectionSeconds.Value);
            if (seconds <= 0f)
            {
                _spawnProtectionEndsAt.Remove(player.PlayerIndex);
                return;
            }

            _spawnProtectionEndsAt[player.PlayerIndex] = Time.unscaledTime + seconds;
            Logger.LogInfo("[DevTestMap] Spawn protection active for " + player.PlayerName +
                " for " + seconds.ToString("0.0", CultureInfo.InvariantCulture) + " seconds.");
        }

        internal bool IsSpawnProtected(TABGPlayerServer player)
        {
            if (player == null)
                return false;

            float endsAt;
            if (!_spawnProtectionEndsAt.TryGetValue(player.PlayerIndex, out endsAt))
                return false;
            if (Time.unscaledTime < endsAt)
                return true;

            _spawnProtectionEndsAt.Remove(player.PlayerIndex);
            return false;
        }

        internal void QueueInitialGunGameLoadout(ServerClient server, TABGPlayerServer player)
        {
            if (server == null || player == null || GunGameEnabled == null || !GunGameEnabled.Value ||
                _gunGameStagesGranted.ContainsKey(player.PlayerIndex) ||
                !_initialLoadoutsQueued.Add(player.PlayerIndex))
                return;

            var playerIndex = player.PlayerIndex;
            server.WaitThenDoAction(0.75f, () =>
            {
                try
                {
                    var room = server.GameRoomReference;
                    if (room == null || room.CurrentGameState != GameState.Started ||
                        room.CurrentGameSettings.GameMode != GameMode.Test || room.Players == null ||
                        !room.Players.Contains(player) || player.IsDead ||
                        !DevTestNetworkPatch.IsCompatibleClient(playerIndex))
                        return;

                    var stage = player.NumberOfKills % GunGameLoadouts.Length;
                    _gunGameStagesGranted[playerIndex] = stage;
                    Logger.LogInfo("[DevTestMap] Clearing join inventory and granting Gun Game stage " +
                        (stage + 1) + "/" + GunGameLoadouts.Length + " to " + player.PlayerName + ": " +
                        GunGameLoadouts[stage].Name + ".");
                    RespawnEntityCommand.Run(server, player, GetCastleSpawnPosition(player.PlayerPosition), byte.MaxValue);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("[DevTestMap] Initial Gun Game loadout failed for " +
                        player.PlayerName + ": " + ex.Message);
                }
                finally
                {
                    _initialLoadoutsQueued.Remove(playerIndex);
                }
            });
        }

        internal void QueueRespawn(ServerClient server, TABGPlayerServer player)
        {
            if (server == null || player == null || !_respawnsQueued.Add(player.PlayerIndex))
                return;

            var playerIndex = player.PlayerIndex;
            Logger.LogInfo("[DevTestMap] Queued respawn for " + player.PlayerName + " in 3 seconds.");
            server.WaitThenDoAction(3f, () =>
            {
                try
                {
                    var room = server.GameRoomReference;
                    if (room == null || room.CurrentGameSettings.GameMode != GameMode.Test ||
                        room.Players == null || !room.Players.Contains(player) || !player.IsDead)
                        return;

                    if (GunGameEnabled.Value)
                        RespawnEntityCommand.Run(server, player, GetCastleSpawnPosition(player.PlayerPosition), byte.MaxValue);
                    else
                        RespawnEntityCommand.Run(server, player);
                    _playersInWater.Remove(playerIndex);
                    _nextWaterDamageAt.Remove(playerIndex);
                    Logger.LogInfo("[DevTestMap] Respawned " + player.PlayerName + ".");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("[DevTestMap] Respawn failed for " + player.PlayerName + ": " + ex.Message);
                }
                finally
                {
                    _respawnsQueued.Remove(playerIndex);
                }
            });
        }

        private void TickMatchStart()
        {
            var server = DevTestNetworkPatch.ActiveServer;
            var room = server?.GameRoomReference;
            if (room == null || room.CurrentGameState != GameState.CountDown ||
                room.CurrentGameSettings.GameMode != GameMode.Test || room.Players == null)
                return;

            var now = Time.unscaledTime;
            if (now - _lastMatchStartAttempt < 0.5f)
                return;

            foreach (var player in room.Players)
            {
                if (player == null || player.PlayerObject == null ||
                    !DevTestNetworkPatch.IsCompatibleClient(player.PlayerIndex))
                    continue;

                _lastMatchStartAttempt = now;
                Logger.LogInfo("[DevTestMap] Starting Test match from the authoritative player-ready fallback.");
                room.CurrentGameMode.RunStart();
                return;
            }
        }

        private void TickWaterDamage()
        {
            if (WaterDamageEnabled == null || !WaterDamageEnabled.Value)
            {
                _playersInWater.Clear();
                _nextWaterDamageAt.Clear();
                return;
            }

            var server = DevTestNetworkPatch.ActiveServer;
            var room = server?.GameRoomReference;
            if (room == null || room.CurrentGameState != GameState.Started ||
                room.CurrentGameSettings.GameMode != GameMode.Test || room.Players == null)
            {
                _playersInWater.Clear();
                _nextWaterDamageAt.Clear();
                return;
            }

            var tickSeconds = Mathf.Max(0.05f, WaterDamageTickSeconds.Value);
            var now = Time.unscaledTime;
            var damage = Mathf.Max(0f, WaterDamagePerSecond.Value) * tickSeconds;
            if (damage <= 0f)
                return;

            var damageLine = WaterHeight.Value + 1f;
            var players = new List<TABGPlayerServer>(room.Players);
            foreach (var player in players)
            {
                if (player == null || player.IsDead || player.InBossFight ||
                    player.PlayerPosition.y >= damageLine)
                {
                    if (player != null)
                    {
                        _playersInWater.Remove(player.PlayerIndex);
                        _nextWaterDamageAt.Remove(player.PlayerIndex);
                    }
                    continue;
                }

                if (_playersInWater.Add(player.PlayerIndex))
                {
                    Logger.LogInfo("[DevTestMap] Water damage started for " + player.PlayerName +
                        " at Y=" + player.PlayerPosition.y.ToString("0.0") + ".");
                    _nextWaterDamageAt[player.PlayerIndex] = now;
                }

                float nextDamageAt;
                if (_nextWaterDamageAt.TryGetValue(player.PlayerIndex, out nextDamageAt) && now < nextDamageAt)
                    continue;

                _nextWaterDamageAt[player.PlayerIndex] = now + tickSeconds;
                ApplyWaterDamage(server, player, damage);
            }
        }

        private void TickGunGameLoadouts()
        {
            if (GunGameEnabled == null || !GunGameEnabled.Value)
            {
                _gunGameStagesGranted.Clear();
                return;
            }

            var server = DevTestNetworkPatch.ActiveServer;
            var room = server?.GameRoomReference;
            if (room == null || room.CurrentGameState != GameState.Started ||
                room.CurrentGameSettings.GameMode != GameMode.Test || room.Players == null)
                return;

            var players = new List<TABGPlayerServer>(room.Players);
            foreach (var player in players)
            {
                if (player == null || player.IsDead || player.InBossFight ||
                    !DevTestNetworkPatch.IsCompatibleClient(player.PlayerIndex))
                    continue;

                var killsToWin = Mathf.Max(1, GunGameKillsToWin.Value);
                if (player.NumberOfKills >= killsToWin)
                {
                    var winner = room.CurrentGameStats?.GetTeam(player.GroupIndex);
                    Logger.LogInfo("[DevTestMap] " + player.PlayerName + " won Gun Game with " +
                        player.NumberOfKills + "/" + killsToWin + " kills.");
                    room.EndMatch(winner);
                    room.ChangeGameState(GameState.Ended);
                    return;
                }

                var stage = player.NumberOfKills % GunGameLoadouts.Length;
                int grantedStage;
                if (!_gunGameStagesGranted.TryGetValue(player.PlayerIndex, out grantedStage))
                {
                    QueueInitialGunGameLoadout(server, player);
                    continue;
                }
                if (grantedStage == stage)
                    continue;

                _gunGameStagesGranted[player.PlayerIndex] = stage;

                Logger.LogInfo("[DevTestMap] Advancing " + player.PlayerName + " to Gun Game stage " +
                    (stage + 1) + "/" + GunGameLoadouts.Length + ": " + GunGameLoadouts[stage].Name + ".");
                RespawnEntityCommand.Run(server, player, player.PlayerPosition, byte.MaxValue);
            }
        }

        private Vector3 GetCastleSpawnPosition(Vector3 fallback)
        {
            var entries = (GunGameCastleSpawns.Value ?? string.Empty).Split(';');
            if (entries.Length == 0)
                return fallback;

            for (var attempt = 0; attempt < entries.Length; attempt++)
            {
                var fields = entries[UnityEngine.Random.Range(0, entries.Length)].Trim().Split(',');
                float x;
                float y;
                float z;
                if (fields.Length == 3 &&
                    float.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                    float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) &&
                    float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                    return new Vector3(x, y, z);

                if (fields.Length != 2 ||
                    !float.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                    !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                    continue;

                RaycastHit hit;
                var ray = new Ray(new Vector3(x, 1000f, z), Vector3.down);
                if (Physics.Raycast(ray, out hit, 10000f, LayerMask.GetMask("Terrain", "Map")))
                    return hit.point + Vector3.up * 10f;

                return new Vector3(x, 145f, z);
            }

            return fallback;
        }

        private void ApplyWaterDamage(ServerClient server, TABGPlayerServer player, float damage)
        {
            try
            {
                if (IsSpawnProtected(player))
                    return;

                if (player.IsDowned || player.Health <= damage)
                {
                    player.UpdateLastAttacker(byte.MaxValue);
                    player.UpdateHealth(0f);
                    server.KillPlayer(player);
                    Logger.LogInfo("[DevTestMap] Water killed " + player.PlayerName + ".");
                    return;
                }

                player.TakeDamage(damage);
                server.DamagePlayer(player);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[DevTestMap] Water damage failed for " + player.PlayerName + ": " + ex.Message);
            }
        }

        internal static void GiveRespawnItems(ServerClient server, IEnumerable<TABGPlayerServer> players)
        {
            var parsed = ParseItems(RespawnItems.Value);
            foreach (var player in players)
            {
                if (player == null)
                    continue;
                foreach (var item in parsed)
                    server.GivePlayerWeapon(player.PlayerIndex, item.Item1, item.Item2);
            }
        }

        internal static void GiveSpawnItems(ServerClient server, IEnumerable<TABGPlayerServer> players)
        {
            if (GunGameEnabled == null || !GunGameEnabled.Value)
            {
                GiveRespawnItems(server, players);
                return;
            }

            foreach (var player in players)
            {
                if (player == null)
                    continue;

                var stage = player.NumberOfKills % GunGameLoadouts.Length;
                var loadout = GunGameLoadouts[stage];
                for (var index = 0; index < loadout.Items.Length; index += 2)
                    server.GivePlayerWeapon(player.PlayerIndex, loadout.Items[index], (byte)loadout.Items[index + 1]);
            }
        }

        private static List<Tuple<int, byte>> ParseItems(string value)
        {
            var result = new List<Tuple<int, byte>>();
            foreach (var token in (value ?? string.Empty).Split(','))
            {
                var fields = token.Trim().Split(':');
                int id;
                byte quantity;
                if (fields.Length == 2 && int.TryParse(fields[0], out id) && byte.TryParse(fields[1], out quantity) && quantity > 0)
                    result.Add(Tuple.Create(id, quantity));
            }
            return result;
        }

        private sealed class GunGameLoadout
        {
            internal readonly string Name;
            internal readonly int[] Items;

            internal GunGameLoadout(string name, params int[] items)
            {
                Name = name;
                Items = items;
            }
        }
    }

    [HarmonyPatch(typeof(ServerClient), "HandleNetorkEvent")]
    internal static class DevTestNetworkPatch
    {
        private static readonly HashSet<byte> CompatibleClients = new HashSet<byte>();
        private static readonly Dictionary<byte, RequestWindow> RequestWindows = new Dictionary<byte, RequestWindow>();
        internal static ServerClient ActiveServer;

        internal static void ClearClients()
        {
            CompatibleClients.Clear();
            RequestWindows.Clear();
            ActiveServer = null;
        }

        internal static bool IsCompatibleClient(byte playerIndex)
        {
            return CompatibleClients.Contains(playerIndex);
        }

        internal static void ResetClientsForEmptyRoom()
        {
            CompatibleClients.Clear();
            RequestWindows.Clear();
        }

        private static bool Prefix(ServerPackage networkEvent, ServerClient __instance)
        {
            ActiveServer = __instance;
            var sender = networkEvent.SenderPlayerID;
            if ((byte)networkEvent.Code == DevTestProtocol.EventCode)
            {
                byte operation;
                int itemId;
                byte quantity;
                if (!DevTestProtocol.TryRead(networkEvent.Buffer, out operation, out itemId, out quantity))
                    return false;

                if (operation == DevTestProtocol.Hello)
                {
                    CompatibleClients.Add(sender);
                    __instance.SendMessageToClients((EventCode)DevTestProtocol.EventCode, DevTestProtocol.CreateAccepted(), sender, true);
                    DevTestMapServerPlugin.Instance?.LogDevTestInfo("[DevTestMap] Accepted compatible client " + sender + ".");
                }
                else if (operation == DevTestProtocol.GiveItem && CompatibleClients.Contains(sender))
                {
                    GiveRequestedItem(__instance, sender, itemId, quantity);
                }
                return false;
            }

            if (networkEvent.Code == EventCode.RoomInit && !CompatibleClients.Contains(sender))
            {
                DevTestMapServerPlugin.Instance?.LogDevTestWarning("[DevTestMap] Rejected client " + sender + " because the DevTest Map client plugin is missing or incompatible.");
                __instance.Server.DisconnectPlayer(sender);
                return false;
            }

            if (networkEvent.Code == EventCode.Leave)
            {
                CompatibleClients.Remove(sender);
                RequestWindows.Remove(sender);
                DevTestMapServerPlugin.Instance?.ForgetPlayer(sender);
            }

            return true;
        }

        private static void Postfix(ServerPackage networkEvent, ServerClient __instance)
        {
            var sender = networkEvent.SenderPlayerID;
            if (networkEvent.Code != EventCode.RequestWorldState || !CompatibleClients.Contains(sender))
                return;

            // The original world-state handler creates the authoritative player object.
            // Start Test and clear/grant the initial loadout only after that object exists.
            var room = __instance.GameRoomReference;
            if (room == null || room.CurrentGameSettings.GameMode != GameMode.Test)
                return;

            if (room.CurrentGameState == GameState.CountDown)
            {
                LandLog.Log("[DevTestMap] Starting Test match after player world initialization.");
                room.CurrentGameMode.RunStart();
            }

            DevTestMapServerPlugin.Instance?.QueueInitialGunGameLoadout(__instance, room.FindPlayer(sender));
        }

        private static void GiveRequestedItem(ServerClient server, byte sender, int itemId, byte quantity)
        {
            if (quantity == 0 || !AllowRequest(sender) || server.GameRoomReference?.FindPlayer(sender) == null)
                return;

            try
            {
                if (!LootDatabase.Instance.HasDataEntry(itemId))
                    return;
                server.GivePlayerWeapon(sender, itemId, quantity);
                DevTestMapServerPlugin.Instance?.LogDevTestInfo("[DevTestMap] Gave item " + itemId + " x" + quantity + " to player " + sender + ".");
            }
            catch (Exception ex)
            {
                DevTestMapServerPlugin.Instance?.LogDevTestWarning("[DevTestMap] Item request failed: " + ex.Message);
            }
        }

        private static bool AllowRequest(byte sender)
        {
            var now = Time.unscaledTime;
            RequestWindow window;
            if (!RequestWindows.TryGetValue(sender, out window) || now - window.StartedAt >= 1f)
            {
                RequestWindows[sender] = new RequestWindow { StartedAt = now, Count = 1 };
                return true;
            }

            window.Count++;
            RequestWindows[sender] = window;
            return window.Count <= 12;
        }

        private struct RequestWindow
        {
            internal float StartedAt;
            internal int Count;
        }
    }

    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.AcceptingPlayers))]
    internal static class KeepDevTestOpenForMultiplayerPatch
    {
        private static void Postfix(GameRoom __instance, ref bool __result)
        {
            if (__instance != null && __instance.CurrentGameSettings.GameMode == GameMode.Test &&
                __instance.CurrentGameState != GameState.Ended &&
                __instance.Players != null &&
                __instance.Players.Count < __instance.CurrentGameSettings.MaxPlayers)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.StartGameRoom))]
    internal static class ClearNativeDevTestWorldLootPatch
    {
        private static void Postfix(GameRoom __instance)
        {
            if (__instance == null || __instance.CurrentGameSettings.GameMode != GameMode.Test ||
                __instance.Weapons == null || __instance.Weapons.Count == 0)
                return;

            var removed = __instance.Weapons.Count;
            __instance.ClearAllWeapons();
            DevTestMapServerPlugin.Instance?.LogDevTestInfo(
                "[DevTestMap] Cleared " + removed +
                " native DevTest world weapons so the initial Login snapshot fits Unity Transport.");
        }
    }

    [HarmonyPatch(typeof(TestGameMode), nameof(TestGameMode.CanApplyDamage))]
    internal static class DevTestSpawnProtectionDamagePatch
    {
        private static bool Prefix(TABGPlayerServer victim, ref bool __result)
        {
            if (DevTestMapServerPlugin.Instance?.IsSpawnProtected(victim) != true)
                return true;

            __result = false;
            return false;
        }
    }

    // Vanilla PlayerFireCommand only relays a shot to players registered as
    // watchers of the shooter's current chunk. DevTest respawns/teleports can
    // leave that interest list stale even though both players are already
    // present on each other's clients, which makes hits work without spawning
    // the remote projectile or tracer. Expand only this command's watcher
    // lookup to every compatible Island Map client. The original command still
    // owns ammo removal, anti-cheat reporting, damage tunnels, and projectile
    // sync-index responses.
    [HarmonyPatch(typeof(PlayerFireCommand), nameof(PlayerFireCommand.Run))]
    internal static class DevTestPlayerFireScopePatch
    {
        internal static ServerClient ActiveWorld;

        private static void Prefix(ServerClient world)
        {
            var room = world?.GameRoomReference;
            ActiveWorld = room != null && room.CurrentGameSettings.GameMode == GameMode.Test
                ? world
                : null;
        }

        private static Exception Finalizer(Exception __exception)
        {
            ActiveWorld = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ServerChunks), nameof(ServerChunks.GetWatchers))]
    internal static class DevTestPlayerFireRecipientsPatch
    {
        private static void Postfix(ref List<TABGPlayerServer> __result)
        {
            var room = DevTestPlayerFireScopePatch.ActiveWorld?.GameRoomReference;
            if (room == null || room.Players == null)
                return;

            if (__result == null)
                __result = new List<TABGPlayerServer>();

            var included = new HashSet<byte>();
            foreach (var watcher in __result)
            {
                if (watcher != null)
                    included.Add(watcher.PlayerIndex);
            }

            foreach (var player in room.Players)
            {
                if (player != null &&
                    DevTestNetworkPatch.IsCompatibleClient(player.PlayerIndex) &&
                    included.Add(player.PlayerIndex))
                    __result.Add(player);
            }
        }
    }

    // Test mode hard-codes a debug gun and ammo grant in its start action. Suppress
    // only those nested vanilla calls; F6 and Gun Game grants happen outside this scope.
    [HarmonyPatch(typeof(TestGameMode), nameof(TestGameMode.RunStart))]
    internal static class SuppressNativeTestLoadoutScopePatch
    {
        internal static bool Active;

        private static void Prefix()
        {
            Active = true;
        }

        private static Exception Finalizer(Exception __exception)
        {
            Active = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.GivePlayerWeapon))]
    internal static class SuppressNativeTestLoadoutGrantPatch
    {
        private static bool Prefix(int lootIndex)
        {
            var suppress = SuppressNativeTestLoadoutScopePatch.Active && (lootIndex == 52 || lootIndex == 5);
            if (suppress)
                DevTestMapServerPlugin.Instance?.LogDevTestInfo(
                    "[DevTestMap] Suppressed native Test debug item " + lootIndex + ".");
            return !suppress;
        }
    }

    [HarmonyPatch(typeof(PlayerDeadDeadBehaviourCommand), "Run")]
    internal static class DevTestDeathRespawnPatch
    {
        private static void Postfix(TABGPlayerServer victimPlayer, ServerClient world)
        {
            var room = world?.GameRoomReference;
            if (room == null || room.CurrentGameSettings.GameMode != GameMode.Test)
                return;

            DevTestMapServerPlugin.Instance?.QueueRespawn(world, victimPlayer);
        }
    }

    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.Terminate),
        new[] { typeof(float), typeof(string) })]
    internal static class KeepDevTestAllocationAlivePatch
    {
        private static bool Prefix(ServerClient __instance, string cause)
        {
            var room = __instance?.GameRoomReference;
            if (room == null || room.CurrentGameSettings.GameMode != GameMode.Test ||
                !string.Equals(cause, TerminateCause.PlayersLeft.ToString(), StringComparison.Ordinal))
                return true;

            DevTestNetworkPatch.ResetClientsForEmptyRoom();
            DevTestMapServerPlugin.Instance?.ResetEmptyRoomState();
            room.Reset();
            DevTestMapServerPlugin.Instance?.LogDevTestInfo(
                "[DevTestMap] Reset empty Test server to WaitingForPlayers while keeping the allocation alive.");
            return false;
        }
    }

    [HarmonyPatch(typeof(TestGameMode), nameof(TestGameMode.CheckGameState))]
    internal static class KeepDevTestMatchRunningPatch
    {
        private static bool Prefix(GameState state)
        {
            return state != GameState.Started;
        }
    }

    [HarmonyPatch(typeof(RespawnEntityCommand), nameof(RespawnEntityCommand.Run),
        new[] { typeof(ServerClient), typeof(List<TABGPlayerServer>), typeof(Vector3), typeof(byte) })]
    internal static class DevTestRespawnLoadoutPatch
    {
        private static void Prefix(List<TABGPlayerServer> players)
        {
            if (players == null)
                return;

            foreach (var player in players)
                DevTestMapServerPlugin.Instance?.StartSpawnProtection(player);
        }

        private static void Postfix(ServerClient world, List<TABGPlayerServer> players)
        {
            if (world == null || players == null || players.Count == 0)
                return;

            var snapshot = new List<TABGPlayerServer>(players);
            world.WaitThenDoAction(0.25f, () => DevTestMapServerPlugin.GiveSpawnItems(world, snapshot));
        }
    }
}
