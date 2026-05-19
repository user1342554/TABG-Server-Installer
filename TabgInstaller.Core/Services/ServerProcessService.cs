using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            var exe = ResolveServerExecutable(_serverDir);
            if (exe == null) throw new FileNotFoundException("TABG server executable not found", _serverDir);

            EOSHelper.EnsureDll(_serverDir, new Progress<string>(s => OutputReceived?.Invoke(s)));
            var startInfo = CreateStartInfo(exe, additionalArgs, _serverDir);

            _proc = new Process
            {
                StartInfo = startInfo,
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

        private static ProcessStartInfo CreateStartInfo(string executablePath, string additionalArgs, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = additionalArgs,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (!OperatingSystem.IsWindows()
                && executablePath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
            {
                ValidateLinuxServerRuntime(workingDirectory);
                AddLinuxNativeLibraryPath(startInfo, workingDirectory);

                var realExecutable = ResolveUnixServerExecutable(workingDirectory);
                if (realExecutable == null)
                    throw new FileNotFoundException("TABG Linux server executable not found", workingDirectory);

                startInfo.FileName = "/usr/bin/env";
                startInfo.Arguments = string.Empty;
                startInfo.ArgumentList.Add("bash");
                startInfo.ArgumentList.Add(executablePath);
                startInfo.ArgumentList.Add(realExecutable);
                foreach (var arg in SplitArgs(additionalArgs))
                    startInfo.ArgumentList.Add(arg);
            }

            return startInfo;
        }

        private static void AddLinuxNativeLibraryPath(ProcessStartInfo startInfo, string serverDir)
        {
            var pluginDir = Path.Combine(serverDir, "TABG_Data", "Plugins");
            var x64PluginDir = Path.Combine(pluginDir, "x86_64");
            var existing = startInfo.Environment.TryGetValue("LD_LIBRARY_PATH", out var current)
                ? current
                : Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? string.Empty;

            var paths = new[] { pluginDir, x64PluginDir }
                .Where(Directory.Exists)
                .Concat(string.IsNullOrWhiteSpace(existing)
                    ? Array.Empty<string>()
                    : existing.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (paths.Length > 0)
                startInfo.Environment["LD_LIBRARY_PATH"] = string.Join(Path.PathSeparator, paths);
        }

        private static string? ResolveUnixServerExecutable(string serverDir)
        {
            var candidates = new[]
            {
                "TABG-DS.x86_64",
                "TABG.x86_64",
                "TotallyAccurateBattlegroundsDedicatedServer.x86_64",
                "TABG-DS.exe",
                "TABG.exe"
            };

            foreach (var candidate in candidates)
            {
                var path = Path.Combine(serverDir, candidate);
                if (File.Exists(path))
                    return path;
            }

            return Directory.Exists(serverDir)
                ? Directory.GetFiles(serverDir, "TABG*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => File.Exists(path) && !path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
                : null;
        }

        private static void ValidateLinuxServerRuntime(string serverDir)
        {
            var requiredFiles = new[]
            {
                "UnityPlayer.so",
                Path.Combine("TABG_Data", "globalgamemanagers"),
                Path.Combine("TABG_Data", "Managed", "Assembly-CSharp.dll")
            };

            var missing = requiredFiles
                .Where(relativePath => !File.Exists(Path.Combine(serverDir, relativePath)))
                .ToArray();

            if (missing.Length > 0)
            {
                throw new FileNotFoundException(
                    "TABG dedicated server install is incomplete. Missing: " + string.Join(", ", missing) +
                    ". Reinstall/validate app 1020290 with Steam or run SteamCMD install/update with your Steam account.");
            }

            EnsureLinuxFmodLibraryLinks(serverDir);
        }

        private static void EnsureLinuxFmodLibraryLinks(string serverDir)
        {
            var source = Path.Combine(serverDir, "TABG_Data", "Plugins", "libfmodstudio.so");
            if (!File.Exists(source))
                return;

            var targetDir = Path.Combine(serverDir, "TABG_Data", "MonoBleedingEdge", "x86_64");
            Directory.CreateDirectory(targetDir);

            foreach (var name in new[] { "fmodstudio", "libfmodstudio", "libfmodstudio.so" })
            {
                var link = Path.Combine(targetDir, name);
                try
                {
                    if (File.Exists(link))
                        File.Delete(link);

                    File.CreateSymbolicLink(link, "../../Plugins/libfmodstudio.so");
                }
                catch
                {
                    File.Copy(source, link, overwrite: true);
                }
            }
        }

        private static IEnumerable<string> SplitArgs(string value)
        {
            return value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string? ResolveServerExecutable(string serverDir)
        {
            var candidates = OperatingSystem.IsWindows()
                ? new[]
                {
                    "TABG-DS.exe",
                    "TABG.exe",
                    "TABG-DS.x86_64",
                    "TABG.x86_64",
                    "TotallyAccurateBattlegroundsDedicatedServer.x86_64"
                }
                : new[]
            {
                "run_bepinex.sh",
                "TABG-DS.x86_64",
                "TABG.x86_64",
                "TotallyAccurateBattlegroundsDedicatedServer.x86_64",
                "TABG-DS.exe",
                "TABG.exe"
            };

            foreach (var candidate in candidates)
            {
                var path = Path.Combine(serverDir, candidate);
                if (File.Exists(path))
                    return path;
            }

            return Directory.Exists(serverDir)
                ? Directory.GetFiles(serverDir, "TABG*", SearchOption.TopDirectoryOnly).FirstOrDefault(File.Exists)
                : null;
        }

        private void OnStdoutLine(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            if (!IsNoisyUnityServerLine(e.Data))
                OutputReceived?.Invoke(e.Data);

            var entry = LogLineParser.Parse(e.Data, isStderr: false);
            AddLogEntry(entry);
        }

        private void OnStderrLine(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            if (!IsNoisyUnityServerLine(e.Data))
                OutputReceived?.Invoke(e.Data);

            var entry = LogLineParser.Parse(e.Data, isStderr: true);
            AddLogEntry(entry);
        }

        private static bool IsNoisyUnityServerLine(string line)
        {
            return line.Contains("DllNotFoundException: fmodstudio", StringComparison.OrdinalIgnoreCase) ||
                (line.Contains("Fallback handler could not load library", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("fmodstudio", StringComparison.OrdinalIgnoreCase)) ||
                line.Contains("FMOD.", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("FMODUnity.", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("VehicleSoundHandler", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("PillarSounds", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("CollisionChecker", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("TakeCollisionDamage", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Unity.Services.Relay.", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("System.Runtime.CompilerServices", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("System.Threading.Tasks.", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("System.Threading.ExecutionContext", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("UnityEngine.AsyncOperation", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("UnityEngine.Object:Destroy", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Rethrow as SystemNotInitializedException: [FMOD]", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("The referenced script on this Behaviour", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("shader is not supported on this GPU", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("WARNING: Shader", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("No mesh data available for mesh", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("is not derived from MonoBehaviour or ScriptableObject", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("[LandLog] - Reading Line:", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("[LandLog] - GameSetting:", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("[LandLog] - Exception when parsing gamesettings:", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("EOS SDK Analytics disabled", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Purchase flow is disabled", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("TabgInstaller.UnusedVehicles.SearchForCarsPatch", StringComparison.OrdinalIgnoreCase) ||
                (line.Contains("Landfall.Network.", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("[LandLog]", StringComparison.OrdinalIgnoreCase)) ||
                line.TrimStart().StartsWith("at (wrapper", StringComparison.OrdinalIgnoreCase) ||
                line.TrimStart().StartsWith("at Landfall.Network.", StringComparison.OrdinalIgnoreCase);
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
