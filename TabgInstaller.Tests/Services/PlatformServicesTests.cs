using FluentAssertions;
using TabgInstaller.UI.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class PlatformServicesTests
    {
        [Fact]
        public void ExternalProcessLauncher_RejectsEmptyPath()
        {
            var launcher = new ExternalProcessLauncher();

            var opened = launcher.TryOpenPath("", out var error);

            opened.Should().BeFalse();
            error.Should().Be("Path is empty.");
        }

        [Fact]
        public void LogNotificationService_EmitsSeverityPrefixes()
        {
            var service = new LogNotificationService();
            var messages = new System.Collections.Generic.List<string>();
            service.Message += messages.Add;

            service.Info("Ready");
            service.Warning("Missing path");
            service.Error("Install failed");

            messages.Should().Equal("Ready", "Warning: Missing path", "Error: Install failed");
        }
    }
}
