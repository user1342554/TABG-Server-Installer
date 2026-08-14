using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.CustomGameSkins.Server
{
    [BepInPlugin("tabginstaller.customgameskins.server", "Custom Game All Skins Server", "1.0.0")]
    public sealed class CustomGameSkinsServerPlugin : BaseUnityPlugin
    {
        internal static CustomGameSkinsServerPlugin Instance;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MaxChangesPerSecond;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Enabled = Config.Bind("CustomGameSkins", "Enabled", true,
                "Authorizes compatible clients to use every built-in clothing skin on this custom server.");
            MaxChangesPerSecond = Config.Bind("CustomGameSkins", "MaxChangesPerSecond", 4,
                new ConfigDescription("Maximum accepted outfit changes per player per second.",
                    new AcceptableValueRange<int>(1, 20)));

            _harmony = new Harmony("tabginstaller.customgameskins.server");
            _harmony.PatchAll();
            Logger.LogInfo("[CustomGameSkins] Server authorization ready. Outfit requests are validated against GearDatabase.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            CustomGameSkinsNetworkPatch.ClearState();
            Instance = null;
        }

        internal void LogInfoMessage(string message) => Logger.LogInfo(message);

        internal void LogWarningMessage(string message) => Logger.LogWarning(message);
    }

    [HarmonyPatch(typeof(ServerClient), "HandleNetorkEvent")]
    internal static class CustomGameSkinsNetworkPatch
    {
        private static readonly HashSet<byte> AuthorizedClients = new HashSet<byte>();
        private static readonly Dictionary<byte, RequestWindow> RequestWindows = new Dictionary<byte, RequestWindow>();

        internal static void ClearState()
        {
            AuthorizedClients.Clear();
            RequestWindows.Clear();
        }

        private static bool Prefix(ServerPackage networkEvent, ServerClient __instance)
        {
            var sender = networkEvent.SenderPlayerID;
            if ((byte)networkEvent.Code != CustomGameSkinsProtocol.EventCode)
            {
                if (networkEvent.Code == EventCode.Leave)
                {
                    AuthorizedClients.Remove(sender);
                    RequestWindows.Remove(sender);
                }
                return true;
            }

            byte operation;
            int[] requestedGear;
            byte reason;
            if (!CustomGameSkinsProtocol.TryRead(networkEvent.Buffer, out operation, out requestedGear, out reason))
            {
                SendDenied(__instance, sender, CustomGameSkinsProtocol.DeniedInvalidOutfit);
                return false;
            }

            if (CustomGameSkinsServerPlugin.Enabled == null || !CustomGameSkinsServerPlugin.Enabled.Value)
            {
                SendDenied(__instance, sender, CustomGameSkinsProtocol.DeniedDisabled);
                return false;
            }

            if (operation == CustomGameSkinsProtocol.Hello)
            {
                AuthorizedClients.Add(sender);
                __instance.SendMessageToClients((EventCode)CustomGameSkinsProtocol.EventCode,
                    CustomGameSkinsProtocol.CreateAccepted(), sender, true);
                CustomGameSkinsServerPlugin.Instance?.LogInfoMessage(
                    "[CustomGameSkins] Authorized custom-game wardrobe for client " + sender + ".");
                return false;
            }

            if (operation != CustomGameSkinsProtocol.ApplyOutfit)
                return false;

            if (!AuthorizedClients.Contains(sender))
            {
                SendDenied(__instance, sender, CustomGameSkinsProtocol.DeniedNotAuthorized);
                return false;
            }

            var player = __instance.GameRoomReference?.FindPlayer(sender);
            if (player == null)
            {
                SendDenied(__instance, sender, CustomGameSkinsProtocol.DeniedPlayerNotReady);
                return false;
            }

            if (!AllowRequest(sender))
            {
                SendDenied(__instance, sender, CustomGameSkinsProtocol.DeniedRateLimited);
                return false;
            }

            int[] validatedGear;
            if (!TryValidateOutfit(requestedGear, out validatedGear))
            {
                SendDenied(__instance, sender, CustomGameSkinsProtocol.DeniedInvalidOutfit);
                CustomGameSkinsServerPlugin.Instance?.LogWarningMessage(
                    "[CustomGameSkins] Rejected invalid outfit from client " + sender + ".");
                return false;
            }

            player.UpdateGear(validatedGear);
            __instance.SendMessageToClients(EventCode.GearChange,
                CreateVanillaGearChange(sender, validatedGear), byte.MaxValue, true);
            __instance.SendMessageToClients((EventCode)CustomGameSkinsProtocol.EventCode,
                CustomGameSkinsProtocol.CreateOutfitApplied(validatedGear), sender, true);
            CustomGameSkinsServerPlugin.Instance?.LogInfoMessage(
                "[CustomGameSkins] Applied validated outfit for client " + sender + ".");
            return false;
        }

        private static bool TryValidateOutfit(int[] requested, out int[] validated)
        {
            validated = null;
            if (requested == null || requested.Length != CustomGameSkinsProtocol.GearValueCount)
                return false;

            try
            {
                var database = GearDatabase.Instance;
                if (database == null)
                    return false;

                var colors = database.Colors;
                var colorCount = colors == null ? 0 : colors.Length;
                var result = new int[CustomGameSkinsProtocol.GearValueCount];
                for (var slot = 0; slot < 6; slot++)
                {
                    var itemIndex = requested[slot * 2];
                    var colorIndex = requested[(slot * 2) + 1];
                    if (itemIndex == -1)
                    {
                        result[slot * 2] = -1;
                        result[(slot * 2) + 1] = -1;
                        continue;
                    }

                    if (itemIndex < 0 || colorIndex < -1 || colorIndex >= colorCount)
                        return false;

                    var entry = database.GetDataEntry(itemIndex);
                    if (entry.m_gear == null || entry.m_gear.GearT != ItemTypeForSlot((Gear.GearType)slot))
                        return false;

                    result[slot * 2] = itemIndex;
                    result[(slot * 2) + 1] = colorIndex;
                }

                validated = result;
                return true;
            }
            catch (Exception ex)
            {
                CustomGameSkinsServerPlugin.Instance?.LogWarningMessage(
                    "[CustomGameSkins] Outfit validation failed: " + ex.Message);
                return false;
            }
        }

        private static Gear.GearType ItemTypeForSlot(Gear.GearType slot)
        {
            if (slot == Gear.GearType.ARMOR)
                return Gear.GearType.TORSO;
            if (slot == Gear.GearType.HELMET)
                return Gear.GearType.HEAD;
            return slot;
        }

        private static byte[] CreateVanillaGearChange(byte playerIndex, int[] gear)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(playerIndex);
                writer.Write(gear.Length);
                for (var i = 0; i < gear.Length; i++)
                    writer.Write(gear[i]);
                return stream.ToArray();
            }
        }

        private static void SendDenied(ServerClient server, byte recipient, byte reason)
        {
            server.SendMessageToClients((EventCode)CustomGameSkinsProtocol.EventCode,
                CustomGameSkinsProtocol.CreateDenied(reason), recipient, true);
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
            return window.Count <= Math.Max(1, CustomGameSkinsServerPlugin.MaxChangesPerSecond?.Value ?? 4);
        }

        private struct RequestWindow
        {
            internal float StartedAt;
            internal int Count;
        }
    }
}
