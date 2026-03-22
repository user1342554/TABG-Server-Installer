using BepInEx;
using BepInEx.Configuration;

namespace TabgInstaller.ProximityChat.Client
{
    [BepInPlugin("tabginstaller.proximitychat.client", "Proximity Voice Chat Client", "1.0.0")]
    [BepInDependency("tabginstaller.modsettings", BepInDependency.DependencyFlags.SoftDependency)]
    public class ProximityChatClientPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> MicSensitivity;
        public static ConfigEntry<float> MasterVolume;
        public static ConfigEntry<string> MicrophoneDevice;

        private void Awake()
        {
            Enabled = Config.Bind("ProximityChat", "Enabled", true, "Enable/disable voice chat");
            MicSensitivity = Config.Bind("ProximityChat", "MicSensitivity", 0.01f, "Voice activity detection threshold (RMS)");
            MasterVolume = Config.Bind("ProximityChat", "MasterVolume", 1.0f, "Overall voice chat volume");
            MicrophoneDevice = Config.Bind("ProximityChat", "MicrophoneDevice", "", "Microphone device name (empty = system default)");

            try
            {
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Enabled", "Toggle voice chat on/off", Enabled);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Mic Sensitivity", "VAD threshold", MicSensitivity);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Master Volume", "Voice chat volume", MasterVolume);
            }
            catch { }

            Logger.LogInfo("[ProximityChat] Client plugin loaded.");
        }
    }
}
