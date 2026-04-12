using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Timers;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Model;
using TabgInstaller.Gui.Resources;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IActiveInstanceService _activeInstance;
        private readonly IAppSettingsService _appSettings;
        private readonly INavigationService _navigation;
        private readonly IToastService _toast;
        private readonly IServerInstanceManager _instanceManager;
        private readonly IRegistryService _registryService;
        private readonly IInstalledPluginTracker _pluginTracker;
        private Timer? _refreshTimer;

        [ObservableProperty] private string _previewText = "";
        [ObservableProperty] private string _startStopButtonText = Strings.StartServer;
        [ObservableProperty] private string _serverPath = "";
        [ObservableProperty] private ObservableCollection<ServerHealthCardData> _healthCards = new();
        [ObservableProperty] private string _pluginUpdatesText = "";
        [ObservableProperty] private bool _hasPluginUpdates;

        public bool IsServerRunning
        {
            get
            {
                try { return _activeInstance.ProcessService.IsRunning; }
                catch { return false; }
            }
        }

        public DashboardViewModel(
            IActiveInstanceService activeInstance,
            IAppSettingsService appSettings,
            INavigationService navigation,
            IToastService toast,
            IServerInstanceManager instanceManager,
            IRegistryService registryService,
            IInstalledPluginTracker pluginTracker)
        {
            _activeInstance = activeInstance;
            _appSettings = appSettings;
            _navigation = navigation;
            _toast = toast;
            _instanceManager = instanceManager;
            _registryService = registryService;
            _pluginTracker = pluginTracker;

            _activeInstance.PathChanged += OnServerPathChanged;
        }

        private void OnServerPathChanged()
        {
            ServerPath = _activeInstance.ServerPath;
            StartRefreshTimer();
            RefreshPreview();
            RefreshHealthCards();
        }

        private void StartRefreshTimer()
        {
            if (_refreshTimer != null)
                return;

            _refreshTimer = new Timer(2000);
            _refreshTimer.Elapsed += (_, _) => RefreshPreview();
            _refreshTimer.AutoReset = true;
            _refreshTimer.Start();
        }

        private void RefreshPreview()
        {
            PreviewText = _activeInstance.ProcessService.GetRecentText(20);
            StartStopButtonText = _activeInstance.ProcessService.IsRunning ? Strings.StopServer : Strings.StartServer;
            OnPropertyChanged(nameof(IsServerRunning));
            RefreshHealthCards();
            RefreshPluginUpdates();
        }

        private void RefreshHealthCards()
        {
            var mgr = _instanceManager as ServerInstanceManager;
            if (mgr == null) return;

            var runtimes = mgr.GetRuntimeInstances();
            HealthCards.Clear();
            foreach (var data in _instanceManager.InstanceDataList)
            {
                if (runtimes.TryGetValue(data.Id, out var instance))
                {
                    HealthCards.Add(new ServerHealthCardData
                    {
                        DisplayName = data.DisplayName,
                        IsRunning = instance.IsRunning,
                        PlayerCountText = instance.IsRunning
                            ? $"{instance.HealthMonitor.PlayerCount} players" : "",
                        UptimeText = instance.IsRunning
                            ? $"Uptime: {instance.HealthMonitor.Uptime:hh\\:mm}" : "",
                        MemoryText = instance.IsRunning
                            ? $"RAM: {instance.HealthMonitor.MemoryUsageMb} MB" : "",
                        JoinCodeText = instance.HealthMonitor.JoinCode != null
                            ? $"Join: {instance.HealthMonitor.JoinCode}" : "",
                        StatusColor = instance.HealthMonitor.Status switch
                        {
                            ServerHealthStatus.Running => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green),
                            ServerHealthStatus.Crashed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red),
                            ServerHealthStatus.Restarting => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange),
                            ServerHealthStatus.Watchdog => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange),
                            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                        }
                    });
                }
                else
                {
                    HealthCards.Add(new ServerHealthCardData
                    {
                        DisplayName = data.DisplayName,
                        StatusColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                    });
                }
            }
        }

        private void RefreshPluginUpdates()
        {
            var registry = _registryService.GetCachedRegistry();
            if (registry == null) return;

            int count = 0;
            foreach (var manifest in registry.Plugins)
            {
                if (MarketplaceInstallService.HasUpdate(manifest, _pluginTracker))
                    count++;
            }

            HasPluginUpdates = count > 0;
            PluginUpdatesText = count > 0
                ? string.Format("{0} plugin update(s) available", count)
                : "";
        }

        [RelayCommand]
        private void StartStop()
        {
            if (_activeInstance.ProcessService.IsRunning)
                _activeInstance.ProcessService.Stop();
            else
                _activeInstance.ProcessService.Start();

            RefreshPreview();
        }

        [RelayCommand]
        private void LaunchClient()
        {
            var settings = _appSettings.Load();
            var moddedDir = settings.ClientModdedPath;
            if (string.IsNullOrEmpty(moddedDir))
            {
                _toast.Warning(Messages.ClientModsNotSetUp);
                return;
            }
            var exe = Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe");
            if (!File.Exists(exe))
            {
                _toast.Warning(Messages.ModdedTabgNotFoundClient);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = moddedDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.FailedToLaunch, ex.Message));
            }
        }

        [RelayCommand]
        private void OpenServerFolder() =>
            Process.Start("explorer", _activeInstance.ServerPath);

        [RelayCommand]
        private void OpenLogs()
        {
            var logDir = Path.Combine(_activeInstance.ServerPath, "BepInEx");
            if (!Directory.Exists(logDir))
                logDir = _activeInstance.ServerPath;
            Process.Start("explorer", logDir);
        }

        [RelayCommand]
        private void OpenConfigs() =>
            Process.Start("explorer", _activeInstance.ServerPath);

        [RelayCommand]
        private void OpenFullConsole() =>
            _navigation.NavigateToTab(4);
    }

    public partial class ServerHealthCardData : ObservableObject
    {
        [ObservableProperty] private string _displayName = "";
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private string _playerCountText = "";
        [ObservableProperty] private string _uptimeText = "";
        [ObservableProperty] private string _memoryText = "";
        [ObservableProperty] private string _joinCodeText = "";
        [ObservableProperty] private System.Windows.Media.Brush _statusColor
            = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
    }
}
