using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic; // For InputBox prompt
using TabgInstaller.Core;

namespace TabgInstaller.Gui.Tabs
{
    public partial class PresetsGrid : UserControl
    {
        private string _serverDir = string.Empty;

        public PresetsGrid()
        {
            InitializeComponent();
        }

        // Simple POCO for binding file checkboxes
        private class FileEntry
        {
            public string RelativePath { get; set; } = string.Empty;
            public string Display => RelativePath.Replace("\\", "/");
            public bool IsSelected { get; set; } = true;
        }

        private ObservableCollection<FileEntry> _fileEntries = new();

        public void SetServerPath(string serverDir)
        {
            _serverDir = serverDir;
            RefreshPresets();
            BuildFileList();
        }

        private void RefreshPresets()
        {
            LstPresets.ItemsSource = PresetManager.ListPresets(_serverDir).OrderBy(p => p).ToList();
        }

        private void BuildFileList()
        {
            _fileEntries.Clear();
            foreach (var rel in PresetManager.DefaultConfigRelativePaths)
            {
                var abs = Path.Combine(_serverDir, rel);
                if (File.Exists(abs))
                {
                    _fileEntries.Add(new FileEntry { RelativePath = rel, IsSelected = true });
                }
            }
            FilesList.ItemsSource = _fileEntries;
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_serverDir)) return;

            // Prompt for preset name
            string presetName = Interaction.InputBox("Enter a name for the preset:", "Save Preset", "MyPreset");
            if (string.IsNullOrWhiteSpace(presetName)) return;

            var selectedPaths = _fileEntries.Where(f => f.IsSelected).Select(f => f.RelativePath).ToArray();
            if (selectedPaths.Length == 0)
            {
                MessageBox.Show("Please select at least one file to include.");
                return;
            }

            try
            {
                PresetManager.SavePreset(_serverDir, presetName, selectedPaths);
                RefreshPresets();
                MessageBox.Show($"Preset '{presetName}' saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save preset: {ex.Message}");
            }
        }

        private void LoadPreset_Click(object sender, RoutedEventArgs e)
        {
            if (LstPresets.SelectedItem is not string presetName)
            {
                MessageBox.Show("Please select a preset to load.");
                return;
            }

            var result = MessageBox.Show($"Overwrite current config files with preset '{presetName}'?", "Load Preset", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                PresetManager.LoadPreset(_serverDir, presetName);
                MessageBox.Show($"Preset '{presetName}' loaded.", "Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load preset: {ex.Message}");
            }
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (LstPresets.SelectedItem is not string presetName)
            {
                MessageBox.Show("Please select a preset to delete.");
                return;
            }

            var result = MessageBox.Show($"Delete preset '{presetName}'? This cannot be undone.", "Delete Preset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                PresetManager.DeletePreset(_serverDir, presetName);
                RefreshPresets();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete preset: {ex.Message}");
            }
        }
    }
} 