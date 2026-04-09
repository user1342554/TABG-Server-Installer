using System;

namespace TabgInstaller.Gui.Services
{
    public class NavigationService : INavigationService
    {
        private Action<int>? _navigateCallback;

        public event Action? HardResetRequested;

        public void Initialize(Action<int> navigateCallback)
        {
            _navigateCallback = navigateCallback;
        }

        public void NavigateToTab(int tabIndex)
        {
            _navigateCallback?.Invoke(tabIndex);
        }

        public void RequestHardReset()
        {
            HardResetRequested?.Invoke();
        }
    }
}
