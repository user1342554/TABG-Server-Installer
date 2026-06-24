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

        [Fact]
        public void Parse_RingSettings_ReadsOptionalSpeedList()
        {
            var cfg = MatchCoreConfig.Parse(new[]
            {
                "RingSettings=Fast:10%1,2,3:100,50,25:12,6,3"
            });

            cfg.Rings.Should().ContainSingle();
            cfg.Rings[0].Name.Should().Be("Fast");
            cfg.Rings[0].Center.Should().Be(new UnityEngine.Vector3(1f, 2f, 3f));
            cfg.Rings[0].Sizes.Should().Equal(100f, 50f, 25f);
            cfg.Rings[0].Speeds.Should().Equal(12f, 6f, 3f);
        }

        [Fact]
        public void Parse_RingSettings_ReadsLegacyPipeFormat()
        {
            var cfg = MatchCoreConfig.Parse(new[]
            {
                "RingSettings=0,160,0|4240,3450,1710|25,3,1.5"
            });

            cfg.Rings.Should().ContainSingle();
            cfg.Rings[0].Center.Should().Be(new UnityEngine.Vector3(0f, 160f, 0f));
            cfg.Rings[0].Sizes.Should().Equal(4240f, 3450f, 1710f);
            cfg.Rings[0].Speeds.Should().Equal(25f, 3f, 1.5f);
        }

        [Fact]
        public void Parse_MalformedRingSettings_DoesNotEraseExistingRing()
        {
            var cfg = MatchCoreConfig.Parse(new[]
            {
                "RingLocation=0,160,0",
                "RingSizes=4240,3450,1710",
                "RingSettings=broken"
            });

            cfg.Rings.Should().ContainSingle();
            cfg.Rings[0].Center.Should().Be(new UnityEngine.Vector3(0f, 160f, 0f));
            cfg.Rings[0].Sizes.Should().Equal(4240f, 3450f, 1710f);
        }

        [Fact]
        public void Parse_RingTimingSettings_ClampsToNonNegativeValues()
        {
            var cfg = MatchCoreConfig.Parse(new[]
            {
                "RingBaseTime=-90",
                "MaxRingWaitSeconds=25"
            });

            cfg.RingBaseTime.Should().Be(0f);
            cfg.MaxRingWaitSeconds.Should().Be(25f);
        }

        [Theory]
        [InlineData("Debug")]
        [InlineData("Endless")]
        public void Parse_WinCondition_SupportsExplicitNonBattleRoyaleModes(string value)
        {
            var cfg = MatchCoreConfig.Parse(new[] { "WinCondition=" + value });

            cfg.WinCondition.ToString().Should().Be(value);
        }
    }
}
