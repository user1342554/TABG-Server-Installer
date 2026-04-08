using System;

namespace TabgInstaller.Core.Model
{
    public enum LogSeverity { Info, Warning, Error }

    public class LogEntry
    {
        public DateTime Timestamp { get; init; }
        public LogSeverity Severity { get; init; }
        public string RawText { get; init; } = "";
        public string Message { get; init; } = "";
    }
}
