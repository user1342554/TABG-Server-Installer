using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
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
    /// Interaction logic for LobbyConfig.xaml
    /// </summary>
    public partial class LobbyConfig : Page
    {
        public LobbyConfig()
        {
            InitializeComponent();

            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "TheStarterPack.txt");

            string[] array = File.ReadAllLines(path);

            foreach (string line in array)
            {
                if (line.Contains('='))
                {
                    string[] array1 = line.Split('=');
                    if (string.IsNullOrEmpty(array1[1]))
                    {
                        //No value, stop attempting to solve it
                        continue;
                    }
                    if (array1[0] == "PreMatchTimeout")
                    {
                        LobbyTimeout.Text = array1[1] ?? "15";
                    }
                    if (array1[0] == "ValidSpawnPoints")
                    {
                        string[] str = array1[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (str.Length > 0)
                        {
                            if (str.Any(s => s.Contains("0"))) { checkbox0.IsChecked = true; }
                            if (str.Any(s => s.Contains("1"))) { checkbox1.IsChecked = true; }
                            if (str.Any(s => s.Contains("2"))) { checkbox2.IsChecked = true; }
                            if (str.Any(s => s.Contains("3"))) { checkbox3.IsChecked = true; }
                            if (str.Any(s => s.Contains("4"))) { checkbox4.IsChecked = true; }
                            if (str.Any(s => s.Contains("5"))) { checkbox5.IsChecked = true; }
                            if (str.Any(s => s.Contains("6"))) { CustomSpawnCheckBox.IsChecked = true; }
                        }

                    }
                    if (array1[0] == "CustomSpawnPoint")
                    {
                        string[] array2 = array1[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        CustomSpawnX.Text = array2[0] ?? "0";
                        CustomSpawnY.Text = array2[1] ?? "0";
                        CustomSpawnZ.Text = array2[2] ?? "0";
                    }
                    if (array1[0] == "PercentOfVotes")
                    {
                        PercentVotes.Text = array1[1] ?? "50";
                    }
                    if (array1[0] == "MinNumberOfVotes")
                    {
                        MinPlayers.Text = array1[1] ?? "2";
                    }
                    if (array1[0] == "TimeToStart")
                    {
                        TimeStart.Text = array1[1] ?? "30";
                    }
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new MainMenu());
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "TheStarterPack.txt");
            //Overwrite config file with new loadout info
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("PreMatchTimeout="))
                {
                    lines[i] = string.Format("PreMatchTimeout={0}", LobbyTimeout.Text);
                }

                if (lines[i].StartsWith("ValidSpawnPoints="))
                {
                    List<int> result = new List<int>();
                    if ((bool)checkbox0.IsChecked) { result.Add(0); }
                    if ((bool)checkbox1.IsChecked) { result.Add(1); }
                    if ((bool)checkbox2.IsChecked) { result.Add(2); }
                    if ((bool)checkbox3.IsChecked) { result.Add(3); }
                    if ((bool)checkbox4.IsChecked) { result.Add(4); }
                    if ((bool)checkbox5.IsChecked) { result.Add(5); }
                    if ((bool)CustomSpawnCheckBox.IsChecked) { result.Add(6); }

                    string str = "";
                    foreach (int j in result)
                    {
                        str += j;
                        str += ',';
                    }

                    lines[i] = string.Format("ValidSpawnPoints={0}", str);
                }
                if (lines[i].StartsWith("CustomSpawnPoint="))
                {
                    if (CustomSpawnX.Text != "" && CustomSpawnX.Text != "X")
                    {
                        lines[i] = string.Format("CustomSpawnPoint={0},{1},{2}", CustomSpawnX.Text, CustomSpawnY.Text, CustomSpawnZ.Text);
                    }
                }
                if (lines[i].StartsWith("PercentOfVotes="))
                {
                    lines[i] = string.Format("PercentOfVotes={0}", PercentVotes.Text);
                }
                if (lines[i].StartsWith("MinNumberOfPlayer="))
                {
                    lines[i] = string.Format("MinNumberOfPlayer={0}", MinPlayers.Text);
                }
                if (lines[i].StartsWith("TimeToStart="))
                {
                    lines[i] = string.Format("TimeToStart={0}", TimeStart.Text);
                }
            }
            File.WriteAllLines(path, lines);
        }
    }
}
