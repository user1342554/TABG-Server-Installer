using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public class RemoteProcessService : IServerProcessService, IDisposable
    {
        private readonly IRemoteSshService _ssh;
        private readonly RemoteConnectionConfig _config;
        private CancellationTokenSource? _tailCts;
        private readonly object _logLock = new();
        private const int MaxLogEntries = 50_000;

        public bool IsRunning { get; private set; }
        public int ProcessId => 0; // Remote PID not tracked locally
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public event Action<LogEntry>? LogEntryReceived;
        public event Action<string>? OutputReceived;
        public event Action<int>? ProcessExited;

        public RemoteProcessService(IRemoteSshService ssh, RemoteConnectionConfig config)
        {
            _ssh = ssh;
            _config = config;
        }

        public bool Start(string additionalArgs = "-batchmode -nographics -nolog")
        {
            if (IsRunning) return false;

            try
            {
                var command = _config.ProcessMode switch
                {
                    RemoteProcessMode.Screen =>
                        $"screen -dmS tabg {_config.RemoteServerPath}/TABG.exe {additionalArgs}",
                    RemoteProcessMode.Systemd =>
                        "systemctl start tabg-server",
                    _ => throw new InvalidOperationException($"Unknown process mode: {_config.ProcessMode}")
                };

                _ssh.ExecuteCommandAsync(command).GetAwaiter().GetResult();
                IsRunning = true;
                StartLogTail();
                return true;
            }
            catch (Exception ex)
            {
                var entry = LogLineParser.Parse($"[ERROR] Failed to start remote server: {ex.Message}");
                AddLogEntry(entry);
                return false;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _tailCts?.Cancel();
            try
            {
                var command = _config.ProcessMode switch
                {
                    RemoteProcessMode.Screen => "screen -S tabg -X quit",
                    RemoteProcessMode.Systemd => "systemctl stop tabg-server",
                    _ => "kill $(pgrep TABG)"
                };

                _ssh.ExecuteCommandAsync(command).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RemoteProcess] Stop failed: {ex.Message}");
            }

            IsRunning = false;
            ProcessExited?.Invoke(0);
        }

        public void ClearEntries()
        {
            lock (_logLock) { LogEntries.Clear(); }
        }

        public void AddEntry(LogEntry entry) => AddLogEntry(entry);

        public string GetRecentText(int maxLines = 20)
        {
            lock (_logLock)
            {
                var count = LogEntries.Count;
                if (count == 0) return "";
                var start = Math.Max(0, count - maxLines);
                var sb = new System.Text.StringBuilder();
                for (int i = start; i < count; i++)
                {
                    if (sb.Length > 0) sb.Append(Environment.NewLine);
                    sb.Append(LogEntries[i].RawText);
                }
                return sb.ToString();
            }
        }

        public void RegisterCollectionSynchronization(Action<object, object> register)
        {
            register(LogEntries, _logLock);
        }

        private void StartLogTail()
        {
            _tailCts?.Cancel();
            _tailCts = new CancellationTokenSource();
            var ct = _tailCts.Token;

            var logPath = $"{_config.RemoteServerPath}/output_log.txt";

            _ = Task.Run(async () =>
            {
                try
                {
                    await _ssh.StartTailAsync(logPath, line =>
                    {
                        OutputReceived?.Invoke(line);
                        var entry = LogLineParser.Parse(line);
                        AddLogEntry(entry);
                    }, ct);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RemoteProcess] Log tail failed: {ex.Message}");
                }
            }, ct);
        }

        private void AddLogEntry(LogEntry entry)
        {
            try
            {
                lock (_logLock)
                {
                    while (LogEntries.Count >= MaxLogEntries)
                        LogEntries.RemoveAt(0);
                    LogEntries.Add(entry);
                }
                LogEntryReceived?.Invoke(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RemoteProcess] AddLogEntry failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _tailCts?.Cancel();
            _tailCts?.Dispose();
        }
    }
}
