using System;

namespace TabgInstaller.Core.Model
{
    public enum ServerEventType
    {
        PlayerJoined,
        PlayerLeft,
        JoinCodeReceived,
        ProcessExited
    }

    public class ServerEvent
    {
        public ServerEventType Type { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string? PlayerName { get; init; }
        public string? EpicId { get; init; }
        public int? PlayerIndex { get; init; }
        public string? JoinCode { get; init; }
    }
}
