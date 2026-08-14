using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TabgInstaller.App.Services;

public sealed record LocalServerProfile(Guid Id, string DisplayName, string ServerPath)
{
    public override string ToString() => DisplayName;
}

public sealed class ServerProfileStore
{
    private readonly string _filePath;
    private ServerProfileDocument _document;

    public ServerProfileStore(string storageDirectory)
    {
        _filePath = Path.Combine(storageDirectory, "server-profiles.json");
        _document = LoadDocument();
    }

    public IReadOnlyList<LocalServerProfile> Profiles
        => _document.Profiles
            .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public Guid? ActiveProfileId => _document.ActiveProfileId;

    public LocalServerProfile? ActiveProfile
        => _document.ActiveProfileId is Guid id
            ? _document.Profiles.FirstOrDefault(profile => profile.Id == id)
            : null;

    public LocalServerProfile AddOrUpdate(string serverPath, string? displayName = null)
    {
        var normalizedPath = NormalizePath(serverPath);
        if (normalizedPath.Length == 0)
            throw new ArgumentException("Server path is required.", nameof(serverPath));

        var existing = _document.Profiles.FirstOrDefault(profile =>
            PathsEqual(profile.ServerPath, normalizedPath));
        var name = string.IsNullOrWhiteSpace(displayName)
            ? BuildDisplayName(normalizedPath)
            : displayName.Trim();
        LocalServerProfile profile;
        if (existing == null)
        {
            profile = new LocalServerProfile(Guid.NewGuid(), name, normalizedPath);
            _document.Profiles.Add(profile);
        }
        else
        {
            profile = existing with { DisplayName = name, ServerPath = normalizedPath };
            var index = _document.Profiles.IndexOf(existing);
            _document.Profiles[index] = profile;
        }

        _document.ActiveProfileId = profile.Id;
        SaveDocument();
        return profile;
    }

    public bool SetActive(Guid profileId)
    {
        if (_document.Profiles.All(profile => profile.Id != profileId))
            return false;

        _document.ActiveProfileId = profileId;
        SaveDocument();
        return true;
    }

    public bool Remove(Guid profileId)
    {
        var removed = _document.Profiles.RemoveAll(profile => profile.Id == profileId) > 0;
        if (!removed)
            return false;

        if (_document.ActiveProfileId == profileId)
            _document.ActiveProfileId = _document.Profiles.FirstOrDefault()?.Id;
        SaveDocument();
        return true;
    }

    private ServerProfileDocument LoadDocument()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new ServerProfileDocument();

            return JsonSerializer.Deserialize<ServerProfileDocument>(File.ReadAllText(_filePath), JsonOptions)
                ?? new ServerProfileDocument();
        }
        catch (JsonException)
        {
            return new ServerProfileDocument();
        }
        catch (IOException)
        {
            return new ServerProfileDocument();
        }
    }

    private void SaveDocument()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_document, JsonOptions));
        File.Move(temporaryPath, _filePath, true);
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length == 0)
            return string.Empty;
        return Path.GetFullPath(trimmed).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(NormalizePath(left), NormalizePath(right), comparison);
    }

    private static string BuildDisplayName(string path)
    {
        var name = new DirectoryInfo(path).Name;
        return string.IsNullOrWhiteSpace(name) ? "TABG Server" : name;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class ServerProfileDocument
    {
        public List<LocalServerProfile> Profiles { get; set; } = new();

        public Guid? ActiveProfileId { get; set; }
    }
}
