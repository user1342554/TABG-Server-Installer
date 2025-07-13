using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StarterPackSetup
{
    /// <summary>
    /// Interaction logic for MainMenu.xaml
    /// </summary>
    public partial class MainMenu : Page
    {
        public MainMenu()
        {
            InitializeComponent();


            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "TheStarterPack.txt");

            if(!File.Exists(path) )
            {
                MessageBox.Show("Config cannot be found in this directory. Please make sure this file is in the same location as the config");
                Environment.Exit(0);
            }
        }

        private void BtnMap_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new MapScreen());

        }
        private void BtnRespawn_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new RespawnConfig());

        }
        private void BtnLoadout_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new LoadoutConfig());

        }
        private void BtnLobby_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new LobbyConfig());

        }
        private void BtnMatch_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new MatchConfig());

        }

    }
}
