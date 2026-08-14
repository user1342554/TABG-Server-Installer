using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.App.Services;
using TabgInstaller.UI.PluginCatalog;
using TabgInstaller.UI.Services;

namespace TabgInstaller.App;

public sealed partial class MainWindow : Window
{
    private const int MaxVisibleLogChars = 120_000;

    private readonly TextBox _serverPath = new();
    private readonly TextBox _clientPath = new();
    private readonly TextBox _moddedClientPath = new();
    private readonly TextBox _steamUser = new() { Text = "anonymous", Width = 150 };
    private readonly TextBox _steamPassword = new() { Width = 150, PasswordChar = '*' };
    private readonly TextBox _steamGuard = new() { Width = 90, Watermark = "optional" };
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
    private readonly Dictionary<string, Control> _gameSettingEditors = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBlock _gameSettingsSaveState = new() { Text = "Geladener Stand", VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _spLoadoutMode = new() { Width = 150, ItemsSource = new[] { "Normal", "GunGame", "ReverseGunGame", "KeepInventory" } };
    private readonly ComboBox _spWinCondition = new() { Width = 150, ItemsSource = new[] { "Default", "KillsToWin", "Debug" } };
    private readonly TextBox _spKillsToWin = SmallBox("20");
    private readonly CheckBox _spForceKillAtStart = new() { Content = "Spieler beim Rundenstart zurücksetzen" };
    private readonly CheckBox _spDropItemsOnDeath = new() { Content = "Gegenstände beim Tod fallen lassen" };
    private readonly TextBox _spItemsGiven = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox _spLoadouts = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly CheckBox _spHealOnKill = new() { Content = "Heilung nach Kill" };
    private readonly TextBox _spHealOnKillAmount = SmallBox("50");
    private readonly CheckBox _spCanGoDown = new() { Content = "Niedergeschlagen möglich" };
    private readonly CheckBox _spCanLockOut = new() { Content = "Aussperren möglich" };
    private readonly TextBox _spPercentVotes = SmallBox("50");
    private readonly TextBox _spMinPlayers = SmallBox("2");
    private readonly TextBox _spTimeToStart = SmallBox("20");
    private readonly CheckBox _spSpellDropEnabled = new() { Content = "Zauber-Drops aktivieren" };
    private readonly TextBox _spMinSpellDelay = SmallBox("30");
    private readonly TextBox _spMaxSpellDelay = SmallBox("90");
    private readonly TextBox _spSpellOffset = SmallBox("0");
    private readonly TextBox _spPreMatchTimeout = SmallBox("15");
    private readonly TextBox _spPeriMatchTimeout = SmallBox("30");
    private readonly TextBox _ringSizes = new() { Width = 420 };
    private readonly TextBox _ringSpeeds = new() { Width = 420 };
    private readonly ComboBox _spawnLocations = new() { Width = 220 };
    private readonly TextBox _validSpawnPoints = SmallBox("6,");
    private readonly TextBox _customSpawnPoint = new() { Width = 260 };
    private readonly TextBox _matchSpawns = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly CheckBox _enableLootDrops = new() { Content = "MatchCore-Loot-Drops aktivieren" };
    private readonly CheckBox _attackerGrenadeEnabled = new() { Content = "Granate des Angreifers" };
    private readonly ComboBox _attackerGrenade = new() { Width = 240 };
    private readonly TextBox _attackerChance = SmallBox("0.2");
    private readonly CheckBox _corpseGrenadeEnabled = new() { Content = "Granate am besiegten Spieler" };
    private readonly ComboBox _corpseGrenade = new() { Width = 240 };
    private readonly TextBox _corpseChance = SmallBox("0.2");
    private readonly TextBox _lives = SmallBox("256");
    private readonly TextBox _streamingDistance = SmallBox("-1");
    private readonly TextBox _banList = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox _proxMaxRange = SmallBox("50");
    private readonly TextBox _proxMinRange = SmallBox("5");
    private readonly ComboBox _proxFalloff = new() { Width = 140, ItemsSource = new[] { "Linear", "Logarithmic" }, SelectedIndex = 0 };
    private readonly CheckBox _serverLoggerLogToConsole = new() { Content = "In BepInEx-Konsole protokollieren" };
    private readonly CheckBox _serverLoggerWriteCsv = new() { Content = "Spieleridentitäten als CSV" };
    private readonly CheckBox _serverLoggerWriteLegacy = new() { Content = "Alte ServerLogger.txt schreiben" };
    private readonly CheckBox _serverLoggerFallbackScan = new() { Content = "Ersatzweise Spielerliste scannen" };
    private readonly TextBox _serverLoggerInterval = SmallBox("2");
    private readonly TextBox _serverLoggerLogDirectory = new() { Width = 220, Text = "server-logs" };
    private readonly TextBox _serverLoggerCsvFile = new() { Width = 160, Text = "players.csv" };
    private readonly TextBox _serverLoggerLegacyFile = new() { Width = 180, Text = "ServerLogger.txt" };
    private readonly StackPanel _additionalServerPluginSettings = new() { Spacing = 8 };
    private readonly StackPanel _clientPluginSettings = new() { Spacing = 8 };
    private readonly TextBlock _clientPluginSettingsStatus = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly List<PluginSettingEditor> _pluginSettingEditors = new();
    private readonly ListBox _adminList = new();
    private readonly TextBox _adminName = new() { Width = 180, Watermark = "Anzeigename" };
    private readonly TextBox _adminEpic = new() { Width = 260, Watermark = "Epic ID" };
    private readonly TextBox _adminLevel = SmallBox("4");
    private readonly ListBox _userPresets = new();
    private readonly ListBox _builtInPresets = new();
    private readonly TextBox _presetName = new()
    {
        MinWidth = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Watermark = "Name des eigenen Profils",
    };
    private readonly ListBox _serverPluginList = new();
    private readonly StackPanel _serverPluginCatalogChecks = new() { Spacing = 4 };
    private readonly ListBox _clientPluginList = new();
    private readonly StackPanel _clientPluginCatalogChecks = new() { Spacing = 4 };
    private readonly ListBox _bundledServerPluginList = new();
    private readonly ListBox _bundledClientPluginList = new();
    private readonly TextBox _consoleCommand = new() { Width = 360, Watermark = "command text (logged; server stdin is unavailable)" };
    private readonly TextBox _consoleSearch = new() { Width = 220, Watermark = "search log" };
    private readonly TextBox _referenceSearch = new() { Width = 240, Watermark = "filter items" };
    private readonly TextBox _settingsSummary = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox _serverProfileName = new() { Watermark = "Anzeigename des Servers" };
    private readonly ServerPathProvider _serverPathProvider = new();
    private readonly ServerProcessService _serverProcess;
    private readonly ServerProfileStore _serverProfiles;
    private readonly IStoragePickerService _storagePicker;
    private readonly IConfirmationDialogService _confirmations;
    private readonly IUiDispatcher _dispatcher;
    private readonly IExternalLauncher _externalLauncher;
    private readonly ISteamPathDetector _steamPathDetector;
    private readonly INotificationService _notifications;
    private readonly string _logPath;
    private readonly object _logBufferLock = new();
    private readonly StringBuilder _pendingLog = new();
    private Process? _clientProcess;
    private CancellationTokenSource? _installCts;
    private bool _logFlushQueued;
    private bool _loadingGameSettings;
    private string _loadedGameSettingsFingerprint = string.Empty;

    public MainWindow()
        : this(
            null,
            null,
            new AvaloniaUiDispatcher(),
            new ExternalProcessLauncher(),
            new InstallerSteamPathDetector(),
            new LogNotificationService())
    {
    }

    internal MainWindow(
        IStoragePickerService? storagePicker,
        IConfirmationDialogService? confirmations,
        IUiDispatcher dispatcher,
        IExternalLauncher externalLauncher,
        ISteamPathDetector steamPathDetector,
        INotificationService notifications)
    {
        _storagePicker = storagePicker ?? new AvaloniaStoragePickerService(this);
        _confirmations = confirmations ?? new AvaloniaConfirmationDialogService(this);
        _dispatcher = dispatcher;
        _externalLauncher = externalLauncher;
        _steamPathDetector = steamPathDetector;
        _notifications = notifications;
        if (_notifications is LogNotificationService logNotifications)
        {
            logNotifications.Message += Log;
        }

        _serverProcess = new ServerProcessService(_serverPathProvider);
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabgInstaller");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, "tabg-installer-app.log");
        _serverProfiles = new ServerProfileStore(logDir);

        Title = "TABG Server Installer";
        Width = 1180;
        Height = 760;
        MinWidth = 900;
        MinHeight = 620;
        Opened += (_, _) => Log("Main window opened.");
        Closing += (_, _) => Log("Main window closing.");
        Closed += (_, _) => Log("Main window closed.");

        LoadPluginRegistry();
        BuildUi();
        WireServerProcess();
        if (!LoadActiveServerProfile())
        {
            TryAutoDetectPaths();
            RegisterCurrentServerProfileIfReady();
        }
        else
        {
            DetectClientPath();
        }
        ShowInitialFunctionalPage();
        RefreshFunctionalUi();
        Log("Avalonia app started. Log file: " + _logPath);
    }

    private void BuildUi()
    {
        BuildFunctionalUi();
    }

    private Control BuildSetupTab()
    {
        return new TabControl
        {
            Margin = new Avalonia.Thickness(6),
            Items =
            {
                new TabItem { Header = "Server install", Content = BuildInstallTab() },
                new TabItem { Header = "Client install", Content = BuildClientTab() }
            }
        };
    }

    private Control BuildServerWorkspaceTab()
    {
        return new TabControl
        {
            Margin = new Avalonia.Thickness(6),
            Items =
            {
                new TabItem { Header = "Run", Content = BuildServerTab() },
                new TabItem { Header = "Console", Content = BuildConsoleTab() },
                new TabItem { Header = "Backups", Content = BuildBackupsTab() }
            }
        };
    }

    private Control BuildModsTab()
    {
        return new TabControl
        {
            Margin = new Avalonia.Thickness(6),
            Items =
            {
                new TabItem { Header = "Server DLLs", Content = BuildServerModsTab() },
                new TabItem { Header = "Client DLLs", Content = BuildClientModsTab() }
            }
        };
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
                Button("Create folder", CreateServerFolder),
                Button("SteamCMD install/update", InstallOrUpdateDedicatedServerAsync),
                Button("Detect server", DetectServerPath)
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
        panel.Children.Add(new TextBlock { Text = "This prepares BepInEx and bundled core server files." });
        panel.Children.Add(_progress);
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Button("Prepare / repair server", InstallServerAsync),
                Button("Cancel install", () => _installCts?.Cancel()),
                Button("Reload plugin list", () => { LoadPluginRegistry(); RebuildPluginChecks(); RebuildClientModChecks(); })
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
        return new TabControl
        {
            Margin = new Avalonia.Thickness(6),
            TabStripPlacement = Dock.Left,
            Items =
            {
                ConfigTab("Spielprofil & Presets", BuildPresetsTab()),
                ConfigTab("Server & Spieler", BuildGameSettingsTab()),
                ConfigTab("Runde, Sieg & Loadouts", BuildMatchSettingsTab()),
                ConfigTab("Welt: Ring & Spawns", BuildRingSpawnsTab()),
                ConfigTab("Mod-Einstellungen", BuildModSettingsTab()),
                ConfigTab("Admins", BuildAdminsTab()),
                ConfigTab("Erweiterte Datei", BuildRawConfigTab()),
            }
        };
    }

    private static TabItem ConfigTab(string title, Control content)
        => new()
        {
            Header = title,
            Content = content,
            FontSize = 15,
        };

    private Control BuildRawConfigTab()
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

    private Control BuildConsoleTab()
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
                        Button("Start server", () => StartServer("-batchmode -nographics -nolog")),
                        Button("Stop server", () => _serverProcess.Stop()),
                        Button("Quick restart", QuickRestartServer),
                        Button("Clear log", () => _log.Text = ""),
                        Button("Export log", ExportVisibleLog)
                    }
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        Label("Search"),
                        _consoleSearch,
                        Button("Find", FindInVisibleLog),
                        Label("Command"),
                        _consoleCommand,
                        Button("Send", SendConsoleCommand)
                    }
                },
                new TextBlock { Text = "The live log is shown at the bottom. TABG dedicated server does not expose a reliable stdin command channel here, so commands are recorded for operator notes." }
            }
        };
    }

    private Control BuildServerModsTab()
    {
        RefreshServerModLists();
        var advanced = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Children =
            {
                Put(new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Installierte Serverdateien", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                        new ScrollViewer { Content = _serverPluginList, Height = 120 },
                        new WrapPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                FlowButton("Aktualisieren", RefreshServerModLists),
                                FlowButton("Aktivieren / deaktivieren", ToggleSelectedServerPlugin),
                                FlowButton("Entfernen", RemoveSelectedServerPlugin),
                                FlowButton("Externe DLL hinzufügen", AddServerPluginAsync),
                                FlowButton("Ordner öffnen", () => OpenPath(ServerPluginDir()))
                            }
                        }
                    }
                }, 0),
                Put(new StackPanel
                {
                    Spacing = 8,
                    Margin = new Avalonia.Thickness(10, 0, 0, 0),
                    Children =
                    {
                        new TextBlock { Text = "Mitgelieferte Rohdateien", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                        new ScrollViewer { Content = _bundledServerPluginList, Height = 120 },
                        new WrapPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children = { FlowButton("Ausgewählte DLL installieren", InstallBundledServerPlugin) }
                        }
                    }
                }, 1)
            }
        };

        return ToolPanel(new StackPanel
        {
            Margin = new Avalonia.Thickness(10),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Server-Erweiterungen", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                new TextBlock
                {
                    Text = "Aktiviere Funktionen. Installation, Deaktivierung und fehlende Dateien werden pro Erweiterung angezeigt.",
                    Foreground = Brush.Parse("#9AA8B7"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new ScrollViewer { Content = _serverPluginCatalogChecks, Height = 420 },
                new Expander
                {
                    Header = "Erweiterte DLL-Verwaltung",
                    IsExpanded = false,
                    Content = advanced,
                },
            }
        });
    }

    private Control BuildGameSettingsTab()
    {
        _gameSettingEditors.Clear();
        var rows = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        var warningBlocks = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
        var list = new StackPanel { Spacing = 6 };

        var props = typeof(GameSettingsData).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var property in props)
        {
            var editor = CreateGameSettingEditor(property);
            _gameSettingEditors[property.Name] = editor;

            KnowledgeIndex.Current.GameSettings.TryGetValue(property.Name, out var knowledge);
            var description = knowledge?.Description ?? GameSettingFallbackDescription(property.Name);
            var warning = new TextBlock
            {
                Foreground = Brush.Parse("#F3C654"),
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                IsVisible = false,
            };
            warningBlocks[property.Name] = warning;

            var labelPanel = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = GameSettingDisplayName(property.Name),
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = description,
                        Foreground = Brush.Parse("#9AA8B7"),
                        FontSize = 12,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                    warning,
                },
            };
            ToolTip.SetTip(editor, $"{property.Name}: {description}");

            var row = new Border
            {
                BorderBrush = Brush.Parse("#303A47"),
                BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
                Padding = new Avalonia.Thickness(10, 9),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children = { labelPanel, editor },
                },
            };
            rows[property.Name] = row;
            list.Children.Add(row);

            switch (editor)
            {
                case TextBox textBox:
                    textBox.TextChanged += (_, _) =>
                    {
                        MarkGameSettingsDirty();
                        ValidateGameSettingsEditors(warningBlocks);
                    };
                    break;
                case CheckBox checkBox:
                    checkBox.IsCheckedChanged += (_, _) =>
                    {
                        MarkGameSettingsDirty();
                        ValidateGameSettingsEditors(warningBlocks);
                    };
                    break;
                case ComboBox comboBox:
                    comboBox.SelectionChanged += (_, _) =>
                    {
                        MarkGameSettingsDirty();
                        ValidateGameSettingsEditors(warningBlocks);
                    };
                    break;
            }
        }

        var search = new TextBox { Watermark = "Einstellungen suchen…", Width = 260 };
        search.TextChanged += (_, _) =>
        {
            var query = search.Text?.Trim() ?? string.Empty;
            foreach (var property in props)
            {
                KnowledgeIndex.Current.GameSettings.TryGetValue(property.Name, out var knowledge);
                rows[property.Name].IsVisible = string.IsNullOrEmpty(query)
                    || property.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || GameSettingDisplayName(property.Name).Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (knowledge?.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
            }
        };

        LoadGameSettingsTyped();
        ValidateGameSettingsEditors(warningBlocks);
        var save = Button("Speichern", SaveGameSettingsTyped);
        save.Classes.Add("accent");
        return new DockPanel
        {
            Children =
            {
                DockTop(new WrapPanel
                {
                    Margin = new Avalonia.Thickness(0, 0, 0, 8),
                    Children =
                    {
                        save,
                        Button("Neu laden", LoadGameSettingsTyped),
                        Button("Dateiordner öffnen", () => OpenPath(GameSettingsPath())),
                        search,
                        _gameSettingsSaveState,
                    }
                }),
                new ScrollViewer { Content = list }
            }
        };
    }

    private Control BuildMatchSettingsTab()
    {
        LoadStarterPackSettings();
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            Label("Siegbedingung"), _spWinCondition,
                            Label("Loadout-Modus"), _spLoadoutMode,
                            Label("Kills zum Sieg"), _spKillsToWin,
                            _spForceKillAtStart,
                            _spDropItemsOnDeath,
                            _spHealOnKill,
                            Label("Heilung %"), _spHealOnKillAmount,
                            _spCanGoDown,
                            _spCanLockOut
                        }
                    },
                    new WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            Label("Benötigte Stimmen %"), _spPercentVotes,
                            Label("Mindestspieler"), _spMinPlayers,
                            Label("Startwartezeit"), _spTimeToStart,
                            _spSpellDropEnabled,
                            Label("Zauber frühestens"), _spMinSpellDelay,
                            Label("Zauber spätestens"), _spMaxSpellDelay,
                            Label("Zauber-Versatz"), _spSpellOffset
                        }
                    },
                    new WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            Label("Vorbereitungs-Timeout"), _spPreMatchTimeout,
                            Label("Runden-Timeout"), _spPeriMatchTimeout,
                            Button("Neu laden", LoadStarterPackSettings),
                            Button("Speichern", SaveStarterPackSettings),
                            Button("Dateiordner öffnen", () => OpenPath(StarterPackConfigService.GetPath(_serverPath.Text ?? "")))
                        }
                    },
                    new TextBlock { Text = "Startgegenstände (erweiterte Syntax)" },
                    new ScrollViewer { Content = _spItemsGiven, Height = 90 },
                    new TextBlock { Text = "Loadouts (erweiterte Syntax)" },
                    new ScrollViewer { Content = _spLoadouts, Height = 210 }
                }
            }
        };
    }

    private Control BuildRingSpawnsTab()
    {
        _spawnLocations.ItemsSource = ItemDatabase.SpawnLocations;
        LoadRingSpawnSettings();
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            Label("Ort"), _spawnLocations,
                            Button("Ort übernehmen", ApplySelectedSpawnLocation),
                            Button("Standard BR", ApplyStandardRingPreset),
                            Button("Deathmatch ohne Ring", ApplyDeathmatchRingPreset)
                        }
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { Label("Ringgrößen"), _ringSizes, Label("Ringgeschwindigkeiten"), _ringSpeeds }
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { Label("Gültige Spawnpunkte"), _validSpawnPoints, Label("Eigener Lobby-Spawn"), _customSpawnPoint }
                    },
                    new TextBlock { Text = "Runden-Spawnpunkte (x,z;x,z;…)" },
                    new ScrollViewer { Content = _matchSpawns, Height = 210 },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            Button("Neu laden", LoadRingSpawnSettings),
                            Button("Speichern", SaveRingSpawnSettings),
                            Button("MatchCore-Datei öffnen", () => OpenPath(Path.Combine(_serverPath.Text ?? "", "BepInEx", "config", "TabgInstaller.MatchCore.cfg")))
                        }
                    }
                }
            }
        };
    }

    private Control BuildModSettingsTab()
    {
        var grenades = ItemDatabase.ByCategory("Grenades").OrderBy(g => g.Name).Select(g => $"{g.Name} ({g.Id})").ToList();
        _attackerGrenade.ItemsSource = grenades;
        _corpseGrenade.ItemsSource = grenades;
        LoadModSettings();
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "MatchCore und mitgelieferte Erweiterungen", FontWeight = FontWeight.SemiBold },
                    _enableLootDrops,
                    new WrapPanel { Orientation = Orientation.Horizontal, Children = { _attackerGrenadeEnabled, _attackerGrenade, Label("Wahrscheinlichkeit"), _attackerChance } },
                    new WrapPanel { Orientation = Orientation.Horizontal, Children = { _corpseGrenadeEnabled, _corpseGrenade, Label("Wahrscheinlichkeit"), _corpseChance } },
                    new WrapPanel { Orientation = Orientation.Horizontal, Children = { Label("Leben"), _lives, Label("Streaming-Distanz"), _streamingDistance } },
                    new TextBlock { Text = "Sperrliste (eine Epic-ID pro Zeile)" },
                    new ScrollViewer { Content = _banList, Height = 90 },
                    new TextBlock { Text = "Nähe-Sprachchat", FontWeight = FontWeight.SemiBold },
                    new WrapPanel { Orientation = Orientation.Horizontal, Children = { Label("Maximale Reichweite"), _proxMaxRange, Label("Minimale Reichweite"), _proxMinRange, Label("Abfallkurve"), _proxFalloff } },
                    new TextBlock { Text = "Server-Protokollierung", FontWeight = FontWeight.SemiBold },
                    new WrapPanel { Orientation = Orientation.Horizontal, Children = { _serverLoggerLogToConsole, _serverLoggerWriteCsv, _serverLoggerWriteLegacy, _serverLoggerFallbackScan } },
                    new WrapPanel { Orientation = Orientation.Horizontal, Children = { Label("Prüfintervall"), _serverLoggerInterval, Label("Protokollordner"), _serverLoggerLogDirectory, Label("CSV-Datei"), _serverLoggerCsvFile, Label("Alte Datei"), _serverLoggerLegacyFile } },
                    new WrapPanel
                    {
                        Children =
                        {
                            Button("ServerLogger-Datei öffnen", () => OpenPath(ModConfigService.ServerLoggerConfigPath(_serverPath.Text ?? ""))),
                            Button("CSV-Protokoll öffnen", () => OpenPath(ModConfigService.GetServerLoggerCsvPath(_serverPath.Text ?? "", BuildServerLoggerSettingsFromFields()))),
                            Button("Altes Protokoll öffnen", () => OpenPath(ModConfigService.GetServerLoggerLegacyPath(_serverPath.Text ?? "", BuildServerLoggerSettingsFromFields())))
                        }
                    },
                    new TextBlock { Text = "Weitere Server-Erweiterungen", FontWeight = FontWeight.SemiBold },
                    _additionalServerPluginSettings,
                    new TextBlock { Text = "Client-Erweiterungen", FontWeight = FontWeight.SemiBold },
                    _clientPluginSettingsStatus,
                    _clientPluginSettings,
                    new WrapPanel { Children = { Button("Neu laden", LoadModSettings), Button("Speichern", SaveModSettings), Button("Konfigurationsordner öffnen", () => OpenPath(Path.Combine(_serverPath.Text ?? "", "BepInEx", "config"))) } }
                }
            }
        };
    }

    private Control BuildAdminsTab()
    {
        LoadAdmins();
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new ScrollViewer { Content = _adminList, Height = 300 },
                new WrapPanel { Children = { _adminName, _adminEpic, Label("Berechtigungsstufe"), _adminLevel } },
                new WrapPanel { Children = { Button("Neu laden", LoadAdmins), Button("Hinzufügen / ändern", AddOrUpdateAdmin), Button("Entfernen", RemoveSelectedAdmin), Button("Speichern", SaveAdmins), Button("PlayerPerms.json öffnen", () => OpenPath(PlayerPermsPath())) } }
            }
        };
    }

    private Control BuildPresetsTab()
    {
        RefreshPresetLists();
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                MaxWidth = 720,
                HorizontalAlignment = HorizontalAlignment.Left,
                Spacing = 22,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Spielprofil auswählen", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                            new TextBlock { Text = "Ein Profil setzt zusammengehörige Spiel-, Match- und Mod-Werte in einem Schritt.", Foreground = Brush.Parse("#9AA8B7"), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            new ScrollViewer { Content = _builtInPresets, Height = 220 },
                            Button("Ausgewähltes Profil anwenden", ApplyBuiltInPreset),
                        },
                    },
                    new Separator(),
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Eigene Profile", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                            new TextBlock { Text = "Speichere den aktuellen Stand oder stelle ein früheres Profil wieder her.", Foreground = Brush.Parse("#9AA8B7"), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            new ScrollViewer { Content = _userPresets, Height = 150 },
                            _presetName,
                            new WrapPanel { Children = { Button("Aktualisieren", RefreshPresetLists), Button("Speichern", SaveUserPreset), Button("Laden", LoadUserPreset), Button("Löschen", DeleteUserPreset) } },
                        },
                    },
                },
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
                new WrapPanel
                {
                    Children =
                    {
                        Button("Neue Sicherung", CreateBackupAsync),
                        Button("Aktualisieren", RefreshBackups),
                        Button("Auswahl wiederherstellen", RestoreBackupAsync),
                        Button("Auswahl löschen", DeleteBackup)
                    }
                },
                new TextBlock
                {
                    Text = "Vor einer Wiederherstellung wird automatisch eine zusätzliche Sicherheitskopie des aktuellen Stands angelegt.",
                    Foreground = Brush.Parse("#9AA8B7"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                _backups
            }
        };
    }

    private Control BuildClientTab()
    {
        RebuildClientModChecks();

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Margin = new Avalonia.Thickness(6),
                Children =
                {
                    PathRow("TABG Steam folder", _clientPath, BrowseClientAsync),
                    PathRow("Modded copy folder", _moddedClientPath, BrowseModdedClientAsync),
                    Button("Detect TABG client", DetectClientPath),
                    new TextBlock { Text = "This prepares a modded TABG copy with bundled client mods." },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            Button("Prepare / update client", InstallClientModsAsync),
                            Button("Start modded client", StartModdedClient),
                            Button("Open modded folder", () => OpenPath(_moddedClientPath.Text))
                        }
                    }
                }
            }
        };
    }

    private Control BuildClientModsTab()
    {
        RefreshClientModLists();
        var advanced = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Children =
            {
                Put(new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Installierte Clientdateien", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                        new ScrollViewer { Content = _clientPluginList, Height = 120 },
                        new WrapPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                FlowButton("Aktualisieren", RefreshClientModLists),
                                FlowButton("Aktivieren / deaktivieren", ToggleSelectedClientPlugin),
                                FlowButton("Entfernen", RemoveSelectedClientPlugin),
                                FlowButton("Externe DLL hinzufügen", AddClientPluginAsync),
                                FlowButton("Ordner öffnen", () => OpenPath(ClientPluginDir()))
                            }
                        }
                    }
                }, 0),
                Put(new StackPanel
                {
                    Spacing = 8,
                    Margin = new Avalonia.Thickness(10, 0, 0, 0),
                    Children =
                    {
                        new TextBlock { Text = "Mitgelieferte Rohdateien", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                        new ScrollViewer { Content = _bundledClientPluginList, Height = 120 },
                        new WrapPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children = { FlowButton("Ausgewählte DLL installieren", InstallBundledClientPlugin) }
                        }
                    }
                }, 1)
            }
        };

        return ToolPanel(new StackPanel
        {
            Margin = new Avalonia.Thickness(10),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Client-Erweiterungen", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                new TextBlock
                {
                    Text = "Diese Erweiterungen gelten nur für die getrennte modifizierte TABG-Kopie.",
                    Foreground = Brush.Parse("#9AA8B7"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new ScrollViewer { Content = _clientPluginCatalogChecks, Height = 420 },
                new Expander
                {
                    Header = "Erweiterte DLL-Verwaltung",
                    IsExpanded = false,
                    Content = advanced,
                },
            }
        });
    }

    private Control BuildReferenceTab()
    {
        var list = new ListBox();
        foreach (var file in FindFilesNearApp("Knowledge", "*.json"))
            list.Items.Add(file);

        var viewer = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        _referenceSearch.TextChanged += (_, _) => viewer.Text = BuildItemsReference(_referenceSearch.Text);
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is string path && File.Exists(path))
                viewer.Text = File.ReadAllText(path);
        };

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
                        Button("Commands", () => viewer.Text = BuildCommandsReference()),
                        Button("Items", () => viewer.Text = BuildItemsReference(_referenceSearch.Text)),
                        Button("Spawns", () => viewer.Text = BuildSpawnsReference()),
                        Button("Loadout syntax", () => viewer.Text = BuildLoadoutReference()),
                        _referenceSearch
                    }
                }),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("300,*"),
                    Children =
                    {
                        Put(list, 0),
                        Put(viewer, 1)
                    }
                }
            }
        };
    }

    private Control BuildSettingsTab()
    {
        RefreshSettingsSummary();
        var saveProfileName = Button("Servernamen speichern", SaveActiveServerProfileName);
        var removeProfile = Button("Server aus Liste entfernen", async () => await RemoveActiveServerProfileAsync());
        return new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(6),
            Children =
            {
                new TextBlock { Text = "Aktiver Server", FontSize = 18, FontWeight = FontWeight.SemiBold },
                new TextBlock
                {
                    Text = "Der Anzeigename ändert nur die Auswahl in dieser App. Serverdateien werden dabei weder verschoben noch gelöscht.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Foreground = Avalonia.Media.Brushes.Gray,
                },
                _serverProfileName,
                new WrapPanel { Children = { saveProfileName, removeProfile } },
                new Separator { Margin = new Avalonia.Thickness(0, 8) },
                _settingsSummary,
                Button("Protokollansicht leeren", () => _log.Text = ""),
                Button("Protokolldatei öffnen", () => OpenPath(_logPath)),
                Button("Anwendungsordner öffnen", () => OpenPath(AppContext.BaseDirectory)),
                Button("Status aktualisieren", RefreshSettingsSummary),
                Button("Pfade neu erkennen", HardResetDetectedPaths)
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
        RefreshServerModLists();
        Log("Server folder ready: " + serverDir);
    }

    private async void InstallOrUpdateDedicatedServerAsync()
        => await InstallOrUpdateDedicatedServerCoreAsync();

    private async Task<bool> InstallOrUpdateDedicatedServerCoreAsync()
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
            SetStatus("SteamCMD fehlt");
            return false;
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
            return code == 0;
        }
        catch (OperationCanceledException)
        {
            Log("SteamCMD install/update cancelled.");
            SetStatus("Cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Log("SteamCMD install/update failed: " + ex.Message);
            SetStatus("SteamCMD failed");
            return false;
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
        => await InstallServerCoreAsync();

    private async Task<bool> InstallServerCoreAsync()
    {
        var serverDir = _serverPath.Text?.Trim() ?? "";
        if (!Directory.Exists(serverDir))
        {
            Log("Select a valid server folder first.");
            SetStatus("Serverordner fehlt");
            return false;
        }

        var serverExe = ResolveServerExecutable(serverDir);
        if (serverExe == null)
        {
            Log("No TABG dedicated server executable found. SteamCMD must complete successfully before Start can work.");
            SetStatus("Serverdateien fehlen");
            return false;
        }

        _installCts = new CancellationTokenSource();
        _progress.Value = 0;

        var bundled = new List<string>();
        var skipStarter = true;
        var community = false;
        foreach (var cb in _pluginChecks.Children.OfType<CheckBox>())
        {
            if (cb.IsChecked != true || cb.Tag is not PluginDefinition plugin)
                continue;

            if (plugin.Kind == PluginKind.CommunityServer)
            {
                community = true;
                continue;
            }

            if (plugin.Kind != PluginKind.Bundled)
                continue;

            foreach (var dll in plugin.DllNames)
                if (!bundled.Contains(dll, StringComparer.OrdinalIgnoreCase))
                    bundled.Add(dll);
        }

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
                "",
                skipStarter,
                false,
                community,
                bundled,
                _installCts.Token);

            _progress.Value = code == 0 ? 100 : _progress.Value;
            Log(code == 0 ? "Install finished." : $"Install exited with code {code}.");
            SetStatus(code == 0 ? "Install finished" : "Install failed");
            RefreshServerModLists();
            return code == 0;
        }
        catch (OperationCanceledException)
        {
            Log("Install cancelled.");
            SetStatus("Cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Log("Install failed: " + ex.Message);
            SetStatus("Install failed");
            return false;
        }
        finally
        {
            _installCts?.Dispose();
            _installCts = null;
        }
    }

    private void StartServer(string args)
        => TryStartServer(args);

    private bool TryStartServer(string args)
    {
        try
        {
            _serverPathProvider.SetPath(_serverPath.Text ?? "");
            var exe = ResolveServerExecutable(_serverPath.Text ?? "");
            if (exe == null)
            {
                Log("No server executable found. SteamCMD did not install the dedicated server yet.");
                Log("If SteamCMD says No subscription, install TABG Dedicated Server from Steam or use an account with access.");
                return false;
            }

            Log("Starting server executable: " + exe);
            if (!_serverProcess.Start(args))
            {
                Log("Server is already running.");
                RefreshFunctionalUi();
                return _serverProcess.IsRunning;
            }
            else
                SetStatus("Server running");
            RefreshFunctionalUi();
            return true;
        }
        catch (Exception ex)
        {
            Log("Could not start server: " + ex.Message);
            SetStatus("Server start failed");
            RefreshFunctionalUi();
            return false;
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

    private string GameSettingsPath() => Path.Combine(_serverPath.Text ?? "", "game_settings.txt");
    private string PlayerPermsPath() => Path.Combine(_serverPath.Text ?? "", "BepInEx", "config", "CitrusLib", "PlayerPerms.json");

    private void LoadGameSettingsTyped()
    {
        _loadingGameSettings = true;
        try
        {
            var file = GameSettingsPath();
            var settings = File.Exists(file) ? ConfigIO.ReadGameSettings(file) : new GameSettingsData();
            foreach (var prop in typeof(GameSettingsData).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!_gameSettingEditors.TryGetValue(prop.Name, out var editor)) continue;
                SetEditorValue(editor, prop.GetValue(settings));
            }
            _loadedGameSettingsFingerprint = BuildGameSettingsFingerprint();
            _gameSettingsSaveState.Text = "Geladener Stand · keine offenen Änderungen";
            _gameSettingsSaveState.Foreground = Brush.Parse("#9AA8B7");
            Log(File.Exists(file) ? "Loaded typed game_settings.txt." : "Using default game settings.");
        }
        catch (Exception ex)
        {
            Log("Could not load typed game settings: " + ex.Message);
        }
        finally
        {
            _loadingGameSettings = false;
        }
    }

    private void SaveGameSettingsTyped()
    {
        var dir = _serverPath.Text?.Trim() ?? "";
        if (!Directory.Exists(dir))
        {
            Log("Select a valid server folder first.");
            return;
        }

        try
        {
            var file = GameSettingsPath();
            var settings = File.Exists(file) ? ConfigIO.ReadGameSettings(file) : new GameSettingsData();
            foreach (var prop in typeof(GameSettingsData).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!_gameSettingEditors.TryGetValue(prop.Name, out var editor)) continue;
                prop.SetValue(settings, ConvertEditorValue(editor, prop.PropertyType));
            }
            ConfigIO.WriteGameSettings(settings, file);
            LoadConfig();
            _loadedGameSettingsFingerprint = BuildGameSettingsFingerprint();
            _gameSettingsSaveState.Text = $"✓ Gespeichert · {DateTime.Now:HH:mm}";
            _gameSettingsSaveState.Foreground = Brush.Parse("#45CF8A");
            Log("Saved typed game_settings.txt.");
        }
        catch (Exception ex)
        {
            Log("Could not save typed game settings: " + ex.Message);
            _gameSettingsSaveState.Text = "Speichern fehlgeschlagen · Details unter Diagnose";
            _gameSettingsSaveState.Foreground = Brush.Parse("#FF777F");
        }
    }

    private void MarkGameSettingsDirty()
    {
        if (_loadingGameSettings)
            return;

        if (string.Equals(BuildGameSettingsFingerprint(), _loadedGameSettingsFingerprint, StringComparison.Ordinal))
        {
            _gameSettingsSaveState.Text = "Geladener Stand · keine offenen Änderungen";
            _gameSettingsSaveState.Foreground = Brush.Parse("#9AA8B7");
        }
        else
        {
            _gameSettingsSaveState.Text = "● Ungespeicherte Änderungen";
            _gameSettingsSaveState.Foreground = Brush.Parse("#F3C654");
        }
    }

    private string BuildGameSettingsFingerprint()
    {
        var parts = new List<string>();
        foreach (var property in typeof(GameSettingsData).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!_gameSettingEditors.TryGetValue(property.Name, out var editor))
                continue;

            var value = editor switch
            {
                CheckBox checkBox => checkBox.IsChecked == true ? "true" : "false",
                ComboBox comboBox => comboBox.SelectedItem?.ToString() ?? string.Empty,
                TextBox textBox => textBox.Text ?? string.Empty,
                _ => string.Empty,
            };
            parts.Add(property.Name + "=" + value);
        }

        return string.Join("\n", parts);
    }

    private void LoadStarterPackSettings()
    {
        try
        {
            var s = StarterPackConfigService.Read(_serverPath.Text ?? "");
            SelectComboText(_spWinCondition, s.WinCondition);
            SelectComboText(_spLoadoutMode, s.GetLoadoutMode());
            _spKillsToWin.Text = s.KillsToWin?.ToString(CultureInfo.InvariantCulture) ?? "";
            _spForceKillAtStart.IsChecked = s.ForceKillAtStart;
            _spDropItemsOnDeath.IsChecked = s.DropItemsOnDeath;
            _spItemsGiven.Text = s.ItemsGiven;
            _spLoadouts.Text = s.GetLoadoutsWithoutPrefix();
            _spHealOnKill.IsChecked = s.HealOnKill;
            _spHealOnKillAmount.Text = s.HealOnKillAmount.ToString(CultureInfo.InvariantCulture);
            _spCanGoDown.IsChecked = s.CanGoDown;
            _spCanLockOut.IsChecked = s.CanLockOut;
            _spPercentVotes.Text = s.PercentOfVotes.ToString(CultureInfo.InvariantCulture);
            _spMinPlayers.Text = s.MinNumberOfPlayers.ToString(CultureInfo.InvariantCulture);
            _spTimeToStart.Text = s.TimeToStart.ToString(CultureInfo.InvariantCulture);
            _spSpellDropEnabled.IsChecked = s.SpelldropEnabled;
            _spMinSpellDelay.Text = s.MinSpellDropDelay.ToString(CultureInfo.InvariantCulture);
            _spMaxSpellDelay.Text = s.MaxSpellDropDelay.ToString(CultureInfo.InvariantCulture);
            _spSpellOffset.Text = s.SpellDropOffset.ToString(CultureInfo.InvariantCulture);
            _spPreMatchTimeout.Text = s.PreMatchTimeout.ToString(CultureInfo.InvariantCulture);
            _spPeriMatchTimeout.Text = s.PeriMatchTimeout.ToString(CultureInfo.InvariantCulture);
            Log("Loaded TheStarterPack.txt settings.");
        }
        catch (Exception ex)
        {
            Log("Could not load StarterPack settings: " + ex.Message);
        }
    }

    private void SaveStarterPackSettings()
    {
        var dir = _serverPath.Text?.Trim() ?? "";
        if (!Directory.Exists(dir))
        {
            Log("Select a valid server folder first.");
            return;
        }

        try
        {
            var s = StarterPackConfigService.Read(dir);
            s.WinCondition = _spWinCondition.SelectedItem?.ToString() ?? "Default";
            s.KillsToWin = ParseNullableInt(_spKillsToWin.Text);
            s.ForceKillAtStart = _spForceKillAtStart.IsChecked == true;
            s.DropItemsOnDeath = _spDropItemsOnDeath.IsChecked == true;
            s.ItemsGiven = _spItemsGiven.Text ?? "";
            s.SetLoadoutsWithPrefix(_spLoadoutMode.SelectedItem?.ToString() ?? "Normal", _spLoadouts.Text ?? "");
            s.HealOnKill = _spHealOnKill.IsChecked == true;
            s.HealOnKillAmount = ParseFloat(_spHealOnKillAmount.Text, s.HealOnKillAmount);
            s.CanGoDown = _spCanGoDown.IsChecked == true;
            s.CanLockOut = _spCanLockOut.IsChecked == true;
            s.PercentOfVotes = ParseInt(_spPercentVotes.Text, s.PercentOfVotes);
            s.MinNumberOfPlayers = ParseInt(_spMinPlayers.Text, s.MinNumberOfPlayers);
            s.TimeToStart = ParseInt(_spTimeToStart.Text, s.TimeToStart);
            s.SpelldropEnabled = _spSpellDropEnabled.IsChecked == true;
            s.MinSpellDropDelay = ParseInt(_spMinSpellDelay.Text, s.MinSpellDropDelay);
            s.MaxSpellDropDelay = ParseInt(_spMaxSpellDelay.Text, s.MaxSpellDropDelay);
            s.SpellDropOffset = ParseInt(_spSpellOffset.Text, s.SpellDropOffset);
            s.PreMatchTimeout = ParseFloat(_spPreMatchTimeout.Text, s.PreMatchTimeout);
            s.PeriMatchTimeout = ParseFloat(_spPeriMatchTimeout.Text, s.PeriMatchTimeout);
            StarterPackConfigService.Write(dir, s);
            Log("Saved TheStarterPack.txt settings.");
        }
        catch (Exception ex)
        {
            Log("Could not save StarterPack settings: " + ex.Message);
        }
    }

    private void LoadRingSpawnSettings()
    {
        try
        {
            var gsPath = GameSettingsPath();
            var gs = File.Exists(gsPath) ? ConfigIO.ReadGameSettings(gsPath) : new GameSettingsData();
            var starter = StarterPackConfigService.Read(_serverPath.Text ?? "");
            _ringSizes.Text = gs.RingSizes;
            _ringSpeeds.Text = gs.RingSpeeds;
            _validSpawnPoints.Text = starter.ValidSpawnPoints;
            _customSpawnPoint.Text = starter.CustomSpawnPoint;
            _matchSpawns.Text = ModConfigService.ReadSpawnPoints(_serverPath.Text ?? "");
            Log("Loaded ring and spawn settings.");
        }
        catch (Exception ex)
        {
            Log("Could not load ring/spawn settings: " + ex.Message);
        }
    }

    private void SaveRingSpawnSettings()
    {
        var dir = _serverPath.Text?.Trim() ?? "";
        if (!Directory.Exists(dir))
        {
            Log("Select a valid server folder first.");
            return;
        }

        try
        {
            var gsPath = GameSettingsPath();
            var gs = File.Exists(gsPath) ? ConfigIO.ReadGameSettings(gsPath) : new GameSettingsData();
            gs.RingSizes = _ringSizes.Text ?? "";
            gs.RingSpeeds = _ringSpeeds.Text ?? "";
            ConfigIO.WriteGameSettings(gs, gsPath);

            var starter = StarterPackConfigService.Read(dir);
            starter.ValidSpawnPoints = _validSpawnPoints.Text ?? "";
            starter.CustomSpawnPoint = _customSpawnPoint.Text ?? "";
            StarterPackConfigService.Write(dir, starter);
            ModConfigService.WriteSpawnPoints(dir, _matchSpawns.Text ?? "");
            Log("Saved ring and spawn settings.");
        }
        catch (Exception ex)
        {
            Log("Could not save ring/spawn settings: " + ex.Message);
        }
    }

    private void ApplySelectedSpawnLocation()
    {
        if (_spawnLocations.SelectedItem is not SpawnLocation spawn)
            return;

        _customSpawnPoint.Text = spawn.LobbySpawn;
        _matchSpawns.Text = spawn.MatchSpawns;
        _validSpawnPoints.Text = "6,";
        _ringSizes.Text = $"{spawn.RingSize},{spawn.RingSize},{spawn.RingSize}";
        _ringSpeeds.Text = "0.001,50,0.001";
        Log("Applied spawn location: " + spawn.Name);
    }

    private void ApplyStandardRingPreset()
    {
        _ringSizes.Text = "4240.0,3450.0,1710.0,830.0,360.0,140.0";
        _ringSpeeds.Text = "25.0,3.0,1.5,1.5,2,2";
    }

    private void ApplyDeathmatchRingPreset()
    {
        _ringSizes.Text = "244,244,244";
        _ringSpeeds.Text = "0.001,50,0.001";
    }

    private void LoadModSettings()
    {
        try
        {
            var dir = _serverPath.Text ?? "";
            var commission = ModConfigService.ReadCommission(dir);
            var fixes = ModConfigService.ReadFixes(dir);
            _enableLootDrops.IsChecked = fixes.EnableLootDrops;
            _attackerGrenadeEnabled.IsChecked = commission.GrenadeAttackerEnabled;
            _attackerChance.Text = commission.GrenadeAttackerChance.ToString(CultureInfo.InvariantCulture);
            SelectGrenade(_attackerGrenade, commission.GrenadeAttackerId);
            _corpseGrenadeEnabled.IsChecked = commission.GrenadeCorpseEnabled;
            _corpseChance.Text = commission.GrenadeCorpseChance.ToString(CultureInfo.InvariantCulture);
            SelectGrenade(_corpseGrenade, commission.GrenadeCorpseId);
            _lives.Text = commission.Lives.ToString(CultureInfo.InvariantCulture);
            _streamingDistance.Text = commission.StreamingDistance.ToString(CultureInfo.InvariantCulture);
            _banList.Text = string.Join(Environment.NewLine, (commission.BanList ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            LoadSimpleCfg(Path.Combine(dir, "BepInEx", "config", "tabginstaller.proximitychat.server.cfg"), new Dictionary<string, TextBox>
            {
                ["MaxRange"] = _proxMaxRange,
                ["MinRange"] = _proxMinRange,
            });
            var falloff = ReadSimpleCfgValue(Path.Combine(dir, "BepInEx", "config", "tabginstaller.proximitychat.server.cfg"), "FalloffCurve");
            SelectComboText(_proxFalloff, string.Equals(falloff, "Logarithmic", StringComparison.OrdinalIgnoreCase) ? "Logarithmic" : "Linear");
            var loggerSettings = ModConfigService.ReadServerLogger(dir);
            _serverLoggerLogToConsole.IsChecked = loggerSettings.LogToBepInExConsole;
            _serverLoggerWriteCsv.IsChecked = loggerSettings.WriteCsv;
            _serverLoggerWriteLegacy.IsChecked = loggerSettings.WriteLegacyServerLoggerTxt;
            _serverLoggerFallbackScan.IsChecked = loggerSettings.FallbackPlayerScan;
            _serverLoggerInterval.Text = loggerSettings.FallbackScanIntervalSeconds.ToString(CultureInfo.InvariantCulture);
            _serverLoggerLogDirectory.Text = loggerSettings.LogDirectory;
            _serverLoggerCsvFile.Text = loggerSettings.CsvFileName;
            _serverLoggerLegacyFile.Text = loggerSettings.LegacyFileName;
            LoadPluginSettingsPanels(dir);
            Log("Loaded mod settings.");
        }
        catch (Exception ex)
        {
            Log("Could not load mod settings: " + ex.Message);
        }
    }

    private void SaveModSettings()
    {
        var dir = _serverPath.Text?.Trim() ?? "";
        if (!Directory.Exists(dir))
        {
            Log("Select a valid server folder first.");
            return;
        }

        try
        {
            var commission = ModConfigService.ReadCommission(dir);
            commission.GrenadeAttackerEnabled = _attackerGrenadeEnabled.IsChecked == true;
            commission.GrenadeAttackerChance = ParseFloat(_attackerChance.Text, commission.GrenadeAttackerChance);
            commission.GrenadeAttackerId = SelectedGrenadeId(_attackerGrenade) ?? commission.GrenadeAttackerId;
            commission.GrenadeCorpseEnabled = _corpseGrenadeEnabled.IsChecked == true;
            commission.GrenadeCorpseChance = ParseFloat(_corpseChance.Text, commission.GrenadeCorpseChance);
            commission.GrenadeCorpseId = SelectedGrenadeId(_corpseGrenade) ?? commission.GrenadeCorpseId;
            commission.Lives = ParseInt(_lives.Text, commission.Lives);
            commission.StreamingDistance = ParseFloat(_streamingDistance.Text, commission.StreamingDistance);
            commission.BanList = string.Join(",", (_banList.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            ModConfigService.WriteCommission(dir, commission);
            ModConfigService.WriteFixes(dir, new StarterPackFixesSettings { EnableLootDrops = _enableLootDrops.IsChecked == true });
            WriteProximityCfg(dir);
            ModConfigService.WriteServerLogger(dir, BuildServerLoggerSettingsFromFields());
            SavePluginSettingsFromEditors();
            Log("Saved mod settings.");
        }
        catch (Exception ex)
        {
            Log("Could not save mod settings: " + ex.Message);
        }
    }

    private ServerLoggerSettings BuildServerLoggerSettingsFromFields()
    {
        var settings = new ServerLoggerSettings
        {
            LogToBepInExConsole = _serverLoggerLogToConsole.IsChecked == true,
            WriteCsv = _serverLoggerWriteCsv.IsChecked == true,
            WriteLegacyServerLoggerTxt = _serverLoggerWriteLegacy.IsChecked == true,
            FallbackPlayerScan = _serverLoggerFallbackScan.IsChecked == true,
            LogDirectory = string.IsNullOrWhiteSpace(_serverLoggerLogDirectory.Text) ? "server-logs" : _serverLoggerLogDirectory.Text.Trim(),
            CsvFileName = string.IsNullOrWhiteSpace(_serverLoggerCsvFile.Text) ? "players.csv" : _serverLoggerCsvFile.Text.Trim(),
            LegacyFileName = string.IsNullOrWhiteSpace(_serverLoggerLegacyFile.Text) ? "ServerLogger.txt" : _serverLoggerLegacyFile.Text.Trim(),
        };

        settings.FallbackScanIntervalSeconds = ParseFloat(_serverLoggerInterval.Text, 2f);
        return settings;
    }

    private void LoadPluginSettingsPanels(string serverDir)
    {
        _pluginSettingEditors.Clear();
        _additionalServerPluginSettings.Children.Clear();
        _clientPluginSettings.Children.Clear();

        BuildPluginSettingsPanel(
            _additionalServerPluginSettings,
            PluginSettingsCatalog.AdditionalServerPlugins,
            serverDir,
            "Select a server folder to edit server plugin settings.");

        var clientDir = _moddedClientPath.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(clientDir))
        {
            _clientPluginSettingsStatus.Text = "Set a modded client folder to edit client plugin settings.";
            return;
        }

        if (!Directory.Exists(clientDir))
        {
            _clientPluginSettingsStatus.Text = "Modded client folder not found: " + clientDir;
            return;
        }

        _clientPluginSettingsStatus.Text = "";
        BuildPluginSettingsPanel(
            _clientPluginSettings,
            PluginSettingsCatalog.ClientPlugins,
            clientDir,
            "Set a modded client folder to edit client plugin settings.");
    }

    private void BuildPluginSettingsPanel(
        StackPanel host,
        IEnumerable<PluginConfigDefinition> definitions,
        string rootPath,
        string missingPathText)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            host.Children.Add(new TextBlock { Text = missingPathText });
            return;
        }

        foreach (var definition in definitions)
        {
            var values = ModConfigService.ReadPluginConfigValues(rootPath, definition);
            var panel = new StackPanel { Spacing = 5 };
            panel.Children.Add(new TextBlock
            {
                Text = definition.DisplayName,
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            });
            panel.Children.Add(new TextBlock
            {
                Text = definition.Description,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            if (definition.Settings.Length == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No installer-configurable settings for this bundled plugin yet.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                });
            }
            else
            {
                foreach (var setting in definition.Settings)
                {
                    values.TryGetValue(setting.FullKey, out var configured);
                    panel.Children.Add(BuildPluginSettingRow(rootPath, definition, setting, configured ?? setting.DefaultValue));
                }

                panel.Children.Add(Button("Open cfg", () => OpenPluginConfig(rootPath, definition)));
            }

            host.Children.Add(ToolPanel(panel));
        }
    }

    private Control BuildPluginSettingRow(
        string rootPath,
        PluginConfigDefinition definition,
        PluginSettingDefinition setting,
        string value)
    {
        var editor = CreatePluginSettingEditor(setting, value);
        _pluginSettingEditors.Add(new PluginSettingEditor(rootPath, definition, setting, editor));

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("190,*"),
            Margin = new Avalonia.Thickness(0, 0, 0, 4)
        };

        var label = new TextBlock
        {
            Text = setting.Label,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(editor);
        if (!string.IsNullOrWhiteSpace(setting.Description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = setting.Description,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(setting.DefaultValue) ? "Default: empty" : "Default: " + setting.DefaultValue
        });
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        return grid;
    }

    private static Control CreatePluginSettingEditor(PluginSettingDefinition setting, string value)
    {
        if (setting.ValueType == PluginSettingValueType.Boolean)
        {
            return new CheckBox
            {
                IsChecked = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            };
        }

        if (setting.Options.Length > 0)
        {
            return new ComboBox
            {
                Width = 180,
                ItemsSource = setting.Options,
                SelectedItem = value
            };
        }

        return new TextBox
        {
            Width = setting.IsMultiline ? 360 : 180,
            MinHeight = setting.IsMultiline ? 72 : 0,
            AcceptsReturn = setting.IsMultiline,
            TextWrapping = setting.IsMultiline ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap,
            Text = value
        };
    }

    private void SavePluginSettingsFromEditors()
    {
        foreach (var group in _pluginSettingEditors.GroupBy(e => new { e.RootPath, e.Definition }))
        {
            if (string.IsNullOrWhiteSpace(group.Key.RootPath))
                continue;

            var values = group.ToDictionary(
                editor => editor.Setting.FullKey,
                editor => GetPluginSettingEditorValue(editor.Editor),
                StringComparer.OrdinalIgnoreCase);
            ModConfigService.WritePluginConfigValues(group.Key.RootPath, group.Key.Definition, values);
        }
    }

    private static string GetPluginSettingEditorValue(Control editor)
    {
        return editor switch
        {
            CheckBox checkBox => (checkBox.IsChecked == true).ToString().ToLowerInvariant(),
            ComboBox comboBox => comboBox.SelectedItem?.ToString() ?? "",
            TextBox textBox => textBox.Text ?? "",
            _ => ""
        };
    }

    private void OpenPluginConfig(string rootPath, PluginConfigDefinition definition)
    {
        var path = ModConfigService.PluginConfigPath(rootPath, definition);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);

        OpenPath(path);
    }

    private async void CreateBackupAsync()
    {
        var backup = new BackupService(new Progress<string>(Log));
        SetStatus("Sicherung wird erstellt…");
        var ok = await backup.CreateBackupAsync(_serverPath.Text ?? "");
        SetStatus(ok ? "Sicherung erstellt" : "Sicherung fehlgeschlagen");
        RefreshBackups();
        RefreshFunctionalUi();
    }

    private void RefreshBackups()
    {
        _backups.Items.Clear();
        var backup = new BackupService(new Progress<string>(Log));
        foreach (var item in backup.GetAvailableBackups(_serverPath.Text ?? ""))
            _backups.Items.Add(new BackupListItem(item));
    }

    private async void RestoreBackupAsync()
    {
        if (_backups.SelectedItem is not BackupListItem selection) return;
        var item = selection.Backup;
        if (_serverProcess.IsRunning)
        {
            if (!await _confirmations.ConfirmAsync(
                    "Server stoppen",
                    "Der Server läuft noch. Für eine sichere Wiederherstellung muss er zuerst gestoppt werden. Jetzt stoppen?"))
            {
                return;
            }

            _serverProcess.Stop();
        }

        if (!await _confirmations.ConfirmAsync(
                "Sicherung wiederherstellen",
                $"Sicherung '{item.Name}' wiederherstellen? Vorher wird automatisch eine Sicherheitskopie des aktuellen Stands erstellt."))
        {
            return;
        }

        var backup = new BackupService(new Progress<string>(Log));
        SetStatus("Sicherheitskopie vor Wiederherstellung…");
        if (!await backup.CreateBackupAsync(_serverPath.Text ?? ""))
        {
            SetStatus("Wiederherstellung abgebrochen");
            Log("Restore was cancelled because the safety backup failed.");
            return;
        }

        SetStatus("Sicherung wird wiederhergestellt…");
        var ok = await backup.RestoreBackupAsync(_serverPath.Text ?? "", item);
        SetStatus(ok ? "Sicherung wiederhergestellt" : "Wiederherstellung fehlgeschlagen");
        ReloadFunctionalServerData();
        RefreshFunctionalUi();
    }

    private async void DeleteBackup()
    {
        if (_backups.SelectedItem is not BackupListItem selection) return;
        var item = selection.Backup;
        if (!await _confirmations.ConfirmAsync(
                "Sicherung löschen",
                $"Sicherung '{item.Name}' endgültig löschen? Diese Aktion kann nicht rückgängig gemacht werden."))
        {
            return;
        }

        var backup = new BackupService(new Progress<string>(Log));
        backup.DeleteBackup(item);
        RefreshBackups();
    }

    private void LoadAdmins()
    {
        _adminList.Items.Clear();
        var path = PlayerPermsPath();
        if (!File.Exists(path))
        {
            Log("PlayerPerms.json not found yet.");
            return;
        }

        try
        {
            var root = JArray.Parse(File.ReadAllText(path));
            foreach (var player in root.SelectTokens("$..players[*]").OfType<JObject>())
            {
                _adminList.Items.Add(new AdminEntry(
                    player.Value<string>("name") ?? "",
                    player.Value<string>("epic") ?? "",
                    player.Value<int?>("permlevel") ?? 1));
            }
            Log("Loaded admins from PlayerPerms.json.");
        }
        catch (Exception ex)
        {
            Log("Could not load PlayerPerms.json: " + ex.Message);
        }
    }

    private void AddOrUpdateAdmin()
    {
        var entry = new AdminEntry(
            _adminName.Text?.Trim() ?? "",
            _adminEpic.Text?.Trim() ?? "",
            ParseInt(_adminLevel.Text, 4));
        if (string.IsNullOrWhiteSpace(entry.Epic))
        {
            Log("Epic ID is required.");
            return;
        }

        foreach (var item in _adminList.Items.OfType<AdminEntry>().ToList())
        {
            if (item.Epic.Equals(entry.Epic, StringComparison.OrdinalIgnoreCase))
            {
                _adminList.Items.Remove(item);
                _adminList.Items.Add(entry);
                return;
            }
        }

        _adminList.Items.Add(entry);
    }

    private void RemoveSelectedAdmin()
    {
        if (_adminList.SelectedItem != null)
            _adminList.Items.Remove(_adminList.SelectedItem);
    }

    private void SaveAdmins()
    {
        var dir = Path.GetDirectoryName(PlayerPermsPath());
        if (dir == null) return;
        Directory.CreateDirectory(dir);

        var players = new JArray();
        foreach (var entry in _adminList.Items.OfType<AdminEntry>())
        {
            players.Add(new JObject
            {
                ["name"] = entry.Name,
                ["epic"] = entry.Epic,
                ["permlevel"] = entry.PermLevel
            });
        }

        var root = new JArray
        {
            new JObject
            {
                ["name"] = "players",
                ["description"] = "TABG installer managed player permissions.",
                ["players"] = players
            }
        };

        File.WriteAllText(PlayerPermsPath(), root.ToString(Formatting.Indented));
        Log("Saved PlayerPerms.json.");
    }

    private void RefreshPresetLists()
    {
        _builtInPresets.Items.Clear();
        foreach (var preset in BuiltInPresets.All)
            _builtInPresets.Items.Add(preset);

        _userPresets.Items.Clear();
        var dir = _serverPath.Text?.Trim() ?? "";
        if (Directory.Exists(dir))
        {
            foreach (var name in PresetManager.ListPresets(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                _userPresets.Items.Add(name);
        }
    }

    private async void ApplyBuiltInPreset()
    {
        if (_builtInPresets.SelectedItem is not BuiltInPresets.BuiltInPreset preset)
        {
            Log("Select a built-in template first.");
            return;
        }

        var dir = _serverPath.Text?.Trim() ?? "";
        if (!Directory.Exists(dir))
        {
            Log("Select a valid server folder first.");
            return;
        }

        if (!await _confirmations.ConfirmAsync(
                "Apply built-in preset",
                $"Overwrite current config files with built-in preset '{preset.Name}'?"))
        {
            return;
        }

        BuiltInPresets.Deploy(preset, dir);
        Log("Applied built-in template: " + preset.Name);
        LoadConfig();
    }

    private void SaveUserPreset()
    {
        var dir = _serverPath.Text?.Trim() ?? "";
        var name = _presetName.Text?.Trim() ?? "";
        if (!Directory.Exists(dir) || string.IsNullOrWhiteSpace(name))
        {
            Log("Select a server folder and enter a preset name.");
            return;
        }

        PresetManager.SavePreset(dir, name, PresetManager.DefaultConfigRelativePaths);
        RefreshPresetLists();
        Log("Saved preset: " + name);
    }

    private async void LoadUserPreset()
    {
        if (_userPresets.SelectedItem is not string name) return;
        if (!await _confirmations.ConfirmAsync(
                "Load preset",
                $"Overwrite current config files with saved preset '{name}'?"))
        {
            return;
        }

        PresetManager.LoadPreset(_serverPath.Text ?? "", name);
        Log("Loaded preset: " + name);
        LoadConfig();
    }

    private async void DeleteUserPreset()
    {
        if (_userPresets.SelectedItem is not string name) return;
        if (!await _confirmations.ConfirmAsync(
                "Delete preset",
                $"Delete preset '{name}'? This cannot be undone."))
        {
            return;
        }

        PresetManager.DeletePreset(_serverPath.Text ?? "", name);
        RefreshPresetLists();
        Log("Deleted preset: " + name);
    }

    private async void InstallClientModsAsync()
    {
        var ok = await ClientModInstaller.InstallAsync(
            _clientPath.Text ?? "",
            _moddedClientPath.Text ?? "",
            new List<string>(),
            new Progress<string>(Log));

        Log(ok ? "Client prepared." : "Client preparation failed.");
        RefreshClientModLists();
    }

    private string ServerPluginDir() => Path.Combine(_serverPath.Text ?? "", "BepInEx", "plugins");
    private string ClientPluginDir() => Path.Combine(_moddedClientPath.Text ?? "", "BepInEx", "plugins");

    private void RefreshServerModLists()
    {
        RefreshPluginList(_serverPluginList, ServerPluginDir());
        RefreshServerPluginCatalog();
        RefreshBundledList(_bundledServerPluginList, "plugins");
    }

    private void RefreshClientModLists()
    {
        RefreshPluginList(_clientPluginList, ClientPluginDir());
        RefreshClientPluginCatalog();
        RefreshBundledList(_bundledClientPluginList, "client-plugins");
    }

    private void RefreshPluginList(ListBox list, string dir)
    {
        list.Items.Clear();
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            list.Items.Add(new PluginFileItem(file, true));
        foreach (var file in Directory.GetFiles(dir, "*.dll.disabled", SearchOption.AllDirectories).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            list.Items.Add(new PluginFileItem(file, false));
    }

    private void RefreshBundledList(ListBox list, string folder)
    {
        list.Items.Clear();
        foreach (var file in FindFilesNearApp(folder, "*.dll").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            list.Items.Add(file);
    }

    private void RefreshServerPluginCatalog()
    {
        _serverPluginCatalogChecks.Children.Clear();
        foreach (var plugin in PluginCatalogGrouper.Collapse(PluginRegistry.ServerPlugins))
            _serverPluginCatalogChecks.Children.Add(BuildCatalogCheckBox(
                BuildCatalogItem(plugin.Primary, plugin.Label, "plugins", ServerPluginDir()),
                ToggleServerCatalogPlugin));
    }

    private void RefreshClientPluginCatalog()
    {
        _clientPluginCatalogChecks.Children.Clear();
        foreach (var plugin in PluginCatalogGrouper.Collapse(PluginRegistry.ClientMods))
            _clientPluginCatalogChecks.Children.Add(BuildCatalogCheckBox(
                BuildCatalogItem(plugin.Primary, plugin.Label, "client-plugins", ClientPluginDir()),
                ToggleClientCatalogPlugin));
    }

    private PluginCatalogItem BuildCatalogItem(PluginDefinition plugin, string label, string bundledFolder, string pluginDir)
    {
        var installed = plugin.DllNames.Length > 0 && plugin.DllNames.All(dll =>
            File.Exists(Path.Combine(pluginDir, dll)) ||
            File.Exists(Path.Combine(pluginDir, dll + ".disabled")));
        var enabled = plugin.DllNames.Length > 0 && plugin.DllNames.All(dll =>
            File.Exists(Path.Combine(pluginDir, dll)));
        var available = plugin.DllNames.Length == 0 || plugin.DllNames.All(dll =>
            FindFileNearApp(Path.Combine(bundledFolder, dll)) != null);

        return new PluginCatalogItem(plugin, label, installed, enabled, available);
    }

    private Control BuildCatalogCheckBox(PluginCatalogItem item, Action<PluginCatalogItem> toggle)
    {
        var checkBox = new CheckBox
        {
            Content = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock { Text = item.Name, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                            new TextBlock { Text = item.Status, Foreground = Brush.Parse("#9AA8B7"), FontSize = 12 },
                        },
                    },
                    new TextBlock
                    {
                            Text = string.IsNullOrWhiteSpace(PluginDescriptionGerman(item.Name))
                                ? "Mitgelieferte TABG-Erweiterung"
                                : PluginDescriptionGerman(item.Name),
                        Foreground = Brush.Parse("#9AA8B7"),
                        FontSize = 12,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                },
            },
            IsChecked = item.Enabled,
            IsEnabled = item.CanToggle,
            Tag = item,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        checkBox.Click += (_, _) => toggle(item);
        return new Border
        {
            BorderBrush = Brush.Parse("#303A47"),
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            Padding = new Avalonia.Thickness(8, 7),
            Child = checkBox,
        };
    }

    private static string PluginDescriptionGerman(string name)
    {
        var normalized = name.ToLowerInvariant();
        if (normalized.Contains("citrus"))
            return "Grundbibliothek für Adminbefehle, Berechtigungen und zentrale Serverfunktionen.";
        if (normalized.Contains("anti-cheat"))
            return "Ermöglicht private oder angepasste Server ohne die normalen EAC-/EOS-Startpfade.";
        if (normalized.Contains("custom grenades"))
            return "Ergänzt besondere Granaten und die dazugehörigen Client-Effekte.";
        if (normalized.Contains("proximity") || normalized.Contains("nähe"))
            return "Sprachchat, dessen Lautstärke von der Entfernung der Spieler abhängt.";
        if (normalized.Contains("match core"))
            return "Steuert Ringe, Loadouts, Abstimmungen, Loot, Timer und Siegbedingungen.";
        if (normalized.Contains("server logger"))
            return "Protokolliert Spielernamen sowie PlayFab- und Epic-IDs nachvollziehbar.";
        if (normalized.Contains("unusedvehicles") || normalized.Contains("unused vehicles"))
            return "Macht zusätzliche Fahrzeuge wie Helikopter, UFO und HoverBike verfügbar.";
        if (normalized.Contains("radar"))
            return "Admin- und Debug-Radar für private Server; sensible Echtspielerdaten bleiben bewusst konfigurierbar.";
        if (normalized.Contains("fake") || normalized.Contains("dummy"))
            return "Erzeugt Testspieler für private Entwicklungs- und Funktionstests.";
        if (normalized.Contains("solo"))
            return "Vereinfacht lokale Einzelspieler- und Serverfunktionstests.";
        if (normalized.Contains("coordinates") || normalized.Contains("coords"))
            return "Zeigt dem modifizierten Client die aktuelle Position an.";
        if (normalized.Contains("enhanced client"))
            return "Bündelt zusätzliche Komfortfunktionen für den modifizierten TABG-Client.";
        if (normalized.Contains("flying"))
            return "Ergänzt Steuerungen für fliegende Fahrzeuge im modifizierten Client.";
        if (normalized.Contains("popup"))
            return "Unterdrückt störende Hinweise, die beim modifizierten Client auftreten können.";
        if (normalized.Contains("mod settings"))
            return "Stellt eine Oberfläche für unterstützte Mod-Einstellungen im Client bereit.";

        return string.Empty;
    }

    private async void AddServerPluginAsync()
    {
        var file = await PickDllAsync("Select server plugin DLL");
        if (file == null) return;
        InstallDllToFolder(file, ServerPluginDir());
        RefreshServerModLists();
    }

    private async void AddClientPluginAsync()
    {
        var file = await PickDllAsync("Select client plugin DLL");
        if (file == null) return;
        InstallDllToFolder(file, ClientPluginDir());
        RefreshClientModLists();
    }

    private void InstallBundledServerPlugin()
    {
        if (_bundledServerPluginList.SelectedItem is not string file) return;
        InstallDllToFolder(file, ServerPluginDir());
        RefreshServerModLists();
    }

    private void ToggleServerCatalogPlugin(PluginCatalogItem item)
    {
        ToggleCatalogPlugin(item, "plugins", ServerPluginDir(), RefreshServerModLists);
    }

    private void ToggleClientCatalogPlugin(PluginCatalogItem item)
    {
        ToggleCatalogPlugin(item, "client-plugins", ClientPluginDir(), RefreshClientModLists);
    }

    private void ToggleCatalogPlugin(PluginCatalogItem item, string bundledFolder, string pluginDir, Action refresh)
    {
        if (item.Enabled)
            MoveCatalogPlugin(item, pluginDir, enable: false);
        else if (item.Installed)
            MoveCatalogPlugin(item, pluginDir, enable: true);
        else
            InstallCatalogPlugin(item, bundledFolder, pluginDir);

        refresh();
    }

    private void InstallCatalogPlugin(PluginCatalogItem item, string bundledFolder, string pluginDir)
    {
        foreach (var dll in item.Plugin.DllNames)
        {
            var file = FindFileNearApp(Path.Combine(bundledFolder, dll));
            if (file == null)
            {
                Log("Bundled DLL not found: " + dll);
                continue;
            }

            InstallDllToFolder(file, pluginDir);
        }
    }

    private void MoveCatalogPlugin(PluginCatalogItem item, string pluginDir, bool enable)
    {
        foreach (var dll in item.Plugin.DllNames)
        {
            var enabledPath = Path.Combine(pluginDir, dll);
            var disabledPath = enabledPath + ".disabled";
            var src = enable ? disabledPath : enabledPath;
            var dst = enable ? enabledPath : disabledPath;
            if (!File.Exists(src)) continue;
            File.Move(src, dst, overwrite: true);
        }

        Log((enable ? "Enabled " : "Disabled ") + item.Name);
    }

    private void InstallBundledClientPlugin()
    {
        if (_bundledClientPluginList.SelectedItem is not string file) return;
        InstallDllToFolder(file, ClientPluginDir());
        RefreshClientModLists();
    }

    private void ToggleSelectedServerPlugin()
    {
        TogglePluginFile(_serverPluginList.SelectedItem as PluginFileItem);
        RefreshServerModLists();
    }

    private void ToggleSelectedClientPlugin()
    {
        TogglePluginFile(_clientPluginList.SelectedItem as PluginFileItem);
        RefreshClientModLists();
    }

    private async void RemoveSelectedServerPlugin()
    {
        var item = _serverPluginList.SelectedItem as PluginFileItem;
        if (item == null) return;
        if (!await _confirmations.ConfirmAsync(
                "Remove server DLL",
                $"Remove '{Path.GetFileName(item.Path)}'?"))
        {
            return;
        }

        RemovePluginFile(item);
        RefreshServerModLists();
    }

    private async void RemoveSelectedClientPlugin()
    {
        var item = _clientPluginList.SelectedItem as PluginFileItem;
        if (item == null) return;
        if (!await _confirmations.ConfirmAsync(
                "Remove client DLL",
                $"Remove '{Path.GetFileName(item.Path)}'?"))
        {
            return;
        }

        RemovePluginFile(item);
        RefreshClientModLists();
    }

    private void QuickRestartServer()
    {
        _serverProcess.Stop();
        Task.Delay(1200).ContinueWith(_ =>
            _dispatcher.Post(() => StartServer("-batchmode -nographics -nolog")));
        Log("Queued server restart.");
    }

    private void ExportVisibleLog()
    {
        var path = Path.Combine(Path.GetDirectoryName(_logPath) ?? AppContext.BaseDirectory, $"tabg-installer-app-export-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.WriteAllText(path, _log.Text ?? "");
        Log("Exported visible log to " + path);
    }

    private void FindInVisibleLog()
    {
        var query = _consoleSearch.Text;
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(_log.Text)) return;
        var index = _log.Text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase);
        Log(index >= 0 ? $"Found '{query}' at visible log offset {index}." : $"'{query}' not found in visible log.");
    }

    private void SendConsoleCommand()
    {
        var command = _consoleCommand.Text?.Trim();
        if (string.IsNullOrWhiteSpace(command)) return;
        Log("[operator command note] " + command);
        _consoleCommand.Text = "";
    }

    private void StartModdedClient()
        => StartModdedClientCore();

    private bool StartModdedClientCore()
    {
        var clientDir = _moddedClientPath.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(clientDir) || !Directory.Exists(clientDir))
        {
            Log("Select or install a valid modded client folder first.");
            return false;
        }

        var executable = ResolveClientLaunchTarget(clientDir);
        if (executable == null)
        {
            Log("No modded TABG launcher found. Install / update client mods first.");
            return false;
        }

        try
        {
            if (_clientProcess?.HasExited == false)
            {
                Log("Modded client is already running.");
                return true;
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
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _clientProcess = process;

            SetStatus("Modded client started");
            RefreshFunctionalUi();
            return true;
        }
        catch (Exception ex)
        {
            Log("Could not start modded client: " + ex.Message);
            SetStatus("Client start failed");
            RefreshFunctionalUi();
            return false;
        }
    }

    private void LoadPluginRegistry()
    {
        PluginRegistry.LoadBundledManifests();
        Log(PluginRegistry.IsLoadedFromRegistry
            ? "Loaded plugin definitions from bundled manifests."
            : "Loaded fallback built-in plugin definitions.");
    }

    private void RebuildPluginChecks()
    {
        _pluginChecks.Children.Clear();
        var plugins = PluginRegistry.ServerPlugins
            .Where(p => p.Kind != PluginKind.CoreDependency)
            .ToArray();

        foreach (var item in PluginCatalogGrouper.Collapse(plugins))
        {
            var parts = item.Label.Split(" - ", 2, StringSplitOptions.None);
            var name = parts[0];
            var description = PluginDescriptionGerman(name);
            if (string.IsNullOrWhiteSpace(description))
                description = "Optionale Funktion für angepasste TABG-Server.";
            if (item.Primary.RequiresClientMod)
                description += " Benötigt die passende Client-Erweiterung.";

            _pluginChecks.Children.Add(new CheckBox
            {
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = name, FontWeight = FontWeight.SemiBold },
                        new TextBlock
                        {
                            Text = description,
                            Foreground = Brush.Parse("#93A4B7"),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
                Tag = item.Primary,
                IsChecked = item.Primary.DefaultChecked,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Avalonia.Thickness(4),
            });
        }
    }

    private void RebuildClientModChecks()
    {
        _clientModChecks.Children.Clear();
        var plugins = PluginRegistry.ClientMods
            .Where(p => p.DefaultChecked)
            .ToArray();

        foreach (var item in PluginCatalogGrouper.Collapse(plugins))
        {
            var plugin = item.Primary;
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

    private void WireServerProcess()
    {
        _serverProcess.OutputReceived += Log;
        _serverProcess.ProcessExited += code =>
        {
            Log($"Server exited with code {code}.");
            SetStatus("Server stopped");
            RefreshFunctionalUi();
        };
    }

    private void TryAutoDetectPaths()
    {
        DetectServerPath();
        DetectClientPath();
    }

    private void DetectServerPath()
    {
        var detected = _steamPathDetector.TryFindServerPath();
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

        RefreshServerModLists();
        LoadModSettings();
        RefreshFunctionalUi();
    }

    private void DetectClientPath()
    {
        var detected = _steamPathDetector.TryFindClientPath();
        if (!string.IsNullOrEmpty(detected))
        {
            _clientPath.Text = detected;
            _moddedClientPath.Text = Path.Combine(Path.GetDirectoryName(detected) ?? detected, "TotallyAccurateBattlegrounds-Modded");
        }

        RefreshClientModLists();
        LoadPluginSettingsPanels(_serverPath.Text ?? "");
        RefreshFunctionalUi();
    }

    private async void BrowseServerAsync()
    {
        if (await PickFolderInto(_serverPath, "Select TABG server folder"))
        {
            RefreshServerModLists();
            LoadModSettings();
            ReloadFunctionalServerData();
            RefreshFunctionalUi();
        }
    }

    private async void BrowseClientAsync()
    {
        if (await PickFolderInto(_clientPath, "Select TABG Steam folder"))
        {
            RefreshClientModLists();
            LoadPluginSettingsPanels(_serverPath.Text ?? "");
            RefreshFunctionalUi();
        }
    }

    private async void BrowseModdedClientAsync()
    {
        if (await PickFolderInto(_moddedClientPath, "Select modded TABG folder"))
        {
            RefreshClientModLists();
            LoadPluginSettingsPanels(_serverPath.Text ?? "");
            RefreshFunctionalUi();
        }
    }

    private async Task<bool> PickFolderInto(TextBox target, string title)
    {
        var path = await _storagePicker.PickFolderAsync(title);
        if (!string.IsNullOrEmpty(path))
        {
            target.Text = path;
            return true;
        }

        return false;
    }

    private IEnumerable<PluginDefinition> SelectedDefinitions(StackPanel host)
        => host.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => cb.Tag)
            .OfType<PluginDefinition>();

    private static void SetChecks(StackPanel host, bool value)
    {
        foreach (var cb in host.Children.OfType<CheckBox>())
            cb.IsChecked = value;
    }

    private void ValidateGameSettingsEditors(IReadOnlyDictionary<string, TextBlock> warningBlocks)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var minimums = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var maximums = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in typeof(GameSettingsData).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!_gameSettingEditors.TryGetValue(property.Name, out var editor))
                continue;

            try
            {
                values[property.Name] = ConvertEditorValue(editor, property.PropertyType);
            }
            catch
            {
                // While a user is typing, incomplete numeric values are validated on the next edit.
            }

            if (property.PropertyType == typeof(int) || property.PropertyType == typeof(float) || property.PropertyType == typeof(double))
            {
                minimums[property.Name] = 0;
                maximums[property.Name] = double.MaxValue;
                if (KnowledgeIndex.Current.GameSettings.TryGetValue(property.Name, out var knowledge)
                    && knowledge.Range != null)
                {
                    if (double.TryParse(knowledge.Range.Min, NumberStyles.Any, CultureInfo.InvariantCulture, out var min))
                        minimums[property.Name] = min;
                    if (double.TryParse(knowledge.Range.Max, NumberStyles.Any, CultureInfo.InvariantCulture, out var max))
                        maximums[property.Name] = max;
                }
            }
        }

        var warnings = new ConfigValidationService().Validate(values, minimums, maximums);
        foreach (var pair in warningBlocks)
        {
            if (warnings.TryGetValue(pair.Key, out var messages) && messages.Count > 0)
            {
                pair.Value.Text = "⚠ " + string.Join(" · ", messages.Select(TranslateValidationWarning));
                pair.Value.IsVisible = true;
            }
            else
            {
                pair.Value.Text = string.Empty;
                pair.Value.IsVisible = false;
            }
        }
    }

    private static string GameSettingDisplayName(string name) => name switch
    {
        "ServerName" => "Servername",
        "ServerDescription" => "Serverbeschreibung",
        "ServerBrowserIP" => "Öffentliche Server-IP",
        "Password" => "Serverpasswort",
        "TeamMode" => "Teammodus",
        "GameMode" => "Spielmodus",
        "Port" => "Serverport",
        "MaxPlayers" => "Maximale Spieler",
        "GroupsToStart" => "Gruppen zum Starten",
        "MinPlayersToForceStart" => "Mindestspieler für erzwungenen Start",
        "PlayersToStart" => "Zum Start benötigte Spieler",
        "KillsToWin" => "Kills zum Gewinnen",
        "RoundsToWin" => "Runden zum Gewinnen",
        "NumberOfLivesPerTeam" => "Leben pro Team",
        "MaxNumberOfTeamsAuto" => "Maximale automatisch erstellte Teams",
        "SpawnBots" => "Bots erzeugen",
        "UseSouls" => "Seelen verwenden",
        "BombTime" => "Bombenzeit",
        "RoundTime" => "Rundendauer",
        "CarSpawnRate" => "Fahrzeug-Spawnrate",
        "ForceStartTime" => "Zeit bis zum erzwungenen Start",
        "Countdown" => "Countdown",
        "BaseRingTime" => "Grunddauer des Rings",
        "TimeBeforeFirstRing" => "Zeit bis zum ersten Ring",
        "StripLootByPercentage" => "Loot-Reduktion",
        "WeaponDissapearTime" => "Zeit bis Waffen verschwinden",
        "BombDefuseTime" => "Entschärfungszeit",
        "RingSizes" => "Ringgrößen",
        "RingSpeeds" => "Ringgeschwindigkeiten",
        "Relay" => "Steam-Relay verwenden",
        "AutoTeam" => "Teams automatisch bilden",
        "AllowRespawnMinigame" => "Respawn-Minispiel erlauben",
        "UseTimedForceStart" => "Zeitgesteuerten Start verwenden",
        "AntiCheat" => "Anti-Cheat aktivieren",
        "LAN" => "Nur im lokalen Netzwerk",
        "AllowSpectating" => "Zuschauen erlauben",
        "NoRing" => "Ring deaktivieren",
        "UseKicks" => "Kicks erlauben",
        "DEBUG_DEATHMATCH" => "Deathmatch-Debugmodus",
        "UsePlayFabStats" => "PlayFab-Statistiken verwenden",
        "AllowRejoins" => "Wiedereinstieg erlauben",
        "AntiCheatDebugLogging" => "Anti-Cheat-Debugprotokoll",
        "AntiCheatEventLogging" => "Anti-Cheat-Ereignisprotokoll",
        _ => HumanizeIdentifier(name),
    };

    private static string GameSettingFallbackDescription(string name) => name switch
    {
        "ServerName" => "Dieser Name erscheint in der Serverliste.",
        "ServerDescription" => "Kurze Beschreibung für Spieler in der Serverliste.",
        "Password" => "Leer lassen, wenn der Server ohne Passwort erreichbar sein soll.",
        "GameMode" => "Der grundlegende TABG-Spielmodus.",
        "TeamMode" => "Legt die Teamgröße und Gruppierung fest.",
        "MaxPlayers" => "Wie viele Spieler gleichzeitig teilnehmen können.",
        "PlayersToStart" => "Die Runde wartet, bis mindestens diese Zahl erreicht ist.",
        "Port" => "Netzwerkport des Dedicated Servers.",
        "RingSizes" => "Kommagetrennte Größen für die Ringphasen.",
        "RingSpeeds" => "Kommagetrennte Geschwindigkeiten für die Ringphasen.",
        _ => $"Technischer Schlüssel: {name}",
    };

    private static string TranslateValidationWarning(string message)
    {
        if (message.Contains("maximum of 253", StringComparison.OrdinalIgnoreCase))
            return "Der Server unterstützt höchstens 253 Spieler.";
        if (message.Contains("PlayersToStart exceeds MaxPlayers", StringComparison.OrdinalIgnoreCase))
            return "Die zum Start benötigten Spieler überschreiten die maximale Spielerzahl.";
        if (message.Contains("MinPlayersToForceStart exceeds MaxPlayers", StringComparison.OrdinalIgnoreCase))
            return "Die Mindestspielerzahl für den erzwungenen Start ist zu hoch.";
        if (message.Contains("unless GameMode is Brawl", StringComparison.OrdinalIgnoreCase))
            return "Dieser Wert wirkt nur im Spielmodus Brawl.";
        if (message.Contains("unless GameMode is Bomb", StringComparison.OrdinalIgnoreCase))
            return "Dieser Wert wirkt nur im Spielmodus Bomb.";
        if (message.Contains("unless GameMode is BattleRoyale", StringComparison.OrdinalIgnoreCase))
            return "Ringwerte wirken nur im Spielmodus Battle Royale.";
        if (message.Contains("when NoRing is enabled", StringComparison.OrdinalIgnoreCase))
            return "Ringwerte werden ignoriert, solange der Ring deaktiviert ist.";
        if (message.Contains("ignored when UseTimedForceStart", StringComparison.OrdinalIgnoreCase))
            return "Dieser Wert wird ohne zeitgesteuerten Start ignoriert.";
        if (message.Contains("Relay is typically disabled for LAN", StringComparison.OrdinalIgnoreCase))
            return "Für einen LAN-Server sollte das Relay normalerweise deaktiviert sein.";
        if (message.Contains("Bots in BattleRoyale", StringComparison.OrdinalIgnoreCase))
            return "Bots können im Battle-Royale-Modus instabil sein.";
        if (message.Contains("outside the valid range", StringComparison.OrdinalIgnoreCase))
            return message.Replace("Value", "Wert", StringComparison.OrdinalIgnoreCase)
                .Replace("is outside the valid range", "liegt außerhalb des gültigen Bereichs", StringComparison.OrdinalIgnoreCase);
        return message;
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]))
                builder.Append(' ');
            builder.Append(current == '_' ? ' ' : current);
        }

        return builder.ToString();
    }

    private void SelectSigmaPreset()
    {
        foreach (var cb in _pluginChecks.Children.OfType<CheckBox>())
        {
            if (cb.Tag is PluginDefinition plugin)
                cb.IsChecked = PluginRegistry.SigmaPresetIds.Contains(plugin.Id);
        }
    }

    private static TextBox SmallBox(string text = "") => new() { Text = text, Width = 80 };

    private static Control CreateGameSettingEditor(PropertyInfo prop)
    {
        if (prop.PropertyType == typeof(bool))
            return new CheckBox { HorizontalAlignment = HorizontalAlignment.Left };

        if (prop.Name == nameof(GameSettingsData.TeamMode))
            return new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "SQUAD", "DUO", "SOLO" } };

        if (prop.Name == nameof(GameSettingsData.GameMode))
            return new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "BattleRoyale", "Brawl", "Test", "Bomb", "Deception" } };

        return new TextBox { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private static void SetEditorValue(Control editor, object? value)
    {
        switch (editor)
        {
            case CheckBox cb:
                cb.IsChecked = value is bool b && b;
                break;
            case ComboBox combo:
                SelectComboText(combo, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                break;
            case TextBox box:
                box.Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                break;
        }
    }

    private static object ConvertEditorValue(Control editor, Type type)
    {
        if (type == typeof(bool))
            return editor is CheckBox cb && cb.IsChecked == true;

        var text = editor switch
        {
            ComboBox combo => combo.SelectedItem?.ToString() ?? "",
            TextBox box => box.Text ?? "",
            _ => ""
        };

        if (type == typeof(int))
            return ParseInt(text, 0);
        if (type == typeof(float))
            return ParseFloat(text, 0);

        return text;
    }

    private static int? ParseNullableInt(string? text)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static int ParseInt(string? text, int fallback)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static float ParseFloat(string? text, float fallback)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static void SelectComboText(ComboBox combo, string text)
    {
        foreach (var item in combo.Items)
        {
            if (string.Equals(item?.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static int? SelectedGrenadeId(ComboBox combo)
    {
        var text = combo.SelectedItem?.ToString() ?? "";
        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open && int.TryParse(text[(open + 1)..close], out var id))
            return id;
        return null;
    }

    private static void SelectGrenade(ComboBox combo, int id)
    {
        var needle = $"({id})";
        foreach (var item in combo.Items)
        {
            if ((item?.ToString() ?? "").Contains(needle, StringComparison.Ordinal))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static void LoadSimpleCfg(string path, IReadOnlyDictionary<string, TextBox> targets)
    {
        if (!File.Exists(path)) return;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("[")) continue;
            var idx = line.IndexOf('=');
            if (idx < 1) continue;
            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if (targets.TryGetValue(key, out var box))
                box.Text = value;
        }
    }

    private static string? ReadSimpleCfgValue(string path, string key)
    {
        if (!File.Exists(path)) return null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
            var idx = line.IndexOf('=');
            if (idx >= 0) return line[(idx + 1)..].Trim();
        }
        return null;
    }

    private void WriteProximityCfg(string serverDir)
    {
        var cfg = Path.Combine(serverDir, "BepInEx", "config", "tabginstaller.proximitychat.server.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(cfg)!);
        File.WriteAllText(cfg, $@"[ProximityChat]

## Distance beyond which audio is not relayed
# Setting type: Single
# Default value: 50
MaxRange = {_proxMaxRange.Text?.Trim()}

## Distance within which audio is full volume
# Setting type: Single
# Default value: 5
MinRange = {_proxMinRange.Text?.Trim()}

## Volume falloff: Linear or Logarithmic
# Setting type: String
# Default value: Linear
FalloffCurve = {_proxFalloff.SelectedItem}
");
    }

    private void InstallDllToFolder(string sourceFile, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        var dest = Path.Combine(targetDir, Path.GetFileName(sourceFile));
        File.Copy(sourceFile, dest, overwrite: true);
        Log("Installed DLL: " + dest);
    }

    private static void TogglePluginFile(PluginFileItem? item)
    {
        if (item == null || !File.Exists(item.Path)) return;
        var target = item.Enabled
            ? item.Path + ".disabled"
            : item.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? item.Path[..^".disabled".Length]
                : item.Path;
        File.Move(item.Path, target, overwrite: true);
    }

    private static void RemovePluginFile(PluginFileItem? item)
    {
        if (item == null || !File.Exists(item.Path)) return;
        File.Delete(item.Path);
    }

    private async Task<string?> PickDllAsync(string title)
    {
        return await _storagePicker.PickFileAsync(title, "*.dll");
    }

    private static string BuildCommandsReference()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "Common TABG dedicated server/operator notes",
            "",
            "Start args used by this GUI: -batchmode -nographics -nolog",
            "Main configs: game_settings.txt, TheStarterPack.txt, BepInEx/config/CitrusLib/PlayerPerms.json",
            "Enable/disable local DLLs by renaming .dll <-> .dll.disabled in the Server Mods or Client panels.",
            "",
            "The dedicated server does not expose a reliable stdin command pipe in this launcher."
        });
    }

    private static string BuildItemsReference(string? filter)
    {
        var query = filter?.Trim();
        var items = ItemDatabase.AllItems
            .Where(i => string.IsNullOrWhiteSpace(query) ||
                i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Id.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name);
        return string.Join(Environment.NewLine, items.Select(i => $"{i.Id,4}  {i.Category,-14}  {i.Name}"));
    }

    private static string BuildSpawnsReference()
    {
        return string.Join(Environment.NewLine + Environment.NewLine,
            ItemDatabase.SpawnLocations.Select(s =>
                $"{s.Name}{Environment.NewLine}Lobby: {s.LobbySpawn}{Environment.NewLine}Ring center: {s.RingCenter} size {s.RingSize}{Environment.NewLine}Match spawns: {s.MatchSpawns}"));
    }

    private static string BuildLoadoutReference()
    {
        return "Loadout syntax:" + Environment.NewLine +
               "Name(allowed attachment ids):weight%itemId:amount,itemId:amount/" + Environment.NewLine +
               "Example: AK2K(1,0,14):10%151:1,6:255,6:255/" + Environment.NewLine +
               "ItemsGiven syntax: itemId:amount,itemId:amount,";
    }

    private void RefreshSettingsSummary()
    {
        _settingsSummary.Text = string.Join(Environment.NewLine, new[]
        {
            "Avalonia app status",
            $"App folder: {AppContext.BaseDirectory}",
            $"Log file: {_logPath}",
            $"Server folder: {_serverPath.Text}",
            $"Client folder: {_clientPath.Text}",
            $"Modded client: {_moddedClientPath.Text}",
            $"Bundled server plugins: {FindFilesNearApp("plugins", "*.dll").Count()}",
            $"Bundled client plugins: {FindFilesNearApp("client-plugins", "*.dll").Count()}"
        });
    }

    private void HardResetDetectedPaths()
    {
        _serverPath.Text = "";
        _clientPath.Text = "";
        _moddedClientPath.Text = "";
        TryAutoDetectPaths();
        RefreshSettingsSummary();
        ShowInitialFunctionalPage();
        RefreshFunctionalUi();
    }

    private void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var target = File.Exists(path)
            ? Path.GetDirectoryName(path)
            : Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(target) || !Directory.Exists(target))
        {
            Log("Path does not exist: " + path);
            return;
        }

        try
        {
            if (!_externalLauncher.TryOpenPath(target, out var error))
                Log("Could not open path: " + (error ?? "unknown launcher error"));
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
        UpdateFunctionalActivity(message);
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

        _dispatcher.Post(FlushVisibleLog);
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
        _dispatcher.Post(() =>
        {
            _functionalOperationStatus = status;
            _status.Text = status;
            RefreshFunctionalUi();
        });
    }

    internal void ReportUnexpectedUiException(Exception exception)
    {
        Log("UI-Aktion fehlgeschlagen, App läuft weiter: " + exception);
        SetStatus("Aktion fehlgeschlagen · Details unter Diagnose");
    }

    private Button Button(string text, Action action)
    {
        var button = new Button { Content = text, MinWidth = 110 };
        button.Click += (_, _) =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                ReportUnexpectedUiException(ex);
            }
        };
        return button;
    }

    private Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text, MinWidth = 110 };
        button.Click += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                ReportUnexpectedUiException(ex);
            }
        };
        return button;
    }

    private Button FlowButton(string text, Action action)
    {
        var button = Button(text, action);
        button.Margin = new Avalonia.Thickness(0, 0, 6, 6);
        return button;
    }

    private static Border ToolPanel(Control child)
    {
        return new Border
        {
            Padding = new Avalonia.Thickness(10),
            BorderBrush = Avalonia.Media.Brushes.Gray,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(4),
            Child = child
        };
    }

    private static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private Control PathRow(string label, TextBox box, Action browse)
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

    private static void Put(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        control.Margin = new Avalonia.Thickness(2);
        grid.Children.Add(control);
    }

    private static string? FindFileNearApp(string relativePath)
    {
        return BundledAssetLocator.FindFile(relativePath);
    }

    private static bool IsBundledClientPluginAvailable(PluginDefinition plugin)
    {
        return plugin.DllNames.Length == 0 ||
            plugin.DllNames.All(dll => FindFileNearApp(Path.Combine("client-plugins", dll)) != null);
    }

    private static IEnumerable<string> FindFilesNearApp(string relativeDir, string pattern)
    {
        return BundledAssetLocator.FindFiles(relativeDir, pattern);
    }

    private sealed record AdminEntry(string Name, string Epic, int PermLevel)
    {
        public override string ToString() => $"{Name} - {Epic} - level {PermLevel}";
    }

    private sealed record BackupListItem(BackupInfo Backup)
    {
        public override string ToString()
            => $"{Backup.CreatedDate:dd.MM.yyyy · HH:mm}  ·  {Backup.SizeInBytes / 1024d / 1024d:0.#} MB  ·  {Backup.Name}";
    }

    private sealed record PluginFileItem(string Path, bool Enabled)
    {
        public override string ToString()
        {
            var name = System.IO.Path.GetFileName(Path);
            if (name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                name = name[..^".disabled".Length];
            return $"[{(Enabled ? "on" : "off")}] {name}";
        }
    }

    private sealed record PluginCatalogItem(PluginDefinition Plugin, string Label, bool Installed, bool Enabled, bool Available)
    {
        public string Name => SplitLabel(Label).name;
        public string Description => SplitLabel(Label).description;
        public string Dlls => Plugin.DllNames.Length == 0 ? "Handled by installer" : string.Join(", ", Plugin.DllNames);
        public string Status => Enabled
            ? "Aktiv"
            : Installed
                ? "Installiert · deaktiviert"
                : Available
                    ? "Bereit zur Installation"
                    : "Mitgelieferte Datei fehlt";
        public bool CanToggle => Installed || Enabled || Available;

        public override string ToString()
        {
            var client = Plugin.RequiresClientMod ? " + client" : "";
            return $"{Name} [{Status}{client}]";
        }

        private static (string name, string description) SplitLabel(string label)
        {
            var parts = label.Split(" - ", 2, StringSplitOptions.None);
            return parts.Length == 2 ? (parts[0], parts[1]) : (label, "");
        }
    }

    private sealed record PluginSettingEditor(
        string RootPath,
        PluginConfigDefinition Definition,
        PluginSettingDefinition Setting,
        Control Editor);
}
