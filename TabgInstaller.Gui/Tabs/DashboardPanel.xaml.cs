using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class DashboardPanel : UserControl
    {
        private string _serverDir = "";
        private ConsolePanel? _consolePanel;
        private System.Windows.Threading.DispatcherTimer? _refreshTimer;

        public event Action? RequestOpenConsole;

        public DashboardPanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir, ConsolePanel consolePanel)
        {
            _serverDir = serverDir;
            _consolePanel = consolePanel;
            TxtServerPath.Text = serverDir;

            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _refreshTimer.Tick += (_, _) => RefreshPreview();
            _refreshTimer.Start();
        }

        private void RefreshPreview()
        {
            if (_consolePanel == null) return;

            var recent = _consolePanel.GetRecentOutput(20);
            if (TxtConsolePreview.Text != recent)
            {
                TxtConsolePreview.Text = recent;
                PreviewScrollViewer.ScrollToEnd();
            }

            var running = _consolePanel.IsServerRunning;
            TxtStatus.Text = running ? "  RUNNING" : "  STOPPED";
            TxtStatus.Foreground = running
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 170, 68))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 0, 0));
            BtnStartStop.Content = running ? "Stop Server" : "Start Server";
        }

        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_consolePanel == null) return;

            if (_consolePanel.IsServerRunning)
                _consolePanel.StopButton_Click(sender, e);
            else
                _consolePanel.StartButton_Click(sender, e);
            RefreshPreview();
        }

        private void BtnLaunchClient_Click(object sender, RoutedEventArgs e)
        {
            var settings = AppSettingsService.Load();
            var moddedDir = settings.ClientModdedPath;
            if (string.IsNullOrEmpty(moddedDir))
            {
                ToastService.Instance.Warning("Client mods not set up. Go to the Client Mods tab first.");
                return;
            }
            var exe = Path.Combine(moddedDir, "TotallyAccurateBattlegrounds.exe");
            if (!File.Exists(exe))
            {
                ToastService.Instance.Warning("Modded TABG not found. Install client mods first.");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = exe, WorkingDirectory = moddedDir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ToastService.Instance.Error($"Failed to launch: {ex.Message}");
            }
        }

        private void OpenServerFolder_Click(object sender, RoutedEventArgs e) => Process.Start("explorer", _serverDir);
        private void OpenLogs_Click(object sender, RoutedEventArgs e)
        {
            var logDir = Path.Combine(_serverDir, "BepInEx");
            if (!Directory.Exists(logDir)) logDir = _serverDir;
            Process.Start("explorer", logDir);
        }
        private void OpenConfigs_Click(object sender, RoutedEventArgs e) => Process.Start("explorer", _serverDir);
        private void OpenFullConsole_Click(object sender, RoutedEventArgs e) => RequestOpenConsole?.Invoke();
    }
}
