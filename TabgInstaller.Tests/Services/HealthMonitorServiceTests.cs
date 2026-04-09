using System;
using System.Collections.ObjectModel;
using System.Threading;
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class HealthMonitorServiceTests
    {
        private readonly Mock<IServerProcessService> _procSvc = new();

        private HealthMonitorService CreateSut()
        {
            _procSvc.SetupGet(p => p.LogEntries).Returns(new ObservableCollection<LogEntry>());
            return new HealthMonitorService(_procSvc.Object);
        }

        [Fact]
        public void InitialState_IsStoppedWithZeroPlayers()
        {
            var sut = CreateSut();
            sut.Status.Should().Be(ServerHealthStatus.Stopped);
            sut.PlayerCount.Should().Be(0);
            sut.IsAlive.Should().BeFalse();
            sut.JoinCode.Should().BeNull();
            sut.ConnectedPlayers.Should().BeEmpty();
        }

        [Fact]
        public void HandleEvent_PlayerJoined_IncrementsCountAndAddsPlayer()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent
            {
                Type = ServerEventType.PlayerJoined,
                PlayerName = "Jon_ass",
                EpicId = "abc123",
                PlayerIndex = 0
            });

            sut.PlayerCount.Should().Be(1);
            sut.ConnectedPlayers.Should().ContainSingle(p => p.Name == "Jon_ass" && p.EpicId == "abc123");
        }

        [Fact]
        public void HandleEvent_PlayerLeft_DecrementsCountAndRemovesPlayer()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerJoined, PlayerName = "Jon_ass", EpicId = "abc", PlayerIndex = 0 });
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerLeft, PlayerName = "Jon_ass" });

            sut.PlayerCount.Should().Be(0);
            sut.ConnectedPlayers.Should().BeEmpty();
        }

        [Fact]
        public void HandleEvent_PlayerLeft_NeverGoesBelowZero()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerLeft, PlayerName = "Ghost" });
            sut.PlayerCount.Should().Be(0);
        }

        [Fact]
        public void HandleEvent_JoinCode_StoresCode()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.JoinCodeReceived, JoinCode = "FWJTKK" });
            sut.JoinCode.Should().Be("FWJTKK");
        }

        [Fact]
        public void MarkRunning_SetsStatusAndStartsUptime()
        {
            var sut = CreateSut();
            sut.MarkRunning();
            sut.Status.Should().Be(ServerHealthStatus.Running);
            sut.IsAlive.Should().BeTrue();
        }

        [Fact]
        public void MarkStopped_ResetsState()
        {
            var sut = CreateSut();
            sut.MarkRunning();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerJoined, PlayerName = "P1", EpicId = "e1", PlayerIndex = 0 });
            sut.MarkStopped();

            sut.Status.Should().Be(ServerHealthStatus.Stopped);
            sut.IsAlive.Should().BeFalse();
            sut.PlayerCount.Should().Be(0);
            sut.ConnectedPlayers.Should().BeEmpty();
            sut.JoinCode.Should().BeNull();
        }

        [Fact]
        public void MarkCrashed_SetsStatusToCrashed()
        {
            var sut = CreateSut();
            sut.MarkRunning();
            sut.MarkCrashed();
            sut.Status.Should().Be(ServerHealthStatus.Crashed);
            sut.IsAlive.Should().BeFalse();
        }

        [Fact]
        public void MarkRestarting_SetsStatusAndTracksAttempt()
        {
            var sut = CreateSut();
            sut.MarkRestarting(1, 3);
            sut.Status.Should().Be(ServerHealthStatus.Restarting);
            sut.RestartAttempt.Should().Be(1);
            sut.MaxRetries.Should().Be(3);
        }

        [Fact]
        public void MarkWatchdog_SetsStatusToWatchdog()
        {
            var sut = CreateSut();
            sut.MarkWatchdog();
            sut.Status.Should().Be(ServerHealthStatus.Watchdog);
        }

        [Fact]
        public void UpdateMemoryUsage_StoresValue()
        {
            var sut = CreateSut();
            sut.UpdateMemoryUsage(842);
            sut.MemoryUsageMb.Should().Be(842);
        }

        [Fact]
        public void Uptime_WhenRunning_ReturnsElapsed()
        {
            var sut = CreateSut();
            sut.MarkRunning();
            Thread.Sleep(50);
            sut.Uptime.TotalMilliseconds.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Uptime_WhenStopped_ReturnsZero()
        {
            var sut = CreateSut();
            sut.Uptime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void HandleEvent_DuplicatePlayerJoin_DoesNotDoubleCount()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerJoined, PlayerName = "Jon_ass", EpicId = "abc", PlayerIndex = 0 });
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerJoined, PlayerName = "Jon_ass", EpicId = "abc", PlayerIndex = 0 });
            sut.PlayerCount.Should().Be(1);
            sut.ConnectedPlayers.Should().HaveCount(1);
        }
    }
}
