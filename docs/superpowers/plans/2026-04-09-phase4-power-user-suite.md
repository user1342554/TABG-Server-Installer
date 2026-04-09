# Phase 4: Power User Suite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the TABG Server Installer from a single-server tool into a multi-server management platform with health monitoring, auto-restart, and remote SSH management.

**Architecture:** Replace global `IServerPathProvider` + singleton `IServerProcessService` with a `ServerInstance` model that encapsulates path, process, health, and config per server. An `IServerInstanceManager` holds all instances; `IActiveInstanceService` proxies the selected instance's services so existing ViewModels require minimal changes. Remote servers use `RemoteServerInstance` subclass backed by SSH.NET.

**Tech Stack:** WPF / .NET 8 / CommunityToolkit.Mvvm 8.4.2 / SSH.NET (Renci.SshNet) / DPAPI / xUnit + Moq + FluentAssertions

---

## File Structure

### New Files — Core (`TabgInstaller.Core/`)

| File | Responsibility |
|---|---|
| `Model/ServerInstanceData.cs` | Data models: `ServerInstanceData`, `AutoRestartConfig`, `RemoteConnectionConfig`, `ServerInstanceType` enum, `ServerHealthStatus` enum, `ConnectedPlayer`, `InstancesFileData` |
| `Model/ServerEvent.cs` | Event types produced by log parsing: `ServerEvent`, `ServerEventType` enum (PlayerJoined, PlayerLeft, JoinCodeReceived, ServerReady, ProcessExited) |
| `Services/IHealthMonitorService.cs` | Interface for per-instance health tracking |
| `Services/HealthMonitorService.cs` | Implementation: player tracking, uptime, memory, status transitions, auto-restart logic |
| `Services/ServerEventParser.cs` | Extends log parsing to produce `ServerEvent` objects from TABG-specific patterns |
| `Services/ICredentialStorageService.cs` | Interface for DPAPI credential store |
| `Services/CredentialStorageService.cs` | DPAPI encrypt/decrypt, file-backed storage |
| `Services/IRemoteSshService.cs` | Interface for SSH operations |
| `Services/RemoteSshService.cs` | SSH.NET wrapper: connect, execute, tail, SFTP |
| `Services/RemoteProcessService.cs` | `IServerProcessService` implementation backed by SSH |

### New Files — GUI (`TabgInstaller.Gui/`)

| File | Responsibility |
|---|---|
| `Services/IServerInstanceManager.cs` | Interface: instance CRUD, active instance, persistence |
| `Services/ServerInstanceManager.cs` | Implementation: manages collection, saves/loads instances.json, migration |
| `Services/IActiveInstanceService.cs` | Interface: proxies active instance's path + services |
| `Services/ActiveInstanceService.cs` | Implementation: listens to manager, forwards PathChanged |
| `Model/ServerInstance.cs` | Runtime model: owns process service, health monitor, observable state |
| `Model/RemoteServerInstance.cs` | Subclass: owns SSH service + remote process service instead of local |
| `ViewModels/ServerListViewModel.cs` | Sidebar ViewModel: instance list, selection, add/remove/rename |
| `ViewModels/AddServerDialogViewModel.cs` | Add server dialog ViewModel |
| `Windows/AddServerDialog.xaml` | Add server dialog UI |
| `Windows/AddServerDialog.xaml.cs` | Add server dialog code-behind (minimal) |
| `Controls/ServerListControl.xaml` | Sidebar UserControl |
| `Controls/ServerListControl.xaml.cs` | Sidebar code-behind (minimal) |
| `Controls/HealthCardControl.xaml` | Dashboard health card per instance |
| `Controls/HealthCardControl.xaml.cs` | Health card code-behind (minimal) |

### Modified Files

| File | Changes |
|---|---|
| `TabgInstaller.Core/Services/LogLineParser.cs` | No changes — ServerEventParser is separate |
| `TabgInstaller.Core/Services/ServerProcessService.cs` | Add `event Action? ProcessExited` event, fire on process exit |
| `TabgInstaller.Core/Services/IServerProcessService.cs` | Add `event Action? ProcessExited` to interface |
| `TabgInstaller.Gui/App.xaml.cs` | Replace old DI registrations with new services |
| `TabgInstaller.Gui/MainWindow.xaml` | Add sidebar Grid column wrapping TabControl |
| `TabgInstaller.Gui/MainWindow.xaml.cs` | Inject `IServerInstanceManager` + `IActiveInstanceService`, wire sidebar, update `InitializeAllPanels` |
| `TabgInstaller.Gui/ViewModels/DashboardViewModel.cs` | Replace `IServerPathProvider`/`IServerProcessService` with `IActiveInstanceService`, add health cards for all instances |
| `TabgInstaller.Gui/ViewModels/ConsolePanelViewModel.cs` | Replace `IServerPathProvider`/`IServerProcessService` with `IActiveInstanceService` |
| All other ViewModels injecting `IServerPathProvider` | Replace with `IActiveInstanceService` (same pattern) |
| `TabgInstaller.Core.csproj` | No new packages (DPAPI already referenced) |
| `TabgInstaller.Gui.csproj` | Add SSH.NET package |

### New Test Files

| File | Tests |
|---|---|
| `TabgInstaller.Tests/Model/ServerInstanceDataTests.cs` | Data model defaults, serialization |
| `TabgInstaller.Tests/Services/ServerEventParserTests.cs` | Parse player join/leave/join-code from real log lines |
| `TabgInstaller.Tests/Services/HealthMonitorServiceTests.cs` | Player tracking, status transitions, auto-restart logic, watchdog |
| `TabgInstaller.Tests/Services/CredentialStorageServiceTests.cs` | Encrypt/decrypt round-trip |
| `TabgInstaller.Tests/Services/ServerInstanceManagerTests.cs` | CRUD, persistence, migration |
| `TabgInstaller.Tests/Services/ActiveInstanceServiceTests.cs` | Proxy behavior, event forwarding |
| `TabgInstaller.Tests/ViewModels/ServerListViewModelTests.cs` | Sidebar ViewModel |
| `TabgInstaller.Tests/Services/RemoteProcessServiceTests.cs` | Command construction for screen/systemd modes |

### Modified Test Files

| File | Changes |
|---|---|
| `TabgInstaller.Tests/ViewModels/DashboardViewModelTests.cs` | Update mocks: `IServerPathProvider` → `IActiveInstanceService` |
| `TabgInstaller.Tests/ViewModels/ConsolePanelViewModelTests.cs` | Same migration |
| All other ViewModel test files mocking `IServerPathProvider` | Same migration |

---

## Task 1: Data Models

**Files:**
- Create: `TabgInstaller.Core/Model/ServerInstanceData.cs`
- Create: `TabgInstaller.Core/Model/ServerEvent.cs`
- Test: `TabgInstaller.Tests/Model/ServerInstanceDataTests.cs`

- [ ] **Step 1: Write failing tests for data models**

Create `TabgInstaller.Tests/Model/ServerInstanceDataTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core.Model;
using Xunit;

namespace TabgInstaller.Tests.Model
{
    public class ServerInstanceDataTests
    {
        [Fact]
        public void ServerInstanceData_DefaultValues_AreCorrect()
        {
            var data = new ServerInstanceData();
            data.Id.Should().NotBeEmpty();
            data.DisplayName.Should().Be("");
            data.ServerPath.Should().Be("");
            data.InstanceType.Should().Be(ServerInstanceType.Local);
            data.AutoRestart.Should().NotBeNull();
        }

        [Fact]
        public void AutoRestartConfig_DefaultValues_AreCorrect()
        {
            var config = new AutoRestartConfig();
            config.Enabled.Should().BeTrue();
            config.MaxRetries.Should().Be(3);
            config.InitialBackoffSeconds.Should().Be(5);
            config.WatchdogIntervalSeconds.Should().Be(300);
            config.StabilityThresholdSeconds.Should().Be(30);
        }

        [Fact]
        public void RemoteConnectionConfig_DefaultValues_AreCorrect()
        {
            var config = new RemoteConnectionConfig();
            config.Host.Should().Be("");
            config.Port.Should().Be(22);
            config.Username.Should().Be("");
            config.AuthMethod.Should().Be(SshAuthMethod.Password);
            config.PrivateKeyPath.Should().Be("");
            config.RemoteServerPath.Should().Be("");
            config.ProcessMode.Should().Be(RemoteProcessMode.Screen);
        }

        [Fact]
        public void InstancesFileData_DefaultValues_AreCorrect()
        {
            var data = new InstancesFileData();
            data.Instances.Should().BeEmpty();
            data.ActiveInstanceId.Should().BeNull();
        }

        [Fact]
        public void ConnectedPlayer_StoresProperties()
        {
            var player = new ConnectedPlayer
            {
                Name = "Jon_ass",
                EpicId = "0002679463fd49ffab724df634f46418",
                JoinedAt = new System.DateTime(2026, 4, 9, 14, 7, 25)
            };
            player.Name.Should().Be("Jon_ass");
            player.EpicId.Should().Be("0002679463fd49ffab724df634f46418");
        }

        [Fact]
        public void ServerHealthStatus_HasExpectedValues()
        {
            ServerHealthStatus.Stopped.Should().BeDefined();
            ServerHealthStatus.Running.Should().BeDefined();
            ServerHealthStatus.Crashed.Should().BeDefined();
            ServerHealthStatus.Restarting.Should().BeDefined();
            ServerHealthStatus.Watchdog.Should().BeDefined();
        }

        [Fact]
        public void ServerEventType_HasExpectedValues()
        {
            ServerEventType.PlayerJoined.Should().BeDefined();
            ServerEventType.PlayerLeft.Should().BeDefined();
            ServerEventType.JoinCodeReceived.Should().BeDefined();
            ServerEventType.ProcessExited.Should().BeDefined();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "ServerInstanceDataTests" -v minimal`
Expected: FAIL — types don't exist yet

- [ ] **Step 3: Create ServerInstanceData.cs**

Create `TabgInstaller.Core/Model/ServerInstanceData.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TabgInstaller.Core.Model
{
    public enum ServerInstanceType { Local, Remote }
    public enum ServerHealthStatus { Stopped, Running, Crashed, Restarting, Watchdog }
    public enum SshAuthMethod { Password, PrivateKey }
    public enum RemoteProcessMode { Screen, Systemd }

    public class ServerInstanceData
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DisplayName { get; set; } = "";
        public string ServerPath { get; set; } = "";
        public ServerInstanceType InstanceType { get; set; } = ServerInstanceType.Local;
        public AutoRestartConfig AutoRestart { get; set; } = new();
        public RemoteConnectionConfig? RemoteConfig { get; set; }
    }

    public class AutoRestartConfig
    {
        public bool Enabled { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
        public int InitialBackoffSeconds { get; set; } = 5;
        public int WatchdogIntervalSeconds { get; set; } = 300;
        public int StabilityThresholdSeconds { get; set; } = 30;
    }

    public class RemoteConnectionConfig
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public SshAuthMethod AuthMethod { get; set; } = SshAuthMethod.Password;
        public string PrivateKeyPath { get; set; } = "";
        public string RemoteServerPath { get; set; } = "";
        public RemoteProcessMode ProcessMode { get; set; } = RemoteProcessMode.Screen;
    }

    public class ConnectedPlayer
    {
        public string Name { get; set; } = "";
        public string EpicId { get; set; } = "";
        public DateTime JoinedAt { get; set; }
    }

    public class InstancesFileData
    {
        public List<ServerInstanceData> Instances { get; set; } = new();
        public Guid? ActiveInstanceId { get; set; }
    }
}
```

- [ ] **Step 4: Create ServerEvent.cs**

Create `TabgInstaller.Core/Model/ServerEvent.cs`:

```csharp
using System;

namespace TabgInstaller.Core.Model
{
    public enum ServerEventType
    {
        PlayerJoined,
        PlayerLeft,
        JoinCodeReceived,
        ProcessExited
    }

    public class ServerEvent
    {
        public ServerEventType Type { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string? PlayerName { get; init; }
        public string? EpicId { get; init; }
        public int? PlayerIndex { get; init; }
        public string? JoinCode { get; init; }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "ServerInstanceDataTests" -v minimal`
Expected: All 7 tests PASS

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Core/Model/ServerInstanceData.cs TabgInstaller.Core/Model/ServerEvent.cs TabgInstaller.Tests/Model/ServerInstanceDataTests.cs
git commit -m "feat: add Phase 4 data models (ServerInstanceData, ServerEvent, enums)"
```

---

## Task 2: ServerEventParser

Parses TABG-specific log lines into structured `ServerEvent` objects. Separate from existing `LogLineParser` — this one extracts game events, not log severity.

**Files:**
- Create: `TabgInstaller.Core/Services/ServerEventParser.cs`
- Test: `TabgInstaller.Tests/Services/ServerEventParserTests.cs`

- [ ] **Step 1: Write failing tests with real log patterns**

Create `TabgInstaller.Tests/Services/ServerEventParserTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ServerEventParserTests
    {
        [Fact]
        public void TryParse_PlayerAssignment_ReturnsPlayerJoined()
        {
            var line = "[LandLog] - Player: 0 Name: Jon_ass : Assigning EPic ID: 0002679463fd49ffab724df634f46418";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.PlayerJoined);
            result.PlayerName.Should().Be("Jon_ass");
            result.EpicId.Should().Be("0002679463fd49ffab724df634f46418");
            result.PlayerIndex.Should().Be(0);
        }

        [Fact]
        public void TryParse_PlayerLeft_ReturnsPlayerLeft()
        {
            var line = "[LandLog] - Player left: Jon_ass";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.PlayerLeft);
            result.PlayerName.Should().Be("Jon_ass");
        }

        [Fact]
        public void TryParse_ClientDisconnected_ReturnsPlayerLeft()
        {
            var line = "[LandLog] - Client: 0 disconnected from server";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.PlayerLeft);
            result.PlayerIndex.Should().Be(0);
        }

        [Fact]
        public void TryParse_JoinCode_ReturnsJoinCodeReceived()
        {
            var line = "[LandLog] - Host - Got join code: FWJTKK";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.JoinCodeReceived);
            result.JoinCode.Should().Be("FWJTKK");
        }

        [Fact]
        public void TryParse_UnrelatedLine_ReturnsNull()
        {
            var line = "[INFO] [UnityMemory] Configuration Parameters";
            var result = ServerEventParser.TryParse(line);
            result.Should().BeNull();
        }

        [Fact]
        public void TryParse_ProcessExited_ReturnsProcessExited()
        {
            var line = "<process exited>";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.Type.Should().Be(ServerEventType.ProcessExited);
        }

        [Fact]
        public void TryParse_PlayerWithSpacesInName_ParsesCorrectly()
        {
            var line = "[LandLog] - Player: 5 Name: Cool Player 123 : Assigning EPic ID: abc123def456";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.PlayerName.Should().Be("Cool Player 123");
            result.EpicId.Should().Be("abc123def456");
            result.PlayerIndex.Should().Be(5);
        }

        [Fact]
        public void TryParse_PlayerLeftWithSpaces_ParsesCorrectly()
        {
            var line = "[LandLog] - Player left: Cool Player 123";
            var result = ServerEventParser.TryParse(line);

            result.Should().NotBeNull();
            result!.PlayerName.Should().Be("Cool Player 123");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "ServerEventParserTests" -v minimal`
Expected: FAIL — `ServerEventParser` doesn't exist

- [ ] **Step 3: Implement ServerEventParser**

Create `TabgInstaller.Core/Services/ServerEventParser.cs`:

```csharp
using System.Text.RegularExpressions;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public static class ServerEventParser
    {
        // [LandLog] - Player: 0 Name: Jon_ass : Assigning EPic ID: 0002679463fd49ffab724df634f46418
        private static readonly Regex PlayerJoinedPattern = new(
            @"\[LandLog\]\s*-\s*Player:\s*(\d+)\s+Name:\s*(.+?)\s*:\s*Assigning EPic ID:\s*(\S+)",
            RegexOptions.Compiled);

        // [LandLog] - Player left: Jon_ass
        private static readonly Regex PlayerLeftPattern = new(
            @"\[LandLog\]\s*-\s*Player left:\s*(.+)$",
            RegexOptions.Compiled);

        // [LandLog] - Client: 0 disconnected from server
        private static readonly Regex ClientDisconnectedPattern = new(
            @"\[LandLog\]\s*-\s*Client:\s*(\d+)\s+disconnected from server",
            RegexOptions.Compiled);

        // [LandLog] - Host - Got join code: FWJTKK
        private static readonly Regex JoinCodePattern = new(
            @"\[LandLog\]\s*-\s*Host\s*-\s*Got join code:\s*(\S+)",
            RegexOptions.Compiled);

        // <process exited>
        private static readonly Regex ProcessExitedPattern = new(
            @"^<process exited>$",
            RegexOptions.Compiled);

        /// <summary>
        /// Attempts to parse a log line into a structured ServerEvent.
        /// Returns null if the line doesn't match any known game event pattern.
        /// </summary>
        public static ServerEvent? TryParse(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            var match = PlayerJoinedPattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.PlayerJoined,
                    PlayerIndex = int.Parse(match.Groups[1].Value),
                    PlayerName = match.Groups[2].Value.Trim(),
                    EpicId = match.Groups[3].Value
                };
            }

            match = PlayerLeftPattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.PlayerLeft,
                    PlayerName = match.Groups[1].Value.Trim()
                };
            }

            match = ClientDisconnectedPattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.PlayerLeft,
                    PlayerIndex = int.Parse(match.Groups[1].Value)
                };
            }

            match = JoinCodePattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.JoinCodeReceived,
                    JoinCode = match.Groups[1].Value
                };
            }

            match = ProcessExitedPattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.ProcessExited
                };
            }

            return null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "ServerEventParserTests" -v minimal`
Expected: All 8 tests PASS

- [ ] **Step 5: Commit**

```bash
git add TabgInstaller.Core/Services/ServerEventParser.cs TabgInstaller.Tests/Services/ServerEventParserTests.cs
git commit -m "feat: add ServerEventParser for TABG player join/leave/join-code detection"
```

---

## Task 3: Add ProcessExited event to ServerProcessService

The health monitor needs to know when the process exits to trigger crash recovery.

**Files:**
- Modify: `TabgInstaller.Core/Services/IServerProcessService.cs`
- Modify: `TabgInstaller.Core/Services/ServerProcessService.cs`

- [ ] **Step 1: Add ProcessExited and ProcessId to the interface**

In `TabgInstaller.Core/Services/IServerProcessService.cs`, add after line 12 (`event Action<string>? OutputReceived;`):

```csharp
        event Action<int>? ProcessExited; // exit code
        int ProcessId { get; }
```

- [ ] **Step 2: Implement in ServerProcessService**

In `TabgInstaller.Core/Services/ServerProcessService.cs`, add the event and property declarations after line 21 (`public event Action<LogEntry>? LogEntryReceived;`):

```csharp
        public event Action<int>? ProcessExited;
        public int ProcessId => _proc?.Id ?? 0;
```

Then update the `_proc.Exited` handler in the `Start()` method. Replace lines 75-81:

```csharp
            _proc.Exited += (s, e) =>
            {
                var line = "<process exited>";
                OutputReceived?.Invoke(line);
                var entry = LogLineParser.Parse(line);
                AddLogEntry(entry);
            };
```

with:

```csharp
            _proc.Exited += (s, e) =>
            {
                var exitCode = -1;
                try { exitCode = _proc?.ExitCode ?? -1; } catch { }
                var line = "<process exited>";
                OutputReceived?.Invoke(line);
                var entry = LogLineParser.Parse(line);
                AddLogEntry(entry);
                ProcessExited?.Invoke(exitCode);
            };
```

- [ ] **Step 3: Run existing tests to verify no regressions**

Run: `dotnet test TabgInstaller.Tests -v minimal`
Expected: All existing tests PASS

- [ ] **Step 4: Commit**

```bash
git add TabgInstaller.Core/Services/IServerProcessService.cs TabgInstaller.Core/Services/ServerProcessService.cs
git commit -m "feat: add ProcessExited event to IServerProcessService"
```

---

## Task 4: HealthMonitorService

Per-instance health tracking: player count, uptime, memory, status, auto-restart with watchdog.

**Files:**
- Create: `TabgInstaller.Core/Services/IHealthMonitorService.cs`
- Create: `TabgInstaller.Core/Services/HealthMonitorService.cs`
- Test: `TabgInstaller.Tests/Services/HealthMonitorServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `TabgInstaller.Tests/Services/HealthMonitorServiceTests.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Threading;
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class HealthMonitorServiceTests
    {
        private readonly Mock<IServerProcessService> _procSvc = new();

        private HealthMonitorService CreateSut()
        {
            _procSvc.SetupGet(p => p.LogEntries).Returns(new ObservableCollection<LogEntry>());
            return new HealthMonitorService(_procSvc.Object);
        }

        [Fact]
        public void InitialState_IsStoppedWithZeroPlayers()
        {
            var sut = CreateSut();
            sut.Status.Should().Be(ServerHealthStatus.Stopped);
            sut.PlayerCount.Should().Be(0);
            sut.IsAlive.Should().BeFalse();
            sut.JoinCode.Should().BeNull();
            sut.ConnectedPlayers.Should().BeEmpty();
        }

        [Fact]
        public void HandleEvent_PlayerJoined_IncrementsCountAndAddsPlayer()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent
            {
                Type = ServerEventType.PlayerJoined,
                PlayerName = "Jon_ass",
                EpicId = "abc123",
                PlayerIndex = 0
            });

            sut.PlayerCount.Should().Be(1);
            sut.ConnectedPlayers.Should().ContainSingle(p => p.Name == "Jon_ass" && p.EpicId == "abc123");
        }

        [Fact]
        public void HandleEvent_PlayerLeft_DecrementsCountAndRemovesPlayer()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerJoined, PlayerName = "Jon_ass", EpicId = "abc", PlayerIndex = 0 });
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerLeft, PlayerName = "Jon_ass" });

            sut.PlayerCount.Should().Be(0);
            sut.ConnectedPlayers.Should().BeEmpty();
        }

        [Fact]
        public void HandleEvent_PlayerLeft_NeverGoesBelowZero()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerLeft, PlayerName = "Ghost" });
            sut.PlayerCount.Should().Be(0);
        }

        [Fact]
        public void HandleEvent_JoinCode_StoresCode()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.JoinCodeReceived, JoinCode = "FWJTKK" });
            sut.JoinCode.Should().Be("FWJTKK");
        }

        [Fact]
        public void MarkRunning_SetsStatusAndStartsUptime()
        {
            var sut = CreateSut();
            sut.MarkRunning();
            sut.Status.Should().Be(ServerHealthStatus.Running);
            sut.IsAlive.Should().BeTrue();
        }

        [Fact]
        public void MarkStopped_ResetsState()
        {
            var sut = CreateSut();
            sut.MarkRunning();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerJoined, PlayerName = "P1", EpicId = "e1", PlayerIndex = 0 });
            sut.MarkStopped();

            sut.Status.Should().Be(ServerHealthStatus.Stopped);
            sut.IsAlive.Should().BeFalse();
            sut.PlayerCount.Should().Be(0);
            sut.ConnectedPlayers.Should().BeEmpty();
            sut.JoinCode.Should().BeNull();
        }

        [Fact]
        public void MarkCrashed_SetsStatusToCrashed()
        {
            var sut = CreateSut();
            sut.MarkRunning();
            sut.MarkCrashed();
            sut.Status.Should().Be(ServerHealthStatus.Crashed);
            sut.IsAlive.Should().BeFalse();
        }

        [Fact]
        public void MarkRestarting_SetsStatusAndTracksAttempt()
        {
            var sut = CreateSut();
            sut.MarkRestarting(1, 3);
            sut.Status.Should().Be(ServerHealthStatus.Restarting);
            sut.RestartAttempt.Should().Be(1);
            sut.MaxRetries.Should().Be(3);
        }

        [Fact]
        public void MarkWatchdog_SetsStatusToWatchdog()
        {
            var sut = CreateSut();
            sut.MarkWatchdog();
            sut.Status.Should().Be(ServerHealthStatus.Watchdog);
        }

        [Fact]
        public void UpdateMemoryUsage_StoresValue()
        {
            var sut = CreateSut();
            sut.UpdateMemoryUsage(842);
            sut.MemoryUsageMb.Should().Be(842);
        }

        [Fact]
        public void Uptime_WhenRunning_ReturnsElapsed()
        {
            var sut = CreateSut();
            sut.MarkRunning();
            Thread.Sleep(50);
            sut.Uptime.TotalMilliseconds.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Uptime_WhenStopped_ReturnsZero()
        {
            var sut = CreateSut();
            sut.Uptime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void HandleEvent_DuplicatePlayerJoin_DoesNotDoubleCount()
        {
            var sut = CreateSut();
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerJoined, PlayerName = "Jon_ass", EpicId = "abc", PlayerIndex = 0 });
            sut.HandleEvent(new ServerEvent { Type = ServerEventType.PlayerJoined, PlayerName = "Jon_ass", EpicId = "abc", PlayerIndex = 0 });
            sut.PlayerCount.Should().Be(1);
            sut.ConnectedPlayers.Should().HaveCount(1);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "HealthMonitorServiceTests" -v minimal`
Expected: FAIL — types don't exist

- [ ] **Step 3: Create IHealthMonitorService interface**

Create `TabgInstaller.Core/Services/IHealthMonitorService.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public interface IHealthMonitorService
    {
        bool IsAlive { get; }
        int PlayerCount { get; }
        TimeSpan Uptime { get; }
        long MemoryUsageMb { get; }
        ServerHealthStatus Status { get; }
        string? JoinCode { get; }
        int RestartAttempt { get; }
        int MaxRetries { get; }
        ObservableCollection<ConnectedPlayer> ConnectedPlayers { get; }

        event Action? StatusChanged;
        event Action? ServerCrashed;
        event Action? ServerRecovered;

        void HandleEvent(ServerEvent serverEvent);
        void MarkRunning();
        void MarkStopped();
        void MarkCrashed();
        void MarkRestarting(int attempt, int maxRetries);
        void MarkWatchdog();
        void UpdateMemoryUsage(long megabytes);
    }
}
```

- [ ] **Step 4: Create HealthMonitorService implementation**

Create `TabgInstaller.Core/Services/HealthMonitorService.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public class HealthMonitorService : IHealthMonitorService
    {
        private readonly IServerProcessService _processService;
        private DateTime? _startedAt;

        public bool IsAlive => Status == ServerHealthStatus.Running;
        public int PlayerCount => ConnectedPlayers.Count;
        public long MemoryUsageMb { get; private set; }
        public ServerHealthStatus Status { get; private set; } = ServerHealthStatus.Stopped;
        public string? JoinCode { get; private set; }
        public int RestartAttempt { get; private set; }
        public int MaxRetries { get; private set; }
        public ObservableCollection<ConnectedPlayer> ConnectedPlayers { get; } = new();

        public TimeSpan Uptime => _startedAt.HasValue
            ? DateTime.Now - _startedAt.Value
            : TimeSpan.Zero;

        public event Action? StatusChanged;
        public event Action? ServerCrashed;
        public event Action? ServerRecovered;

        public HealthMonitorService(IServerProcessService processService)
        {
            _processService = processService;
        }

        public void HandleEvent(ServerEvent serverEvent)
        {
            switch (serverEvent.Type)
            {
                case ServerEventType.PlayerJoined:
                    if (!string.IsNullOrEmpty(serverEvent.PlayerName) &&
                        !ConnectedPlayers.Any(p => p.Name == serverEvent.PlayerName))
                    {
                        ConnectedPlayers.Add(new ConnectedPlayer
                        {
                            Name = serverEvent.PlayerName,
                            EpicId = serverEvent.EpicId ?? "",
                            JoinedAt = serverEvent.Timestamp
                        });
                    }
                    break;

                case ServerEventType.PlayerLeft:
                    if (!string.IsNullOrEmpty(serverEvent.PlayerName))
                    {
                        var player = ConnectedPlayers.FirstOrDefault(p => p.Name == serverEvent.PlayerName);
                        if (player != null)
                            ConnectedPlayers.Remove(player);
                    }
                    else if (serverEvent.PlayerIndex.HasValue)
                    {
                        // Client disconnect by index — we can't reliably map index to player
                        // so this is a secondary signal handled by the PlayerLeft name match
                    }
                    break;

                case ServerEventType.JoinCodeReceived:
                    JoinCode = serverEvent.JoinCode;
                    break;

                case ServerEventType.ProcessExited:
                    // Handled externally by crash recovery logic
                    break;
            }
        }

        public void MarkRunning()
        {
            var wasNotRunning = Status != ServerHealthStatus.Running;
            Status = ServerHealthStatus.Running;
            _startedAt = DateTime.Now;
            RestartAttempt = 0;
            StatusChanged?.Invoke();
            if (wasNotRunning)
                ServerRecovered?.Invoke();
        }

        public void MarkStopped()
        {
            Status = ServerHealthStatus.Stopped;
            _startedAt = null;
            ConnectedPlayers.Clear();
            JoinCode = null;
            MemoryUsageMb = 0;
            RestartAttempt = 0;
            StatusChanged?.Invoke();
        }

        public void MarkCrashed()
        {
            Status = ServerHealthStatus.Crashed;
            _startedAt = null;
            ConnectedPlayers.Clear();
            JoinCode = null;
            ServerCrashed?.Invoke();
            StatusChanged?.Invoke();
        }

        public void MarkRestarting(int attempt, int maxRetries)
        {
            Status = ServerHealthStatus.Restarting;
            RestartAttempt = attempt;
            MaxRetries = maxRetries;
            StatusChanged?.Invoke();
        }

        public void MarkWatchdog()
        {
            Status = ServerHealthStatus.Watchdog;
            StatusChanged?.Invoke();
        }

        public void UpdateMemoryUsage(long megabytes)
        {
            MemoryUsageMb = megabytes;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "HealthMonitorServiceTests" -v minimal`
Expected: All 14 tests PASS

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Core/Services/IHealthMonitorService.cs TabgInstaller.Core/Services/HealthMonitorService.cs TabgInstaller.Tests/Services/HealthMonitorServiceTests.cs
git commit -m "feat: add HealthMonitorService with player tracking, status transitions, and auto-restart state"
```

---

## Task 5: CredentialStorageService

DPAPI-based encrypted credential storage for SSH passwords and key passphrases.

**Files:**
- Create: `TabgInstaller.Core/Services/ICredentialStorageService.cs`
- Create: `TabgInstaller.Core/Services/CredentialStorageService.cs`
- Test: `TabgInstaller.Tests/Services/CredentialStorageServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `TabgInstaller.Tests/Services/CredentialStorageServiceTests.cs`:

```csharp
using System;
using System.IO;
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class CredentialStorageServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly CredentialStorageService _sut;

        public CredentialStorageServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _sut = new CredentialStorageService(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void Store_ThenRetrieve_RoundTrips()
        {
            var id = Guid.NewGuid();
            _sut.Store(id, "password", "my-secret-password");
            var result = _sut.Retrieve(id, "password");
            result.Should().Be("my-secret-password");
        }

        [Fact]
        public void Retrieve_NonExistent_ReturnsNull()
        {
            var result = _sut.Retrieve(Guid.NewGuid(), "password");
            result.Should().BeNull();
        }

        [Fact]
        public void Store_OverwritesExisting()
        {
            var id = Guid.NewGuid();
            _sut.Store(id, "password", "old");
            _sut.Store(id, "password", "new");
            _sut.Retrieve(id, "password").Should().Be("new");
        }

        [Fact]
        public void Remove_DeletesAllCredentialsForInstance()
        {
            var id = Guid.NewGuid();
            _sut.Store(id, "password", "secret");
            _sut.Store(id, "passphrase", "other-secret");
            _sut.Remove(id);
            _sut.Retrieve(id, "password").Should().BeNull();
            _sut.Retrieve(id, "passphrase").Should().BeNull();
        }

        [Fact]
        public void Store_MultipleInstances_Independent()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            _sut.Store(id1, "password", "secret1");
            _sut.Store(id2, "password", "secret2");
            _sut.Retrieve(id1, "password").Should().Be("secret1");
            _sut.Retrieve(id2, "password").Should().Be("secret2");
        }

        [Fact]
        public void PersistsAcrossInstances()
        {
            var id = Guid.NewGuid();
            _sut.Store(id, "password", "persisted-secret");

            // Create a new instance pointing to the same directory
            var sut2 = new CredentialStorageService(_tempDir);
            sut2.Retrieve(id, "password").Should().Be("persisted-secret");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "CredentialStorageServiceTests" -v minimal`
Expected: FAIL — types don't exist

- [ ] **Step 3: Create ICredentialStorageService interface**

Create `TabgInstaller.Core/Services/ICredentialStorageService.cs`:

```csharp
using System;

namespace TabgInstaller.Core.Services
{
    public interface ICredentialStorageService
    {
        void Store(Guid instanceId, string credentialType, string value);
        string? Retrieve(Guid instanceId, string credentialType);
        void Remove(Guid instanceId);
    }
}
```

- [ ] **Step 4: Create CredentialStorageService implementation**

Create `TabgInstaller.Core/Services/CredentialStorageService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Services
{
    public class CredentialStorageService : ICredentialStorageService
    {
        private readonly string _filePath;
        private Dictionary<string, string> _store; // key → base64 encrypted blob

        public CredentialStorageService(string storageDir)
        {
            _filePath = Path.Combine(storageDir, "credentials.dat");
            _store = LoadFromDisk();
        }

        public void Store(Guid instanceId, string credentialType, string value)
        {
            var key = BuildKey(instanceId, credentialType);
            var plainBytes = Encoding.UTF8.GetBytes(value);
            var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            _store[key] = Convert.ToBase64String(encrypted);
            SaveToDisk();
        }

        public string? Retrieve(Guid instanceId, string credentialType)
        {
            var key = BuildKey(instanceId, credentialType);
            if (!_store.TryGetValue(key, out var base64))
                return null;

            try
            {
                var encrypted = Convert.FromBase64String(base64);
                var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                // Decryption failed — credential is invalid (e.g., user profile changed)
                _store.Remove(key);
                SaveToDisk();
                return null;
            }
        }

        public void Remove(Guid instanceId)
        {
            var prefix = instanceId.ToString() + "_";
            var keysToRemove = _store.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
                _store.Remove(key);
            SaveToDisk();
        }

        private static string BuildKey(Guid instanceId, string credentialType)
            => $"{instanceId}_{credentialType}";

        private Dictionary<string, string> LoadFromDisk()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                           ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CredentialStorage] Failed to load: {ex.Message}");
            }
            return new Dictionary<string, string>();
        }

        private void SaveToDisk()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(_store, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CredentialStorage] Failed to save: {ex.Message}");
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "CredentialStorageServiceTests" -v minimal`
Expected: All 6 tests PASS

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Core/Services/ICredentialStorageService.cs TabgInstaller.Core/Services/CredentialStorageService.cs TabgInstaller.Tests/Services/CredentialStorageServiceTests.cs
git commit -m "feat: add DPAPI-based CredentialStorageService for SSH credential encryption"
```

---

## Task 6: IActiveInstanceService & Implementation

The bridge between multi-server and existing ViewModels. Proxies the active instance's path and services.

**Files:**
- Create: `TabgInstaller.Gui/Services/IActiveInstanceService.cs`
- Create: `TabgInstaller.Gui/Services/ActiveInstanceService.cs`
- Test: `TabgInstaller.Tests/Services/ActiveInstanceServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `TabgInstaller.Tests/Services/ActiveInstanceServiceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "ActiveInstanceServiceTests" -v minimal`
Expected: FAIL — types don't exist

- [ ] **Step 3: Create IServerInstanceContext (what an instance exposes to the proxy)**

This is what `ServerInstance` and `RemoteServerInstance` will implement. Add to `TabgInstaller.Gui/Services/IActiveInstanceService.cs`:

```csharp
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
```

- [ ] **Step 4: Create IServerInstanceManager interface**

Create `TabgInstaller.Gui/Services/IServerInstanceManager.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.Services
{
    public interface IServerInstanceManager
    {
        ObservableCollection<ServerInstanceData> InstanceDataList { get; }
        IServerInstanceContext? ActiveInstance { get; }
        ServerInstanceData? ActiveInstanceData { get; }
        event Action? ActiveInstanceChanged;

        IServerInstanceContext AddLocalInstance(string displayName, string serverPath);
        IServerInstanceContext AddRemoteInstance(string displayName, RemoteConnectionConfig config);
        void RemoveInstance(Guid id);
        void SetActiveInstance(Guid id);
        void RenameInstance(Guid id, string newName);
        void Save();
        void Load();
    }
}
```

- [ ] **Step 5: Create ActiveInstanceService implementation**

Create `TabgInstaller.Gui/Services/ActiveInstanceService.cs`:

```csharp
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
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "ActiveInstanceServiceTests" -v minimal`
Expected: All 5 tests PASS

- [ ] **Step 7: Commit**

```bash
git add TabgInstaller.Gui/Services/IActiveInstanceService.cs TabgInstaller.Gui/Services/IServerInstanceManager.cs TabgInstaller.Gui/Services/ActiveInstanceService.cs TabgInstaller.Tests/Services/ActiveInstanceServiceTests.cs
git commit -m "feat: add IActiveInstanceService and IServerInstanceManager interfaces with ActiveInstanceService proxy"
```

---

## Task 7: ServerInstance Runtime Model

The local server instance that owns its own process service and health monitor.

**Files:**
- Create: `TabgInstaller.Gui/Model/ServerInstance.cs`
- Test: (tested via ServerInstanceManager tests in Task 8)

- [ ] **Step 1: Create ServerInstance**

Create `TabgInstaller.Gui/Model/ServerInstance.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Model
{
    public partial class ServerInstance : ObservableObject, IServerInstanceContext, IDisposable
    {
        private readonly ServerProcessService _ownedProcessService;
        private readonly HealthMonitorService _ownedHealthMonitor;
        private CancellationTokenSource? _restartCts;
        private Timer? _memoryTimer;

        public ServerInstanceData Data { get; }
        public Guid Id => Data.Id;
        public string ServerPath => Data.ServerPath;
        public IServerProcessService ProcessService => _ownedProcessService;
        public IHealthMonitorService HealthMonitor => _ownedHealthMonitor;

        [ObservableProperty] private string _displayName;
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private int _playerCount;
        [ObservableProperty] private ServerHealthStatus _healthStatus = ServerHealthStatus.Stopped;

        public ServerInstance(ServerInstanceData data)
        {
            Data = data;
            _displayName = data.DisplayName;
            _ownedProcessService = new ServerProcessService(data.ServerPath);
            _ownedHealthMonitor = new HealthMonitorService(_ownedProcessService);

            // Wire log events to health monitor via ServerEventParser
            _ownedProcessService.OutputReceived += OnOutputReceived;
            _ownedProcessService.ProcessExited += OnProcessExited;
            _ownedHealthMonitor.StatusChanged += OnHealthStatusChanged;
        }

        private void OnOutputReceived(string line)
        {
            var serverEvent = ServerEventParser.TryParse(line);
            if (serverEvent != null)
            {
                _ownedHealthMonitor.HandleEvent(serverEvent);
                PlayerCount = _ownedHealthMonitor.PlayerCount;
            }
        }

        private void OnProcessExited(int exitCode)
        {
            IsRunning = false;
            StopMemoryMonitor();

            if (Data.AutoRestart.Enabled && exitCode != 0)
            {
                _ownedHealthMonitor.MarkCrashed();
                _ = RunAutoRestartAsync();
            }
            else
            {
                _ownedHealthMonitor.MarkStopped();
            }
        }

        private void OnHealthStatusChanged()
        {
            HealthStatus = _ownedHealthMonitor.Status;
        }

        public bool Start()
        {
            _restartCts?.Cancel();
            try
            {
                var result = _ownedProcessService.Start();
                if (result)
                {
                    IsRunning = true;
                    _ownedHealthMonitor.MarkRunning();
                    StartMemoryMonitor();
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ServerInstance] Failed to start: {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            _restartCts?.Cancel();
            _ownedProcessService.Stop();
            IsRunning = false;
            _ownedHealthMonitor.MarkStopped();
            StopMemoryMonitor();
        }

        private async Task RunAutoRestartAsync()
        {
            _restartCts?.Cancel();
            _restartCts = new CancellationTokenSource();
            var ct = _restartCts.Token;
            var config = Data.AutoRestart;
            var delay = TimeSpan.FromSeconds(config.InitialBackoffSeconds);

            for (int attempt = 1; attempt <= config.MaxRetries; attempt++)
            {
                if (ct.IsCancellationRequested) return;

                _ownedHealthMonitor.MarkRestarting(attempt, config.MaxRetries);
                try { await Task.Delay(delay, ct); } catch (TaskCanceledException) { return; }

                if (ct.IsCancellationRequested) return;

                try
                {
                    var started = _ownedProcessService.Start();
                    if (started)
                    {
                        IsRunning = true;
                        StartMemoryMonitor();

                        // Wait for stability threshold
                        try
                        {
                            await Task.Delay(
                                TimeSpan.FromSeconds(config.StabilityThresholdSeconds), ct);
                        }
                        catch (TaskCanceledException) { return; }

                        if (_ownedProcessService.IsRunning)
                        {
                            _ownedHealthMonitor.MarkRunning();
                            return; // Successfully recovered
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ServerInstance] Restart attempt {attempt} failed: {ex.Message}");
                }

                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // Exponential backoff
            }

            // Exhausted retries — enter watchdog mode
            _ = RunWatchdogAsync(ct);
        }

        private async Task RunWatchdogAsync(CancellationToken ct)
        {
            _ownedHealthMonitor.MarkWatchdog();
            var interval = TimeSpan.FromSeconds(Data.AutoRestart.WatchdogIntervalSeconds);

            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(interval, ct); } catch (TaskCanceledException) { return; }
                if (ct.IsCancellationRequested) return;

                try
                {
                    var started = _ownedProcessService.Start();
                    if (started)
                    {
                        IsRunning = true;
                        StartMemoryMonitor();

                        try
                        {
                            await Task.Delay(
                                TimeSpan.FromSeconds(Data.AutoRestart.StabilityThresholdSeconds), ct);
                        }
                        catch (TaskCanceledException) { return; }

                        if (_ownedProcessService.IsRunning)
                        {
                            _ownedHealthMonitor.MarkRunning();
                            return; // Recovered from watchdog
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ServerInstance] Watchdog attempt failed: {ex.Message}");
                }
            }
        }

        private void StartMemoryMonitor()
        {
            StopMemoryMonitor();
            _memoryTimer = new Timer(_ =>
            {
                try
                {
                    if (_ownedProcessService.IsRunning)
                    {
                        var pid = _ownedProcessService.ProcessId;
                        if (pid > 0)
                        {
                            var proc = Process.GetProcessById(pid);
                            _ownedHealthMonitor.UpdateMemoryUsage(proc.WorkingSet64 / (1024 * 1024));
                        }
                    }
                }
                catch { }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        private void StopMemoryMonitor()
        {
            _memoryTimer?.Dispose();
            _memoryTimer = null;
        }

        public void Dispose()
        {
            _restartCts?.Cancel();
            _restartCts?.Dispose();
            StopMemoryMonitor();
            _ownedProcessService.OutputReceived -= OnOutputReceived;
            _ownedProcessService.ProcessExited -= OnProcessExited;
            (_ownedProcessService as IDisposable)?.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run build to verify compilation**

Run: `dotnet build TabgInstaller.Gui -v minimal`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add TabgInstaller.Gui/Model/ServerInstance.cs
git commit -m "feat: add ServerInstance runtime model with process ownership, health wiring, and auto-restart"
```

---

## Task 8: ServerInstanceManager

Instance CRUD, persistence, migration from single-server.

**Files:**
- Create: `TabgInstaller.Gui/Services/ServerInstanceManager.cs`
- Test: `TabgInstaller.Tests/Services/ServerInstanceManagerTests.cs`

- [ ] **Step 1: Write failing tests**

Create `TabgInstaller.Tests/Services/ServerInstanceManagerTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ServerInstanceManagerTests : IDisposable
    {
        private readonly string _tempDir;

        public ServerInstanceManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private ServerInstanceManager CreateSut() => new(_tempDir);

        [Fact]
        public void InitialState_HasNoInstances()
        {
            var sut = CreateSut();
            sut.InstanceDataList.Should().BeEmpty();
            sut.ActiveInstance.Should().BeNull();
        }

        [Fact]
        public void AddLocalInstance_CreatesInstanceAndSetsActive()
        {
            var serverDir = Path.Combine(_tempDir, "server1");
            Directory.CreateDirectory(serverDir);

            var sut = CreateSut();
            var instance = sut.AddLocalInstance("Test Server", serverDir);

            sut.InstanceDataList.Should().HaveCount(1);
            sut.InstanceDataList[0].DisplayName.Should().Be("Test Server");
            sut.ActiveInstance.Should().BeSameAs(instance);
        }

        [Fact]
        public void AddLocalInstance_FiresActiveInstanceChanged()
        {
            var serverDir = Path.Combine(_tempDir, "server1");
            Directory.CreateDirectory(serverDir);

            var sut = CreateSut();
            bool fired = false;
            sut.ActiveInstanceChanged += () => fired = true;
            sut.AddLocalInstance("Test", serverDir);
            fired.Should().BeTrue();
        }

        [Fact]
        public void RemoveInstance_RemovesFromList()
        {
            var dir1 = Path.Combine(_tempDir, "s1");
            var dir2 = Path.Combine(_tempDir, "s2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);

            var sut = CreateSut();
            sut.AddLocalInstance("S1", dir1);
            var i2 = sut.AddLocalInstance("S2", dir2);
            var id1 = sut.InstanceDataList[0].Id;

            sut.RemoveInstance(id1);
            sut.InstanceDataList.Should().HaveCount(1);
            sut.InstanceDataList[0].DisplayName.Should().Be("S2");
        }

        [Fact]
        public void RemoveInstance_CannotRemoveLastInstance()
        {
            var dir = Path.Combine(_tempDir, "s1");
            Directory.CreateDirectory(dir);

            var sut = CreateSut();
            sut.AddLocalInstance("S1", dir);
            var id = sut.InstanceDataList[0].Id;

            var act = () => sut.RemoveInstance(id);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SetActiveInstance_SwitchesActive()
        {
            var dir1 = Path.Combine(_tempDir, "s1");
            var dir2 = Path.Combine(_tempDir, "s2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);

            var sut = CreateSut();
            sut.AddLocalInstance("S1", dir1);
            sut.AddLocalInstance("S2", dir2);

            var id1 = sut.InstanceDataList[0].Id;
            sut.SetActiveInstance(id1);
            sut.ActiveInstance!.ServerPath.Should().Be(dir1);
        }

        [Fact]
        public void RenameInstance_UpdatesDisplayName()
        {
            var dir = Path.Combine(_tempDir, "s1");
            Directory.CreateDirectory(dir);

            var sut = CreateSut();
            sut.AddLocalInstance("Old Name", dir);
            var id = sut.InstanceDataList[0].Id;

            sut.RenameInstance(id, "New Name");
            sut.InstanceDataList[0].DisplayName.Should().Be("New Name");
        }

        [Fact]
        public void SaveAndLoad_PersistsInstances()
        {
            var dir = Path.Combine(_tempDir, "s1");
            Directory.CreateDirectory(dir);

            var sut = CreateSut();
            sut.AddLocalInstance("Persisted", dir);
            sut.Save();

            var sut2 = CreateSut();
            sut2.Load();
            sut2.InstanceDataList.Should().HaveCount(1);
            sut2.InstanceDataList[0].DisplayName.Should().Be("Persisted");
        }

        [Fact]
        public void Load_RestoresActiveInstanceId()
        {
            var dir1 = Path.Combine(_tempDir, "s1");
            var dir2 = Path.Combine(_tempDir, "s2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);

            var sut = CreateSut();
            sut.AddLocalInstance("S1", dir1);
            sut.AddLocalInstance("S2", dir2);
            var id1 = sut.InstanceDataList[0].Id;
            sut.SetActiveInstance(id1);
            sut.Save();

            var sut2 = CreateSut();
            sut2.Load();
            sut2.ActiveInstanceData!.Id.Should().Be(id1);
        }

        [Fact]
        public void MigrateFromSingleServer_CreatesInstanceFromPath()
        {
            var serverDir = Path.Combine(_tempDir, "legacyserver");
            Directory.CreateDirectory(serverDir);
            // Write a minimal game_settings.txt with a ServerName
            File.WriteAllText(Path.Combine(serverDir, "game_settings.txt"),
                "ServerName=My Legacy Server\nMaxPlayers=70\n");

            var sut = CreateSut();
            sut.MigrateFromSingleServer(serverDir);

            sut.InstanceDataList.Should().HaveCount(1);
            sut.InstanceDataList[0].DisplayName.Should().Be("My Legacy Server");
            sut.InstanceDataList[0].ServerPath.Should().Be(serverDir);
            sut.ActiveInstance.Should().NotBeNull();
        }

        [Fact]
        public void MigrateFromSingleServer_FallsBackToDefaultName()
        {
            var serverDir = Path.Combine(_tempDir, "legacyserver");
            Directory.CreateDirectory(serverDir);
            // No game_settings.txt

            var sut = CreateSut();
            sut.MigrateFromSingleServer(serverDir);

            sut.InstanceDataList.Should().HaveCount(1);
            sut.InstanceDataList[0].DisplayName.Should().Be("Server");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "ServerInstanceManagerTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Implement ServerInstanceManager**

Create `TabgInstaller.Gui/Services/ServerInstanceManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.Model;

namespace TabgInstaller.Gui.Services
{
    public class ServerInstanceManager : IServerInstanceManager
    {
        private readonly string _storageDir;
        private readonly string _instancesFilePath;
        private readonly Dictionary<Guid, ServerInstance> _runtimeInstances = new();

        public ObservableCollection<ServerInstanceData> InstanceDataList { get; } = new();
        public IServerInstanceContext? ActiveInstance { get; private set; }
        public ServerInstanceData? ActiveInstanceData { get; private set; }
        public event Action? ActiveInstanceChanged;

        public ServerInstanceManager(string storageDir)
        {
            _storageDir = storageDir;
            _instancesFilePath = Path.Combine(storageDir, "instances.json");
        }

        public IServerInstanceContext AddLocalInstance(string displayName, string serverPath)
        {
            var data = new ServerInstanceData
            {
                DisplayName = displayName,
                ServerPath = serverPath,
                InstanceType = ServerInstanceType.Local
            };

            InstanceDataList.Add(data);
            var runtime = new ServerInstance(data);
            _runtimeInstances[data.Id] = runtime;

            SetActiveInstance(data.Id);
            Save();
            return runtime;
        }

        public IServerInstanceContext AddRemoteInstance(string displayName, RemoteConnectionConfig config)
        {
            var data = new ServerInstanceData
            {
                DisplayName = displayName,
                ServerPath = config.RemoteServerPath,
                InstanceType = ServerInstanceType.Remote,
                RemoteConfig = config
            };

            InstanceDataList.Add(data);
            // RemoteServerInstance will be created in Task 13
            // For now, create a local placeholder
            var runtime = new ServerInstance(data);
            _runtimeInstances[data.Id] = runtime;

            SetActiveInstance(data.Id);
            Save();
            return runtime;
        }

        public void RemoveInstance(Guid id)
        {
            if (InstanceDataList.Count <= 1)
                throw new InvalidOperationException("Cannot remove the last server instance.");

            var data = InstanceDataList.FirstOrDefault(d => d.Id == id);
            if (data == null) return;

            InstanceDataList.Remove(data);

            if (_runtimeInstances.TryGetValue(id, out var runtime))
            {
                runtime.Dispose();
                _runtimeInstances.Remove(id);
            }

            // If we removed the active instance, switch to the first one
            if (ActiveInstanceData?.Id == id)
            {
                SetActiveInstance(InstanceDataList[0].Id);
            }

            Save();
        }

        public void SetActiveInstance(Guid id)
        {
            var data = InstanceDataList.FirstOrDefault(d => d.Id == id);
            if (data == null) return;

            ActiveInstanceData = data;
            _runtimeInstances.TryGetValue(id, out var runtime);
            ActiveInstance = runtime;
            ActiveInstanceChanged?.Invoke();
        }

        public void RenameInstance(Guid id, string newName)
        {
            var data = InstanceDataList.FirstOrDefault(d => d.Id == id);
            if (data == null) return;

            data.DisplayName = newName;
            if (_runtimeInstances.TryGetValue(id, out var runtime))
            {
                runtime.DisplayName = newName;
            }
            Save();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(_storageDir))
                    Directory.CreateDirectory(_storageDir);

                var fileData = new InstancesFileData
                {
                    Instances = InstanceDataList.ToList(),
                    ActiveInstanceId = ActiveInstanceData?.Id
                };

                var json = JsonConvert.SerializeObject(fileData, Formatting.Indented);
                File.WriteAllText(_instancesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InstanceManager] Save failed: {ex.Message}");
            }
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(_instancesFilePath)) return;

                var json = File.ReadAllText(_instancesFilePath);
                var fileData = JsonConvert.DeserializeObject<InstancesFileData>(json);
                if (fileData == null) return;

                InstanceDataList.Clear();
                foreach (var runtime in _runtimeInstances.Values)
                    runtime.Dispose();
                _runtimeInstances.Clear();

                foreach (var data in fileData.Instances)
                {
                    InstanceDataList.Add(data);
                    var runtime = new ServerInstance(data);
                    _runtimeInstances[data.Id] = runtime;
                }

                if (fileData.ActiveInstanceId.HasValue)
                    SetActiveInstance(fileData.ActiveInstanceId.Value);
                else if (InstanceDataList.Count > 0)
                    SetActiveInstance(InstanceDataList[0].Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InstanceManager] Load failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Migrates from the old single-server AppSettings.ServerPath to the new
        /// multi-instance model. Called on first launch when instances.json doesn't exist.
        /// </summary>
        public void MigrateFromSingleServer(string serverPath)
        {
            var displayName = "Server";
            try
            {
                var settingsPath = Path.Combine(serverPath, "game_settings.txt");
                if (File.Exists(settingsPath))
                {
                    var settings = ConfigIO.ReadGameSettings(settingsPath);
                    if (!string.IsNullOrWhiteSpace(settings.ServerName))
                        displayName = settings.ServerName;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InstanceManager] Migration name lookup failed: {ex.Message}");
            }

            AddLocalInstance(displayName, serverPath);
        }

        public IReadOnlyDictionary<Guid, ServerInstance> GetRuntimeInstances() => _runtimeInstances;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "ServerInstanceManagerTests" -v minimal`
Expected: All 10 tests PASS

- [ ] **Step 5: Commit**

```bash
git add TabgInstaller.Gui/Services/ServerInstanceManager.cs TabgInstaller.Tests/Services/ServerInstanceManagerTests.cs
git commit -m "feat: add ServerInstanceManager with CRUD, persistence, and single-server migration"
```

---

## Task 9: Migrate DI Registration & MainWindow

Replace old singleton services with new instance-based architecture. Wire the sidebar.

**Files:**
- Modify: `TabgInstaller.Gui/App.xaml.cs`
- Modify: `TabgInstaller.Gui/MainWindow.xaml.cs`
- Modify: `TabgInstaller.Gui/MainWindow.xaml`

- [ ] **Step 1: Update DI registrations in App.xaml.cs**

In `TabgInstaller.Gui/App.xaml.cs`, replace lines 84-98 (the Infrastructure and Core services section) with:

```csharp
            // Infrastructure
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TabgInstaller");
            services.AddSingleton<IAppSettingsService, AppSettingsService>();
            // Register the canonical singleton so DI and non-DI callers share one instance.
            services.AddSingleton<ToastService>(_ => ToastService.Instance);
            services.AddSingleton<IToastService>(_ => ToastService.Instance);

            // Multi-server instance management (replaces IServerPathProvider + singleton IServerProcessService)
            services.AddSingleton<IServerInstanceManager>(sp => new ServerInstanceManager(settingsDir));
            services.AddSingleton<IActiveInstanceService, ActiveInstanceService>();
            services.AddSingleton<ICredentialStorageService>(sp => new CredentialStorageService(settingsDir));

            // Keep IServerPathProvider as a thin adapter for any code that still references it
            // during the migration period. It reads from ActiveInstanceService.
            services.AddSingleton<IServerPathProvider>(sp =>
            {
                var adapter = new ServerPathProvider();
                var active = sp.GetRequiredService<IActiveInstanceService>();
                active.PathChanged += () => adapter.SetPath(active.ServerPath);
                return adapter;
            });
            // IServerProcessService now proxies through ActiveInstanceService
            services.AddSingleton<IServerProcessService>(sp =>
                sp.GetRequiredService<IActiveInstanceService>().ProcessService);

            // Core services
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<ConfigValidationService>();
            services.AddTransient<IBackupService>(sp =>
                new BackupService(new Progress<string>(msg =>
                    Debug.WriteLine($"[Backup] {msg}"))));
            services.AddTransient<BepInExLoaderService>(sp =>
                new BepInExLoaderService(new Progress<string>(msg =>
                    Debug.WriteLine($"[BepInEx] {msg}"))));
```

Add these using statements at the top of App.xaml.cs if not present:

```csharp
using TabgInstaller.Gui.Model;
```

Note: Remove the old `KnownPlayersService` singleton registration — it will be re-added as-is after. Keep the rest of the ViewModels section unchanged.

- [ ] **Step 2: Update MainWindow.xaml with sidebar**

Replace the content of `TabgInstaller.Gui/MainWindow.xaml`:

```xml
<Window x:Class="TabgInstaller.Gui.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:tabs="clr-namespace:TabgInstaller.Gui.Tabs"
        xmlns:controls="clr-namespace:TabgInstaller.Gui.Controls"
        Title="TABG Manager" Height="800" Width="1200"
        Icon="pack://application:,,,/Assets/tabg-mod-manager-icon-256.png">

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="180"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- Server sidebar -->
        <controls:ServerListControl x:Name="ServerListSidebar" Grid.Column="0"/>

        <!-- Main content -->
        <Grid Grid.Column="1">
            <TabControl x:Name="MainTabs">
                <TabItem Header="Dashboard">
                    <tabs:DashboardPanel x:Name="DashboardTab"/>
                </TabItem>

                <TabItem Header="Server Mods">
                    <tabs:ServerModsPanel x:Name="ServerModsTab"/>
                </TabItem>

                <TabItem Header="Client Mods">
                    <tabs:ClientPanel x:Name="ClientModsTab"/>
                </TabItem>

                <TabItem Header="Config">
                    <tabs:ConfigPanel x:Name="ConfigTab"/>
                </TabItem>

                <TabItem Header="Console">
                    <tabs:ConsolePanel x:Name="ConsoleTab"/>
                </TabItem>

                <TabItem Header="Backups" x:Name="BackupsTabItem">
                    <tabs:BackupsPanel x:Name="BackupsTab"/>
                </TabItem>

                <TabItem Header="Reference">
                    <tabs:ReferencePanel x:Name="ReferenceTab"/>
                </TabItem>

                <TabItem Header="Settings">
                    <tabs:SettingsPanel x:Name="SettingsTab"/>
                </TabItem>
            </TabControl>

            <!-- Toast notification overlay -->
            <controls:ToastNotification x:Name="ToastControl" VerticalAlignment="Top" Panel.ZIndex="100"/>
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 3: Update MainWindow.xaml.cs**

Replace the content of `TabgInstaller.Gui/MainWindow.xaml.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using TabgInstaller.Gui.Windows;

namespace TabgInstaller.Gui
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _services;
        private readonly IAppSettingsService _appSettings;
        private readonly IServerInstanceManager _instanceManager;
        private readonly IActiveInstanceService _activeInstance;

        public MainWindow(
            IServiceProvider services,
            IAppSettingsService appSettings,
            IServerInstanceManager instanceManager,
            IActiveInstanceService activeInstance)
        {
            _services = services;
            _appSettings = appSettings;
            _instanceManager = instanceManager;
            _activeInstance = activeInstance;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize toast system
            var toast = _services.GetRequiredService<ToastService>();
            toast.Initialize((msg, type, dur) =>
                Dispatcher.Invoke(() => ToastControl.Show(msg, type, dur)));

            // Initialize navigation
            var nav = _services.GetRequiredService<INavigationService>() as NavigationService;
            nav?.Initialize(index => MainTabs.SelectedIndex = index);

            // Wire hard reset
            var navService = _services.GetRequiredService<INavigationService>();
            navService.HardResetRequested += () =>
            {
                // Stop all running servers
                if (_instanceManager is ServerInstanceManager mgr)
                {
                    foreach (var rt in mgr.GetRuntimeInstances().Values)
                    {
                        if (rt.IsRunning) rt.Stop();
                    }
                }
                RunSetupWizard();
            };

            // Run update check
            try
            {
                var updater = _services.GetRequiredService<IUpdateService>();
                var updateInfo = await updater.CheckForUpdateAsync();
                if (updateInfo != null)
                {
                    var updateSettings = _appSettings.Load();
                    if (updateInfo.TagName == updateSettings.SkippedUpdateVersion)
                    {
                        // Skipped — don't prompt
                    }
                    else
                    {
                        if (updateSettings.SkippedUpdateVersion != null)
                        {
                            updateSettings.SkippedUpdateVersion = null;
                            _appSettings.Save(updateSettings);
                        }

                        var current = UpdateService.GetCurrentVersion();
                        var dialog = new ChangelogWindow(current, updateInfo.Version,
                            updateInfo.ReleaseNotes, updateInfo.TagName);
                        dialog.Owner = this;

                        if (dialog.ShowDialog() == true)
                        {
                            Title = "TABG Manager — Updating...";
                            bool ok = await updater.ApplyUpdateAsync(updateInfo.DownloadUrl);
                            if (ok)
                            {
                                Application.Current.Shutdown();
                                return;
                            }
                            else
                            {
                                var toastSvc = _services.GetRequiredService<IToastService>();
                                toastSvc.Error("Update failed. You can download manually from GitHub.");
                                Title = "TABG Manager";
                            }
                        }
                        else if (dialog.SkippedVersion != null)
                        {
                            updateSettings.SkippedUpdateVersion = dialog.SkippedVersion;
                            _appSettings.Save(updateSettings);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Failed to check for updates: {ex.Message}");
            }

            // Check for migration or setup
            var settings = _appSettings.Load();
            var instancesExist = _instanceManager.InstanceDataList.Count > 0;

            if (!instancesExist)
            {
                // Try to load existing instances
                _instanceManager.Load();
                instancesExist = _instanceManager.InstanceDataList.Count > 0;
            }

            if (!instancesExist && settings.SetupCompleted &&
                !string.IsNullOrEmpty(settings.ServerPath) &&
                Directory.Exists(settings.ServerPath))
            {
                // Migrate from single-server
                (_instanceManager as ServerInstanceManager)?.MigrateFromSingleServer(settings.ServerPath);
            }
            else if (!instancesExist)
            {
                // No instances and no legacy path — run setup wizard
                RunSetupWizard();
                return;
            }

            // Initialize sidebar
            var serverListVm = _services.GetRequiredService<ServerListViewModel>();
            ServerListSidebar.DataContext = serverListVm;

            // Initialize panels for the active instance
            InitializeAllPanels();
        }

        private void RunSetupWizard()
        {
            this.Visibility = Visibility.Hidden;

            var wizard = new SetupWizardWindow(
                _services.GetRequiredService<IToastService>(),
                _appSettings);
            var result = wizard.ShowDialog();

            this.Visibility = Visibility.Visible;
            this.Activate();

            if (result == true && wizard.SetupCompleted)
            {
                var settings = _appSettings.Load();
                (_instanceManager as ServerInstanceManager)?.MigrateFromSingleServer(settings.ServerPath);

                var serverListVm = _services.GetRequiredService<ServerListViewModel>();
                ServerListSidebar.DataContext = serverListVm;

                InitializeAllPanels();
            }
            else
            {
                var settings = _appSettings.Load();
                if (!string.IsNullOrEmpty(settings.ServerPath) && Directory.Exists(settings.ServerPath))
                {
                    (_instanceManager as ServerInstanceManager)?.MigrateFromSingleServer(settings.ServerPath);
                    InitializeAllPanels();
                }
                else
                {
                    var toastService = _services.GetRequiredService<IToastService>();
                    toastService.Error("Setup was not completed. The app needs a server path to function.");
                    Application.Current.Shutdown();
                }
            }
        }

        // Called externally (e.g. InstallerPanel, PresetsGrid) when the server path changes.
        public void ReloadFromPath(string serverDir) => InitializeAllPanels();

        private void InitializeAllPanels()
        {
            var serverDir = _activeInstance.ServerPath;

            ConsoleTab.DataContext = _services.GetRequiredService<ConsolePanelViewModel>();
            DashboardTab.DataContext = _services.GetRequiredService<DashboardViewModel>();
            ConfigTab.DataContext = _services.GetRequiredService<ConfigViewModel>();
            ConfigTab.InitializeSubPanels(serverDir);
            ConfigTab.SetLoadoutEditorViewModel(_services.GetRequiredService<LoadoutEditorViewModel>());
            ConfigTab.AdminPanelControl.DataContext = _services.GetRequiredService<AdminPanelViewModel>();
            ConfigTab.MatchSettingsControl.SetViewModel(_services.GetRequiredService<MatchSettingsViewModel>());
            ConfigTab.RingSpawnsControl.SetViewModel(_services.GetRequiredService<RingSpawnsViewModel>());
            ConfigTab.ModSettingsControl.SetViewModel(_services.GetRequiredService<ModSettingsViewModel>());
            ConfigTab.PresetsGridControl.SetServerPath(serverDir);
            ServerModsTab.DataContext = _services.GetRequiredService<ServerModsViewModel>();

            var clientVm = _services.GetRequiredService<ClientPanelViewModel>();
            ClientModsTab.DataContext = clientVm;
            clientVm.Initialize();
            BackupsTab.DataContext = _services.GetRequiredService<BackupsPanelViewModel>();
            ReferenceTab.DataContext = _services.GetRequiredService<ReferencePanelViewModel>();
            SettingsTab.DataContext = _services.GetRequiredService<SettingsPanelViewModel>();
            SettingsTab.SuperSecretControl.DataContext = _services.GetRequiredService<SuperSecretSettingsViewModel>();

            MainTabs.SelectedIndex = 0;
        }
    }
}
```

- [ ] **Step 4: Add ServerListViewModel to DI in App.xaml.cs**

Add to the ViewModels section (after line 124 `services.AddTransient<LoadoutEditorViewModel>();`):

```csharp
            services.AddTransient<ServerListViewModel>();
```

- [ ] **Step 5: Run build to verify compilation**

Run: `dotnet build TabgInstaller.Gui -v minimal`
Expected: Build succeeds (ServerListControl and ServerListViewModel don't exist yet — create stubs)

Note: This step will fail until Task 10 creates the sidebar controls. If building incrementally, create minimal stubs first, or combine with Task 10.

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Gui/App.xaml.cs TabgInstaller.Gui/MainWindow.xaml TabgInstaller.Gui/MainWindow.xaml.cs
git commit -m "feat: migrate DI to instance-based architecture, add sidebar layout to MainWindow"
```

---

## Task 10: ServerListControl & ServerListViewModel (Sidebar)

The sidebar showing all server instances with status indicators.

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/ServerListViewModel.cs`
- Create: `TabgInstaller.Gui/Controls/ServerListControl.xaml`
- Create: `TabgInstaller.Gui/Controls/ServerListControl.xaml.cs`
- Test: `TabgInstaller.Tests/ViewModels/ServerListViewModelTests.cs`

- [ ] **Step 1: Write failing tests for ServerListViewModel**

Create `TabgInstaller.Tests/ViewModels/ServerListViewModelTests.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class ServerListViewModelTests
    {
        private readonly Mock<IServerInstanceManager> _manager = new();
        private readonly Mock<IToastService> _toast = new();

        public ServerListViewModelTests()
        {
            _manager.SetupGet(m => m.InstanceDataList)
                .Returns(new ObservableCollection<ServerInstanceData>());
        }

        private ServerListViewModel CreateSut() => new(_manager.Object, _toast.Object);

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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "ServerListViewModelTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create ServerListViewModel**

Create `TabgInstaller.Gui/ViewModels/ServerListViewModel.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class ServerListViewModel : ObservableObject
    {
        private readonly IServerInstanceManager _manager;
        private readonly IToastService _toast;

        public ObservableCollection<ServerInstanceData> Instances => _manager.InstanceDataList;

        [ObservableProperty] private ServerInstanceData? _selectedInstance;

        public ServerListViewModel(
            IServerInstanceManager manager,
            IToastService toast)
        {
            _manager = manager;
            _toast = toast;

            // Sync selection with manager's active instance
            _selectedInstance = _manager.ActiveInstanceData;
        }

        partial void OnSelectedInstanceChanged(ServerInstanceData? value)
        {
            if (value != null)
                _manager.SetActiveInstance(value.Id);
        }

        [RelayCommand]
        private void AddServer()
        {
            // Will be wired to AddServerDialog in Task 12
        }

        [RelayCommand]
        private void RemoveServer()
        {
            if (SelectedInstance == null) return;
            try
            {
                _manager.RemoveInstance(SelectedInstance.Id);
            }
            catch (InvalidOperationException)
            {
                _toast.Warning("Cannot remove the last server instance.");
            }
        }

        [RelayCommand]
        private void RenameServer(string newName)
        {
            if (SelectedInstance == null || string.IsNullOrWhiteSpace(newName)) return;
            _manager.RenameInstance(SelectedInstance.Id, newName.Trim());
        }
    }
}
```

- [ ] **Step 4: Create ServerListControl.xaml**

Create `TabgInstaller.Gui/Controls/ServerListControl.xaml`:

```xml
<UserControl x:Class="TabgInstaller.Gui.Controls.ServerListControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DesignWidth="180" d:DesignHeight="600">

    <UserControl.Resources>
        <Style x:Key="StatusDot" TargetType="Ellipse">
            <Setter Property="Width" Value="10"/>
            <Setter Property="Height" Value="10"/>
            <Setter Property="Margin" Value="0,0,8,0"/>
            <Setter Property="VerticalAlignment" Value="Center"/>
        </Style>
    </UserControl.Resources>

    <Grid Background="#F0F0F0">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Text="Servers" FontWeight="Bold" FontSize="14"
                   Margin="12,12,12,8"/>

        <ListBox Grid.Row="1"
                 ItemsSource="{Binding Instances}"
                 SelectedItem="{Binding SelectedInstance, Mode=TwoWay}"
                 BorderThickness="0"
                 Background="Transparent"
                 Margin="4,0">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal" Margin="8,6">
                        <Ellipse Style="{StaticResource StatusDot}">
                            <Ellipse.Fill>
                                <SolidColorBrush Color="Gray"/>
                            </Ellipse.Fill>
                        </Ellipse>
                        <TextBlock Text="{Binding DisplayName}"
                                   VerticalAlignment="Center"
                                   TextTrimming="CharacterEllipsis"
                                   MaxWidth="130"/>
                    </StackPanel>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <Button Grid.Row="2"
                Content="+ Add Server"
                Command="{Binding AddServerCommand}"
                Margin="8,4,8,8"
                Padding="8,6"/>
    </Grid>
</UserControl>
```

- [ ] **Step 5: Create ServerListControl.xaml.cs**

Create `TabgInstaller.Gui/Controls/ServerListControl.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace TabgInstaller.Gui.Controls
{
    public partial class ServerListControl : UserControl
    {
        public ServerListControl()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "ServerListViewModelTests" -v minimal`
Expected: All 3 tests PASS

- [ ] **Step 7: Run full build**

Run: `dotnet build TabgInstaller.Gui -v minimal`
Expected: Build succeeds

- [ ] **Step 8: Commit**

```bash
git add TabgInstaller.Gui/ViewModels/ServerListViewModel.cs TabgInstaller.Gui/Controls/ServerListControl.xaml TabgInstaller.Gui/Controls/ServerListControl.xaml.cs TabgInstaller.Tests/ViewModels/ServerListViewModelTests.cs
git commit -m "feat: add ServerListControl sidebar with ServerListViewModel"
```

---

## Task 11: Migrate Existing ViewModels from IServerPathProvider to IActiveInstanceService

All ViewModels that inject `IServerPathProvider` need to accept `IActiveInstanceService` instead. The DI adapter (Task 9) means this is optional for the first pass, but for ViewModels that also inject `IServerProcessService`, they MUST change to use the active instance's process service.

**Files:**
- Modify: `TabgInstaller.Gui/ViewModels/DashboardViewModel.cs`
- Modify: `TabgInstaller.Gui/ViewModels/ConsolePanelViewModel.cs`
- Modify: All ViewModel test files that mock `IServerPathProvider`

Note: Since the DI adapter in Task 9 keeps `IServerPathProvider` and `IServerProcessService` working via proxy, ViewModels that ONLY inject `IServerPathProvider` (not `IServerProcessService`) can remain unchanged for now. The critical ones to migrate are `DashboardViewModel` and `ConsolePanelViewModel` since they inject both `IServerPathProvider` AND `IServerProcessService`.

- [ ] **Step 1: Update DashboardViewModel**

In `TabgInstaller.Gui/ViewModels/DashboardViewModel.cs`, replace the field declarations and constructor:

Replace:
```csharp
        private readonly IServerProcessService _procSvc;
        private readonly IServerPathProvider _serverPathProvider;
        private readonly IAppSettingsService _appSettings;
        private readonly INavigationService _navigation;
        private readonly IToastService _toast;
        private Timer? _refreshTimer;

        [ObservableProperty] private string _previewText = "";
        [ObservableProperty] private string _startStopButtonText = "Start Server";
        [ObservableProperty] private string _serverPath = "";

        public bool IsServerRunning => _procSvc.IsRunning;

        public DashboardViewModel(
            IServerProcessService procSvc,
            IServerPathProvider serverPathProvider,
            IAppSettingsService appSettings,
            INavigationService navigation,
            IToastService toast)
        {
            _procSvc = procSvc;
            _serverPathProvider = serverPathProvider;
            _appSettings = appSettings;
            _navigation = navigation;
            _toast = toast;

            _serverPathProvider.PathChanged += OnServerPathChanged;
        }
```

With:
```csharp
        private readonly IActiveInstanceService _activeInstance;
        private readonly IAppSettingsService _appSettings;
        private readonly INavigationService _navigation;
        private readonly IToastService _toast;
        private Timer? _refreshTimer;

        [ObservableProperty] private string _previewText = "";
        [ObservableProperty] private string _startStopButtonText = "Start Server";
        [ObservableProperty] private string _serverPath = "";

        public bool IsServerRunning
        {
            get
            {
                try { return _activeInstance.ProcessService.IsRunning; }
                catch { return false; }
            }
        }

        public DashboardViewModel(
            IActiveInstanceService activeInstance,
            IAppSettingsService appSettings,
            INavigationService navigation,
            IToastService toast)
        {
            _activeInstance = activeInstance;
            _appSettings = appSettings;
            _navigation = navigation;
            _toast = toast;

            _activeInstance.PathChanged += OnServerPathChanged;
        }
```

Also update the methods that reference `_procSvc` and `_serverPathProvider`:

Replace `_serverPathProvider.ServerPath` with `_activeInstance.ServerPath` throughout the file.
Replace `_procSvc.` with `_activeInstance.ProcessService.` throughout the file.

Specifically update:
- `OnServerPathChanged()`: `ServerPath = _activeInstance.ServerPath;`
- `RefreshPreview()`: `PreviewText = _activeInstance.ProcessService.GetRecentText(20);` and `_activeInstance.ProcessService.IsRunning`
- `StartStop()`: `_activeInstance.ProcessService.IsRunning`, `_activeInstance.ProcessService.Stop()`, `_activeInstance.ProcessService.Start()`
- `OpenServerFolder()`: `_activeInstance.ServerPath`
- `OpenLogs()`: `_activeInstance.ServerPath`
- `OpenConfigs()`: `_activeInstance.ServerPath`

Add using: `using TabgInstaller.Gui.Services;`
Remove unused using: `using TabgInstaller.Core;` (if no longer needed)

- [ ] **Step 2: Update DashboardViewModelTests**

In `TabgInstaller.Tests/ViewModels/DashboardViewModelTests.cs`, replace the mock fields and CreateSut:

Replace:
```csharp
        private readonly Mock<IServerProcessService> _procSvc = new();
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IAppSettingsService> _appSettings = new();
        private readonly Mock<INavigationService> _navigation = new();
        private readonly Mock<IToastService> _toast = new();

        public DashboardViewModelTests()
        {
            _serverPath.SetupGet(s => s.ServerPath).Returns(@"C:\Server");
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            _procSvc.Setup(p => p.GetRecentText(It.IsAny<int>())).Returns("");
        }

        private DashboardViewModel CreateSut() =>
            new(_procSvc.Object, _serverPath.Object, _appSettings.Object,
                _navigation.Object, _toast.Object);
```

With:
```csharp
        private readonly Mock<IActiveInstanceService> _activeInstance = new();
        private readonly Mock<IServerProcessService> _procSvc = new();
        private readonly Mock<IAppSettingsService> _appSettings = new();
        private readonly Mock<INavigationService> _navigation = new();
        private readonly Mock<IToastService> _toast = new();

        public DashboardViewModelTests()
        {
            _activeInstance.SetupGet(a => a.ServerPath).Returns(@"C:\Server");
            _procSvc.SetupGet(p => p.IsRunning).Returns(false);
            _procSvc.Setup(p => p.GetRecentText(It.IsAny<int>())).Returns("");
            _activeInstance.SetupGet(a => a.ProcessService).Returns(_procSvc.Object);
        }

        private DashboardViewModel CreateSut() =>
            new(_activeInstance.Object, _appSettings.Object,
                _navigation.Object, _toast.Object);
```

Update test methods that use `_serverPath`:
- Replace `_serverPath.SetupGet(s => s.ServerPath)` with `_activeInstance.SetupGet(a => a.ServerPath)`
- Replace `_serverPath.Raise(s => s.PathChanged += null)` with `_activeInstance.Raise(a => a.PathChanged += null)`

Add using: `using TabgInstaller.Gui.Services;`

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "DashboardViewModelTests" -v minimal`
Expected: All tests PASS

- [ ] **Step 4: Apply same pattern to ConsolePanelViewModel**

Same migration: replace `IServerProcessService` + `IServerPathProvider` constructor parameters with `IActiveInstanceService`. Update all references from `_procSvc` to `_activeInstance.ProcessService` and `_serverPathProvider` to `_activeInstance`. Update corresponding test file.

- [ ] **Step 5: Run full test suite**

Run: `dotnet test TabgInstaller.Tests -v minimal`
Expected: All tests PASS

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Gui/ViewModels/DashboardViewModel.cs TabgInstaller.Gui/ViewModels/ConsolePanelViewModel.cs TabgInstaller.Tests/ViewModels/DashboardViewModelTests.cs TabgInstaller.Tests/ViewModels/ConsolePanelViewModelTests.cs
git commit -m "refactor: migrate DashboardViewModel and ConsolePanelViewModel to IActiveInstanceService"
```

---

## Task 12: Dashboard Health Cards

Add the all-servers health overview to the Dashboard panel.

**Files:**
- Create: `TabgInstaller.Gui/Controls/HealthCardControl.xaml`
- Create: `TabgInstaller.Gui/Controls/HealthCardControl.xaml.cs`
- Modify: `TabgInstaller.Gui/ViewModels/DashboardViewModel.cs` — add health cards collection
- Modify: `TabgInstaller.Gui/Tabs/DashboardPanel.xaml` — add health cards section

- [ ] **Step 1: Create HealthCardControl**

Create `TabgInstaller.Gui/Controls/HealthCardControl.xaml`:

```xml
<UserControl x:Class="TabgInstaller.Gui.Controls.HealthCardControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Margin="4">
    <Border BorderBrush="#DDD" BorderThickness="1" CornerRadius="4" Padding="12,8">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <!-- Status dot + Name -->
            <Ellipse Grid.Row="0" Grid.Column="0" Width="10" Height="10"
                     Fill="{Binding StatusColor}" Margin="0,0,8,0" VerticalAlignment="Center"/>
            <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding DisplayName}"
                       FontWeight="SemiBold" VerticalAlignment="Center"/>
            <TextBlock Grid.Row="0" Grid.Column="2" Text="{Binding PlayerCountText}"
                       VerticalAlignment="Center" Foreground="Gray"/>

            <!-- Details row (only when running) -->
            <StackPanel Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="3"
                        Orientation="Horizontal" Margin="18,4,0,0"
                        Visibility="{Binding IsRunning, Converter={StaticResource BoolToVisibility}}">
                <TextBlock Text="{Binding UptimeText}" Foreground="Gray" Margin="0,0,16,0"/>
                <TextBlock Text="{Binding MemoryText}" Foreground="Gray" Margin="0,0,16,0"/>
                <TextBlock Text="{Binding JoinCodeText}" Foreground="Gray"/>
            </StackPanel>
        </Grid>
    </Border>
</UserControl>
```

Create `TabgInstaller.Gui/Controls/HealthCardControl.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace TabgInstaller.Gui.Controls
{
    public partial class HealthCardControl : UserControl
    {
        public HealthCardControl()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 2: Add health card data to DashboardViewModel**

Add to `DashboardViewModel` after the existing observable properties:

```csharp
        [ObservableProperty] private ObservableCollection<ServerHealthCardData> _healthCards = new();
```

Add a nested data class (or create in a separate file — here inline for simplicity):

```csharp
    public partial class ServerHealthCardData : ObservableObject
    {
        [ObservableProperty] private string _displayName = "";
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private string _playerCountText = "";
        [ObservableProperty] private string _uptimeText = "";
        [ObservableProperty] private string _memoryText = "";
        [ObservableProperty] private string _joinCodeText = "";
        [ObservableProperty] private string _statusColor = "Gray";
    }
```

Add `IServerInstanceManager` to the constructor and a method to refresh health cards:

```csharp
        private readonly IServerInstanceManager _instanceManager;
```

Wire it into the constructor alongside the existing parameters. Then add the refresh method:

```csharp
        private void RefreshHealthCards()
        {
            var mgr = _instanceManager as ServerInstanceManager;
            if (mgr == null) return;

            var runtimes = mgr.GetRuntimeInstances();
            // Sync health cards with current instance list
            HealthCards.Clear();
            foreach (var data in _instanceManager.InstanceDataList)
            {
                if (runtimes.TryGetValue(data.Id, out var instance))
                {
                    HealthCards.Add(new ServerHealthCardData
                    {
                        DisplayName = data.DisplayName,
                        IsRunning = instance.IsRunning,
                        PlayerCountText = instance.IsRunning
                            ? $"{instance.HealthMonitor.PlayerCount}/{70} players" : "",
                        UptimeText = instance.IsRunning
                            ? $"Uptime: {instance.HealthMonitor.Uptime:hh\\:mm}" : "",
                        MemoryText = instance.IsRunning
                            ? $"RAM: {instance.HealthMonitor.MemoryUsageMb} MB" : "",
                        JoinCodeText = instance.HealthMonitor.JoinCode != null
                            ? $"Join: {instance.HealthMonitor.JoinCode}" : "",
                        StatusColor = instance.HealthMonitor.Status switch
                        {
                            ServerHealthStatus.Running => "Green",
                            ServerHealthStatus.Stopped => "Gray",
                            ServerHealthStatus.Crashed => "Red",
                            ServerHealthStatus.Restarting => "Orange",
                            ServerHealthStatus.Watchdog => "Orange",
                            _ => "Gray"
                        }
                    });
                }
                else
                {
                    HealthCards.Add(new ServerHealthCardData
                    {
                        DisplayName = data.DisplayName,
                        StatusColor = "Gray"
                    });
                }
            }
        }
```

Call `RefreshHealthCards()` from the existing `RefreshPreview()` method and from `OnServerPathChanged()`.

- [ ] **Step 3: Run build to verify compilation**

Run: `dotnet build TabgInstaller.Gui -v minimal`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add TabgInstaller.Gui/Controls/HealthCardControl.xaml TabgInstaller.Gui/Controls/HealthCardControl.xaml.cs TabgInstaller.Gui/ViewModels/DashboardViewModel.cs
git commit -m "feat: add HealthCardControl and health card data model to Dashboard"
```

---

## Task 13: Add Server Dialog

Dialog for adding new local or remote server instances.

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/AddServerDialogViewModel.cs`
- Create: `TabgInstaller.Gui/Windows/AddServerDialog.xaml`
- Create: `TabgInstaller.Gui/Windows/AddServerDialog.xaml.cs`

- [ ] **Step 1: Create AddServerDialogViewModel**

Create `TabgInstaller.Gui/ViewModels/AddServerDialogViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class AddServerDialogViewModel : ObservableObject
    {
        [ObservableProperty] private string _displayName = "";
        [ObservableProperty] private string _serverPath = "";
        [ObservableProperty] private bool _isLocal = true;
        [ObservableProperty] private bool _isRemote;

        // Remote fields
        [ObservableProperty] private string _host = "";
        [ObservableProperty] private int _port = 22;
        [ObservableProperty] private string _username = "";
        [ObservableProperty] private bool _usePassword = true;
        [ObservableProperty] private bool _usePrivateKey;
        [ObservableProperty] private string _password = "";
        [ObservableProperty] private string _privateKeyPath = "";
        [ObservableProperty] private string _remoteServerPath = "";
        [ObservableProperty] private bool _useScreen = true;
        [ObservableProperty] private bool _useSystemd;

        [ObservableProperty] private string _validationError = "";

        public bool DialogResult { get; private set; }
        public Action? CloseAction { get; set; }

        [RelayCommand]
        private void BrowseServerPath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "TABG Server|TABG.exe",
                Title = "Select TABG Server"
            };
            if (dialog.ShowDialog() == true)
            {
                ServerPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? "";
                if (string.IsNullOrEmpty(DisplayName))
                    DisplayName = "New Server";
            }
        }

        [RelayCommand]
        private void BrowsePrivateKey()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Private Key|*.pem;*.ppk;*id_rsa;*id_ed25519|All Files|*.*",
                Title = "Select SSH Private Key"
            };
            if (dialog.ShowDialog() == true)
                PrivateKeyPath = dialog.FileName;
        }

        [RelayCommand]
        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                ValidationError = "Display name is required.";
                return;
            }

            if (IsLocal)
            {
                if (string.IsNullOrWhiteSpace(ServerPath))
                {
                    ValidationError = "Server path is required.";
                    return;
                }
                if (!System.IO.File.Exists(System.IO.Path.Combine(ServerPath, "TABG.exe")))
                {
                    ValidationError = "TABG.exe not found in the selected directory.";
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Host))
                {
                    ValidationError = "Hostname is required.";
                    return;
                }
                if (string.IsNullOrWhiteSpace(Username))
                {
                    ValidationError = "Username is required.";
                    return;
                }
                if (string.IsNullOrWhiteSpace(RemoteServerPath))
                {
                    ValidationError = "Remote server path is required.";
                    return;
                }
            }

            DialogResult = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            CloseAction?.Invoke();
        }

        public RemoteConnectionConfig BuildRemoteConfig() => new()
        {
            Host = Host,
            Port = Port,
            Username = Username,
            AuthMethod = UsePassword ? SshAuthMethod.Password : SshAuthMethod.PrivateKey,
            PrivateKeyPath = PrivateKeyPath,
            RemoteServerPath = RemoteServerPath,
            ProcessMode = UseScreen ? RemoteProcessMode.Screen : RemoteProcessMode.Systemd
        };
    }
}
```

- [ ] **Step 2: Create AddServerDialog.xaml**

Create `TabgInstaller.Gui/Windows/AddServerDialog.xaml`:

```xml
<Window x:Class="TabgInstaller.Gui.Windows.AddServerDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Add Server" Width="480" Height="500"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Display Name -->
        <StackPanel Grid.Row="0" Margin="0,0,0,12">
            <TextBlock Text="Display Name" FontWeight="SemiBold" Margin="0,0,0,4"/>
            <TextBox Text="{Binding DisplayName, UpdateSourceTrigger=PropertyChanged}"/>
        </StackPanel>

        <!-- Type selector -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,12">
            <RadioButton Content="Local Server" IsChecked="{Binding IsLocal}" Margin="0,0,16,0"/>
            <RadioButton Content="Remote Server (SSH)" IsChecked="{Binding IsRemote}"/>
        </StackPanel>

        <!-- Local panel -->
        <StackPanel Grid.Row="2" Visibility="{Binding IsLocal, Converter={StaticResource BoolToVisibility}}">
            <TextBlock Text="Server Directory" FontWeight="SemiBold" Margin="0,0,0,4"/>
            <DockPanel>
                <Button Content="Browse..." DockPanel.Dock="Right" Command="{Binding BrowseServerPathCommand}"
                        Margin="8,0,0,0" Padding="12,4"/>
                <TextBox Text="{Binding ServerPath, UpdateSourceTrigger=PropertyChanged}"/>
            </DockPanel>
        </StackPanel>

        <!-- Remote panel -->
        <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto"
                      Visibility="{Binding IsRemote, Converter={StaticResource BoolToVisibility}}">
            <StackPanel>
                <TextBlock Text="Host" FontWeight="SemiBold" Margin="0,0,0,4"/>
                <TextBox Text="{Binding Host, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,0,8"/>

                <Grid Margin="0,0,0,8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="80"/>
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0" Margin="0,0,8,0">
                        <TextBlock Text="Username" FontWeight="SemiBold" Margin="0,0,0,4"/>
                        <TextBox Text="{Binding Username, UpdateSourceTrigger=PropertyChanged}"/>
                    </StackPanel>
                    <StackPanel Grid.Column="1">
                        <TextBlock Text="Port" FontWeight="SemiBold" Margin="0,0,0,4"/>
                        <TextBox Text="{Binding Port, UpdateSourceTrigger=PropertyChanged}"/>
                    </StackPanel>
                </Grid>

                <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
                    <RadioButton Content="Password" IsChecked="{Binding UsePassword}" Margin="0,0,16,0"/>
                    <RadioButton Content="Private Key" IsChecked="{Binding UsePrivateKey}"/>
                </StackPanel>

                <PasswordBox x:Name="PasswordField" Margin="0,0,0,8"
                             Visibility="{Binding UsePassword, Converter={StaticResource BoolToVisibility}}"/>

                <DockPanel Margin="0,0,0,8"
                           Visibility="{Binding UsePrivateKey, Converter={StaticResource BoolToVisibility}}">
                    <Button Content="Browse..." DockPanel.Dock="Right" Command="{Binding BrowsePrivateKeyCommand}"
                            Margin="8,0,0,0" Padding="12,4"/>
                    <TextBox Text="{Binding PrivateKeyPath, UpdateSourceTrigger=PropertyChanged}"/>
                </DockPanel>

                <TextBlock Text="Remote Server Path" FontWeight="SemiBold" Margin="0,0,0,4"/>
                <TextBox Text="{Binding RemoteServerPath, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,0,8"/>

                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="Process Mode:" FontWeight="SemiBold" Margin="0,0,8,0"
                               VerticalAlignment="Center"/>
                    <RadioButton Content="Screen" IsChecked="{Binding UseScreen}" Margin="0,0,16,0"/>
                    <RadioButton Content="Systemd" IsChecked="{Binding UseSystemd}"/>
                </StackPanel>
            </StackPanel>
        </ScrollViewer>

        <!-- Validation error -->
        <TextBlock Grid.Row="3" Text="{Binding ValidationError}" Foreground="Red"
                   Margin="0,8,0,0" TextWrapping="Wrap"/>

        <!-- Buttons -->
        <StackPanel Grid.Row="4" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="Cancel" Command="{Binding CancelCommand}" Padding="16,6" Margin="0,0,8,0"/>
            <Button Content="Add Server" Command="{Binding ConfirmCommand}" Padding="16,6"
                    FontWeight="SemiBold"/>
        </StackPanel>
    </Grid>

    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVisibility"/>
    </Window.Resources>
</Window>
```

- [ ] **Step 3: Create AddServerDialog.xaml.cs**

Create `TabgInstaller.Gui/Windows/AddServerDialog.xaml.cs`:

```csharp
using System.Windows;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Windows
{
    public partial class AddServerDialog : Window
    {
        public AddServerDialogViewModel ViewModel { get; }

        public AddServerDialog()
        {
            ViewModel = new AddServerDialogViewModel();
            ViewModel.CloseAction = () => Close();
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 4: Wire AddServer command in ServerListViewModel**

Update the `AddServer()` method in `ServerListViewModel.cs`:

```csharp
        [RelayCommand]
        private void AddServer()
        {
            var dialog = new Windows.AddServerDialog();
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();

            if (dialog.ViewModel.DialogResult)
            {
                var vm = dialog.ViewModel;
                if (vm.IsLocal)
                {
                    _manager.AddLocalInstance(vm.DisplayName, vm.ServerPath);
                }
                else
                {
                    var config = vm.BuildRemoteConfig();
                    _manager.AddRemoteInstance(vm.DisplayName, config);

                    // Store credentials
                    // Will be wired in SSH task
                }
                _toast.Success($"Added server: {vm.DisplayName}");
            }
        }
```

Add usings to `ServerListViewModel.cs`:
```csharp
using System.Windows;
```

- [ ] **Step 5: Run build to verify compilation**

Run: `dotnet build TabgInstaller.Gui -v minimal`
Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Gui/ViewModels/AddServerDialogViewModel.cs TabgInstaller.Gui/Windows/AddServerDialog.xaml TabgInstaller.Gui/Windows/AddServerDialog.xaml.cs TabgInstaller.Gui/ViewModels/ServerListViewModel.cs
git commit -m "feat: add AddServerDialog for creating local and remote server instances"
```

---

## Task 14: SSH.NET Integration — RemoteSshService

**Files:**
- Modify: `TabgInstaller.Gui/TabgInstaller.Gui.csproj` — add SSH.NET package
- Create: `TabgInstaller.Core/Services/IRemoteSshService.cs`
- Create: `TabgInstaller.Core/Services/RemoteSshService.cs`
- Test: `TabgInstaller.Tests/Services/RemoteProcessServiceTests.cs`

- [ ] **Step 1: Add SSH.NET NuGet package**

Run: `dotnet add TabgInstaller.Gui/TabgInstaller.Gui.csproj package SSH.NET`

- [ ] **Step 2: Create IRemoteSshService interface**

Create `TabgInstaller.Core/Services/IRemoteSshService.cs`:

```csharp
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
```

- [ ] **Step 3: Create RemoteSshService implementation**

Create `TabgInstaller.Core/Services/RemoteSshService.cs`:

```csharp
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
```

- [ ] **Step 4: Create RemoteProcessService**

Create `TabgInstaller.Core/Services/RemoteProcessService.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    /// <summary>
    /// IServerProcessService implementation that manages a TABG server
    /// over SSH via RemoteSshService.
    /// </summary>
    public class RemoteProcessService : IServerProcessService, IDisposable
    {
        private readonly IRemoteSshService _ssh;
        private readonly RemoteConnectionConfig _config;
        private CancellationTokenSource? _tailCts;
        private readonly object _logLock = new();
        private const int MaxLogEntries = 50_000;

        public bool IsRunning { get; private set; }
        public int ProcessId => 0; // Remote PID not tracked locally
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public event Action<LogEntry>? LogEntryReceived;
        public event Action<string>? OutputReceived;
        public event Action<int>? ProcessExited;

        public RemoteProcessService(IRemoteSshService ssh, RemoteConnectionConfig config)
        {
            _ssh = ssh;
            _config = config;
        }

        public bool Start(string additionalArgs = "-batchmode -nographics -nolog")
        {
            if (IsRunning) return false;

            try
            {
                var command = _config.ProcessMode switch
                {
                    RemoteProcessMode.Screen =>
                        $"screen -dmS tabg {_config.RemoteServerPath}/TABG.exe {additionalArgs}",
                    RemoteProcessMode.Systemd =>
                        "systemctl start tabg-server",
                    _ => throw new InvalidOperationException($"Unknown process mode: {_config.ProcessMode}")
                };

                _ssh.ExecuteCommandAsync(command).GetAwaiter().GetResult();
                IsRunning = true;

                // Start tailing logs
                StartLogTail();
                return true;
            }
            catch (Exception ex)
            {
                var entry = LogLineParser.Parse($"[ERROR] Failed to start remote server: {ex.Message}");
                AddLogEntry(entry);
                return false;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _tailCts?.Cancel();
            try
            {
                var command = _config.ProcessMode switch
                {
                    RemoteProcessMode.Screen => "screen -S tabg -X quit",
                    RemoteProcessMode.Systemd => "systemctl stop tabg-server",
                    _ => "kill $(pgrep TABG)"
                };

                _ssh.ExecuteCommandAsync(command).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RemoteProcess] Stop failed: {ex.Message}");
            }

            IsRunning = false;
            ProcessExited?.Invoke(0);
        }

        public void ClearEntries()
        {
            lock (_logLock) { LogEntries.Clear(); }
        }

        public void AddEntry(LogEntry entry) => AddLogEntry(entry);

        public string GetRecentText(int maxLines = 20)
        {
            lock (_logLock)
            {
                var count = LogEntries.Count;
                if (count == 0) return "";
                var start = Math.Max(0, count - maxLines);
                var sb = new System.Text.StringBuilder();
                for (int i = start; i < count; i++)
                {
                    if (sb.Length > 0) sb.Append(Environment.NewLine);
                    sb.Append(LogEntries[i].RawText);
                }
                return sb.ToString();
            }
        }

        public void RegisterCollectionSynchronization(Action<object, object> register)
        {
            register(LogEntries, _logLock);
        }

        private void StartLogTail()
        {
            _tailCts?.Cancel();
            _tailCts = new CancellationTokenSource();
            var ct = _tailCts.Token;

            var logPath = _config.ProcessMode switch
            {
                RemoteProcessMode.Screen => $"{_config.RemoteServerPath}/output_log.txt",
                RemoteProcessMode.Systemd => "", // journalctl handled differently
                _ => $"{_config.RemoteServerPath}/output_log.txt"
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    if (_config.ProcessMode == RemoteProcessMode.Systemd)
                    {
                        // Use journalctl for systemd
                        var output = await _ssh.ExecuteCommandAsync(
                            "journalctl -u tabg-server -f --no-pager", ct);
                        // This won't work well as a one-shot — need streaming
                        // Fall back to tail approach
                    }

                    await _ssh.StartTailAsync(logPath, line =>
                    {
                        OutputReceived?.Invoke(line);
                        var entry = LogLineParser.Parse(line);
                        AddLogEntry(entry);
                    }, ct);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RemoteProcess] Log tail failed: {ex.Message}");
                }
            }, ct);
        }

        private void AddLogEntry(LogEntry entry)
        {
            try
            {
                lock (_logLock)
                {
                    while (LogEntries.Count >= MaxLogEntries)
                        LogEntries.RemoveAt(0);
                    LogEntries.Add(entry);
                }
                LogEntryReceived?.Invoke(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RemoteProcess] AddLogEntry failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _tailCts?.Cancel();
            _tailCts?.Dispose();
        }
    }
}
```

- [ ] **Step 5: Write tests for command construction**

Create `TabgInstaller.Tests/Services/RemoteProcessServiceTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class RemoteProcessServiceTests
    {
        private readonly Mock<IRemoteSshService> _ssh = new();

        [Fact]
        public void Start_ScreenMode_SendsScreenCommand()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Screen
            };
            _ssh.SetupGet(s => s.IsConnected).Returns(true);
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();

            _ssh.Verify(s => s.ExecuteCommandAsync(
                It.Is<string>(cmd => cmd.Contains("screen -dmS tabg") && cmd.Contains("/opt/tabg/TABG.exe")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Start_SystemdMode_SendsSystemctlCommand()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Systemd
            };
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();

            _ssh.Verify(s => s.ExecuteCommandAsync(
                "systemctl start tabg-server",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Stop_ScreenMode_SendsQuitCommand()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Screen
            };
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start(); // need to be running first
            sut.Stop();

            _ssh.Verify(s => s.ExecuteCommandAsync(
                "screen -S tabg -X quit",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void IsRunning_AfterStart_IsTrue()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Screen
            };
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();
            sut.IsRunning.Should().BeTrue();
        }

        [Fact]
        public void IsRunning_AfterStop_IsFalse()
        {
            var config = new RemoteConnectionConfig
            {
                RemoteServerPath = "/opt/tabg",
                ProcessMode = RemoteProcessMode.Screen
            };
            _ssh.Setup(s => s.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            var sut = new RemoteProcessService(_ssh.Object, config);
            sut.Start();
            sut.Stop();
            sut.IsRunning.Should().BeFalse();
        }
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test TabgInstaller.Tests --filter "RemoteProcessServiceTests" -v minimal`
Expected: All 5 tests PASS

- [ ] **Step 7: Commit**

```bash
git add TabgInstaller.Gui/TabgInstaller.Gui.csproj TabgInstaller.Core/Services/IRemoteSshService.cs TabgInstaller.Core/Services/RemoteSshService.cs TabgInstaller.Core/Services/RemoteProcessService.cs TabgInstaller.Tests/Services/RemoteProcessServiceTests.cs
git commit -m "feat: add SSH.NET integration with RemoteSshService and RemoteProcessService"
```

---

## Task 15: RemoteServerInstance

The remote variant of ServerInstance that uses SSH instead of local process management.

**Files:**
- Create: `TabgInstaller.Gui/Model/RemoteServerInstance.cs`
- Modify: `TabgInstaller.Gui/Services/ServerInstanceManager.cs` — wire RemoteServerInstance into AddRemoteInstance

- [ ] **Step 1: Create RemoteServerInstance**

Create `TabgInstaller.Gui/Model/RemoteServerInstance.cs`:

```csharp
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
```

- [ ] **Step 2: Update ServerInstanceManager.AddRemoteInstance**

In `TabgInstaller.Gui/Services/ServerInstanceManager.cs`, replace the `AddRemoteInstance` method:

```csharp
        public IServerInstanceContext AddRemoteInstance(string displayName, RemoteConnectionConfig config)
        {
            var data = new ServerInstanceData
            {
                DisplayName = displayName,
                ServerPath = config.RemoteServerPath,
                InstanceType = ServerInstanceType.Remote,
                RemoteConfig = config
            };

            InstanceDataList.Add(data);
            var runtime = new RemoteServerInstance(data);
            _runtimeInstances[data.Id] = null!; // RemoteServerInstance tracked separately
            _remoteInstances[data.Id] = runtime;

            SetActiveInstance(data.Id);
            Save();
            return runtime;
        }
```

Actually, this gets complicated with two dictionaries. Simpler approach — store IServerInstanceContext in a separate dictionary:

Add a new field to `ServerInstanceManager`:

```csharp
        private readonly Dictionary<Guid, IServerInstanceContext> _contextInstances = new();
```

Then update all methods to use `_contextInstances` instead of `_runtimeInstances` for the IServerInstanceContext lookup. Update `AddLocalInstance` to store in `_contextInstances`, `AddRemoteInstance` to store in `_contextInstances`, `SetActiveInstance` to read from `_contextInstances`, `RemoveInstance` to clean up from `_contextInstances`.

- [ ] **Step 3: Run build to verify compilation**

Run: `dotnet build TabgInstaller.Gui -v minimal`
Expected: Build succeeds

- [ ] **Step 4: Run all tests**

Run: `dotnet test TabgInstaller.Tests -v minimal`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add TabgInstaller.Gui/Model/RemoteServerInstance.cs TabgInstaller.Gui/Services/ServerInstanceManager.cs
git commit -m "feat: add RemoteServerInstance with SSH-backed process and health management"
```

---

## Task 16: Wire Credential Storage into Add Server Flow

Store SSH credentials when adding a remote server, retrieve when connecting.

**Files:**
- Modify: `TabgInstaller.Gui/ViewModels/ServerListViewModel.cs`
- Modify: `TabgInstaller.Gui/Services/ServerInstanceManager.cs`

- [ ] **Step 1: Add ICredentialStorageService to ServerListViewModel**

Update `ServerListViewModel` constructor to accept `ICredentialStorageService`:

```csharp
        private readonly IServerInstanceManager _manager;
        private readonly IToastService _toast;
        private readonly ICredentialStorageService _credentials;

        public ServerListViewModel(
            IServerInstanceManager manager,
            IToastService toast,
            ICredentialStorageService credentials)
        {
            _manager = manager;
            _toast = toast;
            _credentials = credentials;
            _selectedInstance = _manager.ActiveInstanceData;
        }
```

Update the `AddServer()` command to store credentials:

```csharp
        [RelayCommand]
        private void AddServer()
        {
            var dialog = new Windows.AddServerDialog();
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();

            if (dialog.ViewModel.DialogResult)
            {
                var vm = dialog.ViewModel;
                if (vm.IsLocal)
                {
                    _manager.AddLocalInstance(vm.DisplayName, vm.ServerPath);
                }
                else
                {
                    var config = vm.BuildRemoteConfig();
                    var instance = _manager.AddRemoteInstance(vm.DisplayName, config);

                    // Store credentials via DPAPI
                    var instanceData = _manager.ActiveInstanceData;
                    if (instanceData != null)
                    {
                        if (vm.UsePassword && !string.IsNullOrEmpty(vm.Password))
                            _credentials.Store(instanceData.Id, "password", vm.Password);
                        // Private key passphrase could be stored similarly if provided
                    }
                }
                _toast.Success($"Added server: {vm.DisplayName}");
            }
        }
```

- [ ] **Step 2: Update DI registration for ServerListViewModel**

The DI already resolves `ICredentialStorageService` (registered in Task 9). No changes needed — the constructor injection will work automatically.

- [ ] **Step 3: Update ServerListViewModelTests**

Add the mock for `ICredentialStorageService`:

```csharp
        private readonly Mock<IServerInstanceManager> _manager = new();
        private readonly Mock<IToastService> _toast = new();
        private readonly Mock<ICredentialStorageService> _credentials = new();

        private ServerListViewModel CreateSut() => new(_manager.Object, _toast.Object, _credentials.Object);
```

- [ ] **Step 4: Run tests**

Run: `dotnet test TabgInstaller.Tests --filter "ServerListViewModelTests" -v minimal`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add TabgInstaller.Gui/ViewModels/ServerListViewModel.cs TabgInstaller.Tests/ViewModels/ServerListViewModelTests.cs
git commit -m "feat: wire credential storage into add-server flow for SSH passwords"
```

---

## Task 17: Full Test Suite Verification & Remaining ViewModel Migration

Ensure all existing tests still pass after the architecture change, and migrate remaining ViewModels that need it.

**Files:**
- Modify: Various ViewModel test files (update mocks for IServerPathProvider adapter)

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test TabgInstaller.Tests -v normal`

Identify any failures. The DI adapter from Task 9 should keep most things working, but some tests may need mock updates.

- [ ] **Step 2: Fix any failing tests**

For each failing test:
- If it mocks `IServerPathProvider` and the ViewModel still injects `IServerPathProvider` (via the DI adapter), the mock should still work
- If the ViewModel was migrated to `IActiveInstanceService`, update the test mocks accordingly

- [ ] **Step 3: Run full test suite again**

Run: `dotnet test TabgInstaller.Tests -v minimal`
Expected: All tests PASS

- [ ] **Step 4: Run full build**

Run: `dotnet build -v minimal`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix: update remaining test mocks for Phase 4 architecture compatibility"
```

---

## Task 18: Integration Smoke Test

Verify the app launches, sidebar appears, migration works, and basic server operations still function.

- [ ] **Step 1: Run the application**

Run: `dotnet run --project TabgInstaller.Gui`

- [ ] **Step 2: Verify migration**

Expected behavior:
- App launches without errors
- Sidebar appears on the left with the existing server listed (migrated from settings.json)
- Server name in sidebar matches the ServerName from game_settings.txt
- All tabs still accessible and functional
- Dashboard shows the server entry

- [ ] **Step 3: Verify add/remove**

- Click "+ Add Server" in sidebar
- Add a local server (if you have a second TABG installation) or test the dialog opens and validates
- Cancel returns without adding
- Right-click existing server → Rename works

- [ ] **Step 4: Document any issues found**

If issues are found, create fixes and commit:

```bash
git add -A
git commit -m "fix: address integration issues found during smoke testing"
```

---

## Summary of Tasks

| Task | Description | Dependencies |
|------|-------------|--------------|
| 1 | Data models (ServerInstanceData, ServerEvent, enums) | None |
| 2 | ServerEventParser (player join/leave/join-code parsing) | Task 1 |
| 3 | Add ProcessExited event to ServerProcessService | None |
| 4 | HealthMonitorService (player tracking, status, restart state) | Tasks 1, 3 |
| 5 | CredentialStorageService (DPAPI encryption) | Task 1 |
| 6 | IActiveInstanceService & ActiveInstanceService | Tasks 1, 4 |
| 7 | ServerInstance runtime model (owns process + health) | Tasks 1, 2, 3, 4 |
| 8 | ServerInstanceManager (CRUD, persistence, migration) | Tasks 1, 7 |
| 9 | Migrate DI registration & MainWindow | Tasks 6, 8 |
| 10 | ServerListControl sidebar + ServerListViewModel | Task 9 |
| 11 | Migrate DashboardViewModel & ConsolePanelViewModel | Task 6 |
| 12 | Dashboard health cards | Tasks 10, 11 |
| 13 | Add Server dialog | Task 10 |
| 14 | SSH.NET integration (RemoteSshService, RemoteProcessService) | Tasks 1, 3, 4 |
| 15 | RemoteServerInstance | Tasks 7, 14 |
| 16 | Wire credential storage into add-server flow | Tasks 5, 13, 15 |
| 17 | Full test suite verification | All above |
| 18 | Integration smoke test | All above |
