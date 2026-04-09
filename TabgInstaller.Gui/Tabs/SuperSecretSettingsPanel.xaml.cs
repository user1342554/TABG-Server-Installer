using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class SuperSecretSettingsPanel : UserControl
    {
        public SuperSecretSettingsPanel()
        {
            InitializeComponent();
        }

        private SuperSecretSettingsViewModel? Vm =>
            DataContext as SuperSecretSettingsViewModel;

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (Vm != null)
                Vm.PasswordInput = PwdBox.Password;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Vm?.Cleanup();
        }
    }
}
