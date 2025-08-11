using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TabgInstaller.Core.Services
{
    public class SafeConfigEditor
    {
        public record EditResult(bool Success, string Message, string UnifiedDiff, string NewHash);

        public static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            var hash = sha.ComputeHash(fs);
            return Convert.ToHexString(hash);
        }

        public EditResult SetKeyValue(string path, string key, string value, string? expectedHash, bool previewOnly)
        {
            if (!File.Exists(path)) return new EditResult(false, $"Not found: {path}", "", "");

            var originalBytes = File.ReadAllBytes(path);
            var originalText = DetectEncoding(originalBytes, out var encoding, out var hasBom);
            var lines = originalText.Replace("\r\n", "\n").Split('\n').ToList();

            var oldHash = ComputeSha256(path);
            if (!string.IsNullOrEmpty(expectedHash) && !string.Equals(oldHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                return new EditResult(false, "File changed since preview. Please refresh.", "", oldHash);

            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.StartsWith("#")) continue; // skip comments
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var name = line.Substring(0, eq);
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{key}={value}";
                    found = true;
                    break;
                }
            }
            if (!found) lines.Add($"{key}={value}");

            var newText = string.Join("\n", lines);
            if (originalText.EndsWith("\r\n") || originalText.EndsWith("\n"))
            {
                // keep trailing newline
                if (!newText.EndsWith("\n")) newText += "\n";
            }
            // Restore CRLF if needed
            if (originalText.Contains("\r\n")) newText = newText.Replace("\n", "\r\n");

            var diff = BuildUnifiedDiff(originalText, newText, Path.GetFileName(path));
            if (previewOnly)
            {
                return new EditResult(true, "Preview only", diff, oldHash);
            }

            // Backup
            var backupPath = path + ".bak";
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(path, backupPath + DateTime.Now.ToString(".yyyyMMdd_HHmmss"), overwrite: true);
                }
                File.Copy(path, backupPath, overwrite: true);
            }
            catch { }

            // Atomic write via temp
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, newText, encoding);
            if (!hasBom)
            {
                // Remove BOM if encoding inserted one
                var tempBytes = File.ReadAllBytes(tempPath);
                var withoutBom = RemoveUtf8Bom(tempBytes);
                File.WriteAllBytes(tempPath, withoutBom);
            }
            File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);

            var newHash = ComputeSha256(path);
            return new EditResult(true, "Applied", diff, newHash);
        }

        private static string DetectEncoding(byte[] bytes, out Encoding encoding, out bool hasBom)
        {
            // Default to UTF8
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            hasBom = false;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(true);
                hasBom = true;
                return encoding.GetString(bytes, 3, bytes.Length - 3);
            }
            return encoding.GetString(bytes);
        }

        private static byte[] RemoveUtf8Bom(byte[] data)
        {
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                return data.Skip(3).ToArray();
            }
            return data;
        }

        private static string BuildUnifiedDiff(string oldText, string newText, string fileName)
        {
            var oldLines = oldText.Replace("\r\n", "\n").Split('\n');
            var newLines = newText.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();
            sb.AppendLine($"--- a/{fileName}");
            sb.AppendLine($"+++ b/{fileName}");
            int i = 0, j = 0;
            while (i < oldLines.Length || j < newLines.Length)
            {
                if (i < oldLines.Length && j < newLines.Length && oldLines[i] == newLines[j])
                {
                    i++; j++;
                    continue;
                }
                int startI = i, startJ = j;
                var removed = new List<string>();
                var added = new List<string>();
                while (i < oldLines.Length && (j >= newLines.Length || oldLines[i] != newLines[j]))
                {
                    removed.Add(oldLines[i]);
                    i++;
                }
                while (j < newLines.Length && (startI >= oldLines.Length || (i >= oldLines.Length || newLines[j] != oldLines[i])))
                {
                    added.Add(newLines[j]);
                    j++;
                    if (i < oldLines.Length && j < newLines.Length && oldLines[i] == newLines[j]) break;
                }
                sb.AppendLine($"@@ -{startI + 1} +{startJ + 1} @@");
                foreach (var r in removed) sb.AppendLine("-" + r);
                foreach (var a in added) sb.AppendLine("+" + a);
            }
            return sb.ToString();
        }
    }
}


