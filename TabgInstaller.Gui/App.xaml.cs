using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Model;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.Resources;
using TabgInstaller.Gui.ViewModels;

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

                // Crash reporting
                try
                {
                    var settingsService = _host?.Services?.GetService<IAppSettingsService>();
                    var settings = settingsService?.Load();

                    if (settings?.CrashReportingEnabled == null)
                    {
                        // First crash — ask user
                        var dialog = new Windows.CrashReportDialog();
                        dialog.ShowDialog();

                        if (dialog.RememberChoice.HasValue && settingsService != null)
                        {
                            settings!.CrashReportingEnabled = dialog.RememberChoice.Value;
                            settingsService.Save(settings);
                        }

                        if (dialog.SendReport == true)
                        {
                            var crashService = _host?.Services?.GetService<TabgInstaller.Core.Services.ICrashReportService>();
                            if (crashService != null)
                                _ = crashService.ReportCrashAsync(args.Exception);
                        }
                    }
                    else if (settings.CrashReportingEnabled == true)
                    {
                        var crashService = _host?.Services?.GetService<TabgInstaller.Core.Services.ICrashReportService>();
                        if (crashService != null)
                            _ = crashService.ReportCrashAsync(args.Exception);
                    }
                }
                catch (Exception crashEx)
                {
                    Trace.TraceError($"[App] Failed to handle crash report: {crashEx}");
                }

                MessageBox.Show(string.Format(Messages.ErrorPrefix, args.Exception), Messages.TabgManagerError);
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

            // Apply saved language before any UI loads
            try
            {
                var tempSettings = new AppSettingsService();
                var settings = tempSettings.Load();
                LocalizationHelper.ApplyLanguage(settings.Language);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[App] Failed to apply language: {ex}");
            }

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            // Apply high contrast theme if enabled
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            if (themeService.IsHighContrast)
                themeService.SetHighContrast(true);

            // Load plugin registry from cache (instant), then refresh from network in background
            try
            {
                var registryService = _host.Services.GetRequiredService<IRegistryService>();
                var cached = registryService.GetCachedRegistry();
                if (cached?.Plugins != null)
                    PluginRegistry.LoadFromManifests(cached.Plugins);

                // Fire-and-forget: refresh registry from network for next session
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var fresh = await registryService.FetchRegistryAsync();
                        if (fresh?.Plugins != null)
                            PluginRegistry.LoadFromManifests(fresh.Plugins);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError($"[App] Background registry refresh failed: {ex}");
                    }
                });
            }
            catch (Exception regEx)
            {
                Trace.TraceError($"[App] Failed to load plugin registry: {regEx}");
            }

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
                MessageBox.Show(string.Format(Messages.StartupError, ex.Message), Messages.TabgManager,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Infrastructure
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TabgInstaller");
            services.AddSingleton<IAppSettingsService, AppSettingsService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<ToastService>(_ => ToastService.Instance);
            services.AddSingleton<IToastService>(_ => ToastService.Instance);

            // Multi-server instance management
            services.AddSingleton<IServerInstanceManager>(sp => new ServerInstanceManager(settingsDir));
            services.AddSingleton<IActiveInstanceService, ActiveInstanceService>();
            services.AddSingleton<ICredentialStorageService>(sp => new CredentialStorageService(settingsDir));

            // Keep IServerPathProvider as adapter during migration
            services.AddSingleton<IServerPathProvider>(sp =>
            {
                var adapter = new ServerPathProvider();
                var active = sp.GetRequiredService<IActiveInstanceService>();
                active.PathChanged += () => adapter.SetPath(active.ServerPath);
                return adapter;
            });
            // IServerProcessService proxies through ActiveInstanceService
            services.AddSingleton<IServerProcessService>(sp =>
            {
                var active = sp.GetRequiredService<IActiveInstanceService>();
                try { return active.ProcessService; }
                catch { return new ServerProcessService(""); }
            });

            // Crash reporting
            services.AddSingleton<TabgInstaller.Core.Services.ICrashReportService>(
                _ => new TabgInstaller.Core.Services.CrashReportService());

            // Core services
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<KnownPlayersService>();
            services.AddSingleton<IKnownPlayersService>(sp => sp.GetRequiredService<KnownPlayersService>());
            services.AddSingleton<ConfigValidationService>();
            services.AddTransient<IBackupService>(sp =>
                new BackupService(new Progress<string>(msg =>
                    Debug.WriteLine($"[Backup] {msg}"))));
            services.AddTransient<BepInExLoaderService>(sp =>
                new BepInExLoaderService(new Progress<string>(msg =>
                    Debug.WriteLine($"[BepInEx] {msg}"))));

            // Navigation
            services.AddSingleton<INavigationService, NavigationService>();

            // Marketplace services
            services.AddSingleton<TabgInstaller.Core.Services.GitHubService>(sp =>
                new TabgInstaller.Core.Services.GitHubService(new HttpClient(), new Progress<string>(msg =>
                    Debug.WriteLine($"[GitHub] {msg}"))));
            services.AddSingleton<IRegistryService>(sp =>
            {
                var cachePath = Path.Combine(settingsDir, "registry-cache.json");
                return new RegistryService(sp.GetRequiredService<TabgInstaller.Core.Services.GitHubService>(), cachePath);
            });
            services.AddTransient<IInstalledPluginTracker>(sp =>
            {
                var active = sp.GetRequiredService<IActiveInstanceService>();
                return new InstalledPluginTracker(active.ServerPath);
            });
            services.AddTransient<IMarketplaceInstallService>(sp =>
                new MarketplaceInstallService(
                    sp.GetRequiredService<TabgInstaller.Core.Services.GitHubService>(),
                    sp.GetRequiredService<IInstalledPluginTracker>()));

            // ViewModels — registered as each panel is migrated
            services.AddTransient<SettingsPanelViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<AdminPanelViewModel>();
            services.AddTransient<SuperSecretSettingsViewModel>();
            services.AddTransient<MatchSettingsViewModel>();
            services.AddTransient<RingSpawnsViewModel>();
            services.AddTransient<BackupsPanelViewModel>();
            services.AddTransient<ConfigViewModel>();
            services.AddTransient<PresetsViewModel>();
            services.AddTransient<ServerModsViewModel>();
            services.AddTransient<ModSettingsViewModel>();
            services.AddSingleton<ConsolePanelViewModel>();
            services.AddTransient<InstallerPanelViewModel>();
            services.AddTransient<ClientPanelViewModel>();
            services.AddTransient<ReferencePanelViewModel>();
            services.AddTransient<LoadoutEditorViewModel>();
            services.AddTransient<ServerListViewModel>();
            services.AddTransient<BrowsePluginsViewModel>();

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
