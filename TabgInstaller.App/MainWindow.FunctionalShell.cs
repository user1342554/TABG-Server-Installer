using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TabgInstaller.App.Models;
using TabgInstaller.App.Services;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;

namespace TabgInstaller.App;

public sealed partial class MainWindow
{
    private enum FunctionalPage
    {
        Setup,
        Overview,
        Configuration,
        Extensions,
        Backups,
        Diagnostics,
        Reference,
        Settings,
    }

    private readonly ContentControl _functionalPageHost = new();
    private readonly Dictionary<FunctionalPage, Control> _functionalPages = new();
    private readonly Dictionary<FunctionalPage, Button> _functionalNavButtons = new();
    private readonly Grid _quickPlayPageHost = new();
    private readonly Grid _advancedPageHost = new();
    private TabControl? _advancedTabs;
    private FunctionalPage _currentFunctionalPage = FunctionalPage.Overview;

    private readonly TextBlock _sidebarServerName = new() { Text = "TABG Server" };
    private readonly TextBlock _sidebarServerState = new() { Text = "Status wird geprüft…", FontSize = 12 };
    private readonly ComboBox _serverProfileSelector = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        PlaceholderText = "Server auswählen",
    };
    private readonly TextBlock _dashboardTitle = new() { FontSize = 30, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _dashboardDescription = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _dashboardStatus = new() { FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _dashboardFiles = new();
    private readonly TextBlock _dashboardModLoader = new();
    private readonly TextBlock _dashboardPlugins = new();
    private readonly TextBlock _dashboardBackup = new();
    private readonly TextBlock _dashboardActivity = new() { Text = "Noch keine Aktivität in dieser Sitzung.", TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _quickPlayPreset = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        PlaceholderText = "Spielmodus auswählen",
    };
    private readonly TextBlock _quickPlayPresetDescription = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _quickPlayPresetNotes = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _quickPlayStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _quickPlayServerState = new();
    private readonly TextBlock _quickPlayClientState = new();
    private readonly ProgressBar _quickPlayProgress = new() { IsIndeterminate = true, IsVisible = false, Height = 5 };
    private Button? _dashboardPrimary;
    private Button? _dashboardStop;
    private Button? _dashboardConfigure;
    private Button? _advancedSettingsButton;
    private Control? _advancedNavigation;
    private bool _advancedSettingsVisible;
    private bool _quickPlayBusy;

    private readonly ContentControl _setupStepHost = new();
    private readonly TextBlock _setupProgressText = new() { FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _setupFeedback = new() { TextWrapping = TextWrapping.Wrap };
    private Button? _setupBack;
    private Button? _setupNext;
    private Button? _setupCancel;
    private int _setupStep;
    private string _setupMode = "existing";
    private string _functionalOperationStatus = string.Empty;
    private bool _functionalUiBuilt;
    private bool _updatingServerProfileSelector;
    private bool _addingServerProfile;

    private static readonly IBrush CanvasBrush = Brush.Parse("#0B1016");
    private static readonly IBrush SurfaceBrush = Brush.Parse("#121A23");
    private static readonly IBrush SurfaceRaisedBrush = Brush.Parse("#192432");
    private static readonly IBrush BorderBrush = Brush.Parse("#2B3A49");
    private static readonly IBrush MutedBrush = Brush.Parse("#93A4B7");
    private static readonly IBrush AccentBrush = Brush.Parse("#2387E8");
    private static readonly IBrush SuccessBrush = Brush.Parse("#45CF8A");
    private static readonly IBrush WarningBrush = Brush.Parse("#F3C654");
    private static readonly IBrush DangerBrush = Brush.Parse("#FF777F");

    private void BuildFunctionalUi()
    {
        _status.Text = "Status wird geprüft…";
        _log.Height = 160;
        _serverProfileSelector.SelectionChanged += (_, _) => SwitchSelectedServerProfile();

        _advancedTabs = new TabControl();
        _advancedTabs.Items.Add(new TabItem { Header = "Setup", Content = BuildSetupTab() });
        _advancedTabs.Items.Add(new TabItem { Header = "Server", Content = BuildServerWorkspaceTab() });
        _advancedTabs.Items.Add(new TabItem { Header = "Config", Content = BuildConfigTab() });
        _advancedTabs.Items.Add(new TabItem { Header = "Mods", Content = BuildModsTab() });
        _advancedTabs.Items.Add(new TabItem { Header = "Reference", Content = BuildReferenceTab() });
        _advancedTabs.Items.Add(new TabItem { Header = "Settings", Content = BuildSettingsTab() });

        _quickPlayPageHost.Children.Add(BuildFunctionalDashboardPage());
        _advancedPageHost.RowDefinitions = new RowDefinitions("*,Auto");
        _advancedPageHost.Children.Add(_advancedTabs);
        _advancedPageHost.Children.Add(PutRow(_log, 1));
        _advancedPageHost.IsVisible = false;

        _advancedSettingsButton = Button("Erweiterte Einstellungen", () =>
            SetAdvancedSettingsVisible(!_advancedSettingsVisible));
        _dashboardConfigure = _advancedSettingsButton;
        var statusBar = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, 6),
            Children = { DockRight(_advancedSettingsButton), _status },
        };

        var pageHost = new Grid
        {
            Children = { _quickPlayPageHost, _advancedPageHost },
        };
        var root = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(10),
            Children = { DockTop(statusBar), pageHost },
        };

        Content = root;
        _functionalUiBuilt = true;
        RefreshServerProfileSelector();
        SetAdvancedSettingsVisible(false);

        _serverPath.TextChanged += (_, _) => RefreshFunctionalUi();
    }

    private Control BuildFunctionalSidebar()
    {
        _sidebarServerName.FontWeight = FontWeight.SemiBold;
        _sidebarServerName.TextTrimming = TextTrimming.CharacterEllipsis;
        _sidebarServerState.Foreground = MutedBrush;

        _serverProfileSelector.SelectionChanged += (_, _) => SwitchSelectedServerProfile();

        var serverCard = new Border
        {
            Background = SurfaceRaisedBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 14),
            Child = new StackPanel
            {
                Spacing = 7,
                Children =
                {
                    new TextBlock { Text = "AKTIVER SERVER", Foreground = MutedBrush, FontSize = 10, FontWeight = FontWeight.Bold },
                    _serverProfileSelector,
                    _sidebarServerState,
                    LinkButton("＋ Server hinzufügen", BeginAddingServerProfile),
                },
            },
        };

        var nav = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
        nav.Children.Add(NavigationButton("Konfiguration", FunctionalPage.Configuration));
        nav.Children.Add(NavigationButton("Erweiterungen", FunctionalPage.Extensions));
        nav.Children.Add(NavigationButton("Sicherungen", FunctionalPage.Backups));
        nav.Children.Add(new TextBlock
        {
            Text = "ERWEITERT",
            Foreground = MutedBrush,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(10, 18, 0, 5),
        });
        nav.Children.Add(NavigationButton("Diagnose & Konsole", FunctionalPage.Diagnostics));
        nav.Children.Add(NavigationButton("Referenz", FunctionalPage.Reference));
        nav.Children.Add(NavigationButton("Einrichtung / Reparatur", FunctionalPage.Setup));
        nav.Children.Add(NavigationButton("Einstellungen", FunctionalPage.Settings));

        _advancedNavigation = new ScrollViewer
        {
            Content = nav,
            IsVisible = false,
        };
        _advancedSettingsButton = new Button
        {
            Content = "⚙  Erweiterte Einstellungen",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 10),
            Background = SurfaceRaisedBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            FontWeight = FontWeight.SemiBold,
        };
        _advancedSettingsButton.Click += (_, _) => SetAdvancedSettingsVisible(!_advancedSettingsVisible);

        return new Border
        {
            Background = Brush.Parse("#0E151D"),
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(14),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = "TABG SERVER",
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(8, 5, 8, 16),
                    },
                    PutRow(serverCard, 1),
                    PutRow(_advancedSettingsButton, 2),
                    PutRow(_advancedNavigation, 3),
                    PutRow(new TextBlock
                    {
                        Text = "Server Installer · v0.2",
                        Foreground = MutedBrush,
                        FontSize = 11,
                        Margin = new Thickness(8, 12, 8, 4),
                    }, 4),
                },
            },
        };
    }

    private void SetAdvancedSettingsVisible(bool visible)
    {
        _advancedSettingsVisible = visible;
        _quickPlayPageHost.IsVisible = !visible;
        _advancedPageHost.IsVisible = visible;
        if (_advancedSettingsButton != null)
            _advancedSettingsButton.Content = visible
                ? "←  Zurück zum Spielen"
                : "Erweiterte Einstellungen";
        if (!visible)
            _currentFunctionalPage = FunctionalPage.Overview;
    }

    private Button NavigationButton(string text, FunctionalPage page)
    {
        var button = new Button
        {
            Content = text,
            Tag = page,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 9),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Foreground = MutedBrush,
        };
        button.Click += (_, _) => NavigateFunctional(page);
        _functionalNavButtons[page] = button;
        return button;
    }

    private void NavigateFunctional(FunctionalPage page)
    {
        if (!_functionalUiBuilt)
            return;

        if (page == FunctionalPage.Overview)
        {
            SetAdvancedSettingsVisible(false);
            RefreshFunctionalUi();
            return;
        }

        SetAdvancedSettingsVisible(true);
        _currentFunctionalPage = page;
        if (_advancedTabs != null)
        {
            _advancedTabs.SelectedIndex = page switch
            {
                FunctionalPage.Setup => 0,
                FunctionalPage.Configuration => 2,
                FunctionalPage.Extensions => 3,
                FunctionalPage.Reference => 4,
                FunctionalPage.Settings => 5,
                _ => 1,
            };

            if (_advancedTabs.SelectedItem is TabItem { Content: TabControl nested })
            {
                if (page == FunctionalPage.Backups)
                    nested.SelectedIndex = 2;
                else if (page == FunctionalPage.Diagnostics)
                    nested.SelectedIndex = 1;
            }
        }

        if (page == FunctionalPage.Backups)
            RefreshBackups();
        else if (page == FunctionalPage.Settings)
            RefreshSettingsSummary();
    }

    private Control BuildFunctionalDashboardPage()
    {
        _quickPlayPreset.ItemsSource = BuiltInPresets.All
            .Select(preset => new QuickPlayPresetOption(preset))
            .ToArray();
        _quickPlayPreset.SelectionChanged += (_, _) => UpdateQuickPlayPresetDetails();
        _quickPlayPreset.SelectedIndex = BuiltInPresets.All.Count > 0 ? 0 : -1;
        _quickPlayPreset.Width = 560;
        _serverProfileSelector.Width = 360;

        _dashboardPrimary = Button("Play", RunQuickPlayAsync);
        _dashboardPrimary.MinWidth = 140;
        _dashboardStop = Button("Server stoppen", () =>
        {
            _serverProcess.Stop();
            SetStatus("Server wird gestoppt…");
            RefreshFunctionalUi();
        });

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                Children =
                {
                    new TextBlock { Text = "Quick Play", FontSize = 20 },
                    new TextBlock
                    {
                        Text = "Preset auswählen und Play drücken. Der Launcher konfiguriert Server und Client und startet beide.",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            Label("Server"),
                            _serverProfileSelector,
                            Button("Server hinzufügen", BeginAddingServerProfile),
                        },
                    },
                    Label("Preset"),
                    _quickPlayPreset,
                    _quickPlayPresetDescription,
                    _quickPlayPresetNotes,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { _dashboardPrimary, _dashboardStop },
                    },
                    _quickPlayProgress,
                    new TextBlock { Text = "Server:" },
                    _quickPlayServerState,
                    new TextBlock { Text = "Client:" },
                    _quickPlayClientState,
                    _quickPlayStatus,
                },
            },
        };
    }

    private void UpdateQuickPlayPresetDetails()
    {
        var preset = SelectedQuickPlayPreset();
        if (preset == null)
        {
            _quickPlayPresetDescription.Text = "Wähle ein Preset für deinen Server.";
            _quickPlayPresetNotes.Text = string.Empty;
            return;
        }

        _quickPlayPresetDescription.Text = preset.Description;
        _quickPlayPresetNotes.Text = preset.Notes;
    }

    private async Task RunQuickPlayAsync()
    {
        if (_quickPlayBusy)
            return;
        var preset = SelectedQuickPlayPreset();
        if (preset == null)
        {
            SetQuickPlayMessage("Wähle zuerst ein Preset.");
            return;
        }
        if (IsTrackedClientRunning())
        {
            SetQuickPlayMessage("Der modifizierte Client läuft bereits. Schließe ihn vor einem Preset-Wechsel.");
            return;
        }

        _quickPlayBusy = true;
        RefreshFunctionalUi();
        try
        {
            SetQuickPlayStage("Server wird geprüft…");
            if (_serverProcess.IsRunning)
            {
                _serverProcess.Stop();
                if (_serverProcess.IsRunning)
                    throw new InvalidOperationException("Der laufende Server konnte nicht gestoppt werden.");
            }

            var serverDir = _serverPath.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(serverDir))
            {
                CreateServerFolder();
                serverDir = _serverPath.Text?.Trim() ?? string.Empty;
            }

            if (!ServerUiStateEvaluator.HasServerExecutable(serverDir))
            {
                SetQuickPlayStage("TABG Dedicated Server wird installiert…");
                if (!await InstallOrUpdateDedicatedServerCoreAsync())
                    throw new InvalidOperationException("Die Serverdateien konnten nicht installiert werden. Details findest du unter Diagnose.");
            }

            if (!Directory.Exists(Path.Combine(serverDir, "BepInEx")))
            {
                SelectQuickPlayServerPlugins(preset);
                SetQuickPlayStage("Server und Erweiterungen werden vorbereitet…");
                if (!await InstallServerCoreAsync())
                    throw new InvalidOperationException("Der Server konnte nicht vorbereitet werden. Details findest du unter Diagnose.");
            }

            SaveQuickPlayRecoveryPreset(serverDir);
            SetQuickPlayStage($"Preset „{preset.Name}“ wird angewendet…");
            BuiltInPresets.Deploy(preset, serverDir);
            EnsurePresetPluginsExist(serverDir, preset.RequiredPlugins, "Server");
            Log("Quick Play applied preset: " + preset.Name);

            EnsureDetectedClientPaths();
            var sourceDir = _clientPath.Text?.Trim() ?? string.Empty;
            var moddedDir = _moddedClientPath.Text?.Trim() ?? string.Empty;
            var clientPlugins = QuickPlayClientPlugins(preset).ToList();
            if (ClientNeedsPreparation(moddedDir, clientPlugins))
            {
                if (!HasClientExecutable(sourceDir))
                    throw new DirectoryNotFoundException("TABG wurde in Steam nicht gefunden. Wähle den TABG-Ordner unter Erweiterte Einstellungen > Erweiterungen.");

                SetQuickPlayStage("Modifizierter Client wird automatisch vorbereitet…");
                var installed = await ClientModInstaller.InstallAsync(
                    sourceDir,
                    moddedDir,
                    clientPlugins,
                    new Progress<string>(Log));
                if (!installed)
                    throw new InvalidOperationException("Der modifizierte Client konnte nicht vorbereitet werden. Details findest du unter Diagnose.");
            }

            BuiltInPresets.ReconcileClientPlugins(preset, moddedDir);
            EnsurePresetPluginsExist(
                moddedDir,
                preset.RequiredClientPlugins ?? Array.Empty<string>(),
                "Client");
            RefreshClientModLists();

            SetQuickPlayStage("Server wird gestartet…");
            if (!TryStartServer("-batchmode -nographics -nolog"))
                throw new InvalidOperationException("Der Server konnte nicht gestartet werden. Details findest du unter Diagnose.");

            await Task.Delay(900);
            SetQuickPlayStage("Client wird gestartet…");
            if (!StartModdedClientCore())
                throw new InvalidOperationException("Der modifizierte Client konnte nicht gestartet werden. Details findest du unter Diagnose.");

            RegisterCurrentServerProfileIfReady();
            ReloadFunctionalServerData();
            _functionalOperationStatus = "Spiel gestartet";
            SetQuickPlayMessage($"✓ {preset.Name} ist eingerichtet. Server und Client laufen.");
            Log("Quick Play completed successfully.");
        }
        catch (Exception ex)
        {
            Log("Quick Play failed: " + ex.Message);
            _functionalOperationStatus = "Quick Play fehlgeschlagen";
            SetQuickPlayMessage(ex.Message);
        }
        finally
        {
            _quickPlayBusy = false;
            RefreshFunctionalUi();
        }
    }

    private BuiltInPresets.BuiltInPreset? SelectedQuickPlayPreset()
        => (_quickPlayPreset.SelectedItem as QuickPlayPresetOption)?.Preset;

    private void SetQuickPlayStage(string message)
    {
        _functionalOperationStatus = message;
        SetQuickPlayMessage(message);
        RefreshFunctionalUi();
    }

    private void SetQuickPlayMessage(string message)
    {
        _quickPlayStatus.Text = message;
    }

    private void SelectQuickPlayServerPlugins(BuiltInPresets.BuiltInPreset preset)
    {
        var disabled = new HashSet<string>(
            preset.DisabledServerPlugins ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var required = new HashSet<string>(preset.RequiredPlugins, StringComparer.OrdinalIgnoreCase);

        foreach (var checkBox in _pluginChecks.Children.OfType<CheckBox>())
        {
            if (checkBox.Tag is not PluginDefinition plugin)
                continue;

            checkBox.IsChecked = plugin.DllNames.Any(required.Contains) ||
                plugin.DefaultChecked && !plugin.DllNames.Any(disabled.Contains);
        }
    }

    private void SaveQuickPlayRecoveryPreset(string serverDir)
    {
        if (!File.Exists(Path.Combine(serverDir, "game_settings.txt")))
            return;

        var paths = PresetManager.DefaultConfigRelativePaths
            .Concat(BuiltInPresets.All.SelectMany(item => item.Files.Keys))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        PresetManager.SavePreset(serverDir, "Schnellstart-Sicherung", paths);
        Log("Saved the previous configuration as preset: Schnellstart-Sicherung");
    }

    private void EnsureDetectedClientPaths()
    {
        if (!HasClientExecutable(_clientPath.Text))
            DetectClientPath();

        if (string.IsNullOrWhiteSpace(_moddedClientPath.Text) && !string.IsNullOrWhiteSpace(_clientPath.Text))
        {
            var sourceDir = _clientPath.Text!.Trim();
            _moddedClientPath.Text = Path.Combine(
                Path.GetDirectoryName(sourceDir) ?? sourceDir,
                "TotallyAccurateBattlegrounds-Modded");
        }
    }

    private static bool HasClientExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        return new[]
        {
            "TotallyAccurateBattlegrounds.x86_64",
            "TotallyAccurateBattlegrounds.exe",
            "TABG.x86_64",
            "TABG.exe",
            "TABG_Launcher.exe",
        }.Any(name => File.Exists(Path.Combine(path, name)));
    }

    private bool IsTrackedClientRunning()
    {
        try
        {
            return _clientProcess?.HasExited == false;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> QuickPlayClientPlugins(BuiltInPresets.BuiltInPreset preset)
    {
        var disabled = new HashSet<string>(
            preset.DisabledClientPlugins ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        return PluginRegistry.ClientMods
            .Where(plugin => plugin.DefaultChecked)
            .SelectMany(plugin => plugin.DllNames)
            .Concat(preset.RequiredClientPlugins ?? Array.Empty<string>())
            .Where(dll => !disabled.Contains(dll))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool ClientNeedsPreparation(string clientDir, IEnumerable<string> plugins)
    {
        if (string.IsNullOrWhiteSpace(clientDir) ||
            ResolveClientLaunchTarget(clientDir) == null ||
            !File.Exists(Path.Combine(clientDir, "BepInEx", "core", "BepInEx.dll")))
        {
            return true;
        }

        var pluginDir = Path.Combine(clientDir, "BepInEx", "plugins");
        return plugins.Any(dll => !File.Exists(Path.Combine(pluginDir, dll)));
    }

    private static void EnsurePresetPluginsExist(
        string rootDir,
        IEnumerable<string> requiredPlugins,
        string component)
    {
        var pluginDir = Path.Combine(rootDir, "BepInEx", "plugins");
        var missing = requiredPlugins
            .Where(dll => !File.Exists(Path.Combine(pluginDir, dll)))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new FileNotFoundException(
                $"{component}-Erweiterung fehlt: {string.Join(", ", missing)}. Öffne Diagnose für Details.");
        }
    }

    private Control BuildFunctionalSetupPage()
    {
        _setupProgressText.Foreground = MutedBrush;
        _setupFeedback.Foreground = MutedBrush;
        _setupBack = SecondaryButton("Zurück");
        _setupNext = PrimaryButton("Weiter");
        _setupCancel = SecondaryButton("Vorgang abbrechen");
        _setupCancel.IsVisible = false;

        _setupBack.Click += (_, _) =>
        {
            if (_setupStep > 0)
            {
                _setupStep--;
                RenderSetupStep();
            }
        };
        _setupNext.Click += async (_, _) => await AdvanceSetupAsync();
        _setupCancel.Click += (_, _) => _installCts?.Cancel();

        var footer = new DockPanel
        {
            Margin = new Thickness(0, 18, 0, 0),
            LastChildFill = false,
            Children =
            {
                DockLeft(_setupFeedback),
                DockRight(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { _setupCancel, _setupBack, _setupNext },
                }),
            },
        };

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children = { _setupProgressText, PutRow(_setupStepHost, 1), PutRow(footer, 2) },
        };

        var page = BuildFunctionalPage(
            "Server einrichten",
            "Wir prüfen jeden Schritt, bevor Dateien verändert werden.",
            body);
        RenderSetupStep();
        return page;
    }

    private void RenderSetupStep()
    {
        if (_setupStepHost == null)
            return;

        _setupStepHost.Content = null;
        _setupFeedback.Text = string.Empty;
        _setupFeedback.Foreground = MutedBrush;
        _setupProgressText.Text = $"Schritt {_setupStep + 1} von 5  ·  {SetupStepName(_setupStep)}";
        _setupBack!.IsVisible = _setupStep > 0 && _setupStep < 4;
        _setupNext!.IsVisible = _setupStep != 0;
        _setupNext.IsEnabled = _installCts == null;
        _setupCancel!.IsVisible = _installCts != null;
        _setupNext.Content = _setupStep switch
        {
            3 => "Installieren und vorbereiten",
            4 => "Zur Übersicht",
            _ => "Weiter",
        };

        _setupStepHost.Content = _setupStep switch
        {
            0 => BuildSetupModeStep(),
            1 => BuildSetupPathStep(),
            2 => BuildSetupProfileStep(),
            3 => BuildSetupReviewStep(),
            _ => BuildSetupCompleteStep(),
        };
    }

    private Control BuildSetupModeStep()
    {
        var existing = SetupChoice(
            "Vorhandenen Server verwalten",
            "Einen erkannten oder manuell ausgewählten TABG Dedicated Server prüfen und sicher übernehmen.",
            () =>
            {
                _setupMode = "existing";
                _setupStep = 1;
                RenderSetupStep();
            });
        var create = SetupChoice(
            "Neuen Server installieren",
            "Serverdateien mit SteamCMD installieren und anschließend BepInEx sowie empfohlene Erweiterungen vorbereiten.",
            () =>
            {
                _setupMode = "new";
                _setupStep = 1;
                RenderSetupStep();
            });

        return new StackPanel
        {
            MaxWidth = 780,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 25, 0, 0),
            Spacing = 12,
            Children =
            {
                SectionTitle("Was möchtest du tun?"),
                MutedText("Wähle dein Ziel. Technische Einzelschritte werden danach automatisch in die richtige Reihenfolge gebracht."),
                existing,
                create,
                MutedText("Remote/SSH ist in dieser Oberfläche noch nicht verfügbar."),
            },
        };
    }

    private Control BuildSetupPathStep()
    {
        DetachFromParent(_serverPath);
        DetachFromParent(_steamUser);
        DetachFromParent(_steamPassword);
        DetachFromParent(_steamGuard);

        var browse = SecondaryButton("Ordner wählen");
        browse.Click += async (_, _) =>
        {
            if (await PickFolderInto(_serverPath, "TABG-Serverordner auswählen"))
            {
                RefreshServerModLists();
                RefreshSetupPathFeedback();
                RefreshFunctionalUi();
            }
        };

        var detect = SecondaryButton("Automatisch suchen");
        detect.Click += (_, _) =>
        {
            DetectServerPath();
            RefreshSetupPathFeedback();
        };

        var create = SecondaryButton("Ordner anlegen");
        create.Click += (_, _) =>
        {
            CreateServerFolder();
            RefreshSetupPathFeedback();
        };

        var pathGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children = { _serverPath, PutColumn(browse, 1) },
        };
        _serverPath.MinWidth = 0;

        var advancedLogin = new Expander
        {
            Header = "Erweiterte Steam-Anmeldung",
            IsExpanded = false,
            Content = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(0, 8, 0, 0),
                Children =
                {
                    MutedText("Standardmäßig wird SteamCMD anonym verwendet. Trage ein Konto nur ein, wenn Steam den Zugriff ablehnt."),
                    FieldRow("Steam-Benutzer", _steamUser),
                    FieldRow("Passwort", _steamPassword),
                    FieldRow("Steam Guard", _steamGuard),
                },
            },
        };

        var panel = new StackPanel
        {
            MaxWidth = 840,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 24, 0, 0),
            Spacing = 12,
            Children =
            {
                SectionTitle(_setupMode == "new" ? "Installationsordner wählen" : "Vorhandenen Server auswählen"),
                MutedText(_setupMode == "new"
                    ? "Der Ordner darf leer sein. Wir legen ihn bei Bedarf an und installieren anschließend die Serverdateien."
                    : "Der Ordner muss einen startbaren TABG Dedicated Server enthalten."),
                pathGrid,
                new WrapPanel { Children = { detect, create } },
                advancedLogin,
            },
        };

        RefreshSetupPathFeedback();
        return panel;
    }

    private Control BuildSetupProfileStep()
    {
        DetachFromParent(_pluginChecks);
        RebuildPluginChecks();

        var recommended = SecondaryButton("Empfohlen");
        recommended.Click += (_, _) => RebuildPluginChecks();
        var minimal = SecondaryButton("Minimal");
        minimal.Click += (_, _) => SetChecks(_pluginChecks, false);
        var all = SecondaryButton("Alle verfügbaren");
        all.Click += (_, _) => SetChecks(_pluginChecks, true);

        return new StackPanel
        {
            MaxWidth = 860,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 24, 0, 0),
            Spacing = 12,
            Children =
            {
                SectionTitle("Serverprofil und Erweiterungen"),
                MutedText("„Empfohlen“ ist der sichere Standard. Die Auswahl kann später jederzeit unter Erweiterungen geändert werden."),
                new WrapPanel { Children = { recommended, minimal, all } },
                new Expander
                {
                    Header = "Auswahl anpassen",
                    IsExpanded = true,
                    Content = new ScrollViewer
                    {
                        MaxHeight = 360,
                        Content = new Border
                        {
                            Margin = new Thickness(0, 8, 0, 0),
                            Padding = new Thickness(12),
                            Background = SurfaceBrush,
                            CornerRadius = new CornerRadius(8),
                            Child = _pluginChecks,
                        },
                    },
                },
            },
        };
    }

    private Control BuildSetupReviewStep()
    {
        DetachFromParent(_progress);
        var state = CurrentServerUiState();
        var selected = SelectedDefinitions(_pluginChecks).ToArray();
        var operations = new StackPanel { Spacing = 9 };
        operations.Children.Add(CheckLine(
            _setupMode == "new" || !state.ServerExecutableExists,
            "TABG Dedicated Server mit SteamCMD installieren/aktualisieren"));
        operations.Children.Add(CheckLine(true, "Sicherheitskopie vorhandener Dateien erstellen"));
        operations.Children.Add(CheckLine(true, "BepInEx und Kernkomponenten prüfen/installieren"));
        operations.Children.Add(CheckLine(true, $"{selected.Length} ausgewählte Erweiterungen installieren"));
        operations.Children.Add(CheckLine(true, "Startbereitschaft nach Abschluss erneut prüfen"));

        return new StackPanel
        {
            MaxWidth = 820,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 24, 0, 0),
            Spacing = 13,
            Children =
            {
                SectionTitle("Änderungen prüfen"),
                MutedText("Erst mit der nächsten Schaltfläche werden Dateien verändert."),
                SummaryCard("Serverordner", _serverPath.Text ?? "–"),
                SummaryCard("Einrichtungsart", _setupMode == "new" ? "Neuer Server" : "Vorhandener Server"),
                new Border
                {
                    Background = SurfaceBrush,
                    BorderBrush = BorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(15),
                    Child = operations,
                },
                _progress,
                MutedText("Bei einem Fehler bleiben die technischen Details unter Diagnose erhalten. Das Steam-Passwort wird nach dem Vorgang aus dem Formular entfernt."),
            },
        };
    }

    private Control BuildSetupCompleteStep()
    {
        return new StackPanel
        {
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 42, 0, 0),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "✓", FontSize = 46, Foreground = SuccessBrush },
                SectionTitle("Der Server ist startbereit"),
                MutedText("Serverdateien, BepInEx und die ausgewählten Erweiterungen wurden geprüft. Auf der Übersicht kannst du den Server starten oder zuerst die Spielregeln anpassen."),
                new WrapPanel
                {
                    Children =
                    {
                        LinkButton("Konfiguration öffnen", () => NavigateFunctional(FunctionalPage.Configuration)),
                        LinkButton("Erweiterungen prüfen", () => NavigateFunctional(FunctionalPage.Extensions)),
                    },
                },
            },
        };
    }

    private async Task AdvanceSetupAsync()
    {
        if (_setupStep == 1)
        {
            var path = _serverPath.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                SetSetupError("Wähle zuerst einen Serverordner.");
                return;
            }

            if (_setupMode == "new")
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    SetSetupError("Der Ordner konnte nicht angelegt werden: " + ex.Message);
                    return;
                }
            }
            else if (!ServerUiStateEvaluator.HasServerExecutable(path))
            {
                SetSetupError("In diesem Ordner wurde kein TABG Dedicated Server gefunden. Wähle einen anderen Ordner oder gehe zurück und installiere einen neuen Server.");
                return;
            }
        }

        if (_setupStep == 3)
        {
            _setupNext!.IsEnabled = false;
            _setupBack!.IsEnabled = false;
            _setupCancel!.IsVisible = true;
            _setupFeedback.Foreground = MutedBrush;
            _setupFeedback.Text = "Installation läuft…";

            var ok = await RunGuidedSetupAsync();
            _setupBack.IsEnabled = true;
            _setupCancel.IsVisible = false;
            if (!ok)
            {
                _setupNext.IsEnabled = true;
                SetSetupError("Die Einrichtung wurde nicht abgeschlossen. Öffne Diagnose für Details oder versuche den Schritt erneut.");
                RefreshFunctionalUi();
                return;
            }

            _setupStep = 4;
            RegisterCurrentServerProfileIfReady();
            ReloadFunctionalServerData();
            RefreshFunctionalUi();
            RenderSetupStep();
            return;
        }

        if (_setupStep == 4)
        {
            NavigateFunctional(FunctionalPage.Overview);
            return;
        }

        _setupStep = Math.Min(4, _setupStep + 1);
        RenderSetupStep();
    }

    private async Task<bool> RunGuidedSetupAsync()
    {
        var state = CurrentServerUiState();
        if (_setupMode == "new" || !state.ServerExecutableExists)
        {
            if (!await InstallOrUpdateDedicatedServerCoreAsync())
                return false;
        }

        return await InstallServerCoreAsync();
    }

    private void RefreshSetupPathFeedback()
    {
        if (_setupFeedback == null)
            return;

        var path = _serverPath.Text?.Trim() ?? string.Empty;
        var exists = Directory.Exists(path);
        var hasExe = exists && ServerUiStateEvaluator.HasServerExecutable(path);
        if (_setupMode == "new")
        {
            _setupFeedback.Foreground = SuccessBrush;
            _setupFeedback.Text = exists
                ? "✓ Ordner ist vorhanden und kann verwendet werden."
                : "Der Ordner wird beim Fortfahren angelegt.";
        }
        else if (hasExe)
        {
            _setupFeedback.Foreground = SuccessBrush;
            _setupFeedback.Text = "✓ Startbare TABG-Serverdateien wurden gefunden.";
        }
        else
        {
            _setupFeedback.Foreground = WarningBrush;
            _setupFeedback.Text = exists
                ? "In diesem Ordner wurden noch keine TABG-Serverdateien gefunden."
                : "Der ausgewählte Ordner existiert nicht.";
        }
    }

    private void SetSetupError(string message)
    {
        _setupFeedback.Foreground = DangerBrush;
        _setupFeedback.Text = message;
    }

    private Control BuildFunctionalConfigurationPage()
    {
        return BuildFunctionalPage(
            "Konfiguration",
            "Passe Spielmodus, Rundenregeln, Welt, Loadouts, Admins und Mod-Einstellungen an.",
            BuildConfigTab());
    }

    private Control BuildFunctionalExtensionsPage()
    {
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Server-Erweiterungen", Content = BuildServerModsTab() },
                new TabItem { Header = "Modifizierter Client (optional)", Content = BuildFunctionalClientExtensionsPage() },
            },
        };
        return BuildFunctionalPage(
            "Erweiterungen",
            "Verwalte Funktionen und Abhängigkeiten. Direkte DLL-Werkzeuge bleiben im aufklappbaren Expertenbereich.",
            tabs);
    }

    private Control BuildFunctionalClientExtensionsPage()
    {
        return new TabControl
        {
            Items =
            {
                new TabItem { Header = "Client einrichten", Content = BuildClientTab() },
                new TabItem { Header = "Client-Erweiterungen", Content = BuildClientModsTab() },
            },
        };
    }

    private Control BuildFunctionalDiagnosticsPage()
    {
        var start = SecondaryButton("Server starten");
        start.Click += (_, _) => StartServer("-batchmode -nographics -nolog");
        var stop = SecondaryButton("Server stoppen");
        stop.Click += (_, _) => _serverProcess.Stop();
        var restart = SecondaryButton("Sicher neu starten");
        restart.Click += (_, _) => QuickRestartServer();
        var clear = SecondaryButton("Ansicht leeren");
        clear.Click += (_, _) => _log.Text = string.Empty;
        var export = PrimaryButton("Protokoll exportieren");
        export.Click += (_, _) => ExportVisibleLog();
        var find = SecondaryButton("Suchen");
        find.Click += (_, _) => FindInVisibleLog();

        var toolbar = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 10),
            Children = { start, stop, restart, clear, export },
        };
        var search = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,260,Auto,*"),
            ColumnSpacing = 8,
            Margin = new Thickness(0, 0, 0, 10),
            Children =
            {
                new TextBlock { Text = "Protokoll durchsuchen", VerticalAlignment = VerticalAlignment.Center },
                PutColumn(_consoleSearch, 1),
                PutColumn(find, 2),
                PutColumn(MutedText("Die Kommandoeingabe bleibt ausgeblendet, solange der Serverprozess kein zuverlässiges stdin unterstützt."), 3),
            },
        };

        var logPanel = new Border
        {
            Background = Brush.Parse("#090B0E"),
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Child = _log,
        };

        return BuildFunctionalPage(
            "Diagnose & Konsole",
            "Technische Details für Fehlersuche und Support. Im normalen Betrieb bleibt dieser Bereich verborgen.",
            new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,*"),
                Children = { toolbar, PutRow(search, 1), PutRow(logPanel, 2) },
            });
    }

    private Control BuildFunctionalPage(string title, string subtitle, Control body)
    {
        var heading = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 18),
            Children =
            {
                new TextBlock { Text = title, FontSize = 28, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = subtitle, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap },
            },
        };

        return new Grid
        {
            Margin = new Thickness(24),
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { heading, PutRow(body, 1) },
        };
    }

    private void ShowInitialFunctionalPage()
    {
        if (!_functionalUiBuilt)
            return;

        ReloadFunctionalServerData();
        var state = CurrentServerUiState();
        _setupStep = state.PathExists ? 1 : 0;
        _setupMode = state.PathExists ? "existing" : "new";
        SetAdvancedSettingsVisible(false);
        NavigateFunctional(FunctionalPage.Overview);
    }

    private void RefreshFunctionalUi()
    {
        if (!_functionalUiBuilt)
            return;

        var state = CurrentServerUiState();
        var path = _serverPath.Text?.Trim() ?? string.Empty;
        _status.Text = _quickPlayBusy
            ? "Quick Play läuft…"
            : string.IsNullOrWhiteSpace(_functionalOperationStatus)
            ? state.Runtime switch
            {
                ServerRuntimeUiState.Running => "Server läuft",
                ServerRuntimeUiState.Busy => "Einrichtung läuft…",
                ServerRuntimeUiState.Stopped => "Bereit",
                _ => "Einrichtung erforderlich",
            }
            : _functionalOperationStatus;
        _sidebarServerName.Text = string.IsNullOrWhiteSpace(path)
            ? "Noch kein Server"
            : new DirectoryInfo(path).Name;
        _sidebarServerState.Text = state.Runtime switch
        {
            ServerRuntimeUiState.Running => "● Läuft",
            ServerRuntimeUiState.Busy => "● Beschäftigt",
            ServerRuntimeUiState.Stopped => "● Bereit · gestoppt",
            _ => "● Einrichtung nötig",
        };
        _sidebarServerState.Foreground = state.Runtime switch
        {
            ServerRuntimeUiState.Running => SuccessBrush,
            ServerRuntimeUiState.Busy => WarningBrush,
            ServerRuntimeUiState.SetupRequired => DangerBrush,
            _ => MutedBrush,
        };
        ToolTip.SetTip(_serverProfileSelector, string.IsNullOrWhiteSpace(path) ? "Noch kein Server ausgewählt" : path);

        _dashboardTitle.Text = state.Title;
        _dashboardDescription.Text = state.Description;
        _dashboardStatus.Text = state.Runtime switch
        {
            ServerRuntimeUiState.Running => "● SERVER LÄUFT",
            ServerRuntimeUiState.Busy => "● VORGANG LÄUFT",
            ServerRuntimeUiState.Stopped => "● BEREIT",
            _ => "● AUFMERKSAMKEIT NÖTIG",
        };
        _dashboardStatus.Foreground = state.NeedsAttention
            ? DangerBrush
            : state.Runtime == ServerRuntimeUiState.Busy
                ? WarningBrush
                : SuccessBrush;

        if (_dashboardPrimary != null)
        {
            var clientRunning = IsTrackedClientRunning();
            _dashboardPrimary.Content = _quickPlayBusy
                ? "WIRD VORBEREITET…"
                : clientRunning && state.Runtime == ServerRuntimeUiState.Running
                    ? "SPIEL LÄUFT"
                    : "SPIELEN";
            _dashboardPrimary.IsEnabled = !_quickPlayBusy &&
                !clientRunning &&
                SelectedQuickPlayPreset() != null;
        }
        if (_dashboardStop != null)
            _dashboardStop.IsVisible = state.CanStop;
        if (_dashboardConfigure != null)
            _dashboardConfigure.IsEnabled = !_quickPlayBusy;

        _quickPlayProgress.IsVisible = _quickPlayBusy;
        _quickPlayPreset.IsEnabled = !_quickPlayBusy && !IsTrackedClientRunning();
        _quickPlayServerState.Text = state.Runtime switch
        {
            ServerRuntimeUiState.Running => "✓ Läuft",
            ServerRuntimeUiState.Busy => "Wird vorbereitet…",
            ServerRuntimeUiState.Stopped => "✓ Bereit",
            _ => "Wird beim ersten Play eingerichtet",
        };

        var moddedClientDir = _moddedClientPath.Text?.Trim() ?? string.Empty;
        var clientReady = ResolveClientLaunchTarget(moddedClientDir) != null &&
            File.Exists(Path.Combine(moddedClientDir, "BepInEx", "core", "BepInEx.dll"));
        _quickPlayClientState.Text = IsTrackedClientRunning()
            ? "✓ Läuft"
            : clientReady
                ? "✓ Bereit"
                : "Wird beim ersten Play eingerichtet";
        if (string.IsNullOrWhiteSpace(_quickPlayStatus.Text))
        {
            SetQuickPlayMessage(
                state.Readiness == ServerReadiness.Ready && clientReady
                    ? "Bereit. Wähle dein Preset und drücke Play."
                    : "Beim ersten Play werden fehlende Server- und Client-Komponenten automatisch vorbereitet.");
        }

        _dashboardFiles.Text = state.ServerExecutableExists ? "✓ Vollständig" : "! Fehlen";
        _dashboardFiles.Foreground = state.ServerExecutableExists ? SuccessBrush : DangerBrush;
        _dashboardModLoader.Text = state.ModLoaderExists ? "✓ BepInEx bereit" : "! Vorbereitung nötig";
        _dashboardModLoader.Foreground = state.ModLoaderExists ? SuccessBrush : WarningBrush;
        _dashboardPlugins.Text = state.InstalledPluginCount == 1
            ? "1 Plugin installiert"
            : $"{state.InstalledPluginCount} Plugins installiert";
        _dashboardPlugins.Foreground = state.InstalledPluginCount > 0 ? SuccessBrush : MutedBrush;
        _dashboardBackup.Text = LatestBackupDescription(path);
        _dashboardBackup.Foreground = MutedBrush;

        _serverProfileSelector.IsEnabled = !_serverProcess.IsRunning && _installCts == null && !_quickPlayBusy;
        RefreshServerProfileSelector();

        if (_functionalNavButtons.TryGetValue(FunctionalPage.Configuration, out var configuration))
            configuration.IsEnabled = state.CanConfigure && !_quickPlayBusy;
        if (_functionalNavButtons.TryGetValue(FunctionalPage.Extensions, out var extensions))
            extensions.IsEnabled = !_quickPlayBusy;
        if (_functionalNavButtons.TryGetValue(FunctionalPage.Backups, out var backups))
            backups.IsEnabled = state.PathExists && !_quickPlayBusy;
    }

    private ServerUiState CurrentServerUiState()
        => ServerUiStateEvaluator.Inspect(_serverPath.Text, _serverProcess.IsRunning, _installCts != null);

    private void ReloadFunctionalServerData()
    {
        if (!Directory.Exists(_serverPath.Text?.Trim()))
            return;

        LoadGameSettingsTyped();
        LoadStarterPackSettings();
        LoadRingSpawnSettings();
        LoadModSettings();
        LoadAdmins();
        RefreshPresetLists();
        RefreshBackups();
        RefreshServerModLists();
        RefreshClientModLists();
        RefreshSettingsSummary();
    }

    private bool LoadActiveServerProfile()
    {
        var active = _serverProfiles.ActiveProfile;
        if (active == null)
            return false;

        _serverPath.Text = active.ServerPath;
        _serverProfileName.Text = active.DisplayName;
        RefreshServerProfileSelector();
        Log($"Aktives Serverprofil geladen: {active.DisplayName} ({active.ServerPath})");
        return true;
    }

    private void RegisterCurrentServerProfileIfReady()
    {
        var path = _serverPath.Text?.Trim() ?? string.Empty;
        if (!ServerUiStateEvaluator.HasServerExecutable(path))
            return;

        var existing = _serverProfiles.Profiles.FirstOrDefault(profile => PathsReferToSameServer(profile.ServerPath, path));
        var displayName = existing?.DisplayName;
        var profile = _serverProfiles.AddOrUpdate(path, displayName);
        _addingServerProfile = false;
        _serverProfileName.Text = profile.DisplayName;
        RefreshServerProfileSelector();
    }

    private void RefreshServerProfileSelector()
    {
        if (!_functionalUiBuilt)
            return;

        var activeId = _serverProfiles.ActiveProfileId;
        var profiles = _serverProfiles.Profiles.ToArray();
        _updatingServerProfileSelector = true;
        _serverProfileSelector.ItemsSource = profiles;
        _serverProfileSelector.SelectedItem = _addingServerProfile
            ? null
            : profiles.FirstOrDefault(profile => profile.Id == activeId);
        _updatingServerProfileSelector = false;
    }

    private void SwitchSelectedServerProfile()
    {
        if (_updatingServerProfileSelector || _serverProfileSelector.SelectedItem is not LocalServerProfile profile)
            return;
        if (_serverProcess.IsRunning || _installCts != null)
        {
            RefreshServerProfileSelector();
            SetStatus("Serverwechsel ist während eines laufenden Vorgangs gesperrt.");
            return;
        }

        _serverProfiles.SetActive(profile.Id);
        _addingServerProfile = false;
        _serverPath.Text = profile.ServerPath;
        _serverProfileName.Text = profile.DisplayName;
        _functionalOperationStatus = string.Empty;
        ReloadFunctionalServerData();
        RefreshFunctionalUi();
        NavigateFunctional(FunctionalPage.Overview);
        Log($"Serverprofil gewechselt: {profile.DisplayName} ({profile.ServerPath})");
    }

    private void BeginAddingServerProfile()
    {
        if (_serverProcess.IsRunning || _installCts != null)
        {
            SetStatus("Stoppe zuerst den aktiven Server.");
            return;
        }

        _addingServerProfile = true;
        _serverPath.Text = string.Empty;
        _serverProfileName.Text = string.Empty;
        RefreshServerProfileSelector();
        _setupMode = "existing";
        _setupStep = 0;
        NavigateFunctional(FunctionalPage.Setup);
    }

    private void SaveActiveServerProfileName()
    {
        var path = _serverPath.Text?.Trim() ?? string.Empty;
        var name = _serverProfileName.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !ServerUiStateEvaluator.HasServerExecutable(path))
        {
            SetStatus("Es ist kein eingerichteter Server ausgewählt.");
            return;
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Gib einen Servernamen ein.");
            return;
        }

        var profile = _serverProfiles.AddOrUpdate(path, name);
        _serverProfileName.Text = profile.DisplayName;
        RefreshServerProfileSelector();
        RefreshFunctionalUi();
        SetStatus("Servername gespeichert.");
    }

    private async Task RemoveActiveServerProfileAsync()
    {
        var active = _serverProfiles.ActiveProfile;
        if (active == null)
        {
            SetStatus("Es ist kein gespeichertes Serverprofil ausgewählt.");
            return;
        }
        if (_serverProcess.IsRunning || _installCts != null)
        {
            SetStatus("Stoppe zuerst den aktiven Server.");
            return;
        }

        var confirmed = await _confirmations.ConfirmAsync(
            "Server aus Liste entfernen",
            $"{active.DisplayName} wird nur aus dieser App entfernt. Dateien im Serverordner bleiben unverändert. Fortfahren?");
        if (!confirmed)
            return;

        _serverProfiles.Remove(active.Id);
        if (_serverProfiles.ActiveProfile != null)
        {
            LoadActiveServerProfile();
            ReloadFunctionalServerData();
            NavigateFunctional(FunctionalPage.Overview);
        }
        else
        {
            _serverPath.Text = string.Empty;
            _serverProfileName.Text = string.Empty;
            BeginAddingServerProfile();
        }

        RefreshServerProfileSelector();
        RefreshFunctionalUi();
        SetStatus("Server wurde aus der Liste entfernt; die Dateien blieben unverändert.");
    }

    private static bool PathsReferToSameServer(string left, string right)
    {
        try
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                comparison);
        }
        catch
        {
            return false;
        }
    }

    private void UpdateFunctionalActivity(string message)
    {
        if (!_functionalUiBuilt)
            return;

        _dispatcher.Post(() => _dashboardActivity.Text = $"{DateTime.Now:HH:mm} · {message}");
    }

    private static string LatestBackupDescription(string serverPath)
    {
        if (!Directory.Exists(serverPath))
            return "Noch keine";

        var backupDir = Path.Combine(serverPath, "backup");
        if (!Directory.Exists(backupDir))
            return "Noch keine";

        var latest = new DirectoryInfo(backupDir)
            .EnumerateDirectories()
            .OrderByDescending(directory => directory.LastWriteTime)
            .FirstOrDefault();
        return latest == null ? "Noch keine" : latest.LastWriteTime.ToString("dd.MM. HH:mm");
    }

    private static Border HealthCard(string title, TextBlock value)
    {
        value.FontSize = 16;
        value.FontWeight = FontWeight.SemiBold;
        return new Border
        {
            Background = SurfaceBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 10, 0),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = title, Foreground = MutedBrush, FontSize = 12 },
                    value,
                },
            },
        };
    }

    private static Border SetupChoice(string title, string description, Action choose)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(17),
            Background = SurfaceRaisedBrush,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(9),
            Content = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = description, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap },
                },
            },
        };
        button.Click += (_, _) => choose();
        return new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = button,
        };
    }

    private static Border SummaryCard(string label, string value)
    {
        return new Border
        {
            Background = SurfaceBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(13),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock { Text = label, Foreground = MutedBrush, FontSize = 12 },
                    new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap },
                },
            },
        };
    }

    private static Control CheckLine(bool enabled, string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            Opacity = enabled ? 1 : .55,
            Children =
            {
                new TextBlock { Text = enabled ? "✓" : "–", Foreground = enabled ? SuccessBrush : MutedBrush },
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            },
        };
    }

    private static Control FieldRow(string label, Control editor)
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            ColumnSpacing = 10,
            Children =
            {
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
                PutColumn(editor, 1),
            },
        };
    }

    private static TextBlock SectionTitle(string text)
        => new() { Text = text, FontSize = 24, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };

    private static TextBlock MutedText(string text)
        => new() { Text = text, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap };

    private static Button PrimaryButton(string text)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 140,
            Padding = new Thickness(15, 9),
            Margin = new Thickness(0, 0, 8, 8),
            Background = AccentBrush,
            BorderBrush = AccentBrush,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(7),
        };
        return button;
    }

    private static Button SecondaryButton(string text)
        => new()
        {
            Content = text,
            MinWidth = 120,
            Padding = new Thickness(13, 9),
            Margin = new Thickness(0, 0, 8, 8),
            Background = SurfaceRaisedBrush,
            BorderBrush = BorderBrush,
            CornerRadius = new CornerRadius(7),
        };

    private static Button LinkButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 5),
            Margin = new Thickness(0, 3, 8, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brush.Parse("#79BFFF"),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static string SetupStepName(int step) => step switch
    {
        0 => "Ziel",
        1 => "Serverordner",
        2 => "Profil",
        3 => "Prüfen & installieren",
        _ => "Fertig",
    };

    private static Control PutRow(Control control, int row)
    {
        Grid.SetRow(control, row);
        return control;
    }

    private static Control PutColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static Control DockLeft(Control control)
    {
        DockPanel.SetDock(control, Dock.Left);
        return control;
    }

    private static Control DockRight(Control control)
    {
        DockPanel.SetDock(control, Dock.Right);
        return control;
    }

    private static void DetachFromParent(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, control):
                contentControl.Content = null;
                break;
        }
    }

    private sealed record QuickPlayPresetOption(BuiltInPresets.BuiltInPreset Preset)
    {
        public override string ToString() => Preset.Name;
    }
}
