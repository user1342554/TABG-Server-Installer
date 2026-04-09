using System;
using System.Collections.Generic;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Services;

/// <summary>
/// Legacy app-state bag — no longer registered in DI or referenced by any active code.
/// TODO: Remove this class once the migration is verified clean.
/// </summary>
public class AppState
{
    public string ServerDir { get; private set; } = "";
    public string PluginsDir => string.IsNullOrEmpty(ServerDir) ? "" : System.IO.Path.Combine(ServerDir, "BepInEx", "plugins");
    public bool IsServerConfigured => !string.IsNullOrEmpty(ServerDir);
    public GameSettingsDynamicViewModel? GameSettingsVm { get; set; }
    public ServerProcessService? ServerProcess { get; set; }

    // Collapsed category state (persists across tab switches)
    public HashSet<string> InstallerCollapsed { get; } = new() { "core", "gameplay", "content", "social", "modes" };
    public HashSet<string> ClientCollapsed { get; } = new() { "gameplay", "weapons", "utility", "modes" };

    public event Action? OnServerConfigured;
    public event Action? OnStateChanged;

    public void SetServerDir(string serverDir)
    {
        ServerDir = serverDir;
        GlobalServerPath.Set(serverDir);

        var gsPath = System.IO.Path.Combine(serverDir, "game_settings.txt");
        if (System.IO.File.Exists(gsPath))
        {
            var gs = ConfigIO.ReadGameSettings(gsPath);
            GameSettingsVm = new GameSettingsDynamicViewModel(gs, new ConfigValidationService());
        }

        ServerProcess = new ServerProcessService(serverDir);
        OnServerConfigured?.Invoke();
        OnStateChanged?.Invoke();
    }

    public void NotifyStateChanged() => OnStateChanged?.Invoke();
}
