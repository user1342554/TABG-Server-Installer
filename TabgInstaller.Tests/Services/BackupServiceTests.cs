using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class BackupServiceTests
    {
        private readonly BackupService _sut;

        public BackupServiceTests()
        {
            _sut = new BackupService(new Progress<string>(_ => { }));
        }

        [Theory]
        [InlineData(0L, "B", 0.0)]
        [InlineData(512L, "B", 512.0)]
        [InlineData(1024L, "KB", 1.0)]
        [InlineData(1536L, "KB", 1.5)]
        [InlineData(1048576L, "MB", 1.0)]
        [InlineData(1073741824L, "GB", 1.0)]
        [InlineData(1610612736L, "GB", 1.5)]
        public void FormatFileSize_FormatsCorrectly(long bytes, string expectedUnit, double expectedValue)
        {
            var result = _sut.FormatFileSize(bytes);
            result.Should().EndWith(expectedUnit);
            // Parse out the numeric part using the current locale (method uses default formatting)
            var numPart = result.Substring(0, result.Length - expectedUnit.Length).Trim();
            double.TryParse(numPart, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out var parsedValue).Should().BeTrue();
            parsedValue.Should().BeApproximately(expectedValue, 0.01);
        }

        [Fact]
        public void GetAvailableBackups_EmptyDirectory_ReturnsEmptyList()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                var result = _sut.GetAvailableBackups(tempDir);
                result.Should().BeEmpty();
            }
            finally { Directory.Delete(tempDir, true); }
        }

        [Fact]
        public void GetAvailableBackups_WithBackupDirs_ReturnsBackupInfos()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            var backupsDir = Path.Combine(tempDir, "backup");
            Directory.CreateDirectory(backupsDir);
            Directory.CreateDirectory(Path.Combine(backupsDir, "backup 1 2024-01-15 14-30-25"));
            Directory.CreateDirectory(Path.Combine(backupsDir, "backup 2 2024-02-20 10-00-00"));
            try
            {
                var result = _sut.GetAvailableBackups(tempDir);
                result.Should().HaveCount(2);
                result[0].CreatedDate.Should().BeAfter(result[1].CreatedDate);
            }
            finally { Directory.Delete(tempDir, true); }
        }

        [Fact]
        public void GetAvailableBackups_BackupNameParsing_ExtractsDate()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            var backupsDir = Path.Combine(tempDir, "backup");
            Directory.CreateDirectory(backupsDir);
            Directory.CreateDirectory(Path.Combine(backupsDir, "backup 1 2024-06-15 14-30-25"));
            try
            {
                var result = _sut.GetAvailableBackups(tempDir);
                result.Should().HaveCount(1);
                result[0].CreatedDate.Year.Should().Be(2024);
                result[0].CreatedDate.Month.Should().Be(6);
                result[0].CreatedDate.Day.Should().Be(15);
            }
            finally { Directory.Delete(tempDir, true); }
        }

        [Fact]
        public void GetAvailableBackups_NonStandardName_UsesCreationTime()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            var backupsDir = Path.Combine(tempDir, "backup");
            Directory.CreateDirectory(backupsDir);
            Directory.CreateDirectory(Path.Combine(backupsDir, "my-custom-backup"));
            try
            {
                var result = _sut.GetAvailableBackups(tempDir);
                result.Should().HaveCount(1);
                result[0].Name.Should().Be("my-custom-backup");
            }
            finally { Directory.Delete(tempDir, true); }
        }
    }
}
