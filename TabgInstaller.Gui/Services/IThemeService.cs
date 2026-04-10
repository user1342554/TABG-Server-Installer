namespace TabgInstaller.Gui.Services;

public interface IThemeService
{
    bool IsHighContrast { get; }
    void SetHighContrast(bool enabled);
}
