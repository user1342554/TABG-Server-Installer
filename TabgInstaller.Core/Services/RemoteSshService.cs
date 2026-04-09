using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public class RemoteSshService : IRemoteSshService
    {
        private readonly RemoteConnectionConfig _config;
        private readonly string? _password;
        private readonly string? _passphrase;
        private SshClient? _sshClient;
        private SftpClient? _sftpClient;

        public bool IsConnected => _sshClient?.IsConnected ?? false;

        public RemoteSshService(RemoteConnectionConfig config, string? password = null, string? passphrase = null)
        {
            _config = config;
            _password = password;
            _passphrase = passphrase;
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var connectionInfo = CreateConnectionInfo();
                _sshClient = new SshClient(connectionInfo);
                _sshClient.Connect();

                _sftpClient = new SftpClient(connectionInfo);
                _sftpClient.Connect();
            }, ct);
        }

        public void Disconnect()
        {
            _sshClient?.Disconnect();
            _sshClient?.Dispose();
            _sshClient = null;

            _sftpClient?.Disconnect();
            _sftpClient?.Dispose();
            _sftpClient = null;
        }

        public Task<string> ExecuteCommandAsync(string command, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                EnsureConnected();
                using var cmd = _sshClient!.CreateCommand(command);
                cmd.CommandTimeout = TimeSpan.FromSeconds(30);
                var result = cmd.Execute();
                if (cmd.ExitStatus != 0 && !string.IsNullOrEmpty(cmd.Error))
                    throw new InvalidOperationException($"SSH command failed (exit {cmd.ExitStatus}): {cmd.Error}");
                return result;
            }, ct);
        }

        public Task StartTailAsync(string filePath, Action<string> onLine, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                EnsureConnected();
                using var stream = _sshClient!.CreateShellStream("tail", 0, 0, 0, 0, 4096);
                stream.WriteLine($"tail -f {filePath}");

                while (!ct.IsCancellationRequested)
                {
                    var line = stream.ReadLine(TimeSpan.FromSeconds(1));
                    if (line != null)
                        onLine(line);
                }
            }, ct);
        }

        public Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                EnsureSftpConnected();
                using var stream = File.OpenRead(localPath);
                _sftpClient!.UploadFile(stream, remotePath, true);
            }, ct);
        }

        public Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                EnsureSftpConnected();
                using var stream = File.Create(localPath);
                _sftpClient!.DownloadFile(remotePath, stream);
            }, ct);
        }

        private ConnectionInfo CreateConnectionInfo()
        {
            AuthenticationMethod auth = _config.AuthMethod switch
            {
                SshAuthMethod.Password => new PasswordAuthenticationMethod(_config.Username, _password ?? ""),
                SshAuthMethod.PrivateKey => CreatePrivateKeyAuth(),
                _ => throw new InvalidOperationException($"Unknown auth method: {_config.AuthMethod}")
            };

            return new ConnectionInfo(_config.Host, _config.Port, _config.Username, auth)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        private PrivateKeyAuthenticationMethod CreatePrivateKeyAuth()
        {
            PrivateKeyFile keyFile = string.IsNullOrEmpty(_passphrase)
                ? new PrivateKeyFile(_config.PrivateKeyPath)
                : new PrivateKeyFile(_config.PrivateKeyPath, _passphrase);

            return new PrivateKeyAuthenticationMethod(_config.Username, keyFile);
        }

        private void EnsureConnected()
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("SSH client is not connected.");
        }

        private void EnsureSftpConnected()
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
