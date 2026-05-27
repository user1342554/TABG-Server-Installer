using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using Xunit;

namespace TabgInstaller.Tests.Model
{
    [Collection("PluginRegistry")]
    public class PluginRegistryManifestTests
    {
        [Fact]
        public void LoadFromManifests_UsesManifestDefinitions()
        {
            var manifests = LoadRepositoryManifests();

            try
            {
                PluginRegistry.LoadFromManifests(manifests);

                PluginRegistry.IsLoadedFromRegistry.Should().BeTrue();
                PluginRegistry.ServerPlugins.Should().Contain(plugin => plugin.Id == "MatchCore");
                PluginRegistry.ClientMods.Should().Contain(plugin => plugin.Id == "PopupBlocker");
                PluginRegistry.ServerPlugins.Single(plugin => plugin.Id == "BigSmoke")
                    .RequiresClientMod.Should().BeTrue();
            }
            finally
            {
                PluginRegistry.ResetToBuiltIns();
            }
        }

        [Fact]
        public void BundledManifestDlls_ExistInPayloadFolders()
        {
            var root = FindRepositoryRoot();
            var manifests = LoadRepositoryManifests();
            var serverPayload = Path.Combine(root, "TabgInstaller.Gui", "plugins");
            var clientPayload = Path.Combine(root, "TabgInstaller.Gui", "client-plugins");

            var missing = new List<string>();
            foreach (var manifest in manifests.Where(IsBundledPayload))
            {
                var payloadDir = manifest.Type.Equals("client", StringComparison.OrdinalIgnoreCase)
                    ? clientPayload
                    : serverPayload;

                foreach (var dll in manifest.DllNames)
                {
                    if (!File.Exists(Path.Combine(payloadDir, dll)))
                        missing.Add($"{manifest.Id}: {dll}");
                }
            }

            missing.Should().BeEmpty();
        }

        private static bool IsBundledPayload(PluginManifest manifest) =>
            manifest.Kind.Equals("bundled", StringComparison.OrdinalIgnoreCase)
            || manifest.Kind.Equals("core-dependency", StringComparison.OrdinalIgnoreCase);

        private static List<PluginManifest> LoadRepositoryManifests()
        {
            var manifestDir = Path.Combine(FindRepositoryRoot(), "registry", "plugins");
            return Directory
                .GetFiles(manifestDir, "manifest.json", SearchOption.AllDirectories)
                .Select(path => JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(path))!)
                .ToList();
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "TabgInstaller.sln")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
