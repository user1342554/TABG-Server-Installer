# Phase 3: MVVM Migration — Design Spec

**Date:** 2026-04-08
**Scope:** Extract ViewModels from all 15 panel code-behinds, adopt CommunityToolkit.Mvvm, wire up DI container, decouple services from UI
**Goal:** Every panel becomes independently testable. UI changes don't break logic. Logic changes don't break UI.

---

## Overview

All GUI logic currently lives in XAML code-behind files (~4,500 lines across 15 panels). Business logic is tangled with UI event handlers, nothing is independently testable, and every change risks breaking the UI.

This phase extracts that logic into ViewModels using the standard modern WPF approach: CommunityToolkit.Mvvm for source-generated observable properties and commands, Microsoft.Extensions.Hosting for dependency injection (already a project dependency), and panel-by-panel migration protected by the Phase 1 test suite.

---

## 1. CommunityToolkit.Mvvm Adoption

### Package

Add `CommunityToolkit.Mvvm` (latest stable, ~8.4.0) to `TabgInstaller.Gui.csproj`. This is a lightweight, source-generated, reflection-free MVVM toolkit maintained by Microsoft.

### What It Provides

- **`ObservableObject`** — base class replacing manual `INotifyPropertyChanged`
- **`[ObservableProperty]`** — attribute on private fields that source-generates public properties with change notification
- **`[RelayCommand]`** — attribute on methods that source-generates `ICommand` properties for XAML binding
- **`partial class`** — required for source generation to work

### Convention

All ViewModels follow this pattern:

```csharp
public partial class ExampleViewModel : ObservableObject
{
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isLoading;

    [RelayCommand]
    private void Save() { ... }

    [RelayCommand]
    private async Task LoadAsync() { ... }
}
```

Private backing fields use `_camelCase` (matching the project's existing naming convention from Phase 1). The source generator produces PascalCase public properties (`StatusText`, `IsLoading`) and command properties (`SaveCommand`, `LoadAsyncCommand`).

---

## 2. DI Container Setup

### Composition Root

`App.xaml.cs` becomes the composition root using `Microsoft.Extensions.Hosting` (already a dependency).

**App.xaml change:** Remove `StartupUri="MainWindow.xaml"`. The window is created manually from the container.

**App.xaml.cs new shape:**

```csharp
public partial class App : Application
{
    private IHost _host = null!;

    public App()
    {
        DispatcherUnhandledException += (s, args) =>
        {
            // Existing crash logging — unchanged
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<IServerPathProvider, ServerPathProvider>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IToastService, ToastService>();

        // Core services
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IServerProcessService, ServerProcessService>();
        services.AddSingleton<KnownPlayersService>();
        services.AddSingleton<ConfigValidationService>();
        services.AddTransient<BackupService>();
        services.AddTransient<ModConfigService>();
        services.AddTransient<StarterPackConfigService>();
        services.AddTransient<StarterPackLoadoutService>();
        services.AddTransient<BepInExLoaderService>();

        // ViewModels (transient — fresh instance per resolution)
        services.AddTransient<SettingsPanelViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<AdminPanelViewModel>();
        services.AddTransient<SuperSecretSettingsViewModel>();
        services.AddTransient<MatchSettingsViewModel>();
        services.AddTransient<BackupsPanelViewModel>();
        services.AddTransient<RingSpawnsViewModel>();
        services.AddTransient<ConfigViewModel>();
        services.AddTransient<ServerModsViewModel>();
        services.AddTransient<ConsolePanelViewModel>();
        services.AddTransient<ModSettingsViewModel>();
        services.AddTransient<InstallerPanelViewModel>();
        services.AddTransient<ClientPanelViewModel>();
        services.AddTransient<ReferencePanelViewModel>();
        services.AddTransient<LoadoutEditorViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}
```

### Service Lifetimes

| Lifetime | Services | Rationale |
|----------|----------|-----------|
| Singleton | `IServerPathProvider`, `IAppSettingsService`, `IToastService`, `IUpdateService`, `IServerProcessService`, `KnownPlayersService`, `ConfigValidationService` | Hold state or must be shared across panels |
| Transient | `BackupService`, `ModConfigService`, `StarterPackConfigService`, `StarterPackLoadoutService`, `BepInExLoaderService`, all ViewModels | Stateless helpers or per-panel instances |

---

## 3. Service Interface Extraction

### Services That Get Interfaces

Only services directly consumed by ViewModels and requiring mockability in tests.

#### IAppSettingsService

```csharp
public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    void Reset();
}
```

`AppSettingsService` is de-staticified. Its `Load()`, `Save()`, and `Reset()` methods become instance methods with the same logic. Registered as singleton (caches loaded settings in memory).

#### IToastService

```csharp
public interface IToastService
{
    void Success(string message, int durationMs = 3000);
    void Error(string message, int durationMs = 5000);
    void Warning(string message, int durationMs = 4000);
    void Info(string message, int durationMs = 3000);
}
```

The `Initialize(Action<string, ToastType, int> showCallback)` method stays on the concrete class only — `MainWindow` calls it to hook the UI callback. ViewModels only see the interface.

#### IServerProcessService

```csharp
public interface IServerProcessService
{
    bool IsRunning { get; }
    ObservableCollection<LogEntry> LogEntries { get; }
    event Action<LogEntry>? LogEntryReceived;

    void Start();
    void Stop();
    void ClearEntries();
    void AddEntry(LogEntry entry);
    string GetRecentText(int maxLines = 20);
    void RegisterCollectionSynchronization(Action<object, object> enableSync);
}
```

#### IKnownPlayersService

```csharp
public interface IKnownPlayersService
{
    int ScanGuestbooks(string serverDir);
    List<string> GetPlayerNames();
    string? ResolveEpicId(string playerName);
    Dictionary<string, string> Players { get; }
}
```

#### IBackupService

```csharp
public interface IBackupService
{
    Task<bool> CreateBackupAsync(string serverDir);
    List<BackupInfo> GetAvailableBackups(string serverDir);
    Task<bool> RestoreBackupAsync(string serverDir, BackupInfo backup);
    bool DeleteBackup(BackupInfo backup);
    string FormatFileSize(long bytes);
}
```

#### IUpdateService

```csharp
public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task<bool> ApplyUpdateAsync(string downloadUrl);
}
```

### Services That Don't Get Interfaces

| Service | Reason |
|---------|--------|
| `ConfigValidationService` | Pure logic, no side effects, testable as-is |
| `LogLineParser` | Static pure functions |
| `ConfigIO` | Static helpers, no state |
| `ConfigPatcher` | Internal to other services |
| `SafeConfigEditor` | Internal to other services |
| `GlobalServerPath` | Replaced by `IServerPathProvider` |

---

## 4. The ServerPath Problem

### Problem

Every panel currently has `Initialize(string serverDir)` called by `MainWindow` after the setup wizard completes. The DI container builds services at startup, but `serverDir` isn't known until `AppSettingsService.Load()` runs (or the setup wizard finishes).

### Solution: IServerPathProvider

```csharp
// In TabgInstaller.Core
public interface IServerPathProvider
{
    string ServerPath { get; }
    void SetPath(string path);
    event Action? PathChanged;
}

public class ServerPathProvider : IServerPathProvider
{
    public string ServerPath { get; private set; } = "";
    public event Action? PathChanged;

    public void SetPath(string path)
    {
        ServerPath = path;
        PathChanged?.Invoke();
    }
}
```

This replaces the existing static `GlobalServerPath` class. Registered as singleton. `MainWindow.OnLoaded` calls `SetPath(settings.ServerPath)` after loading settings or completing the setup wizard.

ViewModels subscribe to `PathChanged` in their constructor to perform late initialization:

```csharp
public AdminPanelViewModel(IServerPathProvider serverPath, ...)
{
    _serverPath = serverPath;
    _serverPath.PathChanged += OnServerPathChanged;
}

private void OnServerPathChanged()
{
    LoadAdmins();
    RefreshKnownPlayers();
}
```

### ServerProcessService

`ServerProcessService` currently takes `serverDir` as a constructor parameter. After migration, it receives `IServerPathProvider` instead and reads `ServerPath` when starting the process (not at construction time). This is safe because `Start()` is never called before the path is set.

---

## 5. ViewModel Extraction Pattern

### Standard Shape

Every extracted ViewModel follows this structure:

```csharp
public partial class AdminPanelViewModel : ObservableObject
{
    // Injected dependencies
    private readonly IServerPathProvider _serverPath;
    private readonly IKnownPlayersService _knownPlayers;
    private readonly IToastService _toast;

    // Observable state
    [ObservableProperty] private ObservableCollection<AdminEntry> _admins = new();
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _selectedPlayerName = "";
    [ObservableProperty] private ObservableCollection<string> _knownPlayerNames = new();
    [ObservableProperty] private int _selectedPermLevel = 3;

    public AdminPanelViewModel(
        IServerPathProvider serverPath,
        IKnownPlayersService knownPlayers,
        IToastService toast)
    {
        _serverPath = serverPath;
        _knownPlayers = knownPlayers;
        _toast = toast;
        _serverPath.PathChanged += OnServerPathChanged;
    }

    private void OnServerPathChanged()
    {
        RefreshKnownPlayers();
        LoadAdmins();
    }

    [RelayCommand]
    private void AddAdmin() { /* logic from AddAdmin_Click */ }

    [RelayCommand]
    private void RemoveAdmin(AdminEntry entry) { /* logic from RemoveAdmin_Click */ }

    [RelayCommand]
    private void Save() { /* logic from Save_Click */ }

    [RelayCommand]
    private void RefreshPlayers() { /* logic from RefreshPlayers_Click */ }

    private void LoadAdmins() { /* logic from LoadAdmins() */ }
    private void RefreshKnownPlayers() { /* logic from RefreshKnownPlayers() */ }
}
```

### What Code-Behind Becomes

```csharp
public partial class AdminPanel : UserControl
{
    public AdminPanel()
    {
        InitializeComponent();
    }
}
```

`DataContext` is set by `MainWindow` after resolving the ViewModel from DI.

### XAML Binding Changes

```xml
<!-- Before: event handler -->
<Button Content="Add Admin" Click="AddAdmin_Click"/>
<TextBlock x:Name="TxtStatus" Text="Ready"/>
<ListView x:Name="LstAdmins"/>

<!-- After: command binding -->
<Button Content="Add Admin" Command="{Binding AddAdminCommand}"/>
<TextBlock Text="{Binding StatusText}"/>
<ListView ItemsSource="{Binding Admins}"/>
```

### Things That Stay in Code-Behind

These are legitimate View concerns that don't belong in ViewModels:

- **Animations** — the auto-save fade animation in ConfigPanel
- **ScrollViewer management** — auto-scroll detection in ConsolePanel
- **File dialogs** — `SaveFileDialog` / `OpenFileDialog` (opened in code-behind, result passed to ViewModel via method call)
- **Focus management** — setting focus after actions
- **Window-level operations** — `ShowDialog()` calls

For file dialogs, the code-behind handles the dialog and calls a ViewModel method with the result:

```csharp
// Code-behind (thin)
private void ExportButton_Click(object sender, RoutedEventArgs e)
{
    var dialog = new SaveFileDialog { ... };
    if (dialog.ShowDialog() == true)
    {
        ((ConsolePanelViewModel)DataContext).ExportLog(dialog.FileName);
    }
}
```

---

## 6. MainWindow Simplification

### After Migration

```csharp
public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IAppSettingsService _appSettings;
    private readonly IServerPathProvider _serverPath;

    public MainWindow(
        IServiceProvider services,
        IAppSettingsService appSettings,
        IServerPathProvider serverPath)
    {
        _services = services;
        _appSettings = appSettings;
        _serverPath = serverPath;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Hook toast UI callback
        var toast = _services.GetRequiredService<IToastService>() as ToastService;
        toast?.Initialize((msg, type, dur) =>
            Dispatcher.Invoke(() => ToastControl.Show(msg, type, dur)));

        // Update check (same logic, injected service)
        await CheckForUpdates();

        // Setup wizard or initialize
        var settings = _appSettings.Load();
        if (!settings.SetupCompleted || string.IsNullOrEmpty(settings.ServerPath)
            || !Directory.Exists(settings.ServerPath))
        {
            RunSetupWizard();
        }
        else
        {
            InitializePanels(settings.ServerPath);
        }
    }

    private void InitializePanels(string serverDir)
    {
        // Set the server path — triggers all ViewModel initialization
        _serverPath.SetPath(serverDir);

        // Assign ViewModels to panels
        DashboardTab.DataContext = _services.GetRequiredService<DashboardViewModel>();
        ConfigTab.DataContext = _services.GetRequiredService<ConfigViewModel>();
        ConsoleTab.DataContext = _services.GetRequiredService<ConsolePanelViewModel>();
        AdminPanelControl.DataContext = _services.GetRequiredService<AdminPanelViewModel>();
        ServerModsTab.DataContext = _services.GetRequiredService<ServerModsViewModel>();
        BackupsTab.DataContext = _services.GetRequiredService<BackupsPanelViewModel>();
        SettingsTab.DataContext = _services.GetRequiredService<SettingsPanelViewModel>();
        // ... remaining panels

        MainTabs.SelectedIndex = 0;
    }
}
```

### Cross-Panel Communication

Currently `DashboardTab.RequestOpenConsole` fires an event that `MainWindow` handles to switch tabs. After migration, this is handled via a simple navigation service:

```csharp
public interface INavigationService
{
    void NavigateToTab(int tabIndex);
}
```

`MainWindow` implements this (or a simple class that holds a reference to the `TabControl`). Registered as singleton. ViewModels that need tab switching inject `INavigationService`.

The `SettingsTab.RequestHardReset` event is similarly replaced — the ViewModel calls a method on an injected service, and `MainWindow` subscribes.

---

## 7. Panel Migration Order

Migration proceeds one panel at a time, simplest to most complex. Each panel is a self-contained unit of work: extract ViewModel, update XAML, reduce code-behind, write tests.

| Order | Panel | Lines | Complexity | Key Dependencies |
|-------|-------|-------|------------|------------------|
| 1 | SettingsPanel | 51 | Low | IAppSettingsService |
| 2 | DashboardPanel | 102 | Low | IServerProcessService, INavigationService |
| 3 | AdminPanel | 205 | Medium | IKnownPlayersService, IToastService |
| 4 | SuperSecretSettingsPanel | 140 | Medium | IAppSettingsService |
| 5 | MatchSettingsPanel | 199 | Medium | IServerPathProvider, ConfigIO |
| 6 | BackupsPanel | 237 | Medium | IBackupService, IToastService |
| 7 | RingSpawnsPanel | 242 | Medium | IServerPathProvider, ConfigIO |
| 8 | ConfigPanel | 237 | Medium | GameSettingsDynamicViewModel, sub-panel composition (see below) |
| 9 | ServerModsPanel | 274 | Medium | BepInExLoaderService, IToastService |
| 10 | ConsolePanel | 295 | High | IServerProcessService, CollectionViewSource |
| 11 | ModSettingsPanel | 350 | High | ModConfigService, IToastService |
| 12 | InstallerPanel | 335 | High | Multi-step wizard flow |
| 13 | ClientPanel | 382 | High | BepInExLoaderService, file operations |
| 14 | ReferencePanel | 389 | High | KnowledgeIndex, search logic |
| 15 | LoadoutEditorPanel | 852 | Very High | StarterPackLoadoutService, complex UI state |

### ConfigPanel Sub-Panel Composition

`ConfigPanel` is an orchestrator that hosts 5 child UserControls: `MatchSettingsControl`, `RingSpawnsControl`, `LoadoutEditorControl`, `ModSettingsControl`, and `AdminPanelControl`. It also hosts `PresetsGridControl` and `GameSettingsGrid`.

Currently, `ConfigPanel.Initialize(serverDir)` cascades initialization to each child. After migration:

- `ConfigViewModel` is the parent ViewModel. It owns the `GameSettingsDynamicViewModel` (for the settings grid) and coordinates auto-save, file watching, and preset loading.
- Each child panel gets its own ViewModel (already listed in the migration order above).
- `MainWindow.InitializePanels` sets `DataContext` on both the top-level `ConfigTab` and each child panel's ViewModel. Alternatively, `ConfigViewModel` can compose child ViewModels and the child panels bind via `DataContext="{Binding MatchSettingsVm}"` in XAML.
- `PresetsGridControl` gets a small `PresetsViewModel` or stays as a View-only control if its logic is minimal (199 lines of code-behind — it gets a ViewModel).
- The `IServerPathProvider.PathChanged` event triggers `ConfigViewModel`, which coordinates reloading game settings and notifying child ViewModels.

### Per-Panel Checklist

1. Create `ViewModels/PanelViewModel.cs`
2. Move private state fields to `[ObservableProperty]` fields
3. Move `_Click` handlers to `[RelayCommand]` methods
4. Move service calls from code-behind to ViewModel (via injected interfaces)
5. Update XAML: `Click="Handler"` → `Command="{Binding CommandName}"`
6. Update XAML: `x:Name` element references → `{Binding Property}`
7. Reduce code-behind to constructor + legitimate View concerns
8. Register ViewModel in `App.ConfigureServices`
9. Assign DataContext in `MainWindow.InitializePanels`
10. Write ViewModel unit tests

---

## 8. Existing ViewModel Migration

The 3 existing ViewModels are updated when their parent panel is migrated.

### ServerSettingsViewModel (38 lines)

Absorbed into `ConfigViewModel`. Its 3 properties (`ServerName`, `Port`, `MaxPlayers`) become `[ObservableProperty]` fields on the parent ViewModel. The `ToModel()` method stays.

### GameSettingsDynamicViewModel (298 lines)

Stays as a separate class (it's complex enough to warrant isolation) but updated:
- Inherits `ObservableObject` instead of manual `INotifyPropertyChanged`
- `ConfigValidationService` is received via constructor injection instead of `new()`
- `ShowAdvanced` becomes `[ObservableProperty]` with `partial void OnShowAdvancedChanged()` hook
- Empty catch blocks get proper logging (some already fixed in Phase 1)
- Used as a child ViewModel composed inside `ConfigViewModel`

### StarterPackDynamicViewModel

Same treatment as `GameSettingsDynamicViewModel`: `ObservableObject` base, injected services.

---

## 9. Testing Strategy

### Test Project Changes

`TabgInstaller.Tests.csproj` adds a project reference to `TabgInstaller.Gui`:

```xml
<ProjectReference Include="..\TabgInstaller.Gui\TabgInstaller.Gui.csproj" />
```

Test infrastructure (xUnit, FluentAssertions, Moq) is already present from Phase 1.

### Test Organization

```
TabgInstaller.Tests/
├── Services/                    (existing — Phase 1)
├── ViewModels/                  (new)
│   ├── SettingsPanelViewModelTests.cs
│   ├── DashboardViewModelTests.cs
│   ├── AdminPanelViewModelTests.cs
│   ├── ConsolePanelViewModelTests.cs
│   ├── ConfigViewModelTests.cs
│   └── ...                     (one per ViewModel)
└── ConfigIOTests.cs             (existing)
```

### Test Shape

```csharp
public class AdminPanelViewModelTests
{
    private readonly Mock<IServerPathProvider> _serverPath = new();
    private readonly Mock<IKnownPlayersService> _knownPlayers = new();
    private readonly Mock<IToastService> _toast = new();

    private AdminPanelViewModel CreateSut() =>
        new(_serverPath.Object, _knownPlayers.Object, _toast.Object);

    [Fact]
    public void AddAdmin_WithValidPlayer_AddsToCollection()
    {
        var sut = CreateSut();
        _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
        sut.SelectedPlayerName = "Player1";

        sut.AddAdminCommand.Execute(null);

        sut.Admins.Should().ContainSingle(a => a.EpicId == "EPIC123");
    }

    [Fact]
    public void AddAdmin_DuplicatePlayer_ShowsWarningAndDoesNotAdd()
    {
        var sut = CreateSut();
        _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
        sut.SelectedPlayerName = "Player1";
        sut.AddAdminCommand.Execute(null); // first add
        sut.SelectedPlayerName = "Player1";

        sut.AddAdminCommand.Execute(null); // duplicate

        sut.Admins.Should().HaveCount(1);
        _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Save_WritesAdminsToFile_ShowsSuccess()
    {
        // Arrange: add some admins, set server path
        // Act: execute SaveCommand
        // Assert: verify toast success, verify file written
    }
}
```

### Coverage Goal

Every `[RelayCommand]` method and every meaningful property-change side effect has at least one test. Focus on:

- Command happy path
- Command validation / guard conditions
- Service interaction verification (correct service methods called)
- State transitions (property values after commands execute)

No WPF runtime required — ViewModels are plain C# objects.

---

## 10. Files Changed / Created

### New Files

| File | Purpose |
|------|---------|
| `Core/IServerPathProvider.cs` | Interface + `ServerPathProvider` implementation |
| `Gui/Services/IAppSettingsService.cs` | Interface for app settings |
| `Gui/Services/IToastService.cs` | Interface for toast notifications |
| `Core/Services/IServerProcessService.cs` | Interface for server process management |
| `Core/Services/IKnownPlayersService.cs` | Interface for known players |
| `Core/Services/IBackupService.cs` | Interface for backup operations |
| `Core/Services/IUpdateService.cs` | Interface for update checking |
| `Gui/Services/INavigationService.cs` | Interface for tab navigation |
| `Gui/ViewModels/SettingsPanelViewModel.cs` | Settings panel ViewModel |
| `Gui/ViewModels/DashboardViewModel.cs` | Dashboard ViewModel |
| `Gui/ViewModels/AdminPanelViewModel.cs` | Admin panel ViewModel |
| `Gui/ViewModels/SuperSecretSettingsViewModel.cs` | Secret settings ViewModel |
| `Gui/ViewModels/MatchSettingsViewModel.cs` | Match settings ViewModel |
| `Gui/ViewModels/BackupsPanelViewModel.cs` | Backups panel ViewModel |
| `Gui/ViewModels/RingSpawnsViewModel.cs` | Ring spawns ViewModel |
| `Gui/ViewModels/ConfigViewModel.cs` | Config panel ViewModel (orchestrator) |
| `Gui/ViewModels/ServerModsViewModel.cs` | Server mods ViewModel |
| `Gui/ViewModels/ConsolePanelViewModel.cs` | Console panel ViewModel |
| `Gui/ViewModels/ModSettingsViewModel.cs` | Mod settings ViewModel |
| `Gui/ViewModels/InstallerPanelViewModel.cs` | Installer panel ViewModel |
| `Gui/ViewModels/ClientPanelViewModel.cs` | Client panel ViewModel |
| `Gui/ViewModels/ReferencePanelViewModel.cs` | Reference panel ViewModel |
| `Gui/ViewModels/LoadoutEditorViewModel.cs` | Loadout editor ViewModel |
| `Gui/ViewModels/PresetsViewModel.cs` | Presets grid ViewModel |
| `Tests/ViewModels/*.cs` | ViewModel unit tests (one per ViewModel) |

### Modified Files

| File | Changes |
|------|---------|
| `Gui/TabgInstaller.Gui.csproj` | Add `CommunityToolkit.Mvvm` package reference |
| `Gui/App.xaml` | Remove `StartupUri` attribute |
| `Gui/App.xaml.cs` | Add `IHost` setup, `ConfigureServices`, `OnStartup`/`OnExit` |
| `Gui/MainWindow.xaml.cs` | Constructor injection, replace `InitializeAllPanels` with DI resolution |
| `Gui/Services/AppSettingsService.cs` | De-static, implement `IAppSettingsService` |
| `Gui/Services/ToastService.cs` | Remove static `Instance`, implement `IToastService` |
| `Core/Services/ServerProcessService.cs` | Implement `IServerProcessService`, accept `IServerPathProvider` |
| `Core/Services/KnownPlayersService.cs` | Implement `IKnownPlayersService` |
| `Core/Services/BackupService.cs` | Implement `IBackupService` |
| `Core/Services/UpdateService.cs` | Implement `IUpdateService` |
| All 15 `Tabs/*.xaml.cs` files | Reduce to thin code-behind (constructor + View-only concerns) |
| All 15 `Tabs/*.xaml` files | Replace `Click` handlers with `Command` bindings, `x:Name` with `{Binding}` |
| `Gui/ViewModels/GameSettingsDynamicViewModel.cs` | Inherit `ObservableObject`, inject `ConfigValidationService` |
| `Gui/ViewModels/ServerSettingsViewModel.cs` | Absorb into `ConfigViewModel` or update to `ObservableObject` |
| `Tests/TabgInstaller.Tests.csproj` | Add `TabgInstaller.Gui` project reference |

---

## 11. Implementation Order

1. **DI foundation** — Add CommunityToolkit.Mvvm package, wire up `IHost` in `App.xaml.cs`, create `IServerPathProvider`
2. **Extract service interfaces** — `IAppSettingsService`, `IToastService`, `IServerProcessService`, etc.
3. **De-static services** — Convert `AppSettingsService` and `ToastService` from static to instance-based
4. **Update MainWindow** — Constructor injection, replace `InitializeAllPanels` with DI-based initialization
5. **Migrate panels 1-3** (SettingsPanel, DashboardPanel, AdminPanel) — establish the pattern
6. **Migrate panels 4-7** (SuperSecretSettings, MatchSettings, Backups, RingSpawns) — medium complexity
7. **Migrate panels 8-11** (Config, ServerMods, Console, ModSettings) — higher complexity
8. **Migrate panels 12-15** (Installer, Client, Reference, LoadoutEditor) — most complex
9. **Update existing ViewModels** — `GameSettingsDynamicViewModel`, `StarterPackDynamicViewModel` to `ObservableObject`
10. **Write ViewModel tests** — one test class per ViewModel
11. **Verify** — `dotnet build` clean, `dotnet test` all green, CI passes

---

## Out of Scope

- Localization / i18n
- New features (multi-server, plugin marketplace, remote management)
- Accessibility improvements
- Replacing `Newtonsoft.Json` with `System.Text.Json` across Core
- Navigation framework adoption (Prism regions, etc.)
- MVVM for modal dialogs (`SetupWizardWindow`, `ChangelogWindow`) — they stay as-is
- Full UI redesign or theming
