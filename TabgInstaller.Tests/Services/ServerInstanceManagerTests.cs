using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ServerInstanceManagerTests : IDisposable
    {
        private readonly string _tempDir;

        public ServerInstanceManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private ServerInstanceManager CreateSut() => new(_tempDir);

        [Fact]
        public void InitialState_HasNoInstances()
        {
            var sut = CreateSut();
            sut.InstanceDataList.Should().BeEmpty();
            sut.ActiveInstance.Should().BeNull();
        }

        [Fact]
        public void AddLocalInstance_CreatesInstanceAndSetsActive()
        {
            var serverDir = Path.Combine(_tempDir, "server1");
            Directory.CreateDirectory(serverDir);

            var sut = CreateSut();
            var instance = sut.AddLocalInstance("Test Server", serverDir);

            sut.InstanceDataList.Should().HaveCount(1);
            sut.InstanceDataList[0].DisplayName.Should().Be("Test Server");
            sut.ActiveInstance.Should().BeSameAs(instance);
        }

        [Fact]
        public void AddLocalInstance_FiresActiveInstanceChanged()
        {
            var serverDir = Path.Combine(_tempDir, "server1");
            Directory.CreateDirectory(serverDir);

            var sut = CreateSut();
            bool fired = false;
            sut.ActiveInstanceChanged += () => fired = true;
            sut.AddLocalInstance("Test", serverDir);
            fired.Should().BeTrue();
        }

        [Fact]
        public void RemoveInstance_RemovesFromList()
        {
            var dir1 = Path.Combine(_tempDir, "s1");
            var dir2 = Path.Combine(_tempDir, "s2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);

            var sut = CreateSut();
            sut.AddLocalInstance("S1", dir1);
            var i2 = sut.AddLocalInstance("S2", dir2);
            var id1 = sut.InstanceDataList[0].Id;

            sut.RemoveInstance(id1);
            sut.InstanceDataList.Should().HaveCount(1);
            sut.InstanceDataList[0].DisplayName.Should().Be("S2");
        }

        [Fact]
        public void RemoveInstance_CannotRemoveLastInstance()
        {
            var dir = Path.Combine(_tempDir, "s1");
            Directory.CreateDirectory(dir);

            var sut = CreateSut();
            sut.AddLocalInstance("S1", dir);
            var id = sut.InstanceDataList[0].Id;

            var act = () => sut.RemoveInstance(id);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SetActiveInstance_SwitchesActive()
        {
            var dir1 = Path.Combine(_tempDir, "s1");
            var dir2 = Path.Combine(_tempDir, "s2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);

            var sut = CreateSut();
            sut.AddLocalInstance("S1", dir1);
            sut.AddLocalInstance("S2", dir2);

            var id1 = sut.InstanceDataList[0].Id;
            sut.SetActiveInstance(id1);
            sut.ActiveInstance!.ServerPath.Should().Be(dir1);
        }

        [Fact]
        public void RenameInstance_UpdatesDisplayName()
        {
            var dir = Path.Combine(_tempDir, "s1");
            Directory.CreateDirectory(dir);

            var sut = CreateSut();
            sut.AddLocalInstance("Old Name", dir);
            var id = sut.InstanceDataList[0].Id;

            sut.RenameInstance(id, "New Name");
            sut.InstanceDataList[0].DisplayName.Should().Be("New Name");
        }

        [Fact]
        public void SaveAndLoad_PersistsInstances()
        {
            var dir = Path.Combine(_tempDir, "s1");
            Directory.CreateDirectory(dir);

            var sut = CreateSut();
            sut.AddLocalInstance("Persisted", dir);
            sut.Save();

            var sut2 = CreateSut();
            sut2.Load();
            sut2.InstanceDataList.Should().HaveCount(1);
            sut2.InstanceDataList[0].DisplayName.Should().Be("Persisted");
        }

        [Fact]
        public void Load_RestoresActiveInstanceId()
        {
            var dir1 = Path.Combine(_tempDir, "s1");
            var dir2 = Path.Combine(_tempDir, "s2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);

            var sut = CreateSut();
            sut.AddLocalInstance("S1", dir1);
            sut.AddLocalInstance("S2", dir2);
            var id1 = sut.InstanceDataList[0].Id;
            sut.SetActiveInstance(id1);
            sut.Save();

            var sut2 = CreateSut();
            sut2.Load();
            sut2.ActiveInstanceData!.Id.Should().Be(id1);
        }

        [Fact]
        public void MigrateFromSingleServer_CreatesInstanceFromPath()
        {
            var serverDir = Path.Combine(_tempDir, "legacyserver");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "game_settings.txt"),
                "ServerName=My Legacy Server\nMaxPlayers=70\n");

            var sut = CreateSut();
            sut.MigrateFromSingleServer(serverDir);

            sut.InstanceDataList.Should().HaveCount(1);
            sut.InstanceDataList[0].DisplayName.Should().Be("My Legacy Server");
            sut.InstanceDataList[0].ServerPath.Should().Be(serverDir);
            sut.ActiveInstance.Should().NotBeNull();
        }

        [Fact]
        public void MigrateFromSingleServer_FallsBackToDefaultName()
        {
            var serverDir = Path.Combine(_tempDir, "legacyserver");
            Directory.CreateDirectory(serverDir);

            var sut = CreateSut();
            sut.MigrateFromSingleServer(serverDir);

            sut.InstanceDataList.Should().HaveCount(1);
            sut.InstanceDataList[0].DisplayName.Should().Be("Server");
        }
    }
}
