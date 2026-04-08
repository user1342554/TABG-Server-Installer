using System.Windows.Controls;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class MatchSettingsPanel : UserControl
    {
        private MatchSettingsViewModel? _vm;

        public MatchSettingsPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called by ConfigPanel after DI resolves the ViewModel and passes the server directory.
        /// The ViewModel itself subscribes to IServerPathProvider.PathChanged, so this method
        /// is only needed for panels that are initialized before the path provider fires.
        /// </summary>
        public void SetViewModel(MatchSettingsViewModel vm)
        {
            _vm = vm;
            DataContext = vm;
        }
    }
}
