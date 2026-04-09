using System;
using System.Collections.ObjectModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IHealthMonitorService
    {
        bool IsAlive { get; }
        int PlayerCount { get; }
        TimeSpan Uptime { get; }
        long MemoryUsageMb { get; }
        ServerHealthStatus Status { get; }
        string? JoinCode { get; }
        int RestartAttempt { get; }
        int MaxRetries { get; }
        ObservableCollection<ConnectedPlayer> ConnectedPlayers { get; }

        event Action? StatusChanged;
        event Action? ServerCrashed;
        event Action? ServerRecovered;

        void HandleEvent(ServerEvent serverEvent);
        void MarkRunning();
        void MarkStopped();
        void MarkCrashed();
        void MarkRestarting(int attempt, int maxRetries);
        void MarkWatchdog();
        void UpdateMemoryUsage(long megabytes);
    }
}
