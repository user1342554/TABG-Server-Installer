# Phase 1: Foundation — Design Spec

**Date:** 2026-04-08
**Scope:** Code quality infrastructure, exception handling fixes, test suite, CI pipeline
**Goal:** Make the codebase safe to evolve — every subsequent phase depends on this foundation.

---

## 1. `.editorconfig` + Roslyn Analyzers

### 1.1 `.editorconfig` (solution root)

A root `.editorconfig` enforcing the style the codebase already uses:

- **Indentation:** 4 spaces (all C# files)
- **Newlines:** LF preferred, final newline required
- **Naming:** PascalCase for public/protected members, `_camelCase` for private fields, `camelCase` for locals/parameters
- **`var` usage:** Preferred when type is apparent
- **Nullable:** Follow per-project settings (enabled in Core/Gui, disabled in plugins)
- **Braces:** Allman style (new line)
- **Using directives:** Outside namespace

No style rules that contradict the existing codebase. This codifies what's already there.

### 1.2 `Directory.Build.props` (solution root)

Centralizes shared properties across all 13+ projects:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>

  <ItemGroup Condition="'$(TargetFramework)' != 'netstandard2.0'">
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

**Key decisions:**
- StyleCop only on `net8.0` projects (Core, Gui, ConfigSanitizer, Tests) — `netstandard2.0` plugin projects reference local Unity/BepInEx DLLs and shouldn't be burdened with analyzer overhead.
- Severity set to **warning** (not error) so existing code compiles. CI will gate on warnings for new code.
- `EnforceCodeStyleInBuild` makes IDE-only rules visible in `dotnet build`.

### 1.3 Analyzer suppressions

Suppressions go directly in `.editorconfig` as severity overrides (no separate file needed):

```ini
# StyleCop rules that conflict with project conventions
dotnet_diagnostic.SA1200.severity = none  # Using directives inside namespace — project uses outside
dotnet_diagnostic.SA1633.severity = none  # File header — no file headers in this project
dotnet_diagnostic.SA1101.severity = none  # Prefix this. — project doesn't use this. prefix
dotnet_diagnostic.SA1309.severity = none  # Field names with underscore — project uses _camelCase
```

Additionally, a `stylecop.json` at the solution root (referenced via `Directory.Build.props`) to configure StyleCop behavior:

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "documentationRules": {
      "documentInterfaces": false,
      "documentInternalMembers": false
    }
  }
}
```

---

## 2. Fix Silent Exception Swallowing

### 2.1 Principle

**No empty catch blocks remain.** Every catch block must either:
1. Log the exception (preferred), or
2. Have an explicit `// Intentional: <reason>` comment explaining why discarding is safe

### 2.2 Logging strategy by project type

| Project type | Logging mechanism | Example |
|---|---|---|
| Core services (have `IProgress<string>`) | `_log.LogException("context", ex)` via existing `LogExtensions` | UpdateService, BackupService |
| Core services (no log field) | Add `IProgress<string>` parameter or use `Debug.WriteLine` | Static services |
| GUI code-behind | `Debug.WriteLine($"[WARN] {context}: {ex.Message}")` | Panel debounce lambdas |
| GUI App.xaml.cs | `System.Diagnostics.Trace.TraceError(ex.ToString())` | Global exception handler |
| Plugin code (BepInEx) | `Logger.LogWarning($"context: {ex}")` | All mod plugins |

### 2.3 Specific fixes

#### Critical tier (8 instances)

1. **UpdateService.cs:57** — `catch { return null; }` in `FindReleaseTagWithAssetAsync()`
   → `catch (Exception ex) { Debug.WriteLine($"[UpdateService] Update check failed: {ex.Message}"); return null; }`

2. **BepInExLoaderService.cs:49** — `catch { }` in `InstallUnity2021LauncherAsync()`
   → `catch (Exception ex) { _log.Report($"[WARN] Legacy batch cleanup failed (non-fatal): {ex.Message}"); }`

3. **Installer.cs:110** — `catch (Exception)` in `TryFindTabgServerPath()`
   → `catch (Exception ex) { Debug.WriteLine($"[Installer] Steam path detection failed: {ex.Message}"); }`

4. **AppSettingsService.cs:38** — `catch { }` in `Load()`
   → `catch (Exception ex) { Debug.WriteLine($"[AppSettings] Failed to load settings, using defaults: {ex.Message}"); }`

5. **AppSettingsService.cs:55** — `catch { }` in `Save()`
   → `catch (Exception ex) { Debug.WriteLine($"[AppSettings] Failed to save settings: {ex.Message}"); }`

6. **AppSettingsService.cs:71** — `catch { }` in `Reset()`
   → `catch (Exception ex) { Debug.WriteLine($"[AppSettings] Failed to delete settings file: {ex.Message}"); }`

7. **App.xaml.cs:28** — `catch { }` in `DispatcherUnhandledException`
   → `catch (Exception logEx) { System.Diagnostics.Trace.TraceError($"Failed to write crash log: {logEx}"); }`

8. **App.xaml.cs:56** — `catch { }` in `Application_Startup`
   → `catch (Exception logEx) { System.Diagnostics.Trace.TraceError($"Failed to write startup log: {logEx}"); }`

#### Medium tier (~12 instances)

GUI panel debounce lambdas in ConfigPanel, LoadoutEditorPanel, MatchSettingsPanel, ModSettingsPanel, RingSpawnsPanel, ConfigWindow:
→ All get `catch (Exception ex) { Debug.WriteLine($"[PanelName] Debounce reload failed: {ex.Message}"); }`

#### Lower tier (~22 instances)

Plugin catch blocks in CustomGrenades, HuntMode, ProximityChat, FakePlayers, UnusedVehicles, WeaponSpawnConfig, ModSettings:
→ All get `catch (Exception ex) { Logger.LogWarning($"context: {ex}"); }`

---

## 3. Test Project

### 3.1 Project setup

**Project:** `TabgInstaller.Tests/TabgInstaller.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\TabgInstaller.Core\TabgInstaller.Core.csproj" />
  </ItemGroup>
</Project>
```

Add to solution file as a new project entry.

### 3.2 Test organization

```
TabgInstaller.Tests/
├── Services/
│   ├── StarterPackLoadoutServiceTests.cs
│   ├── ConfigPatcherTests.cs
│   ├── SafeConfigEditorTests.cs
│   ├── NewServerConfigManagerTests.cs
│   ├── StarterPackConfigServiceTests.cs
│   ├── ModConfigServiceTests.cs
│   ├── BackupServiceTests.cs
│   └── UpdateServiceTests.cs
├── ConfigIOTests.cs
├── ConfigSanitizer/
│   └── ConfigSanitizerTests.cs
└── TestData/
    ├── sample-game-settings.txt
    ├── sample-starterpack.txt
    ├── sample-cfg-file.cfg
    └── sample-loadouts.txt
```

### 3.3 Test coverage targets (per service)

#### StarterPackLoadoutServiceTests (~15 tests)
- Parse empty loadout string → empty list
- Parse single loadout with one item
- Parse multiple loadouts with multiple items
- Round-trip: parse → build → parse produces same result
- Malformed input handling (missing fields, extra delimiters)
- Edge case: item with quantity 0
- Edge case: loadout with 0% probability

#### ConfigPatcherTests (~12 tests)
- Apply change to existing key → value updated
- Apply change to missing key → key added or error (verify behavior)
- Get value for existing key
- Get value for missing key
- Handle keys with special characters in values
- Handle duplicate keys (first wins? last wins?)
- Empty file handling
- Datapack section changes (JSON manipulation)

#### SafeConfigEditorTests (~10 tests)
- ComputeSha256 produces consistent hash
- SetKeyValue in preview mode returns diff without modifying file
- SetKeyValue in commit mode modifies file
- Hash mismatch detection (concurrent modification)
- UTF-8 BOM encoding preservation
- Unified diff format correctness
- Empty file handling
- Key not found behavior

#### NewServerConfigManagerTests (~10 tests)
- SanitizeName removes disallowed characters
- SanitizeName with all-invalid chars uses fallback
- GeneratePassword produces expected word count
- GeneratePassword doesn't repeat words
- SanitizeServerNameForGameSettings length limits
- Edge case: empty name
- Edge case: name with only spaces

#### StarterPackConfigServiceTests (~8 tests)
- Read valid config file
- Write and re-read round-trip
- ParseBool handles "true", "false", "1", "0", case variations
- Missing keys get defaults
- Empty file handling

#### ModConfigServiceTests (~8 tests)
- Parse `.cfg` file with sections
- Read/write Commission config round-trip
- Read/write Fixes config round-trip
- ParseBool variations
- Missing section handling

#### ConfigIOTests (~10 tests)
- ReadGameSettings with reflection mapping
- WriteGameSettings produces parseable output
- ReadStarterPack JSON deserialization
- WriteStarterPack JSON serialization
- ReadPlayerPerms line-by-line parsing
- WritePlayerPerms list serialization
- Empty/missing file handling

#### UpdateServiceTests (~5 tests)
- ParseVersion from tag "v1.2.3" → Version(1,2,3)
- ParseVersion from tag "1.2.3" → Version(1,2,3)
- ParseVersion from invalid tag → null
- GetCurrentVersion returns non-null
- Version comparison logic

#### BackupServiceTests (~5 tests)
- FormatFileSize: bytes, KB, MB, GB formatting
- GetAvailableBackups with empty directory
- GetAvailableBackups parses timestamp from directory names
- Backup sorting (newest first)

**Total: ~83 tests**

### 3.4 Test data files

Minimal sample config files placed in `TestData/` directory, copied to output during build. These represent valid configurations the app would encounter in production.

---

## 4. GitHub Actions CI Pipeline

### 4.1 Workflow file

**Path:** `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=test-results.trx"

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: '**/test-results.trx'
```

### 4.2 Key decisions

- **Runner:** `windows-latest` — mandatory because `net8.0-windows` + WPF/WinForms won't build on Linux
- **No `-warnaserror` on build step initially** — existing code has analyzer warnings. This gets enabled in a follow-up once warnings are cleaned up.
- **Test results as artifacts** — always uploaded (even on failure) for debugging
- **No publish/release step** — CI validates only. Release automation is a future phase.
- **.NET 8.0.x SDK** — matches the project target framework
- **Single job** — restore → build → test is sequential within one job for simplicity

### 4.3 Plugin build compatibility

The `netstandard2.0` plugin projects reference local DLLs from `Libs/` folders. These are checked into git (`.gitignore` explicitly includes them via `!**/Libs/*.dll`), so CI will find them without extra steps.

---

## 5. Implementation Order

1. `.editorconfig` + `Directory.Build.props` + suppressions (no code changes, just config)
2. Test project skeleton (csproj + solution entry, no tests yet)
3. Fix silent exception swallowing (42 instances across 25+ files)
4. Write tests (grouped by service)
5. GitHub Actions CI pipeline
6. Verify: `dotnet build` clean, `dotnet test` all green, push and confirm CI passes

---

## 6. Out of Scope (Future Phases)

- MVVM migration
- New features (multi-server, plugin marketplace, remote management)
- Localization / i18n
- Accessibility
- Telemetry / crash reporting
- UI improvements (log viewer, config validation warnings, changelog UI)
