using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ActiveInstanceServiceTests
    {
        [Fact]
        public void ServerPath_WhenNoActiveInstance_ReturnsEmpty()
        {
            var manager = new Mock<IServerInstanceManager>();
            manager.SetupGet(m => m.ActiveInstance).Returns((IServerInstanceContext?)null);
            var sut = new ActiveInstanceService(manager.Object);
            sut.ServerPath.Should().Be("");
        }

        [Fact]
        public void ServerPath_ReturnsActiveInstancePath()
        {
            var instance = new Mock<IServerInstanceContext>();
            instance.SetupGet(i => i.ServerPath).Returns(@"C:\Server1");
            instance.SetupGet(i => i.ProcessService).Returns(new Mock<IServerProcessService>().Object);
            instance.SetupGet(i => i.HealthMonitor).Returns(new Mock<IHealthMonitorService>().Object);

            var manager = new Mock<IServerInstanceManager>();
            manager.SetupGet(m => m.ActiveInstance).Returns(instance.Object);

            var sut = new ActiveInstanceService(manager.Object);
            sut.ServerPath.Should().Be(@"C:\Server1");
        }

        [Fact]
        public void ActiveInstanceChanged_FiresPathChanged()
        {
            var manager = new Mock<IServerInstanceManager>();
            manager.SetupGet(m => m.ActiveInstance).Returns((IServerInstanceContext?)null);

            var sut = new ActiveInstanceService(manager.Object);
            bool fired = false;
            sut.PathChanged += () => fired = true;

            manager.Raise(m => m.ActiveInstanceChanged += null);
            fired.Should().BeTrue();
        }

        [Fact]
        public void ProcessService_ProxiesActiveInstance()
        {
            var procSvc = new Mock<IServerProcessService>();
            var instance = new Mock<IServerInstanceContext>();
            instance.SetupGet(i => i.ServerPath).Returns(@"C:\S");
            instance.SetupGet(i => i.ProcessService).Returns(procSvc.Object);
            instance.SetupGet(i => i.HealthMonitor).Returns(new Mock<IHealthMonitorService>().Object);

            var manager = new Mock<IServerInstanceManager>();
            manager.SetupGet(m => m.ActiveInstance).Returns(instance.Object);

            var sut = new ActiveInstanceService(manager.Object);
            sut.ProcessService.Should().BeSameAs(procSvc.Object);
        }

        [Fact]
        public void HealthMonitor_ProxiesActiveInstance()
        {
            var healthMon = new Mock<IHealthMonitorService>();
            var instance = new Mock<IServerInstanceContext>();
            instance.SetupGet(i => i.ServerPath).Returns(@"C:\S");
            instance.SetupGet(i => i.ProcessService).Returns(new Mock<IServerProcessService>().Object);
            instance.SetupGet(i => i.HealthMonitor).Returns(healthMon.Object);

            var manager = new Mock<IServerInstanceManager>();
            manager.SetupGet(m => m.ActiveInstance).Returns(instance.Object);

            var sut = new ActiveInstanceService(manager.Object);
            sut.HealthMonitor.Should().BeSameAs(healthMon.Object);
        }
    }
}
