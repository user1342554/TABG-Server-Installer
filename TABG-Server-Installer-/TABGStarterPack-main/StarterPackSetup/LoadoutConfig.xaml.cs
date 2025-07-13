using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
    /// Interaction logic for LoadoutConfig.xaml
    /// </summary>
    public partial class LoadoutConfig : Page
    {
        List<string> loadout;
        public LoadoutConfig()
        {
            InitializeComponent();
            categoriesTreeView.SelectedItemChanged += CategoriesTreeView_SelectedItemChanged;
            loadout = new List<string>();
        }

        private void CategoriesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem selectedItem)
            {
                if (selectedItem.Items.Count == 0) // Leaf node (item)
                {
                    // Handle item selection here
                    string selectedCategory = ((TreeViewItem)selectedItem.Parent).Header.ToString();
                    string selectedItemName = selectedItem.Header.ToString();
                    // Perform actions based on the selected category and item
                    // For example, add the selected item to the loadoutListBox
                    //loadoutListBox.Items.Add($"{selectedCategory} - {selectedItemName}");
                }
                else // Parent node (category)
                {
                    // Expand or collapse the selected category
                    selectedItem.IsExpanded = !selectedItem.IsExpanded;
                }
            }
        }

        private void AddToLoadout_Click(object sender, RoutedEventArgs e)
        {
            // Add selected item to the loadoutListBox
            if (categoriesTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Items.Count == 0 && !string.IsNullOrEmpty(quantityTextBox.Text))
            {
                string selectedCategory = ((TreeViewItem)selectedItem.Parent).Header.ToString();
                string selectedItemName = selectedItem.Header.ToString();
                string selectedQuantity = quantityTextBox.Text;
                int quan = int.Parse(selectedQuantity);
                while (quan > 255)
                {
                    loadoutListBox.Items.Add($"{selectedCategory} - {selectedItemName} - Quantity: {255}");
                    loadout.Add(string.Format("{0}:{1},", selectedItem.Tag.ToString(), 255));
                    quan -= 255;
                }
                loadoutListBox.Items.Add($"{selectedCategory} - {selectedItemName} - Quantity: {quan}");
                loadout.Add(string.Format("{0}:{1},", selectedItem.Tag.ToString(), quan));
            }
        }

        private void ManageLoadouts_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new ExistingLoadouts());
        }

        private void SaveLoadout_Click(object sender, RoutedEventArgs e)
        {
            string l = "";
            string n = string.Format("{0}:{1}%",LoadoutNameTextBox.Text,LoadoutRarityTextBox.Text);
            foreach(string str in loadout)
            {
                l += str;
            }
            if (l.Length > 1)
            {
                l = l.Remove(l.Length - 1);
                l += "/";
;               n += l;
                Debug.WriteLine(n.ToString());

                loadoutListBox.Items.Clear();
                loadout.Clear();
                LoadoutNameTextBox.Text = "";
                LoadoutRarityTextBox.Text = "";

                string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "TheStarterPack.txt");
                //Overwrite config file with new loadout info
                string[] lines = File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("Loadouts="))
                    {
                        lines[i] += n;
                        break;
                    }
                }
                File.WriteAllLines(path, lines);
            }
        }

        private void RemoveLoadout_Click(object sender, RoutedEventArgs e)
        {
            if (loadoutListBox.SelectedItem != null)
            {
                //Find selected key in Dictionary
                loadoutListBox.Items.IndexOf(loadoutListBox.SelectedItem);
                loadout.RemoveAt(loadoutListBox.Items.IndexOf(loadoutListBox.SelectedItem));
                // Remove the selected item from the loadoutListBox
                loadoutListBox.Items.Remove(loadoutListBox.SelectedItem);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new MainMenu());
        }
    }
}
