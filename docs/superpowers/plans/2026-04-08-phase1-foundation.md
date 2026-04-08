# Phase 1: Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add code quality infrastructure (.editorconfig, analyzers), fix all silent exception swallowing, add xUnit test suite for core services, and set up GitHub Actions CI.

**Architecture:** Config-first approach — add .editorconfig + Directory.Build.props first (zero code changes), then fix exceptions (behavioral improvement), then add test project with ~80 tests covering core parsing/config logic, then wire up CI. Each task is independently valuable.

**Tech Stack:** .NET 8.0, xUnit 2.9, FluentAssertions 7, Moq 4.20, StyleCop.Analyzers, GitHub Actions

---

## File Map

### New files to create:
- `.editorconfig` — root code style rules
- `Directory.Build.props` — shared build properties + analyzer references
- `stylecop.json` — StyleCop configuration
- `.github/workflows/ci.yml` — CI pipeline
- `TabgInstaller.Tests/TabgInstaller.Tests.csproj` — test project
- `TabgInstaller.Tests/Services/StarterPackLoadoutServiceTests.cs`
- `TabgInstaller.Tests/Services/ConfigPatcherTests.cs`
- `TabgInstaller.Tests/Services/SafeConfigEditorTests.cs`
- `TabgInstaller.Tests/Services/BackupServiceTests.cs`
- `TabgInstaller.Tests/Services/UpdateServiceTests.cs`
- `TabgInstaller.Tests/Services/StarterPackConfigServiceTests.cs`
- `TabgInstaller.Tests/Services/ModConfigServiceTests.cs`
- `TabgInstaller.Tests/ConfigIOTests.cs`

### Files to modify (exception fixes):
- `TabgInstaller.Core/Services/UpdateService.cs:57-61`
- `TabgInstaller.Core/Services/BepInExLoaderService.cs:49`
- `TabgInstaller.Core/Installer.cs:110-113`
- `TabgInstaller.Core/ConfigIO.cs:49,225-228,247,267`
- `TabgInstaller.Gui/App.xaml.cs:28,56`
- `TabgInstaller.Gui/Services/AppSettingsService.cs:38,55,71`
- `TabgInstaller.Gui/ConfigWindow.xaml.cs:176`
- `TabgInstaller.Gui/Tabs/ConfigPanel.xaml.cs:226,234`
- `TabgInstaller.Gui/Tabs/LoadoutEditorPanel.xaml.cs:75`
- `TabgInstaller.Gui/Tabs/MatchSettingsPanel.xaml.cs:49`
- `TabgInstaller.Gui/Tabs/ModSettingsPanel.xaml.cs:59,146,175,250,322`
- `TabgInstaller.Gui/Tabs/RingSpawnsPanel.xaml.cs:43`
- Plugin files (CustomGrenades, FakePlayers, FlyingControls, HuntMode, ProximityChat, UnusedVehicles, WeaponSpawnConfig)

### Solution file to modify:
- `TabgInstaller.sln` — add test project entry

---

### Task 1: Add .editorconfig + Directory.Build.props + stylecop.json

**Files:**
- Create: `.editorconfig`
- Create: `Directory.Build.props`
- Create: `stylecop.json`

- [ ] **Step 1: Create `.editorconfig` at solution root**

Create file `/d/tabginststaller/TABG-Server-Installer/.editorconfig`:

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{csproj,props,targets,xml}]
indent_size = 2

[*.cs]
# Organize usings
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

# this. preferences
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_event = false:suggestion

# var preferences
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

# Expression-level preferences
dotnet_style_object_initializer = true:suggestion
dotnet_style_collection_initializer = true:suggestion

# Braces
csharp_prefer_braces = true:suggestion

# New line preferences (Allman style)
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true

# Naming conventions
dotnet_naming_rule.private_fields_should_be_camel_case.severity = suggestion
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = underscore_camel_case

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private, private_protected
dotnet_naming_symbols.private_fields.required_modifiers =

dotnet_naming_style.underscore_camel_case.required_prefix = _
dotnet_naming_style.underscore_camel_case.capitalization = camel_case

dotnet_naming_rule.public_members_should_be_pascal_case.severity = suggestion
dotnet_naming_rule.public_members_should_be_pascal_case.symbols = public_members
dotnet_naming_rule.public_members_should_be_pascal_case.style = pascal_case_style

dotnet_naming_symbols.public_members.applicable_kinds = property, method, event, class, struct, interface, enum, delegate
dotnet_naming_symbols.public_members.applicable_accessibilities = public, protected, internal, protected_internal

dotnet_naming_style.pascal_case_style.capitalization = pascal_case

# StyleCop suppressions for rules that conflict with project conventions
dotnet_diagnostic.SA1200.severity = none
dotnet_diagnostic.SA1633.severity = none
dotnet_diagnostic.SA1101.severity = none
dotnet_diagnostic.SA1309.severity = none
dotnet_diagnostic.SA1600.severity = none
dotnet_diagnostic.SA1602.severity = none
dotnet_diagnostic.SA1516.severity = none
dotnet_diagnostic.SA1201.severity = none
dotnet_diagnostic.SA1204.severity = none
dotnet_diagnostic.SA1413.severity = none
dotnet_diagnostic.SA1127.severity = none
dotnet_diagnostic.SA1128.severity = none
dotnet_diagnostic.SA1000.severity = none
dotnet_diagnostic.SA1009.severity = none
dotnet_diagnostic.SA1111.severity = none
dotnet_diagnostic.SA1503.severity = none
dotnet_diagnostic.SA1122.severity = none
```

- [ ] **Step 2: Create `Directory.Build.props` at solution root**

Create file `/d/tabginststaller/TABG-Server-Installer/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>

  <ItemGroup Condition="'$(TargetFramework)' != 'netstandard2.0'">
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556" PrivateAssets="all" />
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)stylecop.json" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `stylecop.json` at solution root**

Create file `/d/tabginststaller/TABG-Server-Installer/stylecop.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "documentationRules": {
      "documentInterfaces": false,
      "documentInternalMembers": false,
      "documentPrivateMembers": false,
      "documentExposedElements": false
    }
  }
}
```

- [ ] **Step 4: Verify build still succeeds**

Run: `dotnet build /d/tabginststaller/TABG-Server-Installer/TabgInstaller.sln --configuration Release`
Expected: Build succeeds (warnings are OK, errors are not)

- [ ] **Step 5: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add .editorconfig Directory.Build.props stylecop.json
git commit -m "chore: add .editorconfig, Directory.Build.props, and StyleCop analyzers"
```

---

### Task 2: Create test project skeleton

**Files:**
- Create: `TabgInstaller.Tests/TabgInstaller.Tests.csproj`
- Modify: `TabgInstaller.sln`

- [ ] **Step 1: Create the test project directory and csproj**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj`:

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

- [ ] **Step 2: Add test project to solution**

Run: `dotnet sln /d/tabginststaller/TABG-Server-Installer/TabgInstaller.sln add /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj`
Expected: "Project added to the solution."

- [ ] **Step 3: Create a smoke test to verify the project compiles**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/SmokeTest.cs`:

```csharp
using Xunit;

namespace TabgInstaller.Tests
{
    public class SmokeTest
    {
        [Fact]
        public void TestProjectCompiles()
        {
            Assert.True(true);
        }
    }
}
```

- [ ] **Step 4: Restore packages and verify tests run**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj --verbosity normal`
Expected: 1 test passed

- [ ] **Step 5: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Tests/ TabgInstaller.sln
git commit -m "chore: add xUnit test project skeleton with smoke test"
```

---

### Task 3: Fix silent exception swallowing — Critical tier (Core + GUI infrastructure)

**Files:**
- Modify: `TabgInstaller.Core/Services/UpdateService.cs`
- Modify: `TabgInstaller.Core/Services/BepInExLoaderService.cs`
- Modify: `TabgInstaller.Core/Installer.cs`
- Modify: `TabgInstaller.Core/ConfigIO.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`
- Modify: `TabgInstaller.Gui/Services/AppSettingsService.cs`

- [ ] **Step 1: Fix UpdateService.cs — silent catch in CheckForUpdateAsync**

In `TabgInstaller.Core/Services/UpdateService.cs`, replace lines 57-61:

Old:
```csharp
            catch
            {
                // Network errors, rate limits, etc. — silently skip
                return null;
            }
```

New:
```csharp
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateService] Update check failed: {ex.Message}");
                return null;
            }
```

- [ ] **Step 2: Fix BepInExLoaderService.cs — silent catch in InstallUnity2021LauncherAsync**

In `TabgInstaller.Core/Services/BepInExLoaderService.cs`, find the empty catch block around line 49 for the legacy batch file cleanup:

Old:
```csharp
catch { /* cleanup — safe to ignore */ }
```

New:
```csharp
catch (Exception ex) { _log.Report($"[WARN] Legacy batch cleanup failed (non-fatal): {ex.Message}"); }
```

- [ ] **Step 3: Fix Installer.cs — silent catch in TryFindTabgServerPath**

In `TabgInstaller.Core/Installer.cs`, find the catch block around line 110:

Old:
```csharp
            catch (Exception)
            {
                // Silently fail, this is a best-effort detection
            }
```

New:
```csharp
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Installer] Steam path detection failed: {ex.Message}");
            }
```

- [ ] **Step 4: Fix ConfigIO.cs — silent catch blocks (4 instances)**

In `TabgInstaller.Core/ConfigIO.cs`:

(a) Line 49 in ReadGameSettings:
Old: `catch`
New: `catch (Exception ex)`
And add after the opening brace: `System.Diagnostics.Debug.WriteLine($"[ConfigIO] Malformed value for property '{p.Name}': {ex.Message}");`

(b) Lines 225-228 in ReadStarterPack:
Old: `catch`
New: `catch (Exception ex)`
And add: `System.Diagnostics.Debug.WriteLine($"[ConfigIO] Failed to parse StarterPack JSON from '{filePath}': {ex.Message}");`

(c) Line 247 in ReadExtraSettings:
Old: `catch { return new Dictionary<string,string>(); }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ConfigIO] Failed to parse ExtraSettings from '{filePath}': {ex.Message}"); return new Dictionary<string,string>(); }`

(d) Line 267 in ReadPlayerPerms:
Old: `catch { return new List<string>(); }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ConfigIO] Failed to parse PlayerPerms from '{filePath}': {ex.Message}"); return new List<string>(); }`

- [ ] **Step 5: Fix App.xaml.cs — silent catch blocks (2 instances)**

In `TabgInstaller.Gui/App.xaml.cs`:

(a) Line 28 in DispatcherUnhandledException handler:
Old: `catch { }`
New: `catch (Exception logEx) { System.Diagnostics.Trace.TraceError($"[App] Failed to write crash log: {logEx}"); }`

(b) Line 56 in Application_Startup:
Old: `catch { }`
New: `catch (Exception logEx) { System.Diagnostics.Trace.TraceError($"[App] Failed to write startup log: {logEx}"); }`

- [ ] **Step 6: Fix AppSettingsService.cs — silent catch blocks (3 instances)**

In `TabgInstaller.Gui/Services/AppSettingsService.cs`:

(a) Line 38 in Load():
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AppSettings] Failed to load settings, using defaults: {ex.Message}"); }`

(b) Line 55 in Save():
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AppSettings] Failed to save settings: {ex.Message}"); }`

(c) Line 71 in Reset():
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AppSettings] Failed to delete settings file: {ex.Message}"); }`

- [ ] **Step 7: Verify build still succeeds**

Run: `dotnet build /d/tabginststaller/TABG-Server-Installer/TabgInstaller.sln --configuration Release`
Expected: Build succeeds

- [ ] **Step 8: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Core/Services/UpdateService.cs TabgInstaller.Core/Services/BepInExLoaderService.cs TabgInstaller.Core/Installer.cs TabgInstaller.Core/ConfigIO.cs TabgInstaller.Gui/App.xaml.cs TabgInstaller.Gui/Services/AppSettingsService.cs
git commit -m "fix: replace silent exception swallowing with logging in core services and GUI infrastructure"
```

---

### Task 4: Fix silent exception swallowing — GUI panel debounce tier

**Files:**
- Modify: `TabgInstaller.Gui/ConfigWindow.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/ConfigPanel.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/LoadoutEditorPanel.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/MatchSettingsPanel.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/ModSettingsPanel.xaml.cs`
- Modify: `TabgInstaller.Gui/Tabs/RingSpawnsPanel.xaml.cs`

- [ ] **Step 1: Fix all GUI panel catch blocks**

For each file, find every `catch { }` or `catch { /* ... */ }` and replace with a Debug.WriteLine that includes the panel name and context. The pattern is:

Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PanelName] Operation failed: {ex.Message}"); }`

Specific replacements:

**ConfigWindow.xaml.cs** line 176 (CopyConsole_Click):
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ConfigWindow] Clipboard copy failed: {ex.Message}"); }`

**ConfigPanel.xaml.cs** line 226 (debounce lambda):
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ConfigPanel] Debounce reload failed: {ex.Message}"); }`

**ConfigPanel.xaml.cs** line 234 (LoadAll catch):
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ConfigPanel] LoadAll failed: {ex.Message}"); }`

**LoadoutEditorPanel.xaml.cs** line 75 (debounce lambda):
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LoadoutEditor] Debounce reload failed: {ex.Message}"); }`

**MatchSettingsPanel.xaml.cs** line 49 (debounce lambda):
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MatchSettings] Debounce reload failed: {ex.Message}"); }`

**ModSettingsPanel.xaml.cs** lines 59, 146, 175, 250, 322 (multiple catch blocks):
Old: `catch { }` (each instance)
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ModSettings] Operation failed: {ex.Message}"); }` (each instance)

**RingSpawnsPanel.xaml.cs** line 43 (debounce lambda):
Old: `catch { }`
New: `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RingSpawns] Debounce reload failed: {ex.Message}"); }`

- [ ] **Step 2: Verify build**

Run: `dotnet build /d/tabginststaller/TABG-Server-Installer/TabgInstaller.sln --configuration Release`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Gui/
git commit -m "fix: replace silent exception swallowing with Debug.WriteLine in GUI panels"
```

---

### Task 5: Fix silent exception swallowing — Plugin tier

**Files:**
- Modify: All plugin .cs files with empty catch blocks

- [ ] **Step 1: Fix plugin catch blocks**

For each plugin, find every `catch { }` and replace with logging using the available logger for that plugin. The exact replacements:

**CustomGrenades/CustomGrenadesPlugin.cs** line 66:
Old: `catch { }`
New: `catch (Exception ex) { Debug.LogWarning($"[CustomGrenades] Giant smoke creation failed: {ex.Message}"); }`

**CustomGrenades/MGLFlashbangPlugin.cs** line 44:
Old: `catch { }`
New: `catch (Exception ex) { Debug.LogWarning($"[MGLFlashbang] OnGunShoot check failed: {ex.Message}"); }`

**CustomGrenades/MGLFlashbangPlugin.cs** line 77:
Old: `catch { }`
New: `catch (Exception ex) { Debug.LogWarning($"[MGLFlashbang] Visual effect application failed: {ex.Message}"); }`

**FakePlayers/FakePlayersPlugin.cs** line 223:
Old: `catch { }`
New: `catch (Exception ex) { Log($"Error getting spawn position: {ex.Message}"); }`

**FlyingControls/FlyingControlsPlugin.cs** line 56:
Old: `catch { }`
New: `catch (Exception ex) { Debug.LogWarning($"[FlyingControls] ModSettings registration failed (non-fatal): {ex.Message}"); }`

**HuntMode/HuntCrateSystem.cs** line 93:
Old: `catch { /* ignore */ }`
New: `catch (Exception ex) { HuntModePlugin.LogWarning($"IsDowned check failed: {ex.Message}"); }`

**HuntMode/HuntCrateSystem.cs** line 109:
Old: `catch { /* fall through */ }`
New: `catch (Exception ex) { HuntModePlugin.LogWarning($"GetPosition failed: {ex.Message}"); }`

**HuntMode/HuntGameModePatches.cs** line 108-109:
Old: `catch { }`
New: `catch (Exception ex) { HuntModePlugin.LogWarning($"GetPosition failed: {ex.Message}"); }`

**HuntMode/HuntGameModePatches.cs** lines 393-394, 400-401, 422:
Old: `catch { }` (three instances for spawn point constructor attempts)
New: `catch (Exception ex) { HuntModePlugin.LogWarning($"SpawnPoint constructor attempt failed: {ex.Message}"); }` (each instance)

**HuntMode/HuntDownPatches.cs** line 211:
Old: `catch { /* best-effort */ }`
New: `catch (Exception ex) { HuntModePlugin.LogWarning($"Seat eject failed: {ex.Message}"); }`

**HuntMode/HuntPerkEffects.cs** lines 209-210, 220, 228, 259, 269, 297:
Old: `catch { /* fall through */ }` or `catch { /* ignore */ }` (six instances)
New: `catch (Exception ex) { HuntModePlugin.LogWarning($"Reflection setter failed (falling through): {ex.Message}"); }` (each instance)

**HuntMode.Client/HuntNetworkPatch.cs** line 113:
Old: `catch { }`
New: `catch (Exception ex) { HuntClientPlugin.Log?.LogDebug($"[HuntMode.Client] Hashtable data extraction failed: {ex.Message}"); }`

**HuntMode.Client/HuntNetworkPatch.cs** line 120:
Old: `catch { }`
New: `catch (Exception ex) { HuntClientPlugin.Log?.LogDebug($"[HuntMode.Client] Network event handling failed: {ex.Message}"); }`

**ProximityChat.Client/ProximityChatClientPlugin.cs** line 42:
Old: `catch { }`
New: `catch (Exception ex) { Logger.LogDebug($"[ProximityChat] ModSettings registration failed (non-fatal): {ex.Message}"); }`

**ProximityChat.Client/ProximityChatClientPlugin.cs** line 94:
Old: `catch { }`
New: `catch (Exception ex) { Logger.LogDebug($"[ProximityChat] Server detection failed: {ex.Message}"); }`

**ProximityChat.Client/ProximityChatClientPlugin.cs** line 136:
Old: `catch { } // Silently drop on any error (Peer not created, etc.)`
New: `catch (Exception ex) { Logger.LogDebug($"[ProximityChat] Voice send failed (non-fatal): {ex.Message}"); }`

**ProximityChat.Client/SpeakerIcon.cs** line 43:
Old: `catch { }`
New: `catch (Exception ex) { Debug.LogWarning($"[SpeakerIcon] Player name lookup failed: {ex.Message}"); }`

**ProximityChat.Client/VoicePlayback.cs** line 120:
Old: `catch { }`
New: `catch (Exception ex) { Debug.LogWarning($"[VoicePlayback] Player transform cache failed: {ex.Message}"); }`

**UnusedVehicles/UnusedVehiclesPlugin.cs** line 264:
Old: `catch { }`
New: `catch (Exception ex) { Debug.LogWarning($"[UnusedVehicles] Vehicle visual init failed (non-fatal): {ex.Message}"); }`

**WeaponSpawnConfig/WeaponSpawnConfigPlugin.cs** lines 332, 351, 378:
Old: `catch { }` (three instances)
New: `catch (Exception ex) { Instance?.Logger.LogDebug($"[WeaponSpawnConfig] Reflection operation failed: {ex.Message}"); }` (each instance)

- [ ] **Step 2: Verify build**

Run: `dotnet build /d/tabginststaller/TABG-Server-Installer/TabgInstaller.sln --configuration Release`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.CustomGrenades/ TabgInstaller.FakePlayers/ TabgInstaller.FlyingControls/ TabgInstaller.HuntMode/ TabgInstaller.HuntMode.Client/ TabgInstaller.ProximityChat.Client/ TabgInstaller.ProximityChat.Server/ TabgInstaller.UnusedVehicles/ TabgInstaller.WeaponSpawnConfig/
git commit -m "fix: replace silent exception swallowing with logging in all plugins"
```

---

### Task 6: Write StarterPackLoadoutService tests

**Files:**
- Create: `TabgInstaller.Tests/Services/StarterPackLoadoutServiceTests.cs`

- [ ] **Step 1: Create the test file**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/Services/StarterPackLoadoutServiceTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class StarterPackLoadoutServiceTests
    {
        private readonly StarterPackLoadoutService _sut = new();

        [Fact]
        public void ParseLoadoutsValue_EmptyString_ReturnsEmptyList()
        {
            var result = _sut.ParseLoadoutsValue("");
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoadoutsValue_NullString_ReturnsEmptyList()
        {
            var result = _sut.ParseLoadoutsValue(null!);
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoadoutsValue_WhitespaceOnly_ReturnsEmptyList()
        {
            var result = _sut.ParseLoadoutsValue("   ");
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoadoutsValue_SingleLoadoutNoItems_ParsesCorrectly()
        {
            var result = _sut.ParseLoadoutsValue("Pistol:50%/");

            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Pistol");
            result[0].Percent.Should().Be(50);
            result[0].Items.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoadoutsValue_SingleLoadoutWithItems_ParsesCorrectly()
        {
            var result = _sut.ParseLoadoutsValue("Rifle:75% 10:1,20:2/");

            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Rifle");
            result[0].Percent.Should().Be(75);
            result[0].Items.Should().HaveCount(2);
            result[0].Items[0].Id.Should().Be("10");
            result[0].Items[0].Quantity.Should().Be(1);
            result[0].Items[1].Id.Should().Be("20");
            result[0].Items[1].Quantity.Should().Be(2);
        }

        [Fact]
        public void ParseLoadoutsValue_MultipleLoadouts_ParsesAll()
        {
            var result = _sut.ParseLoadoutsValue("Pistol:50% 5:1/Rifle:30% 10:2/Shotgun:20%/");

            result.Should().HaveCount(3);
            result[0].Name.Should().Be("Pistol");
            result[1].Name.Should().Be("Rifle");
            result[2].Name.Should().Be("Shotgun");
        }

        [Fact]
        public void ParseLoadoutsValue_ZeroPercent_ParsesCorrectly()
        {
            var result = _sut.ParseLoadoutsValue("Empty:0%/");

            result.Should().HaveCount(1);
            result[0].Percent.Should().Be(0);
        }

        [Fact]
        public void ParseLoadoutsValue_ItemWithZeroQuantity_ParsesCorrectly()
        {
            var result = _sut.ParseLoadoutsValue("Test:100% 5:0/");

            result.Should().HaveCount(1);
            result[0].Items.Should().HaveCount(1);
            result[0].Items[0].Quantity.Should().Be(0);
        }

        [Fact]
        public void ParseLoadoutsValue_MalformedSegment_IsSkipped()
        {
            var result = _sut.ParseLoadoutsValue("notavalidformat/Pistol:50%/");

            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Pistol");
        }

        [Fact]
        public void ParseLoadoutsValue_NoTrailingSlash_StillParses()
        {
            var result = _sut.ParseLoadoutsValue("Pistol:50%");

            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Pistol");
        }

        [Fact]
        public void BuildLoadoutsValue_EmptyList_ReturnsEmptyString()
        {
            var result = _sut.BuildLoadoutsValue(new List<StarterPackLoadoutService.Loadout>());
            result.Should().BeEmpty();
        }

        [Fact]
        public void BuildLoadoutsValue_SingleLoadoutNoItems_FormatsCorrectly()
        {
            var loadouts = new List<StarterPackLoadoutService.Loadout>
            {
                new("Pistol", 50, new List<StarterPackLoadoutService.Item>())
            };

            var result = _sut.BuildLoadoutsValue(loadouts);
            result.Should().Be("Pistol:50%/");
        }

        [Fact]
        public void BuildLoadoutsValue_LoadoutWithItems_FormatsCorrectly()
        {
            var loadouts = new List<StarterPackLoadoutService.Loadout>
            {
                new("Rifle", 75, new List<StarterPackLoadoutService.Item>
                {
                    new("10", 1),
                    new("20", 2)
                })
            };

            var result = _sut.BuildLoadoutsValue(loadouts);
            result.Should().Be("Rifle:75% 10:1,20:2/");
        }

        [Fact]
        public void RoundTrip_ParseThenBuildThenParse_ProducesSameResult()
        {
            var original = "Pistol:50% 5:1,6:2/Rifle:30% 10:1/Shotgun:20%/";
            var parsed = _sut.ParseLoadoutsValue(original);
            var rebuilt = _sut.BuildLoadoutsValue(parsed);
            var reparsed = _sut.ParseLoadoutsValue(rebuilt);

            reparsed.Should().HaveCount(parsed.Count);
            for (int i = 0; i < parsed.Count; i++)
            {
                reparsed[i].Name.Should().Be(parsed[i].Name);
                reparsed[i].Percent.Should().Be(parsed[i].Percent);
                reparsed[i].Items.Should().HaveCount(parsed[i].Items.Count);
            }
        }

        [Fact]
        public void ParseLoadoutsValue_ExtraWhitespace_HandledGracefully()
        {
            var result = _sut.ParseLoadoutsValue("  Pistol:50%  5:1 /  Rifle:30%  / ");

            result.Should().HaveCount(2);
            result[0].Name.Should().Be("Pistol");
            result[1].Name.Should().Be("Rifle");
        }

        [Fact]
        public void ParseLoadoutsValue_InvalidItemPair_IsSkipped()
        {
            var result = _sut.ParseLoadoutsValue("Test:100% good:1,bad:notanumber,also:2/");

            result.Should().HaveCount(1);
            result[0].Items.Should().HaveCount(2);
            result[0].Items[0].Id.Should().Be("good");
            result[0].Items[1].Id.Should().Be("also");
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj --verbosity normal`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Tests/Services/StarterPackLoadoutServiceTests.cs
git commit -m "test: add StarterPackLoadoutService tests (15 tests)"
```

---

### Task 7: Write ConfigPatcher tests

**Files:**
- Create: `TabgInstaller.Tests/Services/ConfigPatcherTests.cs`

- [ ] **Step 1: Create the test file**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/Services/ConfigPatcherTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ConfigPatcherTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ConfigPatcher _sut = new();

        public ConfigPatcherTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string CreateConfigFile(string content)
        {
            var path = Path.Combine(_tempDir, "test_config.txt");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void ApplyGameSettingsChange_ExistingKey_UpdatesValue()
        {
            var path = CreateConfigFile("ServerName=old\nPort=7777\n");

            var result = _sut.ApplyGameSettingsChange(path, "ServerName", "newname");

            result.Should().Contain("Successfully updated");
            File.ReadAllText(path).Should().Contain("ServerName=newname");
        }

        [Fact]
        public void ApplyGameSettingsChange_MissingKey_AddsIt()
        {
            var path = CreateConfigFile("Port=7777\n");

            var result = _sut.ApplyGameSettingsChange(path, "MaxPlayers", "50");

            result.Should().Contain("Successfully updated");
            File.ReadAllText(path).Should().Contain("MaxPlayers=50");
        }

        [Fact]
        public void ApplyGameSettingsChange_FileNotFound_ReturnsError()
        {
            var path = Path.Combine(_tempDir, "nonexistent.txt");

            var result = _sut.ApplyGameSettingsChange(path, "Key", "Value");

            result.Should().Contain("not found");
        }

        [Fact]
        public void ApplyGameSettingsChange_PreservesOtherKeys()
        {
            var path = CreateConfigFile("ServerName=old\nPort=7777\nMaxPlayers=50\n");

            _sut.ApplyGameSettingsChange(path, "Port", "8888");

            var content = File.ReadAllText(path);
            content.Should().Contain("ServerName=old");
            content.Should().Contain("Port=8888");
            content.Should().Contain("MaxPlayers=50");
        }

        [Fact]
        public void ApplyGameSettingsChange_ValueWithSpecialChars_Preserved()
        {
            var path = CreateConfigFile("RingSizes=4240.0,500.0\n");

            _sut.ApplyGameSettingsChange(path, "RingSizes", "1000.0,500.0,250.0");

            File.ReadAllText(path).Should().Contain("RingSizes=1000.0,500.0,250.0");
        }

        [Fact]
        public void GetGameSettingValue_ExistingKey_ReturnsValue()
        {
            var path = CreateConfigFile("Port=7777\nMaxPlayers=50\n");

            var result = _sut.GetGameSettingValue(path, "Port");

            result.Should().Be("7777");
        }

        [Fact]
        public void GetGameSettingValue_MissingKey_ReturnsEmpty()
        {
            var path = CreateConfigFile("Port=7777\n");

            var result = _sut.GetGameSettingValue(path, "Nonexistent");

            result.Should().BeEmpty();
        }

        [Fact]
        public void GetGameSettingValue_FileNotFound_ReturnsEmpty()
        {
            var path = Path.Combine(_tempDir, "nonexistent.txt");

            var result = _sut.GetGameSettingValue(path, "Key");

            result.Should().BeEmpty();
        }

        [Fact]
        public void GetGameSettingValue_EmptyFile_ReturnsEmpty()
        {
            var path = CreateConfigFile("");

            var result = _sut.GetGameSettingValue(path, "Key");

            result.Should().BeEmpty();
        }

        [Fact]
        public void ApplyGameSettingsChange_EmptyFile_AddsKey()
        {
            var path = CreateConfigFile("");

            var result = _sut.ApplyGameSettingsChange(path, "Port", "7777");

            result.Should().Contain("Successfully");
            File.ReadAllText(path).Should().Contain("Port=7777");
        }

        [Fact]
        public void ApplyDatapackChange_UpdatesMultipleKeys()
        {
            var path = CreateConfigFile("WinCondition=Default\nKillsToWin=20\n");

            var changes = new Newtonsoft.Json.Linq.JObject
            {
                ["WinCondition"] = "KillsToWin",
                ["KillsToWin"] = "50"
            };

            var result = _sut.ApplyDatapackChange(path, "General", changes);

            result.Should().Contain("Successfully");
            var content = File.ReadAllText(path);
            content.Should().Contain("WinCondition=KillsToWin");
            content.Should().Contain("KillsToWin=50");
        }

        [Fact]
        public void ApplyDatapackChange_FileNotFound_ReturnsError()
        {
            var path = Path.Combine(_tempDir, "nonexistent.txt");
            var changes = new Newtonsoft.Json.Linq.JObject { ["Key"] = "Value" };

            var result = _sut.ApplyDatapackChange(path, "Section", changes);

            result.Should().Contain("not found");
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj --verbosity normal`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Tests/Services/ConfigPatcherTests.cs
git commit -m "test: add ConfigPatcher tests (12 tests)"
```

---

### Task 8: Write SafeConfigEditor tests

**Files:**
- Create: `TabgInstaller.Tests/Services/SafeConfigEditorTests.cs`

- [ ] **Step 1: Create the test file**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/Services/SafeConfigEditorTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class SafeConfigEditorTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly SafeConfigEditor _sut = new();

        public SafeConfigEditorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string CreateFile(string content)
        {
            var path = Path.Combine(_tempDir, "config.txt");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void ComputeSha256_SameContent_ProducesSameHash()
        {
            var path = CreateFile("ServerName=test\nPort=7777\n");

            var hash1 = SafeConfigEditor.ComputeSha256(path);
            var hash2 = SafeConfigEditor.ComputeSha256(path);

            hash1.Should().Be(hash2);
            hash1.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void ComputeSha256_DifferentContent_ProducesDifferentHash()
        {
            var path1 = CreateFile("content1");
            var hash1 = SafeConfigEditor.ComputeSha256(path1);

            File.WriteAllText(path1, "content2");
            var hash2 = SafeConfigEditor.ComputeSha256(path1);

            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void SetKeyValue_PreviewMode_DoesNotModifyFile()
        {
            var path = CreateFile("Port=7777\n");
            var originalContent = File.ReadAllText(path);
            var hash = SafeConfigEditor.ComputeSha256(path);

            var result = _sut.SetKeyValue(path, "Port", "8888", hash, previewOnly: true);

            result.Success.Should().BeTrue();
            result.Message.Should().Be("Preview only");
            result.UnifiedDiff.Should().NotBeNullOrWhiteSpace();
            File.ReadAllText(path).Should().Be(originalContent);
        }

        [Fact]
        public void SetKeyValue_CommitMode_ModifiesFile()
        {
            var path = CreateFile("Port=7777\n");
            var hash = SafeConfigEditor.ComputeSha256(path);

            var result = _sut.SetKeyValue(path, "Port", "8888", hash, previewOnly: false);

            result.Success.Should().BeTrue();
            result.Message.Should().Be("Applied");
            File.ReadAllText(path).Should().Contain("Port=8888");
        }

        [Fact]
        public void SetKeyValue_HashMismatch_ReturnsError()
        {
            var path = CreateFile("Port=7777\n");

            var result = _sut.SetKeyValue(path, "Port", "8888", "WRONGHASH", previewOnly: false);

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("changed since preview");
        }

        [Fact]
        public void SetKeyValue_NullHash_SkipsHashCheck()
        {
            var path = CreateFile("Port=7777\n");

            var result = _sut.SetKeyValue(path, "Port", "8888", null, previewOnly: false);

            result.Success.Should().BeTrue();
        }

        [Fact]
        public void SetKeyValue_MissingKey_AddsIt()
        {
            var path = CreateFile("Port=7777\n");

            var result = _sut.SetKeyValue(path, "MaxPlayers", "50", null, previewOnly: false);

            result.Success.Should().BeTrue();
            File.ReadAllText(path).Should().Contain("MaxPlayers=50");
        }

        [Fact]
        public void SetKeyValue_FileNotFound_ReturnsError()
        {
            var path = Path.Combine(_tempDir, "nonexistent.txt");

            var result = _sut.SetKeyValue(path, "Key", "Value", null, previewOnly: false);

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Not found");
        }

        [Fact]
        public void SetKeyValue_CommentsAreSkipped()
        {
            var path = CreateFile("#Port=old\nPort=7777\n");

            var result = _sut.SetKeyValue(path, "Port", "8888", null, previewOnly: false);

            result.Success.Should().BeTrue();
            var content = File.ReadAllText(path);
            content.Should().Contain("#Port=old"); // comment preserved
            content.Should().Contain("Port=8888"); // value updated
        }

        [Fact]
        public void SetKeyValue_ReturnsNewHash()
        {
            var path = CreateFile("Port=7777\n");
            var oldHash = SafeConfigEditor.ComputeSha256(path);

            var result = _sut.SetKeyValue(path, "Port", "8888", null, previewOnly: false);

            result.NewHash.Should().NotBeNullOrWhiteSpace();
            result.NewHash.Should().NotBe(oldHash);
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj --verbosity normal`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Tests/Services/SafeConfigEditorTests.cs
git commit -m "test: add SafeConfigEditor tests (10 tests)"
```

---

### Task 9: Write BackupService and UpdateService tests

**Files:**
- Create: `TabgInstaller.Tests/Services/BackupServiceTests.cs`
- Create: `TabgInstaller.Tests/Services/UpdateServiceTests.cs`

- [ ] **Step 1: Create BackupServiceTests**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/Services/BackupServiceTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class BackupServiceTests
    {
        private readonly BackupService _sut;

        public BackupServiceTests()
        {
            _sut = new BackupService(new Progress<string>(_ => { }));
        }

        [Theory]
        [InlineData(0L, "0 B")]
        [InlineData(512L, "512 B")]
        [InlineData(1024L, "1 KB")]
        [InlineData(1536L, "1.5 KB")]
        [InlineData(1048576L, "1 MB")]
        [InlineData(1073741824L, "1 GB")]
        [InlineData(1610612736L, "1.5 GB")]
        public void FormatFileSize_FormatsCorrectly(long bytes, string expected)
        {
            var result = _sut.FormatFileSize(bytes);
            result.Should().Be(expected);
        }

        [Fact]
        public void GetAvailableBackups_EmptyDirectory_ReturnsEmptyList()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            try
            {
                var result = _sut.GetAvailableBackups(tempDir);
                result.Should().BeEmpty();
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetAvailableBackups_WithBackupDirs_ReturnsBackupInfos()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            var backupsDir = Path.Combine(tempDir, "backup");
            Directory.CreateDirectory(backupsDir);
            Directory.CreateDirectory(Path.Combine(backupsDir, "backup 1 2024-01-15 14-30-25"));
            Directory.CreateDirectory(Path.Combine(backupsDir, "backup 2 2024-02-20 10-00-00"));

            try
            {
                var result = _sut.GetAvailableBackups(tempDir);
                result.Should().HaveCount(2);
                result[0].CreatedDate.Should().BeAfter(result[1].CreatedDate); // sorted newest first
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetAvailableBackups_BackupNameParsing_ExtractsDate()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            var backupsDir = Path.Combine(tempDir, "backup");
            Directory.CreateDirectory(backupsDir);
            Directory.CreateDirectory(Path.Combine(backupsDir, "backup 1 2024-06-15 14-30-25"));

            try
            {
                var result = _sut.GetAvailableBackups(tempDir);
                result.Should().HaveCount(1);
                result[0].CreatedDate.Year.Should().Be(2024);
                result[0].CreatedDate.Month.Should().Be(6);
                result[0].CreatedDate.Day.Should().Be(15);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetAvailableBackups_NonStandardName_UsesCreationTime()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            var backupsDir = Path.Combine(tempDir, "backup");
            Directory.CreateDirectory(backupsDir);
            Directory.CreateDirectory(Path.Combine(backupsDir, "my-custom-backup"));

            try
            {
                var result = _sut.GetAvailableBackups(tempDir);
                result.Should().HaveCount(1);
                result[0].Name.Should().Be("my-custom-backup");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
```

- [ ] **Step 2: Create UpdateServiceTests**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/Services/UpdateServiceTests.cs`:

```csharp
using FluentAssertions;
using Xunit;
using System.Reflection;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Tests.Services
{
    public class UpdateServiceTests
    {
        // ParseVersion is private static, so we use reflection to test it
        private static Version? InvokeParseVersion(string tag)
        {
            var method = typeof(UpdateService).GetMethod("ParseVersion", BindingFlags.NonPublic | BindingFlags.Static);
            return method?.Invoke(null, new object[] { tag }) as Version;
        }

        [Theory]
        [InlineData("v1.2.3", 1, 2, 3, 0)]
        [InlineData("V1.2.3", 1, 2, 3, 0)]
        [InlineData("1.2.3", 1, 2, 3, 0)]
        [InlineData("v4.0.0", 4, 0, 0, 0)]
        [InlineData("v1.0.0.0", 1, 0, 0, 0)]
        public void ParseVersion_ValidTag_ReturnsCorrectVersion(string tag, int major, int minor, int build, int revision)
        {
            var result = InvokeParseVersion(tag);

            result.Should().NotBeNull();
            result!.Major.Should().Be(major);
            result.Minor.Should().Be(minor);
            result.Build.Should().Be(build);
            result.Revision.Should().Be(revision);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-version")]
        [InlineData("vabc")]
        public void ParseVersion_InvalidTag_ReturnsNull(string tag)
        {
            var result = InvokeParseVersion(tag);
            result.Should().BeNull();
        }

        [Fact]
        public void ParseVersion_NullTag_ReturnsNull()
        {
            var method = typeof(UpdateService).GetMethod("ParseVersion", BindingFlags.NonPublic | BindingFlags.Static);
            var result = method?.Invoke(null, new object[] { (string)null! }) as Version;
            result.Should().BeNull();
        }

        [Fact]
        public void GetCurrentVersion_ReturnsNonNull()
        {
            var version = UpdateService.GetCurrentVersion();
            version.Should().NotBeNull();
        }

        [Fact]
        public void ParseVersion_NormalizesThreeComponentTo4()
        {
            // "1.3.0" should become "1.3.0.0" so comparison with assembly versions works
            var result = InvokeParseVersion("v1.3.0");

            result.Should().NotBeNull();
            result!.Revision.Should().Be(0);
        }
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj --verbosity normal`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Tests/Services/BackupServiceTests.cs TabgInstaller.Tests/Services/UpdateServiceTests.cs
git commit -m "test: add BackupService and UpdateService tests (10 tests)"
```

---

### Task 10: Write StarterPackConfigService and ModConfigService tests

**Files:**
- Create: `TabgInstaller.Tests/Services/StarterPackConfigServiceTests.cs`
- Create: `TabgInstaller.Tests/Services/ModConfigServiceTests.cs`

- [ ] **Step 1: Create StarterPackConfigServiceTests**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/Services/StarterPackConfigServiceTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class StarterPackConfigServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public StarterPackConfigServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Fact]
        public void Read_FileNotFound_ReturnsDefaults()
        {
            var result = StarterPackConfigService.Read(_tempDir);

            result.Should().NotBeNull();
            result.WinCondition.Should().Be("Default");
        }

        [Fact]
        public void Read_ValidFile_ParsesAllFields()
        {
            var content = "WinCondition=KillsToWin\nKillsToWin=50\nForceKillAtStart=true\nDropItemsOnDeath=false\nHealOnKill=true\nHealOnKillAmount=0.5\nCanGoDown=false\nCanLockOut=true\nPercentOfVotes=60\nMinNumberOfPlayers=4\nTimeToStart=30\nSpelldropEnabled=true\nMinSpellDropDelay=10\nMaxSpellDropDelay=30\nSpellDropOffset=5\nPreMatchTimeout=5.5\nPeriMatchTimeout=15.0\n";
            File.WriteAllText(Path.Combine(_tempDir, "TheStarterPack.txt"), content);

            var result = StarterPackConfigService.Read(_tempDir);

            result.WinCondition.Should().Be("KillsToWin");
            result.KillsToWin.Should().Be(50);
            result.ForceKillAtStart.Should().BeTrue();
            result.DropItemsOnDeath.Should().BeFalse();
            result.HealOnKill.Should().BeTrue();
            result.HealOnKillAmount.Should().BeApproximately(0.5f, 0.001f);
            result.CanGoDown.Should().BeFalse();
            result.CanLockOut.Should().BeTrue();
            result.PercentOfVotes.Should().Be(60);
            result.MinNumberOfPlayers.Should().Be(4);
            result.TimeToStart.Should().Be(30);
            result.SpelldropEnabled.Should().BeTrue();
            result.PreMatchTimeout.Should().BeApproximately(5.5f, 0.001f);
            result.PeriMatchTimeout.Should().BeApproximately(15.0f, 0.001f);
        }

        [Fact]
        public void Write_ThenRead_RoundTrips()
        {
            var settings = StarterPackConfigService.Read(_tempDir); // defaults
            settings.WinCondition = "KillsToWin";
            settings.KillsToWin = 25;
            settings.ForceKillAtStart = true;
            settings.HealOnKill = true;
            settings.HealOnKillAmount = 0.75f;

            StarterPackConfigService.Write(_tempDir, settings);
            var reread = StarterPackConfigService.Read(_tempDir);

            reread.WinCondition.Should().Be("KillsToWin");
            reread.KillsToWin.Should().Be(25);
            reread.ForceKillAtStart.Should().BeTrue();
            reread.HealOnKill.Should().BeTrue();
            reread.HealOnKillAmount.Should().BeApproximately(0.75f, 0.001f);
        }

        [Fact]
        public void Read_CommentsIgnored()
        {
            var content = "//This is a comment\nWinCondition=Default\n";
            File.WriteAllText(Path.Combine(_tempDir, "TheStarterPack.txt"), content);

            var result = StarterPackConfigService.Read(_tempDir);
            result.WinCondition.Should().Be("Default");
        }

        [Fact]
        public void Read_EmptyFile_ReturnsDefaults()
        {
            File.WriteAllText(Path.Combine(_tempDir, "TheStarterPack.txt"), "");

            var result = StarterPackConfigService.Read(_tempDir);
            result.Should().NotBeNull();
        }

        [Fact]
        public void Read_BoolParsing_CaseInsensitive()
        {
            var content = "ForceKillAtStart=True\nHealOnKill=true\nCanGoDown=FALSE\n";
            File.WriteAllText(Path.Combine(_tempDir, "TheStarterPack.txt"), content);

            var result = StarterPackConfigService.Read(_tempDir);
            result.ForceKillAtStart.Should().BeTrue();
            result.HealOnKill.Should().BeTrue();
            result.CanGoDown.Should().BeFalse();
        }

        [Fact]
        public void GetPath_ReturnsTheStarterPackTxt()
        {
            var path = StarterPackConfigService.GetPath(_tempDir);
            Path.GetFileName(path).Should().Be("TheStarterPack.txt");
        }
    }
}
```

- [ ] **Step 2: Create ModConfigServiceTests**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/Services/ModConfigServiceTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services
{
    public class ModConfigServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public ModConfigServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
            // Create BepInEx/config directory structure
            Directory.CreateDirectory(Path.Combine(_tempDir, "BepInEx", "config"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Fact]
        public void ReadCommission_FileNotFound_ReturnsDefaults()
        {
            var result = ModConfigService.ReadCommission(_tempDir);
            result.Should().NotBeNull();
        }

        [Fact]
        public void WriteCommission_ThenRead_RoundTrips()
        {
            var settings = new TabgInstaller.Core.Model.FreddoCommissionSettings
            {
                BanList = "epic123;epic456",
                Lives = 3,
                GrenadeAttackerEnabled = true,
                GrenadeAttackerChance = 0.5f,
                GrenadeAttackerId = 198,
                GrenadeCorpseEnabled = false,
            };

            ModConfigService.WriteCommission(_tempDir, settings);
            var reread = ModConfigService.ReadCommission(_tempDir);

            reread.BanList.Should().Be("epic123;epic456");
            reread.Lives.Should().Be(3);
            reread.GrenadeAttackerEnabled.Should().BeTrue();
            reread.GrenadeAttackerChance.Should().BeApproximately(0.5f, 0.001f);
        }

        [Fact]
        public void ReadFixes_FileNotFound_ReturnsDefaults()
        {
            var result = ModConfigService.ReadFixes(_tempDir);
            result.Should().NotBeNull();
        }

        [Fact]
        public void WriteFixes_ThenRead_RoundTrips()
        {
            var settings = new TabgInstaller.Core.Model.StarterPackFixesSettings
            {
                EnableLootDrops = false
            };

            ModConfigService.WriteFixes(_tempDir, settings);
            var reread = ModConfigService.ReadFixes(_tempDir);

            reread.EnableLootDrops.Should().BeFalse();
        }

        [Fact]
        public void ReadSpawnPoints_FileNotFound_ReturnsEmpty()
        {
            var result = ModConfigService.ReadSpawnPoints(_tempDir);
            result.Should().BeEmpty();
        }

        [Fact]
        public void WriteSpawnPoints_ThenRead_RoundTrips()
        {
            ModConfigService.WriteSpawnPoints(_tempDir, "100,200;300,400;500,600");
            var result = ModConfigService.ReadSpawnPoints(_tempDir);
            result.Should().Be("100,200;300,400;500,600");
        }

        [Fact]
        public void ReadCommission_CfgWithSections_ParsesCorrectly()
        {
            var cfgContent = @"[Bans]
BanList = epic123

[GrenadesOnDeath.Attacker]
Enabled = true
Chance = 0.3
ID = 199

[GrenadesOnDeath.Corpse]
Enabled = false
Chance = 0.1
ID = 198

[Player]
Lives = 5
";
            File.WriteAllText(Path.Combine(_tempDir, "BepInEx", "config", "FreddoTABGCommission.cfg"), cfgContent);

            var result = ModConfigService.ReadCommission(_tempDir);

            result.BanList.Should().Be("epic123");
            result.GrenadeAttackerEnabled.Should().BeTrue();
            result.GrenadeAttackerChance.Should().BeApproximately(0.3f, 0.001f);
            result.GrenadeAttackerId.Should().Be(199);
            result.GrenadeCorpseEnabled.Should().BeFalse();
            result.Lives.Should().Be(5);
        }
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj --verbosity normal`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Tests/Services/StarterPackConfigServiceTests.cs TabgInstaller.Tests/Services/ModConfigServiceTests.cs
git commit -m "test: add StarterPackConfigService and ModConfigService tests (15 tests)"
```

---

### Task 11: Write ConfigIO tests

**Files:**
- Create: `TabgInstaller.Tests/ConfigIOTests.cs`

- [ ] **Step 1: Create the test file**

Create file `/d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/ConfigIOTests.cs`:

```csharp
using FluentAssertions;
using TabgInstaller.Core;
using TabgInstaller.Core.Model;
using Xunit;

namespace TabgInstaller.Tests
{
    public class ConfigIOTests : IDisposable
    {
        private readonly string _tempDir;

        public ConfigIOTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string TempFile(string name = "config.txt") => Path.Combine(_tempDir, name);

        // --- GameSettings ---

        [Fact]
        public void ReadGameSettings_FileNotFound_ReturnsDefaults()
        {
            var result = ConfigIO.ReadGameSettings(TempFile());
            result.Should().NotBeNull();
            result.ServerName.Should().Be("enormous"); // default value
        }

        [Fact]
        public void ReadGameSettings_ValidFile_ParsesStrings()
        {
            File.WriteAllText(TempFile(), "ServerName=MyServer\nPassword=secret\nTeamMode=SOLO\n");

            var result = ConfigIO.ReadGameSettings(TempFile());

            result.ServerName.Should().Be("MyServer");
            result.Password.Should().Be("secret");
            result.TeamMode.Should().Be("SOLO");
        }

        [Fact]
        public void ReadGameSettings_ValidFile_ParsesInts()
        {
            File.WriteAllText(TempFile(), "Port=8888\nMaxPlayers=100\n");

            var result = ConfigIO.ReadGameSettings(TempFile());

            result.Port.Should().Be(8888);
            result.MaxPlayers.Should().Be(100);
        }

        [Fact]
        public void ReadGameSettings_ValidFile_ParsesFloats()
        {
            File.WriteAllText(TempFile(), "CarSpawnRate=0.5\nCountdown=15.0\n");

            var result = ConfigIO.ReadGameSettings(TempFile());

            result.CarSpawnRate.Should().BeApproximately(0.5f, 0.001f);
            result.Countdown.Should().BeApproximately(15.0f, 0.001f);
        }

        [Fact]
        public void ReadGameSettings_ValidFile_ParsesBools()
        {
            File.WriteAllText(TempFile(), "Relay=true\nNoRing=false\nAutoTeam=True\n");

            var result = ConfigIO.ReadGameSettings(TempFile());

            result.Relay.Should().BeTrue();
            result.NoRing.Should().BeFalse();
            result.AutoTeam.Should().BeTrue();
        }

        [Fact]
        public void ReadGameSettings_MalformedValue_UsesDefault()
        {
            File.WriteAllText(TempFile(), "Port=notanumber\n");

            var result = ConfigIO.ReadGameSettings(TempFile());

            result.Port.Should().Be(7777); // default
        }

        [Fact]
        public void ReadGameSettings_CommentsIgnored()
        {
            File.WriteAllText(TempFile(), "// This is a comment\nServerName=TestServer\n");

            var result = ConfigIO.ReadGameSettings(TempFile());

            result.ServerName.Should().Be("TestServer");
        }

        [Fact]
        public void WriteGameSettings_ThenRead_RoundTrips()
        {
            var data = new GameSettingsData
            {
                ServerName = "RoundTrip",
                Port = 9999,
                MaxPlayers = 42,
                CarSpawnRate = 0.75f,
                Relay = false
            };
            var path = TempFile("game_settings.txt");

            ConfigIO.WriteGameSettings(data, path);
            var reread = ConfigIO.ReadGameSettings(path);

            reread.ServerName.Should().Be("RoundTrip");
            reread.Port.Should().Be(9999);
            reread.MaxPlayers.Should().Be(42);
            reread.CarSpawnRate.Should().BeApproximately(0.75f, 0.001f);
            reread.Relay.Should().BeFalse();
        }

        // --- PlayerPerms ---

        [Fact]
        public void ReadPlayerPerms_FileNotFound_ReturnsEmptyList()
        {
            var result = ConfigIO.ReadPlayerPerms(TempFile("perms.json"));
            result.Should().BeEmpty();
        }

        [Fact]
        public void WritePlayerPerms_ThenRead_RoundTrips()
        {
            var path = TempFile("perms.json");
            var perms = new List<string> { "epic123:4", "epic456:2" };

            ConfigIO.WritePlayerPerms(perms, path);
            var result = ConfigIO.ReadPlayerPerms(path);

            result.Should().BeEquivalentTo(perms);
        }

        // --- ExtraSettings ---

        [Fact]
        public void ReadExtraSettings_FileNotFound_ReturnsEmptyDict()
        {
            var result = ConfigIO.ReadExtraSettings(TempFile("extra.json"));
            result.Should().BeEmpty();
        }

        [Fact]
        public void WriteExtraSettings_ThenRead_RoundTrips()
        {
            var path = TempFile("extra.json");
            var settings = new Dictionary<string, string>
            {
                ["Key1"] = "Value1",
                ["Key2"] = "Value2"
            };

            ConfigIO.WriteExtraSettings(settings, path);
            var result = ConfigIO.ReadExtraSettings(path);

            result.Should().BeEquivalentTo(settings);
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj --verbosity normal`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add TabgInstaller.Tests/ConfigIOTests.cs
git commit -m "test: add ConfigIO tests (12 tests)"
```

---

### Task 12: Remove smoke test and run full test suite

**Files:**
- Delete: `TabgInstaller.Tests/SmokeTest.cs`

- [ ] **Step 1: Delete the smoke test**

Delete file `TabgInstaller.Tests/SmokeTest.cs` — it was scaffolding and is no longer needed.

- [ ] **Step 2: Run full test suite**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.Tests/TabgInstaller.Tests.csproj --verbosity normal`
Expected: All ~75+ tests pass

- [ ] **Step 3: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add -u TabgInstaller.Tests/SmokeTest.cs
git commit -m "chore: remove smoke test, full test suite in place"
```

---

### Task 13: Add GitHub Actions CI pipeline

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Create the workflow file**

Create file `/d/tabginststaller/TABG-Server-Installer/.github/workflows/ci.yml`:

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

- [ ] **Step 2: Verify full build + test locally**

Run: `dotnet build /d/tabginststaller/TABG-Server-Installer/TabgInstaller.sln --configuration Release && dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.sln --configuration Release --verbosity normal`
Expected: Build succeeds, all tests pass

- [ ] **Step 3: Commit**

```bash
cd /d/tabginststaller/TABG-Server-Installer
git add .github/workflows/ci.yml
git commit -m "ci: add GitHub Actions CI pipeline with build and test"
```

---

### Task 14: Final verification

- [ ] **Step 1: Clean build from scratch**

Run:
```bash
cd /d/tabginststaller/TABG-Server-Installer
dotnet clean --configuration Release
dotnet build --configuration Release
```
Expected: Build succeeds with no errors

- [ ] **Step 2: Run full test suite**

Run: `dotnet test /d/tabginststaller/TABG-Server-Installer/TabgInstaller.sln --configuration Release --verbosity normal`
Expected: All tests pass

- [ ] **Step 3: Verify git status is clean**

Run: `git status`
Expected: No uncommitted changes

- [ ] **Step 4: Review commit log**

Run: `git log --oneline -10`
Expected: See all Phase 1 commits in order
