using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public class RegistryService : IRegistryService
    {
        private const string RegistryOwner = "user1342554";
        private const string RegistryRepo = "TABG-Server-Installer";
        private const string RegistryPath = "registry/registry.json";

        private readonly GitHubService _gitHub;
        private readonly string _cachePath;
        private PluginRegistryResponse? _cached;

        public RegistryService(GitHubService gitHub, string cachePath)
        {
            _gitHub = gitHub;
            _cachePath = cachePath;
        }

        public async Task<PluginRegistryResponse?> FetchRegistryAsync()
        {
            var json = await _gitHub.FetchFileContentAsync(RegistryOwner, RegistryRepo, RegistryPath);
            if (json != null)
            {
                try
                {
                    var registry = JsonConvert.DeserializeObject<PluginRegistryResponse>(json);
                    if (registry != null)
                    {
                        _cached = registry;
                        WriteCacheToDisk(json);
                        return registry;
                    }
                }
                catch (JsonException)
                {
                    // Malformed JSON — fall through to cache
                }
            }

            return LoadFromCache();
        }

        public PluginRegistryResponse? GetCachedRegistry()
        {
            if (_cached != null) return _cached;
            return LoadFromCache();
        }

        private PluginRegistryResponse? LoadFromCache()
        {
            if (_cached != null) return _cached;

            try
            {
                if (File.Exists(_cachePath))
                {
                    var json = File.ReadAllText(_cachePath);
                    _cached = JsonConvert.DeserializeObject<PluginRegistryResponse>(json);
                    return _cached;
                }
            }
            catch (Exception)
            {
                // Corrupted cache — ignore
            }

            return null;
        }

        private void WriteCacheToDisk(string json)
        {
            try
            {
                var dir = Path.GetDirectoryName(_cachePath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(_cachePath, json);
            }
            catch (Exception)
            {
                // Non-critical — cache write failure is fine
            }
        }
    }
}
