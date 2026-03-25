using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Tabs
{
    public partial class BackupsPanel : UserControl
    {
        private string? _serverDir;
        private BackupService? _backupService;

        public BackupsPanel()
        {
            InitializeComponent();
        }

        public void Initialize(string serverDir)
        {
            _serverDir = serverDir;
            _backupService = new BackupService(new Progress<string>(msg => { })); // Silent logger for UI
            LoadBackups();
        }

        private void LoadBackups()
        {
            if (string.IsNullOrWhiteSpace(_serverDir) || _backupService == null)
                return;

            BackupsList.Children.Clear();

            var backups = _backupService.GetAvailableBackups(_serverDir);

            if (backups.Count == 0)
            {
                NoBackupsMessage.Visibility = Visibility.Visible;
                return;
            }

            NoBackupsMessage.Visibility = Visibility.Collapsed;

            foreach (var backup in backups)
            {
                var backupCard = CreateBackupCard(backup);
                BackupsList.Children.Add(backupCard);
            }
        }

        private StackPanel CreateBackupCard(BackupInfo backup)
        {
            var card = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            // Left side - backup info
            var infoPanel = new StackPanel { Orientation = Orientation.Vertical, Width = 300 };
            
            var nameText = new TextBlock
            {
                Text = backup.Name,
                FontWeight = FontWeights.Bold
            };

            var detailsText = new TextBlock
            {
                Text = $"{backup.CreatedDate:yyyy-MM-dd HH:mm} ({_backupService?.FormatFileSize(backup.SizeInBytes) ?? "Unknown"})"
            };

            infoPanel.Children.Add(nameText);
            infoPanel.Children.Add(detailsText);

            // Right side - action buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };

            var restoreButton = new Button
            {
                Content = "Restore",
                Width = 60,
                Margin = new Thickness(0, 0, 4, 0),
                Tag = backup
            };
            restoreButton.Click += RestoreBackup_Click;

            var deleteButton = new Button
            {
                Content = "Delete",
                Width = 60,
                Tag = backup
            };
            deleteButton.Click += DeleteBackup_Click;

            buttonPanel.Children.Add(restoreButton);
            buttonPanel.Children.Add(deleteButton);

            card.Children.Add(infoPanel);
            card.Children.Add(buttonPanel);

            return card;
        }

        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_serverDir) || _backupService == null)
            {
                ToastService.Instance.Error("No server directory configured.");
                return;
            }

            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "⏳ Creating...";
            }

            try
            {
                var progress = new Progress<string>(msg =>
                {
                    // Could show progress in a status bar or log
                });

                var backupService = new BackupService(progress);
                bool success = await backupService.CreateBackupAsync(_serverDir);

                if (success)
                {
                    ToastService.Instance.Success("Backup created successfully!");
                    LoadBackups(); // Refresh the list
                }
                else
                {
                    ToastService.Instance.Error("Failed to create backup. Check the log for details.");
                }
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "📦 Create Backup";
                }
            }
        }

        private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_backupService == null) return;

            var button = sender as Button;
            var backup = button?.Tag as BackupInfo;
            if (backup == null) return;

            var result = MessageBox.Show(
                $"⚠️ Are you sure you want to restore backup '{backup.Name}'?\n\n" +
                "This will replace your current server files with the backup files.\n" +
                "Current files will be backed up with '.pre-restore' extension.",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes) return;

            button.IsEnabled = false;
            button.Content = "⏳ Restoring...";

            try
            {
                var progress = new Progress<string>(msg =>
                {
                    // Could show progress
                });

                var backupService = new BackupService(progress);
                bool success = await backupService.RestoreBackupAsync(_serverDir!, backup);

                if (success)
                {
                    ToastService.Instance.Success("Backup restored successfully!");
                }
                else
                {
                    ToastService.Instance.Error("Failed to restore backup. Check the log for details.");
                }
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = "🔄 Restore";
            }
        }

        private void DeleteBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_backupService == null) return;

            var button = sender as Button;
            var backup = button?.Tag as BackupInfo;
            if (backup == null) return;

            var result = MessageBox.Show(
                $"⚠️ Are you sure you want to delete backup '{backup.Name}'?\n\n" +
                "This action cannot be undone!",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result != MessageBoxResult.Yes) return;

            bool success = _backupService.DeleteBackup(backup);

            if (success)
            {
                ToastService.Instance.Success("Backup deleted successfully!");
                LoadBackups(); // Refresh the list
            }
            else
            {
                ToastService.Instance.Error("Failed to delete backup.");
            }
        }

        private void RefreshBackups_Click(object sender, RoutedEventArgs e)
        {
            LoadBackups();
        }
    }
}
