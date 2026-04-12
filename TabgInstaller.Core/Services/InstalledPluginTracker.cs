using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public class InstalledPluginTracker : IInstalledPluginTracker
    {
        private readonly string _filePath;
        private InstalledPluginsData? _cached;

        public InstalledPluginTracker(string serverRoot)
        {
            _filePath = Path.Combine(serverRoot, "BepInEx", "plugins", "community", "installed-plugins.json");
        }

        public InstalledPluginsData Load()
        {
            if (_cached != null) return _cached;

            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    _cached = JsonConvert.DeserializeObject<InstalledPluginsData>(json) ?? new InstalledPluginsData();
                    return _cached;
                }
            }
            catch (Exception)
            {
                // Corrupted file — return fresh data
            }

            _cached = new InstalledPluginsData();
            return _cached;
        }

        public void AddPlugin(string id, string version, string[] dllNames)
        {
            var data = Load();
            data.Plugins.RemoveAll(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            data.Plugins.Add(new InstalledPluginEntry
            {
                Id = id,
                InstalledVersion = version,
                InstalledAt = DateTime.UtcNow.ToString("o"),
                Pinned = false,
                DllNames = dllNames
            });
            Save(data);
        }

        public void UpdatePluginVersion(string id, string newVersion)
        {
            var data = Load();
            var entry = data.Plugins.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return;
            entry.InstalledVersion = newVersion;
            entry.UpdatedAt = DateTime.UtcNow.ToString("o");
            Save(data);
        }

        public void RemovePlugin(string id)
        {
            var data = Load();
            data.Plugins.RemoveAll(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            Save(data);
        }

        public void SetPinned(string id, bool pinned)
        {
            var data = Load();
            var entry = data.Plugins.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return;
            entry.Pinned = pinned;
            Save(data);
        }

        public InstalledPluginEntry? FindById(string id)
        {
            var data = Load();
            return data.Plugins.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsInstalled(string id)
        {
            return FindById(id) != null;
        }

        public void InvalidateCache()
        {
            _cached = null;
        }

        private void Save(InstalledPluginsData data)
        {
            _cached = data;
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (dir != null) Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception)
            {
                // Non-critical write failure
            }
        }
    }
}
