namespace TabgInstaller.Gui.Services
{
    public interface IAppSettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
        void MarkSetupComplete(string serverPath, string clientPath, string clientModdedPath);
        void Reset();
    }
}
