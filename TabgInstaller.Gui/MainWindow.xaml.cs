using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Components.Layout;

namespace TabgInstaller.Gui;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_DLGMODALFRAME = 0x0001;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    public MainWindow()
    {
        InitializeComponent();
        RootComponent.ComponentType = typeof(MainLayout);
        WebView.Services = App.Services;
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // Remove icon from title bar
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_DLGMODALFRAME);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

            // Set caption (title bar) color to match --bg (#F0EDED)
            int captionColor = 0x00EDEDF0; // BGR format: 0x00BBGGRR
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            // Set title text color to same as bg so it's invisible
            int textColor = 0x00EDEDF0; // BGR format
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
        }
        catch
        {
            // DWM attributes not supported on older Windows — just ignore
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var updater = new UpdateService();
            var update = await updater.CheckForUpdateAsync();
            if (update == null) return;

            var (tag, version, url) = update.Value;
            var current = UpdateService.GetCurrentVersion();

            var result = MessageBox.Show(
                $"A new version is available!\n\n" +
                $"Current: {current.Major}.{current.Minor}.{current.Build}\n" +
                $"New: {version.Major}.{version.Minor}.{version.Build} ({tag})\n\n" +
                "Download and install now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes) return;

            Title = "TABG Manager — Updating...";
            bool ok = await updater.ApplyUpdateAsync(url);
            if (ok)
            {
                Application.Current.Shutdown();
            }
            else
            {
                MessageBox.Show("Update failed. You can download manually from GitHub.",
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Title = "TABG Manager";
            }
        }
        catch
        {
            // Never block startup for update check failures
        }
    }
}
