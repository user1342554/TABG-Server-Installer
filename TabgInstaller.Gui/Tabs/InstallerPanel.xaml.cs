using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class InstallerPanel : UserControl
    {
        public InstallerPanel()
        {
            InitializeComponent();
        }

        private InstallerPanelViewModel? Vm => DataContext as InstallerPanelViewModel;

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select TABG Server Folder"
            };

            var detectedPath = Installer.TryFindTabgServerPath();
            if (!string.IsNullOrEmpty(detectedPath) && Directory.Exists(detectedPath))
                dialog.InitialDirectory = Path.GetDirectoryName(detectedPath) ?? detectedPath;

            if (dialog.ShowDialog() == true)
            {
                var selectedPath = dialog.FolderName;
                if (!File.Exists(Path.Combine(selectedPath, "TABG.exe")))
                    ToastService.Instance.Warning(
                        "Selected folder may not be a TABG server directory (no TABG.exe found).");
                Vm?.SetServerPath(selectedPath);
            }
        }
    }
}
