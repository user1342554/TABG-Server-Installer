using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ConsolePanel : UserControl
    {
        private string _serverDir = "";
        private ServerProcessService? _procSvc;
        private CollectionViewSource? _logViewSource;
        private bool _autoScroll = true;
        private DispatcherTimer? _searchDebounceTimer;
        private string _searchText = "";

        // Severity filter state
        private bool _showInfo = true;
        private bool _showWarning = true;
        private bool _showError = true;

        public bool IsServerRunning => _procSvc?.IsRunning == true;

        public ConsolePanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;

            _procSvc = new ServerProcessService(_serverDir);

            // Enable cross-thread access to the ObservableCollection
            _procSvc.RegisterCollectionSynchronization((collection, lockObj) =>
                BindingOperations.EnableCollectionSynchronization((System.Collections.IEnumerable)collection, lockObj));

            // Set up CollectionViewSource
            _logViewSource = (CollectionViewSource)FindResource("LogViewSource");
            _logViewSource.Source = _procSvc.LogEntries;

            // Subscribe to LogEntryReceived for auto-scroll
            _procSvc.LogEntryReceived += entry => Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_autoScroll)
                        ScrollToBottom();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WARN] Auto-scroll failed: {ex.Message}");
                }
            });

            // Set up search debounce timer
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounceTimer.Tick += (_, _) =>
            {
                _searchDebounceTimer.Stop();
                _searchText = SearchBox.Text ?? "";
                RefreshFilter();
            };

            // Hook into ListBox scroll events for auto-scroll detection
            LogListBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnLogListScrollChanged));
        }

        public string GetRecentOutput(int maxLines = 20)
        {
            if (_procSvc == null) return "";
            return _procSvc.GetRecentText(maxLines);
        }

        private void LogViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is not LogEntry entry)
            {
                e.Accepted = false;
                return;
            }

            // Severity filter
            bool accepted = entry.Severity switch
            {
                LogSeverity.Info => _showInfo,
                LogSeverity.Warning => _showWarning,
                LogSeverity.Error => _showError,
                _ => true
            };

            // Search filter
            if (accepted && !string.IsNullOrEmpty(_searchText))
            {
                accepted = entry.RawText.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
            }

            e.Accepted = accepted;
        }

        private void RefreshFilter()
        {
            try
            {
                _logViewSource?.View?.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Filter refresh failed: {ex.Message}");
            }
        }

        private void SeverityToggle_Click(object sender, RoutedEventArgs e)
        {
            _showInfo = ToggleInfo.IsChecked == true;
            _showWarning = ToggleWarn.IsChecked == true;
            _showError = ToggleError.IsChecked == true;
            RefreshFilter();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Update placeholder visibility
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Debounce the search
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Start();
        }

        private void OnLogListScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is ScrollViewer sv)
            {
                // Re-engage auto-scroll when user scrolls to bottom
                _autoScroll = sv.VerticalOffset >= sv.ScrollableHeight - 10;
            }
        }

        private void ScrollToBottom()
        {
            if (LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
            }
        }

        public void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc?.IsRunning == true) return;

            try
            {
                _procSvc!.Start();
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ToastService.Instance.Error($"Failed to start: {ex.Message}");
            }
        }

        public void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc == null || !_procSvc.IsRunning) return;

            _procSvc.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            _procSvc?.ClearEntries();
        }

        private void QuickSaveRestart_Click(object sender, RoutedEventArgs e)
        {
            var gsPath = Path.Combine(_serverDir, "game_settings.txt");
            if (File.Exists(gsPath))
            {
                try
                {
                    var gs = ConfigIO.ReadGameSettings(gsPath);
                    ConfigIO.WriteGameSettings(gs, gsPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WARN] Failed to re-save game_settings.txt before restart: {ex.Message}");
                }
            }

            if (_procSvc?.IsRunning == true)
            {
                _procSvc.Stop();
                Task.Delay(1000).ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _procSvc.Start();
                        StartButton.IsEnabled = false;
                        StopButton.IsEnabled = true;
                    }
                    catch (Exception ex)
                    {
                        ToastService.Instance.Error($"Failed to restart: {ex.Message}");
                    }
                }));
            }
        }

        private void TxtCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SendCommand_Click(sender, e);
        }

        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_procSvc?.IsRunning == true && !string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                // Add command echo and info message as log entries (thread-safe)
                var echoEntry = LogLineParser.Parse($"> {TxtCommand.Text}");
                var infoEntry = LogLineParser.Parse("[INFO] Server does not accept stdin commands. Use RCON or a BepInEx console plugin.");
                _procSvc.AddEntry(echoEntry);
                _procSvc.AddEntry(infoEntry);

                if (_autoScroll)
                    ScrollToBottom();

                TxtCommand.Clear();
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    FileName = $"TABG-Server-Log-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt",
                    DefaultExt = ".txt",
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    var view = _logViewSource?.View;
                    if (view != null)
                    {
                        foreach (var item in view)
                        {
                            if (item is LogEntry entry)
                            {
                                var severity = entry.Severity switch
                                {
                                    LogSeverity.Info => "INFO",
                                    LogSeverity.Warning => "WARNING",
                                    LogSeverity.Error => "ERROR",
                                    _ => "INFO"
                                };
                                sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{severity}] {entry.Message}");
                            }
                        }
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    ToastService.Instance.Success($"Log exported to {dialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                ToastService.Instance.Error($"Failed to export log: {ex.Message}");
            }
        }
    }
}
