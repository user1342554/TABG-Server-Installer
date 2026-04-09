using System;

namespace TabgInstaller.Gui.Services
{
    public interface INavigationService
    {
        void NavigateToTab(int tabIndex);
        event Action? HardResetRequested;
        void RequestHardReset();
    }
}
