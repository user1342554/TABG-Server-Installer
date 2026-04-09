using FluentAssertions;
using TabgInstaller.Core.Model;
using Xunit;

namespace TabgInstaller.Tests.Model
{
    public class ServerInstanceDataTests
    {
        [Fact]
        public void ServerInstanceData_DefaultValues_AreCorrect()
        {
            var data = new ServerInstanceData();
            data.Id.Should().NotBeEmpty();
            data.DisplayName.Should().Be("");
            data.ServerPath.Should().Be("");
            data.InstanceType.Should().Be(ServerInstanceType.Local);
            data.AutoRestart.Should().NotBeNull();
        }

        [Fact]
        public void AutoRestartConfig_DefaultValues_AreCorrect()
        {
            var config = new AutoRestartConfig();
            config.Enabled.Should().BeTrue();
            config.MaxRetries.Should().Be(3);
            config.InitialBackoffSeconds.Should().Be(5);
            config.WatchdogIntervalSeconds.Should().Be(300);
            config.StabilityThresholdSeconds.Should().Be(30);
        }

        [Fact]
        public void RemoteConnectionConfig_DefaultValues_AreCorrect()
        {
            var config = new RemoteConnectionConfig();
            config.Host.Should().Be("");
            config.Port.Should().Be(22);
            config.Username.Should().Be("");
            config.AuthMethod.Should().Be(SshAuthMethod.Password);
            config.PrivateKeyPath.Should().Be("");
            config.RemoteServerPath.Should().Be("");
            config.ProcessMode.Should().Be(RemoteProcessMode.Screen);
        }

        [Fact]
        public void InstancesFileData_DefaultValues_AreCorrect()
        {
            var data = new InstancesFileData();
            data.Instances.Should().BeEmpty();
            data.ActiveInstanceId.Should().BeNull();
        }

        [Fact]
        public void ConnectedPlayer_StoresProperties()
        {
            var player = new ConnectedPlayer
            {
                Name = "Jon_ass",
                EpicId = "0002679463fd49ffab724df634f46418",
                JoinedAt = new System.DateTime(2026, 4, 9, 14, 7, 25)
            };
            player.Name.Should().Be("Jon_ass");
            player.EpicId.Should().Be("0002679463fd49ffab724df634f46418");
        }

        [Fact]
        public void ServerHealthStatus_HasExpectedValues()
        {
            ServerHealthStatus.Stopped.Should().BeDefined();
            ServerHealthStatus.Running.Should().BeDefined();
            ServerHealthStatus.Crashed.Should().BeDefined();
            ServerHealthStatus.Restarting.Should().BeDefined();
            ServerHealthStatus.Watchdog.Should().BeDefined();
        }

        [Fact]
        public void ServerEventType_HasExpectedValues()
        {
            ServerEventType.PlayerJoined.Should().BeDefined();
            ServerEventType.PlayerLeft.Should().BeDefined();
            ServerEventType.JoinCodeReceived.Should().BeDefined();
            ServerEventType.ProcessExited.Should().BeDefined();
        }
    }
}
