using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
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
            services.AddTransient<BepInExLoaderService>(sp =>
                new BepInExLoaderService(new Progress<string>(msg =>
                    Debug.WriteLine($"[BepInEx] {msg}"))));

            // Navigation
            services.AddSingleton<INavigationService, NavigationService>();

            // ViewModels — registered as each panel is migrated
            services.AddTransient<SettingsPanelViewModel>();

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
