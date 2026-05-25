using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using Xunit;

namespace TabgInstaller.Tests
{
    public class ConfigIOTests : IDisposable
    {
        private readonly string _tempDir;

        public ConfigIOTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private string TempFile(string name = "config.txt") => Path.Combine(_tempDir, name);

        [Fact]
        public void ReadGameSettings_FileNotFound_ReturnsDefaults()
        {
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.Should().NotBeNull();
            result.ServerName.Should().Be("enormous");
        }

        [Fact]
        public void ReadGameSettings_ValidFile_ParsesStrings()
        {
            File.WriteAllText(TempFile(), "ServerName=MyServer\nPassword=secret\nTeamMode=SOLO\n");
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.ServerName.Should().Be("MyServer");
            result.Password.Should().Be("secret");
            result.TeamMode.Should().Be("SOLO");
        }

        [Fact]
        public void ReadGameSettings_ValidFile_ParsesInts()
        {
            File.WriteAllText(TempFile(), "Port=8888\nMaxPlayers=100\n");
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.Port.Should().Be(8888);
            result.MaxPlayers.Should().Be(100);
        }

        [Fact]
        public void ReadGameSettings_ValidFile_ParsesFloats()
        {
            File.WriteAllText(TempFile(), "CarSpawnRate=0.5\nCountdown=15.0\n");
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.CarSpawnRate.Should().BeApproximately(0.5f, 0.001f);
            result.Countdown.Should().BeApproximately(15.0f, 0.001f);
        }

        [Fact]
        public void ReadGameSettings_ValidFile_ParsesBools()
        {
            File.WriteAllText(TempFile(), "Relay=true\nNoRing=false\nAutoTeam=True\n");
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.Relay.Should().BeTrue();
            result.NoRing.Should().BeFalse();
            result.AutoTeam.Should().BeTrue();
        }

        [Fact]
        public void ReadGameSettings_NumericBools_ParsesToggles()
        {
            File.WriteAllText(TempFile(), "NoRing=1\nDEBUG_DEATHMATCH=0\nAllowRejoins=1\n");
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.NoRing.Should().BeTrue();
            result.DEBUG_DEATHMATCH.Should().BeFalse();
            result.AllowRejoins.Should().BeTrue();
        }

        [Fact]
        public void WriteGameSettings_NumericBoolKeys_WritesTogglesAsNumbers()
        {
            var path = TempFile("game_settings.txt");
            ConfigIO.WriteGameSettings(new GameSettingsData
            {
                NoRing = true,
                DEBUG_DEATHMATCH = false,
                AllowRejoins = false
            }, path);

            var text = File.ReadAllText(path);
            text.Should().Contain("NoRing=1");
            text.Should().Contain("DEBUG_DEATHMATCH=0");
            text.Should().Contain("AllowRejoins=0");
        }

        [Fact]
        public void ReadGameSettings_MalformedValue_UsesDefault()
        {
            File.WriteAllText(TempFile(), "Port=notanumber\n");
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.Port.Should().Be(7777);
        }

        [Fact]
        public void ReadGameSettings_CommentsIgnored()
        {
            File.WriteAllText(TempFile(), "// This is a comment\nServerName=TestServer\n");
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.ServerName.Should().Be("TestServer");
        }

        [Fact]
        public void WriteGameSettings_ThenRead_RoundTrips()
        {
            var data = new GameSettingsData
            {
                ServerName = "RoundTrip",
                Port = 9999,
                MaxPlayers = 42,
                CarSpawnRate = 0.75f,
                Relay = false
            };
            var path = TempFile("game_settings.txt");
            ConfigIO.WriteGameSettings(data, path);
            var reread = ConfigIO.ReadGameSettings(path);
            reread.ServerName.Should().Be("RoundTrip");
            reread.Port.Should().Be(9999);
            reread.MaxPlayers.Should().Be(42);
            reread.CarSpawnRate.Should().BeApproximately(0.75f, 0.001f);
            reread.Relay.Should().BeFalse();
        }

        [Fact]
        public void ReadPlayerPerms_FileNotFound_ReturnsEmptyList()
        {
            var result = ConfigIO.ReadPlayerPerms(TempFile("perms.json"));
            result.Should().BeEmpty();
        }

        [Fact]
        public void WritePlayerPerms_ThenRead_RoundTrips()
        {
            var path = TempFile("perms.json");
            var perms = new List<string> { "epic123:4", "epic456:2" };
            ConfigIO.WritePlayerPerms(perms, path);
            var result = ConfigIO.ReadPlayerPerms(path);
            result.Should().BeEquivalentTo(perms);
        }

        [Fact]
        public void ReadExtraSettings_FileNotFound_ReturnsEmptyDict()
        {
            var result = ConfigIO.ReadExtraSettings(TempFile("extra.json"));
            result.Should().BeEmpty();
        }

        [Fact]
        public void WriteExtraSettings_ThenRead_RoundTrips()
        {
            var path = TempFile("extra.json");
            var settings = new Dictionary<string, string> { ["Key1"] = "Value1", ["Key2"] = "Value2" };
            ConfigIO.WriteExtraSettings(settings, path);
            var result = ConfigIO.ReadExtraSettings(path);
            result.Should().BeEquivalentTo(settings);
        }
    }
}
