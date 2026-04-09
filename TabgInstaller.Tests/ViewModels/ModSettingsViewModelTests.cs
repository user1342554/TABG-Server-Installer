using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class ModSettingsViewModelTests
    {
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IToastService> _toast = new();

        public ModSettingsViewModelTests()
        {
            // Default: empty path so no I/O happens during construction.
            _serverPath.SetupGet(s => s.ServerPath).Returns(string.Empty);
        }

        private ModSettingsViewModel CreateSut() =>
            new(_serverPath.Object, _toast.Object);

        // ── Initial state ────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_StatusText_IsEmpty()
        {
            var sut = CreateSut();
            sut.StatusText.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_EnableLootDrops_IsFalse()
        {
            var sut = CreateSut();
            sut.EnableLootDrops.Should().BeFalse();
        }

        [Fact]
        public void InitialState_AttackerEnabled_IsFalse()
        {
            var sut = CreateSut();
            sut.AttackerEnabled.Should().BeFalse();
        }

        [Fact]
        public void InitialState_CorpseEnabled_IsFalse()
        {
            var sut = CreateSut();
            sut.CorpseEnabled.Should().BeFalse();
        }

        [Fact]
        public void InitialState_Lives_IsDefault()
        {
            var sut = CreateSut();
            sut.Lives.Should().Be("256");
        }

        [Fact]
        public void InitialState_StreamingDistance_IsDefault()
        {
            var sut = CreateSut();
            sut.StreamingDistance.Should().Be("-1");
        }

        [Fact]
        public void InitialState_BanList_IsEmpty()
        {
            var sut = CreateSut();
            sut.BanList.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_ProxChatMaxRange_IsDefault()
        {
            var sut = CreateSut();
            sut.ProxChatMaxRange.Should().Be("50");
        }

        [Fact]
        public void InitialState_ProxChatMinRange_IsDefault()
        {
            var sut = CreateSut();
            sut.ProxChatMinRange.Should().Be("5");
        }

        [Fact]
        public void InitialState_ProxChatFalloffIndex_IsLinear()
        {
            var sut = CreateSut();
            sut.ProxChatFalloffIndex.Should().Be(0);
        }

        [Fact]
        public void InitialState_JuggPointsToWin_IsDefault()
        {
            var sut = CreateSut();
            sut.JuggPointsToWin.Should().Be("100");
        }

        [Fact]
        public void InitialState_JuggHP_IsDefault()
        {
            var sut = CreateSut();
            sut.JuggHP.Should().Be("1000");
        }

        [Fact]
        public void InitialState_JuggMinPlayers_IsDefault()
        {
            var sut = CreateSut();
            sut.JuggMinPlayers.Should().Be("3");
        }

        [Fact]
        public void InitialState_AttackerGrenadeIndex_IsMinusOne()
        {
            var sut = CreateSut();
            sut.AttackerGrenadeIndex.Should().Be(-1);
        }

        [Fact]
        public void InitialState_CorpseGrenadeIndex_IsMinusOne()
        {
            var sut = CreateSut();
            sut.CorpseGrenadeIndex.Should().Be(-1);
        }

        // ── GrenadeDisplayItems ──────────────────────────────────────────────────

        [Fact]
        public void GrenadeDisplayItems_IsNotEmpty()
        {
            var sut = CreateSut();
            sut.GrenadeDisplayItems.Should().NotBeEmpty();
        }

        [Fact]
        public void GrenadeDisplayItems_ContainsNameAndId()
        {
            var sut = CreateSut();
            // Each item should be "Name (Id)" format
            sut.GrenadeDisplayItems.Should().AllSatisfy(item =>
                item.Should().MatchRegex(@".+\s\(\d+\)"));
        }

        // ── Property round-trips ─────────────────────────────────────────────────

        [Fact]
        public void EnableLootDrops_CanBeToggled()
        {
            var sut = CreateSut();
            sut.EnableLootDrops = true;
            sut.EnableLootDrops.Should().BeTrue();
        }

        [Fact]
        public void AttackerEnabled_CanBeToggled()
        {
            var sut = CreateSut();
            sut.AttackerEnabled = true;
            sut.AttackerEnabled.Should().BeTrue();
        }

        [Fact]
        public void CorpseEnabled_CanBeToggled()
        {
            var sut = CreateSut();
            sut.CorpseEnabled = true;
            sut.CorpseEnabled.Should().BeTrue();
        }

        [Fact]
        public void AttackerChance_CanBeSet()
        {
            var sut = CreateSut();
            sut.AttackerChance = "0.5";
            sut.AttackerChance.Should().Be("0.5");
        }

        [Fact]
        public void CorpseChance_CanBeSet()
        {
            var sut = CreateSut();
            sut.CorpseChance = "0.3";
            sut.CorpseChance.Should().Be("0.3");
        }

        [Fact]
        public void Lives_CanBeSet()
        {
            var sut = CreateSut();
            sut.Lives = "3";
            sut.Lives.Should().Be("3");
        }

        [Fact]
        public void BanList_CanBeSet()
        {
            var sut = CreateSut();
            sut.BanList = "id1\nid2";
            sut.BanList.Should().Be("id1\nid2");
        }

        [Fact]
        public void ProxChatFalloffIndex_CanBeSetToLogarithmic()
        {
            var sut = CreateSut();
            sut.ProxChatFalloffIndex = 1;
            sut.ProxChatFalloffIndex.Should().Be(1);
        }

        [Fact]
        public void JuggPointsToWin_CanBeSet()
        {
            var sut = CreateSut();
            sut.JuggPointsToWin = "200";
            sut.JuggPointsToWin.Should().Be("200");
        }

        [Fact]
        public void JuggHP_CanBeSet()
        {
            var sut = CreateSut();
            sut.JuggHP = "500";
            sut.JuggHP.Should().Be("500");
        }

        [Fact]
        public void AttackerGrenadeIndex_CanBeSet()
        {
            var sut = CreateSut();
            sut.AttackerGrenadeIndex = 0;
            sut.AttackerGrenadeIndex.Should().Be(0);
        }

        [Fact]
        public void StatusText_CanBeSet()
        {
            var sut = CreateSut();
            sut.StatusText = "test";
            sut.StatusText.Should().Be("test");
        }

        // ── Save — no server path ────────────────────────────────────────────────

        [Fact]
        public void SaveCommand_WithEmptyServerPath_ShowsWarningToast()
        {
            var sut = CreateSut();
            sut.SaveCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void SaveCommand_WithEmptyServerPath_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.SaveCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void SaveCommand_WithEmptyServerPath_DoesNotSetStatusText()
        {
            var sut = CreateSut();
            sut.SaveCommand.Execute(null);
            sut.StatusText.Should().BeEmpty();
        }

        // ── ExtractCfgValue static helper ────────────────────────────────────────

        [Fact]
        public void ExtractCfgValue_WithEqualsSign_ReturnsValue()
        {
            ModSettingsViewModel.ExtractCfgValue("MaxRange = 50")
                .Should().Be("50");
        }

        [Fact]
        public void ExtractCfgValue_WithNoEqualsSign_ReturnsEmpty()
        {
            ModSettingsViewModel.ExtractCfgValue("MaxRange")
                .Should().BeEmpty();
        }

        [Fact]
        public void ExtractCfgValue_WithExtraSpaces_TrimsValue()
        {
            ModSettingsViewModel.ExtractCfgValue("HP =  1000  ")
                .Should().Be("1000");
        }

        [Fact]
        public void ExtractCfgValue_WithValueContainingEquals_ReturnsEverythingAfterFirst()
        {
            ModSettingsViewModel.ExtractCfgValue("Key = a=b")
                .Should().Be("a=b");
        }

        // ── PathChanged subscription ─────────────────────────────────────────────

        [Fact]
        public void PathChanged_WhenPathIsEmpty_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => _serverPath.Raise(s => s.PathChanged += null);
            act.Should().NotThrow();
        }

        // ── Dispose ──────────────────────────────────────────────────────────────

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes_DoesNotThrow()
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
        public void Dispose_UnsubscribesFromPathChanged()
        {
            var sut = CreateSut();
            sut.Dispose();
            var act = () => _serverPath.Raise(s => s.PathChanged += null);
            act.Should().NotThrow();
        }
    }
}
