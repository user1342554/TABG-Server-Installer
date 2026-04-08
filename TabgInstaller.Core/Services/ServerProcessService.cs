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
    public class ServerProcessService : IDisposable
    {
        private const int MaxLogEntries = 50_000;

        private Process? _proc;
        private readonly string _serverDir;
        private readonly object _logLock = new();
        public event Action<string>? OutputReceived;
        public event Action<LogEntry>? LogEntryReceived;
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public object LogLock => _logLock;
        public bool IsRunning => _proc != null && !_proc.HasExited;

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
                var line = "<process exited>";
                OutputReceived?.Invoke(line);
                var entry = LogLineParser.Parse(line);
                AddLogEntry(entry);
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
