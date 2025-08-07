using System;
using System.Windows;

namespace TabgInstaller.Gui
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            AiChatTab.ServerPath = Environment.CurrentDirectory;
        }
    }
}