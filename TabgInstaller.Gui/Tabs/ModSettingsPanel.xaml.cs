using System.Windows.Controls;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ModSettingsPanel : UserControl
    {
        public ModSettingsPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called by ConfigPanel after DI resolves the ViewModel.
        /// The ViewModel subscribes to IServerPathProvider.PathChanged internally.
        /// </summary>
        public void SetViewModel(ModSettingsViewModel vm)
        {
            DataContext = vm;

            // Wire ComboBox item sources — these contain display strings the ViewModel exposes.
            CmbAttackerGrenade.ItemsSource = vm.GrenadeDisplayItems;
            CmbCorpseGrenade.ItemsSource = vm.GrenadeDisplayItems;
        }
    }
}
