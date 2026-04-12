using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class InstalledPluginTrackerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _communityDir;

        public InstalledPluginTrackerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            _communityDir = Path.Combine(_tempDir, "BepInEx", "plugins", "community");
            Directory.CreateDirectory(_communityDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private InstalledPluginTracker CreateSut() => new(_tempDir);

        [Fact]
        public void Load_NoFile_ReturnsEmptyList()
        {
            var sut = CreateSut();
            var data = sut.Load();
            data.Plugins.Should().BeEmpty();
        }

        [Fact]
        public void AddPlugin_ThenLoad_ReturnsPlugin()
        {
            var sut = CreateSut();
            sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });
            var data = sut.Load();
            data.Plugins.Should().ContainSingle(p => p.Id == "test-plugin");
            data.Plugins[0].InstalledVersion.Should().Be("1.0.0");
            data.Plugins[0].Pinned.Should().BeFalse();
        }

        [Fact]
        public void UpdatePluginVersion_UpdatesVersionAndTimestamp()
        {
            var sut = CreateSut();
            sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });
            sut.UpdatePluginVersion("test-plugin", "2.0.0");
            var data = sut.Load();
            data.Plugins[0].InstalledVersion.Should().Be("2.0.0");
            data.Plugins[0].UpdatedAt.Should().NotBeNull();
        }

        [Fact]
        public void RemovePlugin_RemovesFromList()
        {
            var sut = CreateSut();
            sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });
            sut.RemovePlugin("test-plugin");
            var data = sut.Load();
            data.Plugins.Should().BeEmpty();
        }

        [Fact]
        public void SetPinned_UpdatesPinnedFlag()
        {
            var sut = CreateSut();
            sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });
            sut.SetPinned("test-plugin", true);
            var data = sut.Load();
            data.Plugins[0].Pinned.Should().BeTrue();
        }

        [Fact]
        public void FindById_ReturnsMatchingEntry()
        {
            var sut = CreateSut();
            sut.AddPlugin("alpha", "1.0.0", new[] { "A.dll" });
            sut.AddPlugin("beta", "2.0.0", new[] { "B.dll" });
            var entry = sut.FindById("beta");
            entry.Should().NotBeNull();
            entry!.InstalledVersion.Should().Be("2.0.0");
        }

        [Fact]
        public void FindById_NotFound_ReturnsNull()
        {
            var sut = CreateSut();
            var entry = sut.FindById("nonexistent");
            entry.Should().BeNull();
        }

        [Fact]
        public void IsInstalled_ReturnsTrueForInstalled()
        {
            var sut = CreateSut();
            sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });
            sut.IsInstalled("test-plugin").Should().BeTrue();
            sut.IsInstalled("other").Should().BeFalse();
        }
    }
}
