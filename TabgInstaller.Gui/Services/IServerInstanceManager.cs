using System;
using System.Collections.ObjectModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.Services
{
    public interface IServerInstanceManager
    {
        ObservableCollection<ServerInstanceData> InstanceDataList { get; }
        IServerInstanceContext? ActiveInstance { get; }
        ServerInstanceData? ActiveInstanceData { get; }
        event Action? ActiveInstanceChanged;

        IServerInstanceContext AddLocalInstance(string displayName, string serverPath);
        IServerInstanceContext AddRemoteInstance(string displayName, RemoteConnectionConfig config);
        void RemoveInstance(Guid id);
        void SetActiveInstance(Guid id);
        void RenameInstance(Guid id, string newName);
        void Save();
        void Load();
    }
}
