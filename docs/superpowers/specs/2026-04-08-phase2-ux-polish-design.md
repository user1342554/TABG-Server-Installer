# Phase 2: UX Polish — Design Spec

**Date:** 2026-04-08
**Scope:** Config validation warnings, log viewer with filtering, changelog UI in auto-updater
**Target:** TabgInstaller.Gui + TabgInstaller.Core

---

## Overview

Three independent features that improve user visibility into what's happening and catch mistakes before they cause silent failures:

1. **Config validation warnings** — inline yellow banners next to settings that conflict or are nonsensical
2. **Log viewer with filtering** — replaces the raw TextBox console with severity coloring, search, filtering, and export
3. **Changelog UI** — "What's New" dialog showing GitHub release notes before the user confirms an update

---

## Feature 1: Config Validation Warnings

### Problem

Settings are auto-saved to `game_settings.txt` without validation. Users set `KillsToWin=50` while `GameMode=BattleRoyale` and wonder why kills-to-win doesn't work. The server silently ignores inapplicable settings. The only hint today is a tooltip with range info — no active warnings.

### Design

#### Validation Rule Engine

New class: `TabgInstaller.Core/Services/ConfigValidationService.cs`

Holds a list of `ValidationRule` objects. Each rule has:

- **AffectedProperty** (`string`) — the setting name the warning attaches to (e.g., `"KillsToWin"`)
- **Evaluate** (`Func<Dictionary<string, SettingPropertyVM>, string?>`) — receives all current properties by name, returns a warning message or null
- **Severity** (`ValidationSeverity.Warning`) — always Warning (yellow) for this phase; Error (red) reserved for future use

Public API:

```csharp
public Dictionary<string, List<string>> Validate(
    Dictionary<string, SettingPropertyVM> allProperties)
```

Returns a map of property name to list of warning strings. Properties with no warnings are absent from the dictionary.

#### Validation Rules

| ID | Rule | Trigger condition | Warning text |
|----|------|-------------------|-------------|
| R1 | Range violation | Any numeric value outside Knowledge JSON `range` or `NumericMinimum`/`NumericMaximum` | "Value {x} is outside the valid range ({min}–{max})" |
| R2 | KillsToWin ignored | `KillsToWin` ≠ 20 (default) AND `GameMode` ≠ `Brawl` | "KillsToWin has no effect unless GameMode is Brawl" |
| R3 | RoundsToWin ignored | `RoundsToWin` is set AND `GameMode` ≠ `Bomb` | "RoundsToWin has no effect unless GameMode is Bomb" |
| R4 | BombTime ignored | `BombTime` ≠ 30.0 AND `GameMode` ≠ `Bomb` | "BombTime has no effect unless GameMode is Bomb" |
| R5 | Ring settings ignored | Any ring setting (RingSizes, RingSpeeds, BaseRingTime, TimeBeforeFirstRing) changed from default AND `GameMode` ≠ `BattleRoyale` | "Ring settings have no effect unless GameMode is BattleRoyale" |
| R6 | NoRing + ring tuning | `NoRing=true` AND any ring size/speed/time value differs from default | "Ring tuning values are ignored when NoRing is enabled" |
| R7 | MaxPlayers ceiling | `MaxPlayers` > 253 | "Server supports a maximum of 253 players" |
| R8 | PlayersToStart > MaxPlayers | `PlayersToStart` > `MaxPlayers` | "PlayersToStart exceeds MaxPlayers — game will never start" |
| R9 | MinPlayersToForceStart > MaxPlayers | `MinPlayersToForceStart` > `MaxPlayers` | "MinPlayersToForceStart exceeds MaxPlayers" |
| R10 | ForceStartTime without toggle | `ForceStartTime` ≠ 200.0 AND `UseTimedForceStart=false` | "ForceStartTime is ignored when UseTimedForceStart is disabled" |
| R11 | GroupsToStart ignored | `GroupsToStart` ≠ 10 AND `GameMode` ≠ `Brawl` | "GroupsToStart has no effect unless GameMode is Brawl" |
| R12 | LAN + Relay conflict | `LAN=true` AND `Relay=true` | "Relay is typically disabled for LAN servers" |
| R13 | SpawnBots in BR | `SpawnBots` > 0 AND `GameMode` = `BattleRoyale` | "Bots in BattleRoyale mode may cause instability" |

Rules are data-driven. Adding a new rule means adding an entry to the rules list — no UI changes required.

#### SettingPropertyVM Changes

New properties:

```csharp
public ObservableCollection<string> ValidationWarnings { get; }  // bound in XAML
public bool HasWarnings => ValidationWarnings.Count > 0;          // drives visibility
```

New method:

```csharp
public void SetWarnings(List<string> warnings)  // replaces collection contents, fires PropertyChanged
```

#### GameSettingsDynamicViewModel Integration

On any `SettingPropertyVM.PropertyChanged` event (already subscribed for GameMode watching):

1. Call `ConfigValidationService.Validate(allProperties)`
2. Distribute warnings: for each property, call `SetWarnings(warnings)` or `SetWarnings(empty)` to clear
3. This runs synchronously on the UI thread — validation is cheap (dozen rules, simple comparisons)

Validation runs on every property change, instantly. Unrelated to the auto-save debounce timer.

#### UI Rendering (GameSettingsGrid.xaml)

Below each setting's control row, add:

```xml
<ItemsControl ItemsSource="{Binding ValidationWarnings}"
              Visibility="{Binding HasWarnings, Converter={StaticResource BoolToVisibilityConverter}}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Border Background="#FFF9E6" BorderBrush="#D4A017" BorderThickness="0,0,0,0"
                    Padding="8,4" Margin="0,2,0,0" CornerRadius="3">
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="⚠ " Foreground="#8A6D00" FontWeight="Bold"/>
                    <TextBlock Text="{Binding}" Foreground="#8A6D00" TextWrapping="Wrap"/>
                </StackPanel>
            </Border>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

Styling: yellow background (`#FFF9E6`), dark amber text (`#8A6D00`), compact (one line height per warning). Collapsed when no warnings.

---

## Feature 2: Log Viewer with Filtering

### Problem

The Console tab is a raw `TextBox` appending stdout lines. No coloring, no search, no filtering, no export. Users diagnosing server issues can't find errors in a wall of text, and sharing logs in Discord means copy-pasting everything.

### Design

#### Data Model

New class: `TabgInstaller.Core/Model/LogEntry.cs`

```csharp
public class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogSeverity Severity { get; init; }
    public string RawText { get; init; }
    public string Message { get; init; }  // RawText with parsed prefix stripped
}

public enum LogSeverity { Info, Warning, Error }
```

#### Log Line Parser

New static class: `TabgInstaller.Core/Services/LogLineParser.cs`

```csharp
public static LogEntry Parse(string rawLine, bool isStderr = false, DateTime? timestamp = null)
```

The `isStderr` flag causes the parser to default to `Error` severity for stderr-sourced lines (before applying regex checks, so an explicit `[INFO]` prefix on stderr still classifies as Info).

Severity detection via ordered checks:

1. **Error** — matches any of (case-insensitive): `\[error\]`, `exception`, `nullreference`, `stacktrace`, `fatal`, line starts with `ERROR:`, or line originates from stderr
2. **Warning** — matches any of: `\[warn`, `warning`, line starts with `WARNING:`
3. **Info** — everything else

Intentionally loose. False positives (a warning-colored info line) are acceptable; missed errors are not.

`Message` field: if a recognized prefix like `[INFO]` or `[ERROR]` is found, it's stripped from the display text. Otherwise `Message` equals `RawText`.

#### Storage in ServerProcessService

New members:

```csharp
public ObservableCollection<LogEntry> LogEntries { get; }  // capped at 50,000
public event Action<LogEntry>? LogEntryReceived;
```

On each `OutputDataReceived`:
1. Parse line via `LogLineParser.Parse(line)`
2. Add to `LogEntries` (if count > 50,000, remove oldest)
3. Fire `LogEntryReceived`
4. Continue firing existing `OutputReceived` string event (backward compat for DashboardPanel)

Stderr lines: same pipeline, but `LogLineParser` receives a flag to default them to `Error` severity.

#### ConsolePanel.xaml — New Layout

**Row 0 — Toolbar** (horizontal StackPanel):

| Element | Behavior |
|---------|----------|
| Start Server button | Same as today |
| Stop Server button | Same as today |
| Clear Console button | Clears `LogEntries` collection |
| Quick-save & Restart button | Same as today |
| Separator | Visual divider |
| `ℹ` toggle button | Filters Info lines in/out. Active (included) by default |
| `⚠` toggle button | Filters Warning lines in/out. Active by default |
| `✖` toggle button | Filters Error lines in/out. Active by default |
| Search TextBox | Placeholder "Search logs…", filters as-you-type (300ms debounce) |
| Export button (💾) | Opens SaveFileDialog, writes filtered entries to .txt |

**Row 1 — Log list**:

`ListBox` with `VirtualizingStackPanel.IsVirtualizing="True"` and `ScrollUnit="Pixel"`, bound to a `CollectionViewSource` over `LogEntries`.

Each item rendered via `DataTemplate`:

```
[HH:mm:ss]  message text here
```

- Timestamp: dim gray (`#888`), monospace (Consolas)
- Info lines: default foreground color
- Warning lines: amber/orange text (`#D48806`)
- Error lines: red text (`#CF1322`)
- Font: Consolas throughout, matching current console style

**Auto-scroll behavior**: scroll to bottom on new entries UNLESS user has scrolled up. Detect via `ScrollViewer.VerticalOffset < ScrollViewer.ScrollableHeight - threshold`. When user scrolls back to bottom, re-engage auto-scroll.

**Row 2 — Command input**: unchanged (disabled stdin with explanation message).

#### Filtering

`CollectionViewSource.Filter` predicate composes two checks (AND):

1. **Severity**: entry's severity matches an active toggle (if Warning toggle is off, skip Warning entries)
2. **Search text**: if non-empty, `RawText.Contains(searchText, StringComparison.OrdinalIgnoreCase)`

Toggling a severity button or changing search text calls `CollectionViewSource.View.Refresh()`.

#### Export

Writes currently visible (filtered) entries to a text file.

Format per line:
```
[2026-04-08 14:32:01] [INFO] Player connected: SteamID 76561198...
[2026-04-08 14:32:05] [ERROR] NullReferenceException in GameManager.Update()
```

`SaveFileDialog` defaults to filename `TABG-Server-Log-{yyyy-MM-dd-HHmmss}.txt`, filter `*.txt`.

#### Dashboard Integration

No changes. `DashboardPanel` continues using `GetRecentOutput(20)` from the string-based event path.

---

## Feature 3: Changelog UI in Auto-Updater

### Problem

The updater downloads silently after a `MessageBox.Show()` confirmation. Users don't know what changed. Builds distrust — "what did this update just do to my server?"

### Design

#### DTO Extension

In `UpdateService.cs`, add to `GitHubReleaseDto`:

```csharp
[JsonPropertyName("body")]
public string? Body { get; set; }
```

The GitHub API already returns this field — no extra API call needed.

Extend `CheckForUpdateAsync` return to include the body alongside existing tag name, version, and download URL. Concrete approach: change the return type from a tuple to a small record:

```csharp
public record UpdateInfo(
    string TagName,
    Version Version,
    string DownloadUrl,
    string? ReleaseNotes);
```

#### ChangelogWindow

New file: `TabgInstaller.Gui/Windows/ChangelogWindow.xaml` + code-behind.

Modal dialog (`Window.ShowDialog()`), replaces the current `MessageBox.Show()`.

**Layout:**

```
┌─────────────────────────────────────────┐
│  ⬆ Update Available                     │
│  v3.2.0  →  v4.0.0                      │
├─────────────────────────────────────────┤
│                                         │
│  ## What's New                          │
│                                         │
│  • Fixed server crash on map load       │
│  • Added support for custom loadouts    │
│  • Improved auto-save reliability       │
│                                         │
│  (scrollable if long)                   │
│                                         │
├─────────────────────────────────────────┤
│  [Update Now]  [Skip Version]  [Later]  │
└─────────────────────────────────────────┘
```

- **Window size**: ~450x400, resizable, centered on parent
- **Header**: app icon + "Update Available" title + version transition line
- **Body**: `ScrollViewer` containing a `StackPanel` of generated XAML elements
- **Footer**: three buttons in a right-aligned `StackPanel`

#### Markdown-to-XAML Converter

New static class: `TabgInstaller.Gui/Converters/MarkdownRenderer.cs`

```csharp
public static IEnumerable<UIElement> RenderMarkdown(string markdown)
```

Handles these patterns (covers 95% of GitHub release notes):

| Markdown | XAML output |
|----------|-------------|
| `# Heading` | `TextBlock` — FontSize 18, Bold, margin above |
| `## Heading` | `TextBlock` — FontSize 15, Bold, margin above |
| `- item` / `* item` | `TextBlock` with "  • " prefix, TextWrapping Wrap |
| `**bold**` | `Bold` inline within a `TextBlock` |
| `` `inline code` `` | `Run` with Consolas font and `#F0F0F0` background |
| ```` ```code block``` ```` | Read-only `TextBox`, Consolas font, `#F5F5F5` background, border |
| Blank line | Small margin spacer |
| Anything else | `TextBlock` with `TextWrapping="Wrap"` (plain text fallback) |

This is a simple line-by-line regex converter, not a full markdown parser. Unrecognized syntax renders as plain text — never crashes.

#### Button Behaviors

| Button | Action |
|--------|--------|
| **Update Now** | Sets `DialogResult = true`, caller proceeds with `ApplyUpdateAsync` |
| **Skip This Version** | Saves skipped version tag to `AppSettingsService`, sets `DialogResult = false` |
| **Remind Me Later** | Sets `DialogResult = false` (no persistence — prompts again next launch) |

#### "Skip This Version" Persistence

`AppSettingsService` gets a new property:

```csharp
public string? SkippedUpdateVersion { get; set; }
```

Persisted to `%LOCALAPPDATA%\TabgInstaller\settings.json` (existing settings file).

On launch, after `CheckForUpdateAsync` returns an update:
- If `update.TagName == settings.SkippedUpdateVersion` → suppress dialog, don't prompt
- If a newer version beyond the skipped one appears → clear `SkippedUpdateVersion`, show dialog

#### Update Flow Integration

In `App.xaml.cs` (or `MainWindow.xaml.cs`), replace:

```csharp
// Before:
var result = MessageBox.Show($"New version available! ...");
if (result == MessageBoxResult.Yes)
    await updater.ApplyUpdateAsync(url);

// After:
if (updateInfo.TagName == settings.SkippedUpdateVersion)
    return;  // user chose to skip this version

var dialog = new ChangelogWindow(
    currentVersion: updater.GetCurrentVersion(),
    newVersion: updateInfo.Version,
    releaseNotes: updateInfo.ReleaseNotes,
    tagName: updateInfo.TagName);

if (dialog.ShowDialog() == true)
    await updater.ApplyUpdateAsync(updateInfo.DownloadUrl);
```

#### Fallback

If `Body` is null or empty (release published without notes), the body area displays:

> "No release notes available for this version."

The dialog still shows. The user can still update.

---

## Files Changed / Created

### New Files

| File | Purpose |
|------|---------|
| `Core/Services/ConfigValidationService.cs` | Validation rule engine |
| `Core/Model/LogEntry.cs` | Log entry data model + LogSeverity enum |
| `Core/Services/LogLineParser.cs` | Severity detection from raw log lines |
| `Gui/Windows/ChangelogWindow.xaml` + `.cs` | Update changelog dialog |
| `Gui/Converters/MarkdownRenderer.cs` | Basic markdown-to-XAML converter |

### Modified Files

| File | Changes |
|------|---------|
| `Gui/ViewModels/SettingPropertyVM.cs` | Add `ValidationWarnings`, `HasWarnings`, `SetWarnings()` |
| `Gui/ViewModels/GameSettingsDynamicViewModel.cs` | Wire up `ConfigValidationService` on property changes |
| `Gui/Tabs/GameSettingsGrid.xaml` | Add warning `ItemsControl` below each setting row |
| `Core/Services/ServerProcessService.cs` | Add `LogEntries` collection, `LogEntryReceived` event, parse pipeline |
| `Gui/Tabs/ConsolePanel.xaml` + `.cs` | Replace TextBox with virtualized ListBox, add toolbar |
| `Core/Services/UpdateService.cs` | Add `Body` to DTO, return `UpdateInfo` record |
| `Gui/App.xaml.cs` or `Gui/MainWindow.xaml.cs` | Replace MessageBox with ChangelogWindow |
| `Gui/Services/AppSettingsService.cs` | Add `SkippedUpdateVersion` property |

---

## Out of Scope

- Error-level severity (red, blocking) for validation — future phase
- Config tab badge showing warning count — future enhancement
- Dashboard panel colored log preview — future enhancement
- Full markdown rendering (tables, images, links) — only common patterns supported
- RCON / stdin command input — unrelated to this phase
