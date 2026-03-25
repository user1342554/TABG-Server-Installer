using System;

namespace TabgInstaller.Gui.Services
{
    public enum ToastType { Success, Error, Warning, Info }

    public sealed class ToastService
    {
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
