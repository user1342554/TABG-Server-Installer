using System;
using System.Collections.ObjectModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IServerProcessService
    {
        bool IsRunning { get; }
        ObservableCollection<LogEntry> LogEntries { get; }
        event Action<LogEntry>? LogEntryReceived;
        event Action<string>? OutputReceived;

        bool Start(string additionalArgs = "-batchmode -nographics -nolog");
        void Stop();
        void ClearEntries();
        void AddEntry(LogEntry entry);
        string GetRecentText(int maxLines = 20);
        void RegisterCollectionSynchronization(Action<object, object> register);
    }
}
