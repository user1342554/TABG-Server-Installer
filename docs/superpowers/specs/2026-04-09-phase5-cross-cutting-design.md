# Phase 5: Cross-cutting — Global Reach

Design spec for localization, accessibility, and crash reporting across the TABG Server Installer WPF application.

## Context

The app uses .NET 8 WPF with CommunityToolkit.Mvvm and Microsoft.Extensions.Hosting DI. There are 19 ViewModels, 17 XAML panels, 4 windows, and 3 controls — all with hardcoded English strings. No localization system, no centralized theme/resource dictionaries (all styling inline), and basic exception handling that logs to `startup.log` and shows a MessageBox. Octokit is already referenced in Core for GitHub API operations.

---

## 1. Localization / i18n

### Resource File Structure

Three `.resx` file groups in `TabgInstaller.Gui/Resources/`:

| File | Purpose | Example keys |
|------|---------|-------------|
| `Strings.resx` / `Strings.de.resx` | General UI labels, headers, buttons | `Dashboard`, `StartServer`, `StopServer`, `Settings` |
| `Messages.resx` / `Messages.de.resx` | Toast messages, dialog text, status text | `BackupCreatedSuccess`, `NoServerDirectory`, `ConfirmDelete` |
| `Tooltips.resx` / `Tooltips.de.resx` | Tooltip and help text | `ServerPathTooltip`, `HardResetTooltip` |

Default (no suffix) = English. `.de` = German. Community contributors add new languages by copying the three base files and adding their locale suffix (e.g., `Strings.fr.resx`).

### XAML Binding Pattern

```xml
<!-- Before -->
<Button Content="Start Server" />

<!-- After -->
<Button Content="{x:Static res:Strings.StartServer}" />
```

Namespace declaration at the top of each XAML file:
```xml
xmlns:res="clr-namespace:TabgInstaller.Gui.Resources"
```

### ViewModel String Pattern

```csharp
// Before
_toastService.ShowSuccess("Backup created successfully!");

// After
_toastService.ShowSuccess(Messages.BackupCreatedSuccess);
```

### Language Selection

- Add `Language` property to `AppSettings` (string, default `"en"`).
- Add a language picker ComboBox in `SettingsPanel.xaml` with available cultures.
- On language change: set `Thread.CurrentThread.CurrentUICulture`, save setting, prompt user to restart (WPF does not support dynamic culture switching without restart).
- On app startup in `App.xaml.cs`: read `Language` from settings and set `CurrentUICulture` before any UI loads.

### Available Languages Discovery

A static helper `LocalizationHelper.GetAvailableLanguages()` scans the app directory for satellite assemblies (folders like `de/`, `fr/`, etc.) and returns culture display names. This lets the Settings dropdown auto-populate as community contributors add languages.

### String Inventory

Full extraction needed from:
- **17 XAML panels** — headers, labels, button content, placeholder text, tooltips
- **4 windows** — dialog text, titles, wizard step labels
- **3 controls** — status text, list headers
- **19 ViewModels** — toast messages, MessageBox text, status strings, validation messages
- **App.xaml.cs** — error dialog text

Estimated ~250-300 unique string keys across the three resource files.

---

## 2. Accessibility

### Layer 1: Keyboard Navigation

Every panel gets explicit tab ordering:

- `TabIndex` on all interactive controls (buttons, text boxes, combo boxes, toggles)
- `KeyboardNavigation.TabNavigation="Cycle"` on panel containers so tab wraps within a section
- Logical flow: sidebar → main panel header → content area → action buttons
- `IsTabStop="False"` on decorative elements (borders, separators, icons)
- `FocusManager.FocusedElement` set on panel activation so keyboard users land in a sensible spot

### Layer 2: Screen Reader Support

Every interactive control gets:

```xml
<Button Content="{x:Static res:Strings.StartServer}"
        AutomationProperties.Name="{x:Static res:Strings.StartServer}"
        AutomationProperties.HelpText="{x:Static res:Tooltips.StartServerHelp}" />
```

- `AutomationProperties.Name` — what the control IS (uses localized strings)
- `AutomationProperties.HelpText` — what it DOES (uses localized tooltip strings)
- `AutomationProperties.AutomationId` — stable ID for automation testing (English, not localized)
- Labels for non-self-describing controls (TextBox, ComboBox) via `AutomationProperties.LabeledBy`
- Live regions for dynamic content (console output, toast notifications) via `AutomationProperties.LiveSetting="Polite"`

### Layer 3: High-Contrast Theme

**System high-contrast detection:**
- App automatically respects Windows high-contrast mode via `SystemColors` brushes
- Replace inline hex colors with `SystemColors` resource references where possible

**Manual high-contrast toggle:**
- `HighContrastEnabled` boolean in `AppSettings`
- Toggle in Settings panel
- `HighContrast.xaml` ResourceDictionary with overrides for all custom colors
- `ThemeService` that merges/removes the dictionary at runtime (no restart needed for theme switching)

**Resource dictionary structure:**
```
TabgInstaller.Gui/
  Themes/
    BaseTheme.xaml          — extracts current inline colors into named brushes
    HighContrast.xaml        — overrides with SystemColors-based brushes
```

**Migration path:**
1. First, extract all inline colors from XAML into `BaseTheme.xaml` as `SolidColorBrush` resources with semantic names (e.g., `ConsoleBg`, `StatusRunning`, `StatusStopped`)
2. All XAML files reference these brush resources instead of hex colors
3. `HighContrast.xaml` overrides those same keys with high-contrast equivalents
4. `ThemeService` swaps dictionaries on toggle

---

## 3. Telemetry / Crash Reporting

### Service Interface

```csharp
// In TabgInstaller.Core
public interface ICrashReportService
{
    Task<bool> ReportCrashAsync(Exception exception, CancellationToken ct = default);
}
```

### Implementation: GitHubCrashReportService

Uses the existing Octokit dependency. Creates GitHub Issues on the project repository.

**Issue format:**
- Title: `[Crash] ExceptionType: first 80 chars of message`
- Body: app version, OS version (`Environment.OSVersion`), .NET runtime version, full stack trace, timestamp (UTC)
- Labels: `crash-report`, `automated`
- No personal data: no username, no paths, no server IPs

**Deduplication:**
- Fingerprint = `SHA256(ExceptionType + top 3 stack frames method names)`
- Before creating a new issue, search open issues for the fingerprint (stored in issue body as a hidden comment `<!-- fingerprint: abc123 -->`)
- If found: add a 👍 reaction to the existing issue (acts as occurrence counter) instead of creating a duplicate
- Rate limit: max 1 report per app session to prevent flood on crash loops

### User Consent Flow

**In App.xaml.cs `DispatcherUnhandledException` handler:**

1. Log exception to `startup.log` (existing behavior, keep)
2. Check `AppSettings.CrashReportingEnabled`
   - If **not set** (first crash): show consent dialog — "The app encountered an error. Would you like to send an anonymous crash report to help improve the app? (Only the error details and app version are sent, no personal data.)" with Yes/No/Always/Never
   - If **enabled**: send report silently, show brief "Crash report sent" in the error dialog
   - If **disabled**: skip, show error dialog only (existing behavior)
3. Show the error MessageBox regardless (existing behavior, keep)

**Settings integration:**
- `CrashReportingEnabled` nullable bool in `AppSettings` (null = not yet decided)
- Toggle in Settings panel under a "Privacy" section: "Send anonymous crash reports"
- Defaults to **off** until user opts in

### GitHub Authentication

- Uses a **read/write Issues** scoped Personal Access Token (PAT) embedded as a build constant
- The PAT is for a bot/service account, not the user's account
- Stored in a `CrashReportConfig` class (repo owner, repo name, token)
- If the token is missing/invalid/expired, crash reporting silently degrades (no error to user)

---

## 4. AppSettings Extensions

New properties added to `AppSettings`:

```csharp
public string Language { get; set; } = "en";
public bool HighContrastEnabled { get; set; } = false;
public bool? CrashReportingEnabled { get; set; } = null; // null = not yet decided
```

---

## 5. Settings Panel Updates

New sections in `SettingsPanel.xaml`:

**Language section:**
- ComboBox with available languages (auto-discovered from satellite assemblies)
- Label: "Language" / "Sprache"
- Restart prompt on change

**Accessibility section:**
- High-contrast toggle
- Label: "High contrast mode" / "Hoher Kontrast"

**Privacy section:**
- Crash reporting toggle
- Label: "Send anonymous crash reports" / "Anonyme Absturzberichte senden"
- Description text explaining what data is sent

---

## 6. Files Changed Summary

### New files:
- `Resources/Strings.resx`, `Resources/Strings.de.resx`
- `Resources/Messages.resx`, `Resources/Messages.de.resx`
- `Resources/Tooltips.resx`, `Resources/Tooltips.de.resx`
- `Resources/LocalizationHelper.cs`
- `Themes/BaseTheme.xaml`, `Themes/HighContrast.xaml`
- `Services/ThemeService.cs`, `Services/IThemeService.cs`
- `Windows/CrashReportDialog.xaml` + code-behind
- Core: `Services/CrashReportService.cs`, `Services/ICrashReportService.cs`, `Models/CrashReportConfig.cs`

### Modified files:
- Every `.xaml` file (string extraction + accessibility properties + theme brush references)
- Every ViewModel `.cs` file (string extraction to `Messages`/`Strings` references)
- `App.xaml` (merge theme dictionaries)
- `App.xaml.cs` (culture setup on startup, crash report consent flow)
- `AppSettingsService.cs` / `AppSettings` (new properties)
- `SettingsPanel.xaml` / `SettingsPanelViewModel.cs` (new sections)
- `TabgInstaller.Gui.csproj` (resource file includes)

---

## 7. Out of Scope

- RTL language support (no RTL languages planned for initial release)
- Dynamic language switching without restart
- Paid telemetry services (Sentry, AppInsights, etc.)
- WCAG AA compliance audit (this lays the foundation; a full audit is separate work)
