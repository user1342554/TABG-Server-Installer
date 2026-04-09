using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
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
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TabgInstaller");
            services.AddSingleton<IAppSettingsService, AppSettingsService>();
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
