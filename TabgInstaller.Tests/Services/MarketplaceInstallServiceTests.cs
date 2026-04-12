using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class MarketplaceInstallServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _communityDir;
        private readonly InstalledPluginTracker _tracker;

        public MarketplaceInstallServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            _communityDir = Path.Combine(_tempDir, "BepInEx", "plugins", "community");
            Directory.CreateDirectory(_communityDir);
            _tracker = new InstalledPluginTracker(_tempDir);

            // Ensure PluginRegistry has the Citruslib entry for bundled-dep test
            PluginRegistry.LoadFromManifests(new List<PluginManifest>
            {
                new() { Id = "Citruslib", Name = "Citruslib", Description = "Core server library",
                    Kind = "core-dependency", Type = "server", DllNames = Array.Empty<string>() },
            });
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private static PluginManifest MakeManifest(string id, string version = "1.0.0", params string[] deps)
        {
            return new PluginManifest
            {
                Id = id,
                Name = id,
                Version = version,
                Description = "Test",
                Author = "Test",
                DownloadUrl = $"https://github.com/test/{id}/releases/latest",
                DllNames = new[] { $"{id}.dll" },
                Type = "server",
                CompatibleTabgVersions = new[] { "*" },
                MinInstallerVersion = "4.0.0",
                BepInExVersion = "5.4.22",
                Dependencies = deps
            };
        }

        // ── Dependency resolution ──────────────────────────────────

        [Fact]
        public void ResolveDependencies_NoDeps_ReturnsOnlyPlugin()
        {
            var plugin = MakeManifest("solo");
            var registry = new List<PluginManifest> { plugin };
            var result = MarketplaceInstallService.ResolveDependencies(plugin, registry, _tracker);
            result.Should().ContainSingle(m => m.Id == "solo");
        }

        [Fact]
        public void ResolveDependencies_WithDeps_ReturnsDepsFirst()
        {
            var dep = MakeManifest("dep-lib");
            var plugin = MakeManifest("main-plugin", "1.0.0", "dep-lib");
            var registry = new List<PluginManifest> { dep, plugin };
            var result = MarketplaceInstallService.ResolveDependencies(plugin, registry, _tracker);
            result.Should().HaveCount(2);
            result[0].Id.Should().Be("dep-lib");
            result[1].Id.Should().Be("main-plugin");
        }

        [Fact]
        public void ResolveDependencies_DepAlreadyInstalled_SkipsDep()
        {
            _tracker.AddPlugin("dep-lib", "1.0.0", new[] { "dep-lib.dll" });
            var dep = MakeManifest("dep-lib");
            var plugin = MakeManifest("main-plugin", "1.0.0", "dep-lib");
            var registry = new List<PluginManifest> { dep, plugin };
            var result = MarketplaceInstallService.ResolveDependencies(plugin, registry, _tracker);
            result.Should().ContainSingle(m => m.Id == "main-plugin");
        }

        [Fact]
        public void ResolveDependencies_CircularDep_DoesNotInfiniteLoop()
        {
            var a = MakeManifest("alpha", "1.0.0", "beta");
            var b = MakeManifest("beta", "1.0.0", "alpha");
            var registry = new List<PluginManifest> { a, b };
            var result = MarketplaceInstallService.ResolveDependencies(a, registry, _tracker);
            result.Select(m => m.Id).Should().Contain("alpha").And.Contain("beta");
            result.Should().HaveCount(2); // no duplicates
        }

        [Fact]
        public void ResolveDependencies_BundledDep_SkipsIt()
        {
            // "Citruslib" is a bundled plugin (PluginRegistry.FindById returns non-null for it)
            var plugin = MakeManifest("my-plugin", "1.0.0", "Citruslib");
            var registry = new List<PluginManifest> { plugin };
            var result = MarketplaceInstallService.ResolveDependencies(plugin, registry, _tracker);
            result.Should().ContainSingle(m => m.Id == "my-plugin");
        }

        // ── HasUpdate ──────────────────────────────────────────────

        [Fact]
        public void HasUpdate_NewerVersion_ReturnsTrue()
        {
            _tracker.AddPlugin("test", "1.0.0", new[] { "test.dll" });
            var manifest = MakeManifest("test", "2.0.0");
            MarketplaceInstallService.HasUpdate(manifest, _tracker).Should().BeTrue();
        }

        [Fact]
        public void HasUpdate_SameVersion_ReturnsFalse()
        {
            _tracker.AddPlugin("test", "1.0.0", new[] { "test.dll" });
            var manifest = MakeManifest("test", "1.0.0");
            MarketplaceInstallService.HasUpdate(manifest, _tracker).Should().BeFalse();
        }

        [Fact]
        public void HasUpdate_Pinned_ReturnsFalse()
        {
            _tracker.AddPlugin("test", "1.0.0", new[] { "test.dll" });
            _tracker.SetPinned("test", true);
            var manifest = MakeManifest("test", "2.0.0");
            MarketplaceInstallService.HasUpdate(manifest, _tracker).Should().BeFalse();
        }

        [Fact]
        public void HasUpdate_NotInstalled_ReturnsFalse()
        {
            var manifest = MakeManifest("test", "1.0.0");
            MarketplaceInstallService.HasUpdate(manifest, _tracker).Should().BeFalse();
        }

        // ── GetCommunityPluginDir ──────────────────────────────────

        [Fact]
        public void GetCommunityPluginDir_Server_ReturnsCorrectPath()
        {
            var dir = MarketplaceInstallService.GetCommunityPluginDir(_tempDir, null, "test-plugin", "server");
            dir.Should().Be(Path.Combine(_tempDir, "BepInEx", "plugins", "community", "test-plugin"));
        }

        [Fact]
        public void GetCommunityPluginDir_Client_ReturnsClientPath()
        {
            var clientPath = Path.Combine(_tempDir, "client");
            var dir = MarketplaceInstallService.GetCommunityPluginDir(null, clientPath, "test-plugin", "client");
            dir.Should().Be(Path.Combine(clientPath, "BepInEx", "plugins", "community", "test-plugin"));
        }
    }
}
