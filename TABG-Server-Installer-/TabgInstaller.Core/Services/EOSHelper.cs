using System;
using System.IO;
using System.Linq;

namespace TabgInstaller.Core.Services
{
    public static class EOSHelper
    {
        private const string DllName = "EOSSDK-Win64-Shipping.dll";

        /// <summary>
        /// Make sure the dedicated-server folder contains a *working* EOSSDK-Win64-Shipping.dll.
        /// We always prefer the one that ships with the regular TABG client, because that
        /// version is known to export <c>EOS_ProductUserId_FromString</c> which the server calls.
        /// </summary>
        public static void EnsureDll(string serverDir, IProgress<string>? log = null)
        {
            var destPath = Path.Combine(serverDir, DllName);

            // Possible locations inside a normal client install
            var candidatePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                             "Steam", "steamapps", "common", "TotallyAccurateBattlegrounds", DllName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                             "Steam", "steamapps", "common", "TotallyAccurateBattlegrounds", "TABG_Data", "Plugins", DllName)
            };

            // Also search in the user's actual TABG client library if it's installed on a different drive
            var clientRoot = TabgInstaller.Core.Installer.TryFindTabgClientPath();
            if (!string.IsNullOrEmpty(clientRoot))
            {
                var dynamicCandidates = new[]
                {
                    Path.Combine(clientRoot, DllName),
                    Path.Combine(clientRoot, "TotallyAccurateBattlegrounds_Data", "Plugins", "x86_64", DllName),
                    Path.Combine(clientRoot, "TABG_Data", "Plugins", DllName),
                    Path.Combine(clientRoot, "TABG_Data", "Plugins", "x86_64", DllName)
                };
                candidatePaths = candidatePaths.Concat(dynamicCandidates).ToArray();
            }

            string? bestSource = candidatePaths
                .Where(File.Exists)
                .OrderByDescending(p => new FileInfo(p).Length)
                .FirstOrDefault();

            if (bestSource == null)
            {
                log?.Report($"[EOS] Could not find {DllName} in the client install. Make sure TABG is installed locally.");
                return;
            }

            try
            {
                var destPaths = new[] {
                    destPath,
                    Path.Combine(serverDir, "TABG_Data", "Plugins", DllName),
                    Path.Combine(serverDir, "TABG_Data", "Plugins", "x86_64", DllName)
                };
                foreach (var dp in destPaths)
                {
                    try {
                        Directory.CreateDirectory(Path.GetDirectoryName(dp)!);
                        File.Copy(bestSource, dp, true);
                    } catch { /* ignore individual copy errors */ }
                }
                log?.Report($"[EOS] Copied/Updated {DllName} to {destPaths.Length} locations.");
            }
            catch (Exception ex)
            {
                log?.Report($"[EOS] Failed to copy {DllName}: {ex.Message}");
            }
        }
    }
} 