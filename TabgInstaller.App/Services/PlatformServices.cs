using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TabgInstaller.Core;

namespace TabgInstaller.App.Services;

public interface IStoragePickerService
{
    Task<string?> PickFolderAsync(Window owner, string title);

    Task<string?> PickFileAsync(Window owner, string title, params string[] patterns);
}

public interface IConfirmationDialogService
{
    Task<bool> ConfirmAsync(Window owner, string title, string message);
}

public interface IClipboardService
{
    Task SetTextAsync(Window owner, string text);
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

public sealed class AvaloniaStoragePickerService : IStoragePickerService
{
    public async Task<string?> PickFolderAsync(Window owner, string title)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickFileAsync(Window owner, string title, params string[] patterns)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Plugin DLL") { Patterns = patterns.Length == 0 ? new[] { "*" } : patterns }
            }
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}

public sealed class AvaloniaConfirmationDialogService : IConfirmationDialogService
{
    public async Task<bool> ConfirmAsync(Window owner, string title, string message)
    {
        var yes = new Button { Content = "Yes", IsDefault = true, MinWidth = 84 };
        var no = new Button { Content = "No", IsCancel = true, MinWidth = 84 };
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { no, yes }
                    }
                }
            }
        };

        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }
}

public sealed class AvaloniaClipboardService : IClipboardService
{
    public async Task SetTextAsync(Window owner, string text)
    {
        var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
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

public sealed class InstallerSteamPathDetector : ISteamPathDetector
{
    public string? TryFindServerPath() => Installer.TryFindTabgServerPath();

    public string? TryFindClientPath() => Installer.TryFindTabgClientPath();
}

public sealed class LogNotificationService : INotificationService
{
    public event Action<string>? Message;

    public void Info(string message) => Message?.Invoke(message);

    public void Warning(string message) => Message?.Invoke("Warning: " + message);

    public void Error(string message) => Message?.Invoke("Error: " + message);
}
