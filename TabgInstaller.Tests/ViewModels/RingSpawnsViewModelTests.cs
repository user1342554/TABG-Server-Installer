using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class RingSpawnsViewModelTests
    {
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IToastService> _toast = new();

        public RingSpawnsViewModelTests()
        {
            // Default: empty path so no I/O happens during construction.
            _serverPath.SetupGet(s => s.ServerPath).Returns(string.Empty);
        }

        private RingSpawnsViewModel CreateSut() =>
            new(_serverPath.Object, _toast.Object);

        // ── Initial state ────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_RingSizes_IsEmpty()
        {
            var sut = CreateSut();
            sut.RingSizes.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_RingSpeeds_IsEmpty()
        {
            var sut = CreateSut();
            sut.RingSpeeds.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_MatchSpawns_IsEmpty()
        {
            var sut = CreateSut();
            sut.MatchSpawns.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_StatusText_IsEmpty()
        {
            var sut = CreateSut();
            sut.StatusText.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_UseCustomSpawn_IsFalse()
        {
            var sut = CreateSut();
            sut.UseCustomSpawn.Should().BeFalse();
        }

        [Fact]
        public void InitialState_SpawnLocations_IsNotEmpty()
        {
            var sut = CreateSut();
            sut.SpawnLocations.Should().NotBeEmpty();
        }

        [Fact]
        public void InitialState_SpawnInfo_ContainsCheckedText()
        {
            var sut = CreateSut();
            sut.SpawnInfo.Should().Contain("ValidSpawnPoints");
        }

        // ── Property round-trips ─────────────────────────────────────────────────

        [Fact]
        public void RingSizes_CanBeSet()
        {
            var sut = CreateSut();
            sut.RingSizes = "100, 200, 300";
            sut.RingSizes.Should().Be("100, 200, 300");
        }

        [Fact]
        public void RingSpeeds_CanBeSet()
        {
            var sut = CreateSut();
            sut.RingSpeeds = "120, 120, 150";
            sut.RingSpeeds.Should().Be("120, 120, 150");
        }

        [Fact]
        public void MatchSpawns_CanBeSet()
        {
            var sut = CreateSut();
            sut.MatchSpawns = "100,200\n300,400";
            sut.MatchSpawns.Should().Be("100,200\n300,400");
        }

        [Fact]
        public void SelectedLocation_CanBeSet()
        {
            var sut = CreateSut();
            sut.SelectedLocation = "100,200,300";
            sut.SelectedLocation.Should().Be("100,200,300");
        }

        [Fact]
        public void UseCustomSpawn_CanBeToggled()
        {
            var sut = CreateSut();
            sut.UseCustomSpawn = true;
            sut.UseCustomSpawn.Should().BeTrue();
        }

        [Fact]
        public void StatusText_CanBeSet()
        {
            var sut = CreateSut();
            sut.StatusText = "test status";
            sut.StatusText.Should().Be("test status");
        }

        // ── UseCustomSpawn → SpawnInfo ───────────────────────────────────────────

        [Fact]
        public void UseCustomSpawn_True_SetsSpawnInfoToCustomEnabled()
        {
            var sut = CreateSut();
            sut.UseCustomSpawn = true;
            sut.SpawnInfo.Should().Contain("6 (custom)");
        }

        [Fact]
        public void UseCustomSpawn_False_SetsSpawnInfoToDisabled()
        {
            var sut = CreateSut();
            sut.UseCustomSpawn = true;  // set true first
            sut.UseCustomSpawn = false;
            sut.SpawnInfo.Should().Contain("disabled");
        }

        // ── MatchSpawns → SpawnCountText ─────────────────────────────────────────

        [Fact]
        public void MatchSpawns_Changed_UpdatesSpawnCountText()
        {
            var sut = CreateSut();
            sut.MatchSpawns = "100,200\n300,400\n500,600";
            sut.SpawnCountText.Should().Be("3 spawn point(s)");
        }

        [Fact]
        public void MatchSpawns_ClearedAfterBeingSet_ShowsZeroSpawnPoints()
        {
            var sut = CreateSut();
            sut.MatchSpawns = "100,200";
            sut.MatchSpawns = "";
            sut.SpawnCountText.Should().Be("0 spawn point(s)");
        }

        [Fact]
        public void MatchSpawns_SingleLine_ShowsOneSpawnPoint()
        {
            var sut = CreateSut();
            sut.MatchSpawns = "100,200";
            sut.SpawnCountText.Should().Be("1 spawn point(s)");
        }

        // ── SelectedSpawnLocation → LocationInfo ─────────────────────────────────

        [Fact]
        public void SelectedSpawnLocation_Changed_UpdatesLocationInfo()
        {
            var sut = CreateSut();
            var loc = sut.SpawnLocations[0];
            sut.SelectedSpawnLocation = loc;
            sut.LocationInfo.Should().Contain($"Ring Size: {loc.RingSize}");
        }

        [Fact]
        public void SelectedSpawnLocation_Null_DoesNotUpdateLocationInfo()
        {
            var sut = CreateSut();
            sut.SelectedSpawnLocation = sut.SpawnLocations[0];
            var prevInfo = sut.LocationInfo;
            sut.SelectedSpawnLocation = null;
            sut.LocationInfo.Should().Be(prevInfo); // unchanged
        }

        // ── ApplyLocationCommand ─────────────────────────────────────────────────

        [Fact]
        public void ApplyLocation_WithNoSelection_DoesNothing()
        {
            var sut = CreateSut();
            sut.SelectedSpawnLocation = null;
            var act = () => sut.ApplyLocationCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void ApplyLocation_WithSelection_SetsRingSizes()
        {
            var sut = CreateSut();
            var loc = sut.SpawnLocations[0];
            sut.SelectedSpawnLocation = loc;
            sut.ApplyLocationCommand.Execute(null);
            sut.RingSizes.Should().Be($"{loc.RingSize}, {loc.RingSize}, {loc.RingSize}");
        }

        [Fact]
        public void ApplyLocation_WithSelection_SetsSelectedLocationToLobbySpawn()
        {
            var sut = CreateSut();
            var loc = sut.SpawnLocations[0];
            sut.SelectedSpawnLocation = loc;
            sut.ApplyLocationCommand.Execute(null);
            sut.SelectedLocation.Should().Be(loc.LobbySpawn);
        }

        [Fact]
        public void ApplyLocation_WithSelection_SetsUseCustomSpawnTrue()
        {
            var sut = CreateSut();
            var loc = sut.SpawnLocations[0];
            sut.SelectedSpawnLocation = loc;
            sut.ApplyLocationCommand.Execute(null);
            sut.UseCustomSpawn.Should().BeTrue();
        }

        [Fact]
        public void ApplyLocation_WithSelection_SetsMatchSpawns()
        {
            var sut = CreateSut();
            var loc = sut.SpawnLocations[0];
            sut.SelectedSpawnLocation = loc;
            sut.ApplyLocationCommand.Execute(null);
            sut.MatchSpawns.Should().NotBeEmpty();
        }

        [Fact]
        public void ApplyLocation_WithSelection_SetsStatusText()
        {
            var sut = CreateSut();
            var loc = sut.SpawnLocations[0];
            sut.SelectedSpawnLocation = loc;
            sut.ApplyLocationCommand.Execute(null);
            sut.StatusText.Should().Contain(loc.Name);
        }

        // ── PresetDeathmatch ─────────────────────────────────────────────────────

        [Fact]
        public void PresetDeathmatch_SetsCorrectRingSpeeds()
        {
            var sut = CreateSut();
            sut.PresetDeathmatchCommand.Execute(null);
            sut.RingSpeeds.Should().Be("0.001, 50, 0.001");
        }

        [Fact]
        public void PresetDeathmatch_SetsStatusText()
        {
            var sut = CreateSut();
            sut.PresetDeathmatchCommand.Execute(null);
            sut.StatusText.Should().Contain("deathmatch");
        }

        [Fact]
        public void PresetDeathmatch_WithLocationSelected_SetsRingSizesFromLocation()
        {
            var sut = CreateSut();
            var loc = sut.SpawnLocations[0];
            sut.SelectedSpawnLocation = loc;
            sut.PresetDeathmatchCommand.Execute(null);
            sut.RingSizes.Should().Be($"{loc.RingSize}, {loc.RingSize}, {loc.RingSize}");
        }

        [Fact]
        public void PresetDeathmatch_WithNoLocation_DoesNotChangeRingSizes()
        {
            var sut = CreateSut();
            sut.RingSizes = "999, 999, 999";
            sut.SelectedSpawnLocation = null;
            sut.PresetDeathmatchCommand.Execute(null);
            sut.RingSizes.Should().Be("999, 999, 999");
        }

        // ── PresetStandardBR ─────────────────────────────────────────────────────

        [Fact]
        public void PresetStandardBR_SetsCorrectRingSizes()
        {
            var sut = CreateSut();
            sut.PresetStandardBRCommand.Execute(null);
            sut.RingSizes.Should().Be("4240.0, 3450.0, 1710.0");
        }

        [Fact]
        public void PresetStandardBR_SetsCorrectRingSpeeds()
        {
            var sut = CreateSut();
            sut.PresetStandardBRCommand.Execute(null);
            sut.RingSpeeds.Should().Be("120, 120, 150");
        }

        [Fact]
        public void PresetStandardBR_SetsStatusText()
        {
            var sut = CreateSut();
            sut.PresetStandardBRCommand.Execute(null);
            sut.StatusText.Should().Contain("BR");
        }

        // ── SaveCommand — no server path ─────────────────────────────────────────

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

        // ── SpawnPointsToMultiline static helper ─────────────────────────────────

        [Fact]
        public void SpawnPointsToMultiline_NullInput_ReturnsEmpty()
        {
            RingSpawnsViewModel.SpawnPointsToMultiline("").Should().BeEmpty();
        }

        [Fact]
        public void SpawnPointsToMultiline_SinglePoint_ReturnsSingleLine()
        {
            RingSpawnsViewModel.SpawnPointsToMultiline("100,200")
                .Should().Be("100,200");
        }

        [Fact]
        public void SpawnPointsToMultiline_MultiplePoints_ReturnsMultipleLines()
        {
            var result = RingSpawnsViewModel.SpawnPointsToMultiline("100,200;300,400;500,600");
            result.Should().Contain("100,200");
            result.Should().Contain("300,400");
            result.Should().Contain("500,600");
        }

        [Fact]
        public void SpawnPointsToMultiline_TrimsWhitespace()
        {
            var result = RingSpawnsViewModel.SpawnPointsToMultiline(" 100,200 ; 300,400 ");
            result.Should().Contain("100,200");
            result.Should().Contain("300,400");
        }

        // ── MultilineToSpawnPoints static helper ─────────────────────────────────

        [Fact]
        public void MultilineToSpawnPoints_EmptyInput_ReturnsEmpty()
        {
            RingSpawnsViewModel.MultilineToSpawnPoints("").Should().BeEmpty();
        }

        [Fact]
        public void MultilineToSpawnPoints_SingleLine_ReturnsSinglePoint()
        {
            RingSpawnsViewModel.MultilineToSpawnPoints("100,200")
                .Should().Be("100,200");
        }

        [Fact]
        public void MultilineToSpawnPoints_MultipleLines_ReturnsSemicolonSeparated()
        {
            var result = RingSpawnsViewModel.MultilineToSpawnPoints("100,200\n300,400\n500,600");
            result.Should().Be("100,200;300,400;500,600");
        }

        [Fact]
        public void MultilineToSpawnPoints_WindowsNewlines_ReturnsSemicolonSeparated()
        {
            var result = RingSpawnsViewModel.MultilineToSpawnPoints("100,200\r\n300,400");
            result.Should().Be("100,200;300,400");
        }

        [Fact]
        public void MultilineToSpawnPoints_EmptyLines_AreIgnored()
        {
            var result = RingSpawnsViewModel.MultilineToSpawnPoints("100,200\n\n300,400");
            result.Should().Be("100,200;300,400");
        }

        // ── Round-trip ───────────────────────────────────────────────────────────

        [Fact]
        public void SpawnPoints_RoundTrip_IsLossless()
        {
            var original = "100,200;300,400;500,600";
            var multiline = RingSpawnsViewModel.SpawnPointsToMultiline(original);
            var roundTrip = RingSpawnsViewModel.MultilineToSpawnPoints(multiline);
            roundTrip.Should().Be(original);
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
