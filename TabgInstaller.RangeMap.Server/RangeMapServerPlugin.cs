using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;
using UnityEngine;

namespace TabgInstaller.RangeMap.Server
{
    [BepInPlugin("tabginstaller.rangemap.server", "Range Map Server", "1.0.0")]
    public sealed class RangeMapServerPlugin : BaseUnityPlugin
    {
        internal static RangeMapServerPlugin Instance;
        internal static ConfigEntry<float> SpawnX;
        internal static ConfigEntry<float> SpawnY;
        internal static ConfigEntry<float> SpawnZ;
        internal static ConfigEntry<float> SpawnRotation;
        internal static ConfigEntry<string> RespawnItems;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            SpawnX = Config.Bind("RangeMap", "SpawnX", 6.32f, "Range respawn X coordinate.");
            SpawnY = Config.Bind("RangeMap", "SpawnY", 117.10f, "Range respawn Y coordinate.");
            SpawnZ = Config.Bind("RangeMap", "SpawnZ", -25.41f, "Range respawn Z coordinate.");
            SpawnRotation = Config.Bind("RangeMap", "SpawnRotation", 0f, "Range respawn facing angle.");
            RespawnItems = Config.Bind("RangeMap", "RespawnItems", "52:1,5:255",
                "Items granted after every respawn as itemId:quantity pairs. The F6 client menu can request every other item.");

            _harmony = new Harmony("tabginstaller.rangemap.server");
            _harmony.PatchAll();
            Logger.LogInfo("[RangeMap] Server ready: WilhelmTest geometry, infinite respawns, and authoritative item requests.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            RangeNetworkPatch.ClearClients();
            Instance = null;
        }

        internal static Vector3 SpawnPosition => new Vector3(SpawnX.Value, SpawnY.Value, SpawnZ.Value);

        internal void LogRangeInfo(string message) => Logger.LogInfo(message);
        internal void LogRangeWarning(string message) => Logger.LogWarning(message);

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
    }

    [HarmonyPatch(typeof(ServerClient), "HandleNetorkEvent")]
    internal static class RangeNetworkPatch
    {
        private static readonly HashSet<byte> CompatibleClients = new HashSet<byte>();
        private static readonly Dictionary<byte, RequestWindow> RequestWindows = new Dictionary<byte, RequestWindow>();

        internal static void ClearClients()
        {
            CompatibleClients.Clear();
            RequestWindows.Clear();
        }

        private static bool Prefix(ServerPackage networkEvent, ServerClient __instance)
        {
            var sender = networkEvent.SenderPlayerID;
            if ((byte)networkEvent.Code == RangeProtocol.EventCode)
            {
                byte operation;
                int itemId;
                byte quantity;
                if (!RangeProtocol.TryRead(networkEvent.Buffer, out operation, out itemId, out quantity))
                    return false;

                if (operation == RangeProtocol.Hello)
                {
                    CompatibleClients.Add(sender);
                    __instance.SendMessageToClients((EventCode)RangeProtocol.EventCode, RangeProtocol.CreateAccepted(), sender, true);
                    RangeMapServerPlugin.Instance?.LogRangeInfo("[RangeMap] Accepted compatible client " + sender + ".");
                }
                else if (operation == RangeProtocol.GiveItem && CompatibleClients.Contains(sender))
                {
                    GiveRequestedItem(__instance, sender, itemId, quantity);
                }
                return false;
            }

            if (networkEvent.Code == EventCode.RoomInit && !CompatibleClients.Contains(sender))
            {
                RangeMapServerPlugin.Instance?.LogRangeWarning("[RangeMap] Rejected client " + sender + " because the Range Map client plugin is missing or incompatible.");
                __instance.Server.DisconnectPlayer(sender);
                return false;
            }

            if (networkEvent.Code == EventCode.Leave)
            {
                CompatibleClients.Remove(sender);
                RequestWindows.Remove(sender);
            }

            return true;
        }

        private static void Postfix(ServerPackage networkEvent, ServerClient __instance)
        {
            var sender = networkEvent.SenderPlayerID;
            if (networkEvent.Code != EventCode.RequestWorldState || !CompatibleClients.Contains(sender))
                return;

            // CurrentGameWorldCommand (the original handler) creates the player's
            // network object. Start only after it returns so Test's starter items
            // have a valid authoritative player object to target.
            var room = __instance.GameRoomReference;
            if (room == null || room.CurrentGameState != GameState.CountDown ||
                room.CurrentGameSettings.GameMode != GameMode.Test)
                return;

            LandLog.Log("[RangeMap] Starting Test match after player world initialization.");
            room.CurrentGameMode.RunStart();
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
                RangeMapServerPlugin.Instance?.LogRangeInfo("[RangeMap] Gave item " + itemId + " x" + quantity + " to player " + sender + ".");
            }
            catch (Exception ex)
            {
                RangeMapServerPlugin.Instance?.LogRangeWarning("[RangeMap] Item request failed: " + ex.Message);
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

    [HarmonyPatch(typeof(TestGameMode), nameof(TestGameMode.GetNewSpawnPoint))]
    internal static class RangeSpawnPatch
    {
        private static bool Prefix(ref SpawnPointWrapper __result)
        {
            __result = new SpawnPointWrapper(RangeMapServerPlugin.SpawnPosition, RangeMapServerPlugin.SpawnRotation.Value);
            return false;
        }
    }

    [HarmonyPatch(typeof(TestGameMode), nameof(TestGameMode.CheckGameState))]
    internal static class KeepRangeMatchRunningPatch
    {
        private static bool Prefix(GameState state)
        {
            return state != GameState.Started;
        }
    }

    [HarmonyPatch(typeof(RespawnEntityCommand), nameof(RespawnEntityCommand.Run),
        new[] { typeof(ServerClient), typeof(List<TABGPlayerServer>), typeof(Vector3), typeof(byte) })]
    internal static class RespawnLoadoutPatch
    {
        private static void Postfix(ServerClient world, List<TABGPlayerServer> players)
        {
            if (world == null || players == null || players.Count == 0)
                return;

            var snapshot = new List<TABGPlayerServer>(players);
            world.WaitThenDoAction(0.25f, () => RangeMapServerPlugin.GiveRespawnItems(world, snapshot));
        }
    }
}
