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
        /// <summary>Core dependency handled separately by the installer (Citruslib, StarterPack).</summary>
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
    /// Loads dynamically from the online registry when available, with hardcoded
    /// fallback for offline use. Both <c>InstallerPanel</c> and <c>SetupWizardWindow</c>
    /// consume these arrays.
    /// </summary>
    public static class PluginRegistry
    {
        // ── Dynamic state (updated from online registry) ────────────────

        private static PluginDefinition[] _serverPlugins;
        private static PluginDefinition[] _clientMods;
        private static bool _loadedFromRegistry;

        static PluginRegistry()
        {
            _serverPlugins = HardcodedServerPlugins;
            _clientMods = HardcodedClientMods;
        }

        /// <summary>Current server plugin definitions (from registry or hardcoded fallback).</summary>
        public static PluginDefinition[] ServerPlugins => _serverPlugins;

        /// <summary>Current client mod definitions (from registry or hardcoded fallback).</summary>
        public static PluginDefinition[] ClientMods => _clientMods;

        /// <summary>True when definitions were loaded from the online registry.</summary>
        public static bool IsLoadedFromRegistry => _loadedFromRegistry;

        // ── Sigma Preset ────────────────────────────────────────────────

        /// <summary>Plugin IDs that make up the "Sigma" preset.</summary>
        public static readonly HashSet<string> SigmaPresetIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "Citruslib", "StarterPack", "StarterPackFixes", "CustomSpawnpoints", "FreddoCommission"
        };

        // ── Load from registry ──────────────────────────────────────────

        /// <summary>
        /// Replaces the hardcoded plugin lists with definitions from the online registry.
        /// Called on app startup after the registry is fetched.
        /// </summary>
        public static void LoadFromManifests(List<PluginManifest> manifests)
        {
            if (manifests == null || manifests.Count == 0) return;

            var server = new List<PluginDefinition>();
            var client = new List<PluginDefinition>();

            foreach (var m in manifests)
            {
                var def = ToDefinition(m);
                if (m.Type == "client")
                    client.Add(def);
                else
                    server.Add(def);
            }

            if (server.Count > 0) _serverPlugins = server.ToArray();
            if (client.Count > 0) _clientMods = client.ToArray();
            _loadedFromRegistry = true;
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

        // ── Hardcoded Fallback (used when offline) ──────────────────────

        private static readonly PluginDefinition[] HardcodedServerPlugins =
        {
            new("Citruslib",
                "Citruslib \u2014 Core server library (admin, permissions, commands)",
                Array.Empty<string>(), true, PluginKind.CoreDependency),

            new("StarterPack",
                "StarterPack \u2014 Match mechanics, loadouts, win conditions",
                Array.Empty<string>(), true, PluginKind.CoreDependency),

            new("StarterPackFixes",
                "StarterPackFixes \u2014 Loot drop control",
                new[] { "StarterPackFixes.dll" }, true, PluginKind.Bundled),

            new("CustomSpawnpoints",
                "CustomSpawnpoints \u2014 Custom spawn points",
                new[] { "CustomSpawnpoints.dll" }, true, PluginKind.Bundled),

            new("FreddoCommission",
                "FreddoTABGCommission \u2014 Curses, grenades on kill, bans",
                new[] { "FreddoTABGCommission.dll" }, true, PluginKind.Bundled),

            new("MatchTimeout",
                "MatchAndPreMatchTimeout \u2014 Match timing",
                new[] { "MatchAndPreMatchTimeout.dll" }, true, PluginKind.Bundled),

            new("ServerLogger",
                "ServerLogger \u2014 Player logging",
                new[] { "ServerLogger.dll" }, true, PluginKind.Bundled),

            new("VoteToStart",
                "VoteToStart \u2014 Vote-to-start",
                new[] { "VoteToStart.dll" }, true, PluginKind.Bundled),

            new("UnusedVehicles",
                "UnusedVehicles \u2014 Spawn cut vehicles (Heli, UFO, Mustang, VW, HoverBike)",
                new[] { "TabgInstaller.UnusedVehicles.dll" }, true, PluginKind.Bundled),

            new("BigSmoke",
                "BigSmokeGrenade \u2014 Giant purple smoke grenades (/give 208)",
                new[] { "TabgInstaller.CustomGrenades.dll" }, true, PluginKind.Bundled),

            new("MGLFlashbang",
                "MGLFlashbang \u2014 MGL shoots flashbang rounds",
                new[] { "TabgInstaller.CustomGrenades.dll" }, true, PluginKind.Bundled),

            new("SoloTesting",
                "SoloTesting \u2014 Prevents 'You Win' when testing alone",
                new[] { "TabgInstaller.SoloTesting.dll" }, false, PluginKind.Bundled),

            new("CommunityServer",
                "TABGCommunityServer \u2014 Community server",
                Array.Empty<string>(), false, PluginKind.CommunityServer),

            new("ProximityChat",
                "ProximityChat \u2014 Proximity voice chat",
                new[] { "TabgInstaller.ProximityChat.Server.dll" }, true, PluginKind.Bundled,
                RequiresClientMod: true),

            new("HuntMode",
                "Hunt Mode (4v1) \u2014 1 Killer vs 4 Survivors survival",
                new[] { "TabgInstaller.HuntMode.dll", "TabgInstaller.HuntMode.Shared.dll" }, false, PluginKind.Bundled,
                RequiresClientMod: true),

            new("JuggernautMode",
                "Juggernaut Mode \u2014 One massive player vs everyone, score-based",
                new[] { "JuggernautMode.Server.dll" }, false, PluginKind.Bundled),

            new("TabgVR",
                "TABGVR Server \u2014 VR hand sync for VR players",
                new[] { "TABGVR.Server.CitrusLib.dll" }, false, PluginKind.Bundled,
                RequiresClientMod: true),

            new("FakePlayers",
                "FakePlayers \u2014 Spawn dummy players for solo testing (/spawndummy, /removedummy)",
                new[] { "TabgInstaller.FakePlayers.dll" }, false, PluginKind.Bundled),
        };

        private static readonly PluginDefinition[] HardcodedClientMods =
        {
            new("FlyingControls",
                "FlyingControls \u2014 Steer Helicopters, UFOs, Hover vehicles (W/S/A/D + Space)",
                new[] { "TabgInstaller.FlyingControls.dll" }, true, PluginKind.Bundled),

            new("EnhancedTABG",
                "Enhanced TABG \u2014 Infinite LOD (F1), HUD toggle (F2), fog removal (F3)",
                new[] { "Enhanced TABG.dll" }, true, PluginKind.Bundled),

            new("CustomGrenades",
                "BigSmokeGrenade + MGLFlashbang \u2014 Custom grenade effects",
                new[] { "TabgInstaller.CustomGrenades.dll" }, true, PluginKind.Bundled),

            new("CoordsDisplay",
                "Coords Display \u2014 Press F5 to show your X/Y/Z position",
                new[] { "TabgInstaller.CoordsDisplay.dll" }, true, PluginKind.Bundled),

            new("ModSettings",
                "Mod Settings \u2014 In-game settings menu for all mods (press #)",
                new[] { "TabgInstaller.ModSettings.dll" }, true, PluginKind.Bundled),

            new("PopupBlocker",
                "Pop-up Blocker \u2014 Disable anti-cheat popups for custom servers",
                new[] { "Pop-up Blocker.dll" }, true, PluginKind.Bundled),

            new("ProximityChatClient",
                "Proximity Chat \u2014 Proximity voice chat for custom servers",
                new[] { "TabgInstaller.ProximityChat.Client.dll" }, true, PluginKind.Bundled),

            new("HuntModeClient",
                "Hunt Mode Client \u2014 HUD and perk selection UI",
                new[] { "TabgInstaller.HuntMode.Client.dll", "TabgInstaller.HuntMode.Shared.dll" }, false, PluginKind.Bundled),

            new("JuggernautClient",
                "Juggernaut Mode Client \u2014 Boss bar, loadout picker, scoreboard",
                new[] { "JuggernautMode.Client.dll" }, false, PluginKind.Bundled),

            new("TabgVR-Client",
                "TABGVR \u2014 Play TABG in Virtual Reality (requires VR headset)",
                new[] { "TABGVR.dll" }, false, PluginKind.Bundled),
        };
    }
}
