using System;
using System.Windows;

namespace TabgInstaller.Gui
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Ensure Config tab starts disabled (XAML sets IsEnabled=false already)
        }
    }
}