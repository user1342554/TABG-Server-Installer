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
    /// Loaded dynamically from the online registry at startup. Both
    /// <c>InstallerPanel</c> and <c>SetupWizardWindow</c> consume these arrays.
    /// </summary>
    public static class PluginRegistry
    {
        // ── Dynamic state (updated from online registry) ────────────────

        private static PluginDefinition[] _serverPlugins;
        private static PluginDefinition[] _clientMods;
        private static bool _loadedFromRegistry;

        static PluginRegistry()
        {
            _serverPlugins = Array.Empty<PluginDefinition>();
            _clientMods = Array.Empty<PluginDefinition>();
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

    }
}
