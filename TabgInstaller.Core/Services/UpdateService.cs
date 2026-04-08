using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public record UpdateInfo(string TagName, Version Version, string DownloadUrl, string? ReleaseNotes);

    public class UpdateService : IUpdateService
    {
        private const string Owner = "user1342554";
        private const string Repo = "TABG-Server-Installer";

        private readonly HttpClient _http;

        public UpdateService()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("TabgInstaller/1.0");
        }

        public static Version GetCurrentVersion()
        {
            var asm = Assembly.GetEntryAssembly();
            return asm?.GetName().Version ?? new Version(0, 0, 0);
        }

        /// <summary>Returns update info if a newer release exists, null otherwise.</summary>
        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                var release = await _http.GetFromJsonAsync<GitHubReleaseDto>(
                    $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

                if (release == null) return null;

                var remoteVersion = ParseVersion(release.TagName);
                if (remoteVersion == null) return null;

                var current = GetCurrentVersion();
                if (remoteVersion <= current) return null;

                // Find the zip asset
                var zipAsset = release.Assets?.FirstOrDefault(a =>
                    a.Name != null && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                if (zipAsset?.BrowserDownloadUrl == null) return null;

                return new UpdateInfo(release.TagName, remoteVersion, zipAsset.BrowserDownloadUrl, release.Body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateService] Update check failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>Downloads the update zip, extracts, and launches a script to replace files and restart.</summary>
        public async Task<bool> ApplyUpdateAsync(string downloadUrl, IProgress<string>? log = null)
        {
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var tempDir = Path.Combine(Path.GetTempPath(), "TabgInstaller_Update");
                var zipPath = Path.Combine(Path.GetTempPath(), "TabgInstaller_Update.zip");

                // Clean up previous update attempts
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                if (File.Exists(zipPath)) File.Delete(zipPath);

                // Download
                log?.Report("Downloading update...");
                using (var response = await _http.GetAsync(downloadUrl))
                {
                    response.EnsureSuccessStatusCode();
                    await using var fs = File.Create(zipPath);
                    await response.Content.CopyToAsync(fs);
                }

                // Extract
                log?.Report("Extracting update...");
                ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);

                // Handle nested folder: if the zip contains a single subfolder with
                // the actual files, use that folder as the source instead of tempDir.
                var sourceDir = tempDir;
                var topEntries = Directory.GetFileSystemEntries(tempDir);
                if (topEntries.Length == 1 && Directory.Exists(topEntries[0]))
                {
                    // ZIP had a single wrapper folder (e.g. "publish/" or "TABG-Server-Installer/")
                    sourceDir = topEntries[0];
                    log?.Report($"Detected nested folder in ZIP, using: {Path.GetFileName(sourceDir)}");
                }

                // Write updater batch script
                var scriptPath = Path.Combine(Path.GetTempPath(), "TabgInstaller_Updater.bat");
                var exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "TabgInstaller.Gui.exe");

                var script = $@"@echo off
echo Updating TABG Installer...
timeout /t 2 /nobreak >nul
REM Wait until the EXE is no longer locked (up to 15 seconds)
set RETRIES=0
:waitloop
copy /b /y ""{Path.Combine(sourceDir, exeName)}"" ""{Path.Combine(appDir, exeName)}"" >nul 2>&1
if not errorlevel 1 goto docopy
set /a RETRIES+=1
if %RETRIES% GEQ 15 goto docopy
timeout /t 1 /nobreak >nul
goto waitloop
:docopy
xcopy /s /y /q ""{sourceDir}\*"" ""{appDir}""
echo Update complete. Restarting...
start """" ""{Path.Combine(appDir, exeName)}""
rd /s /q ""{tempDir}"" 2>nul
del ""{zipPath}"" 2>nul
del ""%~f0"" 2>nul
";
                File.WriteAllText(scriptPath, script);

                // Launch script and exit
                log?.Report("Applying update and restarting...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch (Exception ex)
            {
                log?.Report($"Update failed: {ex.Message}");
                return false;
            }
        }

        private static Version? ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            // Strip common prefixes: "v1.2.0", "V1.2.0"
            var s = tag.TrimStart('v', 'V');
            if (!Version.TryParse(s, out var v)) return null;
            // Normalize to 4-component version so "1.3.0" (Revision=-1) doesn't
            // compare as less than assembly version "1.3.0.0" (Revision=0)
            if (v.Revision < 0)
                v = new Version(v.Major, v.Minor, v.Build, 0);
            return v;
        }

        private class GitHubReleaseDto
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = "";

            [JsonPropertyName("body")]
            public string? Body { get; set; }

            [JsonPropertyName("assets")]
            public GitHubAssetDto[]? Assets { get; set; }
        }

        private class GitHubAssetDto
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}
