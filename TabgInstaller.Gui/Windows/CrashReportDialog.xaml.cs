using System.Windows;

namespace TabgInstaller.Gui.Windows;

public partial class CrashReportDialog : Window
{
    public bool? SendReport { get; private set; }
    public bool? RememberChoice { get; private set; }

    public CrashReportDialog()
    {
        InitializeComponent();
    }

    private void YesSend_Click(object sender, RoutedEventArgs e)
    {
        SendReport = true;
        RememberChoice = null;
        DialogResult = true;
    }

    private void NoThanks_Click(object sender, RoutedEventArgs e)
    {
        SendReport = false;
        RememberChoice = null;
        DialogResult = false;
    }

    private void AlwaysSend_Click(object sender, RoutedEventArgs e)
    {
        SendReport = true;
        RememberChoice = true;
        DialogResult = true;
    }

    private void NeverAsk_Click(object sender, RoutedEventArgs e)
    {
        SendReport = false;
        RememberChoice = false;
        DialogResult = false;
    }
}
