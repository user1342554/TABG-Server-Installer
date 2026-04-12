# Phase 6: Plugin Marketplace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the hardcoded plugin system into an open marketplace where community authors publish, distribute, and update TABG plugins through the installer app.

**Architecture:** A `registry/` directory in the existing GitHub repo holds per-plugin manifest files, auto-compiled into `registry.json` by a GitHub Action. The WPF app fetches this file via Octokit, presents a "Browse Plugins" tab for discovery/install/update, and tracks installed community plugins per server instance in `installed-plugins.json`.

**Tech Stack:** C# .NET 8.0, WPF, CommunityToolkit.Mvvm, Octokit, Newtonsoft.Json, xUnit + FluentAssertions + Moq

---

## File Structure

### New files in `TabgInstaller.Core/`

| File | Responsibility |
|------|---------------|
| `Model/PluginManifest.cs` | C# record matching the community plugin manifest JSON schema |
| `Model/PluginRegistryResponse.cs` | Root model for the fetched `registry.json` (version, generatedAt, plugins list) |
| `Model/InstalledPluginEntry.cs` | Single entry in `installed-plugins.json` |
| `Model/InstalledPluginsData.cs` | Root model for `installed-plugins.json` (list of entries) |
| `Services/IRegistryService.cs` | Interface for registry fetching |
| `Services/RegistryService.cs` | Fetches + caches `registry.json` via Octokit |
| `Services/IInstalledPluginTracker.cs` | Interface for installed plugin tracking |
| `Services/InstalledPluginTracker.cs` | CRUD on `installed-plugins.json` per server instance |
| `Services/IMarketplaceInstallService.cs` | Interface for marketplace install operations |
| `Services/MarketplaceInstallService.cs` | Download, dependency resolution, install, update, uninstall |

### New files in `TabgInstaller.Gui/`

| File | Responsibility |
|------|---------------|
| `Tabs/BrowsePluginsPanel.xaml` | Browse Plugins tab UI — search, filter, plugin cards |
| `Tabs/BrowsePluginsPanel.xaml.cs` | Code-behind (minimal, DI wiring) |
| `ViewModels/BrowsePluginsViewModel.cs` | Tab logic: search, filter, sort, install/update/uninstall actions |
| `ViewModels/PluginCardViewModel.cs` | Individual card state: install status, compatibility, action button |

### New files in `TabgInstaller.Tests/`

| File | Responsibility |
|------|---------------|
| `Model/PluginManifestTests.cs` | Manifest deserialization and validation |
| `Services/RegistryServiceTests.cs` | Registry fetch, cache, error handling |
| `Services/InstalledPluginTrackerTests.cs` | Installed plugin CRUD, file I/O |
| `Services/MarketplaceInstallServiceTests.cs` | Dependency resolution, install/update/uninstall logic |
| `ViewModels/BrowsePluginsViewModelTests.cs` | Search, filter, sort, command behavior |

### New files in repo root

| File | Responsibility |
|------|---------------|
| `registry/schema/plugin-manifest.schema.json` | JSON Schema for manifest validation |
| `registry/TEMPLATE.json` | Blank manifest template for authors |
| `registry/CONTRIBUTING.md` | Step-by-step submission guide |
| `.github/workflows/registry-validate.yml` | PR validation action |
| `.github/workflows/registry-build.yml` | Post-merge registry compilation |

### Modified files

| File | Change |
|------|--------|
| `TabgInstaller.Core/Services/GitHubService.cs` | Add `FetchFileContentAsync()` method |
| `TabgInstaller.Gui/MainWindow.xaml` | Add 9th tab (Browse Plugins) between Reference and Settings |
| `TabgInstaller.Gui/MainWindow.xaml.cs` | Initialize BrowsePluginsPanel DataContext in `InitializeAllPanels()` |
| `TabgInstaller.Gui/App.xaml.cs` | Register new services + ViewModel in DI |
| `TabgInstaller.Gui/ViewModels/DashboardViewModel.cs` | Add "plugin updates available" info card |
| `TabgInstaller.Gui/Resources/Strings.resx` | Add new localization strings |

---

## Task 1: Core Models

**Files:**
- Create: `TabgInstaller.Core/Model/PluginManifest.cs`
- Create: `TabgInstaller.Core/Model/PluginRegistryResponse.cs`
- Create: `TabgInstaller.Core/Model/InstalledPluginEntry.cs`
- Create: `TabgInstaller.Core/Model/InstalledPluginsData.cs`
- Test: `TabgInstaller.Tests/Model/PluginManifestTests.cs`

- [ ] **Step 1: Write tests for PluginManifest deserialization**

```csharp
// TabgInstaller.Tests/Model/PluginManifestTests.cs
using FluentAssertions;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;
using Xunit;

namespace TabgInstaller.Tests.Model;

public class PluginManifestTests
{
    private const string ValidManifestJson = """
        {
          "id": "test-plugin",
          "name": "Test Plugin",
          "version": "1.0.0",
          "description": "A test plugin.",
          "author": "TestAuthor",
          "downloadUrl": "https://github.com/TestAuthor/TestPlugin/releases/latest",
          "dllNames": ["TestPlugin.dll"],
          "type": "server",
          "compatibleTabgVersions": ["*"],
          "minInstallerVersion": "4.0.0",
          "bepInExVersion": "5.4.22",
          "dependencies": ["citruslib"],
          "tags": ["test"],
          "requiresClientMod": false
        }
        """;

    [Fact]
    public void Deserialize_ValidJson_AllFieldsPopulated()
    {
        var manifest = JsonConvert.DeserializeObject<PluginManifest>(ValidManifestJson);

        manifest.Should().NotBeNull();
        manifest!.Id.Should().Be("test-plugin");
        manifest.Name.Should().Be("Test Plugin");
        manifest.Version.Should().Be("1.0.0");
        manifest.Description.Should().Be("A test plugin.");
        manifest.Author.Should().Be("TestAuthor");
        manifest.DownloadUrl.Should().Be("https://github.com/TestAuthor/TestPlugin/releases/latest");
        manifest.DllNames.Should().ContainSingle("TestPlugin.dll");
        manifest.Type.Should().Be("server");
        manifest.CompatibleTabgVersions.Should().ContainSingle("*");
        manifest.MinInstallerVersion.Should().Be("4.0.0");
        manifest.BepInExVersion.Should().Be("5.4.22");
        manifest.Dependencies.Should().ContainSingle("citruslib");
        manifest.Tags.Should().ContainSingle("test");
        manifest.RequiresClientMod.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_OptionalFieldsMissing_DefaultsApplied()
    {
        var json = """
            {
              "id": "minimal",
              "name": "Minimal Plugin",
              "version": "1.0.0",
              "description": "Bare minimum.",
              "author": "Someone",
              "downloadUrl": "https://github.com/Someone/Minimal/releases/latest",
              "dllNames": ["Minimal.dll"],
              "type": "server",
              "compatibleTabgVersions": ["*"],
              "minInstallerVersion": "4.0.0",
              "bepInExVersion": "5.4.22"
            }
            """;

        var manifest = JsonConvert.DeserializeObject<PluginManifest>(json);

        manifest.Should().NotBeNull();
        manifest!.Dependencies.Should().BeEmpty();
        manifest.Tags.Should().BeEmpty();
        manifest.AuthorUrl.Should().BeNull();
        manifest.RepositoryUrl.Should().BeNull();
        manifest.IconUrl.Should().BeNull();
        manifest.RequiresClientMod.Should().BeFalse();
        manifest.ClientPluginId.Should().BeNull();
        manifest.Changelog.Should().BeNull();
    }

    [Fact]
    public void Deserialize_RegistryResponse_ParsesPluginList()
    {
        var json = $$"""
            {
              "version": 1,
              "generatedAt": "2026-04-10T12:00:00Z",
              "plugins": [{{ValidManifestJson}}]
            }
            """;

        var response = JsonConvert.DeserializeObject<PluginRegistryResponse>(json);

        response.Should().NotBeNull();
        response!.Version.Should().Be(1);
        response.GeneratedAt.Should().Be("2026-04-10T12:00:00Z");
        response.Plugins.Should().HaveCount(1);
        response.Plugins[0].Id.Should().Be("test-plugin");
    }

    [Fact]
    public void Deserialize_InstalledPluginsData_RoundTrip()
    {
        var data = new InstalledPluginsData
        {
            Plugins = new List<InstalledPluginEntry>
            {
                new()
                {
                    Id = "test-plugin",
                    InstalledVersion = "1.0.0",
                    InstalledAt = "2026-04-10T12:00:00Z",
                    UpdatedAt = null,
                    Pinned = false,
                    DllNames = new[] { "TestPlugin.dll" }
                }
            }
        };

        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<InstalledPluginsData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Plugins.Should().HaveCount(1);
        deserialized.Plugins[0].Id.Should().Be("test-plugin");
        deserialized.Plugins[0].InstalledVersion.Should().Be("1.0.0");
        deserialized.Plugins[0].Pinned.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~PluginManifestTests" -v minimal`
Expected: Build errors — `PluginManifest`, `PluginRegistryResponse`, `InstalledPluginsData`, `InstalledPluginEntry` types don't exist yet.

- [ ] **Step 3: Create PluginManifest model**

```csharp
// TabgInstaller.Core/Model/PluginManifest.cs
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model;

/// <summary>
/// Represents a community plugin manifest from the marketplace registry.
/// </summary>
public class PluginManifest
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("version")]
    public string Version { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("author")]
    public string Author { get; set; } = "";

    [JsonProperty("authorUrl")]
    public string? AuthorUrl { get; set; }

    [JsonProperty("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    [JsonProperty("downloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonProperty("dllNames")]
    public string[] DllNames { get; set; } = Array.Empty<string>();

    /// <summary>One of "server", "client", or "both".</summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "server";

    [JsonProperty("compatibleTabgVersions")]
    public string[] CompatibleTabgVersions { get; set; } = new[] { "*" };

    [JsonProperty("minInstallerVersion")]
    public string MinInstallerVersion { get; set; } = "";

    [JsonProperty("bepInExVersion")]
    public string BepInExVersion { get; set; } = "";

    [JsonProperty("dependencies")]
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    [JsonProperty("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [JsonProperty("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonProperty("requiresClientMod")]
    public bool RequiresClientMod { get; set; }

    [JsonProperty("clientPluginId")]
    public string? ClientPluginId { get; set; }

    [JsonProperty("changelog")]
    public string? Changelog { get; set; }
}
```

- [ ] **Step 4: Create PluginRegistryResponse model**

```csharp
// TabgInstaller.Core/Model/PluginRegistryResponse.cs
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model;

/// <summary>
/// Root model for the auto-generated registry.json fetched from GitHub.
/// </summary>
public class PluginRegistryResponse
{
    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("generatedAt")]
    public string GeneratedAt { get; set; } = "";

    [JsonProperty("plugins")]
    public List<PluginManifest> Plugins { get; set; } = new();
}
```

- [ ] **Step 5: Create InstalledPluginEntry and InstalledPluginsData models**

```csharp
// TabgInstaller.Core/Model/InstalledPluginEntry.cs
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model;

/// <summary>
/// Tracks a single community plugin installed on a server instance.
/// </summary>
public class InstalledPluginEntry
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("installedVersion")]
    public string InstalledVersion { get; set; } = "";

    [JsonProperty("installedAt")]
    public string InstalledAt { get; set; } = "";

    [JsonProperty("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonProperty("pinned")]
    public bool Pinned { get; set; }

    [JsonProperty("dllNames")]
    public string[] DllNames { get; set; } = Array.Empty<string>();
}
```

```csharp
// TabgInstaller.Core/Model/InstalledPluginsData.cs
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Model;

/// <summary>
/// Root model for installed-plugins.json stored per server instance.
/// </summary>
public class InstalledPluginsData
{
    [JsonProperty("plugins")]
    public List<InstalledPluginEntry> Plugins { get; set; } = new();
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~PluginManifestTests" -v minimal`
Expected: All 4 tests pass.

- [ ] **Step 7: Commit**

```bash
git add TabgInstaller.Core/Model/PluginManifest.cs TabgInstaller.Core/Model/PluginRegistryResponse.cs TabgInstaller.Core/Model/InstalledPluginEntry.cs TabgInstaller.Core/Model/InstalledPluginsData.cs TabgInstaller.Tests/Model/PluginManifestTests.cs
git commit -m "feat(marketplace): add core models for plugin manifest, registry response, and installed plugin tracking"
```

---

## Task 2: GitHubService Extension

**Files:**
- Modify: `TabgInstaller.Core/Services/GitHubService.cs`

- [ ] **Step 1: Make existing GitHubService methods virtual and add `FetchFileContentAsync`**

First, make the three existing methods on `GitHubService` virtual so Moq can mock them in tests. Change the method signatures (add `virtual` keyword):

```csharp
public virtual async Task<Octokit.Release?> GetLatestReleaseAsync(string owner, string repo)
public virtual async Task<Octokit.Release?> GetReleaseAsync(string owner, string repo, string tagName)
public virtual async Task<bool> DownloadAssetAsync(string owner, string repo, string browserDownloadUrl, string destinationPath, string downloadDirectory, string? anotherOptionalString)
```

Then add this new method to `GitHubService` class in `TabgInstaller.Core/Services/GitHubService.cs`:

```csharp
/// <summary>
/// Fetches a file's content from a GitHub repo via the Contents API.
/// Returns the decoded string content, or null on failure.
/// </summary>
public virtual async Task<string?> FetchFileContentAsync(string owner, string repo, string path)
{
    try
    {
        var contents = await _client.Repository.Content.GetAllContents(owner, repo, path);
        if (contents.Count > 0 && contents[0].Content != null)
            return contents[0].Content;

        _log.Report($"[WARN] File {path} in {owner}/{repo} had no content.");
        return null;
    }
    catch (Octokit.NotFoundException)
    {
        _log.Report($"[WARN] File {path} not found in {owner}/{repo}.");
        return null;
    }
    catch (Exception ex)
    {
        _log.LogException($"Error fetching {path} from {owner}/{repo}", ex);
        return null;
    }
}
```

Also add `using System.Linq;` at the top if not already present.

- [ ] **Step 2: Verify build succeeds**

Run: `dotnet build TabgInstaller.Core`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add TabgInstaller.Core/Services/GitHubService.cs
git commit -m "feat(marketplace): add FetchFileContentAsync to GitHubService for registry fetching"
```

---

## Task 3: RegistryService

**Files:**
- Create: `TabgInstaller.Core/Services/IRegistryService.cs`
- Create: `TabgInstaller.Core/Services/RegistryService.cs`
- Test: `TabgInstaller.Tests/Services/RegistryServiceTests.cs`

- [ ] **Step 1: Write tests for RegistryService**

```csharp
// TabgInstaller.Tests/Services/RegistryServiceTests.cs
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services;

public class RegistryServiceTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly string _cachePath;
    private readonly Mock<GitHubService> _gitHub;
    private readonly RegistryService _sut;

    public RegistryServiceTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_cacheDir);
        _cachePath = Path.Combine(_cacheDir, "registry-cache.json");

        // GitHubService requires HttpClient and IProgress<string>
        _gitHub = new Mock<GitHubService>(
            new HttpClient(),
            new Progress<string>(_ => { }))
        { CallBase = false };

        _sut = new RegistryService(_gitHub.Object, _cachePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, true);
    }

    private static PluginRegistryResponse CreateTestRegistry(params PluginManifest[] plugins)
    {
        return new PluginRegistryResponse
        {
            Version = 1,
            GeneratedAt = "2026-04-10T12:00:00Z",
            Plugins = plugins.ToList()
        };
    }

    private static PluginManifest CreateTestManifest(string id = "test-plugin", string version = "1.0.0")
    {
        return new PluginManifest
        {
            Id = id,
            Name = $"Test {id}",
            Version = version,
            Description = "A test plugin.",
            Author = "TestAuthor",
            DownloadUrl = $"https://github.com/TestAuthor/{id}/releases/latest",
            DllNames = new[] { $"{id}.dll" },
            Type = "server",
            CompatibleTabgVersions = new[] { "*" },
            MinInstallerVersion = "4.0.0",
            BepInExVersion = "5.4.22"
        };
    }

    [Fact]
    public async Task FetchRegistryAsync_Success_ReturnsPlugins()
    {
        var registry = CreateTestRegistry(CreateTestManifest());
        var json = JsonConvert.SerializeObject(registry);

        _gitHub.Setup(g => g.FetchFileContentAsync("user1342554", "TABG-Server-Installer", "registry/registry.json"))
            .ReturnsAsync(json);

        var result = await _sut.FetchRegistryAsync();

        result.Should().NotBeNull();
        result!.Plugins.Should().HaveCount(1);
        result.Plugins[0].Id.Should().Be("test-plugin");
    }

    [Fact]
    public async Task FetchRegistryAsync_Success_WritesCache()
    {
        var registry = CreateTestRegistry(CreateTestManifest());
        var json = JsonConvert.SerializeObject(registry);

        _gitHub.Setup(g => g.FetchFileContentAsync("user1342554", "TABG-Server-Installer", "registry/registry.json"))
            .ReturnsAsync(json);

        await _sut.FetchRegistryAsync();

        File.Exists(_cachePath).Should().BeTrue();
        var cached = JsonConvert.DeserializeObject<PluginRegistryResponse>(File.ReadAllText(_cachePath));
        cached!.Plugins.Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchRegistryAsync_NetworkFailure_ReturnsCachedVersion()
    {
        // Pre-populate cache
        var registry = CreateTestRegistry(CreateTestManifest("cached-plugin"));
        File.WriteAllText(_cachePath, JsonConvert.SerializeObject(registry));

        _gitHub.Setup(g => g.FetchFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.FetchRegistryAsync();

        result.Should().NotBeNull();
        result!.Plugins.Should().HaveCount(1);
        result.Plugins[0].Id.Should().Be("cached-plugin");
    }

    [Fact]
    public async Task FetchRegistryAsync_NoNetworkNoCache_ReturnsNull()
    {
        _gitHub.Setup(g => g.FetchFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.FetchRegistryAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedRegistry_AfterFetch_ReturnsCachedData()
    {
        var registry = CreateTestRegistry(CreateTestManifest());
        var json = JsonConvert.SerializeObject(registry);

        _gitHub.Setup(g => g.FetchFileContentAsync("user1342554", "TABG-Server-Installer", "registry/registry.json"))
            .ReturnsAsync(json);

        await _sut.FetchRegistryAsync();
        var cached = _sut.GetCachedRegistry();

        cached.Should().NotBeNull();
        cached!.Plugins.Should().HaveCount(1);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~RegistryServiceTests" -v minimal`
Expected: Build errors — `IRegistryService`, `RegistryService` don't exist yet.

- [ ] **Step 3: Create IRegistryService interface**

```csharp
// TabgInstaller.Core/Services/IRegistryService.cs
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services;

public interface IRegistryService
{
    /// <summary>Fetches registry.json from GitHub, caches locally. Falls back to cache on failure.</summary>
    Task<PluginRegistryResponse?> FetchRegistryAsync();

    /// <summary>Returns the in-memory cached registry without fetching. Null if never fetched.</summary>
    PluginRegistryResponse? GetCachedRegistry();
}
```

- [ ] **Step 4: Create RegistryService implementation**

```csharp
// TabgInstaller.Core/Services/RegistryService.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services;

public class RegistryService : IRegistryService
{
    private const string RegistryOwner = "user1342554";
    private const string RegistryRepo = "TABG-Server-Installer";
    private const string RegistryPath = "registry/registry.json";

    private readonly GitHubService _gitHub;
    private readonly string _cachePath;
    private PluginRegistryResponse? _cached;

    public RegistryService(GitHubService gitHub, string cachePath)
    {
        _gitHub = gitHub;
        _cachePath = cachePath;
    }

    public async Task<PluginRegistryResponse?> FetchRegistryAsync()
    {
        // Try fetching from GitHub
        var json = await _gitHub.FetchFileContentAsync(RegistryOwner, RegistryRepo, RegistryPath);
        if (json != null)
        {
            try
            {
                var registry = JsonConvert.DeserializeObject<PluginRegistryResponse>(json);
                if (registry != null)
                {
                    _cached = registry;
                    WriteCacheToDisk(json);
                    return registry;
                }
            }
            catch (JsonException)
            {
                // Malformed JSON from GitHub — fall through to cache
            }
        }

        // Fallback to local cache
        return LoadFromCache();
    }

    public PluginRegistryResponse? GetCachedRegistry()
    {
        if (_cached != null) return _cached;
        return LoadFromCache();
    }

    private PluginRegistryResponse? LoadFromCache()
    {
        if (_cached != null) return _cached;

        try
        {
            if (File.Exists(_cachePath))
            {
                var json = File.ReadAllText(_cachePath);
                _cached = JsonConvert.DeserializeObject<PluginRegistryResponse>(json);
                return _cached;
            }
        }
        catch (Exception)
        {
            // Corrupted cache — ignore
        }

        return null;
    }

    private void WriteCacheToDisk(string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(_cachePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_cachePath, json);
        }
        catch (Exception)
        {
            // Non-critical — cache write failure is fine
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~RegistryServiceTests" -v minimal`
Expected: All 5 tests pass. Note: if `FetchFileContentAsync` is not virtual and Moq cannot mock it, make the method `virtual` in `GitHubService.cs`.

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Core/Services/IRegistryService.cs TabgInstaller.Core/Services/RegistryService.cs TabgInstaller.Tests/Services/RegistryServiceTests.cs
git commit -m "feat(marketplace): add RegistryService for fetching and caching plugin registry"
```

---

## Task 4: InstalledPluginTracker

**Files:**
- Create: `TabgInstaller.Core/Services/IInstalledPluginTracker.cs`
- Create: `TabgInstaller.Core/Services/InstalledPluginTracker.cs`
- Test: `TabgInstaller.Tests/Services/InstalledPluginTrackerTests.cs`

- [ ] **Step 1: Write tests for InstalledPluginTracker**

```csharp
// TabgInstaller.Tests/Services/InstalledPluginTrackerTests.cs
using FluentAssertions;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services;

public class InstalledPluginTrackerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _communityDir;

    public InstalledPluginTrackerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
        _communityDir = Path.Combine(_tempDir, "BepInEx", "plugins", "community");
        Directory.CreateDirectory(_communityDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private InstalledPluginTracker CreateSut() => new(_tempDir);

    [Fact]
    public void Load_NoFile_ReturnsEmptyList()
    {
        var sut = CreateSut();

        var data = sut.Load();

        data.Plugins.Should().BeEmpty();
    }

    [Fact]
    public void AddPlugin_ThenLoad_ReturnsPlugin()
    {
        var sut = CreateSut();

        sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });

        var data = sut.Load();
        data.Plugins.Should().ContainSingle(p => p.Id == "test-plugin");
        data.Plugins[0].InstalledVersion.Should().Be("1.0.0");
        data.Plugins[0].Pinned.Should().BeFalse();
    }

    [Fact]
    public void UpdatePluginVersion_UpdatesVersionAndTimestamp()
    {
        var sut = CreateSut();
        sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });

        sut.UpdatePluginVersion("test-plugin", "2.0.0");

        var data = sut.Load();
        data.Plugins[0].InstalledVersion.Should().Be("2.0.0");
        data.Plugins[0].UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void RemovePlugin_RemovesFromList()
    {
        var sut = CreateSut();
        sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });

        sut.RemovePlugin("test-plugin");

        var data = sut.Load();
        data.Plugins.Should().BeEmpty();
    }

    [Fact]
    public void SetPinned_UpdatesPinnedFlag()
    {
        var sut = CreateSut();
        sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });

        sut.SetPinned("test-plugin", true);

        var data = sut.Load();
        data.Plugins[0].Pinned.Should().BeTrue();
    }

    [Fact]
    public void FindById_ReturnsMatchingEntry()
    {
        var sut = CreateSut();
        sut.AddPlugin("alpha", "1.0.0", new[] { "A.dll" });
        sut.AddPlugin("beta", "2.0.0", new[] { "B.dll" });

        var entry = sut.FindById("beta");

        entry.Should().NotBeNull();
        entry!.InstalledVersion.Should().Be("2.0.0");
    }

    [Fact]
    public void FindById_NotFound_ReturnsNull()
    {
        var sut = CreateSut();

        var entry = sut.FindById("nonexistent");

        entry.Should().BeNull();
    }

    [Fact]
    public void IsInstalled_ReturnsTrueForInstalled()
    {
        var sut = CreateSut();
        sut.AddPlugin("test-plugin", "1.0.0", new[] { "Test.dll" });

        sut.IsInstalled("test-plugin").Should().BeTrue();
        sut.IsInstalled("other").Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~InstalledPluginTrackerTests" -v minimal`
Expected: Build errors — `IInstalledPluginTracker`, `InstalledPluginTracker` don't exist yet.

- [ ] **Step 3: Create IInstalledPluginTracker interface**

```csharp
// TabgInstaller.Core/Services/IInstalledPluginTracker.cs
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services;

public interface IInstalledPluginTracker
{
    InstalledPluginsData Load();
    void AddPlugin(string id, string version, string[] dllNames);
    void UpdatePluginVersion(string id, string newVersion);
    void RemovePlugin(string id);
    void SetPinned(string id, bool pinned);
    InstalledPluginEntry? FindById(string id);
    bool IsInstalled(string id);
}
```

- [ ] **Step 4: Create InstalledPluginTracker implementation**

```csharp
// TabgInstaller.Core/Services/InstalledPluginTracker.cs
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services;

public class InstalledPluginTracker : IInstalledPluginTracker
{
    private readonly string _filePath;
    private InstalledPluginsData? _cached;

    public InstalledPluginTracker(string serverRoot)
    {
        _filePath = Path.Combine(serverRoot, "BepInEx", "plugins", "community", "installed-plugins.json");
    }

    public InstalledPluginsData Load()
    {
        if (_cached != null) return _cached;

        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _cached = JsonConvert.DeserializeObject<InstalledPluginsData>(json) ?? new InstalledPluginsData();
                return _cached;
            }
        }
        catch (Exception)
        {
            // Corrupted file — return fresh data
        }

        _cached = new InstalledPluginsData();
        return _cached;
    }

    public void AddPlugin(string id, string version, string[] dllNames)
    {
        var data = Load();
        data.Plugins.RemoveAll(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        data.Plugins.Add(new InstalledPluginEntry
        {
            Id = id,
            InstalledVersion = version,
            InstalledAt = DateTime.UtcNow.ToString("o"),
            Pinned = false,
            DllNames = dllNames
        });
        Save(data);
    }

    public void UpdatePluginVersion(string id, string newVersion)
    {
        var data = Load();
        var entry = data.Plugins.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return;

        entry.InstalledVersion = newVersion;
        entry.UpdatedAt = DateTime.UtcNow.ToString("o");
        Save(data);
    }

    public void RemovePlugin(string id)
    {
        var data = Load();
        data.Plugins.RemoveAll(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        Save(data);
    }

    public void SetPinned(string id, bool pinned)
    {
        var data = Load();
        var entry = data.Plugins.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return;

        entry.Pinned = pinned;
        Save(data);
    }

    public InstalledPluginEntry? FindById(string id)
    {
        var data = Load();
        return data.Plugins.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsInstalled(string id)
    {
        return FindById(id) != null;
    }

    private void Save(InstalledPluginsData data)
    {
        _cached = data;
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception)
        {
            // Non-critical write failure
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~InstalledPluginTrackerTests" -v minimal`
Expected: All 8 tests pass.

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Core/Services/IInstalledPluginTracker.cs TabgInstaller.Core/Services/InstalledPluginTracker.cs TabgInstaller.Tests/Services/InstalledPluginTrackerTests.cs
git commit -m "feat(marketplace): add InstalledPluginTracker for per-server community plugin tracking"
```

---

## Task 5: MarketplaceInstallService

**Files:**
- Create: `TabgInstaller.Core/Services/IMarketplaceInstallService.cs`
- Create: `TabgInstaller.Core/Services/MarketplaceInstallService.cs`
- Test: `TabgInstaller.Tests/Services/MarketplaceInstallServiceTests.cs`

- [ ] **Step 1: Write tests for dependency resolution and install logic**

```csharp
// TabgInstaller.Tests/Services/MarketplaceInstallServiceTests.cs
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using Xunit;

namespace TabgInstaller.Tests.Services;

public class MarketplaceInstallServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _communityDir;
    private readonly InstalledPluginTracker _tracker;

    public MarketplaceInstallServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
        _communityDir = Path.Combine(_tempDir, "BepInEx", "plugins", "community");
        Directory.CreateDirectory(_communityDir);
        _tracker = new InstalledPluginTracker(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static PluginManifest MakeManifest(string id, string version = "1.0.0", params string[] deps)
    {
        return new PluginManifest
        {
            Id = id,
            Name = id,
            Version = version,
            Description = "Test",
            Author = "Test",
            DownloadUrl = $"https://github.com/test/{id}/releases/latest",
            DllNames = new[] { $"{id}.dll" },
            Type = "server",
            CompatibleTabgVersions = new[] { "*" },
            MinInstallerVersion = "4.0.0",
            BepInExVersion = "5.4.22",
            Dependencies = deps
        };
    }

    // ── Dependency resolution ──────────────────────────────────

    [Fact]
    public void ResolveDependencies_NoDeps_ReturnsOnlyPlugin()
    {
        var plugin = MakeManifest("solo");
        var registry = new List<PluginManifest> { plugin };

        var result = MarketplaceInstallService.ResolveDependencies(plugin, registry, _tracker);

        result.Should().ContainSingle(m => m.Id == "solo");
    }

    [Fact]
    public void ResolveDependencies_WithDeps_ReturnsDepsFirst()
    {
        var dep = MakeManifest("dep-lib");
        var plugin = MakeManifest("main-plugin", "1.0.0", "dep-lib");
        var registry = new List<PluginManifest> { dep, plugin };

        var result = MarketplaceInstallService.ResolveDependencies(plugin, registry, _tracker);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("dep-lib");
        result[1].Id.Should().Be("main-plugin");
    }

    [Fact]
    public void ResolveDependencies_DepAlreadyInstalled_SkipsDep()
    {
        _tracker.AddPlugin("dep-lib", "1.0.0", new[] { "dep-lib.dll" });

        var dep = MakeManifest("dep-lib");
        var plugin = MakeManifest("main-plugin", "1.0.0", "dep-lib");
        var registry = new List<PluginManifest> { dep, plugin };

        var result = MarketplaceInstallService.ResolveDependencies(plugin, registry, _tracker);

        result.Should().ContainSingle(m => m.Id == "main-plugin");
    }

    [Fact]
    public void ResolveDependencies_CircularDep_DoesNotInfiniteLoop()
    {
        var a = MakeManifest("alpha", "1.0.0", "beta");
        var b = MakeManifest("beta", "1.0.0", "alpha");
        var registry = new List<PluginManifest> { a, b };

        var result = MarketplaceInstallService.ResolveDependencies(a, registry, _tracker);

        result.Select(m => m.Id).Should().Contain("alpha").And.Contain("beta");
        result.Should().HaveCount(2); // no duplicates
    }

    [Fact]
    public void ResolveDependencies_BundledDep_SkipsIt()
    {
        // "citruslib" is a bundled plugin — should not appear in install list
        var plugin = MakeManifest("my-plugin", "1.0.0", "Citruslib");
        var registry = new List<PluginManifest> { plugin };

        var result = MarketplaceInstallService.ResolveDependencies(plugin, registry, _tracker);

        result.Should().ContainSingle(m => m.Id == "my-plugin");
    }

    // ── HasUpdate ──────────────────────────────────────────────

    [Fact]
    public void HasUpdate_NewerVersion_ReturnsTrue()
    {
        _tracker.AddPlugin("test", "1.0.0", new[] { "test.dll" });
        var manifest = MakeManifest("test", "2.0.0");

        MarketplaceInstallService.HasUpdate(manifest, _tracker).Should().BeTrue();
    }

    [Fact]
    public void HasUpdate_SameVersion_ReturnsFalse()
    {
        _tracker.AddPlugin("test", "1.0.0", new[] { "test.dll" });
        var manifest = MakeManifest("test", "1.0.0");

        MarketplaceInstallService.HasUpdate(manifest, _tracker).Should().BeFalse();
    }

    [Fact]
    public void HasUpdate_Pinned_ReturnsFalse()
    {
        _tracker.AddPlugin("test", "1.0.0", new[] { "test.dll" });
        _tracker.SetPinned("test", true);
        var manifest = MakeManifest("test", "2.0.0");

        MarketplaceInstallService.HasUpdate(manifest, _tracker).Should().BeFalse();
    }

    [Fact]
    public void HasUpdate_NotInstalled_ReturnsFalse()
    {
        var manifest = MakeManifest("test", "1.0.0");

        MarketplaceInstallService.HasUpdate(manifest, _tracker).Should().BeFalse();
    }

    // ── GetCommunityPluginDir ──────────────────────────────────

    [Fact]
    public void GetCommunityPluginDir_Server_ReturnsCorrectPath()
    {
        var dir = MarketplaceInstallService.GetCommunityPluginDir(_tempDir, null, "test-plugin", "server");

        dir.Should().Be(Path.Combine(_tempDir, "BepInEx", "plugins", "community", "test-plugin"));
    }

    [Fact]
    public void GetCommunityPluginDir_Client_ReturnsClientPath()
    {
        var clientPath = Path.Combine(_tempDir, "client");

        var dir = MarketplaceInstallService.GetCommunityPluginDir(null, clientPath, "test-plugin", "client");

        dir.Should().Be(Path.Combine(clientPath, "BepInEx", "plugins", "community", "test-plugin"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~MarketplaceInstallServiceTests" -v minimal`
Expected: Build errors — `MarketplaceInstallService` doesn't exist yet.

- [ ] **Step 3: Create IMarketplaceInstallService interface**

```csharp
// TabgInstaller.Core/Services/IMarketplaceInstallService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services;

public interface IMarketplaceInstallService
{
    /// <summary>Installs a community plugin and its missing dependencies. Returns true on success.</summary>
    Task<bool> InstallPluginAsync(PluginManifest manifest, List<PluginManifest> registryPlugins,
        string serverRoot, string? clientModdedPath);

    /// <summary>Updates a community plugin to its latest version. Backs up old DLLs first.</summary>
    Task<bool> UpdatePluginAsync(PluginManifest manifest, string serverRoot, string? clientModdedPath);

    /// <summary>Uninstalls a community plugin by deleting its folder and removing from tracker.</summary>
    bool UninstallPlugin(string pluginId, string serverRoot, string? clientModdedPath);
}
```

- [ ] **Step 4: Create MarketplaceInstallService implementation**

```csharp
// TabgInstaller.Core/Services/MarketplaceInstallService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services;

public class MarketplaceInstallService : IMarketplaceInstallService
{
    private readonly GitHubService _gitHub;
    private readonly IInstalledPluginTracker _tracker;

    public MarketplaceInstallService(GitHubService gitHub, IInstalledPluginTracker tracker)
    {
        _gitHub = gitHub;
        _tracker = tracker;
    }

    /// <summary>
    /// Resolves the full dependency chain for a plugin. Returns the ordered install list
    /// (dependencies first, target plugin last). Skips already-installed and bundled plugins.
    /// </summary>
    public static List<PluginManifest> ResolveDependencies(
        PluginManifest target,
        List<PluginManifest> registryPlugins,
        IInstalledPluginTracker tracker)
    {
        var result = new List<PluginManifest>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(PluginManifest plugin)
        {
            if (visited.Contains(plugin.Id)) return;
            visited.Add(plugin.Id);

            foreach (var depId in plugin.Dependencies)
            {
                // Skip bundled plugins
                if (PluginRegistry.FindById(depId) != null) continue;

                // Skip already-installed community plugins
                if (tracker.IsInstalled(depId)) continue;

                var dep = registryPlugins.FirstOrDefault(
                    p => p.Id.Equals(depId, StringComparison.OrdinalIgnoreCase));
                if (dep != null)
                    Visit(dep);
            }

            result.Add(plugin);
        }

        Visit(target);
        return result;
    }

    /// <summary>
    /// Checks if a manifest has a newer version than what's installed.
    /// Returns false if not installed or if pinned.
    /// </summary>
    public static bool HasUpdate(PluginManifest manifest, IInstalledPluginTracker tracker)
    {
        var entry = tracker.FindById(manifest.Id);
        if (entry == null) return false;
        if (entry.Pinned) return false;

        return CompareVersions(manifest.Version, entry.InstalledVersion) > 0;
    }

    /// <summary>
    /// Returns the correct community plugin directory for a given plugin type.
    /// </summary>
    public static string GetCommunityPluginDir(string? serverRoot, string? clientModdedPath, string pluginId, string type)
    {
        var basePath = type == "client" ? clientModdedPath : serverRoot;
        return Path.Combine(basePath!, "BepInEx", "plugins", "community", pluginId);
    }

    public async Task<bool> InstallPluginAsync(
        PluginManifest manifest,
        List<PluginManifest> registryPlugins,
        string serverRoot,
        string? clientModdedPath)
    {
        var toInstall = ResolveDependencies(manifest, registryPlugins, _tracker);

        foreach (var plugin in toInstall)
        {
            var success = await DownloadAndPlacePlugin(plugin, serverRoot, clientModdedPath);
            if (!success) return false;

            _tracker.AddPlugin(plugin.Id, plugin.Version, plugin.DllNames);
        }

        return true;
    }

    public async Task<bool> UpdatePluginAsync(
        PluginManifest manifest,
        string serverRoot,
        string? clientModdedPath)
    {
        // Backup old files
        var pluginDir = GetInstallDir(manifest, serverRoot, clientModdedPath);
        var backupDir = Path.Combine(pluginDir, ".backup");

        try
        {
            if (Directory.Exists(pluginDir))
            {
                if (Directory.Exists(backupDir))
                    Directory.Delete(backupDir, true);
                Directory.CreateDirectory(backupDir);

                foreach (var file in Directory.GetFiles(pluginDir, "*.dll"))
                    File.Copy(file, Path.Combine(backupDir, Path.GetFileName(file)));
            }

            var success = await DownloadAndPlacePlugin(manifest, serverRoot, clientModdedPath);
            if (!success)
            {
                // Restore from backup
                RestoreBackup(backupDir, pluginDir);
                return false;
            }

            _tracker.UpdatePluginVersion(manifest.Id, manifest.Version);

            // Clean up backup
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);

            return true;
        }
        catch (Exception)
        {
            RestoreBackup(backupDir, pluginDir);
            return false;
        }
    }

    public bool UninstallPlugin(string pluginId, string serverRoot, string? clientModdedPath)
    {
        try
        {
            var entry = _tracker.FindById(pluginId);
            if (entry == null) return false;

            // Try server path
            var serverDir = Path.Combine(serverRoot, "BepInEx", "plugins", "community", pluginId);
            if (Directory.Exists(serverDir))
                Directory.Delete(serverDir, true);

            // Try client path
            if (clientModdedPath != null)
            {
                var clientDir = Path.Combine(clientModdedPath, "BepInEx", "plugins", "community", pluginId);
                if (Directory.Exists(clientDir))
                    Directory.Delete(clientDir, true);
            }

            _tracker.RemovePlugin(pluginId);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> DownloadAndPlacePlugin(
        PluginManifest manifest,
        string serverRoot,
        string? clientModdedPath)
    {
        // Parse owner/repo from downloadUrl
        // Expected format: https://github.com/{owner}/{repo}/releases/latest
        if (!TryParseGitHubUrl(manifest.DownloadUrl, out var owner, out var repo))
            return false;

        var release = await _gitHub.GetLatestReleaseAsync(owner, repo);
        if (release == null) return false;

        var installDir = GetInstallDir(manifest, serverRoot, clientModdedPath);
        Directory.CreateDirectory(installDir);

        foreach (var dllName in manifest.DllNames)
        {
            var asset = release.Assets.FirstOrDefault(a =>
                a.Name.Equals(dllName, StringComparison.OrdinalIgnoreCase));

            if (asset == null) return false;

            var destPath = Path.Combine(installDir, dllName);
            var success = await _gitHub.DownloadAssetAsync(
                owner, repo, asset.BrowserDownloadUrl, destPath, installDir, null);
            if (!success) return false;
        }

        // If type is "both", also install to the other path
        if (manifest.Type == "both" && clientModdedPath != null)
        {
            var clientDir = Path.Combine(clientModdedPath, "BepInEx", "plugins", "community", manifest.Id);
            Directory.CreateDirectory(clientDir);
            foreach (var dllName in manifest.DllNames)
            {
                var srcPath = Path.Combine(installDir, dllName);
                var destPath = Path.Combine(clientDir, dllName);
                if (File.Exists(srcPath))
                    File.Copy(srcPath, destPath, true);
            }
        }

        return true;
    }

    private string GetInstallDir(PluginManifest manifest, string serverRoot, string? clientModdedPath)
    {
        if (manifest.Type == "client" && clientModdedPath != null)
            return GetCommunityPluginDir(null, clientModdedPath, manifest.Id, "client");
        return GetCommunityPluginDir(serverRoot, null, manifest.Id, "server");
    }

    private static void RestoreBackup(string backupDir, string pluginDir)
    {
        try
        {
            if (!Directory.Exists(backupDir)) return;
            foreach (var file in Directory.GetFiles(backupDir, "*.dll"))
                File.Copy(file, Path.Combine(pluginDir, Path.GetFileName(file)), true);
            Directory.Delete(backupDir, true);
        }
        catch (Exception)
        {
            // Best effort
        }
    }

    private static bool TryParseGitHubUrl(string url, out string owner, out string repo)
    {
        owner = "";
        repo = "";
        try
        {
            // https://github.com/{owner}/{repo}/releases/latest
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                owner = segments[0];
                repo = segments[1];
                return true;
            }
        }
        catch (Exception)
        {
            // Invalid URL
        }
        return false;
    }

    /// <summary>
    /// Simple semver comparison: compares major.minor.patch numerically.
    /// Returns positive if a > b, negative if a < b, 0 if equal.
    /// </summary>
    private static int CompareVersions(string a, string b)
    {
        var aParts = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var bParts = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var len = Math.Max(aParts.Length, bParts.Length);

        for (int i = 0; i < len; i++)
        {
            var av = i < aParts.Length ? aParts[i] : 0;
            var bv = i < bParts.Length ? bParts[i] : 0;
            if (av != bv) return av - bv;
        }
        return 0;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~MarketplaceInstallServiceTests" -v minimal`
Expected: All 11 tests pass.

- [ ] **Step 6: Commit**

```bash
git add TabgInstaller.Core/Services/IMarketplaceInstallService.cs TabgInstaller.Core/Services/MarketplaceInstallService.cs TabgInstaller.Tests/Services/MarketplaceInstallServiceTests.cs
git commit -m "feat(marketplace): add MarketplaceInstallService with dependency resolution, install, update, uninstall"
```

---

## Task 6: PluginCardViewModel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/PluginCardViewModel.cs`
- Test: `TabgInstaller.Tests/ViewModels/PluginCardViewModelTests.cs`

- [ ] **Step 1: Write tests for PluginCardViewModel**

```csharp
// TabgInstaller.Tests/ViewModels/PluginCardViewModelTests.cs
using FluentAssertions;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels;

public class PluginCardViewModelTests
{
    private static PluginManifest MakeManifest(string id = "test", string version = "1.0.0", string type = "server")
    {
        return new PluginManifest
        {
            Id = id,
            Name = "Test Plugin",
            Version = version,
            Description = "A test plugin for testing.",
            Author = "TestAuthor",
            DownloadUrl = "https://github.com/Test/Plugin/releases/latest",
            DllNames = new[] { "Test.dll" },
            Type = type,
            CompatibleTabgVersions = new[] { "*" },
            MinInstallerVersion = "4.0.0",
            BepInExVersion = "5.4.22",
            Tags = new[] { "test", "demo" }
        };
    }

    [Fact]
    public void Constructor_NotInstalled_StatusIsAvailable()
    {
        var vm = new PluginCardViewModel(MakeManifest(), null, "4.0.0");

        vm.InstallStatus.Should().Be(PluginInstallStatus.Available);
        vm.ActionButtonText.Should().Be("Install");
        vm.IsActionEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_InstalledSameVersion_StatusIsInstalled()
    {
        var entry = new InstalledPluginEntry
        {
            Id = "test", InstalledVersion = "1.0.0", DllNames = new[] { "Test.dll" }
        };

        var vm = new PluginCardViewModel(MakeManifest(), entry, "4.0.0");

        vm.InstallStatus.Should().Be(PluginInstallStatus.Installed);
        vm.ActionButtonText.Should().Be("Installed ✓");
        vm.IsActionEnabled.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NewerVersionAvailable_StatusIsUpdateAvailable()
    {
        var entry = new InstalledPluginEntry
        {
            Id = "test", InstalledVersion = "0.9.0", DllNames = new[] { "Test.dll" }
        };

        var vm = new PluginCardViewModel(MakeManifest(), entry, "4.0.0");

        vm.InstallStatus.Should().Be(PluginInstallStatus.UpdateAvailable);
        vm.ActionButtonText.Should().Be("Update (0.9.0 → 1.0.0)");
        vm.IsActionEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_IncompatibleInstallerVersion_StatusIsIncompatible()
    {
        var manifest = MakeManifest();
        manifest.MinInstallerVersion = "5.0.0";

        var vm = new PluginCardViewModel(manifest, null, "4.0.0");

        vm.InstallStatus.Should().Be(PluginInstallStatus.Incompatible);
        vm.IsActionEnabled.Should().BeFalse();
        vm.IncompatibilityReason.Should().Contain("5.0.0");
    }

    [Fact]
    public void Constructor_Pinned_ShowsPinned()
    {
        var entry = new InstalledPluginEntry
        {
            Id = "test", InstalledVersion = "0.9.0", Pinned = true, DllNames = new[] { "Test.dll" }
        };

        var vm = new PluginCardViewModel(MakeManifest(), entry, "4.0.0");

        vm.IsPinned.Should().BeTrue();
        // Pinned means no update even though newer version exists
        vm.InstallStatus.Should().Be(PluginInstallStatus.Installed);
    }

    [Fact]
    public void TypeBadge_ReturnsCorrectString()
    {
        var vm = new PluginCardViewModel(MakeManifest(type: "server"), null, "4.0.0");
        vm.TypeBadge.Should().Be("Server");

        vm = new PluginCardViewModel(MakeManifest(type: "client"), null, "4.0.0");
        vm.TypeBadge.Should().Be("Client");

        vm = new PluginCardViewModel(MakeManifest(type: "both"), null, "4.0.0");
        vm.TypeBadge.Should().Be("Both");
    }

    [Fact]
    public void MatchesSearch_ByName_ReturnsTrue()
    {
        var vm = new PluginCardViewModel(MakeManifest(), null, "4.0.0");

        vm.MatchesSearch("test plugin").Should().BeTrue();
        vm.MatchesSearch("xyz").Should().BeFalse();
    }

    [Fact]
    public void MatchesSearch_ByTag_ReturnsTrue()
    {
        var vm = new PluginCardViewModel(MakeManifest(), null, "4.0.0");

        vm.MatchesSearch("demo").Should().BeTrue();
    }

    [Fact]
    public void MatchesSearch_EmptyQuery_ReturnsTrue()
    {
        var vm = new PluginCardViewModel(MakeManifest(), null, "4.0.0");

        vm.MatchesSearch("").Should().BeTrue();
        vm.MatchesSearch(null!).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~PluginCardViewModelTests" -v minimal`
Expected: Build errors — `PluginCardViewModel`, `PluginInstallStatus` don't exist.

- [ ] **Step 3: Create PluginCardViewModel**

```csharp
// TabgInstaller.Gui/ViewModels/PluginCardViewModel.cs
using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.ViewModels;

public enum PluginInstallStatus
{
    Available,
    Installed,
    UpdateAvailable,
    Incompatible
}

public partial class PluginCardViewModel : ObservableObject
{
    public PluginManifest Manifest { get; }

    [ObservableProperty] private PluginInstallStatus _installStatus;
    [ObservableProperty] private string _actionButtonText = "";
    [ObservableProperty] private bool _isActionEnabled;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private string _incompatibilityReason = "";
    [ObservableProperty] private bool _isInstalled;

    public string TypeBadge => Manifest.Type switch
    {
        "server" => "Server",
        "client" => "Client",
        "both" => "Both",
        _ => Manifest.Type
    };

    public PluginCardViewModel(PluginManifest manifest, InstalledPluginEntry? installed, string currentInstallerVersion)
    {
        Manifest = manifest;
        _isPinned = installed?.Pinned ?? false;
        _isInstalled = installed != null;

        // Determine compatibility
        if (!IsCompatible(manifest, currentInstallerVersion, out var reason))
        {
            _installStatus = PluginInstallStatus.Incompatible;
            _actionButtonText = "Incompatible";
            _isActionEnabled = false;
            _incompatibilityReason = reason;
            return;
        }

        if (installed == null)
        {
            _installStatus = PluginInstallStatus.Available;
            _actionButtonText = "Install";
            _isActionEnabled = true;
        }
        else if (_isPinned || CompareVersions(manifest.Version, installed.InstalledVersion) <= 0)
        {
            _installStatus = PluginInstallStatus.Installed;
            _actionButtonText = "Installed ✓";
            _isActionEnabled = false;
        }
        else
        {
            _installStatus = PluginInstallStatus.UpdateAvailable;
            _actionButtonText = $"Update ({installed.InstalledVersion} → {manifest.Version})";
            _isActionEnabled = true;
        }
    }

    public bool MatchesSearch(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;

        var q = query.ToLowerInvariant();
        return Manifest.Name.ToLowerInvariant().Contains(q)
            || Manifest.Description.ToLowerInvariant().Contains(q)
            || Manifest.Author.ToLowerInvariant().Contains(q)
            || Manifest.Tags.Any(t => t.ToLowerInvariant().Contains(q));
    }

    private static bool IsCompatible(PluginManifest manifest, string currentInstallerVersion, out string reason)
    {
        reason = "";
        if (CompareVersions(currentInstallerVersion, manifest.MinInstallerVersion) < 0)
        {
            reason = $"Requires installer version {manifest.MinInstallerVersion} or newer.";
            return false;
        }
        return true;
    }

    private static int CompareVersions(string a, string b)
    {
        var aParts = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var bParts = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var len = Math.Max(aParts.Length, bParts.Length);

        for (int i = 0; i < len; i++)
        {
            var av = i < aParts.Length ? aParts[i] : 0;
            var bv = i < bParts.Length ? bParts[i] : 0;
            if (av != bv) return av - bv;
        }
        return 0;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~PluginCardViewModelTests" -v minimal`
Expected: All 10 tests pass.

- [ ] **Step 5: Commit**

```bash
git add TabgInstaller.Gui/ViewModels/PluginCardViewModel.cs TabgInstaller.Tests/ViewModels/PluginCardViewModelTests.cs
git commit -m "feat(marketplace): add PluginCardViewModel with install status, compatibility check, and search matching"
```

---

## Task 7: BrowsePluginsViewModel

**Files:**
- Create: `TabgInstaller.Gui/ViewModels/BrowsePluginsViewModel.cs`
- Test: `TabgInstaller.Tests/ViewModels/BrowsePluginsViewModelTests.cs`

- [ ] **Step 1: Write tests for BrowsePluginsViewModel**

```csharp
// TabgInstaller.Tests/ViewModels/BrowsePluginsViewModelTests.cs
using FluentAssertions;
using Moq;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels;

public class BrowsePluginsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IRegistryService> _registry = new();
    private readonly Mock<IMarketplaceInstallService> _installer = new();
    private readonly Mock<IActiveInstanceService> _activeInstance = new();
    private readonly Mock<IAppSettingsService> _appSettings = new();
    private readonly Mock<IToastService> _toast = new();
    private readonly InstalledPluginTracker _tracker;

    public BrowsePluginsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TabgTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_tempDir, "BepInEx", "plugins", "community"));
        _tracker = new InstalledPluginTracker(_tempDir);

        _activeInstance.SetupGet(a => a.ServerPath).Returns(_tempDir);
        _appSettings.Setup(a => a.Load()).Returns(new AppSettings { ClientModdedPath = "" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private BrowsePluginsViewModel CreateSut() =>
        new(_registry.Object, _installer.Object, _tracker, _activeInstance.Object, _appSettings.Object, _toast.Object);

    private static PluginManifest MakeManifest(string id, string version = "1.0.0", string type = "server")
    {
        return new PluginManifest
        {
            Id = id, Name = $"Plugin {id}", Version = version, Description = $"Desc for {id}",
            Author = "Author", DownloadUrl = $"https://github.com/a/{id}/releases/latest",
            DllNames = new[] { $"{id}.dll" }, Type = type,
            CompatibleTabgVersions = new[] { "*" }, MinInstallerVersion = "4.0.0",
            BepInExVersion = "5.4.22", Tags = new[] { id }
        };
    }

    // ── Initial State ──────────────────────────────────────────

    [Fact]
    public void Constructor_PluginCardsEmpty()
    {
        var sut = CreateSut();

        sut.FilteredPlugins.Should().BeEmpty();
        sut.SearchText.Should().Be("");
        sut.SelectedCategory.Should().Be("All");
    }

    // ── Loading ────────────────────────────────────────────────

    [Fact]
    public async Task LoadPluginsAsync_PopulatesCards()
    {
        var registry = new PluginRegistryResponse
        {
            Version = 1, GeneratedAt = "2026-04-10T12:00:00Z",
            Plugins = new List<PluginManifest> { MakeManifest("alpha"), MakeManifest("beta") }
        };
        _registry.Setup(r => r.FetchRegistryAsync()).ReturnsAsync(registry);

        var sut = CreateSut();
        await sut.LoadPluginsAsync();

        sut.AllPluginCards.Should().HaveCount(2);
        sut.FilteredPlugins.Should().HaveCount(2);
    }

    // ── Filtering ──────────────────────────────────────────────

    [Fact]
    public async Task SearchText_FiltersPlugins()
    {
        var registry = new PluginRegistryResponse
        {
            Version = 1, GeneratedAt = "2026-04-10T12:00:00Z",
            Plugins = new List<PluginManifest> { MakeManifest("alpha"), MakeManifest("beta") }
        };
        _registry.Setup(r => r.FetchRegistryAsync()).ReturnsAsync(registry);

        var sut = CreateSut();
        await sut.LoadPluginsAsync();

        sut.SearchText = "alpha";

        sut.FilteredPlugins.Should().ContainSingle(c => c.Manifest.Id == "alpha");
    }

    [Fact]
    public async Task SelectedCategory_FiltersPlugins()
    {
        var registry = new PluginRegistryResponse
        {
            Version = 1, GeneratedAt = "2026-04-10T12:00:00Z",
            Plugins = new List<PluginManifest> { MakeManifest("srv", type: "server"), MakeManifest("cli", type: "client") }
        };
        _registry.Setup(r => r.FetchRegistryAsync()).ReturnsAsync(registry);

        var sut = CreateSut();
        await sut.LoadPluginsAsync();

        sut.SelectedCategory = "Server";
        sut.FilteredPlugins.Should().ContainSingle(c => c.Manifest.Id == "srv");

        sut.SelectedCategory = "Client";
        sut.FilteredPlugins.Should().ContainSingle(c => c.Manifest.Id == "cli");

        sut.SelectedCategory = "All";
        sut.FilteredPlugins.Should().HaveCount(2);
    }

    // ── Update Count ───────────────────────────────────────────

    [Fact]
    public async Task UpdateCount_ReflectsOutdatedPlugins()
    {
        _tracker.AddPlugin("alpha", "0.5.0", new[] { "alpha.dll" });

        var registry = new PluginRegistryResponse
        {
            Version = 1, GeneratedAt = "2026-04-10T12:00:00Z",
            Plugins = new List<PluginManifest> { MakeManifest("alpha", "1.0.0"), MakeManifest("beta") }
        };
        _registry.Setup(r => r.FetchRegistryAsync()).ReturnsAsync(registry);

        var sut = CreateSut();
        await sut.LoadPluginsAsync();

        sut.UpdateCount.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~BrowsePluginsViewModelTests" -v minimal`
Expected: Build errors — `BrowsePluginsViewModel` doesn't exist.

- [ ] **Step 3: Create BrowsePluginsViewModel**

```csharp
// TabgInstaller.Gui/ViewModels/BrowsePluginsViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.ViewModels;

public partial class BrowsePluginsViewModel : ObservableObject
{
    private readonly IRegistryService _registry;
    private readonly IMarketplaceInstallService _installer;
    private readonly IInstalledPluginTracker _tracker;
    private readonly IActiveInstanceService _activeInstance;
    private readonly IAppSettingsService _appSettings;
    private readonly IToastService _toast;

    private static readonly string CurrentInstallerVersion = "4.0.0";

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private string _selectedSort = "A-Z";
    [ObservableProperty] private int _updateCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private PluginCardViewModel? _selectedPlugin;

    public List<PluginCardViewModel> AllPluginCards { get; private set; } = new();
    public ObservableCollection<PluginCardViewModel> FilteredPlugins { get; } = new();

    public string[] Categories { get; } = { "All", "Server", "Client" };
    public string[] SortOptions { get; } = { "A-Z", "Recently Updated", "Newest" };

    public BrowsePluginsViewModel(
        IRegistryService registry,
        IMarketplaceInstallService installer,
        IInstalledPluginTracker tracker,
        IActiveInstanceService activeInstance,
        IAppSettingsService appSettings,
        IToastService toast)
    {
        _registry = registry;
        _installer = installer;
        _tracker = tracker;
        _activeInstance = activeInstance;
        _appSettings = appSettings;
        _toast = toast;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();

    public async Task LoadPluginsAsync()
    {
        IsLoading = true;
        StatusText = "Fetching plugin registry...";

        var response = await _registry.FetchRegistryAsync();
        if (response == null)
        {
            StatusText = "Plugin marketplace unavailable.";
            IsLoading = false;
            return;
        }

        AllPluginCards = response.Plugins.Select(manifest =>
        {
            var installed = _tracker.FindById(manifest.Id);
            return new PluginCardViewModel(manifest, installed, CurrentInstallerVersion);
        }).ToList();

        UpdateCount = AllPluginCards.Count(c => c.InstallStatus == PluginInstallStatus.UpdateAvailable);
        StatusText = "";
        IsLoading = false;

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = AllPluginCards.AsEnumerable();

        // Search
        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(c => c.MatchesSearch(SearchText));

        // Category
        if (SelectedCategory != "All")
        {
            var type = SelectedCategory.ToLowerInvariant();
            filtered = filtered.Where(c =>
                c.Manifest.Type == type || c.Manifest.Type == "both");
        }

        // Sort
        filtered = SelectedSort switch
        {
            "A-Z" => filtered.OrderBy(c => c.Manifest.Name, StringComparer.OrdinalIgnoreCase),
            "Recently Updated" => filtered.OrderByDescending(c => c.Manifest.Version),
            "Newest" => filtered.OrderByDescending(c => c.Manifest.Version),
            _ => filtered.OrderBy(c => c.Manifest.Name, StringComparer.OrdinalIgnoreCase)
        };

        FilteredPlugins.Clear();
        foreach (var card in filtered)
            FilteredPlugins.Add(card);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadPluginsAsync();
        _toast.Info("Plugin registry refreshed.");
    }

    [RelayCommand]
    private async Task InstallPluginAsync(PluginCardViewModel card)
    {
        if (card.InstallStatus != PluginInstallStatus.Available) return;

        var serverRoot = _activeInstance.ServerPath;
        var clientPath = _appSettings.Load().ClientModdedPath;
        var registry = _registry.GetCachedRegistry();
        if (registry == null) return;

        // Check dependencies
        var toInstall = MarketplaceInstallService.ResolveDependencies(
            card.Manifest, registry.Plugins, _tracker);

        var success = await _installer.InstallPluginAsync(
            card.Manifest, registry.Plugins, serverRoot, clientPath);

        if (success)
        {
            _toast.Success($"{card.Manifest.Name} installed successfully.");
            await LoadPluginsAsync(); // Refresh cards
        }
        else
        {
            _toast.Error($"Failed to install {card.Manifest.Name}.");
        }
    }

    [RelayCommand]
    private async Task UpdatePluginAsync(PluginCardViewModel card)
    {
        if (card.InstallStatus != PluginInstallStatus.UpdateAvailable) return;

        var serverRoot = _activeInstance.ServerPath;
        var clientPath = _appSettings.Load().ClientModdedPath;

        var success = await _installer.UpdatePluginAsync(
            card.Manifest, serverRoot, clientPath);

        if (success)
        {
            _toast.Success($"{card.Manifest.Name} updated to {card.Manifest.Version}.");
            await LoadPluginsAsync();
        }
        else
        {
            _toast.Error($"Failed to update {card.Manifest.Name}.");
        }
    }

    [RelayCommand]
    private async Task UninstallPluginAsync(PluginCardViewModel card)
    {
        if (!card.IsInstalled) return;

        var serverRoot = _activeInstance.ServerPath;
        var clientPath = _appSettings.Load().ClientModdedPath;

        var success = _installer.UninstallPlugin(card.Manifest.Id, serverRoot, clientPath);

        if (success)
        {
            _toast.Success($"{card.Manifest.Name} uninstalled.");
            await LoadPluginsAsync();
        }
        else
        {
            _toast.Error($"Failed to uninstall {card.Manifest.Name}.");
        }
    }

    [RelayCommand]
    private async Task UpdateAllAsync()
    {
        var serverRoot = _activeInstance.ServerPath;
        var clientPath = _appSettings.Load().ClientModdedPath;

        var updatable = AllPluginCards
            .Where(c => c.InstallStatus == PluginInstallStatus.UpdateAvailable)
            .ToList();

        int updated = 0;
        foreach (var card in updatable)
        {
            var success = await _installer.UpdatePluginAsync(card.Manifest, serverRoot, clientPath);
            if (success) updated++;
        }

        _toast.Success($"Updated {updated} of {updatable.Count} plugins.");
        await LoadPluginsAsync();
    }

    [RelayCommand]
    private void TogglePin(PluginCardViewModel card)
    {
        var newPinned = !card.IsPinned;
        _tracker.SetPinned(card.Manifest.Id, newPinned);
        card.IsPinned = newPinned;

        // Refresh to recalculate status
        var installed = _tracker.FindById(card.Manifest.Id);
        var refreshed = new PluginCardViewModel(card.Manifest, installed, CurrentInstallerVersion);
        var idx = AllPluginCards.FindIndex(c => c.Manifest.Id == card.Manifest.Id);
        if (idx >= 0) AllPluginCards[idx] = refreshed;
        ApplyFilters();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TabgInstaller.Tests --filter "FullyQualifiedName~BrowsePluginsViewModelTests" -v minimal`
Expected: All 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add TabgInstaller.Gui/ViewModels/BrowsePluginsViewModel.cs TabgInstaller.Tests/ViewModels/BrowsePluginsViewModelTests.cs
git commit -m "feat(marketplace): add BrowsePluginsViewModel with search, filter, sort, install, update, and uninstall commands"
```

---

## Task 8: Localization Strings

**Files:**
- Modify: `TabgInstaller.Gui/Resources/Strings.resx`

- [ ] **Step 1: Add marketplace localization strings to Strings.resx**

Add these entries to `TabgInstaller.Gui/Resources/Strings.resx` (before the closing `</root>` tag):

```xml
  <data name="TabBrowsePlugins" xml:space="preserve">
    <value>Browse Plugins</value>
  </data>
  <data name="BrowsePluginsTitle" xml:space="preserve">
    <value>Plugin Marketplace</value>
  </data>
  <data name="BrowsePluginsSubtitle" xml:space="preserve">
    <value>Discover and install community plugins for your server</value>
  </data>
  <data name="SearchPlugins" xml:space="preserve">
    <value>Search plugins...</value>
  </data>
  <data name="CheckForUpdates" xml:space="preserve">
    <value>Check for Updates</value>
  </data>
  <data name="UpdateAll" xml:space="preserve">
    <value>Update All</value>
  </data>
  <data name="CategoryAll" xml:space="preserve">
    <value>All</value>
  </data>
  <data name="CategoryServer" xml:space="preserve">
    <value>Server</value>
  </data>
  <data name="CategoryClient" xml:space="preserve">
    <value>Client</value>
  </data>
  <data name="PluginUpdatesAvailable" xml:space="preserve">
    <value>{0} plugin update(s) available</value>
  </data>
  <data name="MarketplaceUnavailable" xml:space="preserve">
    <value>Plugin marketplace unavailable — check your internet connection.</value>
  </data>
  <data name="Uninstall" xml:space="preserve">
    <value>Uninstall</value>
  </data>
  <data name="PinVersion" xml:space="preserve">
    <value>Pin to version {0}</value>
  </data>
  <data name="UnpinVersion" xml:space="preserve">
    <value>Unpin version</value>
  </data>
  <data name="ViewOnGitHub" xml:space="preserve">
    <value>View on GitHub</value>
  </data>
  <data name="NoLongerInRegistry" xml:space="preserve">
    <value>No longer in registry</value>
  </data>
```

Also add matching entries to `Strings.de.resx` if it exists (with German translations).

- [ ] **Step 2: Verify build succeeds**

Run: `dotnet build TabgInstaller.Gui`
Expected: Build succeeds. The `Strings.Designer.cs` file is auto-generated.

- [ ] **Step 3: Commit**

```bash
git add TabgInstaller.Gui/Resources/Strings.resx
git commit -m "feat(marketplace): add localization strings for Browse Plugins tab"
```

---

## Task 9: BrowsePluginsPanel XAML

**Files:**
- Create: `TabgInstaller.Gui/Tabs/BrowsePluginsPanel.xaml`
- Create: `TabgInstaller.Gui/Tabs/BrowsePluginsPanel.xaml.cs`

- [ ] **Step 1: Create BrowsePluginsPanel.xaml**

```xml
<!-- TabgInstaller.Gui/Tabs/BrowsePluginsPanel.xaml -->
<UserControl x:Class="TabgInstaller.Gui.Tabs.BrowsePluginsPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:TabgInstaller.Gui.Converters"
             xmlns:res="clr-namespace:TabgInstaller.Gui.Resources"
             xmlns:vm="clr-namespace:TabgInstaller.Gui.ViewModels">
    <UserControl.Resources>
        <conv:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
        <conv:BoolToVisibilityConverter x:Key="InverseBoolToVisibility" Inverse="True"/>
    </UserControl.Resources>

    <Grid Margin="10" KeyboardNavigation.TabNavigation="Cycle">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <StackPanel Grid.Row="0" Margin="0,0,0,10">
            <TextBlock Text="{x:Static res:Strings.BrowsePluginsTitle}" FontSize="18" FontWeight="SemiBold"/>
            <TextBlock Text="{x:Static res:Strings.BrowsePluginsSubtitle}"
                       Foreground="{DynamicResource SecondaryTextBrush}" FontSize="12"/>
        </StackPanel>

        <!-- Top bar: search + filters + actions -->
        <Grid Grid.Row="1" Margin="0,0,0,10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBox Grid.Column="0" Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
                     VerticalContentAlignment="Center" Padding="6,4"
                     AutomationProperties.Name="Search plugins"
                     AutomationProperties.AutomationId="PluginSearchBox"
                     TabIndex="10">
                <TextBox.Style>
                    <Style TargetType="TextBox">
                        <Style.Triggers>
                            <Trigger Property="Text" Value="">
                                <Setter Property="Background">
                                    <Setter.Value>
                                        <VisualBrush Stretch="None" AlignmentX="Left" AlignmentY="Center">
                                            <VisualBrush.Visual>
                                                <TextBlock Text="{x:Static res:Strings.SearchPlugins}"
                                                           Foreground="Gray" Margin="6,0,0,0"/>
                                            </VisualBrush.Visual>
                                        </VisualBrush>
                                    </Setter.Value>
                                </Setter>
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </TextBox.Style>
            </TextBox>

            <ComboBox Grid.Column="1" ItemsSource="{Binding Categories}" SelectedItem="{Binding SelectedCategory}"
                      Width="90" Margin="6,0,0,0"
                      AutomationProperties.Name="Filter by category"
                      AutomationProperties.AutomationId="CategoryComboBox"
                      TabIndex="20"/>

            <ComboBox Grid.Column="2" ItemsSource="{Binding SortOptions}" SelectedItem="{Binding SelectedSort}"
                      Width="140" Margin="6,0,0,0"
                      AutomationProperties.Name="Sort by"
                      AutomationProperties.AutomationId="SortComboBox"
                      TabIndex="30"/>

            <Button Grid.Column="3" Content="{x:Static res:Strings.CheckForUpdates}"
                    Command="{Binding RefreshCommand}" Width="130" Margin="6,0,0,0"
                    AutomationProperties.AutomationId="RefreshRegistryButton"
                    TabIndex="40"/>

            <Button Grid.Column="4" Content="{x:Static res:Strings.UpdateAll}"
                    Command="{Binding UpdateAllCommand}" Width="90" Margin="6,0,0,0"
                    Visibility="{Binding HasUpdates, Converter={StaticResource BoolToVisibility}}"
                    AutomationProperties.AutomationId="UpdateAllButton"
                    TabIndex="41"/>
        </Grid>

        <!-- Plugin cards list -->
        <Grid Grid.Row="2">
            <!-- Loading indicator -->
            <TextBlock Text="Loading plugins..."
                       HorizontalAlignment="Center" VerticalAlignment="Center"
                       Foreground="{DynamicResource SecondaryTextBrush}"
                       Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibility}}"/>

            <!-- Status text (errors) -->
            <TextBlock Text="{Binding StatusText}"
                       HorizontalAlignment="Center" VerticalAlignment="Center"
                       Foreground="{DynamicResource SecondaryTextBrush}"
                       Visibility="{Binding IsLoading, Converter={StaticResource InverseBoolToVisibility}}"/>

            <!-- Plugin cards -->
            <ListView ItemsSource="{Binding FilteredPlugins}" SelectedItem="{Binding SelectedPlugin}"
                      BorderThickness="0" ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                      Visibility="{Binding IsLoading, Converter={StaticResource InverseBoolToVisibility}}"
                      AutomationProperties.Name="Community plugins"
                      AutomationProperties.AutomationId="PluginCardsListView"
                      TabIndex="50">
                <ListView.ItemTemplate>
                    <DataTemplate DataType="{x:Type vm:PluginCardViewModel}">
                        <Border BorderBrush="{DynamicResource SecondaryTextBrush}" BorderThickness="0,0,0,1"
                                Padding="8" Margin="0,2">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                </Grid.RowDefinitions>

                                <!-- Name + Author -->
                                <StackPanel Grid.Row="0" Grid.Column="0" Orientation="Horizontal">
                                    <TextBlock Text="{Binding Manifest.Name}" FontWeight="SemiBold" FontSize="14"/>
                                    <TextBlock Text="{Binding Manifest.Author, StringFormat=' by {0}'}"
                                               Foreground="{DynamicResource SecondaryTextBrush}" FontSize="11"
                                               VerticalAlignment="Bottom" Margin="4,0,0,2"/>
                                </StackPanel>

                                <!-- Description -->
                                <TextBlock Grid.Row="1" Grid.Column="0" Text="{Binding Manifest.Description}"
                                           TextTrimming="CharacterEllipsis" MaxHeight="36"
                                           Foreground="{DynamicResource SecondaryTextBrush}" FontSize="12"
                                           Margin="0,2,10,0"/>

                                <!-- Tags + badges -->
                                <StackPanel Grid.Row="2" Grid.Column="0" Orientation="Horizontal" Margin="0,4,0,0">
                                    <!-- Type badge -->
                                    <Border Background="{DynamicResource AccentBrush}" CornerRadius="3" Padding="6,2" Margin="0,0,4,0">
                                        <TextBlock Text="{Binding TypeBadge}" FontSize="10" Foreground="White"/>
                                    </Border>
                                    <!-- Pin icon -->
                                    <TextBlock Text="📌" FontSize="12" Margin="4,0"
                                               Visibility="{Binding IsPinned, Converter={StaticResource BoolToVisibility}}"
                                               ToolTip="Version pinned"/>
                                </StackPanel>

                                <!-- Action button -->
                                <Button Grid.Row="0" Grid.RowSpan="3" Grid.Column="1"
                                        Content="{Binding ActionButtonText}"
                                        IsEnabled="{Binding IsActionEnabled}"
                                        Width="160" VerticalAlignment="Center"
                                        AutomationProperties.Name="{Binding ActionButtonText}"
                                        Command="{Binding DataContext.InstallOrUpdateCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                        CommandParameter="{Binding}"/>
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>
        </Grid>

        <!-- Detail panel (shown when a plugin is selected) -->
        <Border Grid.Row="2" Background="{DynamicResource BackgroundBrush}"
                HorizontalAlignment="Right" Width="320" Padding="12"
                BorderBrush="{DynamicResource SecondaryTextBrush}" BorderThickness="1,0,0,0"
                Visibility="{Binding HasSelectedPlugin, Converter={StaticResource BoolToVisibility}}">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel DataContext="{Binding SelectedPlugin}">
                    <TextBlock Text="{Binding Manifest.Name}" FontSize="16" FontWeight="SemiBold"/>
                    <TextBlock Text="{Binding Manifest.Author, StringFormat='by {0}'}"
                               Foreground="{DynamicResource SecondaryTextBrush}" FontSize="11" Margin="0,2,0,8"/>
                    <TextBlock Text="{Binding Manifest.Description}" TextWrapping="Wrap" FontSize="12" Margin="0,0,0,10"/>

                    <!-- Version -->
                    <TextBlock FontSize="11" Margin="0,0,0,4">
                        <Run Text="Version: " FontWeight="SemiBold"/>
                        <Run Text="{Binding Manifest.Version, Mode=OneWay}"/>
                    </TextBlock>

                    <!-- Dependencies -->
                    <TextBlock FontSize="11" Margin="0,0,0,4"
                               Visibility="{Binding HasDependencies, Converter={StaticResource BoolToVisibility}}">
                        <Run Text="Dependencies: " FontWeight="SemiBold"/>
                        <Run Text="{Binding DependenciesText, Mode=OneWay}"/>
                    </TextBlock>

                    <!-- Changelog -->
                    <TextBlock Text="Changelog" FontWeight="SemiBold" FontSize="11" Margin="0,8,0,2"
                               Visibility="{Binding HasChangelog, Converter={StaticResource BoolToVisibility}}"/>
                    <TextBlock Text="{Binding Manifest.Changelog}" TextWrapping="Wrap" FontSize="11"
                               Foreground="{DynamicResource SecondaryTextBrush}"
                               Visibility="{Binding HasChangelog, Converter={StaticResource BoolToVisibility}}"/>

                    <!-- Actions -->
                    <StackPanel Margin="0,12,0,0">
                        <Button Content="{Binding ActionButtonText}" IsEnabled="{Binding IsActionEnabled}"
                                Command="{Binding DataContext.InstallOrUpdateCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                CommandParameter="{Binding}"
                                Margin="0,0,0,4" HorizontalAlignment="Stretch"/>
                        <Button Content="{x:Static res:Strings.Uninstall}"
                                Command="{Binding DataContext.UninstallPluginCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                CommandParameter="{Binding}"
                                Visibility="{Binding IsInstalled, Converter={StaticResource BoolToVisibility}}"
                                Margin="0,0,0,4" HorizontalAlignment="Stretch"/>
                        <CheckBox Content="Pin version" IsChecked="{Binding IsPinned}"
                                  Command="{Binding DataContext.TogglePinCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                  CommandParameter="{Binding}"
                                  Visibility="{Binding IsInstalled, Converter={StaticResource BoolToVisibility}}"
                                  Margin="0,4,0,4"/>
                    </StackPanel>
                </StackPanel>
            </ScrollViewer>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create BrowsePluginsPanel.xaml.cs code-behind**

```csharp
// TabgInstaller.Gui/Tabs/BrowsePluginsPanel.xaml.cs
using System.Windows.Controls;

namespace TabgInstaller.Gui.Tabs;

public partial class BrowsePluginsPanel : UserControl
{
    public BrowsePluginsPanel()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: Add helper properties to BrowsePluginsViewModel for XAML bindings**

Add these properties and the `InstallOrUpdateCommand` to `BrowsePluginsViewModel.cs`:

```csharp
public bool HasUpdates => UpdateCount > 0;
public bool HasSelectedPlugin => SelectedPlugin != null;

partial void OnUpdateCountChanged(int value) => OnPropertyChanged(nameof(HasUpdates));
partial void OnSelectedPluginChanged(PluginCardViewModel? value) => OnPropertyChanged(nameof(HasSelectedPlugin));

[RelayCommand]
private async Task InstallOrUpdateAsync(PluginCardViewModel card)
{
    if (card.InstallStatus == PluginInstallStatus.Available)
        await InstallPluginAsync(card);
    else if (card.InstallStatus == PluginInstallStatus.UpdateAvailable)
        await UpdatePluginAsync(card);
}
```

Add these helper properties to `PluginCardViewModel.cs`:

```csharp
public bool HasDependencies => Manifest.Dependencies.Length > 0;
public string DependenciesText => string.Join(", ", Manifest.Dependencies);
public bool HasChangelog => !string.IsNullOrEmpty(Manifest.Changelog);
```

- [ ] **Step 4: Verify build succeeds**

Run: `dotnet build TabgInstaller.Gui`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add TabgInstaller.Gui/Tabs/BrowsePluginsPanel.xaml TabgInstaller.Gui/Tabs/BrowsePluginsPanel.xaml.cs TabgInstaller.Gui/ViewModels/BrowsePluginsViewModel.cs TabgInstaller.Gui/ViewModels/PluginCardViewModel.cs
git commit -m "feat(marketplace): add BrowsePluginsPanel XAML with search, filter, plugin cards, and detail panel"
```

---

## Task 10: Wire Up — MainWindow, DI, Dashboard

**Files:**
- Modify: `TabgInstaller.Gui/MainWindow.xaml`
- Modify: `TabgInstaller.Gui/MainWindow.xaml.cs`
- Modify: `TabgInstaller.Gui/App.xaml.cs`
- Modify: `TabgInstaller.Gui/ViewModels/DashboardViewModel.cs`

- [ ] **Step 1: Add Browse Plugins tab to MainWindow.xaml**

In `MainWindow.xaml`, add the new tab between the Reference tab and Settings tab:

```xml
                <TabItem Header="{x:Static res:Strings.TabReference}"
                         AutomationProperties.AutomationId="ReferenceTabItem">
                    <tabs:ReferencePanel x:Name="ReferenceTab"/>
                </TabItem>

                <TabItem Header="{x:Static res:Strings.TabBrowsePlugins}"
                         AutomationProperties.AutomationId="BrowsePluginsTabItem">
                    <tabs:BrowsePluginsPanel x:Name="BrowsePluginsTab"/>
                </TabItem>

                <TabItem Header="{x:Static res:Strings.TabSettings}"
                         AutomationProperties.AutomationId="SettingsTabItem">
                    <tabs:SettingsPanel x:Name="SettingsTab"/>
                </TabItem>
```

- [ ] **Step 2: Register new services and ViewModel in App.xaml.cs**

Add to the `ConfigureServices` method in `App.xaml.cs`, after the existing core services section:

```csharp
            // Marketplace services
            services.AddSingleton<GitHubService>(sp =>
                new GitHubService(new HttpClient(), new Progress<string>(msg =>
                    Debug.WriteLine($"[GitHub] {msg}"))));
            services.AddSingleton<IRegistryService>(sp =>
            {
                var cachePath = Path.Combine(settingsDir, "registry-cache.json");
                return new RegistryService(sp.GetRequiredService<GitHubService>(), cachePath);
            });
            services.AddTransient<IInstalledPluginTracker>(sp =>
            {
                var active = sp.GetRequiredService<IActiveInstanceService>();
                return new InstalledPluginTracker(active.ServerPath);
            });
            services.AddTransient<IMarketplaceInstallService>(sp =>
                new MarketplaceInstallService(
                    sp.GetRequiredService<GitHubService>(),
                    sp.GetRequiredService<IInstalledPluginTracker>()));
```

Add to the ViewModels section:

```csharp
            services.AddTransient<BrowsePluginsViewModel>();
```

Add required `using` statements at the top of `App.xaml.cs`:

```csharp
using System.Net.Http;
using TabgInstaller.Core.Model;
```

- [ ] **Step 3: Initialize BrowsePluginsPanel in MainWindow.xaml.cs**

In the `InitializeAllPanels()` method, add after the `ReferenceTab` line:

```csharp
            var browsePluginsVm = _services.GetRequiredService<BrowsePluginsViewModel>();
            BrowsePluginsTab.DataContext = browsePluginsVm;
            _ = browsePluginsVm.LoadPluginsAsync(); // Fire-and-forget on startup
```

Also update the `OpenFullConsole` command's tab index in `DashboardViewModel.cs` if needed (Console tab is now at index 4, Browse Plugins is at index 8, Settings is at index 9).

- [ ] **Step 4: Add plugin updates info to DashboardViewModel**

Add a new property and update `RefreshHealthCards()` in `DashboardViewModel.cs`:

Add field and property:
```csharp
        private readonly IRegistryService _registryService;
        private readonly IInstalledPluginTracker _pluginTracker;

        [ObservableProperty] private string _pluginUpdatesText = "";
        [ObservableProperty] private bool _hasPluginUpdates;
```

Update the constructor to accept new dependencies and assign the new fields:
```csharp
        public DashboardViewModel(
            IActiveInstanceService activeInstance,
            IAppSettingsService appSettings,
            INavigationService navigation,
            IToastService toast,
            IServerInstanceManager instanceManager,
            IRegistryService registryService,
            IInstalledPluginTracker pluginTracker)
        {
            _activeInstance = activeInstance;
            _appSettings = appSettings;
            _navigation = navigation;
            _toast = toast;
            _instanceManager = instanceManager;
            _registryService = registryService;
            _pluginTracker = pluginTracker;

            _activeInstance.PathChanged += OnServerPathChanged;
        }
```

Add a method to check for plugin updates:
```csharp
        private void RefreshPluginUpdates()
        {
            var registry = _registryService.GetCachedRegistry();
            if (registry == null) return;

            int count = 0;
            foreach (var manifest in registry.Plugins)
            {
                if (MarketplaceInstallService.HasUpdate(manifest, _pluginTracker))
                    count++;
            }

            HasPluginUpdates = count > 0;
            PluginUpdatesText = count > 0
                ? string.Format("{0} plugin update(s) available", count)
                : "";
        }
```

Call `RefreshPluginUpdates()` at the end of `RefreshPreview()`.

- [ ] **Step 5: Verify build succeeds**

Run: `dotnet build TabgInstaller.Gui`
Expected: Build succeeds.

- [ ] **Step 6: Run all existing tests to ensure nothing broke**

Run: `dotnet test TabgInstaller.Tests -v minimal`
Expected: All existing tests still pass. DashboardViewModel tests may need updates for the new constructor parameters — add mocks for `IRegistryService` and `IInstalledPluginTracker`.

- [ ] **Step 7: Commit**

```bash
git add TabgInstaller.Gui/MainWindow.xaml TabgInstaller.Gui/MainWindow.xaml.cs TabgInstaller.Gui/App.xaml.cs TabgInstaller.Gui/ViewModels/DashboardViewModel.cs
git commit -m "feat(marketplace): wire Browse Plugins tab into MainWindow, DI, and Dashboard health card"
```

---

## Task 11: Registry Repo Structure

**Files:**
- Create: `registry/schema/plugin-manifest.schema.json`
- Create: `registry/TEMPLATE.json`
- Create: `registry/CONTRIBUTING.md`

- [ ] **Step 1: Create JSON Schema for manifest validation**

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "TABG Plugin Manifest",
  "description": "Schema for community plugin manifests in the TABG Server Installer plugin marketplace.",
  "type": "object",
  "required": ["id", "name", "version", "description", "author", "downloadUrl", "dllNames", "type", "compatibleTabgVersions", "minInstallerVersion", "bepInExVersion"],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$",
      "description": "Unique kebab-case identifier, 3-50 characters."
    },
    "name": { "type": "string", "minLength": 1, "maxLength": 100 },
    "version": {
      "type": "string",
      "pattern": "^\\d+\\.\\d+\\.\\d+$",
      "description": "Semantic version (major.minor.patch)."
    },
    "description": { "type": "string", "minLength": 1, "maxLength": 500 },
    "author": { "type": "string", "minLength": 1 },
    "authorUrl": { "type": "string", "format": "uri" },
    "repositoryUrl": { "type": "string", "format": "uri" },
    "downloadUrl": {
      "type": "string",
      "format": "uri",
      "pattern": "^https://github\\.com/.+/releases"
    },
    "dllNames": {
      "type": "array",
      "items": { "type": "string", "pattern": "\\.dll$" },
      "minItems": 1
    },
    "type": { "type": "string", "enum": ["server", "client", "both"] },
    "compatibleTabgVersions": {
      "type": "array",
      "items": { "type": "string" },
      "minItems": 1
    },
    "minInstallerVersion": { "type": "string", "pattern": "^\\d+\\.\\d+\\.\\d+$" },
    "bepInExVersion": { "type": "string" },
    "dependencies": {
      "type": "array",
      "items": { "type": "string" },
      "default": []
    },
    "tags": {
      "type": "array",
      "items": { "type": "string" },
      "default": []
    },
    "iconUrl": { "type": "string", "format": "uri" },
    "requiresClientMod": { "type": "boolean", "default": false },
    "clientPluginId": { "type": "string" },
    "changelog": { "type": "string" }
  },
  "if": { "properties": { "requiresClientMod": { "const": true } } },
  "then": { "required": ["clientPluginId"] },
  "additionalProperties": false
}
```

Write to: `registry/schema/plugin-manifest.schema.json`

- [ ] **Step 2: Create manifest template**

```json
{
  "id": "your-plugin-id",
  "name": "Your Plugin Name",
  "version": "1.0.0",
  "description": "A short description of what your plugin does.",
  "author": "YourGitHubUsername",
  "authorUrl": "https://github.com/YourGitHubUsername",
  "repositoryUrl": "https://github.com/YourGitHubUsername/YourPlugin",
  "downloadUrl": "https://github.com/YourGitHubUsername/YourPlugin/releases/latest",
  "dllNames": ["YourPlugin.dll"],
  "type": "server",
  "compatibleTabgVersions": ["*"],
  "minInstallerVersion": "4.0.0",
  "bepInExVersion": "5.4.22",
  "dependencies": [],
  "tags": [],
  "requiresClientMod": false
}
```

Write to: `registry/TEMPLATE.json`

- [ ] **Step 3: Create CONTRIBUTING.md**

```markdown
# Contributing a Plugin to the TABG Marketplace

## Prerequisites

- Your plugin is hosted on GitHub with releases containing the DLL files.
- Your plugin works with BepInEx 5.4.22 on a TABG dedicated server.

## Steps

### 1. Fork this repository

Fork `user1342554/TABG-Server-Installer` on GitHub.

### 2. Create your manifest

Copy `registry/TEMPLATE.json` to `registry/plugins/<your-plugin-id>/manifest.json`.

Replace `<your-plugin-id>` with a unique kebab-case identifier (e.g., `my-cool-plugin`).

### 3. Fill in your manifest

Edit the manifest with your plugin's details. See `registry/schema/plugin-manifest.schema.json` for the full field reference.

**Required fields:** `id`, `name`, `version`, `description`, `author`, `downloadUrl`, `dllNames`, `type`, `compatibleTabgVersions`, `minInstallerVersion`, `bepInExVersion`.

**Important:**
- `id` must match your folder name exactly.
- `downloadUrl` must point to your GitHub releases (e.g., `https://github.com/you/plugin/releases/latest`).
- `dllNames` must list every DLL file in your release that should be installed.
- `version` must be valid semver (e.g., `1.0.0`).

### 4. Validate locally (optional)

You can validate your manifest against the schema using any JSON Schema validator:

```bash
npx ajv-cli validate -s registry/schema/plugin-manifest.schema.json -d registry/plugins/your-plugin-id/manifest.json
```

### 5. Submit a Pull Request

Push your branch and open a PR. The CI will automatically validate your manifest and post results as a comment.

### 6. Wait for review

A maintainer will review your submission and merge it. Once merged, your plugin will appear in the app's Browse Plugins tab.

## Updating Your Plugin

To release a new version:

1. Create a new GitHub release with your updated DLLs.
2. Submit a PR updating the `version` field in your manifest.

## Plugin Types

| Type | Install Location |
|------|-----------------|
| `server` | Server's `BepInEx/plugins/community/<id>/` |
| `client` | Client's `BepInEx/plugins/community/<id>/` |
| `both` | Both locations |

## Questions?

Open an issue on this repository.
```

Write to: `registry/CONTRIBUTING.md`

- [ ] **Step 4: Commit**

```bash
git add registry/schema/plugin-manifest.schema.json registry/TEMPLATE.json registry/CONTRIBUTING.md
git commit -m "feat(marketplace): add registry structure — JSON Schema, template, and contributing guide"
```

---

## Task 12: GitHub Actions

**Files:**
- Create: `.github/workflows/registry-validate.yml`
- Create: `.github/workflows/registry-build.yml`

- [ ] **Step 1: Create PR validation workflow**

```yaml
# .github/workflows/registry-validate.yml
name: Validate Plugin Manifests

on:
  pull_request:
    paths:
      - 'registry/plugins/**'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install ajv-cli
        run: npm install -g ajv-cli ajv-formats

      - name: Validate manifests
        id: validate
        run: |
          SCHEMA="registry/schema/plugin-manifest.schema.json"
          ERRORS=""
          SUCCESS=true

          for manifest in registry/plugins/*/manifest.json; do
            PLUGIN_DIR=$(dirname "$manifest")
            PLUGIN_ID=$(basename "$PLUGIN_DIR")
            MANIFEST_ID=$(jq -r '.id' "$manifest")

            echo "Validating $manifest..."

            # Check ID matches folder name
            if [ "$PLUGIN_ID" != "$MANIFEST_ID" ]; then
              ERRORS="$ERRORS\n❌ **$PLUGIN_ID**: manifest id '$MANIFEST_ID' does not match folder name '$PLUGIN_ID'"
              SUCCESS=false
              continue
            fi

            # Validate against schema
            if ! ajv validate -s "$SCHEMA" -d "$manifest" --spec=draft2020 -c ajv-formats 2>/tmp/ajv_err; then
              ERR=$(cat /tmp/ajv_err)
              ERRORS="$ERRORS\n❌ **$PLUGIN_ID**: Schema validation failed\n\`\`\`\n$ERR\n\`\`\`"
              SUCCESS=false
              continue
            fi

            # Verify downloadUrl is reachable
            DOWNLOAD_URL=$(jq -r '.downloadUrl' "$manifest")
            HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -L "$DOWNLOAD_URL" || echo "000")
            if [ "$HTTP_CODE" != "200" ]; then
              ERRORS="$ERRORS\n⚠️ **$PLUGIN_ID**: downloadUrl returned HTTP $HTTP_CODE"
            fi

            echo "✅ $PLUGIN_ID passed"
          done

          # Check for duplicate IDs
          DUPES=$(find registry/plugins -name manifest.json -exec jq -r '.id' {} \; | sort | uniq -d)
          if [ -n "$DUPES" ]; then
            ERRORS="$ERRORS\n❌ **Duplicate IDs found:** $DUPES"
            SUCCESS=false
          fi

          if [ "$SUCCESS" = true ]; then
            echo "RESULT=✅ All manifests validated successfully." >> $GITHUB_OUTPUT
          else
            echo "RESULT<<EOF" >> $GITHUB_OUTPUT
            echo -e "## ❌ Manifest Validation Failed\n$ERRORS" >> $GITHUB_OUTPUT
            echo "EOF" >> $GITHUB_OUTPUT
          fi

          echo "SUCCESS=$SUCCESS" >> $GITHUB_OUTPUT

      - name: Comment on PR
        uses: actions/github-script@v7
        with:
          script: |
            const result = `${{ steps.validate.outputs.RESULT }}`;
            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              body: `### Plugin Manifest Validation\n\n${result}`
            });

      - name: Fail if validation errors
        if: steps.validate.outputs.SUCCESS != 'true'
        run: exit 1
```

- [ ] **Step 2: Create post-merge build workflow**

```yaml
# .github/workflows/registry-build.yml
name: Build Plugin Registry

on:
  push:
    branches: [main]
    paths:
      - 'registry/plugins/**'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Compile registry.json
        run: |
          echo '{"version":1,"generatedAt":"'"$(date -u +%Y-%m-%dT%H:%M:%SZ)"'","plugins":[' > /tmp/registry_parts.json

          FIRST=true
          for manifest in registry/plugins/*/manifest.json; do
            if [ "$FIRST" = true ]; then
              FIRST=false
            else
              echo ',' >> /tmp/registry_parts.json
            fi
            cat "$manifest" >> /tmp/registry_parts.json
          done

          echo ']}' >> /tmp/registry_parts.json

          # Pretty-print with jq
          jq '.' /tmp/registry_parts.json > registry/registry.json

      - name: Commit registry.json
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add registry/registry.json
          if git diff --cached --quiet; then
            echo "No changes to registry.json"
          else
            git commit -m "chore: auto-generate registry.json [skip ci]"
            git push
          fi
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/registry-validate.yml .github/workflows/registry-build.yml
git commit -m "feat(marketplace): add GitHub Actions for manifest validation and registry compilation"
```

---

## Task 13: Integration Smoke Test & Final Verification

**Files:**
- All files from previous tasks

- [ ] **Step 1: Run full test suite**

Run: `dotnet test TabgInstaller.Tests -v minimal`
Expected: All tests pass (existing + new). Fix any failures from DashboardViewModel constructor changes by updating mocks in `DashboardViewModelTests.cs`.

- [ ] **Step 2: Build the entire solution**

Run: `dotnet build`
Expected: Clean build with no errors.

- [ ] **Step 3: Verify the registry directory structure**

Run: `ls registry/` and `ls .github/workflows/`
Expected:
```
registry/
├── CONTRIBUTING.md
├── TEMPLATE.json
├── schema/
│   └── plugin-manifest.schema.json

.github/workflows/
├── registry-validate.yml
├── registry-build.yml
```

- [ ] **Step 4: Create a test plugin manifest to validate the schema**

Create `registry/plugins/example-plugin/manifest.json` with valid content to test the GitHub Action locally (optional). Delete after testing.

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "feat(marketplace): Phase 6 Plugin Marketplace — complete implementation"
```
