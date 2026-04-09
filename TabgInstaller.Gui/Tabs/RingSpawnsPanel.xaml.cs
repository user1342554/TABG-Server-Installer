using System.Windows.Controls;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class RingSpawnsPanel : UserControl
    {
        public RingSpawnsPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called by ConfigPanel after DI resolves the ViewModel.
        /// The ViewModel subscribes to IServerPathProvider.PathChanged internally.
        /// </summary>
        public void SetViewModel(RingSpawnsViewModel vm)
        {
            DataContext = vm;
        }
    }
}
