using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class SettingsPanelViewModelTests
    {
        private readonly Mock<IAppSettingsService> _appSettings = new();
        private readonly Mock<INavigationService> _navigation = new();
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IThemeService> _themeService = new();

        public SettingsPanelViewModelTests()
        {
            // Default setup so the constructor doesn't NRE on settings.Language
            _appSettings.Setup(a => a.Load()).Returns(new AppSettings());
        }

        private SettingsPanelViewModel CreateSut() =>
            new(_appSettings.Object, _navigation.Object, _serverPath.Object, _themeService.Object);

        [Fact]
        public void OnServerPathChanged_LoadsSettingsIntoPaths()
        {
            _appSettings.Setup(a => a.Load()).Returns(new AppSettings
            {
                ServerPath = @"C:\Server",
                ClientPath = @"C:\Client",
                ClientModdedPath = @"C:\Modded"
            });
            _serverPath.SetupGet(s => s.ServerPath).Returns(@"C:\Server");

            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);

            sut.ServerPath.Should().Be(@"C:\Server");
            sut.ClientPath.Should().Be(@"C:\Client");
            sut.ModdedPath.Should().Be(@"C:\Modded");
        }

        [Fact]
        public void HardResetCommand_CallsResetAndRequestsHardReset()
        {
            var sut = CreateSut();

            sut.HardResetCommand.Execute(null);

            _appSettings.Verify(a => a.Reset(), Times.Once);
            _navigation.Verify(n => n.RequestHardReset(), Times.Once);
        }
    }
}
