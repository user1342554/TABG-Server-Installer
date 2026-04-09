using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ServerModsPanel : UserControl
    {
        private ServerModsViewModel? ViewModel => DataContext as ServerModsViewModel;

        public ServerModsPanel()
        {
            InitializeComponent();
        }

        private void AddDll_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll",
                Title = "Select plugin DLL"
            };

            if (dialog.ShowDialog() == true)
                ViewModel?.AddDll(dialog.FileName);
        }
    }
}
