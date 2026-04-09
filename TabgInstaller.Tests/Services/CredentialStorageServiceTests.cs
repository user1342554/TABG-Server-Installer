using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class CredentialStorageServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly CredentialStorageService _sut;

        public CredentialStorageServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _sut = new CredentialStorageService(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void Store_ThenRetrieve_RoundTrips()
        {
            var id = Guid.NewGuid();
            _sut.Store(id, "password", "my-secret-password");
            var result = _sut.Retrieve(id, "password");
            result.Should().Be("my-secret-password");
        }

        [Fact]
        public void Retrieve_NonExistent_ReturnsNull()
        {
            var result = _sut.Retrieve(Guid.NewGuid(), "password");
            result.Should().BeNull();
        }

        [Fact]
        public void Store_OverwritesExisting()
        {
            var id = Guid.NewGuid();
            _sut.Store(id, "password", "old");
            _sut.Store(id, "password", "new");
            _sut.Retrieve(id, "password").Should().Be("new");
        }

        [Fact]
        public void Remove_DeletesAllCredentialsForInstance()
        {
            var id = Guid.NewGuid();
            _sut.Store(id, "password", "secret");
            _sut.Store(id, "passphrase", "other-secret");
            _sut.Remove(id);
            _sut.Retrieve(id, "password").Should().BeNull();
            _sut.Retrieve(id, "passphrase").Should().BeNull();
        }

        [Fact]
        public void Store_MultipleInstances_Independent()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            _sut.Store(id1, "password", "secret1");
            _sut.Store(id2, "password", "secret2");
            _sut.Retrieve(id1, "password").Should().Be("secret1");
            _sut.Retrieve(id2, "password").Should().Be("secret2");
        }

        [Fact]
        public void PersistsAcrossInstances()
        {
            var id = Guid.NewGuid();
            _sut.Store(id, "password", "persisted-secret");

            // Create a new instance pointing to the same directory
            var sut2 = new CredentialStorageService(_tempDir);
            sut2.Retrieve(id, "password").Should().Be("persisted-secret");
        }
    }
}
