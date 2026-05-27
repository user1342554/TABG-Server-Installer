using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using CitrusLib;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Server
{
    [BepInPlugin("tabginstaller.proximitychat.server", "Proximity Voice Chat Server", "1.0.0")]
    [BepInDependency("com.cyrusthelesser.citruslib", BepInDependency.DependencyFlags.SoftDependency)]
    public class ProximityChatServerPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> MaxRange;
        public static ConfigEntry<float> MinRange;
        public static ConfigEntry<string> FalloffCurve;

        private Harmony _harmony;

        internal static ProximityChatServerPlugin Instance;

        private void Awake()
        {
            Instance = this;
            MaxRange = Config.Bind("ProximityChat", "MaxRange", 50f, "Distance beyond which audio is not relayed");
            MinRange = Config.Bind("ProximityChat", "MinRange", 5f, "Distance within which audio is full volume");
            FalloffCurve = Config.Bind("ProximityChat", "FalloffCurve", "Linear", "Volume falloff: Linear or Logarithmic");

            _harmony = new Harmony("tabginstaller.proximitychat.server");
            _harmony.PatchAll(typeof(VoiceMessagePatch));

            Logger.LogInfo("[ProximityChat] Server plugin loaded — using game network for voice relay.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// Harmony prefix patch on ServerClient.HandleNetorkEvent to intercept voice packets (EventCode 240).
        /// Voice data is relayed only to nearby players using the game's built-in networking.
        /// </summary>
        [HarmonyPatch(typeof(ServerClient), "HandleNetorkEvent")]
        internal static class VoiceMessagePatch
        {
            private static int _relayCount;
            static bool Prefix(ServerPackage networkEvent, ServerClient __instance)
            {
                // Only intercept our custom voice event code (240).
                if ((byte)networkEvent.Code != VoicePacket.EventCode) return true;

                try
                {
                    byte senderIndex = networkEvent.SenderPlayerID;
                    byte[] voiceData = networkEvent.Buffer;
                    if (!VoicePacket.TryRead(voiceData, out _, out ushort sequence, out _, out int pcmLength))
                    {
                        return false;
                    }

                    _relayCount++;
                    if (_relayCount % 50 == 1 && Instance != null)
                        Instance.Logger.LogInfo($"[ProximityChat] Relay #{_relayCount}: PCM voice from player {senderIndex} seq {sequence} ({pcmLength} bytes)");

                    // Find sender player via GameRoom
                    var senderPlayer = __instance.GameRoomReference.FindPlayer(senderIndex);
                    if (senderPlayer == null) return false;

                    Vector3 senderPos = senderPlayer.PlayerPosition;
                    float maxRange = Mathf.Max(0f, MaxRange.Value);
                    float maxRangeSqr = maxRange * maxRange;

                    // Find all players in range using Citrus.players
                    var recipients = new List<byte>();
                    var players = Citrus.players;
                    if (players == null) return false;

                    foreach (var playerRef in players)
                    {
                        if (playerRef == null || playerRef.player == null) continue;
                        var player = playerRef.player;
                        if (player.PlayerIndex == senderIndex) continue;

                        float distSqr = (senderPos - player.PlayerPosition).sqrMagnitude;
                        if (distSqr <= maxRangeSqr)
                        {
                            recipients.Add(player.PlayerIndex);
                        }
                    }

                    if (recipients.Count > 0)
                    {
                        // The server is authoritative for sender identity; ignore the client header's sender byte.
                        byte[] relayData = VoicePacket.WithSender(voiceData, senderIndex);

                        // Send unreliably for low-latency voice delivery
                        __instance.SendMessageToClients(
                            (EventCode)VoicePacket.EventCode,
                            relayData,
                            recipients.ToArray(),
                            false,  // reliable = false (voice doesn't need guaranteed delivery)
                            false   // encrypted = false
                        );
                    }
                }
                catch (Exception ex)
                {
                    if (Instance != null)
                        Instance.Logger.LogError($"[ProximityChat] Relay error: {ex.Message}");
                }

                return false; // Consume the event — no default handler for code 240
            }
        }
    }
}
