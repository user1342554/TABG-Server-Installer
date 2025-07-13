using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using Octokit;

namespace TabgInstaller.Core.Services
{
    public class CommunityServerService
    {
        private readonly IProgress<string> _log;
        private readonly GitHubClient _githubClient;
        private readonly HttpClient _httpClient;

        public CommunityServerService(IProgress<string> log)
        {
            _log = log;
            _githubClient = new GitHubClient(new ProductHeaderValue("TabgInstaller"));
            _httpClient = new HttpClient();
        }

        public async Task<bool> InstallCommunityServerAsync(string serverDir, CancellationToken ct)
        {
            try
            {
                _log.Report("• Downloading TABGCommunityServer...");
                
                // Get latest release from GitHub
                var releases = await _githubClient.Repository.Release.GetAll("JIBSIL", "TABGCommunityServer");
                if (releases.Count == 0)
                {
                    _log.Report("[ERROR] No releases found for TABGCommunityServer");
                    return false;
                }

                var latestRelease = releases[0];
                ReleaseAsset? zipAsset = null;
                
                // Find the Windows x64 build
                foreach (var asset in latestRelease.Assets)
                {
                    if (asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase) && 
                        asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        zipAsset = asset;
                        break;
                    }
                }

                if (zipAsset == null)
                {
                    _log.Report("[WARN] No Windows x64 build found, downloading source code instead");
                    // Download source code as fallback
                    var sourceUrl = latestRelease.ZipballUrl;
                    return await DownloadAndExtractSourceAsync(sourceUrl, serverDir, ct);
                }

                // Download the release
                var downloadUrl = zipAsset.BrowserDownloadUrl;
                var tempFile = Path.Combine(Path.GetTempPath(), $"TABGCommunityServer_{Guid.NewGuid()}.zip");
                
                _log.Report($"  → Downloading from: {downloadUrl}");
                
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    
                    using (var fs = new FileStream(tempFile, System.IO.FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs, ct);
                    }
                }

                // Extract to CommunityServer folder
                var communityServerDir = Path.Combine(serverDir, "CommunityServer");
                if (Directory.Exists(communityServerDir))
                {
                    Directory.Delete(communityServerDir, true);
                }
                
                _log.Report($"  → Extracting to: {communityServerDir}");
                ZipFile.ExtractToDirectory(tempFile, communityServerDir);
                
                // Clean up
                File.Delete(tempFile);
                
                _log.Report("• TABGCommunityServer installed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to install TABGCommunityServer: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DownloadAndExtractSourceAsync(string sourceUrl, string serverDir, CancellationToken ct)
        {
            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"TABGCommunityServer_src_{Guid.NewGuid()}.zip");
                
                _log.Report($"  → Downloading source from: {sourceUrl}");
                
                using (var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    
                    using (var fs = new FileStream(tempFile, System.IO.FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs, ct);
                    }
                }

                var communityServerDir = Path.Combine(serverDir, "CommunityServer");
                if (Directory.Exists(communityServerDir))
                {
                    Directory.Delete(communityServerDir, true);
                }
                
                // Extract and move the contents up one level (GitHub adds a folder)
                var tempExtractDir = Path.Combine(Path.GetTempPath(), $"temp_extract_{Guid.NewGuid()}");
                ZipFile.ExtractToDirectory(tempFile, tempExtractDir);
                
                // Find the actual source folder (GitHub creates JIBSIL-TABGCommunityServer-xxxxx)
                var sourceFolders = Directory.GetDirectories(tempExtractDir);
                if (sourceFolders.Length > 0)
                {
                    Directory.Move(sourceFolders[0], communityServerDir);
                }
                
                // Clean up
                File.Delete(tempFile);
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, true);
                }
                
                _log.Report("  → Source code extracted. You'll need to build it manually.");
                return true;
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to download source: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 