using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class RegistryServiceTests : IDisposable
    {
        private readonly string _cacheDir;
        private readonly string _cachePath;
        private readonly Mock<GitHubService> _gitHub;
        private readonly RegistryService _sut;

        public RegistryServiceTests()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_cacheDir);
            _cachePath = Path.Combine(_cacheDir, "registry-cache.json");

            _gitHub = new Mock<GitHubService>(
                new HttpClient(),
                new Progress<string>(_ => { }))
            { CallBase = false };

            _sut = new RegistryService(_gitHub.Object, _cachePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, true);
        }

        private static PluginRegistryResponse CreateTestRegistry(params PluginManifest[] plugins)
        {
            return new PluginRegistryResponse
            {
                Version = 1,
                GeneratedAt = "2026-04-10T12:00:00Z",
                Plugins = plugins.ToList()
            };
        }

        private static PluginManifest CreateTestManifest(string id = "test-plugin", string version = "1.0.0")
        {
            return new PluginManifest
            {
                Id = id,
                Name = $"Test {id}",
                Version = version,
                Description = "A test plugin.",
                Author = "TestAuthor",
                DownloadUrl = $"https://github.com/TestAuthor/{id}/releases/latest",
                DllNames = new[] { $"{id}.dll" },
                Type = "server",
                CompatibleTabgVersions = new[] { "*" },
                MinInstallerVersion = "4.0.0",
                BepInExVersion = "5.4.22"
            };
        }

        [Fact]
        public async Task FetchRegistryAsync_Success_ReturnsPlugins()
        {
            var registry = CreateTestRegistry(CreateTestManifest());
            var json = JsonConvert.SerializeObject(registry);

            _gitHub.Setup(g => g.FetchFileContentAsync("user1342554", "TABG-Server-Installer", "registry/registry.json"))
                .ReturnsAsync(json);

            var result = await _sut.FetchRegistryAsync();

            result.Should().NotBeNull();
            result!.Plugins.Should().HaveCount(1);
            result.Plugins[0].Id.Should().Be("test-plugin");
        }

        [Fact]
        public async Task FetchRegistryAsync_Success_WritesCache()
        {
            var registry = CreateTestRegistry(CreateTestManifest());
            var json = JsonConvert.SerializeObject(registry);

            _gitHub.Setup(g => g.FetchFileContentAsync("user1342554", "TABG-Server-Installer", "registry/registry.json"))
                .ReturnsAsync(json);

            await _sut.FetchRegistryAsync();

            File.Exists(_cachePath).Should().BeTrue();
            var cached = JsonConvert.DeserializeObject<PluginRegistryResponse>(File.ReadAllText(_cachePath));
            cached!.Plugins.Should().HaveCount(1);
        }

        [Fact]
        public async Task FetchRegistryAsync_NetworkFailure_ReturnsCachedVersion()
        {
            var registry = CreateTestRegistry(CreateTestManifest("cached-plugin"));
            File.WriteAllText(_cachePath, JsonConvert.SerializeObject(registry));

            _gitHub.Setup(g => g.FetchFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            var result = await _sut.FetchRegistryAsync();

            result.Should().NotBeNull();
            result!.Plugins.Should().HaveCount(1);
            result.Plugins[0].Id.Should().Be("cached-plugin");
        }

        [Fact]
        public async Task FetchRegistryAsync_NoNetworkNoCache_ReturnsNull()
        {
            _gitHub.Setup(g => g.FetchFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            var result = await _sut.FetchRegistryAsync();

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCachedRegistry_AfterFetch_ReturnsCachedData()
        {
            var registry = CreateTestRegistry(CreateTestManifest());
            var json = JsonConvert.SerializeObject(registry);

            _gitHub.Setup(g => g.FetchFileContentAsync("user1342554", "TABG-Server-Installer", "registry/registry.json"))
                .ReturnsAsync(json);

            await _sut.FetchRegistryAsync();
            var cached = _sut.GetCachedRegistry();

            cached.Should().NotBeNull();
            cached!.Plugins.Should().HaveCount(1);
        }
    }
}
