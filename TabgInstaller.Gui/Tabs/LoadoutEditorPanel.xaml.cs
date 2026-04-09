using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class LoadoutEditorPanel : UserControl
    {
        public LoadoutEditorPanel()
        {
            InitializeComponent();
        }

        public void SetViewModel(LoadoutEditorViewModel vm)
        {
            DataContext = vm;

            // Populate category combo from ItemDatabase
            foreach (var cat in TabgInstaller.Core.Model.ItemDatabase.Categories)
                CmbCategory.Items.Add(cat);
            if (CmbCategory.Items.Count > 0)
                CmbCategory.SelectedIndex = 0;
        }

        private LoadoutEditorViewModel? Vm => DataContext as LoadoutEditorViewModel;

        // ── Name / Percent text boxes — delegates to ViewModel ─────────────────

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            Vm?.UpdateSelectedLoadoutName(TxtName.Text);

            // Force ListBox to refresh display names
            var idx = LstLoadouts.SelectedIndex;
            LstLoadouts.Items.Refresh();
            LstLoadouts.SelectedIndex = idx;
        }

        private void TxtPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            Vm?.UpdateSelectedLoadoutPercent(TxtPercent.Text);

            var idx = LstLoadouts.SelectedIndex;
            LstLoadouts.Items.Refresh();
            LstLoadouts.SelectedIndex = idx;
        }

        // ── Selection Changed — sync text boxes from SelectedLoadout ──────────

        private void LstLoadouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var lo = LstLoadouts.SelectedItem as LoadoutVm;
            PnlEditor.IsEnabled = lo != null;

            if (lo == null)
            {
                TxtName.Text = "";
                TxtPercent.Text = "";
                return;
            }

            // Suppress TextChanged feedback while populating
            TxtName.TextChanged -= TxtName_TextChanged;
            TxtPercent.TextChanged -= TxtPercent_TextChanged;

            TxtName.Text = lo.Name;
            TxtPercent.Text = lo.Percent.ToString();

            TxtName.TextChanged += TxtName_TextChanged;
            TxtPercent.TextChanged += TxtPercent_TextChanged;
        }

        // ── Category dropdown populates item sub-list ─────────────────────────

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CmbItem.Items.Clear();
            if (CmbCategory.SelectedItem is string cat)
            {
                foreach (var item in TabgInstaller.Core.Model.ItemDatabase.ByCategory(cat))
                    CmbItem.Items.Add($"{item.Name} ({item.Id})");
                if (CmbItem.Items.Count > 0)
                    CmbItem.SelectedIndex = 0;
            }
        }

        // ── Mode ComboBox — marks dirty ───────────────────────────────────────

        private void CmbMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Sync SelectedMode on the ViewModel when the ComboBox changes
            if (Vm == null) return;
            Vm.SelectedMode = CmbMode.SelectedIndex switch
            {
                1 => "GunGame",
                2 => "ReverseGunGame",
                3 => "KeepInventory",
                _ => "Normal"
            };
            Vm.MarkDirty();
        }

        // ── Import Raw dialog ─────────────────────────────────────────────────

        private void ImportRaw_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            var dlg = new Window
            {
                Title = "Import Raw Loadouts",
                Width = 600,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var sp = new StackPanel { Margin = new System.Windows.Thickness(8) };
            sp.Children.Add(new TextBlock
            {
                Text = "Paste the raw loadout string (including optional GunGame/ prefix):",
                Margin = new System.Windows.Thickness(0, 0, 0, 4)
            });
            var txt = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 180
            };
            sp.Children.Add(txt);
            var btn = new Button
            {
                Content = "Import",
                Width = 80,
                Margin = new System.Windows.Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btn.Click += (_, _) =>
            {
                Vm.ImportRawCommand.Execute(txt.Text.Trim());
                dlg.Close();
            };
            sp.Children.Add(btn);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        // ── Export Raw dialog ─────────────────────────────────────────────────

        private void ExportRaw_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            var full = Vm.BuildExportRaw();

            var dlg = new Window
            {
                Title = "Export Raw Loadouts",
                Width = 600,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var sp = new StackPanel { Margin = new System.Windows.Thickness(8) };
            sp.Children.Add(new TextBlock
            {
                Text = "Raw loadout string (copy this):",
                Margin = new System.Windows.Thickness(0, 0, 0, 4)
            });
            var txtBox = new TextBox
            {
                Text = full,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 180,
                IsReadOnly = true
            };
            sp.Children.Add(txtBox);
            var btnCopy = new Button
            {
                Content = "Copy to Clipboard",
                Width = 130,
                Margin = new System.Windows.Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btnCopy.Click += (_, _) =>
            {
                Clipboard.SetText(full);
                Vm.StatusText = "Copied to clipboard.";
            };
            sp.Children.Add(btnCopy);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        // ── Add Item button — delegates to ViewModel ──────────────────────────

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            // If search results visible and something selected, add from search
            if (LstSearchResults.Visibility == Visibility.Visible
                && LstSearchResults.SelectedItem is ItemSearchResult searchResult)
            {
                Vm.AddItemFromSearchCommand.Execute(searchResult);
                return;
            }

            // Otherwise add from category dropdown — sync the selected item first
            if (CmbItem.SelectedItem is string selected)
            {
                Vm.SelectedCategoryItem = selected;
                Vm.AddItemFromCategoryCommand.Execute(null);
            }
        }

        // ── Remove Item button (Tag-based) ────────────────────────────────────

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            if (sender is Button btn && btn.Tag is ItemDisplayRow row)
                Vm.RemoveItemCommand.Execute(row);
        }

        // ── Search results double-click ───────────────────────────────────────

        private void LstSearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (LstSearchResults.SelectedItem is ItemSearchResult item)
                Vm.AddItemFromSearchCommand.Execute(item);
        }

        // ── DataGrid CellEditEnding — delegates quantity update to ViewModel ──

        private void DgItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (Vm == null) return;
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (e.Column.Header?.ToString() != "Qty") return;

            if (e.EditingElement is TextBox tb && e.Row.Item is ItemDisplayRow row)
            {
                if (int.TryParse(tb.Text, out var newQty))
                    Vm.UpdateItemQuantity(row, newQty);
            }
        }
    }
}
