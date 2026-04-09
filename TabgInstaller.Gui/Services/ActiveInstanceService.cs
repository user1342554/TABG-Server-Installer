using System;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Gui.Services
{
    public class ActiveInstanceService : IActiveInstanceService
    {
        private readonly IServerInstanceManager _manager;

        public string ServerPath => _manager.ActiveInstance?.ServerPath ?? "";

        public IServerProcessService ProcessService => _manager.ActiveInstance?.ProcessService
            ?? throw new InvalidOperationException("No active server instance");

        public IHealthMonitorService HealthMonitor => _manager.ActiveInstance?.HealthMonitor
            ?? throw new InvalidOperationException("No active server instance");

        public event Action? PathChanged;

        public ActiveInstanceService(IServerInstanceManager manager)
        {
            _manager = manager;
            _manager.ActiveInstanceChanged += () => PathChanged?.Invoke();
        }
    }
}
