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
                PluginRegistry.ServerPlugins.Single(plugin => plugin.Id == "CustomGrenades")
                    .DllNames.Should().Contain("TabgInstaller.CustomGrenades.dll");
                PluginRegistry.ClientMods.Single(plugin => plugin.Id == "CustomGrenades")
                    .DllNames.Should().Contain("TabgInstaller.CustomGrenades.dll");
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
                foreach (var side in ManifestSides(manifest))
                {
                    var payloadDir = side.Side.Equals("client", StringComparison.OrdinalIgnoreCase)
                        ? clientPayload
                        : serverPayload;

                    foreach (var dll in side.Manifest.DllNames)
                    {
                        if (!File.Exists(Path.Combine(payloadDir, dll)))
                            missing.Add($"{side.Manifest.Id} ({side.Side}): {dll}");
                    }
                }
            }

            missing.Should().BeEmpty();
        }

        [Fact]
        public void RepositoryManifests_HaveUniqueIdsAndValidReferences()
        {
            var root = FindRepositoryRoot();
            var manifestDir = Path.Combine(root, "registry", "plugins");
            var manifests = Directory
                .GetFiles(manifestDir, "manifest.json", SearchOption.AllDirectories)
                .Select(path => new
                {
                    Path = path,
                    Folder = new DirectoryInfo(Path.GetDirectoryName(path)!).Name,
                    Manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(path))!
                })
                .ToList();

            manifests.Select(item => item.Manifest.Id)
                .Should().OnlyHaveUniqueItems("each registry folder should define one stable plugin id");

            manifests.Where(item => !item.Folder.Equals(item.Manifest.Id, StringComparison.Ordinal))
                .Select(item => $"{item.Folder} -> {item.Manifest.Id}")
                .Should().BeEmpty("manifest ids must match their registry folder names");

            var knownIds = manifests.Select(item => item.Manifest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var brokenDependencies = new List<string>();
            foreach (var item in manifests)
            {
                foreach (string dependency in item.Manifest.Dependencies ?? Array.Empty<string>())
                {
                    if (!knownIds.Contains(dependency))
                        brokenDependencies.Add($"{item.Manifest.Id}: {dependency}");
                }

                if (item.Manifest.RequiresClientMod &&
                    (string.IsNullOrWhiteSpace(item.Manifest.ClientPluginId) || !knownIds.Contains(item.Manifest.ClientPluginId)))
                {
                    brokenDependencies.Add($"{item.Manifest.Id}: clientPluginId={item.Manifest.ClientPluginId}");
                }
            }

            brokenDependencies.Should().BeEmpty("dependency and client plugin ids must exist in the registry");
        }

        [Fact]
        public void RepositoryManifests_DoNotExposeDuplicatePayloadsAsSeparatePluginsOnSameSide()
        {
            var manifests = LoadRepositoryManifests();
            var duplicates = manifests
                .SelectMany(ManifestSides)
                .GroupBy(item => item.Side + ":" + string.Join("|", item.Manifest.DllNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Select(item => item.Manifest.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(group => string.Join(", ", group.Select(item => item.Manifest.Id).Distinct(StringComparer.OrdinalIgnoreCase)))
                .ToList();

            duplicates.Should().BeEmpty("one DLL on the same side should be presented as one plugin with sub-feature config toggles");
        }

        private static bool IsBundledPayload(PluginManifest manifest) =>
            manifest.Kind.Equals("bundled", StringComparison.OrdinalIgnoreCase)
            || manifest.Kind.Equals("core-dependency", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<(PluginManifest Manifest, string Side)> ManifestSides(PluginManifest manifest)
        {
            if (manifest.Type.Equals("both", StringComparison.OrdinalIgnoreCase))
            {
                yield return (manifest, "server");
                yield return (manifest, "client");
                yield break;
            }

            yield return (manifest, manifest.Type);
        }

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
