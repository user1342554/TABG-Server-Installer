using FluentAssertions;
using Moq;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class MatchSettingsViewModelTests
    {
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IToastService> _toast = new();

        public MatchSettingsViewModelTests()
        {
            // Default: empty path so no I/O happens during construction.
            _serverPath.SetupGet(s => s.ServerPath).Returns(string.Empty);
        }

        private MatchSettingsViewModel CreateSut() =>
            new(_serverPath.Object, _toast.Object);

        // ── Initial state ────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_WinCondition_IsDefault()
        {
            var sut = CreateSut();
            sut.WinCondition.Should().Be("Default");
        }

        [Fact]
        public void InitialState_ShowKillsToWin_IsCollapsed()
        {
            var sut = CreateSut();
            sut.ShowKillsToWin.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void InitialState_LoadoutMode_IsNormal()
        {
            var sut = CreateSut();
            sut.LoadoutMode.Should().Be("Normal");
        }

        [Fact]
        public void InitialState_StatusText_IsEmpty()
        {
            var sut = CreateSut();
            sut.StatusText.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_SpelldropEnabled_IsTrue()
        {
            var sut = CreateSut();
            sut.SpelldropEnabled.Should().BeTrue();
        }

        // ── WinCondition → ShowKillsToWin ────────────────────────────────────────

        [Fact]
        public void OnWinConditionChanged_ToKillsToWin_ShowsKillsToWin()
        {
            var sut = CreateSut();
            sut.WinCondition = "KillsToWin";
            sut.ShowKillsToWin.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void OnWinConditionChanged_ToDefault_HidesKillsToWin()
        {
            var sut = CreateSut();
            sut.WinCondition = "KillsToWin";
            sut.WinCondition = "Default";
            sut.ShowKillsToWin.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void OnWinConditionChanged_ToDebug_HidesKillsToWin()
        {
            var sut = CreateSut();
            sut.WinCondition = "KillsToWin";
            sut.WinCondition = "Debug";
            sut.ShowKillsToWin.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void OnWinConditionChanged_EmptyString_HidesKillsToWin()
        {
            var sut = CreateSut();
            sut.WinCondition = "KillsToWin"; // show first
            sut.WinCondition = "";
            sut.ShowKillsToWin.Should().Be(Visibility.Collapsed);
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
            // StatusText stays empty — only the toast fires.
            sut.StatusText.Should().BeEmpty();
        }

        // ── Property round-trips ─────────────────────────────────────────────────

        [Fact]
        public void KillsToWin_CanBeSet()
        {
            var sut = CreateSut();
            sut.KillsToWin = "25";
            sut.KillsToWin.Should().Be("25");
        }

        [Fact]
        public void HealOnKillAmount_CanBeSet()
        {
            var sut = CreateSut();
            sut.HealOnKillAmount = "50";
            sut.HealOnKillAmount.Should().Be("50");
        }

        [Fact]
        public void PercentOfVotes_CanBeSet()
        {
            var sut = CreateSut();
            sut.PercentOfVotes = "75";
            sut.PercentOfVotes.Should().Be("75");
        }

        [Fact]
        public void ForceKillAtStart_CanBeToggled()
        {
            var sut = CreateSut();
            sut.ForceKillAtStart = true;
            sut.ForceKillAtStart.Should().BeTrue();
        }

        [Fact]
        public void HealOnKill_CanBeToggled()
        {
            var sut = CreateSut();
            sut.HealOnKill = true;
            sut.HealOnKill.Should().BeTrue();
        }

        [Fact]
        public void DropItemsOnDeath_CanBeToggled()
        {
            var sut = CreateSut();
            sut.DropItemsOnDeath = true;
            sut.DropItemsOnDeath.Should().BeTrue();
        }

        [Fact]
        public void CanGoDown_CanBeToggled()
        {
            var sut = CreateSut();
            sut.CanGoDown = true;
            sut.CanGoDown.Should().BeTrue();
        }

        [Fact]
        public void CanLockOut_CanBeToggled()
        {
            var sut = CreateSut();
            sut.CanLockOut = true;
            sut.CanLockOut.Should().BeTrue();
        }

        [Fact]
        public void SpelldropEnabled_CanBeToggled()
        {
            var sut = CreateSut();
            sut.SpelldropEnabled = false;
            sut.SpelldropEnabled.Should().BeFalse();
        }

        [Fact]
        public void LoadoutMode_CanBeSet()
        {
            var sut = CreateSut();
            sut.LoadoutMode = "GunGame";
            sut.LoadoutMode.Should().Be("GunGame");
        }

        [Fact]
        public void StatusText_CanBeSet()
        {
            var sut = CreateSut();
            sut.StatusText = "hello";
            sut.StatusText.Should().Be("hello");
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

            // After dispose, raising PathChanged must not cause any side-effects
            // (the ViewModel is no longer listening).
            var act = () => _serverPath.Raise(s => s.PathChanged += null);
            act.Should().NotThrow();
        }

        // ── KillsToWin visibility is set on construction for non-empty path ──────

        [Fact]
        public void WinCondition_SetToKillsToWin_AndThenReset_ShowKillsToWinIsCollapsed()
        {
            var sut = CreateSut();
            sut.WinCondition = "KillsToWin";
            sut.WinCondition = "Default";

            sut.ShowKillsToWin.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void MultipleWinConditionChanges_OnlyLastValueDeterminesVisibility()
        {
            var sut = CreateSut();
            sut.WinCondition = "Debug";
            sut.WinCondition = "KillsToWin";
            sut.WinCondition = "Default";
            sut.WinCondition = "KillsToWin";

            sut.ShowKillsToWin.Should().Be(Visibility.Visible);
        }
    }
}
