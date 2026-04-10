using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Resources;
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
            StatusText = list.Count == 0 ? Strings.NoBackupsFound : string.Format(Messages.BackupCount, list.Count);
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
                _toast.Error(Messages.NoServerDirectory);
                return;
            }

            IsCreatingBackup = true;
            StatusText = Messages.CreatingBackup;

            try
            {
                bool success = await _backupService.CreateBackupAsync(serverDir);
                if (success)
                {
                    _toast.Success(Messages.BackupCreatedSuccess);
                    RefreshBackupsInternal();
                }
                else
                {
                    _toast.Error(Messages.FailedToCreateBackup);
                    StatusText = Messages.BackupFailed;
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
                string.Format(Messages.ConfirmRestore, backup.Name),
                Messages.ConfirmRestoreTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            StatusText = string.Format(Messages.RestoringBackup, backup.Name);

            try
            {
                bool success = await _backupService.RestoreBackupAsync(_serverPathProvider.ServerPath, backup);
                if (success)
                {
                    _toast.Success(Messages.BackupRestoredSuccess);
                    StatusText = string.Format(Messages.RestoredBackup, backup.Name);
                }
                else
                {
                    _toast.Error(Messages.FailedToRestoreBackup);
                    StatusText = Messages.RestoreFailed;
                }
            }
            catch (Exception ex)
            {
                _toast.Error(string.Format(Messages.RestoreError, ex.Message));
                StatusText = Messages.RestoreFailed;
            }
        }

        [RelayCommand]
        private void DeleteBackup(BackupInfo? backup)
        {
            if (backup == null) return;

            var result = MessageBox.Show(
                string.Format(Messages.ConfirmDelete, backup.Name),
                Messages.ConfirmDeleteTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            bool success = _backupService.DeleteBackup(backup);
            if (success)
            {
                _toast.Success(Messages.BackupDeletedSuccess);
                Backups.Remove(backup);
                StatusText = Backups.Count == 0 ? Strings.NoBackupsFound : string.Format(Messages.BackupCount, Backups.Count);
            }
            else
            {
                _toast.Error(Messages.FailedToDeleteBackup);
            }
        }
    }
}
