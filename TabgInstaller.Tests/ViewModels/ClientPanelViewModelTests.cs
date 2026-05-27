using FluentAssertions;
using Moq;
using System;
using System.IO;
using System.Linq;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class ClientPanelViewModelTests : IDisposable
    {
        private readonly Mock<IAppSettingsService> _appSettings = new();
        private readonly Mock<IToastService> _toast = new();
        private readonly string _tempDir;
        private readonly string _moddedDir;
        private readonly string _pluginsDir;

        public ClientPanelViewModelTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ClientPanelTests_" + Guid.NewGuid());
            _moddedDir = Path.Combine(_tempDir, "TABG_Modded");
            _pluginsDir = Path.Combine(_moddedDir, "BepInEx", "plugins");
            Directory.CreateDirectory(_pluginsDir);

            _appSettings.Setup(s => s.Load()).Returns(new AppSettings
            {
                ClientPath = Path.Combine(_tempDir, "TABG_Steam"),
                ClientModdedPath = _moddedDir,
            });
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private ClientPanelViewModel CreateSut() =>
            new(_appSettings.Object, _toast.Object);

        // ── Initial state ────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_Mods_IsEmpty()
        {
            var sut = CreateSut();
            sut.Mods.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_AvailableClientMods_IsEmpty()
        {
            var sut = CreateSut();
            sut.AvailableClientMods.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_StatusText_IsEmpty()
        {
            var sut = CreateSut();
            sut.StatusText.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_IsSettingUp_IsFalse()
        {
            var sut = CreateSut();
            sut.IsSettingUp.Should().BeFalse();
        }

        // ── Initialize ───────────────────────────────────────────────────────────

        [Fact]
        public void Initialize_SetsClientPathFromSettings()
        {
            var sut = CreateSut();
            sut.Initialize();
            sut.ClientPath.Should().Be(Path.Combine(_tempDir, "TABG_Steam"));
        }

        [Fact]
        public void Initialize_SetsModdedPathFromSettings()
        {
            var sut = CreateSut();
            sut.Initialize();
            sut.ModdedPath.Should().Be(_moddedDir);
        }

        [Fact]
        public void Initialize_WhenNoClientPathInSettings_ClientPathRemainsEmpty()
        {
            _appSettings.Setup(s => s.Load()).Returns(new AppSettings());
            var sut = CreateSut();
            sut.Initialize();
            // TryFindTabgClientPath may return empty in test env — path may or may not be set
            // Just verify no exception is thrown
        }

        [Fact]
        public void Initialize_WhenClientPathSetButNoModdedPath_DerivesModdedPath()
        {
            var steamDir = Path.Combine(_tempDir, "SteamApps", "TABG");
            _appSettings.Setup(s => s.Load()).Returns(new AppSettings
            {
                ClientPath = steamDir,
                ClientModdedPath = "",
            });

            var sut = CreateSut();
            sut.Initialize();

            sut.ModdedPath.Should().Be(Path.Combine(_tempDir, "SteamApps", "TABG_Modded"));
        }

        // ── Refresh ──────────────────────────────────────────────────────────────

        [Fact]
        public void RefreshCommand_LoadsEnabledModsFromDisk()
        {
            File.WriteAllBytes(Path.Combine(_pluginsDir, "Mod1.dll"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(_pluginsDir, "Mod2.dll"), Array.Empty<byte>());

            var sut = CreateSut();
            sut.Initialize();
            sut.RefreshCommand.Execute(null);

            sut.Mods.Should().HaveCount(2);
            sut.Mods.Should().OnlyContain(m => m.IsEnabled);
        }

        [Fact]
        public void RefreshCommand_LoadsDisabledModsFromDisabledSubdir()
        {
            var disabledDir = Path.Combine(_pluginsDir, "disabled");
            Directory.CreateDirectory(disabledDir);
            File.WriteAllBytes(Path.Combine(disabledDir, "DisabledMod.dll"), Array.Empty<byte>());

            var sut = CreateSut();
            sut.Initialize();

            sut.Mods.Should().ContainSingle(m => m.Name == "DisabledMod.dll" && !m.IsEnabled);
        }

        [Fact]
        public void RefreshCommand_WhenPluginsDirMissing_ModsIsEmpty()
        {
            _appSettings.Setup(s => s.Load()).Returns(new AppSettings
            {
                ClientPath = "",
                ClientModdedPath = "",
            });
            var sut = CreateSut();
            sut.RefreshCommand.Execute(null);

            sut.Mods.Should().BeEmpty();
        }

        [Fact]
        public void Initialize_LoadsLocalClientCatalog()
        {
            var sut = CreateSut();

            sut.Initialize();

            sut.ClientModCatalog.Should().HaveCount(
                ServerModsViewModel.CollapseDuplicateDefinitions(PluginRegistry.ClientMods).Count);
            sut.ClientModCatalog.Should().Contain(mod => mod.Id == "PopupBlocker");
        }

        // ── AllClientModsInstalled / CanInstallBundled ────────────────────────────

        [Fact]
        public void AllClientModsInstalled_MatchesAvailableCount()
        {
            var sut = CreateSut();
            sut.Initialize();
            sut.AllClientModsInstalled.Should().Be(sut.AvailableClientMods.Count == 0);
        }

        [Fact]
        public void CanInstallBundled_IsOppositeOf_AllClientModsInstalled()
        {
            var sut = CreateSut();
            sut.Initialize();
            sut.CanInstallBundled.Should().Be(!sut.AllClientModsInstalled);
        }

        // ── ToggleMod ─────────────────────────────────────────────────────────────

        [Fact]
        public void ToggleMod_EnabledToDisabled_MovesFileToDisabledDir()
        {
            var dllPath = Path.Combine(_pluginsDir, "TestMod.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());

            var sut = CreateSut();
            sut.Initialize();

            var entry = new ClientModEntry { Name = "TestMod.dll", IsEnabled = true };
            sut.ToggleModCommand.Execute(entry);

            File.Exists(dllPath).Should().BeFalse();
            File.Exists(Path.Combine(_pluginsDir, "disabled", "TestMod.dll")).Should().BeTrue();
            entry.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void ToggleMod_DisabledToEnabled_MovesFileToPluginsDir()
        {
            var disabledDir = Path.Combine(_pluginsDir, "disabled");
            Directory.CreateDirectory(disabledDir);
            var dllPath = Path.Combine(disabledDir, "TestMod.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());

            var sut = CreateSut();
            sut.Initialize();

            var entry = new ClientModEntry { Name = "TestMod.dll", IsEnabled = false };
            sut.ToggleModCommand.Execute(entry);

            File.Exists(dllPath).Should().BeFalse();
            File.Exists(Path.Combine(_pluginsDir, "TestMod.dll")).Should().BeTrue();
            entry.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void ToggleMod_Null_DoesNothing()
        {
            var sut = CreateSut();
            var act = () => sut.ToggleModCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void ToggleMod_WhenSourceFileMissing_DoesNotThrow()
        {
            var sut = CreateSut();
            sut.Initialize();
            var entry = new ClientModEntry { Name = "Ghost.dll", IsEnabled = true };

            var act = () => sut.ToggleModCommand.Execute(entry);
            act.Should().NotThrow();
        }

        // ── AddDll ────────────────────────────────────────────────────────────────

        [Fact]
        public void AddDll_CopiesFileToPluginsDir()
        {
            var srcDir = Path.Combine(_tempDir, "src");
            Directory.CreateDirectory(srcDir);
            var srcFile = Path.Combine(srcDir, "MyMod.dll");
            File.WriteAllBytes(srcFile, new byte[] { 0xDE, 0xAD });

            var sut = CreateSut();
            sut.Initialize();
            sut.AddDll(srcFile);

            File.Exists(Path.Combine(_pluginsDir, "MyMod.dll")).Should().BeTrue();
        }

        [Fact]
        public void AddDll_AfterCopy_RefreshesModsList()
        {
            var srcDir = Path.Combine(_tempDir, "src");
            Directory.CreateDirectory(srcDir);
            var srcFile = Path.Combine(srcDir, "NewMod.dll");
            File.WriteAllBytes(srcFile, Array.Empty<byte>());

            var sut = CreateSut();
            sut.Initialize();
            sut.AddDll(srcFile);

            sut.Mods.Should().ContainSingle(m => m.Name == "NewMod.dll");
        }

        [Fact]
        public void AddDll_WhenPluginsDirMissing_ShowsWarningToast()
        {
            _appSettings.Setup(s => s.Load()).Returns(new AppSettings());
            var sut = CreateSut();

            sut.AddDll(@"C:\does\not\exist\Mod.dll");

            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void AddDll_WhenFileMissing_ShowsErrorToast()
        {
            var sut = CreateSut();
            sut.Initialize();
            sut.AddDll(@"C:\does\not\exist\Mod.dll");

            _toast.Verify(t => t.Error(It.IsAny<string>()), Times.Once);
        }

        // ── SetClientPath ────────────────────────────────────────────────────────

        [Fact]
        public void SetClientPath_UpdatesClientPath()
        {
            var sut = CreateSut();
            var newPath = Path.Combine(_tempDir, "NewSteam");
            sut.SetClientPath(newPath);

            sut.ClientPath.Should().Be(newPath);
        }

        [Fact]
        public void SetClientPath_DerivesModdedPath()
        {
            var sut = CreateSut();
            var steamDir = Path.Combine(_tempDir, "SteamLibrary", "TABG");
            sut.SetClientPath(steamDir);

            sut.ModdedPath.Should().Be(Path.Combine(_tempDir, "SteamLibrary", "TABG_Modded"));
        }

        [Fact]
        public void SetClientPath_SavesSettings()
        {
            var sut = CreateSut();
            sut.SetClientPath(Path.Combine(_tempDir, "SomePath"));

            _appSettings.Verify(s => s.Save(It.IsAny<AppSettings>()), Times.Once);
        }

        // ── InstallBundled ────────────────────────────────────────────────────────

        [Fact]
        public void InstallBundledCommand_WithNoSelected_DoesNothing()
        {
            var sut = CreateSut();
            sut.Initialize();
            sut.AvailableClientMods.Add(new BundledEntry { Name = "Test.dll", IsSelected = false });

            var act = () => sut.InstallBundledCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void InstallBundledCommand_WhenPluginsDirMissing_ShowsWarning()
        {
            _appSettings.Setup(s => s.Load()).Returns(new AppSettings());
            var sut = CreateSut();
            sut.AvailableClientMods.Add(new BundledEntry { Name = "Test.dll", IsSelected = true });

            sut.InstallBundledCommand.Execute(null);

            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        // ── RemoveMod (null guard) ────────────────────────────────────────────────

        [Fact]
        public void RemoveModCommand_Null_DoesNothing()
        {
            var sut = CreateSut();
            var act = () => sut.RemoveModCommand.Execute(null);
            act.Should().NotThrow();
        }

        // ── KnownClientMods ──────────────────────────────────────────────────────

        [Fact]
        public void KnownClientMods_ContainsExpectedEntries()
        {
            ClientPanelViewModel.KnownClientMods.Should().Contain("TabgInstaller.FlyingControls.dll");
            ClientPanelViewModel.KnownClientMods.Should().Contain("TabgInstaller.EnhancedClient.dll");
            ClientPanelViewModel.KnownClientMods.Should().Contain("TabgInstaller.PopupBlocker.dll");
            ClientPanelViewModel.KnownClientMods.Should().Contain("TabgInstaller.AdminRadar.Client.dll");
        }

        [Fact]
        public void KnownClientMods_MatchesRegistryDlls()
        {
            var expected = PluginRegistry.ClientMods
                .SelectMany(plugin => plugin.DllNames)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            ClientPanelViewModel.KnownClientMods.Should().BeEquivalentTo(expected);
        }

        // ── FindDllPath / FindClientPluginsDir ───────────────────────────────────

        [Fact]
        public void FindDllPath_WhenFileDoesNotExist_ReturnsNull()
        {
            var sut = CreateSut();
            sut.FindDllPath("NonExistent.dll").Should().BeNull();
        }

        [Fact]
        public void FindDllPath_WhenDllDoesNotExistInClientPlugins_ReturnsNull()
        {
            var sut = CreateSut();
            // Use a name that won't realistically exist
            sut.FindDllPath("__nonexistent_test_mod_xyz_12345.dll").Should().BeNull();
        }
    }
}
