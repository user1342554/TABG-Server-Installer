using System;
using System.Reflection;
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class UpdateServiceTests
    {
        private static Version? InvokeParseVersion(string tag)
        {
            var method = typeof(UpdateService).GetMethod("ParseVersion", BindingFlags.NonPublic | BindingFlags.Static);
            return method?.Invoke(null, new object[] { tag }) as Version;
        }

        [Theory]
        [InlineData("v1.2.3", 1, 2, 3, 0)]
        [InlineData("V1.2.3", 1, 2, 3, 0)]
        [InlineData("1.2.3", 1, 2, 3, 0)]
        [InlineData("v4.0.0", 4, 0, 0, 0)]
        [InlineData("v1.0.0.0", 1, 0, 0, 0)]
        public void ParseVersion_ValidTag_ReturnsCorrectVersion(string tag, int major, int minor, int build, int revision)
        {
            var result = InvokeParseVersion(tag);
            result.Should().NotBeNull();
            result!.Major.Should().Be(major);
            result.Minor.Should().Be(minor);
            result.Build.Should().Be(build);
            result.Revision.Should().Be(revision);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-version")]
        [InlineData("vabc")]
        public void ParseVersion_InvalidTag_ReturnsNull(string tag)
        {
            var result = InvokeParseVersion(tag);
            result.Should().BeNull();
        }

        [Fact]
        public void GetCurrentVersion_ReturnsNonNull()
        {
            var version = UpdateService.GetCurrentVersion();
            version.Should().NotBeNull();
        }

        [Fact]
        public void ParseVersion_NormalizesThreeComponentTo4()
        {
            var result = InvokeParseVersion("v1.3.0");
            result.Should().NotBeNull();
            result!.Revision.Should().Be(0);
        }
    }
}
