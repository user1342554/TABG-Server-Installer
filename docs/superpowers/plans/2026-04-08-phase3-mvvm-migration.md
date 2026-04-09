# Phase 3: MVVM Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract all GUI logic from 15 XAML code-behind files into independently testable ViewModels using CommunityToolkit.Mvvm and Microsoft.Extensions.Hosting DI.

**Architecture:** ViewModels inherit `ObservableObject`, use `[ObservableProperty]` for state and `[RelayCommand]` for actions. Services are injected via constructor. `IServerPathProvider` replaces the static `GlobalServerPath` and triggers late initialization via `PathChanged` event. Panels become thin code-behind shells with `DataContext` set by `MainWindow` from the DI container.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm ~8.4.0, Microsoft.Extensions.Hosting 8.0.0 (existing), xUnit, FluentAssertions, Moq

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `Core/IServerPathProvider.cs` | Interface + implementation for server directory path |
| `Gui/Services/IAppSettingsService.cs` | Interface for app settings load/save/reset |
| `Gui/Services/IToastService.cs` | Interface for toast notifications |
| `Gui/Services/INavigationService.cs` | Interface for tab navigation + hard reset |
| `Core/Services/IServerProcessService.cs` | Interface for server process management |
| `Core/Services/IKnownPlayersService.cs` | Interface for known players |
| `Core/Services/IBackupService.cs` | Interface for backup operations |
| `Core/Services/IUpdateService.cs` | Interface for update checking |
| `Gui/ViewModels/SettingsPanelViewModel.cs` | Settings panel ViewModel |
| `Gui/ViewModels/DashboardViewModel.cs` | Dashboard panel ViewModel |
| `Gui/ViewModels/AdminPanelViewModel.cs` | Admin panel ViewModel |
| `Gui/ViewModels/SuperSecretSettingsViewModel.cs` | Secret settings ViewModel |
| `Gui/ViewModels/MatchSettingsViewModel.cs` | Match settings ViewModel |
| `Gui/ViewModels/BackupsPanelViewModel.cs` | Backups panel ViewModel |
| `Gui/ViewModels/RingSpawnsViewModel.cs` | Ring spawns ViewModel |
| `Gui/ViewModels/ConfigViewModel.cs` | Config orchestrator ViewModel |
| `Gui/ViewModels/PresetsViewModel.cs` | Presets grid ViewModel |
| `Gui/ViewModels/ServerModsViewModel.cs` | Server mods ViewModel |
| `Gui/ViewModels/ConsolePanelViewModel.cs` | Console panel ViewModel |
| `Gui/ViewModels/ModSettingsViewModel.cs` | Mod settings ViewModel |
| `Gui/ViewModels/InstallerPanelViewModel.cs` | Installer panel ViewModel |
| `Gui/ViewModels/ClientPanelViewModel.cs` | Client panel ViewModel |
| `Gui/ViewModels/ReferencePanelViewModel.cs` | Reference panel ViewModel |
| `Gui/ViewModels/LoadoutEditorViewModel.cs` | Loadout editor ViewModel |
| `Tests/ViewModels/*.cs` | One test class per ViewModel |

### Modified Files

| File | Changes |
|------|---------|
| `Gui/TabgInstaller.Gui.csproj` | Add CommunityToolkit.Mvvm package |
| `Gui/App.xaml` | Remove `Startup` event handler |
| `Gui/App.xaml.cs` | Add IHost, ConfigureServices, OnStartup/OnExit |
| `Gui/MainWindow.xaml.cs` | Constructor injection, DI-based panel initialization |
| `Gui/Services/AppSettingsService.cs` | De-static, implement IAppSettingsService |
| `Gui/Services/ToastService.cs` | Remove static Instance, implement IToastService |
| `Core/Services/ServerProcessService.cs` | Implement IServerProcessService, accept IServerPathProvider |
| `Core/Services/KnownPlayersService.cs` | Implement IKnownPlayersService |
| `Core/Services/BackupService.cs` | Implement IBackupService |
| `Core/Services/UpdateService.cs` | Implement IUpdateService |
| `Tests/TabgInstaller.Tests.csproj` | Add Gui project reference |
| All 15 `Tabs/*.xaml` | Replace Click handlers with Command bindings |
| All 15 `Tabs/*.xaml.cs` | Reduce to thin code-behind |
| `Gui/ViewModels/GameSettingsDynamicViewModel.cs` | Inherit ObservableObject |

---

### Task 1: Add CommunityToolkit.Mvvm and Create IServerPathProvider

**Files:**
- Modify: `TabgInstaller.Gui/TabgInstaller.Gui.csproj`
- Create: `TabgInstaller.Core/IServerPathProvider.cs`
- Create: `TabgInstaller.Tests/ServerPathProviderTests.cs`

- [ ] **Step 1: Add CommunityToolkit.Mvvm NuGet package**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet add TabgInstaller.Gui/TabgInstaller.Gui.csproj package CommunityToolkit.Mvvm
```

- [ ] **Step 2: Write ServerPathProvider test**

Create `TabgInstaller.Tests/ServerPathProviderTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core;
using Xunit;

namespace TabgInstaller.Tests
{
    public class ServerPathProviderTests
    {
        [Fact]
        public void ServerPath_InitiallyEmpty()
        {
            var sut = new ServerPathProvider();
            sut.ServerPath.Should().Be("");
        }

        [Fact]
        public void SetPath_UpdatesServerPath()
        {
            var sut = new ServerPathProvider();
            sut.SetPath(@"C:\GameServer");
            sut.ServerPath.Should().Be(@"C:\GameServer");
        }

        [Fact]
        public void SetPath_FiresPathChangedEvent()
        {
            var sut = new ServerPathProvider();
            bool fired = false;
            sut.PathChanged += () => fired = true;

            sut.SetPath(@"C:\GameServer");

            fired.Should().BeTrue();
        }

        [Fact]
        public void SetPath_CalledTwice_FiresEventTwice()
        {
            var sut = new ServerPathProvider();
            int count = 0;
            sut.PathChanged += () => count++;

            sut.SetPath(@"C:\Server1");
            sut.SetPath(@"C:\Server2");

            count.Should().Be(2);
            sut.ServerPath.Should().Be(@"C:\Server2");
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~ServerPathProviderTests" --verbosity normal
```
Expected: FAIL — `ServerPathProvider` class does not exist yet.

- [ ] **Step 4: Create IServerPathProvider interface and ServerPathProvider implementation**

Create `TabgInstaller.Core/IServerPathProvider.cs`:

```csharp
using System;

namespace TabgInstaller.Core
{
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
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~ServerPathProviderTests" --verbosity normal
```
Expected: 4 tests pass.

- [ ] **Step 6: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer && git add TabgInstaller.Gui/TabgInstaller.Gui.csproj TabgInstaller.Core/IServerPathProvider.cs TabgInstaller.Tests/ServerPathProviderTests.cs && git commit -m "feat: add CommunityToolkit.Mvvm and IServerPathProvider"
```

---

### Task 2: Create Service Interfaces

**Files:**
- Create: `TabgInstaller.Gui/Services/IAppSettingsService.cs`
- Create: `TabgInstaller.Gui/Services/IToastService.cs`
- Create: `TabgInstaller.Gui/Services/INavigationService.cs`
- Create: `TabgInstaller.Core/Services/IServerProcessService.cs`
- Create: `TabgInstaller.Core/Services/IKnownPlayersService.cs`
- Create: `TabgInstaller.Core/Services/IBackupService.cs`
- Create: `TabgInstaller.Core/Services/IUpdateService.cs`

- [ ] **Step 1: Create IAppSettingsService**

Create `TabgInstaller.Gui/Services/IAppSettingsService.cs`:

```csharp
namespace TabgInstaller.Gui.Services
{
    public interface IAppSettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
        void MarkSetupComplete(string serverPath, string clientPath, string clientModdedPath);
        void Reset();
    }
}
```

- [ ] **Step 2: Create IToastService**

Create `TabgInstaller.Gui/Services/IToastService.cs`:

```csharp
namespace TabgInstaller.Gui.Services
{
    public interface IToastService
    {
        void Success(string message);
        void Error(string message);
        void Warning(string message);
        void Info(string message);
    }
}
```

- [ ] **Step 3: Create INavigationService**

Create `TabgInstaller.Gui/Services/INavigationService.cs`:

```csharp
using System;

namespace TabgInstaller.Gui.Services
{
    public interface INavigationService
    {
        void NavigateToTab(int tabIndex);
        event Action? HardResetRequested;
        void RequestHardReset();
    }
}
```

- [ ] **Step 4: Create IServerProcessService**

Create `TabgInstaller.Core/Services/IServerProcessService.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IServerProcessService
    {
        bool IsRunning { get; }
        ObservableCollection<LogEntry> LogEntries { get; }
        event Action<LogEntry>? LogEntryReceived;
        event Action<string>? OutputReceived;

        bool Start(string additionalArgs = "-batchmode -nographics -nolog");
        void Stop();
        void ClearEntries();
        void AddEntry(LogEntry entry);
        string GetRecentText(int maxLines = 20);
        void RegisterCollectionSynchronization(Action<object, object> register);
    }
}
```

- [ ] **Step 5: Create IKnownPlayersService**

Create `TabgInstaller.Core/Services/IKnownPlayersService.cs`:

```csharp
using System.Collections.Generic;

namespace TabgInstaller.Core.Services
{
    public interface IKnownPlayersService
    {
        IReadOnlyList<KnownPlayer> Players { get; }
        int ScanGuestbooks(string serverDir);
        string? ResolveEpicId(string playerName);
        List<string> GetPlayerNames();
    }
}
```

- [ ] **Step 6: Create IBackupService**

Create `TabgInstaller.Core/Services/IBackupService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public interface IBackupService
    {
        Task<bool> CreateBackupAsync(string serverDir);
        List<BackupInfo> GetAvailableBackups(string serverDir);
        Task<bool> RestoreBackupAsync(string serverDir, BackupInfo backup);
        bool DeleteBackup(BackupInfo backup);
        string FormatFileSize(long bytes);
    }
}
```

- [ ] **Step 7: Create IUpdateService**

Create `TabgInstaller.Core/Services/IUpdateService.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public interface IUpdateService
    {
        Task<UpdateInfo?> CheckForUpdateAsync();
        Task<bool> ApplyUpdateAsync(string downloadUrl, IProgress<string>? log = null);
    }
}
```

- [ ] **Step 8: Build to verify interfaces compile**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet build --verbosity normal
```
Expected: Build succeeds (interfaces are unused at this point — no errors).

- [ ] **Step 9: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer && git add TabgInstaller.Gui/Services/IAppSettingsService.cs TabgInstaller.Gui/Services/IToastService.cs TabgInstaller.Gui/Services/INavigationService.cs TabgInstaller.Core/Services/IServerProcessService.cs TabgInstaller.Core/Services/IKnownPlayersService.cs TabgInstaller.Core/Services/IBackupService.cs TabgInstaller.Core/Services/IUpdateService.cs && git commit -m "feat: add service interfaces for DI"
```

---

### Task 3: Implement Service Interfaces on Existing Services

**Files:**
- Modify: `TabgInstaller.Gui/Services/AppSettingsService.cs`
- Modify: `TabgInstaller.Gui/Services/ToastService.cs`
- Modify: `TabgInstaller.Core/Services/ServerProcessService.cs`
- Modify: `TabgInstaller.Core/Services/KnownPlayersService.cs`
- Modify: `TabgInstaller.Core/Services/BackupService.cs`
- Modify: `TabgInstaller.Core/Services/UpdateService.cs`

- [ ] **Step 1: De-static AppSettingsService**

Read `TabgInstaller.Gui/Services/AppSettingsService.cs`. Make these changes:
- Remove `static` from the class declaration
- Remove `static` from all methods (`Load`, `Save`, `MarkSetupComplete`, `Reset`)
- Remove `static` from `_cached`, `SettingsDir`, `SettingsPath` fields
- Add `: IAppSettingsService` to the class declaration
- Keep all method logic identical — only remove `static` keyword

The class becomes:
```csharp
public class AppSettingsService : IAppSettingsService
{
    private readonly string SettingsDir = ...;  // same path logic
    private readonly string SettingsPath = ...;
    private AppSettings? _cached;

    public AppSettings Load() { ... }  // same logic, not static
    public void Save(AppSettings settings) { ... }
    public void MarkSetupComplete(...) { ... }
    public void Reset() { ... }
}
```

**Important:** Every existing call site that uses `AppSettingsService.Load()` (static call) will now break. Do NOT fix them yet — they will be fixed panel-by-panel as each ViewModel is extracted. For now, add a temporary static shim to maintain backward compatibility:

```csharp
// Temporary backward compatibility — remove when all panels are migrated
public static class AppSettingsServiceStatic
{
    private static readonly AppSettingsService _instance = new();
    public static AppSettings Load() => _instance.Load();
    public static void Save(AppSettings settings) => _instance.Save(settings);
    public static void MarkSetupComplete(string s, string c, string m) => _instance.MarkSetupComplete(s, c, m);
    public static void Reset() => _instance.Reset();
}
```

Then find-and-replace all existing `AppSettingsService.Load()` calls to `AppSettingsServiceStatic.Load()`, `AppSettingsService.Save(...)` to `AppSettingsServiceStatic.Save(...)`, etc. throughout the codebase. This ensures the app compiles and runs during incremental migration.

- [ ] **Step 2: De-static ToastService**

Read `TabgInstaller.Gui/Services/ToastService.cs`. Make these changes:
- Add `: IToastService` to the class declaration
- Keep the `Initialize` method on the concrete class (not on the interface)
- Keep all existing method logic

Add a temporary static shim:
```csharp
// Temporary backward compatibility — remove when all panels are migrated
public static class ToastServiceStatic
{
    public static ToastService Instance { get; } = new();
}
```

Then find-and-replace all existing `ToastService.Instance.X()` calls to `ToastServiceStatic.Instance.X()` throughout the codebase.

- [ ] **Step 3: Implement interfaces on Core services**

For each Core service, add the interface to the class declaration. No other changes needed — the methods already match the interfaces.

`ServerProcessService.cs`: Change `public class ServerProcessService : IDisposable` to `public class ServerProcessService : IServerProcessService, IDisposable`

`KnownPlayersService.cs`: Change `public sealed class KnownPlayersService` to `public sealed class KnownPlayersService : IKnownPlayersService`

`BackupService.cs`: Change `public class BackupService` to `public class BackupService : IBackupService`

`UpdateService.cs`: Change `public class UpdateService` to `public class UpdateService : IUpdateService`

- [ ] **Step 4: Build and run existing tests**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet build --verbosity normal && dotnet test --verbosity normal
```
Expected: Build succeeds, all existing tests pass. The static shims ensure backward compatibility.

- [ ] **Step 5: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer && git add -A && git commit -m "feat: implement service interfaces, de-static AppSettingsService and ToastService"
```

---

### Task 4: Wire DI Container and Update MainWindow

**Files:**
- Modify: `TabgInstaller.Gui/App.xaml`
- Modify: `TabgInstaller.Gui/App.xaml.cs`
- Modify: `TabgInstaller.Gui/MainWindow.xaml.cs`

- [ ] **Step 1: Update App.xaml**

Read `TabgInstaller.Gui/App.xaml`. Remove the `Startup="Application_Startup"` attribute from the `<Application>` tag. The file should look like:

```xml
<Application x:Class="TabgInstaller.Gui.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Rewrite App.xaml.cs**

Read `TabgInstaller.Gui/App.xaml.cs`. Replace the contents with:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui
{
    public partial class App : Application
    {
        private IHost _host = null!;

        public App()
        {
            DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "startup.log"),
                        "UNHANDLED: " + args.Exception.ToString() + "\n");
                }
                catch (Exception logEx)
                {
                    Trace.TraceError($"[App] Failed to write crash log: {logEx}");
                }
                MessageBox.Show("Error: " + args.Exception.ToString(), "TABG Manager Error");
                args.Handled = true;
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "startup.log"),
                    $"Starting {DateTime.Now}\n");
            }
            catch (Exception logEx)
            {
                Trace.TraceError($"[App] Failed to write startup log: {logEx}");
            }

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            try
            {
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                try
                {
                    var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "startup.log"),
                        "ERROR: " + ex.ToString() + "\n");
                }
                catch (Exception logEx)
                {
                    Trace.TraceError($"[App] Failed to write startup log: {logEx}");
                }
                MessageBox.Show("Startup error: " + ex.Message, "TABG Manager",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Infrastructure
            services.AddSingleton<IServerPathProvider, ServerPathProvider>();
            services.AddSingleton<IAppSettingsService, AppSettingsService>();
            services.AddSingleton<ToastService>();
            services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());

            // Core services
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<ServerProcessService>();
            services.AddSingleton<IServerProcessService>(sp => sp.GetRequiredService<ServerProcessService>());
            services.AddSingleton<KnownPlayersService>();
            services.AddSingleton<IKnownPlayersService>(sp => sp.GetRequiredService<KnownPlayersService>());
            services.AddSingleton<ConfigValidationService>();
            services.AddTransient<IBackupService>(sp =>
                new BackupService(new Progress<string>(msg =>
                    Debug.WriteLine($"[Backup] {msg}"))));
            services.AddTransient<ModConfigService>();
            services.AddTransient<StarterPackConfigService>();
            services.AddTransient<StarterPackLoadoutService>();
            services.AddTransient<BepInExLoaderService>();

            // Navigation
            services.AddSingleton<INavigationService, NavigationService>();

            // ViewModels — registered as each panel is migrated
            // (added in subsequent tasks)

            // Windows
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
```

- [ ] **Step 3: Create NavigationService implementation**

Create `TabgInstaller.Gui/Services/NavigationService.cs`:

```csharp
using System;

namespace TabgInstaller.Gui.Services
{
    public class NavigationService : INavigationService
    {
        private Action<int>? _navigateCallback;

        public event Action? HardResetRequested;

        public void Initialize(Action<int> navigateCallback)
        {
            _navigateCallback = navigateCallback;
        }

        public void NavigateToTab(int tabIndex)
        {
            _navigateCallback?.Invoke(tabIndex);
        }

        public void RequestHardReset()
        {
            HardResetRequested?.Invoke();
        }
    }
}
```

- [ ] **Step 4: Update MainWindow.xaml.cs for constructor injection**

Read `TabgInstaller.Gui/MainWindow.xaml.cs`. Replace the contents with:

```csharp
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.Windows;

namespace TabgInstaller.Gui
{
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
            // Initialize toast system
            var toast = _services.GetRequiredService<ToastService>();
            toast.Initialize((msg, type, dur) =>
                Dispatcher.Invoke(() => ToastControl.Show(msg, type, dur)));

            // Initialize navigation
            var nav = _services.GetRequiredService<INavigationService>() as NavigationService;
            nav?.Initialize(index => MainTabs.SelectedIndex = index);

            // Wire hard reset
            var navService = _services.GetRequiredService<INavigationService>();
            navService.HardResetRequested += () =>
            {
                // Stop server if running
                var procSvc = _services.GetRequiredService<ServerProcessService>();
                if (procSvc.IsRunning) procSvc.Stop();
                RunSetupWizard();
            };

            // Run update check
            try
            {
                var updater = _services.GetRequiredService<IUpdateService>();
                var updateInfo = await updater.CheckForUpdateAsync();
                if (updateInfo != null)
                {
                    var updateSettings = _appSettings.Load();
                    if (updateInfo.TagName == updateSettings.SkippedUpdateVersion)
                    {
                        // Skipped — don't prompt
                    }
                    else
                    {
                        if (updateSettings.SkippedUpdateVersion != null)
                        {
                            updateSettings.SkippedUpdateVersion = null;
                            _appSettings.Save(updateSettings);
                        }

                        var current = UpdateService.GetCurrentVersion();
                        var dialog = new ChangelogWindow(current, updateInfo.Version,
                            updateInfo.ReleaseNotes, updateInfo.TagName);
                        dialog.Owner = this;

                        if (dialog.ShowDialog() == true)
                        {
                            Title = "TABG Manager — Updating...";
                            bool ok = await updater.ApplyUpdateAsync(updateInfo.DownloadUrl);
                            if (ok)
                            {
                                Application.Current.Shutdown();
                                return;
                            }
                            else
                            {
                                var toastSvc = _services.GetRequiredService<IToastService>();
                                toastSvc.Error("Update failed. You can download manually from GitHub.");
                                Title = "TABG Manager";
                            }
                        }
                        else if (dialog.SkippedVersion != null)
                        {
                            updateSettings.SkippedUpdateVersion = dialog.SkippedVersion;
                            _appSettings.Save(updateSettings);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Failed to check for updates: {ex.Message}");
            }

            // Check if setup is needed
            var settings = _appSettings.Load();
            if (!settings.SetupCompleted || string.IsNullOrEmpty(settings.ServerPath)
                || !Directory.Exists(settings.ServerPath))
            {
                RunSetupWizard();
            }
            else
            {
                InitializeAllPanels(settings.ServerPath);
            }
        }

        private void RunSetupWizard()
        {
            this.Visibility = Visibility.Hidden;

            var wizard = new SetupWizardWindow();
            var result = wizard.ShowDialog();

            this.Visibility = Visibility.Visible;
            this.Activate();

            if (result == true && wizard.SetupCompleted)
            {
                var settings = _appSettings.Load();
                InitializeAllPanels(settings.ServerPath);
            }
            else
            {
                var settings = _appSettings.Load();
                if (!string.IsNullOrEmpty(settings.ServerPath) && Directory.Exists(settings.ServerPath))
                {
                    InitializeAllPanels(settings.ServerPath);
                }
                else
                {
                    var toast = _services.GetRequiredService<IToastService>();
                    toast.Error("Setup was not completed. The app needs a server path to function.");
                    Application.Current.Shutdown();
                }
            }
        }

        private void InitializeAllPanels(string serverDir)
        {
            // Set the server path — triggers all ViewModel initialization via PathChanged
            (_serverPath as ServerPathProvider)?.SetPath(serverDir);

            // Initialize panels that haven't been migrated to MVVM yet
            // (these calls are removed one by one as panels are migrated)
            ConsoleTab.Initialize(serverDir);
            DashboardTab.Initialize(serverDir, ConsoleTab);
            DashboardTab.RequestOpenConsole += () => MainTabs.SelectedIndex = 4;
            ConfigTab.Initialize(serverDir);
            ServerModsTab.Initialize(serverDir);
            BackupsTab.Initialize(serverDir);
            SettingsTab.RequestHardReset += () =>
            {
                if (ConsoleTab.IsServerRunning)
                    ConsoleTab.StopButton_Click(this, new RoutedEventArgs());
                RunSetupWizard();
            };

            MainTabs.SelectedIndex = 0;
        }
    }
}
```

**Note:** `InitializeAllPanels` still calls the old `Initialize()` methods on non-migrated panels. These calls are removed one-by-one in Tasks 5-19 as each panel gets its ViewModel.

- [ ] **Step 5: Build and run to verify app still works**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet build --verbosity normal && dotnet test --verbosity normal
```
Expected: Build succeeds, all existing tests pass. The app starts correctly.

- [ ] **Step 6: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer && git add -A && git commit -m "feat: wire DI container in App.xaml.cs, update MainWindow for constructor injection"
```

---

### Task 5: Migrate SettingsPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/SettingsPanelViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/SettingsPanelViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/SettingsPanel.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/SettingsPanel.xaml`
- Modify: `TabgInstaller.Gui/App.xaml.cs` (register ViewModel)
- Modify: `TabgInstaller.Gui/MainWindow.xaml.cs` (set DataContext)
- Modify: `TabgInstaller.Tests/TabgInstaller.Tests.csproj` (add Gui reference)

- [ ] **Step 1: Add Gui project reference to test project**

This is needed once — subsequent panel tasks don't repeat it.

Edit `TabgInstaller.Tests/TabgInstaller.Tests.csproj` to add after the existing Core project reference:
```xml
<ProjectReference Include="..\TabgInstaller.Gui\TabgInstaller.Gui.csproj" />
```

- [ ] **Step 2: Write SettingsPanelViewModel test**

Create `TabgInstaller.Tests/ViewModels/SettingsPanelViewModelTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class SettingsPanelViewModelTests
    {
        private readonly Mock<IAppSettingsService> _appSettings = new();
        private readonly Mock<INavigationService> _navigation = new();
        private readonly Mock<IServerPathProvider> _serverPath = new();

        private SettingsPanelViewModel CreateSut() =>
            new(_appSettings.Object, _navigation.Object, _serverPath.Object);

        [Fact]
        public void OnServerPathChanged_LoadsSettingsIntoPaths()
        {
            _appSettings.Setup(a => a.Load()).Returns(new AppSettings
            {
                ServerPath = @"C:\Server",
                ClientPath = @"C:\Client",
                ClientModdedPath = @"C:\Modded"
            });
            _serverPath.SetupGet(s => s.ServerPath).Returns(@"C:\Server");

            var sut = CreateSut();
            _serverPath.Raise(s => s.PathChanged += null);

            sut.ServerPath.Should().Be(@"C:\Server");
            sut.ClientPath.Should().Be(@"C:\Client");
            sut.ModdedPath.Should().Be(@"C:\Modded");
        }

        [Fact]
        public void HardResetCommand_CallsResetAndRequestsHardReset()
        {
            var sut = CreateSut();

            sut.HardResetCommand.Execute(null);

            _appSettings.Verify(a => a.Reset(), Times.Once);
            _navigation.Verify(n => n.RequestHardReset(), Times.Once);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~SettingsPanelViewModelTests" --verbosity normal
```
Expected: FAIL — `SettingsPanelViewModel` does not exist.

- [ ] **Step 4: Create SettingsPanelViewModel**

Create `TabgInstaller.Gui/ViewModels/SettingsPanelViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class SettingsPanelViewModel : ObservableObject
    {
        private readonly IAppSettingsService _appSettings;
        private readonly INavigationService _navigation;
        private readonly IServerPathProvider _serverPath;

        [ObservableProperty] private string _serverPath2 = "";
        [ObservableProperty] private string _clientPath = "";
        [ObservableProperty] private string _moddedPath = "";
        [ObservableProperty] private string _appVersion = "";

        public string ServerPath => _serverPath2;

        public SettingsPanelViewModel(
            IAppSettingsService appSettings,
            INavigationService navigation,
            IServerPathProvider serverPath)
        {
            _appSettings = appSettings;
            _navigation = navigation;
            _serverPath = serverPath;
            _serverPath.PathChanged += OnServerPathChanged;

            AppVersion = $"v{UpdateService.GetCurrentVersion()}";
        }

        private void OnServerPathChanged()
        {
            var settings = _appSettings.Load();
            ServerPath2 = settings.ServerPath;
            ClientPath = settings.ClientPath;
            ModdedPath = settings.ClientModdedPath;
        }

        [RelayCommand]
        private void HardReset()
        {
            var result = System.Windows.MessageBox.Show(
                "This will reset all settings and re-run the setup wizard.\n\nAre you sure?",
                "Hard Reset",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _appSettings.Reset();
                _navigation.RequestHardReset();
            }
        }
    }
}
```

**Note on naming conflict:** The ViewModel has a `_serverPath` field (IServerPathProvider) and needs a `ServerPath` property for display. Using `_serverPath2`/`ServerPath2` with a public `ServerPath` alias avoids the conflict. Alternatively, rename the field to `_serverPathProvider` and use `[ObservableProperty] private string _serverPath`. Choose whichever is clearer — the executing agent should use the naming that makes sense. A cleaner approach:

```csharp
private readonly IServerPathProvider _serverPathProvider;

[ObservableProperty] private string _serverPath = "";
[ObservableProperty] private string _clientPath = "";
[ObservableProperty] private string _moddedPath = "";
[ObservableProperty] private string _appVersion = "";

public SettingsPanelViewModel(
    IAppSettingsService appSettings,
    INavigationService navigation,
    IServerPathProvider serverPathProvider)
{
    _appSettings = appSettings;
    _navigation = navigation;
    _serverPathProvider = serverPathProvider;
    _serverPathProvider.PathChanged += OnServerPathChanged;
    AppVersion = $"v{UpdateService.GetCurrentVersion()}";
}
```

Use this naming pattern throughout all ViewModels: `_serverPathProvider` for the injected `IServerPathProvider`.

- [ ] **Step 5: Run tests to verify they pass**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~SettingsPanelViewModelTests" --verbosity normal
```
Expected: 2 tests pass.

- [ ] **Step 6: Register ViewModel in DI and set DataContext**

In `App.xaml.cs`, add to `ConfigureServices` in the ViewModels section:
```csharp
services.AddTransient<SettingsPanelViewModel>();
```

In `MainWindow.xaml.cs`, in `InitializeAllPanels`, REPLACE the `SettingsTab.RequestHardReset` block with:
```csharp
SettingsTab.DataContext = _services.GetRequiredService<SettingsPanelViewModel>();
```

Remove the old `SettingsTab.RequestHardReset += ...` code — the ViewModel now handles hard reset via INavigationService.

- [ ] **Step 7: Update SettingsPanel.xaml for data binding**

Read `TabgInstaller.Gui/Tabs/SettingsPanel.xaml`. Replace Click handlers and x:Name references with bindings:

```xml
<UserControl x:Class="TabgInstaller.Gui.Tabs.SettingsPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:TabgInstaller.Gui.Tabs">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Margin="10">
            <GroupBox Header="Paths" Padding="10">
                <StackPanel>
                    <TextBlock Text="Server Path:"/>
                    <TextBox Text="{Binding ServerPath, Mode=OneWay}" IsReadOnly="True" Margin="0,0,0,5"/>
                    <TextBlock Text="Client Path:"/>
                    <TextBox Text="{Binding ClientPath, Mode=OneWay}" IsReadOnly="True" Margin="0,0,0,5"/>
                    <TextBlock Text="Modded Client Path:"/>
                    <TextBox Text="{Binding ModdedPath, Mode=OneWay}" IsReadOnly="True"/>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Hard Reset" Padding="10" Margin="0,10,0,0">
                <StackPanel>
                    <TextBlock Text="Reset all settings and re-run setup wizard." TextWrapping="Wrap" Margin="0,0,0,5"/>
                    <Button Content="Hard Reset" Command="{Binding HardResetCommand}"
                            Width="120" HorizontalAlignment="Left"/>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Super Secret Settings" Padding="10" Margin="0,10,0,0">
                <local:SuperSecretSettingsPanel/>
            </GroupBox>

            <GroupBox Header="About" Padding="10" Margin="0,10,0,0">
                <TextBlock Text="{Binding AppVersion}"/>
            </GroupBox>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 8: Reduce SettingsPanel.xaml.cs code-behind**

Replace `TabgInstaller.Gui/Tabs/SettingsPanel.xaml.cs` contents with:

```csharp
using System.Windows.Controls;

namespace TabgInstaller.Gui.Tabs
{
    public partial class SettingsPanel : UserControl
    {
        public SettingsPanel()
        {
            InitializeComponent();
        }
    }
}
```

Remove the `RequestHardReset` event, `OnLoaded`, and `HardReset_Click` methods — they now live in the ViewModel.

- [ ] **Step 9: Build and run all tests**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet build --verbosity normal && dotnet test --verbosity normal
```
Expected: Build succeeds, all tests pass.

- [ ] **Step 10: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer && git add -A && git commit -m "feat: migrate SettingsPanel to MVVM"
```

---

### Task 6: Migrate DashboardPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/DashboardViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/DashboardViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/DashboardPanel.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/DashboardPanel.xaml`
- Modify: `TabgInstaller.Gui/App.xaml.cs` (register ViewModel)
- Modify: `TabgInstaller.Gui/MainWindow.xaml.cs` (set DataContext, remove old Initialize call)

- [ ] **Step 1: Write DashboardViewModel test**

Create `TabgInstaller.Tests/ViewModels/DashboardViewModelTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class DashboardViewModelTests
    {
        private readonly Mock<IServerProcessService> _procSvc = new();
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IAppSettingsService> _appSettings = new();
        private readonly Mock<INavigationService> _navigation = new();
        private readonly Mock<IToastService> _toast = new();

        private DashboardViewModel CreateSut() =>
            new(_procSvc.Object, _serverPath.Object, _appSettings.Object,
                _navigation.Object, _toast.Object);

        [Fact]
        public void IsServerRunning_DelegatesToProcessService()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning.Should().BeTrue();
        }

        [Fact]
        public void StartStopCommand_WhenNotRunning_StartsServer()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            var sut = CreateSut();

            sut.StartStopCommand.Execute(null);

            _procSvc.Verify(p => p.Start(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void StartStopCommand_WhenRunning_StopsServer()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();

            sut.StartStopCommand.Execute(null);

            _procSvc.Verify(p => p.Stop(), Times.Once);
        }

        [Fact]
        public void OpenFullConsoleCommand_NavigatesToConsoleTab()
        {
            var sut = CreateSut();
            sut.OpenFullConsoleCommand.Execute(null);
            _navigation.Verify(n => n.NavigateToTab(4), Times.Once);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~DashboardViewModelTests" --verbosity normal
```
Expected: FAIL — `DashboardViewModel` does not exist.

- [ ] **Step 3: Create DashboardViewModel**

Read `TabgInstaller.Gui/Tabs/DashboardPanel.xaml.cs` to understand the full logic, then create `TabgInstaller.Gui/ViewModels/DashboardViewModel.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IServerProcessService _procSvc;
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IAppSettingsService _appSettings;
        private readonly INavigationService _navigation;
        private readonly IToastService _toast;
        private Timer? _refreshTimer;

        [ObservableProperty] private string _previewText = "";
        [ObservableProperty] private string _startStopButtonText = "Start Server";

        public bool IsServerRunning => _procSvc.IsRunning;

        public DashboardViewModel(
            IServerProcessService procSvc,
            IServerPathProvider serverPathProvider,
            IAppSettingsService appSettings,
            INavigationService navigation,
            IToastService toast)
        {
            _procSvc = procSvc;
            _serverPathProvider = serverPathProvider;
            _appSettings = appSettings;
            _navigation = navigation;
            _toast = toast;
            _serverPathProvider.PathChanged += OnServerPathChanged;
        }

        private void OnServerPathChanged()
        {
            _refreshTimer?.Dispose();
            _refreshTimer = new Timer(2000);
            _refreshTimer.Elapsed += (_, _) => RefreshPreview();
            _refreshTimer.Start();
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            PreviewText = _procSvc.GetRecentText(20);
            StartStopButtonText = _procSvc.IsRunning ? "Stop Server" : "Start Server";
            OnPropertyChanged(nameof(IsServerRunning));
        }

        [RelayCommand]
        private void StartStop()
        {
            if (_procSvc.IsRunning)
            {
                _procSvc.Stop();
            }
            else
            {
                try
                {
                    _procSvc.Start();
                }
                catch (Exception ex)
                {
                    _toast.Error($"Failed to start: {ex.Message}");
                }
            }
            RefreshPreview();
        }

        [RelayCommand]
        private void LaunchClient()
        {
            var settings = _appSettings.Load();
            var clientPath = settings.ClientModdedPath;
            if (string.IsNullOrEmpty(clientPath))
                clientPath = settings.ClientPath;

            if (string.IsNullOrEmpty(clientPath))
            {
                _toast.Warning("No client path configured. Check Settings.");
                return;
            }

            var exe = Path.Combine(clientPath, "TABG.exe");
            if (!File.Exists(exe))
            {
                _toast.Warning($"TABG.exe not found at {clientPath}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = clientPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to launch: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenServerFolder()
        {
            Process.Start("explorer", _serverPathProvider.ServerPath);
        }

        [RelayCommand]
        private void OpenLogs()
        {
            var logDir = Path.Combine(_serverPathProvider.ServerPath, "BepInEx");
            if (!Directory.Exists(logDir)) logDir = _serverPathProvider.ServerPath;
            Process.Start("explorer", logDir);
        }

        [RelayCommand]
        private void OpenConfigs()
        {
            Process.Start("explorer", _serverPathProvider.ServerPath);
        }

        [RelayCommand]
        private void OpenFullConsole()
        {
            _navigation.NavigateToTab(4);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~DashboardViewModelTests" --verbosity normal
```
Expected: 4 tests pass.

- [ ] **Step 5: Register ViewModel in DI, set DataContext, update XAML and code-behind**

In `App.xaml.cs` `ConfigureServices`, add:
```csharp
services.AddTransient<DashboardViewModel>();
```

In `MainWindow.xaml.cs` `InitializeAllPanels`, REPLACE the `DashboardTab.Initialize(...)` and `DashboardTab.RequestOpenConsole` lines with:
```csharp
DashboardTab.DataContext = _services.GetRequiredService<DashboardViewModel>();
```

Read `TabgInstaller.Gui/Tabs/DashboardPanel.xaml` and update all `Click="..."` attributes to `Command="{Binding ...Command}"`. Replace `x:Name` text references with `Text="{Binding PropertyName}"`.

Reduce `DashboardPanel.xaml.cs` to:
```csharp
using System.Windows.Controls;

namespace TabgInstaller.Gui.Tabs
{
    public partial class DashboardPanel : UserControl
    {
        public DashboardPanel()
        {
            InitializeComponent();
        }
    }
}
```

Remove the old `Initialize`, `RequestOpenConsole` event, refresh timer, and all click handlers.

- [ ] **Step 6: Build and run all tests**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet build --verbosity normal && dotnet test --verbosity normal
```

- [ ] **Step 7: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer && git add -A && git commit -m "feat: migrate DashboardPanel to MVVM"
```

---

### Task 7: Migrate AdminPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/AdminPanelViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/AdminPanelViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/AdminPanel.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/AdminPanel.xaml`
- Modify: `TabgInstaller.Gui/App.xaml.cs`
- Modify: `TabgInstaller.Gui/MainWindow.xaml.cs`

- [ ] **Step 1: Write AdminPanelViewModel test**

Create `TabgInstaller.Tests/ViewModels/AdminPanelViewModelTests.cs`:

```csharp
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
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
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";

            sut.AddAdminCommand.Execute(null);

            sut.Admins.Should().ContainSingle(a => a.EpicId == "EPIC123");
        }

        [Fact]
        public void AddAdmin_EmptyName_ShowsWarning()
        {
            var sut = CreateSut();
            sut.SelectedPlayerName = "";

            sut.AddAdminCommand.Execute(null);

            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
            sut.Admins.Should().BeEmpty();
        }

        [Fact]
        public void AddAdmin_DuplicateEpicId_ShowsWarning()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            sut.SelectedPlayerName = "Player1";

            sut.AddAdminCommand.Execute(null);

            sut.Admins.Should().HaveCount(1);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void RemoveAdmin_RemovesSelectedAdmin()
        {
            _knownPlayers.Setup(k => k.ResolveEpicId("Player1")).Returns("EPIC123");
            var sut = CreateSut();
            sut.SelectedPlayerName = "Player1";
            sut.AddAdminCommand.Execute(null);
            var admin = sut.Admins[0];

            sut.RemoveAdminCommand.Execute(admin);

            sut.Admins.Should().BeEmpty();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~AdminPanelViewModelTests" --verbosity normal
```

- [ ] **Step 3: Create AdminPanelViewModel**

Read `TabgInstaller.Gui/Tabs/AdminPanel.xaml.cs` for full logic. Create `TabgInstaller.Gui/ViewModels/AdminPanelViewModel.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class AdminPanelViewModel : ObservableObject
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IKnownPlayersService _knownPlayers;
        private readonly IToastService _toast;

        [ObservableProperty] private ObservableCollection<AdminEntry> _admins = new();
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private string _selectedPlayerName = "";
        [ObservableProperty] private ObservableCollection<string> _knownPlayerNames = new();
        [ObservableProperty] private int _selectedPermLevel = 3;
        [ObservableProperty] private string _manualName = "";
        [ObservableProperty] private string _manualEpicId = "";
        [ObservableProperty] private int _manualPermLevel = 3;

        public AdminPanelViewModel(
            IServerPathProvider serverPathProvider,
            IKnownPlayersService knownPlayers,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _knownPlayers = knownPlayers;
            _toast = toast;
            _serverPathProvider.PathChanged += OnServerPathChanged;
        }

        private void OnServerPathChanged()
        {
            RefreshKnownPlayersInternal();
            LoadAdmins();
        }

        private string GetPermsPath() =>
            Path.Combine(_serverPathProvider.ServerPath, "PlayerPerms.json");

        private void RefreshKnownPlayersInternal()
        {
            var count = _knownPlayers.ScanGuestbooks(_serverPathProvider.ServerPath);
            var names = _knownPlayers.GetPlayerNames();
            KnownPlayerNames = new ObservableCollection<string>(names);
            if (count > 0)
                StatusText = $"Found {names.Count} known players ({count} new)";
        }

        private void LoadAdmins()
        {
            Admins.Clear();
            var path = GetPermsPath();
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var root = JsonSerializer.Deserialize<PlayerPermsRoot[]>(json);
                if (root == null || root.Length == 0) return;

                var section = root[0];
                if (section.Players == null) return;

                foreach (var p in section.Players)
                {
                    if (string.IsNullOrWhiteSpace(p.Epic)) continue;
                    Admins.Add(new AdminEntry
                    {
                        Name = p.Name ?? "",
                        EpicId = p.Epic ?? "",
                        PermLevel = p.PermLevel
                    });
                }
                StatusText = $"Loaded {Admins.Count} admin(s)";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RefreshPlayers()
        {
            RefreshKnownPlayersInternal();
            _toast.Info($"Scanned Guestbooks — {_knownPlayers.Players.Count} players known");
        }

        [RelayCommand]
        private void AddAdmin()
        {
            var playerName = SelectedPlayerName?.Trim();
            if (string.IsNullOrEmpty(playerName))
            {
                _toast.Warning("Please select or type a player name.");
                return;
            }

            var epicId = _knownPlayers.ResolveEpicId(playerName);
            if (epicId == null)
            {
                _toast.Warning($"Player '{playerName}' not found in Guestbooks. Use manual entry below.");
                return;
            }

            if (Admins.Any(a => a.EpicId.Equals(epicId, StringComparison.OrdinalIgnoreCase)))
            {
                _toast.Warning($"'{playerName}' is already an admin.");
                return;
            }

            var level = SelectedPermLevel + 1;
            Admins.Add(new AdminEntry { Name = playerName, EpicId = epicId, PermLevel = level });
            SelectedPlayerName = "";
            StatusText = $"Added {playerName}";
        }

        [RelayCommand]
        private void AddManualAdmin()
        {
            var name = ManualName.Trim();
            var epicId = ManualEpicId.Trim();
            var level = ManualPermLevel + 1;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(epicId))
            {
                _toast.Warning("Please enter both a name and an Epic ID.");
                return;
            }

            if (Admins.Any(a => a.EpicId.Equals(epicId, StringComparison.OrdinalIgnoreCase)))
            {
                _toast.Warning($"'{name}' is already an admin.");
                return;
            }

            Admins.Add(new AdminEntry { Name = name, EpicId = epicId, PermLevel = level });
            ManualName = "";
            ManualEpicId = "";
            StatusText = $"Added {name} (manual)";
        }

        [RelayCommand]
        private void RemoveAdmin(AdminEntry entry)
        {
            if (entry != null)
            {
                Admins.Remove(entry);
                StatusText = $"Removed {entry.Name}";
            }
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                var players = Admins.Select(a => new PlayerPermsPlayer
                {
                    Name = a.Name,
                    Epic = a.EpicId,
                    PermLevel = a.PermLevel
                }).ToArray();

                var root = new[]
                {
                    new PlayerPermsRoot
                    {
                        Name = "players",
                        Description = "List of players with modified permission level. Default permission level is 0.",
                        Players = players
                    }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(root, options);
                File.WriteAllText(GetPermsPath(), json);
                _toast.Success("Admins saved. Restart server to apply changes.");
                StatusText = $"Saved {Admins.Count} admin(s)";
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to save admins: {ex.Message}");
            }
        }
    }

    public class AdminEntry
    {
        public string Name { get; set; } = "";
        public string EpicId { get; set; } = "";
        public int PermLevel { get; set; } = 4;
    }

    internal class PlayerPermsRoot
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "players";
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
        [JsonPropertyName("players")]
        public PlayerPermsPlayer[]? Players { get; set; }
    }

    internal class PlayerPermsPlayer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("epic")]
        public string Epic { get; set; } = "";
        [JsonPropertyName("permlevel")]
        public int PermLevel { get; set; } = 4;
    }
}
```

- [ ] **Step 4: Run tests, register ViewModel, update XAML and code-behind**

Run tests:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~AdminPanelViewModelTests" --verbosity normal
```

Register in `App.xaml.cs`: `services.AddTransient<AdminPanelViewModel>();`

Read `TabgInstaller.Gui/Tabs/AdminPanel.xaml`. Replace all `Click="..."` with `Command="{Binding ...Command}"`, replace `x:Name` element references with `{Binding}`. Replace `LstAdmins.ItemsSource` code-behind with `ItemsSource="{Binding Admins}"`.

Reduce `AdminPanel.xaml.cs` to constructor-only.

Update `MainWindow.xaml.cs` — in `InitializeAllPanels`, the AdminPanel is a child of ConfigPanel. When ConfigPanel is migrated (Task 12), set `AdminPanelControl.DataContext`. For now, add:
```csharp
// AdminPanel is nested inside ConfigPanel — set its DataContext directly
var adminPanel = FindChildByType<Tabs.AdminPanel>(ConfigTab);
if (adminPanel != null)
    adminPanel.DataContext = _services.GetRequiredService<AdminPanelViewModel>();
```

Or simpler: set it after ConfigTab initialization. The exact approach depends on how the panels are nested — the executing agent should read `ConfigPanel.xaml` to find the `AdminPanelControl` reference.

- [ ] **Step 5: Build and run all tests**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet build --verbosity normal && dotnet test --verbosity normal
```

- [ ] **Step 6: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer && git add -A && git commit -m "feat: migrate AdminPanel to MVVM"
```

---

### Tasks 8-19: Remaining Panel Migrations

Each of the following tasks follows the same structure as Tasks 5-7. For each panel:

1. Write failing ViewModel tests (2-3 key scenarios)
2. Run tests to verify failure
3. Create the ViewModel class (inherit `ObservableObject`, inject services, use `[ObservableProperty]` and `[RelayCommand]`)
4. Run tests to verify pass
5. Register ViewModel in `App.xaml.cs` ConfigureServices
6. Set DataContext in `MainWindow.xaml.cs` InitializeAllPanels (remove old Initialize call)
7. Update XAML bindings (Click → Command, x:Name → {Binding})
8. Reduce code-behind to constructor-only (keep View-only concerns: animations, dialogs, scroll management)
9. Build + run all tests
10. Commit

Below are the specifications for each ViewModel — exact properties, commands, constructor dependencies, and key logic patterns.

---

### Task 8: Migrate SuperSecretSettingsPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/SuperSecretSettingsViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/SuperSecretSettingsViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/SuperSecretSettingsPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class SuperSecretSettingsViewModel : ObservableObject
{
    // No service dependencies — this panel is self-contained

    [ObservableProperty] private string _passwordInput = "";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isUnlocked;

    private SigmaModeApp? _sigmaMode;

    [RelayCommand]
    private async Task Enter()
    {
        // Logic from EnterButton_Click:
        // - Check password (constant "123")
        // - If correct, set IsUnlocked = true, start SigmaModeApp
        // - Update StatusText and IsRunning
    }

    [RelayCommand]
    private void Stop()
    {
        // Logic from StopButton_Click:
        // - Stop SigmaModeApp, set IsRunning = false
    }
}
```

Read `SuperSecretSettingsPanel.xaml.cs` for the full `EnterButton_Click` async logic (lines 19-117) and translate to the ViewModel. The `SigmaModeApp` creation and management moves entirely to the ViewModel.

**Key tests:**
- `Enter_WrongPassword_DoesNotUnlock()`
- `Enter_CorrectPassword_SetsIsUnlocked()`
- `Stop_SetsIsRunningFalse()`

---

### Task 9: Migrate MatchSettingsPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/MatchSettingsViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/MatchSettingsViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/MatchSettingsPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class MatchSettingsViewModel : ObservableObject
{
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IToastService _toast;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _saving;

    // Properties from MatchSettingsPanel UI controls — read the XAML to get the exact list.
    // These typically include:
    [ObservableProperty] private string _winCondition = "Last Man Standing";
    [ObservableProperty] private bool _showKillsToWin;
    [ObservableProperty] private int _killsToWin = 20;
    [ObservableProperty] private int _numberOfLives = 1;
    [ObservableProperty] private string _statusText = "";
    // ... additional properties from the StarterPackTextSettings model

    public MatchSettingsViewModel(IServerPathProvider serverPathProvider, IToastService toast) { ... }

    [RelayCommand] private void Save() { /* logic from SaveButton_Click */ }

    partial void OnWinConditionChanged(string value) => UpdateKillsToWinVisibility();
    private void UpdateKillsToWinVisibility() { ShowKillsToWin = WinCondition == "Kills"; }
}
```

Read `MatchSettingsPanel.xaml.cs` (198 lines) for full LoadSettings and SaveButton_Click logic. The file watcher setup (lines 32-51) and debounce timer move to the ViewModel. Config I/O uses `StarterPackConfigService`.

**Key tests:**
- `OnWinConditionChanged_ToKills_ShowsKillsToWin()`
- `Save_WritesSettingsToFile()`

---

### Task 10: Migrate BackupsPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/BackupsPanelViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/BackupsPanelViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/BackupsPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class BackupsPanelViewModel : ObservableObject
{
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IBackupService _backupService;
    private readonly IToastService _toast;

    [ObservableProperty] private ObservableCollection<BackupInfo> _backups = new();
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isCreatingBackup;

    public BackupsPanelViewModel(
        IServerPathProvider serverPathProvider,
        IBackupService backupService,
        IToastService toast) { ... }

    [RelayCommand] private async Task CreateBackup() { /* logic from CreateBackup_Click */ }
    [RelayCommand] private async Task RestoreBackup(BackupInfo backup) { /* logic from RestoreBackup_Click */ }
    [RelayCommand] private void DeleteBackup(BackupInfo backup) { /* logic from DeleteBackup_Click */ }
    [RelayCommand] private void RefreshBackups() { /* calls LoadBackups() */ }
}
```

Read `BackupsPanel.xaml.cs` (237 lines). The `CreateBackupCard` method (lines 54-106) creates UI programmatically — this is replaced by an `ItemsControl` with a `DataTemplate` in XAML binding to the `Backups` collection. Each `BackupInfo` item gets buttons via the DataTemplate with commands bound to `RestoreBackupCommand` and `DeleteBackupCommand` using `CommandParameter="{Binding}"`.

**Key tests:**
- `CreateBackup_SetsIsCreatingBackup_ThenClearsIt()`
- `DeleteBackup_RemovesFromCollection()`
- `RefreshBackups_LoadsFromService()`

---

### Task 11: Migrate RingSpawnsPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/RingSpawnsViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/RingSpawnsViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/RingSpawnsPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class RingSpawnsViewModel : ObservableObject
{
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IToastService _toast;
    private FileSystemWatcher? _watcherRoot;
    private FileSystemWatcher? _watcherCfg;
    private Timer? _debounce;
    private bool _saving;

    [ObservableProperty] private string _ringSizes = "";
    [ObservableProperty] private string _ringSpeeds = "";
    [ObservableProperty] private string _baseRingTime = "";
    [ObservableProperty] private string _timeBeforeFirstRing = "";
    [ObservableProperty] private string _selectedLocation = "";
    [ObservableProperty] private bool _useCustomSpawn;
    [ObservableProperty] private string _matchSpawns = "";
    [ObservableProperty] private string _spawnCountText = "";
    [ObservableProperty] private string _statusText = "";

    public RingSpawnsViewModel(IServerPathProvider serverPathProvider, IToastService toast) { ... }

    [RelayCommand] private void Save() { /* logic from SaveButton_Click lines 108-148 */ }
    [RelayCommand] private void ApplyLocation() { /* logic from ApplyLocation_Click lines 158-174 */ }
    [RelayCommand] private void PresetDeathmatch() { /* logic from PresetDeathmatch_Click lines 176-185 */ }
    [RelayCommand] private void PresetStandardBR() { /* logic from PresetStandardBR_Click lines 187-192 */ }
}
```

Read `RingSpawnsPanel.xaml.cs` (242 lines). File watcher and debounce logic (lines 34-70) move to ViewModel. Static helpers `SpawnPointsToMultiline` and `MultilineToSpawnPoints` (lines 218-240) become private methods on the ViewModel.

**Key tests:**
- `PresetDeathmatch_SetsExpectedValues()`
- `UseCustomSpawn_Changed_UpdatesSpawnCount()`

---

### Task 12: Migrate ConfigPanel and Update Existing ViewModels

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/ConfigViewModel.cs`
- Create: `TabgInstaller.Gui/ViewModels/PresetsViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/ConfigViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/ConfigPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/PresetsGrid.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/ViewModels/GameSettingsDynamicViewModel.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`
- Modify: `TabgInstaller.Gui/MainWindow.xaml.cs`

**ConfigViewModel specification:**

```csharp
public partial class ConfigViewModel : ObservableObject
{
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IToastService _toast;
    private readonly ConfigValidationService _validationService;
    private FileSystemWatcher? _gameSettingsWatcher;
    private DateTime _lastWriteTime = DateTime.MinValue;
    private System.Timers.Timer? _autoSaveTimer;

    [ObservableProperty] private GameSettingsDynamicViewModel? _gameSettingsVm;
    [ObservableProperty] private string _statusText = "";

    public ConfigViewModel(
        IServerPathProvider serverPathProvider,
        IToastService toast,
        ConfigValidationService validationService) { ... }

    [RelayCommand] private void Save() { /* logic from SaveButton_Click */ }
    [RelayCommand] private void OpenGameSettings() { /* logic from OpenGameSettings_Click */ }
    [RelayCommand] private void OpenServerFolder() { /* Process.Start("explorer", ...) */ }
    [RelayCommand] private void OpenLogs() { /* Process.Start("explorer", ...) */ }
    [RelayCommand] private void OpenConfigs() { /* Process.Start("explorer", ...) */ }
}
```

**GameSettingsDynamicViewModel update:**
- Change `public class GameSettingsDynamicViewModel : INotifyPropertyChanged` to `public partial class GameSettingsDynamicViewModel : ObservableObject`
- Remove the manual `PropertyChanged` event and `OnPropertyChanged` method (inherited from `ObservableObject`)
- Inject `ConfigValidationService` instead of `new()`-ing it: `public GameSettingsDynamicViewModel(GameSettingsData model, ConfigValidationService validationService)`
- Keep `ShowAdvanced` as a manual property with `OnPropertyChanged()` calls (it has complex side effects)

**StarterPackDynamicViewModel update:**
- Same treatment: change to `public partial class StarterPackDynamicViewModel : ObservableObject`
- Remove manual `INotifyPropertyChanged` implementation
- Inject any services it creates via `new()` through constructor parameters instead

**ServerSettingsViewModel absorption:**
- `ServerSettingsViewModel` (38 lines) is a thin wrapper with 3 properties (`ServerName`, `Port`, `MaxPlayers`). It is absorbed into `ConfigViewModel` — its properties become `[ObservableProperty]` fields on `ConfigViewModel`, or `GameSettingsDynamicViewModel` already exposes them. Remove `ServerSettingsViewModel.cs` after verifying no remaining references.

**PresetsViewModel specification:**

```csharp
public partial class PresetsViewModel : ObservableObject
{
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IToastService _toast;

    [ObservableProperty] private ObservableCollection<string> _presetNames = new();
    [ObservableProperty] private string? _selectedPreset;
    [ObservableProperty] private ObservableCollection<FileEntry> _fileEntries = new();

    [RelayCommand] private void ApplyTemplate() { /* logic from ApplyTemplate_Click */ }
    [RelayCommand] private void SavePreset() { /* logic from SavePreset_Click */ }
    [RelayCommand] private void LoadPreset() { /* logic from LoadPreset_Click */ }
    [RelayCommand] private void DeletePreset() { /* logic from DeletePreset_Click */ }
}
```

Read `ConfigPanel.xaml.cs` (237 lines) and `PresetsGrid.xaml.cs` (199 lines) for all logic. The auto-save timer and file watcher setup move to ConfigViewModel. Sub-panel DataContext assignment happens in MainWindow after ConfigPanel is loaded.

**Key tests:**
- `Save_WritesGameSettings()`
- `AutoSave_TriggeredOnPropertyChange()`
- `FileWatcher_ReloadsSettings()`

---

### Task 13: Migrate ServerModsPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/ServerModsViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/ServerModsViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/ServerModsPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class ServerModsViewModel : ObservableObject
{
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IToastService _toast;

    [ObservableProperty] private ObservableCollection<PluginEntry> _plugins = new();
    [ObservableProperty] private ObservableCollection<BundledEntry> _availableMods = new();
    [ObservableProperty] private string _statusText = "";

    public ServerModsViewModel(IServerPathProvider serverPathProvider, IToastService toast) { ... }

    [RelayCommand] private void TogglePlugin(PluginEntry plugin) { /* rename/enable logic */ }
    [RelayCommand] private void InstallBundled() { /* logic from InstallBundled_Click */ }
    [RelayCommand] private void RemovePlugin(PluginEntry plugin) { /* logic from RemovePlugin_Click */ }
    [RelayCommand] private void Refresh() { /* calls RefreshAll() */ }
    [RelayCommand] private void OpenFolder() { /* Process.Start("explorer", ...) */ }

    // AddDll requires OpenFileDialog — keep a thin Click handler in code-behind
    // that opens the dialog and calls ViewModel.AddDll(filePath)
    public void AddDll(string filePath) { /* logic from AddDll_Click */ }
}

public class PluginEntry : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isEnabled;
}

public class BundledEntry : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isSelected;
}
```

Read `ServerModsPanel.xaml.cs` (273 lines). The `LoadPluginsList` (lines 38-63), `LoadAvailableList` (lines 76-128), and plugin toggle logic (lines 130-163) move to ViewModel. `KnownServerPlugins` array (line 66-74) becomes a private field on the ViewModel.

**Code-behind retains:** `AddDll_Click` handler that opens `OpenFileDialog` and passes result to ViewModel.

**Key tests:**
- `Refresh_LoadsPluginsAndAvailableMods()`
- `TogglePlugin_RenamesFile()`

---

### Task 14: Migrate ConsolePanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/ConsolePanelViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/ConsolePanelViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/ConsolePanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class ConsolePanelViewModel : ObservableObject
{
    private readonly IServerProcessService _procSvc;
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IToastService _toast;

    [ObservableProperty] private bool _showInfo = true;
    [ObservableProperty] private bool _showWarning = true;
    [ObservableProperty] private bool _showError = true;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _commandText = "";
    [ObservableProperty] private bool _isServerRunning;

    public ObservableCollection<LogEntry> LogEntries => _procSvc.LogEntries;

    public ConsolePanelViewModel(
        IServerProcessService procSvc,
        IServerPathProvider serverPathProvider,
        IToastService toast) { ... }

    [RelayCommand] private void Start() { /* _procSvc.Start(); update IsServerRunning */ }
    [RelayCommand] private void Stop() { /* _procSvc.Stop(); update IsServerRunning */ }
    [RelayCommand] private void ClearConsole() { /* _procSvc.ClearEntries() */ }
    [RelayCommand] private void QuickSaveRestart() { /* logic from QuickSaveRestart_Click */ }
    [RelayCommand] private void SendCommand() { /* logic from SendCommand_Click */ }

    // Export needs SaveFileDialog — keep thin code-behind handler
    public void ExportLog(string filePath) { /* logic from ExportButton_Click */ }

    // Filter predicate — used by CollectionViewSource in View
    public bool FilterLogEntry(LogEntry entry)
    {
        bool accepted = entry.Severity switch
        {
            LogSeverity.Info => ShowInfo,
            LogSeverity.Warning => ShowWarning,
            LogSeverity.Error => ShowError,
            _ => true
        };
        if (accepted && !string.IsNullOrEmpty(SearchText))
            accepted = entry.RawText.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        return accepted;
    }
}
```

Read `ConsolePanel.xaml.cs` (295 lines). The `CollectionViewSource` filter stays in the View (code-behind), but it calls `ViewModel.FilterLogEntry()`. Auto-scroll detection (lines 147-154) stays in code-behind. Search debounce timer can move to ViewModel or stay in View.

**Code-behind retains:**
- `CollectionViewSource.Filter` handler that delegates to ViewModel
- Auto-scroll detection on ScrollViewer
- `ExportButton_Click` that opens SaveFileDialog and calls `ViewModel.ExportLog(path)`

**Key tests:**
- `Start_CallsProcessServiceStart()`
- `FilterLogEntry_HidesInfoWhenDisabled()`
- `FilterLogEntry_MatchesSearchText()`
- `SendCommand_AddsLogEntries()`

---

### Task 15: Migrate ModSettingsPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/ModSettingsViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/ModSettingsViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/ModSettingsPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class ModSettingsViewModel : ObservableObject
{
    private readonly IServerPathProvider _serverPathProvider;
    private readonly IToastService _toast;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _saving;

    // Many observable properties for mod settings — read ModSettingsPanel.xaml.cs LoadSettings()
    // (lines 63-176) to identify all settings fields. These include booleans, strings, and
    // int values for commission, custom grenades, proxy chat, fake players, etc.
    [ObservableProperty] private bool _commissionEnabled;
    [ObservableProperty] private string _commissionRate = "";
    // ... (20+ properties — extract from LoadSettings and SaveButton_Click)
    [ObservableProperty] private ObservableCollection<GameItem> _grenades = new();
    [ObservableProperty] private string _statusText = "";

    public ModSettingsViewModel(IServerPathProvider serverPathProvider, IToastService toast) { ... }

    [RelayCommand] private void Save()
    {
        // Logic from SaveButton_Click (lines 178-329) — reads all properties,
        // writes to .cfg files via ModConfigService or direct file I/O
    }
}
```

Read `ModSettingsPanel.xaml.cs` (350 lines). This is one of the more complex panels. `LoadSettings` (lines 63-176) reads multiple .cfg files and populates many UI controls. `SaveButton_Click` (lines 178-329) reverses the process. All of this logic moves to the ViewModel, using the same file I/O patterns but referencing `_serverPathProvider.ServerPath` instead of `_serverDir`.

**Key tests:**
- `Save_WritesCfgFiles()`
- `LoadSettings_PopulatesProperties()`

---

### Task 16: Migrate InstallerPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/InstallerPanelViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/InstallerPanelViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/InstallerPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class InstallerPanelViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettings;
    private readonly IToastService _toast;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _serverPath = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private bool _canContinue;
    [ObservableProperty] private bool _uiEnabled = true;
    [ObservableProperty] private ObservableCollection<PluginSelection> _pluginSelections = new();

    public InstallerPanelViewModel(IAppSettingsService appSettings, IToastService toast) { ... }

    [RelayCommand] private async Task Install() { /* logic from BtnInstall_Click lines 105-290 */ }
    [RelayCommand] private void Cancel() { /* _cts?.Cancel() */ }
    [RelayCommand] private void Continue() { /* logic from BtnContinue_Click */ }
    [RelayCommand] private void SelectAll() { /* set all IsSelected = true */ }
    [RelayCommand] private void SelectNone() { /* set all IsSelected = false */ }
    [RelayCommand] private void SelectSigma() { /* logic from SelectSigmaPlugins_Click */ }

    // Browse needs FolderBrowserDialog — code-behind opens dialog, sets ServerPath
    public void SetServerPath(string path) { ServerPath = path; }
}

public partial class PluginSelection : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private bool _isEnabled = true;
}
```

Read `InstallerPanel.xaml.cs` (335 lines). The `BtnInstall_Click` (lines 105-290) is the most complex method — it performs file extraction, BepInEx installation, plugin copying, and config setup. All of this async logic moves to the ViewModel's `Install()` command.

**Code-behind retains:** `Browse_Click` that opens `FolderBrowserDialog` and calls `ViewModel.SetServerPath(path)`.

**Key tests:**
- `Install_SetsIsInstalling_True()`
- `Cancel_CancelsInstallation()`
- `SelectAll_SelectsAllPlugins()`

---

### Task 17: Migrate ClientPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/ClientPanelViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/ClientPanelViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/ClientPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

```csharp
public partial class ClientPanelViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettings;
    private readonly IToastService _toast;

    [ObservableProperty] private string _clientPath = "";
    [ObservableProperty] private string _moddedPath = "";
    [ObservableProperty] private ObservableCollection<ModEntry> _mods = new();
    [ObservableProperty] private ObservableCollection<BundledEntry> _availableMods = new();
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isSettingUp;

    public ClientPanelViewModel(IAppSettingsService appSettings, IToastService toast) { ... }

    [RelayCommand] private void ToggleMod(ModEntry mod) { /* rename/enable logic */ }
    [RelayCommand] private async Task Setup() { /* logic from BtnSetup_Click */ }
    [RelayCommand] private void InstallBundled() { /* logic from InstallBundled_Click */ }
    [RelayCommand] private void RemoveMod(ModEntry mod) { /* logic from RemoveMod_Click */ }
    [RelayCommand] private void Refresh() { /* RefreshAll() */ }
    [RelayCommand] private void Launch() { /* logic from BtnLaunch_Click */ }
    [RelayCommand] private void OpenModdedFolder() { /* Process.Start("explorer", ...) */ }

    public void SetClientPath(string path) { ClientPath = path; /* reload mods */ }
    public void AddDll(string filePath) { /* logic from AddDll_Click */ }
}

public partial class ModEntry : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isEnabled;
}
```

Read `ClientPanel.xaml.cs` (382 lines). Similar structure to ServerModsPanel — plugin management with toggle, install, remove operations.

**Code-behind retains:** `BrowseClient_Click` (OpenFileDialog), `AddDll_Click` (OpenFileDialog).

**Key tests:**
- `Refresh_LoadsModsAndAvailable()`
- `Launch_OpensTabgExe()`

---

### Task 18: Migrate ReferencePanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/ReferencePanelViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/ReferencePanelViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/ReferencePanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

This is the simplest ViewModel — each click handler just sets a large string property.

```csharp
public partial class ReferencePanelViewModel : ObservableObject
{
    [ObservableProperty] private string _referenceText = "";

    [RelayCommand]
    private void ShowCommands()
    {
        // Copy the multiline string from RefCommands_Click (lines 13-50)
        ReferenceText = "=== RCON Commands ===\n...";
    }

    [RelayCommand]
    private void ShowItems()
    {
        // Copy the multiline string from RefItems_Click (lines 52-184)
        ReferenceText = "=== Item IDs ===\n...";
    }

    [RelayCommand]
    private void ShowLoadouts()
    {
        // Copy the multiline string from RefLoadouts_Click (lines 186-228)
        ReferenceText = "=== Loadout Format ===\n...";
    }

    [RelayCommand]
    private void ShowSpawns()
    {
        // Copy the multiline string from RefSpawns_Click (lines 230-301)
        ReferenceText = "=== Spawn Points ===\n...";
    }

    [RelayCommand]
    private void ShowMatchSettings()
    {
        // Copy the multiline string from RefMatchSettings_Click (lines 303-387)
        ReferenceText = "=== Match Settings Reference ===\n...";
    }
}
```

Read `ReferencePanel.xaml.cs` (389 lines). Each click handler just sets `ReferenceTextBox.Text` to a large hardcoded string. Copy these strings verbatim into the ViewModel methods.

**Key tests:**
- `ShowCommands_SetsReferenceText()`
- `ShowItems_SetsReferenceText()`

---

### Task 19: Migrate LoadoutEditorPanel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/LoadoutEditorViewModel.cs`
- Create: `TabgInstaller.Tests/ViewModels/LoadoutEditorViewModelTests.cs`
- Modify: `TabgInstaller.Gui/Tabs/LoadoutEditorPanel.xaml` and `.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`

**ViewModel specification:**

This is the most complex ViewModel (original code-behind: 852 lines). It manages loadouts with items, curses, blessings, and complex editing operations.

```csharp
public partial class LoadoutEditorViewModel : ObservableObject
{
    private readonly IServerPathProvider _serverPathProvider;
    private readonly StarterPackLoadoutService _loadoutSvc;
    private readonly IToastService _toast;
    private FileSystemWatcher? _watcherRoot;
    private FileSystemWatcher? _watcherCfg;
    private Timer? _debounce;
    private bool _saving;
    private bool _suppressDirty;

    [ObservableProperty] private ObservableCollection<LoadoutVm> _loadouts = new();
    [ObservableProperty] private LoadoutVm? _selectedLoadout;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _selectedMode = "Default";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private ObservableCollection<ItemSearchResult> _searchResults = new();
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private ObservableCollection<ItemDisplayRow> _currentItems = new();
    [ObservableProperty] private ObservableCollection<CurseCheckEntry> _currentCurses = new();

    public LoadoutEditorViewModel(
        IServerPathProvider serverPathProvider,
        StarterPackLoadoutService loadoutSvc,
        IToastService toast) { ... }

    [RelayCommand] private void Save() { /* logic from Save_Click lines 216-250 */ }
    [RelayCommand] private void ImportRaw() { /* logic from ImportRaw_Click lines 254-311 */ }
    [RelayCommand] private void ExportRaw() { /* logic from ExportRaw_Click lines 313-359 */ }
    [RelayCommand] private void AddLoadout() { /* logic from AddLoadout_Click lines 363-369 */ }
    [RelayCommand] private void DuplicateLoadout() { /* logic from DuplicateLoadout_Click lines 371-390 */ }
    [RelayCommand] private void RemoveLoadout() { /* logic from RemoveLoadout_Click lines 392-410 */ }
    [RelayCommand] private void MoveUp() { /* logic from MoveUp_Click lines 412-421 */ }
    [RelayCommand] private void MoveDown() { /* logic from MoveDown_Click lines 423-432 */ }
    [RelayCommand] private void AddItem(ItemSearchResult item) { /* logic from AddItem_Click lines 670-710 */ }
    [RelayCommand] private void RemoveItem(ItemDisplayRow item) { /* logic from RemoveItem_Click lines 712-726 */ }

    partial void OnSelectedLoadoutChanged(LoadoutVm? value)
    {
        if (value != null)
        {
            RefreshItemsGrid(value);
            RefreshCurseCheckboxes(value);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // logic from TxtItemSearch_TextChanged lines 596-629
    }
}

// Inner ViewModels — convert from INotifyPropertyChanged to ObservableObject
public partial class LoadoutVm : ObservableObject
{
    [ObservableProperty] private string _name = "New Loadout";
    [ObservableProperty] private int _percent = 100;
    [ObservableProperty] private ObservableCollection<ItemVm> _items = new();
    [ObservableProperty] private List<HashSet<int>> _curses = new();
    public string DisplayName => $"{Name} ({Percent}%)";
}

public class ItemVm
{
    public string Id { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public class ItemDisplayRow
{
    public string ItemName { get; set; } = "";
    public string Id { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public partial class CurseCheckEntry : ObservableObject
{
    [ObservableProperty] private int _curseId;
    [ObservableProperty] private string _displayText = "";
    [ObservableProperty] private bool _isChecked;
}

public class ItemSearchResult
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
```

Read `LoadoutEditorPanel.xaml.cs` (852 lines) for all logic. Key methods to translate:
- `LoadAll()` (lines 104-140) — reads loadout strings and commission settings
- `Save_Click` (lines 216-250) — writes loadout strings and config
- `ImportRaw_Click` (lines 254-311) — parses raw loadout string input
- `ExportRaw_Click` (lines 313-359) — exports loadout as raw string
- Item search filtering (lines 596-629)
- Curse checkbox management (lines 471-540)

**Code-behind retains:**
- DataGrid cell editing events (`DgItems_CellEditEnding`)
- ListBox double-click handler (delegates to ViewModel command)

**Key tests:**
- `AddLoadout_AddsNewLoadoutWithDefaults()`
- `RemoveLoadout_RemovesSelectedLoadout()`
- `DuplicateLoadout_CreatesDeepCopy()`
- `MoveUp_SwapsWithPrevious()`
- `AddItem_AddsToSelectedLoadout()`

---

### Task 20: Remove Static Shims and Final Verification

**Files:**
- Delete: Static shim code in `AppSettingsService.cs` and `ToastService.cs`
- Modify: Any remaining references to `AppSettingsServiceStatic` or `ToastServiceStatic`

- [ ] **Step 1: Search for remaining static shim references**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && grep -r "AppSettingsServiceStatic\|ToastServiceStatic" --include="*.cs" -l
```

If any files still reference the static shims, they are panels that weren't fully migrated. Update them to use injected services.

- [ ] **Step 2: Remove static shim classes**

Delete `AppSettingsServiceStatic` and `ToastServiceStatic` classes from their respective files.

- [ ] **Step 3: Remove GlobalServerPath**

The static `GlobalServerPath` class is now replaced by `IServerPathProvider`. Search for remaining references:
```bash
cd /d/tabginststaller/TABG-Server-Installer && grep -r "GlobalServerPath" --include="*.cs" -l
```

Update any remaining references to use `IServerPathProvider`.

- [ ] **Step 4: Full build and test**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && dotnet build --configuration Release --verbosity normal && dotnet test --configuration Release --verbosity normal
```
Expected: Build succeeds with no errors. All tests pass.

- [ ] **Step 5: Verify all code-behind files are thin**

Check that every Panel.xaml.cs file is reduced to:
- Constructor with `InitializeComponent()`
- Only legitimate View concerns (file dialogs, scroll management, animations)

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer && wc -l TabgInstaller.Gui/Tabs/*.xaml.cs
```
Expected: Most files under 30 lines. ConsolePanel and ServerModsPanel may be ~40-50 lines (file dialog handlers).

- [ ] **Step 6: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer && git add -A && git commit -m "chore: remove static shims, final MVVM migration cleanup"
```
