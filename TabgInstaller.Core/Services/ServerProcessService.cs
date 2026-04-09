using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Core.Services
{
    public class ServerProcessService : IServerProcessService, IDisposable
    {
        private const int MaxLogEntries = 50_000;

        private Process? _proc;
        private readonly IServerPathProvider? _serverPathProvider;
        private string _serverDir;
        private readonly object _logLock = new();
        public event Action<string>? OutputReceived;
        public event Action<LogEntry>? LogEntryReceived;
        public event Action<int>? ProcessExited;
        public int ProcessId => _proc?.Id ?? 0;
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public bool IsRunning => _proc != null && !_proc.HasExited;

        /// <summary>
        /// Calls the provided action with the internal lock object, allowing callers
        /// (e.g., WPF's BindingOperations.EnableCollectionSynchronization) to register
        /// the lock without exposing it as a public field.
        /// </summary>
        public void RegisterCollectionSynchronization(Action<object, object> register)
        {
            register(LogEntries, _logLock);
        }

        /// <summary>DI constructor — path is resolved from IServerPathProvider at Start() time.</summary>
        public ServerProcessService(IServerPathProvider serverPathProvider)
        {
            _serverPathProvider = serverPathProvider;
            _serverDir = serverPathProvider.ServerPath;
            serverPathProvider.PathChanged += () => _serverDir = serverPathProvider.ServerPath;
        }

        /// <summary>Legacy constructor for callers that supply serverDir directly.</summary>
        public ServerProcessService(string serverDir)
        {
            _serverDir = serverDir;
        }

        public bool Start(string additionalArgs = "-batchmode -nographics -nolog")
        {
            if (IsRunning) return false;
            var exe = Path.Combine(_serverDir, "TABG.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("TABG.exe not found", exe);

            EOSHelper.EnsureDll(_serverDir, new Progress<string>(s => OutputReceived?.Invoke(s)));

            _proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = additionalArgs,
                    WorkingDirectory = _serverDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };
            _proc.OutputDataReceived += OnStdoutLine;
            _proc.ErrorDataReceived += OnStderrLine;
            _proc.Exited += (s, e) =>
            {
                var exitCode = -1;
                try { exitCode = _proc?.ExitCode ?? -1; } catch { }
                var line = "<process exited>";
                OutputReceived?.Invoke(line);
                var entry = LogLineParser.Parse(line);
                AddLogEntry(entry);
                ProcessExited?.Invoke(exitCode);
            };
            if (_proc.Start())
            {
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
                return true;
            }
            return false;
        }

        private void OnStdoutLine(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            OutputReceived?.Invoke(e.Data);
            var entry = LogLineParser.Parse(e.Data, isStderr: false);
            AddLogEntry(entry);
        }

        private void OnStderrLine(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            OutputReceived?.Invoke(e.Data);
            var entry = LogLineParser.Parse(e.Data, isStderr: true);
            AddLogEntry(entry);
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
                System.Diagnostics.Debug.WriteLine($"[WARN] Failed to add log entry: {ex.Message}");
            }
        }

        /// <summary>Thread-safe: adds a LogEntry from any thread (e.g., UI thread for echo commands).</summary>
        public void AddEntry(LogEntry entry)
        {
            AddLogEntry(entry);
        }

        /// <summary>Thread-safe: clears all log entries.</summary>
        public void ClearEntries()
        {
            try
            {
                lock (_logLock)
                {
                    LogEntries.Clear();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Failed to clear log entries: {ex.Message}");
            }
        }

        /// <summary>Thread-safe: returns a snapshot of recent entries for reading without holding the lock.</summary>
        public string GetRecentText(int maxLines = 20)
        {
            try
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] GetRecentText failed: {ex.Message}");
                return "";
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;
            try { _proc!.Kill(true); _proc.WaitForExit(3000); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Failed to stop server process: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _proc?.Dispose();
        }
    }
}
