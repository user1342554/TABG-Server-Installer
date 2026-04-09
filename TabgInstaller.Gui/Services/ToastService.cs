using System;

namespace TabgInstaller.Gui.Services
{
    public enum ToastType { Success, Error, Warning, Info }

    public sealed class ToastService : IToastService
    {
        /// <summary>
        /// Canonical singleton used by code that cannot receive DI injection (XAML UserControls,
        /// standalone windows). DI is configured to return this same instance so all callers
        /// share one initialized object.
        /// </summary>
        public static ToastService Instance { get; } = new();

        private Action<string, ToastType, int>? _showCallback;

        public void Initialize(Action<string, ToastType, int> showCallback)
        {
            _showCallback = showCallback;
        }

        public void Show(string message, ToastType type, int durationMs = 4000)
        {
            _showCallback?.Invoke(message, type, durationMs);
        }

        public void Success(string message) => Show(message, ToastType.Success);
        public void Error(string message) => Show(message, ToastType.Error);
        public void Warning(string message) => Show(message, ToastType.Warning);
        public void Info(string message) => Show(message, ToastType.Info);
    }
}
