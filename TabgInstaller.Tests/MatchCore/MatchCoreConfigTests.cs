using FluentAssertions;
using TabgInstaller.MatchCore;
using Xunit;

namespace TabgInstaller.Tests.MatchCore
{
    public class MatchCoreConfigTests
    {
        [Theory]
        [InlineData("PeriMatchTimeout=12.5")]
        [InlineData("MatchTimeout=12.5")]
        public void Parse_AcceptsLegacyAndCorrectMatchTimeoutKeys(string line)
        {
            var cfg = MatchCoreConfig.Parse(new[] { line });

            cfg.MatchTimeout.Should().BeApproximately(12.5f, 0.001f);
        }

        [Fact]
        public void Parse_ClampsVoteAndTimeoutSettings()
        {
            var cfg = MatchCoreConfig.Parse(new[]
            {
                "PercentOfVotes=250",
                "MinNumberOfPlayers=-5",
                "TimeToStart=-1",
                "PreMatchTimeout=-10",
                "MatchTimeout=-20"
            });

            cfg.VotePercent.Should().Be(100f);
            cfg.VoteMinimumPlayers.Should().Be(1);
            cfg.VoteStartCountdown.Should().Be(0f);
            cfg.PreMatchTimeout.Should().Be(0f);
            cfg.MatchTimeout.Should().Be(0f);
        }

        [Fact]
        public void Parse_IgnoresMalformedLoadoutsAndKeepsValidEntries()
        {
            var cfg = MatchCoreConfig.Parse(new[]
            {
                "Loadouts=broken/Default:2%1:1,2:30/Empty:5%bad"
            });

            cfg.Loadouts.Should().ContainSingle();
            cfg.Loadouts[0].Name.Should().Be("Default");
            cfg.Loadouts[0].Weight.Should().Be(2);
            cfg.Loadouts[0].Items.Should().HaveCount(2);
        }
    }
}
