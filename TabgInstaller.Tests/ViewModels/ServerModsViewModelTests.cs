using FluentAssertions;
using Moq;
using System;
using System.IO;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class ServerModsViewModelTests : IDisposable
    {
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IToastService> _toast = new();
        private readonly string _tempDir;
        private readonly string _pluginsDir;

        public ServerModsViewModelTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ServerModsTests_" + Guid.NewGuid());
            _pluginsDir = Path.Combine(_tempDir, "BepInEx", "plugins");
            Directory.CreateDirectory(_pluginsDir);

            _serverPath.SetupGet(s => s.ServerPath).Returns(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private ServerModsViewModel CreateSut() =>
            new(_serverPath.Object, _toast.Object);

        // ── Initial state ────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_Plugins_IsEmpty()
        {
            var sut = CreateSut();
            sut.Plugins.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_AvailableMods_IsEmpty()
        {
            var sut = CreateSut();
            sut.AvailableMods.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_StatusText_IsEmpty()
        {
            var sut = CreateSut();
            sut.StatusText.Should().BeEmpty();
        }

        // ── PathChanged / Refresh ────────────────────────────────────────────────

        [Fact]
        public void PathChanged_LoadsEnabledPluginsFromDisk()
        {
            File.WriteAllBytes(Path.Combine(_pluginsDir, "Plugin1.dll"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(_pluginsDir, "Plugin2.dll"), Array.Empty<byte>());

            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);

            sut.Plugins.Should().HaveCount(2);
            sut.Plugins.Should().OnlyContain(p => p.IsEnabled);
        }

        [Fact]
        public void PathChanged_LoadsDisabledPluginsFromDisabledSubdir()
        {
            var disabledDir = Path.Combine(_pluginsDir, "disabled");
            Directory.CreateDirectory(disabledDir);
            File.WriteAllBytes(Path.Combine(disabledDir, "Disabled.dll"), Array.Empty<byte>());

            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);

            sut.Plugins.Should().ContainSingle(p => p.Name == "Disabled.dll" && !p.IsEnabled);
        }

        [Fact]
        public void PathChanged_WhenServerPathEmpty_DoesNotLoad()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(string.Empty);
            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);

            sut.Plugins.Should().BeEmpty();
        }

        [Fact]
        public void RefreshCommand_ReloadsPluginsFromDisk()
        {
            var sut = CreateSut();
            File.WriteAllBytes(Path.Combine(_pluginsDir, "NewPlugin.dll"), Array.Empty<byte>());

            sut.RefreshCommand.Execute(null);

            sut.Plugins.Should().ContainSingle(p => p.Name == "NewPlugin.dll");
        }

        // ── AllPluginsInstalled / CanInstallBundled ──────────────────────────────

        [Fact]
        public void AllPluginsInstalled_MatchesAvailableModsCount()
        {
            var sut = CreateSut();
            // Trigger a load
            _serverPath.Raise(s => s.PathChanged += null);
            // AllPluginsInstalled is true when no available (uninstalled) mods remain
            sut.AllPluginsInstalled.Should().Be(sut.AvailableMods.Count == 0);
        }

        [Fact]
        public void CanInstallBundled_IsOppositeOf_AllPluginsInstalled()
        {
            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);
            sut.CanInstallBundled.Should().Be(!sut.AllPluginsInstalled);
        }

        // ── TogglePlugin ─────────────────────────────────────────────────────────

        [Fact]
        public void TogglePlugin_EnabledToDisabled_MovesFileToDisabledDir()
        {
            var dllPath = Path.Combine(_pluginsDir, "TestPlugin.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());

            var sut = CreateSut();
            var entry = new PluginEntry { Name = "TestPlugin.dll", IsEnabled = true };

            sut.TogglePluginCommand.Execute(entry);

            File.Exists(dllPath).Should().BeFalse();
            File.Exists(Path.Combine(_pluginsDir, "disabled", "TestPlugin.dll")).Should().BeTrue();
            entry.IsEnabled.Should().BeFalse(); // was enabled, now disabled
        }

        [Fact]
        public void TogglePlugin_DisabledToEnabled_MovesFileToPluginsDir()
        {
            var disabledDir = Path.Combine(_pluginsDir, "disabled");
            Directory.CreateDirectory(disabledDir);
            var dllPath = Path.Combine(disabledDir, "TestPlugin.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());

            var sut = CreateSut();
            var entry = new PluginEntry { Name = "TestPlugin.dll", IsEnabled = false };

            sut.TogglePluginCommand.Execute(entry);

            File.Exists(dllPath).Should().BeFalse();
            File.Exists(Path.Combine(_pluginsDir, "TestPlugin.dll")).Should().BeTrue();
            entry.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void TogglePlugin_Null_DoesNothing()
        {
            var sut = CreateSut();
            var act = () => sut.TogglePluginCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void TogglePlugin_WhenSourceFileMissing_DoesNotThrow()
        {
            var sut = CreateSut();
            var entry = new PluginEntry { Name = "Ghost.dll", IsEnabled = true };

            var act = () => sut.TogglePluginCommand.Execute(entry);
            act.Should().NotThrow();
        }

        // ── AddDll ───────────────────────────────────────────────────────────────

        [Fact]
        public void AddDll_CopiesFileToPluginsDir()
        {
            var srcDir = Path.Combine(_tempDir, "src");
            Directory.CreateDirectory(srcDir);
            var srcFile = Path.Combine(srcDir, "MyPlugin.dll");
            File.WriteAllBytes(srcFile, new byte[] { 0xDE, 0xAD });

            var sut = CreateSut();
            sut.AddDll(srcFile);

            File.Exists(Path.Combine(_pluginsDir, "MyPlugin.dll")).Should().BeTrue();
        }

        [Fact]
        public void AddDll_AfterCopy_RefreshesPluginList()
        {
            var srcDir = Path.Combine(_tempDir, "src");
            Directory.CreateDirectory(srcDir);
            var srcFile = Path.Combine(srcDir, "NewPlugin.dll");
            File.WriteAllBytes(srcFile, Array.Empty<byte>());

            var sut = CreateSut();
            sut.AddDll(srcFile);

            sut.Plugins.Should().ContainSingle(p => p.Name == "NewPlugin.dll");
        }

        [Fact]
        public void AddDll_WhenFileMissing_ShowsErrorToast()
        {
            var sut = CreateSut();
            sut.AddDll(@"C:\does\not\exist\Plugin.dll");

            _toast.Verify(t => t.Error(It.IsAny<string>()), Times.Once);
        }

        // ── InstallBundled ───────────────────────────────────────────────────────

        [Fact]
        public void InstallBundledCommand_WithNoSelected_DoesNothing()
        {
            var sut = CreateSut();
            sut.AvailableMods.Add(new BundledEntry { Name = "Test.dll", IsSelected = false });

            var act = () => sut.InstallBundledCommand.Execute(null);
            act.Should().NotThrow();
        }

        // ── RemovePlugin (null guard) ────────────────────────────────────────────

        [Fact]
        public void RemovePluginCommand_Null_DoesNothing()
        {
            var sut = CreateSut();
            var act = () => sut.RemovePluginCommand.Execute(null);
            act.Should().NotThrow();
        }

        // ── KnownServerPlugins ───────────────────────────────────────────────────

        [Fact]
        public void KnownServerPlugins_ContainsCitruslib()
        {
            ServerModsViewModel.KnownServerPlugins.Should().Contain("Citruslib.dll");
        }

        [Fact]
        public void KnownServerPlugins_HasExpectedCount()
        {
            ServerModsViewModel.KnownServerPlugins.Should().HaveCount(12);
        }

        // ── FindDllPath / FindBundledPluginsDir ──────────────────────────────────

        [Fact]
        public void FindDllPath_WhenFileDoesNotExist_ReturnsNull()
        {
            var sut = CreateSut();
            sut.FindDllPath("NonExistent.dll", "plugins").Should().BeNull();
        }

        [Fact]
        public void FindBundledPluginsDir_WhenDirDoesNotExist_ReturnsNull()
        {
            var sut = CreateSut();
            sut.FindBundledPluginsDir("nonexistent_folder_xyz").Should().BeNull();
        }
    }
}
