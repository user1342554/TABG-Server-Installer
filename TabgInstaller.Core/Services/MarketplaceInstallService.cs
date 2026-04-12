using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public class MarketplaceInstallService : IMarketplaceInstallService
    {
        private readonly GitHubService _gitHub;
        private readonly IInstalledPluginTracker _tracker;

        public MarketplaceInstallService(GitHubService gitHub, IInstalledPluginTracker tracker)
        {
            _gitHub = gitHub;
            _tracker = tracker;
        }

        public static List<PluginManifest> ResolveDependencies(
            PluginManifest target,
            List<PluginManifest> registryPlugins,
            IInstalledPluginTracker tracker)
        {
            var result = new List<PluginManifest>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Visit(PluginManifest plugin)
            {
                if (visited.Contains(plugin.Id)) return;
                visited.Add(plugin.Id);

                foreach (var depId in plugin.Dependencies)
                {
                    // Skip bundled plugins
                    if (PluginRegistry.FindById(depId) != null) continue;

                    // Skip already-installed community plugins
                    if (tracker.IsInstalled(depId)) continue;

                    var dep = registryPlugins.FirstOrDefault(
                        p => p.Id.Equals(depId, StringComparison.OrdinalIgnoreCase));
                    if (dep != null)
                        Visit(dep);
                }

                result.Add(plugin);
            }

            Visit(target);
            return result;
        }

        public static bool HasUpdate(PluginManifest manifest, IInstalledPluginTracker tracker)
        {
            var entry = tracker.FindById(manifest.Id);
            if (entry == null) return false;
            if (entry.Pinned) return false;
            return CompareVersions(manifest.Version, entry.InstalledVersion) > 0;
        }

        public static string GetCommunityPluginDir(string? serverRoot, string? clientModdedPath, string pluginId, string type)
        {
            var basePath = type == "client" ? clientModdedPath : serverRoot;
            return Path.Combine(basePath!, "BepInEx", "plugins", "community", pluginId);
        }

        public async Task<bool> InstallPluginAsync(
            PluginManifest manifest,
            List<PluginManifest> registryPlugins,
            string serverRoot,
            string? clientModdedPath)
        {
            var toInstall = ResolveDependencies(manifest, registryPlugins, _tracker);

            foreach (var plugin in toInstall)
            {
                var success = await DownloadAndPlacePlugin(plugin, serverRoot, clientModdedPath);
                if (!success) return false;
                _tracker.AddPlugin(plugin.Id, plugin.Version, plugin.DllNames);
            }

            return true;
        }

        public async Task<bool> UpdatePluginAsync(
            PluginManifest manifest,
            string serverRoot,
            string? clientModdedPath)
        {
            var pluginDir = GetInstallDir(manifest, serverRoot, clientModdedPath);
            var backupDir = Path.Combine(pluginDir, ".backup");

            try
            {
                if (Directory.Exists(pluginDir))
                {
                    if (Directory.Exists(backupDir))
                        Directory.Delete(backupDir, true);
                    Directory.CreateDirectory(backupDir);

                    foreach (var file in Directory.GetFiles(pluginDir, "*.dll"))
                        File.Copy(file, Path.Combine(backupDir, Path.GetFileName(file)));
                }

                var success = await DownloadAndPlacePlugin(manifest, serverRoot, clientModdedPath);
                if (!success)
                {
                    RestoreBackup(backupDir, pluginDir);
                    return false;
                }

                _tracker.UpdatePluginVersion(manifest.Id, manifest.Version);

                if (Directory.Exists(backupDir))
                    Directory.Delete(backupDir, true);

                return true;
            }
            catch (Exception)
            {
                RestoreBackup(backupDir, pluginDir);
                return false;
            }
        }

        public bool UninstallPlugin(string pluginId, string serverRoot, string? clientModdedPath)
        {
            try
            {
                var entry = _tracker.FindById(pluginId);
                if (entry == null) return false;

                var serverDir = Path.Combine(serverRoot, "BepInEx", "plugins", "community", pluginId);
                if (Directory.Exists(serverDir))
                    Directory.Delete(serverDir, true);

                if (clientModdedPath != null)
                {
                    var clientDir = Path.Combine(clientModdedPath, "BepInEx", "plugins", "community", pluginId);
                    if (Directory.Exists(clientDir))
                        Directory.Delete(clientDir, true);
                }

                _tracker.RemovePlugin(pluginId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> DownloadAndPlacePlugin(
            PluginManifest manifest,
            string serverRoot,
            string? clientModdedPath)
        {
            if (!TryParseGitHubUrl(manifest.DownloadUrl, out var owner, out var repo))
                return false;

            var release = await _gitHub.GetLatestReleaseAsync(owner, repo);
            if (release == null) return false;

            var installDir = GetInstallDir(manifest, serverRoot, clientModdedPath);
            Directory.CreateDirectory(installDir);

            foreach (var dllName in manifest.DllNames)
            {
                var asset = release.Assets.FirstOrDefault(a =>
                    a.Name.Equals(dllName, StringComparison.OrdinalIgnoreCase));
                if (asset == null) return false;

                var destPath = Path.Combine(installDir, dllName);
                var success = await _gitHub.DownloadAssetAsync(
                    owner, repo, asset.BrowserDownloadUrl, destPath, installDir, null);
                if (!success) return false;
            }

            if (manifest.Type == "both" && clientModdedPath != null)
            {
                var clientDir = Path.Combine(clientModdedPath, "BepInEx", "plugins", "community", manifest.Id);
                Directory.CreateDirectory(clientDir);
                foreach (var dllName in manifest.DllNames)
                {
                    var srcPath = Path.Combine(installDir, dllName);
                    var destPath = Path.Combine(clientDir, dllName);
                    if (File.Exists(srcPath))
                        File.Copy(srcPath, destPath, true);
                }
            }

            return true;
        }

        private string GetInstallDir(PluginManifest manifest, string serverRoot, string? clientModdedPath)
        {
            if (manifest.Type == "client" && clientModdedPath != null)
                return GetCommunityPluginDir(null, clientModdedPath, manifest.Id, "client");
            return GetCommunityPluginDir(serverRoot, null, manifest.Id, "server");
        }

        private static void RestoreBackup(string backupDir, string pluginDir)
        {
            try
            {
                if (!Directory.Exists(backupDir)) return;
                foreach (var file in Directory.GetFiles(backupDir, "*.dll"))
                    File.Copy(file, Path.Combine(pluginDir, Path.GetFileName(file)), true);
                Directory.Delete(backupDir, true);
            }
            catch (Exception)
            {
                // Best effort
            }
        }

        private static bool TryParseGitHubUrl(string url, out string owner, out string repo)
        {
            owner = "";
            repo = "";
            try
            {
                var uri = new Uri(url);
                var segments = uri.AbsolutePath.Trim('/').Split('/');
                if (segments.Length >= 2)
                {
                    owner = segments[0];
                    repo = segments[1];
                    return true;
                }
            }
            catch (Exception)
            {
                // Invalid URL
            }

            return false;
        }

        private static int CompareVersions(string a, string b)
        {
            var aParts = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
            var bParts = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
            var len = Math.Max(aParts.Length, bParts.Length);

            for (int i = 0; i < len; i++)
            {
                var av = i < aParts.Length ? aParts[i] : 0;
                var bv = i < bParts.Length ? bParts[i] : 0;
                if (av != bv) return av - bv;
            }

            return 0;
        }
    }
}
