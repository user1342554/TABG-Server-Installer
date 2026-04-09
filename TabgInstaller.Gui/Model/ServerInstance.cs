using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Model
{
    /// <summary>
    /// The runtime representation of a single local server instance.
    /// Owns its own process service and health monitor, wires log parsing,
    /// and handles crash detection with auto-restart logic.
    /// </summary>
    public partial class ServerInstance : ObservableObject, IServerInstanceContext, IDisposable
    {
        private readonly ServerProcessService _ownedProcessService;
        private readonly HealthMonitorService _ownedHealthMonitor;
        private readonly Timer _memoryTimer;
        private CancellationTokenSource? _restartCts;
        private bool _disposed;

        // --- IServerInstanceContext ---
        public string ServerPath => Data.ServerPath;
        public IServerProcessService ProcessService => _ownedProcessService;
        public IHealthMonitorService HealthMonitor => _ownedHealthMonitor;

        // --- Data ---
        public ServerInstanceData Data { get; }
        public Guid Id => Data.Id;

        // --- Observable properties (for UI binding) ---
        [ObservableProperty]
        private string _displayName = "";

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private int _playerCount;

        [ObservableProperty]
        private ServerHealthStatus _healthStatus = ServerHealthStatus.Stopped;

        public ServerInstance(ServerInstanceData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            DisplayName = data.DisplayName;

            // Create owned services using legacy string constructor
            _ownedProcessService = new ServerProcessService(data.ServerPath);
            _ownedHealthMonitor = new HealthMonitorService(_ownedProcessService);

            // Wire log output → ServerEventParser → HealthMonitor
            _ownedProcessService.OutputReceived += OnOutputReceived;

            // Wire process exit → crash detection
            _ownedProcessService.ProcessExited += OnProcessExited;

            // Wire health status changes → observable property
            _ownedHealthMonitor.StatusChanged += OnHealthStatusChanged;

            // Create memory monitor timer (initially not running)
            _memoryTimer = new Timer(OnMemoryTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Starts the server process and begins monitoring.
        /// </summary>
        public void Start()
        {
            CancelRestartLoop();

            bool started = _ownedProcessService.Start();
            if (started)
            {
                IsRunning = true;
                _ownedHealthMonitor.MarkRunning();
                StartMemoryMonitor();
            }
        }

        /// <summary>
        /// Stops the server process and all monitoring/restart loops.
        /// </summary>
        public void Stop()
        {
            CancelRestartLoop();

            _ownedProcessService.Stop();
            IsRunning = false;
            _ownedHealthMonitor.MarkStopped();
            StopMemoryMonitor();
        }

        private void OnOutputReceived(string line)
        {
            var serverEvent = ServerEventParser.TryParse(line);
            if (serverEvent != null)
            {
                _ownedHealthMonitor.HandleEvent(serverEvent);
                PlayerCount = _ownedHealthMonitor.PlayerCount;
            }
        }

        private void OnProcessExited(int exitCode)
        {
            IsRunning = false;
            StopMemoryMonitor();

            if (Data.AutoRestart.Enabled && exitCode != 0)
            {
                _ownedHealthMonitor.MarkCrashed();
                _ = RunAutoRestartAsync();
            }
            else
            {
                _ownedHealthMonitor.MarkStopped();
            }
        }

        private void OnHealthStatusChanged()
        {
            HealthStatus = _ownedHealthMonitor.Status;
            PlayerCount = _ownedHealthMonitor.PlayerCount;
        }

        // --- Auto-restart logic ---

        private async Task RunAutoRestartAsync()
        {
            var cts = new CancellationTokenSource();
            _restartCts = cts;
            var token = cts.Token;
            var config = Data.AutoRestart;
            var backoffSeconds = config.InitialBackoffSeconds;

            for (int attempt = 1; attempt <= config.MaxRetries; attempt++)
            {
                if (token.IsCancellationRequested) return;

                _ownedHealthMonitor.MarkRestarting(attempt, config.MaxRetries);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested) return;

                bool started = _ownedProcessService.Start();
                if (!started) continue;

                IsRunning = true;
                StartMemoryMonitor();

                // Wait for stability threshold
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(config.StabilityThresholdSeconds), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested) return;

                // If still running after stability period, consider it recovered
                if (_ownedProcessService.IsRunning)
                {
                    _ownedHealthMonitor.MarkRunning();
                    return;
                }

                // Process died during stability check — continue to next attempt
                IsRunning = false;
                StopMemoryMonitor();
                backoffSeconds *= 2;
            }

            // Exhausted all retries — enter watchdog mode
            await RunWatchdogAsync(token);
        }

        private async Task RunWatchdogAsync(CancellationToken token)
        {
            _ownedHealthMonitor.MarkWatchdog();
            var config = Data.AutoRestart;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(config.WatchdogIntervalSeconds), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested) return;

                bool started = _ownedProcessService.Start();
                if (!started) continue;

                IsRunning = true;
                StartMemoryMonitor();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(config.StabilityThresholdSeconds), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested) return;

                if (_ownedProcessService.IsRunning)
                {
                    _ownedHealthMonitor.MarkRunning();
                    return;
                }

                IsRunning = false;
                StopMemoryMonitor();
            }
        }

        private void CancelRestartLoop()
        {
            if (_restartCts != null)
            {
                _restartCts.Cancel();
                _restartCts.Dispose();
                _restartCts = null;
            }
        }

        // --- Memory monitoring ---

        private void StartMemoryMonitor()
        {
            _memoryTimer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private void StopMemoryMonitor()
        {
            _memoryTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnMemoryTimerTick(object? state)
        {
            try
            {
                var pid = _ownedProcessService.ProcessId;
                if (pid <= 0) return;

                using var process = Process.GetProcessById(pid);
                var memoryMb = process.WorkingSet64 / (1024 * 1024);
                _ownedHealthMonitor.UpdateMemoryUsage(memoryMb);
            }
            catch
            {
                // Process may have exited — ignore
            }
        }

        // --- IDisposable ---

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CancelRestartLoop();
            StopMemoryMonitor();
            _memoryTimer.Dispose();

            _ownedProcessService.OutputReceived -= OnOutputReceived;
            _ownedProcessService.ProcessExited -= OnProcessExited;
            _ownedHealthMonitor.StatusChanged -= OnHealthStatusChanged;

            _ownedProcessService.Dispose();
        }
    }
}
