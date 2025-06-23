using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace TabgInstaller.Core.Services
{
    public class AntiCheatRemoverService
    {
        private readonly IProgress<string> _log;
        private readonly GitHubClient _githubClient;
        private readonly HttpClient _httpClient;

        public AntiCheatRemoverService(IProgress<string> log)
        {
            _log = log;
            _githubClient = new GitHubClient(new ProductHeaderValue("TabgInstaller"));
            _httpClient = new HttpClient();
        }

        public async Task<bool> InstallAntiCheatRemoverAsync(string serverDir, CancellationToken ct)
        {
            try
            {
                _log.Report("• Installing AntiCheatBootErrorRemover...");
                
                // Get latest release
                var releases = await _githubClient.Repository.Release.GetAll("C0mputery", "AntiCheatBootErrorRemover");
                if (releases.Count == 0)
                {
                    _log.Report("[ERROR] No releases found for AntiCheatBootErrorRemover");
                    return false;
                }

                var latestRelease = releases[0];
                ReleaseAsset? dllAsset = null;
                
                // Find the DLL file
                foreach (var asset in latestRelease.Assets)
                {
                    if (asset.Name.Equals("AntiCheatBootErrorRemover.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        dllAsset = asset;
                        break;
                    }
                }

                if (dllAsset == null)
                {
                    _log.Report("[ERROR] AntiCheatBootErrorRemover.dll not found in release");
                    return false;
                }

                // Ensure plugins directory exists
                var pluginsDir = Path.Combine(serverDir, "BepInEx", "plugins");
                Directory.CreateDirectory(pluginsDir);

                // Download the DLL
                var downloadUrl = dllAsset.BrowserDownloadUrl;
                var targetPath = Path.Combine(pluginsDir, "AntiCheatBootErrorRemover.dll");
                
                _log.Report($"  → Downloading from: {downloadUrl}");
                
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    
                    using (var fs = new FileStream(targetPath, System.IO.FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs, ct);
                    }
                }
                
                _log.Report($"  → AntiCheatBootErrorRemover installed to: {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to install AntiCheatBootErrorRemover: {ex.Message}");
                return false;
            }
        }

        public Task<bool> PatchClientAsync(string tabgClientPath, CancellationToken ct)
        {
            try
            {
                _log.Report("• Patching TABG client to remove AntiCheat...");
                
                // The AntiCheatBootErrorRemover works at runtime, but we might need to patch Assembly-CSharp.dll
                var assemblyPath = Path.Combine(tabgClientPath, "TotallyAccurateBattlegrounds_Data", "Managed", "Assembly-CSharp.dll");
                
                if (!File.Exists(assemblyPath))
                {
                    _log.Report($"[ERROR] Assembly-CSharp.dll not found at: {assemblyPath}");
                    return Task.FromResult(false);
                }

                // Create backup
                var backupPath = assemblyPath + ".bak";
                if (!File.Exists(backupPath))
                {
                    File.Copy(assemblyPath, backupPath);
                    _log.Report($"  → Created backup: {backupPath}");
                }

                // Note: Actual patching would require dnlib or similar library
                // For now, we just inform the user
                _log.Report("  → Client patching requires manual intervention or a patched Assembly-CSharp.dll");
                _log.Report("  → Please use a pre-patched Assembly-CSharp.dll or use BepInEx with AntiCheatBootErrorRemover");
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _log.Report($"[ERROR] Failed to patch client: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 