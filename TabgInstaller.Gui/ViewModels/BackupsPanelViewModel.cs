using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class BackupsPanelViewModel : ObservableObject
    {
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IBackupService _backupService;
        private readonly IToastService _toast;

        [ObservableProperty] private ObservableCollection<BackupInfo> _backups = new();
        [ObservableProperty] private string _statusText = "";
        [ObservableProperty] private bool _isCreatingBackup;

        public BackupsPanelViewModel(
            IServerPathProvider serverPathProvider,
            IBackupService backupService,
            IToastService toast)
        {
            _serverPathProvider = serverPathProvider;
            _backupService = backupService;
            _toast = toast;

            _serverPathProvider.PathChanged += OnServerPathChanged;
        }

        private void OnServerPathChanged()
        {
            RefreshBackups();
        }

        private void RefreshBackupsInternal()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrWhiteSpace(serverDir))
            {
                Backups.Clear();
                StatusText = "";
                return;
            }

            var list = _backupService.GetAvailableBackups(serverDir);
            Backups = new ObservableCollection<BackupInfo>(list);
            StatusText = list.Count == 0 ? "No backups found." : $"{list.Count} backup(s)";
        }

        [RelayCommand]
        private void RefreshBackups()
        {
            RefreshBackupsInternal();
        }

        [RelayCommand]
        private async Task CreateBackup()
        {
            var serverDir = _serverPathProvider.ServerPath;
            if (string.IsNullOrWhiteSpace(serverDir))
            {
                _toast.Error("No server directory configured.");
                return;
            }

            IsCreatingBackup = true;
            StatusText = "Creating backup...";

            try
            {
                bool success = await _backupService.CreateBackupAsync(serverDir);
                if (success)
                {
                    _toast.Success("Backup created successfully!");
                    RefreshBackupsInternal();
                }
                else
                {
                    _toast.Error("Failed to create backup. Check the log for details.");
                    StatusText = "Backup failed.";
                }
            }
            finally
            {
                IsCreatingBackup = false;
            }
        }

        [RelayCommand]
        private async Task RestoreBackup(BackupInfo? backup)
        {
            if (backup == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to restore backup '{backup.Name}'?\n\n" +
                "This will replace your current server files with the backup files.\n" +
                "Current files will be backed up with '.pre-restore' extension.",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            StatusText = $"Restoring '{backup.Name}'...";

            try
            {
                bool success = await _backupService.RestoreBackupAsync(_serverPathProvider.ServerPath, backup);
                if (success)
                {
                    _toast.Success("Backup restored successfully!");
                    StatusText = $"Restored '{backup.Name}'.";
                }
                else
                {
                    _toast.Error("Failed to restore backup. Check the log for details.");
                    StatusText = "Restore failed.";
                }
            }
            catch (Exception ex)
            {
                _toast.Error($"Restore error: {ex.Message}");
                StatusText = "Restore failed.";
            }
        }

        [RelayCommand]
        private void DeleteBackup(BackupInfo? backup)
        {
            if (backup == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete backup '{backup.Name}'?\n\nThis action cannot be undone!",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            bool success = _backupService.DeleteBackup(backup);
            if (success)
            {
                _toast.Success("Backup deleted successfully!");
                Backups.Remove(backup);
                StatusText = Backups.Count == 0 ? "No backups found." : $"{Backups.Count} backup(s)";
            }
            else
            {
                _toast.Error("Failed to delete backup.");
            }
        }
    }
}
