using System;
using System.Collections.Generic;
using System.IO;

namespace TabgInstaller.Core
{
    /// <summary>
    /// Helper class that can snapshot and restore a set of configuration files (a "preset").
    /// A preset is saved as a sub-folder inside the top-level "Presets" folder that sits next to the server executable.
    /// </summary>
    public static class PresetManager
    {
        /// <summary>Default set of config files that make sense to snapshot. They are relative to the server root.</summary>
        public static readonly string[] DefaultConfigRelativePaths =
        {
            "game_settings.txt",
            "TheStarterPack.txt",
            "TheStarterPack.json",
            Path.Combine("BepInEx","config","CitrusLib","ExtraSettings.json"),
            Path.Combine("BepInEx","config","CitrusLib","PlayerPerms.json"),
        };

        private static string PresetsRoot(string serverDir) => Path.Combine(serverDir, "Presets");

        /// <summary>Returns the names of all presets (folder names) found in the server's Presets directory.</summary>
        public static IEnumerable<string> ListPresets(string serverDir)
        {
            var root = PresetsRoot(serverDir);
            if (!Directory.Exists(root)) yield break;
            foreach (var dir in Directory.GetDirectories(root))
                yield return Path.GetFileName(dir);
        }

        /// <summary>Save a preset consisting of the supplied relative paths. Missing files are skipped.</summary>
        public static void SavePreset(string serverDir, string presetName, IEnumerable<string> relativePaths)
        {
            if (string.IsNullOrWhiteSpace(presetName)) throw new ArgumentException("Preset name is required", nameof(presetName));
            var root = PresetsRoot(serverDir);
            var presetDir = Path.Combine(root, presetName);
            Directory.CreateDirectory(presetDir);

            foreach (var rel in relativePaths)
            {
                if (string.IsNullOrWhiteSpace(rel)) continue;
                var src = Path.Combine(serverDir, rel);
                if (!File.Exists(src)) continue; // skip non-existing files

                var dst = Path.Combine(presetDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite:true);
            }
        }

        /// <summary>Copies all files from the preset folder back into the server directory, overwriting existing files.</summary>
        public static void LoadPreset(string serverDir, string presetName)
        {
            var presetDir = Path.Combine(PresetsRoot(serverDir), presetName);
            if (!Directory.Exists(presetDir)) throw new DirectoryNotFoundException($"Preset '{presetName}' not found.");

            foreach (var file in Directory.GetFiles(presetDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(presetDir, file);
                var dst = Path.Combine(serverDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(file, dst, overwrite:true);
            }
        }

        /// <summary>Deletes a preset folder.</summary>
        public static void DeletePreset(string serverDir, string presetName)
        {
            var presetDir = Path.Combine(PresetsRoot(serverDir), presetName);
            if (Directory.Exists(presetDir)) Directory.Delete(presetDir, recursive:true);
        }
    }
} 