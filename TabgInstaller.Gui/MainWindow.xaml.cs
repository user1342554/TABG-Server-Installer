using System;
using System.Windows;
// using TabgInstaller.Gui.Windows; // removed

namespace TabgInstaller.Gui
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Ensure Config tab starts disabled (XAML sets IsEnabled=false already)
        }

        // Removed Loadout Creator
    }
}