using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class BrowsePluginsViewModelTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly Mock<IRegistryService> _registry = new();
        private readonly Mock<IMarketplaceInstallService> _installer = new();
        private readonly Mock<IActiveInstanceService> _activeInstance = new();
        private readonly Mock<IAppSettingsService> _appSettings = new();
        private readonly Mock<IToastService> _toast = new();
        private readonly InstalledPluginTracker _tracker;

        public BrowsePluginsViewModelTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Path.Combine(_tempDir, "BepInEx", "plugins", "community"));
            _tracker = new InstalledPluginTracker(_tempDir);

            _activeInstance.SetupGet(a => a.ServerPath).Returns(_tempDir);
            _appSettings.Setup(a => a.Load()).Returns(new AppSettings { ClientModdedPath = "" });
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private BrowsePluginsViewModel CreateSut() =>
            new(_registry.Object, _installer.Object, _tracker, _activeInstance.Object, _appSettings.Object, _toast.Object);

        private static PluginManifest MakeManifest(string id, string version = "1.0.0", string type = "server")
        {
            return new PluginManifest
            {
                Id = id, Name = $"Plugin {id}", Version = version, Description = $"Desc for {id}",
                Author = "Author", DownloadUrl = $"https://github.com/a/{id}/releases/latest",
                DllNames = new[] { $"{id}.dll" }, Type = type,
                CompatibleTabgVersions = new[] { "*" }, MinInstallerVersion = "4.0.0",
                BepInExVersion = "5.4.22", Tags = new[] { id }
            };
        }

        [Fact]
        public void Constructor_PluginCardsEmpty()
        {
            var sut = CreateSut();
            sut.FilteredPlugins.Should().BeEmpty();
            sut.SearchText.Should().Be("");
            sut.SelectedCategory.Should().Be("All");
        }

        [Fact]
        public async Task LoadPluginsAsync_PopulatesCards()
        {
            var registry = new PluginRegistryResponse
            {
                Version = 1, GeneratedAt = "2026-04-10T12:00:00Z",
                Plugins = new List<PluginManifest> { MakeManifest("alpha"), MakeManifest("beta") }
            };
            _registry.Setup(r => r.FetchRegistryAsync()).ReturnsAsync(registry);

            var sut = CreateSut();
            await sut.LoadPluginsAsync();

            sut.AllPluginCards.Should().HaveCount(2);
            sut.FilteredPlugins.Should().HaveCount(2);
        }

        [Fact]
        public async Task SearchText_FiltersPlugins()
        {
            var registry = new PluginRegistryResponse
            {
                Version = 1, GeneratedAt = "2026-04-10T12:00:00Z",
                Plugins = new List<PluginManifest> { MakeManifest("alpha"), MakeManifest("beta") }
            };
            _registry.Setup(r => r.FetchRegistryAsync()).ReturnsAsync(registry);

            var sut = CreateSut();
            await sut.LoadPluginsAsync();
            sut.SearchText = "alpha";

            sut.FilteredPlugins.Should().ContainSingle(c => c.Manifest.Id == "alpha");
        }

        [Fact]
        public async Task SelectedCategory_FiltersPlugins()
        {
            var registry = new PluginRegistryResponse
            {
                Version = 1, GeneratedAt = "2026-04-10T12:00:00Z",
                Plugins = new List<PluginManifest> { MakeManifest("srv", type: "server"), MakeManifest("cli", type: "client") }
            };
            _registry.Setup(r => r.FetchRegistryAsync()).ReturnsAsync(registry);

            var sut = CreateSut();
            await sut.LoadPluginsAsync();

            sut.SelectedCategory = "Server";
            sut.FilteredPlugins.Should().ContainSingle(c => c.Manifest.Id == "srv");

            sut.SelectedCategory = "Client";
            sut.FilteredPlugins.Should().ContainSingle(c => c.Manifest.Id == "cli");

            sut.SelectedCategory = "All";
            sut.FilteredPlugins.Should().HaveCount(2);
        }

        [Fact]
        public async Task UpdateCount_ReflectsOutdatedPlugins()
        {
            _tracker.AddPlugin("alpha", "0.5.0", new[] { "alpha.dll" });

            var registry = new PluginRegistryResponse
            {
                Version = 1, GeneratedAt = "2026-04-10T12:00:00Z",
                Plugins = new List<PluginManifest> { MakeManifest("alpha", "1.0.0"), MakeManifest("beta") }
            };
            _registry.Setup(r => r.FetchRegistryAsync()).ReturnsAsync(registry);

            var sut = CreateSut();
            await sut.LoadPluginsAsync();

            sut.UpdateCount.Should().Be(1);
        }
    }
}
