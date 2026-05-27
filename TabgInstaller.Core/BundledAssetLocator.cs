using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TabgInstaller.Core
{
    /// <summary>
    /// Resolves bundled plugin payloads from published app output or from a source checkout.
    /// </summary>
    public static class BundledAssetLocator
    {
        public const string ServerPluginsFolder = "plugins";
        public const string ClientPluginsFolder = "client-plugins";

        public static string? FindServerPluginsDirectory() =>
            FindDirectory(ServerPluginsFolder, "*.dll");

        public static string? FindClientPluginsDirectory() =>
            FindDirectory(ClientPluginsFolder, "*.dll");

        public static string? FindDirectory(string relativeFolder, string requiredPattern = "*")
        {
            foreach (var root in CandidateRoots())
            {
                foreach (var relative in DirectoryCandidates(relativeFolder))
                {
                    var candidate = Path.GetFullPath(Path.Combine(root, relative));
                    if (Directory.Exists(candidate) &&
                        Directory.EnumerateFiles(candidate, requiredPattern, SearchOption.TopDirectoryOnly).Any())
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        public static string? FindFile(string relativePath)
        {
            foreach (var root in CandidateRoots())
            {
                foreach (var relative in FileCandidates(relativePath))
                {
                    var candidate = Path.GetFullPath(Path.Combine(root, relative));
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        public static IReadOnlyList<string> FindFiles(string relativeFolder, string pattern)
        {
            var dir = FindDirectory(relativeFolder, pattern);
            return dir == null
                ? Array.Empty<string>()
                : Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
        }

        private static IEnumerable<string> DirectoryCandidates(string relativeFolder)
        {
            yield return relativeFolder;
            yield return Path.Combine("bundled", relativeFolder);
            yield return Path.Combine("Assets", "bundled", relativeFolder);

            if (relativeFolder.Equals(ServerPluginsFolder, StringComparison.OrdinalIgnoreCase))
            {
                yield return "mods";
            }
        }

        private static IEnumerable<string> FileCandidates(string relativePath)
        {
            yield return relativePath;
            yield return Path.Combine("bundled", relativePath);
            yield return Path.Combine("Assets", "bundled", relativePath);
        }

        private static IEnumerable<string> CandidateRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                if (string.IsNullOrWhiteSpace(start))
                {
                    continue;
                }

                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (seen.Add(dir.FullName))
                    {
                        yield return dir.FullName;
                    }

                    dir = dir.Parent;
                }
            }
        }
    }
}
