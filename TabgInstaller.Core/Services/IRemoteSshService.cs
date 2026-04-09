using System;
using System.Threading;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public interface IRemoteSshService : IDisposable
    {
        bool IsConnected { get; }
        Task ConnectAsync(CancellationToken ct = default);
        void Disconnect();
        Task<string> ExecuteCommandAsync(string command, CancellationToken ct = default);
        Task StartTailAsync(string filePath, Action<string> onLine, CancellationToken ct = default);
        Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default);
        Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default);
    }
}
