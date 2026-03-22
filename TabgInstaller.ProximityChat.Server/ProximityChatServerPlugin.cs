using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;

namespace TabgInstaller.ProximityChat.Server
{
    [BepInPlugin("tabginstaller.proximitychat.server", "Proximity Voice Chat Server", "1.0.0")]
    [BepInDependency("com.cyrusthelesser.citruslib", BepInDependency.DependencyFlags.SoftDependency)]
    public class ProximityChatServerPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> MaxRange;
        public static ConfigEntry<float> MinRange;
        public static ConfigEntry<int> VoicePort;
        public static ConfigEntry<string> FalloffCurve;

        private VoiceServer _voiceServer;

        private void Awake()
        {
            MaxRange = Config.Bind("ProximityChat", "MaxRange", 50f, "Distance beyond which audio is not relayed");
            MinRange = Config.Bind("ProximityChat", "MinRange", 5f, "Distance within which audio is full volume");
            VoicePort = Config.Bind("ProximityChat", "VoicePort", 7778, "UDP port for voice traffic");
            FalloffCurve = Config.Bind("ProximityChat", "FalloffCurve", "Linear", "Volume falloff: Linear or Logarithmic");

            Logger.LogInfo("[ProximityChat] Server plugin loaded.");
        }

        private Harmony _harmony;

        private void Start()
        {
            try
            {
                _voiceServer = new VoiceServer(
                    VoicePort.Value,
                    MinRange.Value,
                    MaxRange.Value,
                    FalloffCurve.Value,
                    msg => Logger.LogInfo(msg)
                );
                _voiceServer.Start();

                _harmony = new Harmony("tabginstaller.proximitychat.server");
                _harmony.PatchAll(typeof(PlayerDisconnectPatch));
                PlayerDisconnectPatch.Server = _voiceServer;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ProximityChat] Failed to start: {ex}");
            }
        }

        private void OnDestroy()
        {
            _voiceServer?.Dispose();
            _harmony?.UnpatchSelf();
        }

        [HarmonyPatch(typeof(GameRoom), "RemovePlayer")]
        internal static class PlayerDisconnectPatch
        {
            internal static VoiceServer Server;
            static void Postfix(TABGPlayerServer player)
            {
                if (player != null && Server != null)
                    Server.OnPlayerDisconnected(player.PlayerIndex);
            }
        }
    }
}
