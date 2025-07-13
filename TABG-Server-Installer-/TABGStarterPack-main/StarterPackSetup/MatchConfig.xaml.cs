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
    /// Interaction logic for MatchConfig.xaml
    /// </summary>
    public partial class MatchConfig : Page
    {
        public MatchConfig()
        {
            InitializeComponent();
            HealOnKillCheck.Checked += HealOnKillCheck_Checked;
            HealOnKillCheck.Unchecked += HealOnKillCheck_Unchecked;

            SpelldropsEnabled.Checked += SpellDropCheck_Checked;
            SpelldropsEnabled.Unchecked += SpellDropCheck_Unchecked;


            //Pull data from config file
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
                    if (array1[0] == "PeriMatchTimeout")
                    {
                        MatchTimeout.Text = array1[1];
                    }
                    if (array1[0] == "WinCondition")
                    {
                        if (array1[1] == "Debug") { Debug.IsChecked = true; }
                        else if (array1[1] == "Default") { Default.IsChecked = true; }
                        else if (array1[1] == "KillsToWin") { KillsToWin.IsChecked = true; }
                    }
                    if (array1[0] == "KillsToWin")
                    {
                        KillstoWinNum.Text = array1[1];
                    } 
                    if (array1[0] == "HealOnKill")
                    {
                        HealOnKillCheck.IsChecked = bool.Parse(array1[1]);
                    }
                    if (array1[0] == "SpelldropEnabled")
                    {
                        SpelldropsEnabled.IsChecked = bool.Parse(array1[1]);
                    }
                    if (array1[0] == "MinSpellDropDelay")
                    {
                        MinTime.Text= array1[1];
                    }
                    if (array1[0] == "MaxSpellDropDelay")
                    {
                        MaxTime.Text = array1[1];
                    }
                    if (array1[0] == "SpellDropOffset")
                    {
                        TimeOffset.Text = array1[1];
                    }
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new MainMenu());
        }

        private void HealOnKillCheck_Checked(object sender, RoutedEventArgs e)
        {
            HealKillAmount.IsEnabled = true;
        }
        private void HealOnKillCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            HealKillAmount.IsEnabled = false;
        }

        private void SpellDropCheck_Checked(object sender, RoutedEventArgs e)
        {
            SpellDropBody.IsEnabled = true;
        }
        private void SpellDropCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            SpellDropBody.IsEnabled = false;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "TheStarterPack.txt");
            //Overwrite config file with new loadout info
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains('='))
                {
                    string[] array1 = lines[i].Split('=');
                    if (array1[0] == "PeriMatchTimeout")
                    {
                        lines[i] = string.Format("PeriMatchTimeout={0}",MatchTimeout.Text);
                    }
                    if (array1[0] == "WinCondition")
                    {
                        if ((bool)Debug.IsChecked) { lines[i] = string.Format("WinCondition={0}", "Debug"); }
                        else if ((bool)Default.IsChecked) { lines[i] = string.Format("WinCondition={0}", "Default"); }
                        else if ((bool)KillsToWin.IsChecked){ lines[i] = string.Format("WinCondition={0}", "KillsToWin"); }
                    }
                    if (array1[0] == "KillsToWin")
                    {
                        lines[i] = string.Format("KillsToWin={0}", KillstoWinNum.Text);
                    }
                    if (array1[0] == "HealOnKill")
                    {
                        lines[i] = string.Format("HealOnKill={0}", HealOnKillCheck.IsChecked.ToString());
                    }
                    if (array1[0] == "SpelldropEnabled")
                    {
                        lines[i] = string.Format("SpelldropEnabled={0}", SpelldropsEnabled.IsChecked.ToString());
                    }
                    if (array1[0] == "MinSpellDropDelay")
                    {
                        lines[i] = string.Format("MinSpellDropDelay={0}", MinTime.Text);
                    }
                    if (array1[0] == "MaxSpellDropDelay")
                    {
                        lines[i] = string.Format("MaxSpellDropDelay={0}", MaxTime.Text);
                    }
                    if (array1[0] == "SpellDropOffset")
                    {
                        lines[i] = string.Format("SpellDropOffset={0}", TimeOffset.Text);
                    }
                }
            }
            File.WriteAllLines(path, lines);
        }
    }
}
