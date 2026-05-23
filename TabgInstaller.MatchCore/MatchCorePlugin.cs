using System;
using BepInEx;
using BepInEx.Logging;
using CitrusLib;
using HarmonyLib;
using Landfall.Network;

namespace TabgInstaller.MatchCore
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.cyrusthelesser.citruslib", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class MatchCorePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tabginstaller.matchcore";
        public const string PluginName = "TABG Match Core";
        public const string PluginVersion = "1.0.0";

        internal static MatchCorePlugin Instance;
        internal static ManualLogSource LogSource;
        internal static MatchCoreConfig Settings;
        internal static Harmony HarmonyInstance;

        private void Awake()
        {
            Instance = this;
            LogSource = Logger;
            Settings = MatchCoreConfig.LoadOrCreate(Logger);

            HarmonyInstance = new Harmony(PluginGuid);
            HarmonyInstance.PatchAll(typeof(MatchCorePlugin).Assembly);

            RegisterCommands();
            Logger.LogInfo("[MatchCore] Loaded owned match/ring/loadout/vote systems.");
        }

        private void OnDestroy()
        {
            HarmonyInstance?.UnpatchSelf();
        }

        private static void RegisterCommands()
        {
            try
            {
                Citrus.AddCommand("matchcore", (args, player) =>
                {
                    if (args != null && args.Length > 0 && args[0].Equals("reload", StringComparison.OrdinalIgnoreCase))
                    {
                        Settings = MatchCoreConfig.LoadOrCreate(LogSource);
                        MatchCoreRuntime.Reset();
                        Citrus.SelfParrot(player, "MatchCore config reloaded.");
                        return;
                    }

                    Citrus.SelfParrot(player, "MatchCore is active. Use /matchcore reload after editing TheStarterPack.txt.");
                }, "MatchCore", "Reload or inspect MatchCore", "[reload]", 0);
            }
            catch (Exception ex)
            {
                LoggerSafe("Could not register Citrus command: " + ex.Message);
            }
        }

        internal static void LoggerSafe(string message)
        {
            LogSource?.LogInfo("[MatchCore] " + message);
        }

        internal static void Warning(string message)
        {
            LogSource?.LogWarning("[MatchCore] " + message);
        }
    }
}
