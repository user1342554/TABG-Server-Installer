using System.Linq;
using FluentAssertions;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class GameSettingsDynamicViewModelTests
    {
        private readonly ConfigValidationService _validation = new();

        private GameSettingsDynamicViewModel CreateSut(GameSettingsData? model = null) =>
            new(model ?? new GameSettingsData(), _validation);

        [Fact]
        public void Constructor_PopulatesProperties()
        {
            var sut = CreateSut();
            sut.Properties.Should().NotBeEmpty();
        }

        [Fact]
        public void ShowAdvanced_Default_IsFalse()
        {
            var sut = CreateSut();
            sut.ShowAdvanced.Should().BeFalse();
        }

        [Fact]
        public void ShowAdvanced_SetTrue_RaisesPropertyChanged()
        {
            var sut = CreateSut();
            var raised = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(sut.ShowAdvanced))
                    raised = true;
            };

            sut.ShowAdvanced = true;

            raised.Should().BeTrue();
        }

        [Fact]
        public void ShowGameModeWarning_BattleRoyaleMode_ReturnsFalse()
        {
            var model = new GameSettingsData { GameMode = "BattleRoyale" };
            var sut = CreateSut(model);
            sut.ShowGameModeWarning.Should().BeFalse();
        }

        [Fact]
        public void ShowGameModeWarning_BombMode_ReturnsTrue()
        {
            var model = new GameSettingsData { GameMode = "Bomb" };
            var sut = CreateSut(model);
            sut.ShowGameModeWarning.Should().BeTrue();
        }

        [Fact]
        public void GameModeWarningText_BattleRoyaleMode_ReturnsEmpty()
        {
            var model = new GameSettingsData { GameMode = "BattleRoyale" };
            var sut = CreateSut(model);
            sut.GameModeWarningText.Should().BeEmpty();
        }

        [Fact]
        public void GameModeWarningText_BombMode_ReturnsWarning()
        {
            var model = new GameSettingsData { GameMode = "Bomb" };
            var sut = CreateSut(model);
            sut.GameModeWarningText.Should().Contain("Bomb");
        }

        [Fact]
        public void ToModel_ReturnsSameModel()
        {
            var model = new GameSettingsData { ServerName = "TestServer" };
            var sut = CreateSut(model);
            sut.ToModel().Should().BeSameAs(model);
        }

        [Fact]
        public void UpdateFromGameSettings_UpdatesPropertyValues()
        {
            var model = new GameSettingsData { ServerName = "OldName" };
            var sut = CreateSut(model);

            var newModel = new GameSettingsData { ServerName = "NewName" };
            sut.UpdateFromGameSettings(newModel);

            var serverNameProp = sut.Properties.FirstOrDefault(p => p.Name == "ServerName");
            serverNameProp.Should().NotBeNull();
            serverNameProp!.ValueString.Should().Be("NewName");
        }

        [Fact]
        public void Properties_ContainsServerName()
        {
            var sut = CreateSut();
            sut.Properties.Should().Contain(p => p.Name == "ServerName");
        }

        [Fact]
        public void Properties_ContainsGameMode()
        {
            var sut = CreateSut();
            sut.Properties.Should().Contain(p => p.Name == "GameMode");
        }

        [Fact]
        public void ImplementsINotifyPropertyChanged()
        {
            var sut = CreateSut();
            sut.Should().BeAssignableTo<System.ComponentModel.INotifyPropertyChanged>();
        }
    }
}
