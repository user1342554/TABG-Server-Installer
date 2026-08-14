using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace TabgInstaller.App;

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Default;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            Dispatcher.UIThread.UnhandledException += (_, args) =>
            {
                if (desktop.MainWindow is MainWindow mainWindow)
                    mainWindow.ReportUnexpectedUiException(args.Exception);
                args.Handled = true;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
