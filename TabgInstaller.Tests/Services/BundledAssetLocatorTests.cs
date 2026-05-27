using System.IO;
using FluentAssertions;
using TabgInstaller.Core;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class BundledAssetLocatorTests
    {
        [Fact]
        public void FindFile_FindsServerPluginFromSharedBundledFolder()
        {
            var path = BundledAssetLocator.FindFile(Path.Combine("plugins", "Citruslib.dll"));

            path.Should().NotBeNull();
            Path.GetFileName(path).Should().Be("Citruslib.dll");
        }

        [Fact]
        public void FindFile_FindsClientPluginFromSharedBundledFolder()
        {
            var path = BundledAssetLocator.FindFile(Path.Combine("client-plugins", "TabgInstaller.PopupBlocker.dll"));

            path.Should().NotBeNull();
            Path.GetFileName(path).Should().Be("TabgInstaller.PopupBlocker.dll");
        }

        [Fact]
        public void FindDirectory_ReturnsSharedBundledDirectory()
        {
            var path = BundledAssetLocator.FindServerPluginsDirectory();

            path.Should().NotBeNull();
            Directory.GetFiles(path!, "*.dll").Should().NotBeEmpty();
        }
    }
}
