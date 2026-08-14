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
        public void BuiltIns_ExposePairedRangeMapPlugins()
        {
            PluginRegistry.ResetToBuiltIns();

            var server = PluginRegistry.ServerPlugins.Single(plugin => plugin.Id == "RangeMap");
            var client = PluginRegistry.ClientMods.Single(plugin => plugin.Id == "RangeMapClient");

            server.RequiresClientMod.Should().BeTrue();
            server.DllNames.Should().ContainSingle().Which.Should().Be("TabgInstaller.RangeMap.Server.dll");
            client.DllNames.Should().ContainSingle().Which.Should().Be("TabgInstaller.RangeMap.Client.dll");
        }

        [Fact]
        public void BuiltIns_ExposePairedDevTestMapPlugins()
        {
            PluginRegistry.ResetToBuiltIns();

            var server = PluginRegistry.ServerPlugins.Single(plugin => plugin.Id == "DevTestMap");
            var client = PluginRegistry.ClientMods.Single(plugin => plugin.Id == "DevTestMapClient");

            server.RequiresClientMod.Should().BeTrue();
            server.DllNames.Should().ContainSingle().Which.Should().Be("TabgInstaller.DevTestMap.Server.dll");
            client.DllNames.Should().ContainSingle().Which.Should().Be("TabgInstaller.DevTestMap.Client.dll");
        }

        [Fact]
        public void BuiltIns_ExposePairedCustomGameSkinsPlugins()
        {
            PluginRegistry.ResetToBuiltIns();

            var server = PluginRegistry.ServerPlugins.Single(plugin => plugin.Id == "CustomGameSkins");
            var client = PluginRegistry.ClientMods.Single(plugin => plugin.Id == "CustomGameSkinsClient");

            server.RequiresClientMod.Should().BeTrue();
            server.DefaultChecked.Should().BeFalse();
            server.DllNames.Should().ContainSingle().Which.Should().Be("TabgInstaller.CustomGameSkins.Server.dll");
            client.DefaultChecked.Should().BeFalse();
            client.DllNames.Should().ContainSingle().Which.Should().Be("TabgInstaller.CustomGameSkins.Client.dll");
        }

        [Fact]
        public void BuiltIns_ExposeOptInPairedPerformancePlugins()
        {
            PluginRegistry.ResetToBuiltIns();

            var server = PluginRegistry.ServerPlugins.Single(plugin => plugin.Id == "PerformanceServer");
            var client = PluginRegistry.ClientMods.Single(plugin => plugin.Id == "PerformanceClient");

            server.RequiresClientMod.Should().BeTrue();
            server.DefaultChecked.Should().BeFalse();
            server.DllNames.Should().ContainSingle().Which.Should().Be("TabgInstaller.PerformanceServer.dll");
            client.DefaultChecked.Should().BeFalse();
            client.DllNames.Should().ContainSingle().Which.Should().Be("TabgInstaller.PerformanceClient.dll");
        }

        [Fact]
        public void ShootingRangePreset_UsesTestModeAndInfiniteLives()
        {
            var preset = BuiltInPresets.All.Single(item => item.Name == "Multiplayer Shooting Range");
            var settings = preset.Files["game_settings.txt"];

            settings.Should().Contain("GameMode=Test");
            settings.Should().Contain("PlayersToStart=2");
            settings.Should().Contain("NumberOfLivesPerTeam=2147483647");
            preset.RequiredPlugins.Should().Contain("TabgInstaller.RangeMap.Server.dll");
            preset.RequiredClientPlugins.Should().Contain("TabgInstaller.RangeMap.Client.dll");
            preset.DisabledServerPlugins.Should().Contain("TabgInstaller.AntiCheatBypass.dll");
            preset.DisabledServerPlugins.Should().Contain("TabgInstaller.DevTestMap.Server.dll");
            preset.DisabledClientPlugins.Should().Contain("TabgInstaller.DevTestMap.Client.dll");
            preset.Notes.Should().Contain("TabgInstaller.RangeMap.Client");
        }

        [Fact]
        public void ShootingRangePreset_DeploysConfigAndServerPlugin()
        {
            var target = Path.Combine(Path.GetTempPath(), "tabg-range-preset-" + Guid.NewGuid().ToString("N"));
            var preset = BuiltInPresets.All.Single(item => item.Name == "Multiplayer Shooting Range");
            try
            {
                var plugins = Path.Combine(target, "BepInEx", "plugins");
                Directory.CreateDirectory(plugins);
                File.WriteAllText(Path.Combine(plugins, "TabgInstaller.AntiCheatBypass.dll"), "test");
                File.WriteAllText(Path.Combine(plugins, "TabgInstaller.DevTestMap.Server.dll"), "test");

                BuiltInPresets.Deploy(preset, target);

                File.ReadAllText(Path.Combine(target, "game_settings.txt")).Should().Contain("GameMode=Test");
                File.Exists(Path.Combine(target, "BepInEx", "config", "tabginstaller.rangemap.server.cfg")).Should().BeTrue();
                File.Exists(Path.Combine(target, "BepInEx", "plugins", "TabgInstaller.RangeMap.Server.dll")).Should().BeTrue();
                File.Exists(Path.Combine(plugins, "TabgInstaller.AntiCheatBypass.dll")).Should().BeFalse();
                File.Exists(Path.Combine(plugins, "TabgInstaller.AntiCheatBypass.dll.disabled")).Should().BeTrue();
                File.Exists(Path.Combine(plugins, "TabgInstaller.DevTestMap.Server.dll")).Should().BeFalse();
                File.Exists(Path.Combine(plugins, "TabgInstaller.DevTestMap.Server.dll.disabled")).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(target))
                    Directory.Delete(target, true);
            }
        }

        [Fact]
        public void DevTestPreset_UsesTestModeAndInfiniteLives()
        {
            var preset = BuiltInPresets.All.Single(item => item.Name == "Island Map Gun Game");
            var settings = preset.Files["game_settings.txt"];

            settings.Should().Contain("GameMode=Test");
            settings.Should().Contain("AntiCheat=false");
            settings.Should().Contain("PlayersToStart=2");
            settings.Should().Contain("Countdown=0");
            settings.Should().Contain("NumberOfLivesPerTeam=2147483647");
            var devTestConfig = preset.Files[Path.Combine("BepInEx", "config", "tabginstaller.devtestmap.server.cfg")];
            devTestConfig.Should().Contain("RespawnItems =");
            devTestConfig.Should().NotContain("52:1");
            devTestConfig.Should().NotContain("5:255");
            devTestConfig.Should().Contain("WaterDamageEnabled = true");
            devTestConfig.Should().Contain("WaterHeight = 109");
            devTestConfig.Should().Contain("WaterDamagePerSecond = 20");
            devTestConfig.Should().Contain("WaterDamageTickSeconds = 0.1");
            devTestConfig.Should().Contain("[GunGame]");
            devTestConfig.Should().Contain("KillsToWin = 32");
            devTestConfig.Should().Contain("SpawnProtectionSeconds = 1");
            devTestConfig.Should().Contain("Enabled = true");
            devTestConfig.Should().Contain("CastleSpawns = -37,111,-11");
            preset.RequiredPlugins.Should().Contain("TabgInstaller.DevTestMap.Server.dll");
            preset.RequiredClientPlugins.Should().Contain("TabgInstaller.DevTestMap.Client.dll");
            preset.DisabledServerPlugins.Should().Contain("TabgInstaller.AntiCheatBypass.dll");
            preset.DisabledServerPlugins.Should().Contain("TabgInstaller.RangeMap.Server.dll");
            preset.DisabledClientPlugins.Should().Contain("TabgInstaller.RangeMap.Client.dll");
            preset.Notes.Should().Contain("TabgInstaller.DevTestMap.Client");
            preset.Notes.Should().Contain("AntiCheatBypass disabled");
        }

        [Fact]
        public void ShootingRangePreset_ReconcilesPairedClientPlugins()
        {
            var target = Path.Combine(Path.GetTempPath(), "tabg-range-client-preset-" + Guid.NewGuid().ToString("N"));
            var preset = BuiltInPresets.All.Single(item => item.Name == "Multiplayer Shooting Range");
            try
            {
                var plugins = Path.Combine(target, "BepInEx", "plugins");
                Directory.CreateDirectory(plugins);
                File.WriteAllText(Path.Combine(plugins, "TabgInstaller.RangeMap.Client.dll.disabled"), "range");
                File.WriteAllText(Path.Combine(plugins, "TabgInstaller.DevTestMap.Client.dll"), "devtest");

                BuiltInPresets.ReconcileClientPlugins(preset, target);

                File.Exists(Path.Combine(plugins, "TabgInstaller.RangeMap.Client.dll")).Should().BeTrue();
                File.Exists(Path.Combine(plugins, "TabgInstaller.RangeMap.Client.dll.disabled")).Should().BeFalse();
                File.Exists(Path.Combine(plugins, "TabgInstaller.DevTestMap.Client.dll")).Should().BeFalse();
                File.Exists(Path.Combine(plugins, "TabgInstaller.DevTestMap.Client.dll.disabled")).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(target))
                    Directory.Delete(target, true);
            }
        }

        [Fact]
        public void StandardPreset_DisablesTestMapPluginsFromPreviousSession()
        {
            var target = Path.Combine(Path.GetTempPath(), "tabg-standard-client-preset-" + Guid.NewGuid().ToString("N"));
            var preset = BuiltInPresets.All.Single(item => item.Name == "Battle Royale - More Loot");
            try
            {
                var plugins = Path.Combine(target, "BepInEx", "plugins");
                Directory.CreateDirectory(plugins);
                File.WriteAllText(Path.Combine(plugins, "TabgInstaller.RangeMap.Client.dll"), "range");
                File.WriteAllText(Path.Combine(plugins, "TabgInstaller.DevTestMap.Client.dll"), "devtest");

                BuiltInPresets.ReconcileClientPlugins(preset, target);

                File.Exists(Path.Combine(plugins, "TabgInstaller.RangeMap.Client.dll")).Should().BeFalse();
                File.Exists(Path.Combine(plugins, "TabgInstaller.RangeMap.Client.dll.disabled")).Should().BeTrue();
                File.Exists(Path.Combine(plugins, "TabgInstaller.DevTestMap.Client.dll")).Should().BeFalse();
                File.Exists(Path.Combine(plugins, "TabgInstaller.DevTestMap.Client.dll.disabled")).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(target))
                    Directory.Delete(target, true);
            }
        }

        [Fact]
        public void DevTestPreset_DeploysConfigAndServerPlugin()
        {
            var target = Path.Combine(Path.GetTempPath(), "tabg-devtest-preset-" + Guid.NewGuid().ToString("N"));
            var preset = BuiltInPresets.All.Single(item => item.Name == "Island Map Gun Game");
            try
            {
                BuiltInPresets.Deploy(preset, target);

                File.ReadAllText(Path.Combine(target, "game_settings.txt")).Should().Contain("GameMode=Test");
                File.Exists(Path.Combine(target, "BepInEx", "config", "tabginstaller.devtestmap.server.cfg")).Should().BeTrue();
                File.Exists(Path.Combine(target, "BepInEx", "plugins", "TabgInstaller.DevTestMap.Server.dll")).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(target))
                    Directory.Delete(target, true);
            }
        }

        [Fact]
        public void BundledManifestDlls_ExistInPayloadFolders()
        {
            var root = FindRepositoryRoot();
            var manifests = LoadRepositoryManifests();
            var serverPayload = Path.Combine(root, "bundled", "plugins");
            var clientPayload = Path.Combine(root, "bundled", "client-plugins");

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
