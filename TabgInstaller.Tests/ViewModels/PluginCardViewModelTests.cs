using FluentAssertions;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class PluginCardViewModelTests
    {
        private static PluginManifest MakeManifest(string id = "test", string version = "1.0.0", string type = "server")
        {
            return new PluginManifest
            {
                Id = id,
                Name = "Test Plugin",
                Version = version,
                Description = "A test plugin for testing.",
                Author = "TestAuthor",
                DownloadUrl = "https://github.com/Test/Plugin/releases/latest",
                DllNames = new[] { "Test.dll" },
                Type = type,
                CompatibleTabgVersions = new[] { "*" },
                MinInstallerVersion = "4.0.0",
                BepInExVersion = "5.4.22",
                Tags = new[] { "test", "demo" }
            };
        }

        [Fact]
        public void Constructor_NotInstalled_StatusIsAvailable()
        {
            var vm = new PluginCardViewModel(MakeManifest(), null, "4.0.0");
            vm.InstallStatus.Should().Be(PluginInstallStatus.Available);
            vm.ActionButtonText.Should().Be("Install");
            vm.IsActionEnabled.Should().BeTrue();
        }

        [Fact]
        public void Constructor_InstalledSameVersion_StatusIsInstalled()
        {
            var entry = new InstalledPluginEntry
            {
                Id = "test", InstalledVersion = "1.0.0", DllNames = new[] { "Test.dll" }
            };
            var vm = new PluginCardViewModel(MakeManifest(), entry, "4.0.0");
            vm.InstallStatus.Should().Be(PluginInstallStatus.Installed);
            vm.ActionButtonText.Should().Be("Installed \u2713");
            vm.IsActionEnabled.Should().BeFalse();
        }

        [Fact]
        public void Constructor_NewerVersionAvailable_StatusIsUpdateAvailable()
        {
            var entry = new InstalledPluginEntry
            {
                Id = "test", InstalledVersion = "0.9.0", DllNames = new[] { "Test.dll" }
            };
            var vm = new PluginCardViewModel(MakeManifest(), entry, "4.0.0");
            vm.InstallStatus.Should().Be(PluginInstallStatus.UpdateAvailable);
            vm.ActionButtonText.Should().Be("Update (0.9.0 \u2192 1.0.0)");
            vm.IsActionEnabled.Should().BeTrue();
        }

        [Fact]
        public void Constructor_IncompatibleInstallerVersion_StatusIsIncompatible()
        {
            var manifest = MakeManifest();
            manifest.MinInstallerVersion = "5.0.0";
            var vm = new PluginCardViewModel(manifest, null, "4.0.0");
            vm.InstallStatus.Should().Be(PluginInstallStatus.Incompatible);
            vm.IsActionEnabled.Should().BeFalse();
            vm.IncompatibilityReason.Should().Contain("5.0.0");
        }

        [Fact]
        public void Constructor_Pinned_ShowsPinned()
        {
            var entry = new InstalledPluginEntry
            {
                Id = "test", InstalledVersion = "0.9.0", Pinned = true, DllNames = new[] { "Test.dll" }
            };
            var vm = new PluginCardViewModel(MakeManifest(), entry, "4.0.0");
            vm.IsPinned.Should().BeTrue();
            vm.InstallStatus.Should().Be(PluginInstallStatus.Installed);
        }

        [Fact]
        public void TypeBadge_ReturnsCorrectString()
        {
            var vm = new PluginCardViewModel(MakeManifest(type: "server"), null, "4.0.0");
            vm.TypeBadge.Should().Be("Server");

            vm = new PluginCardViewModel(MakeManifest(type: "client"), null, "4.0.0");
            vm.TypeBadge.Should().Be("Client");

            vm = new PluginCardViewModel(MakeManifest(type: "both"), null, "4.0.0");
            vm.TypeBadge.Should().Be("Both");
        }

        [Fact]
        public void MatchesSearch_ByName_ReturnsTrue()
        {
            var vm = new PluginCardViewModel(MakeManifest(), null, "4.0.0");
            vm.MatchesSearch("test plugin").Should().BeTrue();
            vm.MatchesSearch("xyz").Should().BeFalse();
        }

        [Fact]
        public void MatchesSearch_ByTag_ReturnsTrue()
        {
            var vm = new PluginCardViewModel(MakeManifest(), null, "4.0.0");
            vm.MatchesSearch("demo").Should().BeTrue();
        }

        [Fact]
        public void MatchesSearch_EmptyQuery_ReturnsTrue()
        {
            var vm = new PluginCardViewModel(MakeManifest(), null, "4.0.0");
            vm.MatchesSearch("").Should().BeTrue();
            vm.MatchesSearch(null!).Should().BeTrue();
        }
    }
}
