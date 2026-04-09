using System;
using System.Collections.ObjectModel;
using System.Linq;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public class HealthMonitorService : IHealthMonitorService
    {
        private readonly IServerProcessService _processService;
        private DateTime? _startedAt;

        public bool IsAlive => Status == ServerHealthStatus.Running;
        public int PlayerCount => ConnectedPlayers.Count;
        public long MemoryUsageMb { get; private set; }
        public ServerHealthStatus Status { get; private set; } = ServerHealthStatus.Stopped;
        public string? JoinCode { get; private set; }
        public int RestartAttempt { get; private set; }
        public int MaxRetries { get; private set; }
        public ObservableCollection<ConnectedPlayer> ConnectedPlayers { get; } = new();

        public TimeSpan Uptime => _startedAt.HasValue
            ? DateTime.Now - _startedAt.Value
            : TimeSpan.Zero;

        public event Action? StatusChanged;
        public event Action? ServerCrashed;
        public event Action? ServerRecovered;

        public HealthMonitorService(IServerProcessService processService)
        {
            _processService = processService;
        }

        public void HandleEvent(ServerEvent serverEvent)
        {
            switch (serverEvent.Type)
            {
                case ServerEventType.PlayerJoined:
                    if (!string.IsNullOrEmpty(serverEvent.PlayerName) &&
                        !ConnectedPlayers.Any(p => p.Name == serverEvent.PlayerName))
                    {
                        ConnectedPlayers.Add(new ConnectedPlayer
                        {
                            Name = serverEvent.PlayerName,
                            EpicId = serverEvent.EpicId ?? "",
                            JoinedAt = serverEvent.Timestamp
                        });
                    }
                    break;

                case ServerEventType.PlayerLeft:
                    if (!string.IsNullOrEmpty(serverEvent.PlayerName))
                    {
                        var player = ConnectedPlayers.FirstOrDefault(p => p.Name == serverEvent.PlayerName);
                        if (player != null)
                            ConnectedPlayers.Remove(player);
                    }
                    break;

                case ServerEventType.JoinCodeReceived:
                    JoinCode = serverEvent.JoinCode;
                    break;

                case ServerEventType.ProcessExited:
                    break;
            }
        }

        public void MarkRunning()
        {
            var wasNotRunning = Status != ServerHealthStatus.Running;
            Status = ServerHealthStatus.Running;
            _startedAt = DateTime.Now;
            RestartAttempt = 0;
            StatusChanged?.Invoke();
            if (wasNotRunning)
                ServerRecovered?.Invoke();
        }

        public void MarkStopped()
        {
            Status = ServerHealthStatus.Stopped;
            _startedAt = null;
            ConnectedPlayers.Clear();
            JoinCode = null;
            MemoryUsageMb = 0;
            RestartAttempt = 0;
            StatusChanged?.Invoke();
        }

        public void MarkCrashed()
        {
            Status = ServerHealthStatus.Crashed;
            _startedAt = null;
            ConnectedPlayers.Clear();
            JoinCode = null;
            ServerCrashed?.Invoke();
            StatusChanged?.Invoke();
        }

        public void MarkRestarting(int attempt, int maxRetries)
        {
            Status = ServerHealthStatus.Restarting;
            RestartAttempt = attempt;
            MaxRetries = maxRetries;
            StatusChanged?.Invoke();
        }

        public void MarkWatchdog()
        {
            Status = ServerHealthStatus.Watchdog;
            StatusChanged?.Invoke();
        }

        public void UpdateMemoryUsage(long megabytes)
        {
            MemoryUsageMb = megabytes;
        }
    }
}
