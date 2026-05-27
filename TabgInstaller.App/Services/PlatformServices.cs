using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Threading.Tasks;
using TabgInstaller.Core;
using TabgInstaller.UI.Services;

namespace TabgInstaller.App.Services;

public sealed class AvaloniaStoragePickerService : IStoragePickerService
{
    private readonly Window _owner;

    public AvaloniaStoragePickerService(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickFileAsync(string title, params string[] patterns)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
    private readonly Window _owner;

    public AvaloniaConfirmationDialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
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
        var result = await dialog.ShowDialog<bool?>(_owner);
        return result == true;
    }
}

public sealed class AvaloniaClipboardService : IClipboardService
{
    private readonly Window _owner;

    public AvaloniaClipboardService(Window owner)
    {
        _owner = owner;
    }

    public async Task SetTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(_owner)?.Clipboard;
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

public sealed class InstallerSteamPathDetector : ISteamPathDetector
{
    public string? TryFindServerPath() => Installer.TryFindTabgServerPath();

    public string? TryFindClientPath() => Installer.TryFindTabgClientPath();
}
