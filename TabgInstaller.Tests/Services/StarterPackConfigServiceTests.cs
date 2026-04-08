using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class StarterPackConfigServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public StarterPackConfigServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        [Fact]
        public void Read_FileNotFound_ReturnsDefaults()
        {
            var result = StarterPackConfigService.Read(_tempDir);
            result.Should().NotBeNull();
            result.WinCondition.Should().Be("Default");
        }

        [Fact]
        public void Read_ValidFile_ParsesAllFields()
        {
            var content = "WinCondition=KillsToWin\nKillsToWin=50\nForceKillAtStart=true\nDropItemsOnDeath=false\nHealOnKill=true\nHealOnKillAmount=0.5\nCanGoDown=false\nCanLockOut=true\nPercentOfVotes=60\nMinNumberOfPlayers=4\nTimeToStart=30\nSpelldropEnabled=true\nMinSpellDropDelay=10\nMaxSpellDropDelay=30\nSpellDropOffset=5\nPreMatchTimeout=5.5\nPeriMatchTimeout=15.0\n";
            File.WriteAllText(Path.Combine(_tempDir, "TheStarterPack.txt"), content);
            var result = StarterPackConfigService.Read(_tempDir);
            result.WinCondition.Should().Be("KillsToWin");
            result.KillsToWin.Should().Be(50);
            result.ForceKillAtStart.Should().BeTrue();
            result.DropItemsOnDeath.Should().BeFalse();
            result.HealOnKill.Should().BeTrue();
            result.HealOnKillAmount.Should().BeApproximately(0.5f, 0.001f);
            result.CanGoDown.Should().BeFalse();
            result.CanLockOut.Should().BeTrue();
            result.PercentOfVotes.Should().Be(60);
            result.MinNumberOfPlayers.Should().Be(4);
            result.TimeToStart.Should().Be(30);
            result.SpelldropEnabled.Should().BeTrue();
            result.PreMatchTimeout.Should().BeApproximately(5.5f, 0.001f);
            result.PeriMatchTimeout.Should().BeApproximately(15.0f, 0.001f);
        }

        [Fact]
        public void Write_ThenRead_RoundTrips()
        {
            var settings = StarterPackConfigService.Read(_tempDir);
            settings.WinCondition = "KillsToWin";
            settings.KillsToWin = 25;
            settings.ForceKillAtStart = true;
            settings.HealOnKill = true;
            settings.HealOnKillAmount = 0.75f;
            StarterPackConfigService.Write(_tempDir, settings);
            var reread = StarterPackConfigService.Read(_tempDir);
            reread.WinCondition.Should().Be("KillsToWin");
            reread.KillsToWin.Should().Be(25);
            reread.ForceKillAtStart.Should().BeTrue();
            reread.HealOnKill.Should().BeTrue();
            reread.HealOnKillAmount.Should().BeApproximately(0.75f, 0.001f);
        }

        [Fact]
        public void Read_CommentsIgnored()
        {
            File.WriteAllText(Path.Combine(_tempDir, "TheStarterPack.txt"), "//This is a comment\nWinCondition=Default\n");
            var result = StarterPackConfigService.Read(_tempDir);
            result.WinCondition.Should().Be("Default");
        }

        [Fact]
        public void Read_EmptyFile_ReturnsDefaults()
        {
            File.WriteAllText(Path.Combine(_tempDir, "TheStarterPack.txt"), "");
            var result = StarterPackConfigService.Read(_tempDir);
            result.Should().NotBeNull();
        }

        [Fact]
        public void Read_BoolParsing_CaseInsensitive()
        {
            File.WriteAllText(Path.Combine(_tempDir, "TheStarterPack.txt"), "ForceKillAtStart=True\nHealOnKill=true\nCanGoDown=FALSE\n");
            var result = StarterPackConfigService.Read(_tempDir);
            result.ForceKillAtStart.Should().BeTrue();
            result.HealOnKill.Should().BeTrue();
            result.CanGoDown.Should().BeFalse();
        }

        [Fact]
        public void GetPath_ReturnsTheStarterPackTxt()
        {
            var path = StarterPackConfigService.GetPath(_tempDir);
            Path.GetFileName(path).Should().Be("TheStarterPack.txt");
        }
    }
}
