using FluentAssertions;
using TabgInstaller.Core;
using Xunit;

namespace TabgInstaller.Tests
{
    public class ServerPathProviderTests
    {
        [Fact]
        public void ServerPath_InitiallyEmpty()
        {
            var sut = new ServerPathProvider();
            sut.ServerPath.Should().Be("");
        }

        [Fact]
        public void SetPath_UpdatesServerPath()
        {
            var sut = new ServerPathProvider();
            sut.SetPath(@"C:\GameServer");
            sut.ServerPath.Should().Be(@"C:\GameServer");
        }

        [Fact]
        public void SetPath_FiresPathChangedEvent()
        {
            var sut = new ServerPathProvider();
            bool fired = false;
            sut.PathChanged += () => fired = true;

            sut.SetPath(@"C:\GameServer");

            fired.Should().BeTrue();
        }

        [Fact]
        public void SetPath_CalledTwice_FiresEventTwice()
        {
            var sut = new ServerPathProvider();
            int count = 0;
            sut.PathChanged += () => count++;

            sut.SetPath(@"C:\Server1");
            sut.SetPath(@"C:\Server2");

            count.Should().Be(2);
            sut.ServerPath.Should().Be(@"C:\Server2");
        }
    }
}
