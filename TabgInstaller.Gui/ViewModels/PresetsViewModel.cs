using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class PresetsViewModel : ObservableObject
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IToastService _toast;

        [ObservableProperty] private ObservableCollection<string> _presetNames = new();
        [ObservableProperty] private string? _selectedPreset;

        public PresetsViewModel(
            IServerPathProvider serverPathProvider,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _toast = toast;

            _serverPathProvider.PathChanged += OnServerPathChanged;

            if (!string.IsNullOrEmpty(_serverPathProvider.ServerPath))
                RefreshPresets();
        }

        private void OnServerPathChanged()
        {
            RefreshPresets();
        }

        private void RefreshPresets()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir)) return;

            var names = PresetManager.ListPresets(serverDir).OrderBy(p => p).ToList();
            PresetNames = new ObservableCollection<string>(names);
        }

        [RelayCommand]
        private void ApplyTemplate(BuiltInPresets.BuiltInPreset? preset)
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir))
            {
                _toast.Warning("No server directory set. Please install/select a server first.");
                return;
            }
            if (preset == null)
            {
                _toast.Warning("Please select a template to apply.");
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
                BuiltInPresets.Deploy(preset, serverDir);
                _toast.Success($"Template '{preset.Name}' applied successfully. Reload the Config tab to see new settings.");
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to apply template: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SavePreset(string? presetName)
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir)) return;
            if (string.IsNullOrWhiteSpace(presetName)) return;

            try
            {
                PresetManager.SavePreset(serverDir, presetName, PresetManager.DefaultConfigRelativePaths);
                RefreshPresets();
                _toast.Success($"Preset '{presetName}' saved.");
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to save preset: {ex.Message}");
            }
        }

        [RelayCommand]
        private void LoadPreset()
        {
            if (string.IsNullOrEmpty(SelectedPreset))
            {
                _toast.Warning("Please select a preset to load.");
                return;
            }

            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir)) return;

            var result = MessageBox.Show(
                $"Overwrite current config files with preset '{SelectedPreset}'?",
                "Load Preset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                PresetManager.LoadPreset(serverDir, SelectedPreset);
                _toast.Success($"Preset '{SelectedPreset}' loaded. Reload the Config tab to see new settings.");
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to load preset: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DeletePreset()
        {
            if (string.IsNullOrEmpty(SelectedPreset))
            {
                _toast.Warning("Please select a preset to delete.");
                return;
            }

            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrEmpty(serverDir)) return;

            var result = MessageBox.Show(
                $"Delete preset '{SelectedPreset}'? This cannot be undone.",
                "Delete Preset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                PresetManager.DeletePreset(serverDir, SelectedPreset);
                RefreshPresets();
            }
            catch (Exception ex)
            {
                _toast.Error($"Failed to delete preset: {ex.Message}");
            }
        }
    }
}
