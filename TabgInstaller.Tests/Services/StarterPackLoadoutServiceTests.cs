using System.Collections.Generic;
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class StarterPackLoadoutServiceTests
    {
        private readonly StarterPackLoadoutService _sut = new();

        [Fact]
        public void ParseLoadoutsValue_EmptyString_ReturnsEmptyList()
        {
            var result = _sut.ParseLoadoutsValue("");
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoadoutsValue_WhitespaceOnly_ReturnsEmptyList()
        {
            var result = _sut.ParseLoadoutsValue("   ");
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoadoutsValue_SingleLoadoutNoItems_ParsesCorrectly()
        {
            var result = _sut.ParseLoadoutsValue("Pistol:50%/");
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Pistol");
            result[0].Percent.Should().Be(50);
            result[0].Items.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoadoutsValue_SingleLoadoutWithItems_ParsesCorrectly()
        {
            var result = _sut.ParseLoadoutsValue("Rifle:75% 10:1,20:2/");
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Rifle");
            result[0].Percent.Should().Be(75);
            result[0].Items.Should().HaveCount(2);
            result[0].Items[0].Id.Should().Be("10");
            result[0].Items[0].Quantity.Should().Be(1);
            result[0].Items[1].Id.Should().Be("20");
            result[0].Items[1].Quantity.Should().Be(2);
        }

        [Fact]
        public void ParseLoadoutsValue_MultipleLoadouts_ParsesAll()
        {
            var result = _sut.ParseLoadoutsValue("Pistol:50% 5:1/Rifle:30% 10:2/Shotgun:20%/");
            result.Should().HaveCount(3);
            result[0].Name.Should().Be("Pistol");
            result[1].Name.Should().Be("Rifle");
            result[2].Name.Should().Be("Shotgun");
        }

        [Fact]
        public void ParseLoadoutsValue_ZeroPercent_ParsesCorrectly()
        {
            var result = _sut.ParseLoadoutsValue("Empty:0%/");
            result.Should().HaveCount(1);
            result[0].Percent.Should().Be(0);
        }

        [Fact]
        public void ParseLoadoutsValue_ItemWithZeroQuantity_ParsesCorrectly()
        {
            var result = _sut.ParseLoadoutsValue("Test:100% 5:0/");
            result.Should().HaveCount(1);
            result[0].Items.Should().HaveCount(1);
            result[0].Items[0].Quantity.Should().Be(0);
        }

        [Fact]
        public void ParseLoadoutsValue_MalformedSegment_IsSkipped()
        {
            var result = _sut.ParseLoadoutsValue("notavalidformat/Pistol:50%/");
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Pistol");
        }

        [Fact]
        public void ParseLoadoutsValue_NoTrailingSlash_StillParses()
        {
            var result = _sut.ParseLoadoutsValue("Pistol:50%");
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Pistol");
        }

        [Fact]
        public void BuildLoadoutsValue_EmptyList_ReturnsEmptyString()
        {
            var result = _sut.BuildLoadoutsValue(new List<StarterPackLoadoutService.Loadout>());
            result.Should().BeEmpty();
        }

        [Fact]
        public void BuildLoadoutsValue_SingleLoadoutNoItems_FormatsCorrectly()
        {
            var loadouts = new List<StarterPackLoadoutService.Loadout>
            {
                new("Pistol", 50, new List<StarterPackLoadoutService.Item>())
            };
            var result = _sut.BuildLoadoutsValue(loadouts);
            result.Should().Be("Pistol:50%/");
        }

        [Fact]
        public void BuildLoadoutsValue_LoadoutWithItems_FormatsCorrectly()
        {
            var loadouts = new List<StarterPackLoadoutService.Loadout>
            {
                new("Rifle", 75, new List<StarterPackLoadoutService.Item>
                {
                    new("10", 1),
                    new("20", 2)
                })
            };
            var result = _sut.BuildLoadoutsValue(loadouts);
            result.Should().Be("Rifle:75% 10:1,20:2/");
        }

        [Fact]
        public void RoundTrip_ParseThenBuildThenParse_ProducesSameResult()
        {
            var original = "Pistol:50% 5:1,6:2/Rifle:30% 10:1/Shotgun:20%/";
            var parsed = _sut.ParseLoadoutsValue(original);
            var rebuilt = _sut.BuildLoadoutsValue(parsed);
            var reparsed = _sut.ParseLoadoutsValue(rebuilt);
            reparsed.Should().HaveCount(parsed.Count);
            for (int i = 0; i < parsed.Count; i++)
            {
                reparsed[i].Name.Should().Be(parsed[i].Name);
                reparsed[i].Percent.Should().Be(parsed[i].Percent);
                reparsed[i].Items.Should().HaveCount(parsed[i].Items.Count);
            }
        }

        [Fact]
        public void ParseLoadoutsValue_ExtraWhitespace_HandledGracefully()
        {
            var result = _sut.ParseLoadoutsValue("  Pistol:50%  5:1 /  Rifle:30%  / ");
            result.Should().HaveCount(2);
            result[0].Name.Should().Be("Pistol");
            result[1].Name.Should().Be("Rifle");
        }

        [Fact]
        public void ParseLoadoutsValue_InvalidItemPair_IsSkipped()
        {
            var result = _sut.ParseLoadoutsValue("Test:100% good:1,bad:notanumber,also:2/");
            result.Should().HaveCount(1);
            result[0].Items.Should().HaveCount(2);
            result[0].Items[0].Id.Should().Be("good");
            result[0].Items[1].Id.Should().Be("also");
        }
    }
}
