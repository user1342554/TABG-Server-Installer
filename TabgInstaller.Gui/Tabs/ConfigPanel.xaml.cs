using System.Windows.Controls;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ConfigPanel : UserControl
    {
        public ConfigPanel()
        {
            InitializeComponent();
        }

        public void InitializeSubPanels(string serverDir)
        {
            // LoadoutEditorControl is now MVVM — SetViewModel is called from MainWindow.
            // Keep this method stub for any remaining non-MVVM sub-panels in future.
        }

        public void SetLoadoutEditorViewModel(LoadoutEditorViewModel vm)
        {
            LoadoutEditorControl.SetViewModel(vm);
        }
    }
}
