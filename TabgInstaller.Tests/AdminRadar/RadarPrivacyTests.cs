using System.Collections.Generic;
using FluentAssertions;
using TabgInstaller.AdminRadar.Server;
using Xunit;

namespace TabgInstaller.Tests.AdminRadar
{
    public class RadarPrivacyTests
    {
        [Fact]
        public void SanitizeBotDebugTargetName_WhenRealPlayersExcluded_HidesUnknownTargets()
        {
            var dummyNames = new HashSet<string> { "AIPlayer 1" };

            string result = RadarPrivacy.SanitizeBotDebugTargetName(
                "RealPlayerName",
                includeRealPlayers: false,
                dummyTargetNames: dummyNames);

            result.Should().Be(RadarPrivacy.HiddenTargetName);
        }

        [Fact]
        public void SanitizeBotDebugTargetName_WhenRealPlayersExcluded_PreservesDummyTargets()
        {
            var dummyNames = new HashSet<string> { "AIPlayer 1" };

            string result = RadarPrivacy.SanitizeBotDebugTargetName(
                "AIPlayer 1",
                includeRealPlayers: false,
                dummyTargetNames: dummyNames);

            result.Should().Be("AIPlayer 1");
        }

        [Theory]
        [InlineData("last-heard")]
        [InlineData("last-seen")]
        public void SanitizeBotDebugTargetName_WhenRealPlayersExcluded_PreservesThreatMemoryMarkers(string marker)
        {
            string result = RadarPrivacy.SanitizeBotDebugTargetName(
                marker,
                includeRealPlayers: false,
                dummyTargetNames: new HashSet<string>());

            result.Should().Be(marker);
        }
    }
}
