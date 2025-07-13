using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Core.Services
{
    public class ServerProcessService : IDisposable
    {
        private Process? _proc;
        private readonly string _serverDir;
        public event Action<string>? OutputReceived;
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

            EOSHelper.EnsureDll(_serverDir, new Progress<string>(s=>OutputReceived?.Invoke(s)));

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
            _proc.OutputDataReceived += OnLine;
            _proc.ErrorDataReceived += OnLine;
            _proc.Exited += (s,e)=>OutputReceived?.Invoke("<process exited>");
            if (_proc.Start())
            {
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
                return true;
            }
            return false;
        }

        private void OnLine(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                OutputReceived?.Invoke(e.Data);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            try { _proc!.Kill(true); _proc.WaitForExit(3000);} catch { }
        }

        public void Dispose()
        {
            Stop();
            _proc?.Dispose();
        }
    }
} 