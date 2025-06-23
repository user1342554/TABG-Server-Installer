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
    /// Interaction logic for RespawnConfig.xaml
    /// </summary>
    public partial class RespawnConfig : Page
    {
        public RespawnConfig()
        {
            InitializeComponent();
            //BufferCheckBox.Checked += Buffer_Checked;
            //BufferCheckBox.Unchecked += Buffer_Unchecked;

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
                    if (array1[0] == "CanGoDown")
                    {
                        checkBox2.IsChecked = bool.Parse(array1[1]);
                    }
                    if (array1[0] == "CanLockOut")
                    {
                        checkBox3.IsChecked = bool.Parse(array1[1]);
                    }
                    if (array1[0] == "ForceKillAtStart")
                    {
                        checkBox4.IsChecked = bool.Parse(array1[1]);
                    }
                    /*if (array1[0] == "BufferZone")
                    {
                        PercentVotes.Text = array1[1];
                    }*/
                    if (array1[0] == "DropItemsOnDeath")
                    {
                        ItemDropCheckbox.IsChecked = bool.Parse(array1[1]);
                    }

                    //Given items
                    if (array1[0] == "ItemsGiven")
                    {
                        string[] array2 = array1[1].Split(new char[] {','},StringSplitOptions.RemoveEmptyEntries);
                        foreach(string str in array2)
                        {
                            string[] set = str.Split(':');
                            if (int.Parse(set[0]) == 1)
                            {
                                BigTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 2)
                            {
                                BoltTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 3)
                            {
                                MoneyTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 4)
                            {
                                MusketTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 6)
                            {
                                NormalTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 7)
                            {
                                RocketTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 8)
                            {
                                ShotgunTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 9)
                            {
                                SmallTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 11)
                            {
                                TaserTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 12)
                            {
                                WaterTextbox.Text = set[1];
                            }

                            if (int.Parse(set[0]) == 131)
                            {
                                BandageTextbox.Text = set[1];
                            }
                            if (int.Parse(set[0]) == 132)
                            {
                                MedkitTextbox.Text = set[1];
                            }
                        }
                    }
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new MainMenu());
        }

        private void Buffer_Checked(object sender, RoutedEventArgs e)
        {
            //BufferSizeTextBox.IsEnabled = true;
        }
        private void Buffer_Unchecked(object sender, RoutedEventArgs e)
        {
            //BufferSizeTextBox.IsEnabled = false;
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
                    if (array1[0] == "CanGoDown")
                    {
                        lines[i] = string.Format("CanGoDown={0}", ((bool)checkBox2.IsChecked).ToString());
                    }
                    if (array1[0] == "CanLockOut")
                    {
                        lines[i] = string.Format("CanLockOut={0}", ((bool)checkBox3.IsChecked).ToString());
                    }
                    if (array1[0] == "ForceKillAtStart")
                    {
                        lines[i] = string.Format("ForceKillAtStart={0}", ((bool)checkBox4.IsChecked).ToString());
                    }
                    /*if (array1[0] == "BufferZone")
                    {
                        PercentVotes.Text = array1[1];
                    }*/
                    if (array1[0] == "DropItemsOnDeath")
                    {
                        lines[i] = string.Format("DropItemsOnDeath={0}", ((bool)ItemDropCheckbox.IsChecked).ToString());
                    }

                    //Given items
                    if (array1[0] == "ItemsGiven")
                    {
                        string str = "";
                        if(!String.IsNullOrEmpty(BigTextbox.Text)) { str += "1:" + BigTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(BoltTextbox.Text)) { str += "2:" + BoltTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(MoneyTextbox.Text)) { str += "3:" + MoneyTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(MusketTextbox.Text)) { str += "4:" + MusketTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(NormalTextbox.Text)) { str += "6:" + NormalTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(RocketTextbox.Text)) { str += "7:" + RocketTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(ShotgunTextbox.Text)) { str += "8:" + ShotgunTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(SmallTextbox.Text)) { str += "9:" + SmallTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(TaserTextbox.Text)) { str += "11:" + TaserTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(WaterTextbox.Text)) { str += "12:" + WaterTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(BandageTextbox.Text)) { str += "131:" + BandageTextbox.Text + ","; }
                        if(!String.IsNullOrEmpty(MedkitTextbox.Text)) { str += "132:" + MedkitTextbox.Text + ","; }
                        lines[i] = string.Format("ItemsGiven={0}", str);
                    }
                }
            }
            File.WriteAllLines(path, lines);
        }
    }
}
