using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.Model;

namespace TabgInstaller.Gui.Services
{
    /// <summary>
    /// Manages the collection of server instances: CRUD, persistence to JSON,
    /// and migration from a single-server setup.
    /// </summary>
    public class ServerInstanceManager : IServerInstanceManager
    {
        private readonly string _storageDir;
        private readonly string _instancesFilePath;

        // Runtime instances (IServerInstanceContext) keyed by Id
        private readonly Dictionary<Guid, IServerInstanceContext> _runtimeInstances = new();

        // Local ServerInstance objects specifically (for GetRuntimeInstances)
        private readonly Dictionary<Guid, ServerInstance> _localInstances = new();

        // --- IServerInstanceManager ---

        public ObservableCollection<ServerInstanceData> InstanceDataList { get; } = new();
        public IServerInstanceContext? ActiveInstance { get; private set; }
        public ServerInstanceData? ActiveInstanceData { get; private set; }

        public event Action? ActiveInstanceChanged;

        public ServerInstanceManager(string storageDir)
        {
            _storageDir = storageDir ?? throw new ArgumentNullException(nameof(storageDir));
            _instancesFilePath = Path.Combine(_storageDir, "instances.json");
        }

        // --- CRUD ---

        public IServerInstanceContext AddLocalInstance(string displayName, string serverPath)
        {
            var data = new ServerInstanceData
            {
                DisplayName = displayName,
                ServerPath = serverPath,
                InstanceType = ServerInstanceType.Local
            };

            var instance = new ServerInstance(data);

            _runtimeInstances[data.Id] = instance;
            _localInstances[data.Id] = instance;
            InstanceDataList.Add(data);

            SetActiveInstance(data.Id);
            Save();

            return instance;
        }

        public IServerInstanceContext AddRemoteInstance(string displayName, RemoteConnectionConfig config)
        {
            var data = new ServerInstanceData
            {
                DisplayName = displayName,
                InstanceType = ServerInstanceType.Remote,
                RemoteConfig = config
            };

            // Use ServerInstance as a placeholder — RemoteServerInstance comes in Task 15
            var instance = new ServerInstance(data);

            _runtimeInstances[data.Id] = instance;
            _localInstances[data.Id] = instance;
            InstanceDataList.Add(data);

            SetActiveInstance(data.Id);
            Save();

            return instance;
        }

        public void RemoveInstance(Guid id)
        {
            if (InstanceDataList.Count <= 1)
                throw new InvalidOperationException("Cannot remove the last server instance.");

            // Dispose runtime
            if (_runtimeInstances.TryGetValue(id, out var runtime))
            {
                if (runtime is IDisposable disposable)
                    disposable.Dispose();
                _runtimeInstances.Remove(id);
            }

            _localInstances.Remove(id);

            var data = InstanceDataList.FirstOrDefault(d => d.Id == id);
            if (data != null)
                InstanceDataList.Remove(data);

            // If the removed instance was active, switch to the first remaining one
            if (ActiveInstanceData?.Id == id)
            {
                var first = InstanceDataList.FirstOrDefault();
                if (first != null)
                    SetActiveInstance(first.Id);
            }

            Save();
        }

        public void SetActiveInstance(Guid id)
        {
            var data = InstanceDataList.FirstOrDefault(d => d.Id == id);
            if (data == null)
                throw new ArgumentException($"No instance with id {id}.", nameof(id));

            _runtimeInstances.TryGetValue(id, out var runtime);

            ActiveInstanceData = data;
            ActiveInstance = runtime;

            ActiveInstanceChanged?.Invoke();
        }

        public void RenameInstance(Guid id, string newName)
        {
            var data = InstanceDataList.FirstOrDefault(d => d.Id == id);
            if (data == null)
                throw new ArgumentException($"No instance with id {id}.", nameof(id));

            data.DisplayName = newName;

            // Also update the runtime object's observable property if it's a ServerInstance
            if (_runtimeInstances.TryGetValue(id, out var runtime) && runtime is ServerInstance si)
                si.DisplayName = newName;

            Save();
        }

        // --- Persistence ---

        public void Save()
        {
            Directory.CreateDirectory(_storageDir);

            var fileData = new InstancesFileData
            {
                Instances = InstanceDataList.ToList(),
                ActiveInstanceId = ActiveInstanceData?.Id
            };

            var json = JsonConvert.SerializeObject(fileData, Formatting.Indented);
            File.WriteAllText(_instancesFilePath, json);
        }

        public void Load()
        {
            // Dispose and clear existing runtime
            foreach (var runtime in _runtimeInstances.Values)
            {
                if (runtime is IDisposable disposable)
                    disposable.Dispose();
            }
            _runtimeInstances.Clear();
            _localInstances.Clear();
            InstanceDataList.Clear();
            ActiveInstance = null;
            ActiveInstanceData = null;

            if (!File.Exists(_instancesFilePath))
                return;

            var json = File.ReadAllText(_instancesFilePath);
            var fileData = JsonConvert.DeserializeObject<InstancesFileData>(json);
            if (fileData == null)
                return;

            foreach (var data in fileData.Instances)
            {
                var instance = new ServerInstance(data);
                _runtimeInstances[data.Id] = instance;
                _localInstances[data.Id] = instance;
                InstanceDataList.Add(data);
            }

            // Restore active instance
            var activeId = fileData.ActiveInstanceId;
            if (activeId.HasValue && _runtimeInstances.ContainsKey(activeId.Value))
                SetActiveInstance(activeId.Value);
            else if (InstanceDataList.Count > 0)
                SetActiveInstance(InstanceDataList[0].Id);
        }

        // --- Migration ---

        public void MigrateFromSingleServer(string serverPath)
        {
            var settingsFile = Path.Combine(serverPath, "game_settings.txt");
            var settings = ConfigIO.ReadGameSettings(settingsFile);

            // Use ServerName from settings, fall back to "Server" if it's the default or empty
            var displayName = string.IsNullOrWhiteSpace(settings.ServerName) || settings.ServerName == "enormous"
                ? "Server"
                : settings.ServerName;

            AddLocalInstance(displayName, serverPath);
        }

        // --- Additional helpers ---

        /// <summary>
        /// Returns the local ServerInstance objects, needed for dashboard health cards.
        /// </summary>
        public IReadOnlyDictionary<Guid, ServerInstance> GetRuntimeInstances()
            => _localInstances;
    }
}
