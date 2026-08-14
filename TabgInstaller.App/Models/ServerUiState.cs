using System;
using System.IO;
using System.Linq;

namespace TabgInstaller.App.Models;

public enum ServerReadiness
{
    MissingPath,
    MissingServerFiles,
    NeedsPreparation,
    Ready,
}

public enum ServerRuntimeUiState
{
    SetupRequired,
    Busy,
    Stopped,
    Running,
}

public sealed record ServerUiState(
    ServerReadiness Readiness,
    ServerRuntimeUiState Runtime,
    string Title,
    string Description,
    string PrimaryAction,
    bool CanStart,
    bool CanStop,
    bool CanConfigure,
    bool NeedsAttention,
    bool PathExists,
    bool ServerExecutableExists,
    bool ModLoaderExists,
    bool ConfigExists,
    int InstalledPluginCount);

public static class ServerUiStateEvaluator
{
    private static readonly string[] ServerExecutableCandidates =
    {
        "run_bepinex.sh",
        "TABG-DS.x86_64",
        "TABG.x86_64",
        "TotallyAccurateBattlegroundsDedicatedServer.x86_64",
        "TABG-DS.exe",
        "TABG.exe",
    };

    public static ServerUiState Inspect(string? serverPath, bool isRunning, bool isBusy)
    {
        var path = serverPath?.Trim() ?? string.Empty;
        var pathExists = path.Length > 0 && Directory.Exists(path);
        var executableExists = pathExists && HasServerExecutable(path);
        var modLoaderExists = pathExists && Directory.Exists(Path.Combine(path, "BepInEx"));
        var configExists = pathExists && File.Exists(Path.Combine(path, "game_settings.txt"));
        var pluginDir = pathExists ? Path.Combine(path, "BepInEx", "plugins") : string.Empty;
        var pluginCount = Directory.Exists(pluginDir)
            ? Directory.EnumerateFiles(pluginDir, "*.dll", SearchOption.AllDirectories).Count()
            : 0;

        var readiness = !pathExists
            ? ServerReadiness.MissingPath
            : !executableExists
                ? ServerReadiness.MissingServerFiles
                : !modLoaderExists
                    ? ServerReadiness.NeedsPreparation
                    : ServerReadiness.Ready;

        var runtime = isBusy
            ? ServerRuntimeUiState.Busy
            : isRunning
                ? ServerRuntimeUiState.Running
                : readiness == ServerReadiness.Ready
                    ? ServerRuntimeUiState.Stopped
                    : ServerRuntimeUiState.SetupRequired;

        return Build(
            readiness,
            runtime,
            pathExists,
            executableExists,
            modLoaderExists,
            configExists,
            pluginCount);
    }

    public static bool HasServerExecutable(string serverPath)
    {
        if (!Directory.Exists(serverPath))
            return false;

        if (ServerExecutableCandidates.Any(name => File.Exists(Path.Combine(serverPath, name))))
            return true;

        return Directory.EnumerateFiles(serverPath, "TABG*", SearchOption.TopDirectoryOnly)
            .Any(path => !path.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
    }

    private static ServerUiState Build(
        ServerReadiness readiness,
        ServerRuntimeUiState runtime,
        bool pathExists,
        bool executableExists,
        bool modLoaderExists,
        bool configExists,
        int pluginCount)
    {
        var (title, description, primaryAction) = runtime switch
        {
            ServerRuntimeUiState.Busy => (
                "Server wird vorbereitet",
                "Die aktuelle Aufgabe wird abgeschlossen. Technische Details findest du unter Diagnose.",
                "Bitte warten"),
            ServerRuntimeUiState.Running => (
                "Server läuft",
                "Der Serverprozess ist aktiv. Änderungen werden beim nächsten Neustart wirksam.",
                "Server läuft"),
            ServerRuntimeUiState.Stopped => (
                "Bereit zum Starten",
                "Serverdateien und Mod-Loader wurden gefunden.",
                "Server starten"),
            _ when readiness == ServerReadiness.MissingPath => (
                "Einrichtung erforderlich",
                "Wähle einen vorhandenen Server oder installiere einen neuen.",
                "Server einrichten"),
            _ when readiness == ServerReadiness.MissingServerFiles => (
                "Serverdateien fehlen",
                "Der gewählte Ordner enthält noch keinen startbaren TABG Dedicated Server.",
                "Installation fortsetzen"),
            _ => (
                "Server muss vorbereitet werden",
                "TABG wurde gefunden, aber BepInEx und die Kern-Erweiterungen fehlen noch.",
                "Server vorbereiten"),
        };

        return new ServerUiState(
            readiness,
            runtime,
            title,
            description,
            primaryAction,
            runtime == ServerRuntimeUiState.Stopped,
            runtime == ServerRuntimeUiState.Running,
            readiness == ServerReadiness.Ready && !isBusy(runtime),
            readiness != ServerReadiness.Ready,
            pathExists,
            executableExists,
            modLoaderExists,
            configExists,
            pluginCount);

        static bool isBusy(ServerRuntimeUiState value) => value == ServerRuntimeUiState.Busy;
    }
}
