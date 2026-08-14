using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.App.Models;
using Xunit;

namespace TabgInstaller.Tests.App;

public sealed class ServerUiStateEvaluatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "tabg-ui-state-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Missing_directory_requires_setup()
    {
        var state = ServerUiStateEvaluator.Inspect(Path.Combine(_root, "missing"), false, false);

        state.Readiness.Should().Be(ServerReadiness.MissingPath);
        state.Runtime.Should().Be(ServerRuntimeUiState.SetupRequired);
        state.PrimaryAction.Should().Be("Server einrichten");
        state.CanStart.Should().BeFalse();
    }

    [Fact]
    public void Server_files_without_bepinex_require_preparation()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "TABG-DS.x86_64"), string.Empty);

        var state = ServerUiStateEvaluator.Inspect(_root, false, false);

        state.Readiness.Should().Be(ServerReadiness.NeedsPreparation);
        state.PrimaryAction.Should().Be("Server vorbereiten");
        state.NeedsAttention.Should().BeTrue();
    }

    [Fact]
    public void Prepared_server_is_ready_to_start()
    {
        Directory.CreateDirectory(Path.Combine(_root, "BepInEx", "plugins"));
        File.WriteAllText(Path.Combine(_root, "TABG-DS.x86_64"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "game_settings.txt"), "ServerName=Test");
        File.WriteAllText(Path.Combine(_root, "BepInEx", "plugins", "MatchCore.dll"), string.Empty);

        var state = ServerUiStateEvaluator.Inspect(_root, false, false);

        state.Readiness.Should().Be(ServerReadiness.Ready);
        state.Runtime.Should().Be(ServerRuntimeUiState.Stopped);
        state.CanStart.Should().BeTrue();
        state.InstalledPluginCount.Should().Be(1);
    }

    [Fact]
    public void Running_server_exposes_stop_instead_of_start()
    {
        Directory.CreateDirectory(Path.Combine(_root, "BepInEx"));
        File.WriteAllText(Path.Combine(_root, "TABG-DS.x86_64"), string.Empty);

        var state = ServerUiStateEvaluator.Inspect(_root, true, false);

        state.Runtime.Should().Be(ServerRuntimeUiState.Running);
        state.CanStart.Should().BeFalse();
        state.CanStop.Should().BeTrue();
        state.PrimaryAction.Should().Be("Server läuft");
    }

    [Fact]
    public void Busy_state_suppresses_conflicting_actions()
    {
        Directory.CreateDirectory(Path.Combine(_root, "BepInEx"));
        File.WriteAllText(Path.Combine(_root, "TABG-DS.x86_64"), string.Empty);

        var state = ServerUiStateEvaluator.Inspect(_root, false, true);

        state.Runtime.Should().Be(ServerRuntimeUiState.Busy);
        state.CanStart.Should().BeFalse();
        state.CanStop.Should().BeFalse();
        state.CanConfigure.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
