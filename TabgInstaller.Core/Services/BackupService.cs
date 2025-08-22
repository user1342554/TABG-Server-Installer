using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public class BackupInfo
    {
        public string Name { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string BackupPath { get; set; } = "";
        public long SizeInBytes { get; set; }
    }

    public class BackupService
    {
        private readonly IProgress<string> _log;

        public BackupService(IProgress<string> log)
        {
            _log = log;
        }

        public async Task<bool> CreateBackupAsync(string serverDir)
        {
            try
            {
                var backupsDir = Path.Combine(serverDir, "backup");
                if (!Directory.Exists(backupsDir))
                {
                    Directory.CreateDirectory(backupsDir);
                    _log.Report("Created backup directory");
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                var backupName = $"backup {Directory.GetDirectories(backupsDir).Length + 1} {timestamp}";
                var backupPath = Path.Combine(backupsDir, backupName);
                
                Directory.CreateDirectory(backupPath);
                _log.Report($"Creating backup: {backupName}");

                // Copy all server files except the backup folder itself
                int copiedFiles = 0;
                foreach (var item in Directory.GetFileSystemEntries(serverDir))
                {
                    var itemName = Path.GetFileName(item);
                    if (itemName.Equals("backup", StringComparison.OrdinalIgnoreCase))
                        continue; // Skip backup folder to avoid recursion

                    var destPath = Path.Combine(backupPath, itemName);

                    if (File.Exists(item))
                    {
                        File.Copy(item, destPath, true);
                        copiedFiles++;
                    }
                    else if (Directory.Exists(item))
                    {
                        await CopyDirectoryAsync(item, destPath);
                        copiedFiles++;
                    }
                }

                _log.Report($"Backup created with {copiedFiles} items");
                return true;
            }
            catch (Exception ex)
            {
                _log.Report($"Backup failed: {ex.Message}");
                return false;
            }
        }

        public List<BackupInfo> GetAvailableBackups(string serverDir)
        {
            var backups = new List<BackupInfo>();
            var backupsDir = Path.Combine(serverDir, "backup");

            if (!Directory.Exists(backupsDir))
                return backups;

            try
            {
                foreach (var backupDir in Directory.GetDirectories(backupsDir))
                {
                    var dirInfo = new DirectoryInfo(backupDir);
                    var backupName = dirInfo.Name;
                    
                    // Try to parse date from backup name (format: "backup 1 2024-01-15 14-30-25")
                    DateTime createdDate = dirInfo.CreationTime;
                    if (backupName.StartsWith("backup "))
                    {
                        var parts = backupName.Split(' ');
                        if (parts.Length >= 4)
                        {
                            var dateStr = $"{parts[2]} {parts[3].Replace('-', ':')}";
                            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                            {
                                createdDate = parsedDate;
                            }
                        }
                    }

                    var sizeInBytes = GetDirectorySize(backupDir);

                    backups.Add(new BackupInfo
                    {
                        Name = backupName,
                        CreatedDate = createdDate,
                        BackupPath = backupDir,
                        SizeInBytes = sizeInBytes
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Report($"⚠️ Error reading backups: {ex.Message}");
            }

            return backups.OrderByDescending(b => b.CreatedDate).ToList();
        }

        public async Task<bool> RestoreBackupAsync(string serverDir, BackupInfo backup)
        {
            try
            {
                _log.Report($"🔄 Restoring backup: {backup.Name}");
                
                // Stop any running server first (optional warning)
                
                foreach (var item in Directory.GetFileSystemEntries(backup.BackupPath))
                {
                    var itemName = Path.GetFileName(item);
                    var destPath = Path.Combine(serverDir, itemName);

                    if (File.Exists(item))
                    {
                        // Backup existing file if it exists
                        if (File.Exists(destPath))
                        {
                            File.Copy(destPath, destPath + ".pre-restore", true);
                        }
                        File.Copy(item, destPath, true);
                    }
                    else if (Directory.Exists(item))
                    {
                        if (Directory.Exists(destPath))
                        {
                            Directory.Delete(destPath, true);
                        }
                        await CopyDirectoryAsync(item, destPath);
                    }
                }

                _log.Report($"✅ Backup restored successfully: {backup.Name}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Report($"❌ Restore failed: {ex.Message}");
                return false;
            }
        }

        public bool DeleteBackup(BackupInfo backup)
        {
            try
            {
                if (Directory.Exists(backup.BackupPath))
                {
                    Directory.Delete(backup.BackupPath, true);
                    _log.Report($"🗑️ Deleted backup: {backup.Name}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _log.Report($"❌ Delete failed: {ex.Message}");
                return false;
            }
        }

        private async Task CopyDirectoryAsync(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                await CopyDirectoryAsync(dir, destSubDir);
            }
        }

        private long GetDirectorySize(string directory)
        {
            try
            {
                return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }

        public string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
