using System.Collections.Generic;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IMarketplaceInstallService
    {
        Task<bool> InstallPluginAsync(PluginManifest manifest, List<PluginManifest> registryPlugins,
            string serverRoot, string? clientModdedPath);
        Task<bool> UpdatePluginAsync(PluginManifest manifest, string serverRoot, string? clientModdedPath);
        bool UninstallPlugin(string pluginId, string serverRoot, string? clientModdedPath);
    }
}
