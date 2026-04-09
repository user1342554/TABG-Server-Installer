using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class PresetsViewModelTests
    {
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IToastService> _toast = new();

        public PresetsViewModelTests()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(string.Empty);
        }

        private PresetsViewModel CreateSut() =>
            new(_serverPath.Object, _toast.Object);

        [Fact]
        public void Constructor_EmptyServerPath_PresetNamesIsEmpty()
        {
            var sut = CreateSut();
            sut.PresetNames.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_SelectedPresetIsNull()
        {
            var sut = CreateSut();
            sut.SelectedPreset.Should().BeNull();
        }

        [Fact]
        public void LoadPresetCommand_NoPresetSelected_ShowsWarning()
        {
            var sut = CreateSut();
            sut.SelectedPreset = null;
            sut.LoadPresetCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void DeletePresetCommand_NoPresetSelected_ShowsWarning()
        {
            var sut = CreateSut();
            sut.SelectedPreset = null;
            sut.DeletePresetCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ApplyTemplateCommand_NullPreset_ShowsWarning()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(@"C:\server");
            var sut = CreateSut();
            sut.ApplyTemplateCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ApplyTemplateCommand_EmptyServerPath_ShowsWarning()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(string.Empty);
            var sut = CreateSut();
            sut.ApplyTemplateCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void PresetNames_IsObservableProperty_RaisesPropertyChanged()
        {
            var sut = CreateSut();
            var raised = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(sut.PresetNames))
                    raised = true;
            };

            sut.PresetNames = new System.Collections.ObjectModel.ObservableCollection<string> { "test" };

            raised.Should().BeTrue();
        }

        [Fact]
        public void SelectedPreset_IsObservableProperty_RaisesPropertyChanged()
        {
            var sut = CreateSut();
            var raised = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(sut.SelectedPreset))
                    raised = true;
            };

            sut.SelectedPreset = "some-preset";

            raised.Should().BeTrue();
        }

        [Fact]
        public void PathChanged_ToNonExistentDir_PresetNamesRemainsEmpty()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(@"C:\nonexistent_server_path_12345");
            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);
            sut.PresetNames.Should().BeEmpty();
        }
    }
}
