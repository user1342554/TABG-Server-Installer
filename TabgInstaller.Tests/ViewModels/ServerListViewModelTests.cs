using System;
using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class ServerListViewModelTests
    {
        private readonly Mock<IServerInstanceManager> _manager = new();
        private readonly Mock<IToastService> _toast = new();
        private readonly Mock<ICredentialStorageService> _credentials = new();

        public ServerListViewModelTests()
        {
            _manager.SetupGet(m => m.InstanceDataList)
                .Returns(new ObservableCollection<ServerInstanceData>());
        }

        private ServerListViewModel CreateSut() => new(_manager.Object, _toast.Object, _credentials.Object);

        [Fact]
        public void Instances_BoundToManagerList()
        {
            var list = new ObservableCollection<ServerInstanceData>
            {
                new() { DisplayName = "S1" },
                new() { DisplayName = "S2" }
            };
            _manager.SetupGet(m => m.InstanceDataList).Returns(list);

            var sut = CreateSut();
            sut.Instances.Should().HaveCount(2);
        }

        [Fact]
        public void SelectedInstance_ChangeSetsActiveInManager()
        {
            var data = new ServerInstanceData { DisplayName = "S1" };
            _manager.SetupGet(m => m.InstanceDataList)
                .Returns(new ObservableCollection<ServerInstanceData> { data });

            var sut = CreateSut();
            sut.SelectedInstance = data;
            _manager.Verify(m => m.SetActiveInstance(data.Id), Times.Once);
        }

        [Fact]
        public void RemoveServerCommand_CannotRemoveLastInstance()
        {
            var data = new ServerInstanceData { DisplayName = "S1" };
            _manager.SetupGet(m => m.InstanceDataList)
                .Returns(new ObservableCollection<ServerInstanceData> { data });
            _manager.Setup(m => m.RemoveInstance(It.IsAny<Guid>()))
                .Throws(new InvalidOperationException("Cannot remove last"));

            var sut = CreateSut();
            sut.SelectedInstance = data;
            sut.RemoveServerCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }
    }
}
