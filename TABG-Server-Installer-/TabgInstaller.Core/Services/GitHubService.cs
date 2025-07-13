using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Octokit;
using TabgInstaller.Core;
// using TabgInstaller.Core.Model; // Octokit.ReleaseAsset is used directly, so specific models might not be needed here

namespace TabgInstaller.Core.Services
{
    public class GitHubService
    {
        private readonly GitHubClient _client;
        private readonly IProgress<string> _log;
        // HttpClient field, will be initialized in the constructor but not used yet.
        private readonly HttpClient _httpClientUnused;

        // Modified constructor to match compiler error expectation
        public GitHubService(HttpClient httpClient, IProgress<string> log)
        {
            _log = log;
            _httpClientUnused = httpClient; // Assign to the unused field
            _client = new GitHubClient(new ProductHeaderValue("TabgInstaller"));
        }

        public async Task<Octokit.Release?> GetLatestReleaseAsync(string owner, string repo)
        {
            try
            {
                return await _client.Repository.Release.GetLatest(owner, repo);
            }
            catch (Octokit.NotFoundException)
            {
                _log.Report($"[WARN] Latest release not found for {owner}/{repo}.");
                return null;
            }
            catch (Exception ex)
            {
                _log.LogException($"Error fetching latest release for {owner}/{repo}", ex);
                return null;
            }
        }

        public async Task<Octokit.Release?> GetReleaseAsync(string owner, string repo, string tagName)
        {
            try
            {
                return await _client.Repository.Release.Get(owner, repo, tagName);
            }
            catch (Octokit.NotFoundException)
            {
                _log.Report($"[WARN] Release {tagName} not found for {owner}/{repo}.");
                return null;
            }
            catch (Exception ex)
            {
                _log.LogException($"Error fetching release {tagName} for {owner}/{repo}", ex);
                return null;
            }
        }

        // Modified DownloadAssetAsync to match compiler error expectation
        public async Task<bool> DownloadAssetAsync(string owner, string repo, string browserDownloadUrl, string destinationPath, string downloadDirectory, string? anotherOptionalString)
        {
            _log.Report($"• Attempting to download asset from {browserDownloadUrl} to {destinationPath}..."); 
            try
            {
                // The 'owner', 'repo', 'downloadDirectory', and 'anotherOptionalString' parameters are not used in this HttpClient-based download logic.
                // They are added to match the signature the compiler expects.
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("TabgInstaller", "1.1"));
                
                using var response = await httpClient.GetAsync(browserDownloadUrl);
                response.EnsureSuccessStatusCode();

                var directoryName = Path.GetDirectoryName(destinationPath);
                if (directoryName != null) 
                {
                    Directory.CreateDirectory(directoryName);
                }
                else
                {
                    _log.Report($"[ERROR] Could not determine directory for destination path: {destinationPath}");
                    return false;
                }

                using var fs = new FileStream(destinationPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);

                _log.Report($"  → Heruntergeladen: {browserDownloadUrl} to {destinationPath}");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogException($"Fehler beim Herunterladen von {browserDownloadUrl}", ex);
                return false;
            }
        }

        // FindReleaseTagWithAssetAsync (from previous HttpClient version) might need to be re-implemented using Octokit if still needed
        // For now, it's not in the user's diff for GitHubService.cs

        // LoadAllowedWordsAsync (from previous HttpClient version) also not in the user's diff for GitHubService.cs
        // If this functionality is still needed, it must be re-added or moved.
    }
} 