using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IRegistryService
    {
        /// <summary>Fetches registry.json from GitHub, caches locally. Falls back to cache on failure.</summary>
        Task<PluginRegistryResponse?> FetchRegistryAsync();

        /// <summary>Returns the in-memory cached registry without fetching. Null if never fetched.</summary>
        PluginRegistryResponse? GetCachedRegistry();
    }
}
