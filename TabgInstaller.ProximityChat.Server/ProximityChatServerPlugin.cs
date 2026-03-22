using BepInEx;
using BepInEx.Configuration;

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

        private void Awake()
        {
            MaxRange = Config.Bind("ProximityChat", "MaxRange", 50f, "Distance beyond which audio is not relayed");
            MinRange = Config.Bind("ProximityChat", "MinRange", 5f, "Distance within which audio is full volume");
            VoicePort = Config.Bind("ProximityChat", "VoicePort", 7778, "UDP port for voice traffic");
            FalloffCurve = Config.Bind("ProximityChat", "FalloffCurve", "Linear", "Volume falloff: Linear or Logarithmic");

            Logger.LogInfo("[ProximityChat] Server plugin loaded.");
        }
    }
}
