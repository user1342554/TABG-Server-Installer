using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public interface IBackupService
    {
        Task<bool> CreateBackupAsync(string serverDir);
        List<BackupInfo> GetAvailableBackups(string serverDir);
        Task<bool> RestoreBackupAsync(string serverDir, BackupInfo backup);
        bool DeleteBackup(BackupInfo backup);
        string FormatFileSize(long bytes);
    }
}
