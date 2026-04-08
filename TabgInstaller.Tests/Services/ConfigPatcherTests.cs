using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ConfigPatcherTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ConfigPatcher _sut = new();

        public ConfigPatcherTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string CreateConfigFile(string content)
        {
            var path = Path.Combine(_tempDir, "test_config.txt");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void ApplyGameSettingsChange_ExistingKey_UpdatesValue()
        {
            var path = CreateConfigFile("ServerName=old\nPort=7777\n");
            var result = _sut.ApplyGameSettingsChange(path, "ServerName", "newname");
            result.Should().Contain("Successfully updated");
            File.ReadAllText(path).Should().Contain("ServerName=newname");
        }

        [Fact]
        public void ApplyGameSettingsChange_MissingKey_AddsIt()
        {
            var path = CreateConfigFile("Port=7777\n");
            var result = _sut.ApplyGameSettingsChange(path, "MaxPlayers", "50");
            result.Should().Contain("Successfully updated");
            File.ReadAllText(path).Should().Contain("MaxPlayers=50");
        }

        [Fact]
        public void ApplyGameSettingsChange_FileNotFound_ReturnsError()
        {
            var path = Path.Combine(_tempDir, "nonexistent.txt");
            var result = _sut.ApplyGameSettingsChange(path, "Key", "Value");
            result.Should().Contain("not found");
        }

        [Fact]
        public void ApplyGameSettingsChange_PreservesOtherKeys()
        {
            var path = CreateConfigFile("ServerName=old\nPort=7777\nMaxPlayers=50\n");
            _sut.ApplyGameSettingsChange(path, "Port", "8888");
            var content = File.ReadAllText(path);
            content.Should().Contain("ServerName=old");
            content.Should().Contain("Port=8888");
            content.Should().Contain("MaxPlayers=50");
        }

        [Fact]
        public void ApplyGameSettingsChange_ValueWithSpecialChars_Preserved()
        {
            var path = CreateConfigFile("RingSizes=4240.0,500.0\n");
            _sut.ApplyGameSettingsChange(path, "RingSizes", "1000.0,500.0,250.0");
            File.ReadAllText(path).Should().Contain("RingSizes=1000.0,500.0,250.0");
        }

        [Fact]
        public void GetGameSettingValue_ExistingKey_ReturnsValue()
        {
            var path = CreateConfigFile("Port=7777\nMaxPlayers=50\n");
            var result = _sut.GetGameSettingValue(path, "Port");
            result.Should().Be("7777");
        }

        [Fact]
        public void GetGameSettingValue_MissingKey_ReturnsEmpty()
        {
            var path = CreateConfigFile("Port=7777\n");
            var result = _sut.GetGameSettingValue(path, "Nonexistent");
            result.Should().BeEmpty();
        }

        [Fact]
        public void GetGameSettingValue_FileNotFound_ReturnsEmpty()
        {
            var path = Path.Combine(_tempDir, "nonexistent.txt");
            var result = _sut.GetGameSettingValue(path, "Key");
            result.Should().BeEmpty();
        }

        [Fact]
        public void GetGameSettingValue_EmptyFile_ReturnsEmpty()
        {
            var path = CreateConfigFile("");
            var result = _sut.GetGameSettingValue(path, "Key");
            result.Should().BeEmpty();
        }

        [Fact]
        public void ApplyGameSettingsChange_EmptyFile_AddsKey()
        {
            var path = CreateConfigFile("");
            var result = _sut.ApplyGameSettingsChange(path, "Port", "7777");
            result.Should().Contain("Successfully");
            File.ReadAllText(path).Should().Contain("Port=7777");
        }

        [Fact]
        public void ApplyDatapackChange_UpdatesMultipleKeys()
        {
            var path = CreateConfigFile("WinCondition=Default\nKillsToWin=20\n");
            var changes = new Newtonsoft.Json.Linq.JObject
            {
                ["WinCondition"] = "KillsToWin",
                ["KillsToWin"] = "50"
            };
            var result = _sut.ApplyDatapackChange(path, "General", changes);
            result.Should().Contain("Successfully");
            var content = File.ReadAllText(path);
            content.Should().Contain("WinCondition=KillsToWin");
            content.Should().Contain("KillsToWin=50");
        }

        [Fact]
        public void ApplyDatapackChange_FileNotFound_ReturnsError()
        {
            var path = Path.Combine(_tempDir, "nonexistent.txt");
            var changes = new Newtonsoft.Json.Linq.JObject { ["Key"] = "Value" };
            var result = _sut.ApplyDatapackChange(path, "Section", changes);
            result.Should().Contain("not found");
        }
    }
}
