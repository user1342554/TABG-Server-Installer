using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ClientPanel : UserControl
    {
        private ClientPanelViewModel? ViewModel => DataContext as ClientPanelViewModel;

        public ClientPanel()
        {
            InitializeComponent();
        }

        private void BrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select TABG Steam Folder" };
            if (dialog.ShowDialog() == true)
                ViewModel?.SetClientPath(dialog.FolderName);
        }

        private void AddDll_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll",
                Title = "Select client mod DLL"
            };

            if (dialog.ShowDialog() == true)
                ViewModel?.AddDll(dialog.FileName);
        }
    }
}
