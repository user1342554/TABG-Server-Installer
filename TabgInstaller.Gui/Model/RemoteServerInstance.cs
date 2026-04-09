using System;
using CommunityToolkit.Mvvm.ComponentModel;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Model
{
    public partial class RemoteServerInstance : ObservableObject, IServerInstanceContext, IDisposable
    {
        private readonly RemoteSshService _sshService;
        private readonly RemoteProcessService _remoteProcessService;
        private readonly HealthMonitorService _healthMonitor;

        public ServerInstanceData Data { get; }
        public Guid Id => Data.Id;
        public string ServerPath => Data.RemoteConfig?.RemoteServerPath ?? "";
        public IServerProcessService ProcessService => _remoteProcessService;
        public IHealthMonitorService HealthMonitor => _healthMonitor;

        [ObservableProperty] private string _displayName;
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private bool _isConnected;
        [ObservableProperty] private ServerHealthStatus _healthStatus = ServerHealthStatus.Stopped;

        public RemoteServerInstance(
            ServerInstanceData data,
            string? password = null,
            string? passphrase = null)
        {
            Data = data;
            _displayName = data.DisplayName;

            if (data.RemoteConfig == null)
                throw new ArgumentException("RemoteConfig is required for RemoteServerInstance");

            _sshService = new RemoteSshService(data.RemoteConfig, password, passphrase);
            _remoteProcessService = new RemoteProcessService(_sshService, data.RemoteConfig);
            _healthMonitor = new HealthMonitorService(_remoteProcessService);

            _remoteProcessService.OutputReceived += OnOutputReceived;
            _remoteProcessService.ProcessExited += OnProcessExited;
            _healthMonitor.StatusChanged += () => HealthStatus = _healthMonitor.Status;
        }

        private void OnOutputReceived(string line)
        {
            var serverEvent = ServerEventParser.TryParse(line);
            if (serverEvent != null)
                _healthMonitor.HandleEvent(serverEvent);
        }

        private void OnProcessExited(int exitCode)
        {
            IsRunning = false;
            _healthMonitor.MarkStopped();
        }

        public async System.Threading.Tasks.Task ConnectAsync()
        {
            await _sshService.ConnectAsync();
            IsConnected = _sshService.IsConnected;
        }

        public void Dispose()
        {
            _remoteProcessService.Dispose();
            _sshService.Dispose();
        }
    }
}
