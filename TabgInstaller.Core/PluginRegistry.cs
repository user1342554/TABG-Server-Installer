using System;
using System.Collections.Generic;
using System.Linq;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core
{
    /// <summary>
    /// Defines how the installer handles a given plugin.
    /// </summary>
    public enum PluginKind
    {
        /// <summary>Normal DLL copied from the bundled-plugins directory.</summary>
        Bundled,
        /// <summary>Core dependency handled separately by the installer.</summary>
        CoreDependency,
        /// <summary>Community server — downloaded/handled separately by the installer.</summary>
        CommunityServer
    }

    /// <summary>
    /// Describes a single installable plugin or client mod.
    /// </summary>
    public sealed record PluginDefinition(
        string Id,
        string Label,
        string[] DllNames,
        bool DefaultChecked,
        PluginKind Kind,
        bool RequiresClientMod = false
    );

    /// <summary>
    /// Single source of truth for all server plugin and client mod definitions.
    /// The launcher now uses owned bundled definitions directly instead of
    /// replacing them with runtime registry data at startup.
    /// </summary>
    public static class PluginRegistry
    {
        // -- Built-in state ------------------------------------------------

        private static PluginDefinition[] _serverPlugins;
        private static PluginDefinition[] _clientMods;
        private static bool _loadedFromRegistry;

        static PluginRegistry()
        {
            ResetToBuiltIns();
        }

        /// <summary>Current server plugin definitions (from registry or hardcoded fallback).</summary>
        public static PluginDefinition[] ServerPlugins => _serverPlugins;

        /// <summary>Current client mod definitions (from registry or hardcoded fallback).</summary>
        public static PluginDefinition[] ClientMods => _clientMods;

        /// <summary>False in the built-in launcher flow; retained for old callers.</summary>
        public static bool IsLoadedFromRegistry => _loadedFromRegistry;

        // -- Sigma Preset --------------------------------------------------

        /// <summary>Plugin IDs that make up the "Sigma" preset.</summary>
        public static readonly HashSet<string> SigmaPresetIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "Citruslib", "MatchCore", "ServerLogger", "BigSmoke", "MGLFlashbang", "UnusedVehicles", "ProximityChat"
        };

        // -- Built-in definitions ----------------------------------------

        private static readonly PluginDefinition[] BuiltInServerPlugins =
        {
            new("Citruslib", "Citruslib - required server modding API", new[] { "Citruslib.dll" }, true, PluginKind.CoreDependency),
            new("MatchCore", "TABG Match Core - rings, loadouts, vote-start, drops, timers, win rules", new[] { "TabgInstaller.MatchCore.dll" }, true, PluginKind.Bundled),
            new("ServerLogger", "Server Logger - player name, PlayFab, and Epic identity log", new[] { "TabgInstaller.ServerLogger.dll" }, true, PluginKind.Bundled),
            new("UnusedVehicles", "Unused Vehicles - spawn and manage hidden TABG vehicles", new[] { "TabgInstaller.UnusedVehicles.dll" }, true, PluginKind.Bundled),
            new("BigSmoke", "Big Smoke Grenade - custom grenade gameplay", new[] { "TabgInstaller.CustomGrenades.dll" }, true, PluginKind.Bundled, RequiresClientMod: true),
            new("MGLFlashbang", "MGL Flashbang - custom grenade gameplay", new[] { "TabgInstaller.CustomGrenades.dll" }, true, PluginKind.Bundled, RequiresClientMod: true),
            new("SoloTesting", "Solo Testing - local testing helpers", new[] { "TabgInstaller.SoloTesting.dll" }, false, PluginKind.Bundled),
            new("ProximityChat", "Proximity Chat Server - relays nearby voice packets", new[] { "TabgInstaller.ProximityChat.Server.dll" }, true, PluginKind.Bundled, RequiresClientMod: true),
            new("HuntMode", "Hunt Mode - asymmetric 4v1 survival mode", new[] { "TabgInstaller.HuntMode.dll", "TabgInstaller.HuntMode.Shared.dll" }, false, PluginKind.Bundled, RequiresClientMod: true),
            new("JuggernautMode", "Juggernaut Mode - boss player versus everyone", new[] { "JuggernautMode.Server.dll" }, false, PluginKind.Bundled, RequiresClientMod: true),
            new("FakePlayers", "Fake Players - dummy players and AI test targets", new[] { "TabgInstaller.FakePlayers.dll" }, false, PluginKind.Bundled),
            new("AdminRadar", "Admin Radar Server - sends admin-only player telemetry", new[] { "TabgInstaller.AdminRadar.Server.dll" }, false, PluginKind.Bundled, RequiresClientMod: true),
        };

        private static readonly PluginDefinition[] BuiltInClientMods =
        {
            new("FlyingControls", "Flying Controls - steering support for custom flying vehicles", new[] { "TabgInstaller.FlyingControls.dll" }, true, PluginKind.Bundled),
            new("CustomGrenades", "Custom Grenades Client - visuals and effects for custom grenades", new[] { "TabgInstaller.CustomGrenades.dll" }, true, PluginKind.Bundled),
            new("CoordsDisplay", "Coords Display - client coordinate overlay", new[] { "TabgInstaller.CoordsDisplay.dll" }, true, PluginKind.Bundled),
            new("ModSettings", "Mod Settings - client-side settings support", new[] { "TabgInstaller.ModSettings.dll" }, true, PluginKind.Bundled),
            new("EnhancedClient", "Enhanced Client - LOD, draw distance, haze, and HUD controls", new[] { "TabgInstaller.EnhancedClient.dll" }, true, PluginKind.Bundled),
            new("PopupBlocker", "Popup Blocker - suppresses modded-client anti-cheat popups", new[] { "TabgInstaller.PopupBlocker.dll" }, true, PluginKind.Bundled),
            new("ProximityChatClient", "Proximity Chat Client - captures and plays proximity voice", new[] { "TabgInstaller.ProximityChat.Client.dll" }, true, PluginKind.Bundled),
            new("HuntModeClient", "Hunt Mode Client - HUD for Hunt Mode", new[] { "TabgInstaller.HuntMode.Client.dll", "TabgInstaller.HuntMode.Shared.dll" }, false, PluginKind.Bundled),
            new("JuggernautClient", "Juggernaut Client - boss bar, loadout picker, scoreboard", new[] { "JuggernautMode.Client.dll" }, false, PluginKind.Bundled),
            new("AdminRadarClient", "Admin Radar Client - admin-only radar overlay", new[] { "TabgInstaller.AdminRadar.Client.dll" }, false, PluginKind.Bundled),
        };

        public static void ResetToBuiltIns()
        {
            _serverPlugins = BuiltInServerPlugins;
            _clientMods = BuiltInClientMods;
            _loadedFromRegistry = false;
        }

        /// <summary>
        /// Registry loading is intentionally disabled. The launcher owns the
        /// bundled plugin list now, so stale cached registry data cannot
        /// reintroduce removed third-party DLLs.
        /// </summary>
        public static void LoadFromManifests(List<PluginManifest> manifests)
        {
            ResetToBuiltIns();
        }

        private static PluginDefinition ToDefinition(PluginManifest m)
        {
            var kind = m.Kind switch
            {
                "core-dependency" => PluginKind.CoreDependency,
                "community-server" => PluginKind.CommunityServer,
                _ => PluginKind.Bundled
            };

            return new PluginDefinition(
                Id: m.Id,
                Label: $"{m.Name} \u2014 {m.Description}",
                DllNames: m.DllNames ?? Array.Empty<string>(),
                DefaultChecked: m.DefaultChecked,
                Kind: kind,
                RequiresClientMod: m.RequiresClientMod
            );
        }

        // ── Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Given a set of selected plugin definitions, returns the deduplicated list
        /// of bundled DLL file names to copy.
        /// </summary>
        public static List<string> CollectBundledDlls(PluginDefinition[] plugins, IReadOnlyList<bool> selected)
        {
            var dlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < plugins.Length && i < selected.Count; i++)
            {
                if (selected[i] && plugins[i].Kind == PluginKind.Bundled)
                {
                    foreach (var dll in plugins[i].DllNames)
                        dlls.Add(dll);
                }
            }
            return dlls.ToList();
        }

        /// <summary>
        /// Looks up a plugin by its Id. Returns null if not found.
        /// </summary>
        public static PluginDefinition? FindById(string id)
        {
            return Array.Find(_serverPlugins, p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                ?? Array.Find(_clientMods, p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

    }
}
