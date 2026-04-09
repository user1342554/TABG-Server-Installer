using System;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Gui.Services
{
    /// <summary>
    /// What a ServerInstance exposes to the rest of the app.
    /// Both local and remote instances implement this.
    /// </summary>
    public interface IServerInstanceContext
    {
        string ServerPath { get; }
        IServerProcessService ProcessService { get; }
        IHealthMonitorService HealthMonitor { get; }
    }

    /// <summary>
    /// Proxies the active instance's services so existing ViewModels
    /// can inject this instead of IServerPathProvider.
    /// Drop-in replacement: same ServerPath property, same PathChanged event.
    /// </summary>
    public interface IActiveInstanceService
    {
        string ServerPath { get; }
        IServerProcessService ProcessService { get; }
        IHealthMonitorService HealthMonitor { get; }
        event Action? PathChanged;
    }
}
