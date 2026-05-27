using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
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
    /// In-memory plugin catalog used by the launcher. The preferred source is
    /// the bundled manifest files under registry/plugins; built-ins are a
    /// fallback for incomplete development or publish layouts.
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

        /// <summary>Current server plugin definitions.</summary>
        public static PluginDefinition[] ServerPlugins => _serverPlugins;

        /// <summary>Current client mod definitions.</summary>
        public static PluginDefinition[] ClientMods => _clientMods;

        /// <summary>True when definitions came from bundled manifest files.</summary>
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
            new("AdminRadarClient", "Admin Radar Client - admin-only radar overlay", new[] { "TabgInstaller.AdminRadar.Client.dll" }, false, PluginKind.Bundled),
        };

        public static void ResetToBuiltIns()
        {
            _serverPlugins = BuiltInServerPlugins;
            _clientMods = BuiltInClientMods;
            _loadedFromRegistry = false;
        }

        /// <summary>Loads plugin definitions from manifests and falls back to built-ins when empty.</summary>
        public static void LoadFromManifests(List<PluginManifest> manifests)
        {
            if (manifests == null || manifests.Count == 0)
            {
                ResetToBuiltIns();
                return;
            }

            var ownedManifests = manifests
                .Where(m => m.Kind.Equals("bundled", StringComparison.OrdinalIgnoreCase)
                    || m.Kind.Equals("core-dependency", StringComparison.OrdinalIgnoreCase)
                    || m.Kind.Equals("community-server", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var serverPlugins = ownedManifests
                .Where(m => m.Type.Equals("server", StringComparison.OrdinalIgnoreCase)
                    || m.Type.Equals("both", StringComparison.OrdinalIgnoreCase))
                .OrderBy(DefinitionSortKey)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToDefinition)
                .ToArray();

            var clientMods = ownedManifests
                .Where(m => m.Type.Equals("client", StringComparison.OrdinalIgnoreCase)
                    || m.Type.Equals("both", StringComparison.OrdinalIgnoreCase))
                .OrderBy(DefinitionSortKey)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToDefinition)
                .ToArray();

            if (serverPlugins.Length == 0 && clientMods.Length == 0)
            {
                ResetToBuiltIns();
                return;
            }

            _serverPlugins = serverPlugins;
            _clientMods = clientMods;
            _loadedFromRegistry = true;
        }

        /// <summary>
        /// Loads bundled manifests from registry/plugins near the app output or
        /// from a parent source checkout. Falls back to built-ins if unavailable.
        /// </summary>
        public static void LoadBundledManifests()
        {
            try
            {
                var manifestDir = FindManifestDirectory();
                if (manifestDir == null)
                {
                    ResetToBuiltIns();
                    return;
                }

                var manifests = Directory
                    .GetFiles(manifestDir, "manifest.json", SearchOption.AllDirectories)
                    .Select(path => JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(path)))
                    .Where(manifest => manifest != null)
                    .Cast<PluginManifest>()
                    .ToList();

                LoadFromManifests(manifests);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"[PluginRegistry] Failed to load bundled manifests: {ex}");
                ResetToBuiltIns();
            }
        }

        private static string? FindManifestDirectory()
        {
            var candidates = new List<string>();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            AddCandidate(baseDir);
            AddCandidate(Directory.GetCurrentDirectory());

            var dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                AddCandidate(dir.FullName);
                dir = dir.Parent;
            }

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate) &&
                    Directory.GetFiles(candidate, "manifest.json", SearchOption.AllDirectories).Length > 0)
                {
                    return candidate;
                }
            }

            return null;

            void AddCandidate(string root)
            {
                var candidate = Path.Combine(root, "registry", "plugins");
                if (!candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(candidate);
            }
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
                Label: $"{m.Name} - {m.Description}",
                DllNames: m.DllNames ?? Array.Empty<string>(),
                DefaultChecked: m.DefaultChecked,
                Kind: kind,
                RequiresClientMod: m.RequiresClientMod
            );
        }

        private static int DefinitionSortKey(PluginManifest manifest)
        {
            if (manifest.Id.Equals("Citruslib", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (manifest.DefaultChecked)
                return 10;
            return 20;
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
