# Phase 4: Power User Suite — Design Specification

**Date:** 2026-04-09
**Status:** Approved
**Builds on:** Phase 3 MVVM Migration (completed 2026-04-09)

## Overview

Phase 4 transforms the TABG Server Installer from a single-server tool into a multi-server management platform. Three interconnected features: multi-server management, server health monitoring, and remote server management via SSH.

## Architecture: ServerInstance as Root Context

The central architectural change: replace the single-server assumption (`IServerPathProvider` with one path) with a `ServerInstance` model that encapsulates everything about one server. An `IServerInstanceManager` holds a collection of instances, one of which is "active" and displayed in the existing panel tabs.

Existing ViewModels migrate from `IServerPathProvider` to `IActiveInstanceService`, which proxies the active instance's path and services using the same `PathChanged` event signature. This minimizes changes to the 15 already-migrated ViewModels.

---

## 1. ServerInstance Model & Instance Manager

### ServerInstance

A class encapsulating everything about one server:

- `Guid Id` — unique, stable across sessions
- `string DisplayName` — user-editable label ("BR Server", "Gun Game", etc.)
- `string ServerPath` — path to this server's TABG installation directory
- `ServerInstanceType InstanceType` — enum: `Local | Remote`
- `ServerProcessService` (owned) — its own process manager instance
- `HealthMonitorService` (owned) — its own health/uptime tracker
- `AutoRestartConfig` — per-instance crash recovery settings
- Observable properties: `IsRunning`, `PlayerCount`, `Uptime`, `HealthStatus`

Each local instance creates its own `ServerProcessService` in its constructor. No more global singleton process service.

### IServerInstanceManager

Registered as a singleton. Replaces `IServerPathProvider` as the primary server context.

```
ObservableCollection<ServerInstance> Instances
ServerInstance? ActiveInstance
event Action? ActiveInstanceChanged

AddInstance(string name, string path) → ServerInstance
AddRemoteInstance(string name, RemoteConnectionConfig config) → RemoteServerInstance
RemoveInstance(Guid id)
Save()  — persist to %LOCALAPPDATA%\TabgInstaller\instances.json
Load()  — restore from disk
```

### IActiveInstanceService

Thin proxy so existing ViewModels don't need to know about multi-server:

```
string ServerPath          → ActiveInstance.ServerPath
IServerProcessService ProcessService → ActiveInstance.ServerProcessService
IHealthMonitorService HealthMonitor  → ActiveInstance.HealthMonitor
event Action? PathChanged  → fires on instance switch
```

Existing ViewModels that inject `IServerPathProvider` migrate to `IActiveInstanceService` with minimal changes — same `PathChanged` event, same path access pattern.

### Migration from Single-Server

On first launch after update: if `settings.json` has a `ServerPath` but no `instances.json` exists, the manager creates one `ServerInstance` from the existing path, names it from the server's `game_settings.txt` `ServerName` value, and writes `instances.json`. Silent, no user prompt.

### Instance Persistence (instances.json)

```json
{
  "instances": [
    {
      "id": "a1b2c3d4-...",
      "displayName": "BR Server",
      "serverPath": "D:\\steam\\steamapps\\common\\TABGServer1",
      "instanceType": "Local",
      "autoRestart": {
        "enabled": true,
        "maxRetries": 3,
        "initialBackoffSeconds": 5,
        "watchdogIntervalSeconds": 300,
        "stabilityThresholdSeconds": 30
      }
    },
    {
      "id": "e5f6g7h8-...",
      "displayName": "VPS Deathmatch",
      "instanceType": "Remote",
      "remoteConfig": {
        "host": "192.168.1.100",
        "port": 22,
        "username": "tabg",
        "authMethod": "PrivateKey",
        "privateKeyPath": "C:\\Users\\Jonas\\.ssh\\id_rsa",
        "remoteServerPath": "/opt/tabg-server",
        "processMode": "Screen"
      },
      "autoRestart": {
        "enabled": true,
        "maxRetries": 3,
        "initialBackoffSeconds": 5,
        "watchdogIntervalSeconds": 300,
        "stabilityThresholdSeconds": 30
      }
    }
  ],
  "activeInstanceId": "a1b2c3d4-..."
}
```

Credentials (passwords, key passphrases) are NOT stored in this file — they go to the encrypted credential store.

---

## 2. Multi-Server Sidebar & Workspace Switching

### MainWindow Layout Change

The current full-width `TabControl` is wrapped in a horizontal split:

```
┌──────────────────────────────────────────────────┐
│  [App Title Bar]                                 │
├────────┬─────────────────────────────────────────┤
│        │                                         │
│ Server │   Existing TabControl                   │
│ List   │   (Dashboard, Config, Console, etc.)    │
│        │                                         │
│ ────── │   All panels scoped to the selected     │
│ BR     │   server in the sidebar                 │
│ ●      │                                         │
│ ────── │                                         │
│ DM     │                                         │
│ ○      │                                         │
│ ────── │                                         │
│ GunGm  │                                         │
│ ○      │                                         │
│        │                                         │
│        │                                         │
│ [+ Add]│                                         │
├────────┴─────────────────────────────────────────┤
│  [Toast notifications overlay]                   │
└──────────────────────────────────────────────────┘
```

### Sidebar Items

Each item displays:
- Display name
- Status indicator: green dot (running), red dot (stopped), yellow dot (crashed/restarting), blue dot (remote connected, server stopped)
- Player count badge when running (e.g., "3/70")
- Right-click context menu: Rename, Start/Stop, Remove

### ServerListViewModel

Owns the sidebar UI logic:

- `ObservableCollection<ServerInstance> Instances` — bound to sidebar ListView
- `ServerInstance SelectedInstance` — two-way bound to selection
- Commands: `AddServerCommand`, `RemoveServerCommand`, `RenameServerCommand`, `StartStopCommand`
- On `SelectedInstance` change → sets `IServerInstanceManager.ActiveInstance` → triggers cascade

### Switching Behavior

Click server in sidebar → `ActiveInstance` changes → `IActiveInstanceService` fires `PathChanged` → all ViewModels reload state. This reuses the existing initialization logic every ViewModel already implements for `PathChanged`.

### Add Server Flow

Click [+ Add] → dialog with two options:
1. **Local Server** — enter display name, browse to existing TABG server directory (or launch setup wizard to install new one)
2. **Remote Server** — enter display name, SSH connection details (see Section 4)

Creates a `ServerInstance` or `RemoteServerInstance`, adds to manager, auto-selects it.

### Remove Server

Right-click → Remove → confirmation dialog. Removes from instance list only — does NOT delete files on disk. Cannot remove the last server.

---

## 3. Server Health Monitoring

### IHealthMonitorService

Per-instance service, created and started when the instance's server process starts:

```
bool IsAlive
int PlayerCount
int MaxPlayers
TimeSpan Uptime
long MemoryUsageMb
ServerHealthStatus Status  — enum: Stopped | Running | Crashed | Restarting | Watchdog
string? JoinCode
ObservableCollection<ConnectedPlayer> ConnectedPlayers

event Action? ServerCrashed
event Action? ServerRecovered
```

### ConnectedPlayer

```
string Name
string EpicId
DateTime JoinedAt
```

### Player Tracking via Log Parsing

Extends the existing `LogLineParser` with new structured event patterns parsed from real TABG server output:

| Log Pattern | Action |
|---|---|
| `Player: {idx} Name: {name} : Assigning EPic ID: {id}` | Add to player list, increment count |
| `Spawned player object: {name}` | Secondary join confirmation |
| `Player left: {name}` | Remove from player list, decrement count |
| `Client: {idx} disconnected from server` | Secondary disconnect signal |
| `Host - Got join code: {code}` | Store join code, mark server as ready |

The parser produces structured `ServerEvent` objects that `HealthMonitorService` consumes to update its observable properties. The parser is separate from the health monitor — it transforms log lines into events, the monitor maintains state.

### Dashboard Health Widget

The Dashboard panel gets a health summary showing ALL server instances (not just the active one — this is the cross-server overview):

```
┌─ BR Server ──────────────────────┐
│ ● Running    3/70 players        │
│ Uptime: 2h 14m    RAM: 842 MB   │
│ Join Code: FWJTKK                │
└──────────────────────────────────┘
┌─ Deathmatch ─────────────────────┐
│ ○ Stopped                        │
└──────────────────────────────────┘
```

Each card is clickable — switches to that instance in the sidebar.

### Memory Monitoring

For local instances: poll `Process.WorkingSet64` on a timer (every 5 seconds). For remote instances: `ssh: ps -o rss -p <pid>`.

### Auto-Restart & Crash Recovery

When `ServerProcessService` detects unexpected process exit (exit code ≠ 0, or process disappeared while `IsRunning` was true):

1. `Status` → `Crashed`
2. Toast: "{DisplayName} crashed. Restarting (attempt 1/{MaxRetries})..."
3. Wait `BackoffDelay` (configurable, default 5s, doubles each retry)
4. Restart the process
5. If alive for `StabilityThreshold` (default 30s) → reset retry counter, `Status` → `Running`, toast: "{DisplayName} recovered"
6. If crashes again → increment retry count, repeat from step 2
7. After exhausting `MaxRetries` (default 3) → `Status` → `Watchdog`
8. Watchdog mode: retry every `WatchdogInterval` (default 5 min) indefinitely
9. Toast: "{DisplayName} entered watchdog mode. Retrying every 5 minutes."
10. If watchdog attempt succeeds → back to `Running`, reset everything

User can manually stop at any time to exit the restart loop.

### AutoRestartConfig (per-instance)

```
bool Enabled (default: true)
int MaxRetries (default: 3)
TimeSpan InitialBackoff (default: 5 seconds)
TimeSpan WatchdogInterval (default: 5 minutes)
TimeSpan StabilityThreshold (default: 30 seconds)
```

Configurable in the Settings panel scoped to each server instance.

---

## 4. Remote Server Management

### RemoteServerInstance

Subclass of `ServerInstance` with `InstanceType = Remote`. Instead of owning a local `ServerProcessService`, it owns a `RemoteSshService`.

### RemoteSshService

Wraps SSH.NET library (`Renci.SshNet`, NuGet package):

```
Connect() / Disconnect() / bool IsConnected
StartServer(string args) — run TABG.exe on remote machine
StopServer() — kill signal or systemd stop
bool IsServerRunning() — check remote process status
TailLog(Action<string> onLine) — stream log output via SSH channel
string ExecuteCommand(string cmd) — general purpose
UploadFile(string localPath, string remotePath)
DownloadFile(string remotePath, string localPath)
```

### Remote Process Management Modes

Two modes, configurable per-instance. Remote servers are assumed to be Linux-based (standard for VPS/dedicated hosting). All commands target Linux. Windows remote servers are out of scope for initial implementation.

**Screen mode (default):**
- Start: `screen -dmS tabg ./TABG.exe -batchmode -nographics -nolog`
- Stop: `screen -S tabg -X quit` then fallback to `kill`
- Check: `screen -ls | grep tabg`
- Logs: `tail -f` on server log file

**Systemd mode:**
- Start: `systemctl start tabg-server`
- Stop: `systemctl stop tabg-server`
- Check: `systemctl is-active tabg-server`
- Logs: `journalctl -u tabg-server -f`

### How Remote Panels Work

`IActiveInstanceService` abstracts local vs remote. The same panels work for both:

| Operation | Local Implementation | Remote Implementation |
|---|---|---|
| Start server | `Process.Start()` | `ssh: screen -dmS tabg ./TABG.exe` |
| Stop server | `Process.Kill()` | `ssh: screen -S tabg -X quit` |
| Read console | `Process.StandardOutput` | `ssh: tail -f` on log file |
| Read config file | `File.ReadAllText(path)` | SFTP download to temp, read locally |
| Write config file | `File.WriteAllText(path)` | Write to temp, SFTP upload |
| Check alive | `!Process.HasExited` | `ssh: pgrep TABG` |
| Memory usage | `Process.WorkingSet64` | `ssh: ps -o rss -p <pid>` |

Config editing for remote servers: download config via SFTP to a temp file, edit locally using existing config panels, upload on save. The ViewModel doesn't know the difference.

### Connection Manager (Add Remote Server Dialog)

Fields:
- Display name
- Hostname / IP
- SSH port (default: 22)
- Username
- Auth method: Password or Private Key file (browse to `.pem`/`.ppk`)
- Remote TABG server path (where installed on remote machine)
- Process mode: Screen (default) or Systemd
- "Test Connection" button — verifies SSH connects, path exists, and TABG.exe is found

### Credential Storage

SSH passwords and private key passphrases encrypted at rest using DPAPI (`System.Security.Cryptography.ProtectedData`, already referenced in Core .csproj):

**`ICredentialStorageService`:**
```
Store(Guid instanceId, string credentialType, string value)
string? Retrieve(Guid instanceId, string credentialType)
Remove(Guid instanceId)
```

Implementation:
- Encrypt via `ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)`
- Decrypt via `ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser)`
- Stored in `%LOCALAPPDATA%\TabgInstaller\credentials.dat` as JSON dictionary of `{instanceId}_{type}` → base64-encoded encrypted blob
- Tied to Windows user account — cannot be decrypted by another user or on another machine
- Private key files referenced by path only — not copied, only passphrases stored

### Connection Lifecycle

- SSH connection established lazily when user selects a remote instance in the sidebar
- Auto-reconnect with exponential backoff if connection drops (max 3 attempts, then manual retry)
- Sidebar status: green (connected + running), blue (connected + stopped), red (disconnected), yellow (crashed/restarting)
- Disconnect when switching away from a remote instance (configurable — option to keep alive in background)
- Health monitoring works identically — `HealthMonitorService` gets data from SSH commands instead of local process APIs
- Log streaming via persistent SSH channel — same `LogEntry` parsing as local

---

## 5. New NuGet Dependencies

| Package | Purpose |
|---|---|
| SSH.NET (`Renci.SshNet`) | SSH/SFTP client for remote server management |

No other new dependencies. CommunityToolkit.Mvvm, DI, DPAPI already in place.

---

## 6. DI Registration Changes

```
// Remove
services.AddSingleton<IServerPathProvider, ServerPathProvider>();
services.AddSingleton<IServerProcessService, ServerProcessService>();

// Add
services.AddSingleton<IServerInstanceManager, ServerInstanceManager>();
services.AddSingleton<IActiveInstanceService, ActiveInstanceService>();
services.AddSingleton<ICredentialStorageService, CredentialStorageService>();

// ServerProcessService no longer registered globally — each ServerInstance creates its own
// HealthMonitorService — each ServerInstance creates its own
```

ViewModels change their constructor injection from `IServerPathProvider` to `IActiveInstanceService`. The `PathChanged` event contract is preserved.

---

## 7. Testing Strategy

- **ServerInstance:** Unit test creation, state management, auto-restart logic
- **ServerInstanceManager:** Unit test CRUD, persistence, migration from single-server
- **ActiveInstanceService:** Unit test proxy behavior, event forwarding on instance switch
- **HealthMonitorService:** Unit test player tracking, status transitions, crash detection, retry logic with backoff
- **Log parser extensions:** Unit test new player join/leave/join-code patterns against real log samples
- **CredentialStorageService:** Unit test encrypt/decrypt round-trip (DPAPI available in test environment)
- **RemoteSshService:** Integration tests with mock SSH server, unit tests for command construction
- **Existing ViewModel tests:** Verify they still pass after migrating from `IServerPathProvider` to `IActiveInstanceService`

---

## 8. Error Handling

- **SSH connection failure:** Toast with error details, sidebar shows red status, retry button in sidebar context menu
- **SSH timeout:** Configurable timeout (default 10s for connect, 30s for commands), toast on timeout
- **Remote process won't start:** Toast with SSH command output for debugging
- **Config upload/download failure:** Toast, local temp file preserved for manual recovery
- **Instance path invalid:** Detected on selection, toast warning, option to reconfigure path
- **Credential decryption failure:** Prompt for re-entry (can happen if Windows user profile changes), don't crash
- **Migration failure:** Fall back to empty instance list, prompt user to add a server manually
