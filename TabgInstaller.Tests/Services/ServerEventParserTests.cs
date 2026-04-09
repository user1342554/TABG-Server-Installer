using FluentAssertions;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ServerEventParserTests
    {
        [Fact]
        public void TryParse_PlayerAssignment_ReturnsPlayerJoined()
        {
            var line = "[LandLog] - Player: 0 Name: Jon_ass : Assigning EPic ID: 0002679463fd49ffab724df634f46418";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.PlayerJoined);
            result.PlayerName.Should().Be("Jon_ass");
            result.EpicId.Should().Be("0002679463fd49ffab724df634f46418");
            result.PlayerIndex.Should().Be(0);
        }

        [Fact]
        public void TryParse_PlayerLeft_ReturnsPlayerLeft()
        {
            var line = "[LandLog] - Player left: Jon_ass";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.PlayerLeft);
            result.PlayerName.Should().Be("Jon_ass");
        }

        [Fact]
        public void TryParse_ClientDisconnected_ReturnsPlayerLeft()
        {
            var line = "[LandLog] - Client: 0 disconnected from server";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.PlayerLeft);
            result.PlayerIndex.Should().Be(0);
        }

        [Fact]
        public void TryParse_JoinCode_ReturnsJoinCodeReceived()
        {
            var line = "[LandLog] - Host - Got join code: FWJTKK";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.JoinCodeReceived);
            result.JoinCode.Should().Be("FWJTKK");
        }

        [Fact]
        public void TryParse_UnrelatedLine_ReturnsNull()
        {
            var line = "[INFO] [UnityMemory] Configuration Parameters";
            var result = ServerEventParser.TryParse(line);
            result.Should().BeNull();
        }

        [Fact]
        public void TryParse_ProcessExited_ReturnsProcessExited()
        {
            var line = "<process exited>";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.ProcessExited);
        }

        [Fact]
        public void TryParse_PlayerWithSpacesInName_ParsesCorrectly()
        {
            var line = "[LandLog] - Player: 5 Name: Cool Player 123 : Assigning EPic ID: abc123def456";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.PlayerName.Should().Be("Cool Player 123");
            result.EpicId.Should().Be("abc123def456");
            result.PlayerIndex.Should().Be(5);
        }

        [Fact]
        public void TryParse_PlayerLeftWithSpaces_ParsesCorrectly()
        {
            var line = "[LandLog] - Player left: Cool Player 123";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.PlayerName.Should().Be("Cool Player 123");
        }
    }
}
