using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IInstalledPluginTracker
    {
        InstalledPluginsData Load();
        void AddPlugin(string id, string version, string[] dllNames);
        void UpdatePluginVersion(string id, string newVersion);
        void RemovePlugin(string id);
        void SetPinned(string id, bool pinned);
        InstalledPluginEntry? FindById(string id);
        bool IsInstalled(string id);
    }
}
