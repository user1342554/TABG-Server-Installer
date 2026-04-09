using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class AdminPanelViewModelTests
    {
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IKnownPlayersService> _knownPlayers = new();
        private readonly Mock<IToastService> _toast = new();

        public AdminPanelViewModelTests()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(@"C:\Server");
            _knownPlayers.Setup(k => k.GetPlayerNames()).Returns(new System.Collections.Generic.List<string>());
            _knownPlayers.Setup(k => k.ScanGuestbooks(It.IsAny<string>())).Returns(0);
            _knownPlayers.SetupGet(k => k.Players).Returns(new System.Collections.Generic.List<KnownPlayer>());
        }

        private AdminPanelViewModel CreateSut() =>
            new(_serverPath.Object, _knownPlayers.Object, _toast.Object);

        [Fact]
        public void AddAdmin_WithValidPlayer_AddsToCollection()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            sut.Admins.Should().ContainSingle(a => a.EpicId == "EPIC123");
        }

        [Fact]
        public void AddAdmin_EmptyName_ShowsWarning()
        {
            var sut = CreateSut();
            sut.SelectedPlayerName = "";
            sut.AddAdminCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
            sut.Admins.Should().BeEmpty();
        }

        [Fact]
        public void AddAdmin_DuplicateEpicId_ShowsWarning()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            sut.Admins.Should().HaveCount(1);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void RemoveAdmin_RemovesFromCollection()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            sut.RemoveAdminCommand.Execute(sut.Admins[0]);
            sut.Admins.Should().BeEmpty();
        }

        [Fact]
        public void AddAdmin_PlayerNotInGuestbook_ShowsWarning()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Unknown")).Returns((string?)null);
            var sut = CreateSut();
            sut.SelectedPlayerName = "Unknown";
            sut.AddAdminCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
            sut.Admins.Should().BeEmpty();
        }

        [Fact]
        public void AddManualAdmin_WithValidData_AddsToCollection()
        {
            var sut = CreateSut();
            sut.ManualName = "TestAdmin";
            sut.ManualEpicId = "ABCDEF123";
            sut.AddManualAdminCommand.Execute(null);
            sut.Admins.Should().ContainSingle(a => a.EpicId == "ABCDEF123" && a.Name == "TestAdmin");
        }

        [Fact]
        public void AddManualAdmin_EmptyName_ShowsWarning()
        {
            var sut = CreateSut();
            sut.ManualName = "";
            sut.ManualEpicId = "ABCDEF123";
            sut.AddManualAdminCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
            sut.Admins.Should().BeEmpty();
        }

        [Fact]
        public void AddManualAdmin_EmptyEpicId_ShowsWarning()
        {
            var sut = CreateSut();
            sut.ManualName = "SomePlayer";
            sut.ManualEpicId = "";
            sut.AddManualAdminCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
            sut.Admins.Should().BeEmpty();
        }

        [Fact]
        public void AddManualAdmin_DuplicateEpicId_ShowsWarning()
        {
            var sut = CreateSut();
            sut.ManualName = "Player1";
            sut.ManualEpicId = "DUPE123";
            sut.AddManualAdminCommand.Execute(null);
            sut.ManualName = "Player1Alias";
            sut.ManualEpicId = "DUPE123";
            sut.AddManualAdminCommand.Execute(null);
            sut.Admins.Should().HaveCount(1);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void AddAdmin_ClearsSelectedPlayerNameAfterAdd()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            sut.SelectedPlayerName.Should().Be("");
        }

        [Fact]
        public void AddManualAdmin_ClearsFieldsAfterAdd()
        {
            var sut = CreateSut();
            sut.ManualName = "TestAdmin";
            sut.ManualEpicId = "XYZ789";
            sut.AddManualAdminCommand.Execute(null);
            sut.ManualName.Should().Be("");
            sut.ManualEpicId.Should().Be("");
        }

        [Fact]
        public void RefreshPlayersCommand_ShowsInfoToast()
        {
            var players = new System.Collections.Generic.List<KnownPlayer>
            {
                new KnownPlayer { Name = "A", EpicId = "1" }
            };
            _knownPlayers.SetupGet(k => k.Players).Returns(players);
            _knownPlayers.Setup(k => k.ScanGuestbooks(It.IsAny<string>())).Returns(1);
            _knownPlayers.Setup(k => k.GetPlayerNames()).Returns(new System.Collections.Generic.List<string> { "A" });

            var sut = CreateSut();
            sut.RefreshPlayersCommand.Execute(null);
            _toast.Verify(t => t.Info(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void PathChanged_TriggersRefreshKnownPlayers()
        {
            var names = new System.Collections.Generic.List<string> { "PlayerA", "PlayerB" };
            _knownPlayers.Setup(k => k.GetPlayerNames()).Returns(names);
            _knownPlayers.Setup(k => k.ScanGuestbooks(It.IsAny<string>())).Returns(2);

            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);

            sut.KnownPlayerNames.Should().Contain("PlayerA");
            sut.KnownPlayerNames.Should().Contain("PlayerB");
        }

        [Fact]
        public void AddAdmin_SetsStatusTextAfterAdd()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            sut.StatusText.Should().Be("Added Player1");
        }

        [Fact]
        public void RemoveAdmin_SetsStatusTextAfterRemove()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            var entry = sut.Admins[0];
            sut.RemoveAdminCommand.Execute(entry);
            sut.StatusText.Should().Be("Removed Player1");
        }
    }
}
