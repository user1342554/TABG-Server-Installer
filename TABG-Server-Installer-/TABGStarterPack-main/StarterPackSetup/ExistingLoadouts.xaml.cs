using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Channels;
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
    /// Interaction logic for ExistingLoadouts.xaml
    /// </summary>
    public partial class ExistingLoadouts : Page
    {
        List<string> loadoutList;
        string path;
        public ExistingLoadouts()
        {
            InitializeComponent();

            loadoutList = new List<string>();

            path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "TheStarterPack.txt");

            string[] array = File.ReadAllLines(path);

            foreach(string line in array)
            {
                if (line.Contains('='))
                {
                    string[] array1 = line.Split('=');
                    if (array1[0] == "Loadouts")
                    {
                        string[] loadouts = array1[1].Split(new char[] { '/' },StringSplitOptions.RemoveEmptyEntries);

                        foreach(string loadout in loadouts)
                        {
                            loadoutList.Add(loadout);
                        }
                        break;
                    }
                }
            }
            LoadoutListView.ItemsSource = loadoutList;
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.GoBack();
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            int index = LoadoutListView.SelectedIndex;
            loadoutList.RemoveAt(index);

            //Remove from display
            LoadoutListView.ItemsSource = null;
            LoadoutListView.Items.Clear();
            LoadoutListView.ItemsSource = loadoutList;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            //Overwrite config file with new loadout info
            string[] lines = File.ReadAllLines(path);

            for(int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Loadouts="))
                {
                    string str = "";
                    foreach(string line in loadoutList)
                    {
                        str += line;
                        str += '/';
                    }
                    lines[i] = string.Format("Loadouts={0}",str);
                    break;
                }
            }
            File.WriteAllLines(path, lines);
        }

    }
}
