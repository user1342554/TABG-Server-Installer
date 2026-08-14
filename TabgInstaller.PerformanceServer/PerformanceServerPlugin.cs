using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace TabgInstaller.PerformanceServer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PerformanceServerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tabginstaller.performanceserver";
        public const string PluginName = "TABG Performance Server";
        public const string PluginVersion = "2.0.0";

        internal static PerformanceServerPlugin Instance;
        private ConfigEntry<bool> _deltaSnapshots;
        private ConfigEntry<int> _keyframeInterval;
        private ConfigEntry<int> _maximumQueuePacketSize;
        private ConfigEntry<bool> _disableProductionGui;
        private Harmony _harmony;

        internal bool DeltaSnapshots => _deltaSnapshots != null && _deltaSnapshots.Value;
        internal int KeyframeInterval => _keyframeInterval != null ? _keyframeInterval.Value : 60;
        internal int MaximumQueuePacketSize => _maximumQueuePacketSize != null ? _maximumQueuePacketSize.Value : 1200;
        internal bool DisableProductionGui => _disableProductionGui != null && _disableProductionGui.Value;

        private void Awake()
        {
            Instance = this;
            _deltaSnapshots = Config.Bind("Replication", "EnableDeltaSnapshots", true,
                "Send dirty entity fields with periodic full keyframes. Optimized clients are required.");
            _keyframeInterval = Config.Bind("Replication", "FullKeyframeInterval", 60,
                new ConfigDescription("Entity-update runs between full recovery keyframes.", new AcceptableValueRange<int>(15, 300)));
            _maximumQueuePacketSize = Config.Bind("Transport", "MaximumQueuePacketSize", 1200,
                new ConfigDescription("Split queued entity updates before this practical UDP payload size.", new AcceptableValueRange<int>(576, 1300)));
            _disableProductionGui = Config.Bind("Headless", "DisableProductionOnGUI", true,
                "Skip per-player IMGUI labels outside the Unity editor.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PerformanceServerPlugin).Assembly);
            Logger.LogInfo("[PerformanceServer] Delta replication, bounded queues, exact chunk packets, and hot packet parsing enabled.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Instance = null;
        }
    }
}
