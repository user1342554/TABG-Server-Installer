using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class ConfigViewModelTests
    {
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IToastService> _toast = new();
        private readonly ConfigValidationService _validation = new();

        public ConfigViewModelTests()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(string.Empty);
        }

        private ConfigViewModel CreateSut() =>
            new(_serverPath.Object, _toast.Object, _validation);

        [Fact]
        public void Constructor_EmptyServerPath_GameSettingsVmIsNull()
        {
            var sut = CreateSut();
            sut.GameSettingsVm.Should().BeNull();
        }

        [Fact]
        public void Constructor_StatusTextIsEmpty()
        {
            var sut = CreateSut();
            sut.StatusText.Should().Be(string.Empty);
        }

        [Fact]
        public void SaveCommand_WhenGameSettingsVmIsNull_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.SaveCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void OpenGameSettingsCommand_WhenServerPathEmpty_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.OpenGameSettingsCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void OpenServerFolderCommand_WhenServerPathEmpty_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.OpenServerFolderCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void OpenLogsCommand_WhenServerPathEmpty_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.OpenLogsCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void OpenConfigsCommand_WhenServerPathEmpty_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.OpenConfigsCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void PathChanged_ToNonExistentDir_GameSettingsVmIsNull()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(@"C:\nonexistent_server_path_12345");

            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);

            sut.GameSettingsVm.Should().BeNull();
        }

        [Fact]
        public void Dispose_CanBeCalledTwice_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () =>
            {
                sut.Dispose();
                sut.Dispose();
            };
            act.Should().NotThrow();
        }

        [Fact]
        public void GameSettingsVm_IsObservableProperty_RaisesPropertyChanged()
        {
            var sut = CreateSut();
            var raised = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(sut.GameSettingsVm))
                    raised = true;
            };

            // Setting GameSettingsVm directly should raise PropertyChanged
            sut.GameSettingsVm = null;

            // PropertyChanged should have been raised (even setting to same null value triggers
            // when ObservableObject detects the set; use a non-null then null cycle to guarantee)
            // ObservableObject.SetProperty only fires if value differs. Start at null, set to same = no event.
            // The reliable approach: confirm the property is an [ObservableProperty] by verifying
            // PropertyChanged fires when value actually changes.
            var model = new TabgInstaller.Core.Model.GameSettingsData();
            var vm = new TabgInstaller.Gui.ViewModels.GameSettingsDynamicViewModel(
                model, new TabgInstaller.Core.Services.ConfigValidationService());
            sut.GameSettingsVm = vm;

            raised.Should().BeTrue();
        }

        [Fact]
        public void StatusText_IsObservableProperty_RaisesPropertyChanged()
        {
            var sut = CreateSut();
            var raised = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(sut.StatusText))
                    raised = true;
            };

            sut.StatusText = "Test";

            raised.Should().BeTrue();
        }
    }
}
