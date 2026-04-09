using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
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

        /// <summary>Convenience inverse for IsEnabled binding on Start button without a converter.</summary>
        public bool IsServerNotRunning => !IsServerRunning;

        partial void OnIsServerRunningChanged(bool value) =>
            OnPropertyChanged(nameof(IsServerNotRunning));

        public ObservableCollection<LogEntry> LogEntries => _procSvc.LogEntries;

        /// <summary>
        /// Forwards to <see cref="IServerProcessService.LogEntryReceived"/> so the code-behind
        /// can subscribe without needing a direct reference to the process service.
        /// </summary>
        public event Action<LogEntry>? LogEntryReceived
        {
            add => _procSvc.LogEntryReceived += value;
            remove => _procSvc.LogEntryReceived -= value;
        }

        /// <summary>
        /// Forwards to <see cref="IServerProcessService.RegisterCollectionSynchronization"/>
        /// so code-behind can call <see cref="System.Windows.Data.BindingOperations.EnableCollectionSynchronization"/>
        /// without a direct reference to the process service.
        /// </summary>
        public void RegisterCollectionSynchronization(Action<object, object> register) =>
            _procSvc.RegisterCollectionSynchronization(register);

        public ConsolePanelViewModel(
            IServerProcessService procSvc,
            IServerPathProvider serverPathProvider,
            IToastService toast)
        {
            _procSvc = procSvc;
            _serverPathProvider = serverPathProvider;
            _toast = toast;

            IsServerRunning = _procSvc.IsRunning;
        }

        [RelayCommand]
        private void Start()
        {
            if (_procSvc.IsRunning) return;
            try
            {
                _procSvc.Start();
                IsServerRunning = true;
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to start: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Stop()
        {
            if (!_procSvc.IsRunning) return;
            _procSvc.Stop();
            IsServerRunning = false;
        }

        [RelayCommand]
        private void ClearConsole()
        {
            _procSvc.ClearEntries();
        }

        [RelayCommand]
        private void QuickSaveRestart()
        {
            var serverDir = _serverPathProvider.ServerPath;
            var gsPath = Path.Combine(serverDir, "game_settings.txt");
            if (File.Exists(gsPath))
            {
                try
                {
                    var gs = ConfigIO.ReadGameSettings(gsPath);
                    ConfigIO.WriteGameSettings(gs, gsPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WARN] Failed to re-save game_settings.txt before restart: {ex.Message}");
                }
            }

            if (_procSvc.IsRunning)
            {
                _procSvc.Stop();
                Task.Delay(1000).ContinueWith(_ =>
                {
                    try
                    {
                        _procSvc.Start();
                        IsServerRunning = true;
                    }
                    catch (Exception ex)
                    {
                        _toast.Error($"Failed to restart: {ex.Message}");
                    }
                });
            }
        }

        [RelayCommand]
        private void SendCommand()
        {
            if (_procSvc.IsRunning && !string.IsNullOrWhiteSpace(CommandText))
            {
                var echoEntry = LogLineParser.Parse($"> {CommandText}");
                var infoEntry = LogLineParser.Parse(
                    "[INFO] Server does not accept stdin commands. Use RCON or a BepInEx console plugin.");
                _procSvc.AddEntry(echoEntry);
                _procSvc.AddEntry(infoEntry);
                CommandText = "";
            }
        }

        /// <summary>
        /// Exports the currently-filtered view to <paramref name="filePath"/>.
        /// Call this from the thin ExportButton_Click handler in code-behind after
        /// the SaveFileDialog has resolved the file path.
        /// </summary>
        public void ExportLog(string filePath, System.Collections.IEnumerable filteredView)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var item in filteredView)
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

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                _toast.Success($"Log exported to {filePath}");
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to export log: {ex.Message}");
            }
        }

        /// <summary>
        /// Filter predicate used by the CollectionViewSource in code-behind.
        /// Returns true when the entry should be visible given current filter state.
        /// </summary>
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
}
