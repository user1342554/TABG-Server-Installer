using FluentAssertions;
using Moq;
using System.Threading;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class RemoteProcessServiceTests
    {
        private readonly Mock<IRemoteSshService> _ssh = new();

        [Fact]
        public void Start_ScreenMode_SendsScreenCommand()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Screen
            };
            _ssh.SetupGet(s => s.IsConnected).Returns(true);
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();

            _ssh.Verify(s => s.ExecuteCommandAsync(
                It.Is<string>(cmd => cmd.Contains("screen -dmS tabg") && cmd.Contains("/opt/tabg/TABG.exe")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Start_SystemdMode_SendsSystemctlCommand()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Systemd
            };
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();

            _ssh.Verify(s => s.ExecuteCommandAsync(
                "systemctl start tabg-server",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Stop_ScreenMode_SendsQuitCommand()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Screen
            };
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();
            sut.Stop();

            _ssh.Verify(s => s.ExecuteCommandAsync(
                "screen -S tabg -X quit",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void IsRunning_AfterStart_IsTrue()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Screen
            };
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();
            sut.IsRunning.Should().BeTrue();
        }

        [Fact]
        public void IsRunning_AfterStop_IsFalse()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Screen
            };
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();
            sut.Stop();
            sut.IsRunning.Should().BeFalse();
        }
    }
}
