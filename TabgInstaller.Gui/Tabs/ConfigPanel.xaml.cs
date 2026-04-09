using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ConfigPanel : UserControl
    {
        public ConfigPanel()
        {
            InitializeComponent();
        }

        // Called by MainWindow for sub-panels that are not yet fully MVVM-migrated.
        public void InitializeSubPanels(string serverDir)
        {
            LoadoutEditorControl.Initialize(serverDir);
            ModSettingsControl.Initialize(serverDir);
        }
    }
}
