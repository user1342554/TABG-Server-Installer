using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class SafeConfigEditorTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly SafeConfigEditor _sut = new();

        public SafeConfigEditorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string CreateFile(string content)
        {
            var path = Path.Combine(_tempDir, "config.txt");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void ComputeSha256_SameContent_ProducesSameHash()
        {
            var path = CreateFile("ServerName=test\nPort=7777\n");
            var hash1 = SafeConfigEditor.ComputeSha256(path);
            var hash2 = SafeConfigEditor.ComputeSha256(path);
            hash1.Should().Be(hash2);
            hash1.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void ComputeSha256_DifferentContent_ProducesDifferentHash()
        {
            var path = CreateFile("content1");
            var hash1 = SafeConfigEditor.ComputeSha256(path);
            File.WriteAllText(path, "content2");
            var hash2 = SafeConfigEditor.ComputeSha256(path);
            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void SetKeyValue_PreviewMode_DoesNotModifyFile()
        {
            var path = CreateFile("Port=7777\n");
            var originalContent = File.ReadAllText(path);
            var hash = SafeConfigEditor.ComputeSha256(path);
            var result = _sut.SetKeyValue(path, "Port", "8888", hash, previewOnly: true);
            result.Success.Should().BeTrue();
            result.Message.Should().Be("Preview only");
            result.UnifiedDiff.Should().NotBeNullOrWhiteSpace();
            File.ReadAllText(path).Should().Be(originalContent);
        }

        [Fact]
        public void SetKeyValue_CommitMode_ModifiesFile()
        {
            var path = CreateFile("Port=7777\n");
            var hash = SafeConfigEditor.ComputeSha256(path);
            var result = _sut.SetKeyValue(path, "Port", "8888", hash, previewOnly: false);
            result.Success.Should().BeTrue();
            result.Message.Should().Be("Applied");
            File.ReadAllText(path).Should().Contain("Port=8888");
        }

        [Fact]
        public void SetKeyValue_HashMismatch_ReturnsError()
        {
            var path = CreateFile("Port=7777\n");
            var result = _sut.SetKeyValue(path, "Port", "8888", "WRONGHASH", previewOnly: false);
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("changed since preview");
        }

        [Fact]
        public void SetKeyValue_NullHash_SkipsHashCheck()
        {
            var path = CreateFile("Port=7777\n");
            var result = _sut.SetKeyValue(path, "Port", "8888", null, previewOnly: false);
            result.Success.Should().BeTrue();
        }

        [Fact]
        public void SetKeyValue_MissingKey_AddsIt()
        {
            var path = CreateFile("Port=7777\n");
            var result = _sut.SetKeyValue(path, "MaxPlayers", "50", null, previewOnly: false);
            result.Success.Should().BeTrue();
            File.ReadAllText(path).Should().Contain("MaxPlayers=50");
        }

        [Fact]
        public void SetKeyValue_FileNotFound_ReturnsError()
        {
            var path = Path.Combine(_tempDir, "nonexistent.txt");
            var result = _sut.SetKeyValue(path, "Key", "Value", null, previewOnly: false);
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Not found");
        }

        [Fact]
        public void SetKeyValue_CommentsAreSkipped()
        {
            var path = CreateFile("#Port=old\nPort=7777\n");
            var result = _sut.SetKeyValue(path, "Port", "8888", null, previewOnly: false);
            result.Success.Should().BeTrue();
            var content = File.ReadAllText(path);
            content.Should().Contain("#Port=old");
            content.Should().Contain("Port=8888");
        }

        [Fact]
        public void SetKeyValue_ReturnsNewHash()
        {
            var path = CreateFile("Port=7777\n");
            var oldHash = SafeConfigEditor.ComputeSha256(path);
            var result = _sut.SetKeyValue(path, "Port", "8888", null, previewOnly: false);
            result.NewHash.Should().NotBeNullOrWhiteSpace();
            result.NewHash.Should().NotBe(oldHash);
        }
    }
}
