using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Timers;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IActiveInstanceService _activeInstance;
        private readonly IAppSettingsService _appSettings;
        private readonly INavigationService _navigation;
        private readonly IToastService _toast;
        private Timer? _refreshTimer;

        [ObservableProperty] private string _previewText = "";
        [ObservableProperty] private string _startStopButtonText = "Start Server";
        [ObservableProperty] private string _serverPath = "";

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
            IToastService toast)
        {
            _activeInstance = activeInstance;
            _appSettings = appSettings;
            _navigation = navigation;
            _toast = toast;

            _activeInstance.PathChanged += OnServerPathChanged;
        }

        private void OnServerPathChanged()
        {
            ServerPath = _activeInstance.ServerPath;
            StartRefreshTimer();
            RefreshPreview();
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
            StartStopButtonText = _activeInstance.ProcessService.IsRunning ? "Stop Server" : "Start Server";
            OnPropertyChanged(nameof(IsServerRunning));
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
                _toast.Warning("Client mods not set up. Go to the Client Mods tab first.");
                return;
            }
            var exe = Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe");
            if (!File.Exists(exe))
            {
                _toast.Warning("Modded TABG not found. Install client mods first.");
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
                _toast.Error($"Failed to launch: {ex.Message}");
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
}
