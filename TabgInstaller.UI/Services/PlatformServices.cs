using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TabgInstaller.UI.Services;

public interface IStoragePickerService
{
    Task<string?> PickFolderAsync(string title);

    Task<string?> PickFileAsync(string title, params string[] patterns);
}

public interface IConfirmationDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
}

public interface IClipboardService
{
    Task SetTextAsync(string text);
}

public interface IUiDispatcher
{
    void Post(Action action);
}

public interface IExternalLauncher
{
    bool TryOpenPath(string path, out string? error);
}

public interface ISteamPathDetector
{
    string? TryFindServerPath();

    string? TryFindClientPath();
}

public interface INotificationService
{
    void Info(string message);

    void Warning(string message);

    void Error(string message);
}

public sealed class ExternalProcessLauncher : IExternalLauncher
{
    public bool TryOpenPath(string path, out string? error)
    {
        error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Path is empty.";
                return false;
            }

            var fileName = OperatingSystem.IsWindows()
                ? "explorer"
                : OperatingSystem.IsMacOS()
                    ? "open"
                    : "xdg-open";

            var info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = Quote(path),
                UseShellExecute = false
            };

            return Process.Start(info) != null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

public sealed class LogNotificationService : INotificationService
{
    public event Action<string>? Message;

    public void Info(string message) => Message?.Invoke(message);

    public void Warning(string message) => Message?.Invoke("Warning: " + message);

    public void Error(string message) => Message?.Invoke("Error: " + message);
}
