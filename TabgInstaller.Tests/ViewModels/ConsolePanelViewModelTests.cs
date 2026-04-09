using FluentAssertions;
using Moq;
using System;
using System.Collections.ObjectModel;
using System.IO;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class ConsolePanelViewModelTests
    {
        private readonly Mock<IServerProcessService> _procSvc = new();
        private readonly Mock<IActiveInstanceService> _activeInstance = new();
        private readonly Mock<IToastService> _toast = new();

        public ConsolePanelViewModelTests()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            _procSvc.SetupGet(p => p.LogEntries).Returns(new ObservableCollection<LogEntry>());
            _activeInstance.SetupGet(a => a.ServerPath).Returns(@"C:\Server");
            _activeInstance.SetupGet(a => a.ProcessService).Returns(_procSvc.Object);
        }

        private ConsolePanelViewModel CreateSut() =>
            new(_activeInstance.Object, _toast.Object);

        // ── Initial state ────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_ShowInfo_IsTrue()
        {
            var sut = CreateSut();
            sut.ShowInfo.Should().BeTrue();
        }

        [Fact]
        public void InitialState_ShowWarning_IsTrue()
        {
            var sut = CreateSut();
            sut.ShowWarning.Should().BeTrue();
        }

        [Fact]
        public void InitialState_ShowError_IsTrue()
        {
            var sut = CreateSut();
            sut.ShowError.Should().BeTrue();
        }

        [Fact]
        public void InitialState_SearchText_IsEmpty()
        {
            var sut = CreateSut();
            sut.SearchText.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_CommandText_IsEmpty()
        {
            var sut = CreateSut();
            sut.CommandText.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_IsServerRunning_ReflectsService()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning.Should().BeTrue();
        }

        [Fact]
        public void IsServerNotRunning_IsInverseOfIsServerRunning()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            var sut = CreateSut();
            sut.IsServerNotRunning.Should().BeTrue();
        }

        [Fact]
        public void IsServerNotRunning_UpdatesWhenIsServerRunningChanges()
        {
            var sut = CreateSut();
            sut.IsServerRunning = true;
            sut.IsServerNotRunning.Should().BeFalse();
        }

        // ── Start command ────────────────────────────────────────────────────────

        [Fact]
        public void StartCommand_WhenNotRunning_CallsServiceStart()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            _procSvc.Setup(p => p.Start(It.IsAny<string>())).Returns(true);
            var sut = CreateSut();

            sut.StartCommand.Execute(null);

            _procSvc.Verify(p => p.Start(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void StartCommand_WhenNotRunning_SetsIsServerRunningTrue()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            _procSvc.Setup(p => p.Start(It.IsAny<string>())).Returns(true);
            var sut = CreateSut();

            sut.StartCommand.Execute(null);

            sut.IsServerRunning.Should().BeTrue();
        }

        [Fact]
        public void StartCommand_WhenAlreadyRunning_DoesNotCallServiceStart()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning = true;

            sut.StartCommand.Execute(null);

            _procSvc.Verify(p => p.Start(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void StartCommand_WhenServiceThrows_ShowsErrorToast()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            _procSvc.Setup(p => p.Start(It.IsAny<string>())).Throws(new FileNotFoundException("TABG.exe not found"));
            var sut = CreateSut();

            sut.StartCommand.Execute(null);

            _toast.Verify(t => t.Error(It.IsAny<string>()), Times.Once);
        }

        // ── Stop command ─────────────────────────────────────────────────────────

        [Fact]
        public void StopCommand_WhenRunning_CallsServiceStop()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning = true;

            sut.StopCommand.Execute(null);

            _procSvc.Verify(p => p.Stop(), Times.Once);
        }

        [Fact]
        public void StopCommand_WhenRunning_SetsIsServerRunningFalse()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning = true;

            sut.StopCommand.Execute(null);

            sut.IsServerRunning.Should().BeFalse();
        }

        [Fact]
        public void StopCommand_WhenNotRunning_DoesNotCallServiceStop()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            var sut = CreateSut();

            sut.StopCommand.Execute(null);

            _procSvc.Verify(p => p.Stop(), Times.Never);
        }

        // ── ClearConsole command ─────────────────────────────────────────────────

        [Fact]
        public void ClearConsoleCommand_CallsClearEntries()
        {
            var sut = CreateSut();
            sut.ClearConsoleCommand.Execute(null);
            _procSvc.Verify(p => p.ClearEntries(), Times.Once);
        }

        // ── SendCommand command ──────────────────────────────────────────────────

        [Fact]
        public void SendCommandCommand_WhenRunningWithText_AddsEchoEntry()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning = true;
            sut.CommandText = "hello world";

            sut.SendCommandCommand.Execute(null);

            _procSvc.Verify(p => p.AddEntry(It.Is<LogEntry>(e => e.RawText.Contains("hello world"))), Times.Once);
        }

        [Fact]
        public void SendCommandCommand_WhenRunningWithText_ClearsCommandText()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning = true;
            sut.CommandText = "test";

            sut.SendCommandCommand.Execute(null);

            sut.CommandText.Should().BeEmpty();
        }

        [Fact]
        public void SendCommandCommand_WhenNotRunning_DoesNotAddEntry()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            var sut = CreateSut();
            sut.CommandText = "hello";

            sut.SendCommandCommand.Execute(null);

            _procSvc.Verify(p => p.AddEntry(It.IsAny<LogEntry>()), Times.Never);
        }

        [Fact]
        public void SendCommandCommand_WhenTextIsWhitespace_DoesNotAddEntry()
        {
            _procSvc.SetupGet(p => p.IsRunning).Returns(true);
            var sut = CreateSut();
            sut.IsServerRunning = true;
            sut.CommandText = "   ";

            sut.SendCommandCommand.Execute(null);

            _procSvc.Verify(p => p.AddEntry(It.IsAny<LogEntry>()), Times.Never);
        }

        // ── FilterLogEntry ───────────────────────────────────────────────────────

        [Fact]
        public void FilterLogEntry_InfoEntry_AcceptedWhenShowInfoTrue()
        {
            var sut = CreateSut();
            var entry = new LogEntry { Severity = LogSeverity.Info, RawText = "some info", Message = "some info" };

            sut.FilterLogEntry(entry).Should().BeTrue();
        }

        [Fact]
        public void FilterLogEntry_InfoEntry_RejectedWhenShowInfoFalse()
        {
            var sut = CreateSut();
            sut.ShowInfo = false;
            var entry = new LogEntry { Severity = LogSeverity.Info, RawText = "some info", Message = "some info" };

            sut.FilterLogEntry(entry).Should().BeFalse();
        }

        [Fact]
        public void FilterLogEntry_WarningEntry_AcceptedWhenShowWarningTrue()
        {
            var sut = CreateSut();
            var entry = new LogEntry { Severity = LogSeverity.Warning, RawText = "some warning", Message = "some warning" };

            sut.FilterLogEntry(entry).Should().BeTrue();
        }

        [Fact]
        public void FilterLogEntry_WarningEntry_RejectedWhenShowWarningFalse()
        {
            var sut = CreateSut();
            sut.ShowWarning = false;
            var entry = new LogEntry { Severity = LogSeverity.Warning, RawText = "a warning", Message = "a warning" };

            sut.FilterLogEntry(entry).Should().BeFalse();
        }

        [Fact]
        public void FilterLogEntry_ErrorEntry_AcceptedWhenShowErrorTrue()
        {
            var sut = CreateSut();
            var entry = new LogEntry { Severity = LogSeverity.Error, RawText = "some error", Message = "some error" };

            sut.FilterLogEntry(entry).Should().BeTrue();
        }

        [Fact]
        public void FilterLogEntry_ErrorEntry_RejectedWhenShowErrorFalse()
        {
            var sut = CreateSut();
            sut.ShowError = false;
            var entry = new LogEntry { Severity = LogSeverity.Error, RawText = "an error", Message = "an error" };

            sut.FilterLogEntry(entry).Should().BeFalse();
        }

        [Fact]
        public void FilterLogEntry_WithSearchText_AcceptsMatchingEntry()
        {
            var sut = CreateSut();
            sut.SearchText = "hello";
            var entry = new LogEntry { Severity = LogSeverity.Info, RawText = "say hello world", Message = "say hello world" };

            sut.FilterLogEntry(entry).Should().BeTrue();
        }

        [Fact]
        public void FilterLogEntry_WithSearchText_RejectsNonMatchingEntry()
        {
            var sut = CreateSut();
            sut.SearchText = "xyz";
            var entry = new LogEntry { Severity = LogSeverity.Info, RawText = "say hello world", Message = "say hello world" };

            sut.FilterLogEntry(entry).Should().BeFalse();
        }

        [Fact]
        public void FilterLogEntry_SearchText_IsCaseInsensitive()
        {
            var sut = CreateSut();
            sut.SearchText = "HELLO";
            var entry = new LogEntry { Severity = LogSeverity.Info, RawText = "say hello world", Message = "say hello world" };

            sut.FilterLogEntry(entry).Should().BeTrue();
        }

        [Fact]
        public void FilterLogEntry_SeverityRejected_SearchNotEvaluated()
        {
            // Even if text matches, rejected severity means the entry is rejected
            var sut = CreateSut();
            sut.ShowInfo = false;
            sut.SearchText = "hello";
            var entry = new LogEntry { Severity = LogSeverity.Info, RawText = "hello world", Message = "hello world" };

            sut.FilterLogEntry(entry).Should().BeFalse();
        }

        // ── LogEntries proxy ─────────────────────────────────────────────────────

        [Fact]
        public void LogEntries_ReturnsSameCollectionAsService()
        {
            var collection = new ObservableCollection<LogEntry>();
            _procSvc.SetupGet(p => p.LogEntries).Returns(collection);
            var sut = CreateSut();

            sut.LogEntries.Should().BeSameAs(collection);
        }

        // ── ExportLog ────────────────────────────────────────────────────────────

        [Fact]
        public void ExportLog_WritesFilteredEntriesToFile()
        {
            var sut = CreateSut();
            var path = Path.GetTempFileName();
            try
            {
                var entries = new[]
                {
                    new LogEntry
                    {
                        Timestamp = new DateTime(2024, 1, 15, 10, 0, 0),
                        Severity = LogSeverity.Info,
                        RawText = "hello",
                        Message = "hello"
                    }
                };

                sut.ExportLog(path, entries);

                var content = File.ReadAllText(path);
                content.Should().Contain("[INFO]");
                content.Should().Contain("hello");
                content.Should().Contain("2024-01-15");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ExportLog_WhenSuccessful_ShowsSuccessToast()
        {
            var sut = CreateSut();
            var path = Path.GetTempFileName();
            try
            {
                sut.ExportLog(path, Array.Empty<LogEntry>());
                _toast.Verify(t => t.Success(It.IsAny<string>()), Times.Once);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ExportLog_WhenPathIsInvalid_ShowsErrorToast()
        {
            var sut = CreateSut();

            sut.ExportLog(@"Z:\nonexistent\path\file.txt", Array.Empty<LogEntry>());

            _toast.Verify(t => t.Error(It.IsAny<string>()), Times.Once);
        }
    }
}
