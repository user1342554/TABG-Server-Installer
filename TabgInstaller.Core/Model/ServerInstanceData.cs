using System;
using System.Collections.Generic;

namespace TabgInstaller.Core.Model
{
    public enum ServerInstanceType { Local, Remote }
    public enum ServerHealthStatus { Stopped, Running, Crashed, Restarting, Watchdog }
    public enum SshAuthMethod { Password, PrivateKey }
    public enum RemoteProcessMode { Screen, Systemd }

    public class ServerInstanceData
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DisplayName { get; set; } = "";
        public string ServerPath { get; set; } = "";
        public ServerInstanceType InstanceType { get; set; } = ServerInstanceType.Local;
        public AutoRestartConfig AutoRestart { get; set; } = new();
        public RemoteConnectionConfig? RemoteConfig { get; set; }
    }

    public class AutoRestartConfig
    {
        public bool Enabled { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
        public int InitialBackoffSeconds { get; set; } = 5;
        public int WatchdogIntervalSeconds { get; set; } = 300;
        public int StabilityThresholdSeconds { get; set; } = 30;
    }

    public class RemoteConnectionConfig
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public SshAuthMethod AuthMethod { get; set; } = SshAuthMethod.Password;
        public string PrivateKeyPath { get; set; } = "";
        public string RemoteServerPath { get; set; } = "";
        public RemoteProcessMode ProcessMode { get; set; } = RemoteProcessMode.Screen;
    }

    public class ConnectedPlayer
    {
        public string Name { get; set; } = "";
        public string EpicId { get; set; } = "";
        public DateTime JoinedAt { get; set; }
    }

    public class InstancesFileData
    {
        public List<ServerInstanceData> Instances { get; set; } = new();
        public Guid? ActiveInstanceId { get; set; }
    }
}
