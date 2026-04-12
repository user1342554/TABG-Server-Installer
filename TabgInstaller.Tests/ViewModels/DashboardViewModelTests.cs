using FluentAssertions;
using Moq;
using System.Collections.ObjectModel;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class DashboardViewModelTests
    {
        private readonly Mock<IServerProcessService> _procSvc = new();
        private readonly Mock<IActiveInstanceService> _activeInstance = new();
        private readonly Mock<IAppSettingsService> _appSettings = new();
        private readonly Mock<INavigationService> _navigation = new();
        private readonly Mock<IToastService> _toast = new();
        private readonly Mock<IServerInstanceManager> _instanceManager = new();
        private readonly Mock<IRegistryService> _registryService = new();
        private readonly Mock<IInstalledPluginTracker> _pluginTracker = new();

        public DashboardViewModelTests()
        {
            _activeInstance.SetupGet(a => a.ServerPath).Returns(@"C:\Server");
            _activeInstance.SetupGet(a => a.ProcessService).Returns(_procSvc.Object);
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            _procSvc.Setup(p => p.GetRecentText(It.IsAny<int>())).Returns("");
            _instanceManager.SetupGet(m => m.InstanceDataList)
                .Returns(new ObservableCollection<ServerInstanceData>());
        }

        private DashboardViewModel CreateSut() =>
            new(_activeInstance.Object, _appSettings.Object,
                _navigation.Object, _toast.Object, _instanceManager.Object,
                _registryService.Object, _pluginTracker.Object);

        [Fact]
        public void IsServerRunning_DelegatesToProcessService()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning.Should().BeTrue();
        }

        [Fact]
        public void IsServerRunning_WhenNotRunning_ReturnsFalse()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            var sut = CreateSut();
            sut.IsServerRunning.Should().BeFalse();
        }

        [Fact]
        public void StartStopCommand_WhenNotRunning_StartsServer()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            var sut = CreateSut();
            sut.StartStopCommand.Execute(null);
            _procSvc.Verify(p => p.Start(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void StartStopCommand_WhenRunning_StopsServer()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.StartStopCommand.Execute(null);
            _procSvc.Verify(p => p.Stop(), Times.Once);
        }

        [Fact]
        public void OpenFullConsoleCommand_NavigatesToConsoleTab()
        {
            var sut = CreateSut();
            sut.OpenFullConsoleCommand.Execute(null);
            _navigation.Verify(n => n.NavigateToTab(4), Times.Once);
        }

        [Fact]
        public void StartStopButtonText_Default_IsStartServer()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            var sut = CreateSut();
            sut.StartStopButtonText.Should().Be("Start Server");
        }

        [Fact]
        public void LaunchClientCommand_WhenClientModdedPathEmpty_ShowsWarning()
        {
            _appSettings.Setup(a => a.Load()).Returns(new AppSettings { ClientModdedPath = "" });
            var sut = CreateSut();
            sut.LaunchClientCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void OpenServerFolderCommand_CanExecute_WhenServerPathSet()
        {
            _activeInstance.SetupGet(a => a.ServerPath).Returns(@"C:\Server");
            var sut = CreateSut();
            sut.OpenServerFolderCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void PathChanged_FiresPreviewTextUpdate()
        {
            _procSvc.Setup(p => p.GetRecentText(20)).Returns("some log text");
            var sut = CreateSut();
            _activeInstance.Raise(a => a.PathChanged += null);
            sut.PreviewText.Should().Be("some log text");
        }

        [Fact]
        public void PathChanged_UpdatesStartStopButtonText_WhenRunning()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            _procSvc.Setup(p => p.GetRecentText(20)).Returns("");
            var sut = CreateSut();
            _activeInstance.Raise(a => a.PathChanged += null);
            sut.StartStopButtonText.Should().Be("Stop Server");
        }
    }
}
