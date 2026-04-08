using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class SettingsPanel : UserControl
    {
        public event System.Action? RequestHardReset;

        public SettingsPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var settings = AppSettingsServiceStatic.Load();
            TxtServerPath.Text = settings.ServerPath;
            TxtClientPath.Text = string.IsNullOrEmpty(settings.ClientPath) ? "(not set)" : settings.ClientPath;
            TxtModdedPath.Text = string.IsNullOrEmpty(settings.ClientModdedPath) ? "(not set)" : settings.ClientModdedPath;

            try
            {
                var ver = UpdateService.GetCurrentVersion();
                TxtVersion.Text = $"Version {ver.Major}.{ver.Minor}.{ver.Build}";
            }
            catch
            {
                TxtVersion.Text = "Version unknown";
            }
        }

        private void HardReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset everything and run the setup wizard again.\n\nA backup of your current server will be created first.\n\nContinue?",
                "Confirm Hard Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                AppSettingsServiceStatic.Reset();
                RequestHardReset?.Invoke();
            }
        }
    }
}
