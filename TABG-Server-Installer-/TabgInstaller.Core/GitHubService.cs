using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core
{
    public class GitHubService
    {
        private readonly HttpClient _httpClient;
        private readonly IProgress<string> _log;
        private const string StarterPackOwner = "ContagiouslyStupid";
        private const string StarterPackRepo = "TABGStarterPack";
        private const string WordListRawUrl = "https://raw.githubusercontent.com/landfallgames/tabg-word-list/main/all_words.txt";
        private const string CacheDirName = "TabgInstallerCache";
        private static readonly string AppDataCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CacheDirName);

        public GitHubService(HttpClient httpClient, IProgress<string> log)
        {
            _httpClient = httpClient;
            _log = log;
            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TABGInstaller", "1.0"));
            }
            Directory.CreateDirectory(AppDataCachePath);
        }

        public async Task<string?> FindReleaseTagWithAssetAsync(string assetName)
        {
            try
            {
                var releases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>($"https://api.github.com/repos/{StarterPackOwner}/{StarterPackRepo}/releases?per_page=10");
                if (releases == null) return null;

                foreach (var release in releases)
                {
                    if (release.Assets.Any(a => a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return release.TagName;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _log.Report($"[ERROR] Error fetching releases for {StarterPackOwner}/{StarterPackRepo}: {ex.Message}");
                return null;
            }
            return null;
        }
        
        public async Task<bool> DownloadAssetAsync(string owner, string repo, string tagName, string assetName, string downloadDirectory, string? browserDownloadUrlOverride = null)
        {
            var assetDownloadUrl = browserDownloadUrlOverride;

            if (string.IsNullOrEmpty(assetDownloadUrl))
            {
                try
                {
                    var release = await GetReleaseAsync(owner, repo, tagName);
                    var asset = release?.Assets.FirstOrDefault(a => a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));
                    if (asset == null)
                    {
                        _log.Report($"[WARN] Asset '{assetName}' not found in release '{tagName}' of {owner}/{repo}.");
                        return false;
                    }
                    assetDownloadUrl = asset.BrowserDownloadUrl;
                }
                catch (HttpRequestException ex)
                {
                    _log.Report($"[ERROR] Error fetching release asset info for {assetName} in {tagName} ({owner}/{repo}): {ex.Message}");
                    return false;
                }
            }
            
            if (string.IsNullOrEmpty(assetDownloadUrl))
            {
                 _log.Report($"[WARN] No download URL could be determined for asset '{assetName}'.");
                 return false;
            }

            try
            {
                var response = await _httpClient.GetAsync(assetDownloadUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _log.Report($"[ERROR] Failed to download {assetName}: {response.StatusCode} from {assetDownloadUrl}");
                    return false;
                }

                Directory.CreateDirectory(downloadDirectory);
                var filePath = Path.Combine(downloadDirectory, assetName);

                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }
                _log.Report($"  → Downloaded {assetName} to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Exception during download or saving of {assetName}: {ex.Message}");
                return false;
            }
        }

        public async Task<HashSet<string>> LoadAllowedWordsAsync()
        {
            var cachedFilePath = Path.Combine(AppDataCachePath, "all_words.txt");
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (File.Exists(cachedFilePath) && File.GetLastWriteTimeUtc(cachedFilePath) > DateTime.UtcNow.AddDays(-1))
                {
                    _log.Report("[INFO] Loading allowed words from cache.");
                    var lines = await File.ReadAllLinesAsync(cachedFilePath);
                    foreach (var line in lines)
                    {
                        var word = line.Trim();
                        if (!string.IsNullOrWhiteSpace(word)) words.Add(word);
                    }
                }
                else
                {
                    _log.Report("[INFO] Downloading allowed words list...");
                    var response = await _httpClient.GetStringAsync(WordListRawUrl);
                    var lines = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    var wordsToCache = new List<string>();
                    foreach (var line in lines)
                    {
                        var word = line.Trim();
                        if (!string.IsNullOrWhiteSpace(word))
                        {
                            words.Add(word);
                            wordsToCache.Add(word);
                        }
                    }
                    await File.WriteAllLinesAsync(cachedFilePath, wordsToCache, Encoding.UTF8);
                    _log.Report("[INFO] Allowed words list downloaded and cached.");
                }
            }
            catch (Exception ex)
            {
                _log.Report($"[WARN] Error loading allowed words: {ex.Message}. Proceeding with empty list or previously cached if available.");
                if (words.Count == 0 && File.Exists(cachedFilePath))
                {
                     _log.Report("[INFO] Attempting to load from stale cache due to download error...");
                     var lines = await File.ReadAllLinesAsync(cachedFilePath);
                     foreach (var line in lines)
                    {
                        var word = line.Trim();
                        if (!string.IsNullOrWhiteSpace(word)) words.Add(word);
                    }
                }
            }
            return words;
        }

        public async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<GitHubRelease>($"https://api.github.com/repos/{owner}/{repo}/releases/latest");
            }
            catch (HttpRequestException ex)
            {
                _log.Report($"[ERROR] Error fetching latest release for {owner}/{repo}: {ex.Message}");
                return null;
            }
        }

        public async Task<GitHubRelease?> GetReleaseAsync(string owner, string repo, string tag)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<GitHubRelease>($"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}");
            }
            catch (HttpRequestException ex)
            {
                _log.Report($"[ERROR] Error fetching release {tag} for {owner}/{repo}: {ex.Message}");
                return null;
            }
        }
    }
} 