using System.Windows;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Windows
{
    public partial class AddServerDialog : Window
    {
        public AddServerDialogViewModel ViewModel { get; }

        public AddServerDialog()
        {
            ViewModel = new AddServerDialogViewModel();
            ViewModel.CloseAction = () => Close();
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
