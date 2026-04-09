using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic; // For InputBox prompt
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class PresetsGrid : UserControl
    {
        private string _serverDir = string.Empty;

        public PresetsGrid()
        {
            InitializeComponent();
            LstTemplates.ItemsSource = BuiltInPresets.All;
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

        // ── Built-in Templates ──

        private void LstTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstTemplates.SelectedItem is BuiltInPresets.BuiltInPreset preset)
            {
                TxtTemplateDesc.Text = preset.Description;
                TxtTemplateNotes.Text = string.IsNullOrWhiteSpace(preset.Notes) ? "" : $"Notes: {preset.Notes}";
            }
            else
            {
                TxtTemplateDesc.Text = "";
                TxtTemplateNotes.Text = "";
            }
        }

        private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_serverDir))
            {
                ToastServiceStatic.Instance.Warning("No server directory set. Please install/select a server first.");
                return;
            }

            if (LstTemplates.SelectedItem is not BuiltInPresets.BuiltInPreset preset)
            {
                ToastServiceStatic.Instance.Warning("Please select a template to apply.");
                return;
            }

            var result = MessageBox.Show(
                $"Apply template '{preset.Name}'?\n\n" +
                "This will overwrite:\n" +
                string.Join("\n", preset.Files.Keys.Select(k => $"  - {k}")) +
                "\n\nContinue?",
                "Apply Server Template",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                BuiltInPresets.Deploy(preset, _serverDir);

                // Reload all config panels to reflect the new template settings
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.ReloadFromPath(_serverDir);
                }

                ToastServiceStatic.Instance.Success($"Template '{preset.Name}' applied successfully. All settings have been reloaded.");
            }
            catch (Exception ex)
            {
                ToastServiceStatic.Instance.Error($"Failed to apply template: {ex.Message}");
            }
        }

        // ── User Presets ──

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_serverDir)) return;

            // Prompt for preset name
            string presetName = Interaction.InputBox("Enter a name for the preset:", "Save Preset", "MyPreset");
            if (string.IsNullOrWhiteSpace(presetName)) return;

            var selectedPaths = _fileEntries.Where(f => f.IsSelected).Select(f => f.RelativePath).ToArray();
            if (selectedPaths.Length == 0)
            {
                ToastServiceStatic.Instance.Warning("Please select at least one file to include.");
                return;
            }

            try
            {
                PresetManager.SavePreset(_serverDir, presetName, selectedPaths);
                RefreshPresets();
                ToastServiceStatic.Instance.Success($"Preset '{presetName}' saved.");
            }
            catch (Exception ex)
            {
                ToastServiceStatic.Instance.Error($"Failed to save preset: {ex.Message}");
            }
        }

        private void LoadPreset_Click(object sender, RoutedEventArgs e)
        {
            if (LstPresets.SelectedItem is not string presetName)
            {
                ToastServiceStatic.Instance.Warning("Please select a preset to load.");
                return;
            }

            var result = MessageBox.Show($"Overwrite current config files with preset '{presetName}'?", "Load Preset", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                PresetManager.LoadPreset(_serverDir, presetName);

                // Reload all config panels to reflect the loaded preset
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.ReloadFromPath(_serverDir);
                }

                ToastServiceStatic.Instance.Success($"Preset '{presetName}' loaded.");
            }
            catch (Exception ex)
            {
                ToastServiceStatic.Instance.Error($"Failed to load preset: {ex.Message}");
            }
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (LstPresets.SelectedItem is not string presetName)
            {
                ToastServiceStatic.Instance.Warning("Please select a preset to delete.");
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
                ToastServiceStatic.Instance.Error($"Failed to delete preset: {ex.Message}");
            }
        }
    }
}
