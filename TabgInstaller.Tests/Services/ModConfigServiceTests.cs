using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ModConfigServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public ModConfigServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
            Directory.CreateDirectory(Path.Combine(_tempDir, "BepInEx", "config"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        [Fact]
        public void ReadCommission_FileNotFound_ReturnsDefaults()
        {
            var result = ModConfigService.ReadCommission(_tempDir);
            result.Should().NotBeNull();
        }

        [Fact]
        public void WriteCommission_ThenRead_RoundTrips()
        {
            var settings = new FreddoCommissionSettings
            {
                BanList = "epic123;epic456",
                Lives = 3,
                GrenadeAttackerEnabled = true,
                GrenadeAttackerChance = 0.5f,
                GrenadeAttackerId = 198,
                GrenadeCorpseEnabled = false,
            };
            ModConfigService.WriteCommission(_tempDir, settings);
            var reread = ModConfigService.ReadCommission(_tempDir);
            reread.BanList.Should().Be("epic123;epic456");
            reread.Lives.Should().Be(3);
            reread.GrenadeAttackerEnabled.Should().BeTrue();
            reread.GrenadeAttackerChance.Should().BeApproximately(0.5f, 0.001f);
        }

        [Fact]
        public void ReadFixes_FileNotFound_ReturnsDefaults()
        {
            var result = ModConfigService.ReadFixes(_tempDir);
            result.Should().NotBeNull();
        }

        [Fact]
        public void WriteFixes_ThenRead_RoundTrips()
        {
            var settings = new StarterPackFixesSettings { EnableLootDrops = false };
            ModConfigService.WriteFixes(_tempDir, settings);
            var reread = ModConfigService.ReadFixes(_tempDir);
            reread.EnableLootDrops.Should().BeFalse();
        }

        [Fact]
        public void ReadSpawnPoints_FileNotFound_ReturnsEmpty()
        {
            var result = ModConfigService.ReadSpawnPoints(_tempDir);
            result.Should().BeEmpty();
        }

        [Fact]
        public void WriteSpawnPoints_ThenRead_RoundTrips()
        {
            ModConfigService.WriteSpawnPoints(_tempDir, "100,200;300,400;500,600");
            var result = ModConfigService.ReadSpawnPoints(_tempDir);
            result.Should().Be("100,200;300,400;500,600");
        }

        [Fact]
        public void ReadServerLogger_FileNotFound_ReturnsDefaults()
        {
            var result = ModConfigService.ReadServerLogger(_tempDir);
            result.WriteCsv.Should().BeTrue();
            result.LogDirectory.Should().Be("server-logs");
            result.CsvFileName.Should().Be("players.csv");
        }

        [Fact]
        public void WriteServerLogger_ThenRead_RoundTrips()
        {
            var settings = new ServerLoggerSettings
            {
                LogToBepInExConsole = false,
                WriteCsv = true,
                WriteLegacyServerLoggerTxt = false,
                FallbackPlayerScan = true,
                FallbackScanIntervalSeconds = 3.5f,
                LogDirectory = "identity-logs",
                CsvFileName = "joins.csv",
                LegacyFileName = "legacy-joins.txt"
            };

            ModConfigService.WriteServerLogger(_tempDir, settings);
            var reread = ModConfigService.ReadServerLogger(_tempDir);

            reread.LogToBepInExConsole.Should().BeFalse();
            reread.WriteCsv.Should().BeTrue();
            reread.WriteLegacyServerLoggerTxt.Should().BeFalse();
            reread.FallbackPlayerScan.Should().BeTrue();
            reread.FallbackScanIntervalSeconds.Should().BeApproximately(3.5f, 0.001f);
            reread.LogDirectory.Should().Be("identity-logs");
            reread.CsvFileName.Should().Be("joins.csv");
            reread.LegacyFileName.Should().Be("legacy-joins.txt");
        }

        [Fact]
        public void ReadCommission_CfgWithSections_ParsesCorrectly()
        {
            var cfgContent = "[Bans]\nBanList = epic123\n\n[GrenadesOnDeath.Attacker]\nEnabled = true\nChance = 0.3\nID = 199\n\n[GrenadesOnDeath.Corpse]\nEnabled = false\nChance = 0.1\nID = 198\n\n[Player]\nLives = 5\n";
            File.WriteAllText(Path.Combine(_tempDir, "BepInEx", "config", "FreddoTABGCommission.cfg"), cfgContent);
            var result = ModConfigService.ReadCommission(_tempDir);
            result.BanList.Should().Be("epic123");
            result.GrenadeAttackerEnabled.Should().BeTrue();
            result.GrenadeAttackerChance.Should().BeApproximately(0.3f, 0.001f);
            result.GrenadeAttackerId.Should().Be(199);
            result.GrenadeCorpseEnabled.Should().BeFalse();
            result.Lives.Should().Be(5);
        }
    }
}
