using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.App.Services;
using Xunit;

namespace TabgInstaller.Tests.App;

public sealed class ServerProfileStoreTests : IDisposable
{
    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "tabg-profile-store-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Add_profile_persists_across_store_instances()
    {
        var serverPath = Path.Combine(_storage, "server-one");
        var first = new ServerProfileStore(_storage);

        var profile = first.AddOrUpdate(serverPath, "Server Eins");
        var reloaded = new ServerProfileStore(_storage);

        reloaded.Profiles.Should().ContainSingle();
        reloaded.ActiveProfileId.Should().Be(profile.Id);
        reloaded.ActiveProfile!.DisplayName.Should().Be("Server Eins");
        reloaded.ActiveProfile.ServerPath.Should().Be(Path.GetFullPath(serverPath));
    }

    [Fact]
    public void Same_path_updates_instead_of_duplicating()
    {
        var store = new ServerProfileStore(_storage);
        var serverPath = Path.Combine(_storage, "server-one");

        var first = store.AddOrUpdate(serverPath, "Alt");
        var updated = store.AddOrUpdate(serverPath + Path.DirectorySeparatorChar, "Neu");

        store.Profiles.Should().ContainSingle();
        updated.Id.Should().Be(first.Id);
        updated.DisplayName.Should().Be("Neu");
    }

    [Fact]
    public void Removing_active_profile_selects_remaining_profile()
    {
        var store = new ServerProfileStore(_storage);
        var first = store.AddOrUpdate(Path.Combine(_storage, "one"), "Eins");
        var second = store.AddOrUpdate(Path.Combine(_storage, "two"), "Zwei");

        store.Remove(second.Id).Should().BeTrue();

        store.ActiveProfileId.Should().Be(first.Id);
        store.Profiles.Should().ContainSingle();
    }

    public void Dispose()
    {
        if (Directory.Exists(_storage))
            Directory.Delete(_storage, true);
    }
}
