using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;

namespace TabgInstaller.LinuxGui;

public sealed class MainWindow : Window
{
    private const int MaxVisibleLogChars = 120_000;

    private readonly TextBox _serverPath = new();
    private readonly TextBox _clientPath = new();
    private readonly TextBox _moddedClientPath = new();
    private readonly TextBox _steamUser = new() { Text = "anonymous", Width = 150 };
    private readonly TextBox _steamPassword = new() { Width = 150, PasswordChar = '*' };
    private readonly TextBox _steamGuard = new() { Width = 90, Watermark = "optional" };
    private readonly TextBox _citrusTag = new() { Text = "v0.7", Width = 90 };
    private readonly TextBox _log = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
    };

    private readonly TextBox _rawConfig = new()
    {
        AcceptsReturn = true,
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
    };

    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100 };
    private readonly TextBlock _status = new() { Text = "Idle" };
    private TabControl? _tabs;
    private readonly StackPanel _pluginChecks = new() { Spacing = 4 };
    private readonly StackPanel _clientModChecks = new() { Spacing = 4 };
    private readonly ListBox _backups = new();
    private readonly ListBox _marketplace = new();
    private readonly TextBox _marketplaceSearch = new() { Width = 240, Watermark = "Search plugins" };
    private readonly ComboBox _marketplaceType = new()
    {
        Width = 120,
        ItemsSource = new[] { "All", "Server", "Client", "Both" },
        SelectedIndex = 0,
    };
    private readonly TextBlock _marketplaceDetails = new()
    {
        Text = "Select a plugin to see details.",
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
    };
    private readonly List<PluginManifest> _registryPlugins = new();
    private readonly ServerPathProvider _serverPathProvider = new();
    private readonly ServerProcessService _serverProcess;
    private readonly string _logPath;
    private readonly object _logBufferLock = new();
    private readonly StringBuilder _pendingLog = new();
    private Process? _clientProcess;
    private CancellationTokenSource? _installCts;
    private bool _logFlushQueued;

    public MainWindow()
    {
        _serverProcess = new ServerProcessService(_serverPathProvider);
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabgInstaller");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, "linux-gui.log");

        Title = "TABG Server Installer - Linux";
        Width = 1180;
        Height = 760;
        MinWidth = 900;
        MinHeight = 620;
        Opened += (_, _) => Log("Main window opened.");
        Closing += (_, _) => Log("Main window closing.");
        Closed += (_, _) => Log("Main window closed.");

        LoadPluginRegistry();
        BuildUi();
        WireMarketplace();
        WireServerProcess();
        TryAutoDetectPaths();
        Log("Linux GUI started. Log file: " + _logPath);
    }

    private void BuildUi()
    {
        var root = new DockPanel { LastChildFill = true, Margin = new Avalonia.Thickness(10) };
        var statusBar = new DockPanel { LastChildFill = true, Margin = new Avalonia.Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(statusBar, Dock.Top);
        statusBar.Children.Add(_status);
        root.Children.Add(statusBar);

        DockPanel.SetDock(_log, Dock.Bottom);
        _log.Height = 160;
        root.Children.Add(_log);

        _tabs = new TabControl();
        _tabs.Items.Add(new TabItem { Header = "Install", Content = BuildInstallTab() });
        _tabs.Items.Add(new TabItem { Header = "Server", Content = BuildServerTab() });
        _tabs.Items.Add(new TabItem { Header = "Config", Content = BuildConfigTab() });
        _tabs.Items.Add(new TabItem { Header = "Backups", Content = BuildBackupsTab() });
        _tabs.Items.Add(new TabItem { Header = "Client", Content = BuildClientTab() });
        _tabs.Items.Add(new TabItem { Header = "Marketplace", Content = BuildMarketplaceTab() });
        _tabs.Items.Add(new TabItem { Header = "Reference", Content = BuildReferenceTab() });
        _tabs.Items.Add(new TabItem { Header = "Settings", Content = BuildSettingsTab() });
        root.Children.Add(_tabs);

        Content = root;
    }

    private Control BuildInstallTab()
    {
        RebuildPluginChecks();

        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(6) };
        panel.Children.Add(PathRow("Server folder", _serverPath, BrowseServerAsync));
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Label("Citruslib tag"),
                _citrusTag,
                Button("Create folder", CreateServerFolder),
                Button("SteamCMD install/update", InstallOrUpdateDedicatedServerAsync),
                Button("Detect server", DetectServerPath),
                Button("Open Marketplace", SelectMarketplaceTab)
            }
        });
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Label("Steam user"),
                _steamUser,
                Label("Steam password"),
                _steamPassword,
                Label("Steam Guard"),
                _steamGuard
            }
        });
        panel.Children.Add(new TextBlock { Text = "This prepares BepInEx and core server files. Install mods from Marketplace." });
        panel.Children.Add(_progress);
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Button("Prepare / repair server", InstallServerAsync),
                Button("Cancel install", () => _installCts?.Cancel()),
                Button("Reload registry", () => { LoadPluginRegistry(); RebuildPluginChecks(); RebuildClientModChecks(); RebuildMarketplace(); })
            }
        });
        return panel;
    }

    private Control BuildServerTab()
    {
        var args = new TextBox { Text = "-batchmode -nographics -nolog" };
        return new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(6),
            Children =
            {
                new TextBlock { Text = "Uses the server folder from the Install tab." },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { Label("Args"), args }
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        Button("Start", () => StartServer(args.Text ?? "")),
                        Button("Stop", () => _serverProcess.Stop()),
                        Button("Open folder", () => OpenPath(_serverPath.Text)),
                        Button("Open logs", () => OpenPath(Path.Combine(_serverPath.Text ?? "", "BepInEx", "LogOutput.log")))
                    }
                }
            }
        };
    }

    private Control BuildConfigTab()
    {
        return new DockPanel
        {
            Margin = new Avalonia.Thickness(6),
            Children =
            {
                DockTop(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        Button("Load game_settings.txt", LoadConfig),
                        Button("Save game_settings.txt", SaveConfig),
                        Button("Open server folder", () => OpenPath(_serverPath.Text))
                    }
                }),
                _rawConfig
            }
        };
    }

    private Control BuildBackupsTab()
    {
        return new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(6),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        Button("Create backup", CreateBackupAsync),
                        Button("Refresh", RefreshBackups),
                        Button("Restore selected", RestoreBackupAsync),
                        Button("Delete selected", DeleteBackup)
                    }
                },
                _backups
            }
        };
    }

    private Control BuildClientTab()
    {
        RebuildClientModChecks();

        return new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(6),
            Children =
            {
                PathRow("TABG Steam folder", _clientPath, BrowseClientAsync),
                PathRow("Modded copy folder", _moddedClientPath, BrowseModdedClientAsync),
                Button("Detect TABG client", DetectClientPath),
                new TextBlock { Text = "This prepares a modded TABG copy. Install client mods from Marketplace." },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        Button("Prepare / update client", InstallClientModsAsync),
                        Button("Open Marketplace", SelectMarketplaceTab),
                        Button("Start modded client", StartModdedClient),
                        Button("Open modded folder", () => OpenPath(_moddedClientPath.Text))
                    }
                }
            }
        };
    }

    private Control BuildMarketplaceTab()
    {
        RebuildMarketplace();
        return new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(6),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        Label("Search"),
                        _marketplaceSearch,
                        Label("Type"),
                        _marketplaceType,
                        Button("Refresh registry", () => { LoadPluginRegistry(); RebuildMarketplace(); }),
                        Button("Install selected", InstallMarketplacePluginAsync),
                        Button("Uninstall selected", UninstallMarketplacePlugin)
                    }
                },
                new ScrollViewer { Content = _marketplace, Height = 340 },
                new Border
                {
                    BorderThickness = new Avalonia.Thickness(1),
                    Padding = new Avalonia.Thickness(8),
                    Child = _marketplaceDetails
                }
            }
        };
    }

    private Control BuildReferenceTab()
    {
        var list = new ListBox();
        foreach (var file in FindFilesNearApp("Knowledge", "*.json"))
            list.Items.Add(file);

        var viewer = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is string path && File.Exists(path))
                viewer.Text = File.ReadAllText(path);
        };

        return new Grid
        {
            Margin = new Avalonia.Thickness(6),
            ColumnDefinitions = new ColumnDefinitions("300,*"),
            Children =
            {
                Put(list, 0),
                Put(viewer, 1)
            }
        };
    }

    private Control BuildSettingsTab()
    {
        return new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(6),
            Children =
            {
                new TextBlock { Text = "Functional Linux build. Settings are kept minimal in this first pass." },
                Button("Clear log", () => _log.Text = ""),
                Button("Open log file", () => OpenPath(_logPath)),
                Button("Open app folder", () => OpenPath(AppContext.BaseDirectory))
            }
        };
    }

    private void CreateServerFolder()
    {
        var serverDir = _serverPath.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(serverDir))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            serverDir = Path.Combine(home, "TABG-Dedicated-Server");
            _serverPath.Text = serverDir;
        }

        Directory.CreateDirectory(serverDir);
        Log("Server folder ready: " + serverDir);
    }

    private async void InstallOrUpdateDedicatedServerAsync()
    {
        var serverDir = _serverPath.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(serverDir))
        {
            CreateServerFolder();
            serverDir = _serverPath.Text?.Trim() ?? "";
        }

        Directory.CreateDirectory(serverDir);

        var steamCmd = FindSteamCmd();
        if (steamCmd == null)
        {
            Log("SteamCMD was not found. Install it or put steamcmd/steamcmd.sh on PATH.");
            return;
        }

        _installCts?.Cancel();
        _installCts = new CancellationTokenSource();
        SetStatus("Installing dedicated server through SteamCMD...");
        _progress.Value = 0;

        try
        {
            var code = await RunSteamCmdAsync(
                steamCmd,
                serverDir,
                _steamUser.Text?.Trim(),
                _steamPassword.Text,
                _steamGuard.Text?.Trim(),
                _installCts.Token);
            _progress.Value = code == 0 ? 100 : _progress.Value;
            Log(code == 0
                ? "SteamCMD dedicated server install/update finished."
                : $"SteamCMD exited with code {code}.");
            if (code == 8)
            {
                Log("SteamCMD reported No subscription. Install TABG Dedicated Server in Steam once, or login with an account that owns access instead of anonymous.");
            }

            SetStatus(code == 0 ? "Server installed/updated" : "SteamCMD failed");
        }
        catch (OperationCanceledException)
        {
            Log("SteamCMD install/update cancelled.");
            SetStatus("Cancelled");
        }
        catch (Exception ex)
        {
            Log("SteamCMD install/update failed: " + ex.Message);
            SetStatus("SteamCMD failed");
        }
        finally
        {
            _steamPassword.Text = "";
            _steamGuard.Text = "";
            _installCts?.Dispose();
            _installCts = null;
        }
    }

    private async void InstallServerAsync()
    {
        var serverDir = _serverPath.Text?.Trim() ?? "";
        if (!Directory.Exists(serverDir))
        {
            Log("Select a valid server folder first.");
            return;
        }

        var serverExe = ResolveServerExecutable(serverDir);
        if (serverExe == null)
        {
            Log("No TABG dedicated server executable found. SteamCMD must complete successfully before Start can work.");
            return;
        }

        _installCts = new CancellationTokenSource();
        _progress.Value = 0;

        var bundled = new List<string>();
        var skipCitrus = true;
        var skipStarter = true;
        var community = false;

        var progress = new Progress<string>(line =>
        {
            Log(line);
            var pct = ProgressEstimator.Estimate(line);
            if (pct >= 0) _progress.Value = pct;
        });

        try
        {
            SetStatus("Preparing server...");
            var backup = new BackupService(progress);
            if (Directory.GetFileSystemEntries(serverDir).Length > 0)
                await backup.CreateBackupAsync(serverDir);

            using var installer = new Installer(serverDir, progress);
            var code = await installer.RunAsync(
                serverDir,
                "",
                "",
                "",
                "",
                _citrusTag.Text?.Trim() ?? "v0.7",
                skipStarter,
                skipCitrus,
                community,
                bundled,
                _installCts.Token);

            _progress.Value = code == 0 ? 100 : _progress.Value;
            Log(code == 0 ? "Install finished." : $"Install exited with code {code}.");
            SetStatus(code == 0 ? "Install finished" : "Install failed");
        }
        catch (OperationCanceledException)
        {
            Log("Install cancelled.");
            SetStatus("Cancelled");
        }
        catch (Exception ex)
        {
            Log("Install failed: " + ex.Message);
            SetStatus("Install failed");
        }
        finally
        {
            _installCts.Dispose();
            _installCts = null;
        }
    }

    private void StartServer(string args)
    {
        try
        {
            _serverPathProvider.SetPath(_serverPath.Text ?? "");
            var exe = ResolveServerExecutable(_serverPath.Text ?? "");
            if (exe == null)
            {
                Log("No server executable found. SteamCMD did not install the dedicated server yet.");
                Log("If SteamCMD says No subscription, install TABG Dedicated Server from Steam or use an account with access.");
                return;
            }

            Log("Starting server executable: " + exe);
            if (!_serverProcess.Start(args))
                Log("Server is already running.");
            else
                SetStatus("Server running");
        }
        catch (Exception ex)
        {
            Log("Could not start server: " + ex.Message);
            SetStatus("Server start failed");
        }
    }

    private void LoadConfig()
    {
        var file = Path.Combine(_serverPath.Text ?? "", "game_settings.txt");
        _rawConfig.Text = File.Exists(file) ? File.ReadAllText(file) : "";
        Log(File.Exists(file) ? "Loaded game_settings.txt." : "game_settings.txt not found.");
    }

    private void SaveConfig()
    {
        var dir = _serverPath.Text?.Trim() ?? "";
        if (!Directory.Exists(dir))
        {
            Log("Select a valid server folder first.");
            return;
        }

        File.WriteAllText(Path.Combine(dir, "game_settings.txt"), _rawConfig.Text ?? "");
        Log("Saved game_settings.txt.");
    }

    private async void CreateBackupAsync()
    {
        var backup = new BackupService(new Progress<string>(Log));
        await backup.CreateBackupAsync(_serverPath.Text ?? "");
        RefreshBackups();
    }

    private void RefreshBackups()
    {
        _backups.Items.Clear();
        var backup = new BackupService(new Progress<string>(Log));
        foreach (var item in backup.GetAvailableBackups(_serverPath.Text ?? ""))
            _backups.Items.Add(item);
    }

    private async void RestoreBackupAsync()
    {
        if (_backups.SelectedItem is not BackupInfo item) return;
        var backup = new BackupService(new Progress<string>(Log));
        await backup.RestoreBackupAsync(_serverPath.Text ?? "", item);
    }

    private void DeleteBackup()
    {
        if (_backups.SelectedItem is not BackupInfo item) return;
        var backup = new BackupService(new Progress<string>(Log));
        backup.DeleteBackup(item);
        RefreshBackups();
    }

    private async void InstallClientModsAsync()
    {
        var ok = await ClientModInstaller.InstallAsync(
            _clientPath.Text ?? "",
            _moddedClientPath.Text ?? "",
            new List<string>(),
            new Progress<string>(Log));

        Log(ok ? "Client prepared." : "Client preparation failed.");
    }

    private void StartModdedClient()
    {
        var clientDir = _moddedClientPath.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(clientDir) || !Directory.Exists(clientDir))
        {
            Log("Select or install a valid modded client folder first.");
            return;
        }

        var executable = ResolveClientLaunchTarget(clientDir);
        if (executable == null)
        {
            Log("No modded TABG launcher found. Install / update client mods first.");
            return;
        }

        try
        {
            if (_clientProcess?.HasExited == false)
            {
                Log("Modded client is already running.");
                return;
            }

            var psi = CreateClientStartInfo(executable, clientDir);
            Log("Starting modded client: " + executable);
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Log("[client] " + e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Log("[client] " + e.Data); };
            process.Exited += (_, _) => Log("Modded client exited with code " + process.ExitCode);

            if (!process.Start())
            {
                Log("Could not start modded client.");
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _clientProcess = process;

            SetStatus("Modded client started");
        }
        catch (Exception ex)
        {
            Log("Could not start modded client: " + ex.Message);
            SetStatus("Client start failed");
        }
    }

    private async void InstallMarketplacePluginAsync()
    {
        var manifest = GetSelectedMarketplaceManifest();
        if (manifest == null)
        {
            Log("Select a marketplace plugin first.");
            return;
        }

        if (!IsMarketplaceInstallable(manifest))
        {
            Log($"{manifest.Name} is handled by the main Install tab, not Marketplace.");
            return;
        }

        var missingDlls = FindMissingBundledMarketplaceDlls(manifest);
        if (missingDlls.Length > 0)
        {
            Log($"{manifest.Name} cannot be installed because bundled DLLs are missing: {string.Join(", ", missingDlls)}");
            return;
        }

        var serverRoot = _serverPath.Text?.Trim() ?? "";
        var clientPath = _moddedClientPath.Text?.Trim();
        if (!ValidateMarketplaceTargets(manifest, serverRoot, clientPath))
            return;

        if (IsMarketplaceInstalled(manifest))
        {
            Log($"{manifest.Name} is already installed.");
            return;
        }

        var trackerRoot = manifest.Type.Equals("client", StringComparison.OrdinalIgnoreCase)
            ? clientPath!
            : serverRoot;

        var tracker = new InstalledPluginTracker(trackerRoot);
        var service = new MarketplaceInstallService(
            new TabgInstaller.Core.Services.GitHubService(new HttpClient(), new Progress<string>(Log)),
            tracker);

        Log($"Installing marketplace plugin: {manifest.Name}");
        var ok = await service.InstallPluginAsync(
            manifest,
            _registryPlugins,
            serverRoot,
            clientPath);

        if (ok && manifest.RequiresClientMod && !string.IsNullOrWhiteSpace(manifest.ClientPluginId))
        {
            var companion = _registryPlugins.FirstOrDefault(p =>
                p.Id.Equals(manifest.ClientPluginId, StringComparison.OrdinalIgnoreCase));
            if (companion != null)
            {
                Log($"Installing required client plugin: {companion.Name}");
                var clientTracker = new InstalledPluginTracker(clientPath!);
                var clientService = new MarketplaceInstallService(
                    new TabgInstaller.Core.Services.GitHubService(new HttpClient(), new Progress<string>(Log)),
                    clientTracker);
                ok = await clientService.InstallPluginAsync(companion, _registryPlugins, serverRoot, clientPath);
            }
        }

        Log(ok ? $"Installed {manifest.Name}." : $"Failed to install {manifest.Name}.");
        RebuildMarketplace();
        UpdateMarketplaceDetails();
    }

    private void UninstallMarketplacePlugin()
    {
        var manifest = GetSelectedMarketplaceManifest();
        if (manifest == null)
        {
            Log("Select a marketplace plugin first.");
            return;
        }

        var serverRoot = _serverPath.Text?.Trim() ?? "";
        var clientPath = _moddedClientPath.Text?.Trim();
        if (!ValidateMarketplaceTargets(manifest, serverRoot, clientPath))
            return;

        var trackerRoot = manifest.Type.Equals("client", StringComparison.OrdinalIgnoreCase)
            ? clientPath!
            : serverRoot;

        var tracker = new InstalledPluginTracker(trackerRoot);
        var service = new MarketplaceInstallService(
            new TabgInstaller.Core.Services.GitHubService(new HttpClient(), new Progress<string>(Log)),
            tracker);
        var ok = service.UninstallPlugin(manifest.Id, serverRoot, clientPath);

        if (ok && manifest.RequiresClientMod && !string.IsNullOrWhiteSpace(manifest.ClientPluginId) && Directory.Exists(clientPath))
        {
            var clientTracker = new InstalledPluginTracker(clientPath!);
            var clientService = new MarketplaceInstallService(
                new TabgInstaller.Core.Services.GitHubService(new HttpClient(), new Progress<string>(Log)),
                clientTracker);
            clientService.UninstallPlugin(manifest.ClientPluginId, serverRoot, clientPath);
        }

        Log(ok ? $"Uninstalled {manifest.Name}." : $"Could not uninstall {manifest.Name}.");
        RebuildMarketplace();
        UpdateMarketplaceDetails();
    }

    private void LoadPluginRegistry()
    {
        _registryPlugins.Clear();
        var registryPath = FindFileNearApp(Path.Combine("registry", "registry.json"));
        if (registryPath == null)
        {
            Log("registry/registry.json not found; plugin lists may be empty.");
            return;
        }

        var data = JsonConvert.DeserializeObject<PluginRegistryResponse>(File.ReadAllText(registryPath));
        if (data?.Plugins == null) return;
        _registryPlugins.AddRange(data.Plugins);
        PluginRegistry.LoadFromManifests(data.Plugins);
        Log($"Loaded {_registryPlugins.Count} registry plugins.");
    }

    private void RebuildPluginChecks()
    {
        _pluginChecks.Children.Clear();
        var plugins = PluginRegistry.ServerPlugins
            .Where(p => p.DefaultChecked || p.Kind == PluginKind.CoreDependency)
            .ToArray();

        foreach (var item in CollapseDuplicateDefinitions(plugins))
        {
            _pluginChecks.Children.Add(new CheckBox
            {
                Content = item.Label,
                Tag = item.Plugin,
                IsChecked = item.Plugin.DefaultChecked,
            });
        }
    }

    private void RebuildClientModChecks()
    {
        _clientModChecks.Children.Clear();
        var plugins = PluginRegistry.ClientMods
            .Where(p => p.DefaultChecked)
            .ToArray();

        foreach (var item in CollapseDuplicateDefinitions(plugins))
        {
            var plugin = item.Plugin;
            var available = IsBundledClientPluginAvailable(plugin);
            _clientModChecks.Children.Add(new CheckBox
            {
                Content = available ? item.Label : item.Label + " (missing bundled DLL)",
                Tag = plugin,
                IsChecked = available && plugin.DefaultChecked,
                IsEnabled = available,
            });
        }
    }

    private void RebuildMarketplace()
    {
        var selectedId = GetSelectedMarketplaceManifest()?.Id;
        _marketplace.Items.Clear();
        var query = _marketplaceSearch.Text?.Trim();
        var typeFilter = _marketplaceType.SelectedItem?.ToString() ?? "All";

        var plugins = _registryPlugins
            .Where(IsMarketplaceInstallable)
            .Where(p => MarketplaceMatchesFilter(p, query, typeFilter))
            .OrderBy(p => p.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var plugin in plugins)
        {
            var item = CreateMarketplaceItem(plugin);
            _marketplace.Items.Add(item);
            if (selectedId != null && plugin.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                _marketplace.SelectedItem = item;
        }

        Log($"Marketplace loaded {plugins.Count} installable plugins.");
        UpdateMarketplaceDetails();
    }

    private static bool MarketplaceMatchesFilter(PluginManifest plugin, string? query, string typeFilter)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            var haystack = string.Join(" ", new[]
            {
                plugin.Id,
                plugin.Name,
                plugin.Description,
                plugin.Author,
                string.Join(" ", plugin.Tags ?? Array.Empty<string>()),
            });

            if (!haystack.Contains(query, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!typeFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            return plugin.Type.Equals(typeFilter, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private MarketplacePluginItem CreateMarketplaceItem(PluginManifest plugin)
    {
        var missingDlls = FindMissingBundledMarketplaceDlls(plugin);
        if (missingDlls.Length > 0)
            return new MarketplacePluginItem(plugin, "missing DLL");

        if (IsMarketplaceInstalled(plugin))
            return new MarketplacePluginItem(plugin, "installed");

        return new MarketplacePluginItem(plugin, "available");
    }

    private static bool IsMarketplaceInstallable(PluginManifest plugin)
    {
        if (plugin.Kind.Equals("core-dependency", StringComparison.OrdinalIgnoreCase))
            return false;

        if (plugin.Kind.Equals("community-server", StringComparison.OrdinalIgnoreCase))
            return false;

        return plugin.DllNames.Length > 0
            && (plugin.Type.Equals("server", StringComparison.OrdinalIgnoreCase)
                || plugin.Type.Equals("client", StringComparison.OrdinalIgnoreCase)
                || plugin.Type.Equals("both", StringComparison.OrdinalIgnoreCase));
    }

    private bool ValidateMarketplaceTargets(PluginManifest manifest, string serverRoot, string? clientPath)
    {
        var type = manifest.Type.ToLowerInvariant();
        var needsServer = type is "server" or "both";
        var needsClient = type is "client" or "both" || manifest.RequiresClientMod;

        if (needsServer && !Directory.Exists(serverRoot))
        {
            Log("Marketplace server plugin install needs a valid server folder. Use SteamCMD install/update first.");
            return false;
        }

        if (needsClient && (string.IsNullOrWhiteSpace(clientPath) || !Directory.Exists(clientPath)))
        {
            Log("Marketplace client plugin install needs a valid modded client folder. Install client mods first or select the folder.");
            return false;
        }

        return true;
    }

    private void WireMarketplace()
    {
        _marketplace.SelectionChanged += (_, _) => UpdateMarketplaceDetails();
        _marketplaceSearch.TextChanged += (_, _) => RebuildMarketplace();
        _marketplaceType.SelectionChanged += (_, _) => RebuildMarketplace();
    }

    private PluginManifest? GetSelectedMarketplaceManifest()
    {
        return _marketplace.SelectedItem switch
        {
            MarketplacePluginItem item => item.Manifest,
            PluginManifest manifest => manifest,
            _ => null
        };
    }

    private void UpdateMarketplaceDetails()
    {
        var manifest = GetSelectedMarketplaceManifest();
        if (manifest == null)
        {
            _marketplaceDetails.Text = "Select a plugin to see details.";
            return;
        }

        var missingDlls = FindMissingBundledMarketplaceDlls(manifest);
        var status = missingDlls.Length > 0
            ? "Missing bundled DLLs: " + string.Join(", ", missingDlls)
            : IsMarketplaceInstalled(manifest) ? "Installed" : "Available";

        var companion = !string.IsNullOrWhiteSpace(manifest.ClientPluginId)
            ? _registryPlugins.FirstOrDefault(p => p.Id.Equals(manifest.ClientPluginId, StringComparison.OrdinalIgnoreCase))
            : null;

        var lines = new List<string>
        {
            $"{manifest.Name} {manifest.Version}",
            $"Status: {status}",
            $"Type: {manifest.Type}",
            $"Author: {manifest.Author}",
            $"DLLs: {string.Join(", ", manifest.DllNames)}",
        };

        if (manifest.Dependencies.Length > 0)
            lines.Add($"Dependencies: {string.Join(", ", manifest.Dependencies)}");

        if (companion != null)
            lines.Add($"Required client plugin: {companion.Name}");

        if (!string.IsNullOrWhiteSpace(manifest.Description))
            lines.Add(manifest.Description);

        _marketplaceDetails.Text = string.Join(Environment.NewLine, lines);
    }

    private bool IsMarketplaceInstalled(PluginManifest manifest)
    {
        var type = manifest.Type.ToLowerInvariant();
        var root = type == "client"
            ? _moddedClientPath.Text?.Trim()
            : _serverPath.Text?.Trim();

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return false;

        return new InstalledPluginTracker(root).IsInstalled(manifest.Id);
    }

    private static string[] FindMissingBundledMarketplaceDlls(PluginManifest manifest)
    {
        if (!manifest.Kind.Equals("bundled", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        var folder = manifest.Type.Equals("client", StringComparison.OrdinalIgnoreCase)
            ? "client-plugins"
            : "plugins";

        return manifest.DllNames
            .Where(dll => FindFileNearApp(Path.Combine(folder, dll)) == null)
            .ToArray();
    }

    private void WireServerProcess()
    {
        _serverProcess.OutputReceived += Log;
        _serverProcess.ProcessExited += code =>
        {
            Log($"Server exited with code {code}.");
            SetStatus("Server stopped");
        };
    }

    private void TryAutoDetectPaths()
    {
        DetectServerPath();
        DetectClientPath();
    }

    private void DetectServerPath()
    {
        var detected = Installer.TryFindTabgServerPath();
        if (!string.IsNullOrEmpty(detected))
        {
            _serverPath.Text = detected;
            Log("Detected server path: " + detected);
        }
        else
        {
            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "TABG-Dedicated-Server");
            if (string.IsNullOrWhiteSpace(_serverPath.Text))
                _serverPath.Text = fallback;
            Log("Server path was not auto-detected. Suggested path: " + _serverPath.Text);
        }
    }

    private void DetectClientPath()
    {
        var detected = Installer.TryFindTabgClientPath();
        if (!string.IsNullOrEmpty(detected))
        {
            _clientPath.Text = detected;
            _moddedClientPath.Text = Path.Combine(Path.GetDirectoryName(detected) ?? detected, "TotallyAccurateBattlegrounds-Modded");
        }
    }

    private async void BrowseServerAsync() => await PickFolderInto(_serverPath, "Select TABG server folder");
    private async void BrowseClientAsync() => await PickFolderInto(_clientPath, "Select TABG Steam folder");
    private async void BrowseModdedClientAsync() => await PickFolderInto(_moddedClientPath, "Select modded TABG folder");

    private async Task PickFolderInto(TextBox target, string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            target.Text = path;
    }

    private IEnumerable<PluginDefinition> SelectedDefinitions(StackPanel host)
        => host.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => cb.Tag)
            .OfType<PluginDefinition>();

    private static IEnumerable<(PluginDefinition Plugin, string Label)> CollapseDuplicateDefinitions(IEnumerable<PluginDefinition> plugins)
    {
        foreach (var group in plugins.GroupBy(GetPluginDllKey, StringComparer.OrdinalIgnoreCase))
        {
            var plugin = group.First();
            if (group.Count() == 1)
            {
                yield return (plugin, plugin.Label);
                continue;
            }

            var names = group
                .Select(p => p.Label.Split('—')[0].Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var dlls = plugin.DllNames.Length == 0
                ? plugin.Id
                : string.Join(", ", plugin.DllNames);

            yield return (plugin, $"{string.Join(" / ", names)} — {dlls}");
        }
    }

    private static string GetPluginDllKey(PluginDefinition plugin)
    {
        return plugin.DllNames.Length == 0
            ? "id:" + plugin.Id
            : "dll:" + string.Join("|", plugin.DllNames.OrderBy(dll => dll, StringComparer.OrdinalIgnoreCase));
    }

    private static void SetChecks(StackPanel host, bool value)
    {
        foreach (var cb in host.Children.OfType<CheckBox>())
            cb.IsChecked = value;
    }

    private void SelectSigmaPreset()
    {
        foreach (var cb in _pluginChecks.Children.OfType<CheckBox>())
        {
            if (cb.Tag is PluginDefinition plugin)
                cb.IsChecked = PluginRegistry.SigmaPresetIds.Contains(plugin.Id);
        }
    }

    private void SelectMarketplaceTab()
    {
        if (_tabs != null)
            _tabs.SelectedIndex = 5;
    }

    private void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var target = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (string.IsNullOrEmpty(target) || !Directory.Exists(target))
        {
            Log("Path does not exist: " + path);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "explorer" : "xdg-open",
                Arguments = $"\"{target}\"",
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Log("Could not open path: " + ex.Message);
        }
    }

    private void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never crash the app.
        }

        QueueVisibleLogLine(line);
    }

    private void QueueVisibleLogLine(string line)
    {
        lock (_logBufferLock)
        {
            _pendingLog.AppendLine(line);
            if (_logFlushQueued)
                return;

            _logFlushQueued = true;
        }

        Dispatcher.UIThread.Post(FlushVisibleLog);
    }

    private void FlushVisibleLog()
    {
        string chunk;
        lock (_logBufferLock)
        {
            if (_pendingLog.Length == 0)
            {
                _logFlushQueued = false;
                return;
            }

            chunk = _pendingLog.ToString();
            _pendingLog.Clear();
            _logFlushQueued = false;
        }

        var text = _log.Text + chunk;
        if (text.Length > MaxVisibleLogChars)
            text = text[^MaxVisibleLogChars..];

        _log.Text = text;
        _log.CaretIndex = _log.Text.Length;
    }

    private void SetStatus(string status)
    {
        Dispatcher.UIThread.Post(() => _status.Text = status);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, MinWidth = 110 };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text, MinWidth = 110 };
        button.Click += async (_, _) => await action();
        return button;
    }

    private static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private static Control PathRow(string label, TextBox box, Action browse)
    {
        box.MinWidth = 560;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { Label(label), box, Button("Browse", browse) }
        };
    }

    private static string? FindSteamCmd()
    {
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in pathDirs)
        {
            foreach (var name in new[] { "steamcmd", "steamcmd.sh" })
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, ".local", "share", "SteamCMD", "steamcmd.sh"),
            Path.Combine(home, "Steam", "steamcmd.sh"),
            "/usr/games/steamcmd",
            "/usr/bin/steamcmd",
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task<int> RunSteamCmdAsync(
        string steamCmd,
        string serverDir,
        string? steamUser,
        string? steamPassword,
        string? steamGuard,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = steamCmd,
            WorkingDirectory = Path.GetDirectoryName(steamCmd) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };

        Log("Running SteamCMD: " + steamCmd);
        Log("Install directory: " + serverDir);
        Log("Steam login: " + (string.IsNullOrWhiteSpace(steamUser) ? "anonymous" : steamUser));

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) Log(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) Log("[steamcmd] " + e.Data); };

        if (!process.Start())
            throw new InvalidOperationException("Could not start SteamCMD.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var loginCommand = BuildSteamLoginCommand(steamUser, steamPassword, steamGuard);
        await process.StandardInput.WriteLineAsync("force_install_dir " + SteamCmdQuote(serverDir));
        await process.StandardInput.WriteLineAsync(loginCommand);
        await process.StandardInput.WriteLineAsync("app_update 1020290 validate");
        await process.StandardInput.WriteLineAsync("quit");
        await process.StandardInput.FlushAsync(ct);

        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    private static string BuildSteamLoginCommand(string? steamUser, string? steamPassword, string? steamGuard)
    {
        if (string.IsNullOrWhiteSpace(steamUser) ||
            string.Equals(steamUser.Trim(), "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            return "login anonymous";
        }

        var command = new StringBuilder("login ");
        command.Append(SteamCmdQuote(steamUser.Trim()));
        if (!string.IsNullOrWhiteSpace(steamPassword))
        {
            command.Append(' ');
            command.Append(SteamCmdQuote(steamPassword));
        }

        if (!string.IsNullOrWhiteSpace(steamGuard))
        {
            command.Append(' ');
            command.Append(SteamCmdQuote(steamGuard.Trim()));
        }

        return command.ToString();
    }

    private static string SteamCmdQuote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string? ResolveServerExecutable(string serverDir)
    {
        var candidates = new[]
        {
            "run_bepinex.sh",
            "TABG-DS.x86_64",
            "TABG.x86_64",
            "TotallyAccurateBattlegroundsDedicatedServer.x86_64",
            "TABG-DS.exe",
            "TABG.exe",
        };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(serverDir, candidate);
            if (File.Exists(path))
                return path;
        }

        return Directory.Exists(serverDir)
            ? Directory.GetFiles(serverDir, "TABG*", SearchOption.TopDirectoryOnly).FirstOrDefault(File.Exists)
            : null;
    }

    private static string? ResolveClientLaunchTarget(string clientDir)
    {
        if (!OperatingSystem.IsWindows())
        {
            var nativeExecutable = ResolveClientExecutable(
                clientDir,
                "TotallyAccurateBattlegrounds.x86_64",
                "TABG.x86_64");
            if (nativeExecutable != null)
            {
                var script = Path.Combine(clientDir, "run_bepinex.sh");
                return File.Exists(script) ? script : nativeExecutable;
            }

            return ResolveClientExecutable(
                clientDir,
                "TotallyAccurateBattlegrounds.exe",
                "TABG.exe") ?? ResolveClientExecutableFallback(clientDir);
        }

        return ResolveClientExecutable(
            clientDir,
            "TotallyAccurateBattlegrounds.exe",
            "TABG.exe",
            "TotallyAccurateBattlegrounds.x86_64",
            "TABG.x86_64") ?? ResolveClientExecutableFallback(clientDir);
    }

    private static string? ResolveClientExecutable(string clientDir, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var path = Path.Combine(clientDir, candidate);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string? ResolveClientExecutableFallback(string clientDir)
    {
        return Directory.Exists(clientDir)
            ? Directory.GetFiles(clientDir, "*Battlegrounds*", SearchOption.TopDirectoryOnly).FirstOrDefault(File.Exists)
            : null;
    }

    private static ProcessStartInfo CreateClientStartInfo(string executablePath, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        psi.Environment["SteamAppId"] = "823130";
        psi.Environment["SteamGameId"] = "823130";

        if (!OperatingSystem.IsWindows() &&
            executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var proton = FindProtonExecutable();
            if (proton == null)
            {
                throw new FileNotFoundException("Steam Proton was not found. Install Proton Experimental in Steam first.");
            }

            var steamRoot = FindSteamRoot() ?? throw new DirectoryNotFoundException("Steam installation folder was not found.");
            psi.FileName = proton;
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add(executablePath);
            var compatData = Path.Combine(steamRoot, "steamapps", "compatdata", "823130");
            Directory.CreateDirectory(compatData);
            psi.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = steamRoot;
            psi.Environment["STEAM_COMPAT_DATA_PATH"] = compatData;
            psi.Environment["STEAM_COMPAT_APP_ID"] = "823130";
            psi.Environment["WINEDLLOVERRIDES"] = "winhttp=n,b";
            return psi;
        }

        if (!OperatingSystem.IsWindows() &&
            executablePath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "/usr/bin/env";
            psi.ArgumentList.Add("bash");
            psi.ArgumentList.Add(executablePath);
        }

        return psi;
    }

    private static string? FindSteamRoot()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".local", "share", "Steam"),
        };

        return candidates.FirstOrDefault(path =>
            Directory.Exists(Path.Combine(path, "steamapps")));
    }

    private static string? FindProtonExecutable()
    {
        var steamRoot = FindSteamRoot();
        if (steamRoot == null)
            return null;

        var common = Path.Combine(steamRoot, "steamapps", "common");
        var preferred = Path.Combine(common, "Proton - Experimental", "proton");
        if (File.Exists(preferred))
            return preferred;

        if (!Directory.Exists(common))
            return null;

        return Directory.GetDirectories(common, "Proton*")
            .Select(dir => Path.Combine(dir, "proton"))
            .Where(File.Exists)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static Control DockTop(Control control)
    {
        DockPanel.SetDock(control, Dock.Top);
        return control;
    }

    private static Control Put(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static string? FindFileNearApp(string relativePath)
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(current, relativePath);
            if (File.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(current)?.FullName;
            if (parent == null || parent == current) break;
            current = parent;
        }

        var cwdCandidate = Path.Combine(Environment.CurrentDirectory, relativePath);
        return File.Exists(cwdCandidate) ? cwdCandidate : null;
    }

    private static bool IsBundledClientPluginAvailable(PluginDefinition plugin)
    {
        return plugin.DllNames.Length == 0 ||
            plugin.DllNames.All(dll => FindFileNearApp(Path.Combine("client-plugins", dll)) != null);
    }

    private static IEnumerable<string> FindFilesNearApp(string relativeDir, string pattern)
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(current, relativeDir);
            if (Directory.Exists(candidate))
                return Directory.GetFiles(candidate, pattern);

            var parent = Directory.GetParent(current)?.FullName;
            if (parent == null || parent == current) break;
            current = parent;
        }

        var cwdCandidate = Path.Combine(Environment.CurrentDirectory, relativeDir);
        return Directory.Exists(cwdCandidate) ? Directory.GetFiles(cwdCandidate, pattern) : Array.Empty<string>();
    }

    private sealed class MarketplacePluginItem
    {
        public MarketplacePluginItem(PluginManifest manifest, string status)
        {
            Manifest = manifest;
            Status = status;
        }

        public PluginManifest Manifest { get; }

        public string Status { get; }

        public override string ToString()
        {
            return $"[{Manifest.Type}] [{Status}] {Manifest.Name} {Manifest.Version} - {Manifest.Description}";
        }
    }
}
