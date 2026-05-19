using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace TabgInstaller.Core.Services
{
    public class CredentialStorageService : ICredentialStorageService
    {
        private readonly string _filePath;
        private Dictionary<string, string> _store; // key -> base64 encrypted blob

        public CredentialStorageService(string storageDir)
        {
            _filePath = Path.Combine(storageDir, "credentials.dat");
            _store = LoadFromDisk();
        }

        public void Store(Guid instanceId, string credentialType, string value)
        {
            var key = BuildKey(instanceId, credentialType);
            var plainBytes = Encoding.UTF8.GetBytes(value);
            if (OperatingSystem.IsWindows())
            {
                var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                _store[key] = "dpapi:" + Convert.ToBase64String(encrypted);
            }
            else
            {
                // Functional Linux fallback. Users can secure this further later with libsecret/keyring.
                _store[key] = "plain:" + Convert.ToBase64String(plainBytes);
            }
            SaveToDisk();
        }

        public string? Retrieve(Guid instanceId, string credentialType)
        {
            var key = BuildKey(instanceId, credentialType);
            if (!_store.TryGetValue(key, out var base64))
                return null;

            try
            {
                if (base64.StartsWith("plain:", StringComparison.Ordinal))
                {
                    var plain = Convert.FromBase64String(base64.Substring("plain:".Length));
                    return Encoding.UTF8.GetString(plain);
                }

                var encryptedBlob = base64.StartsWith("dpapi:", StringComparison.Ordinal)
                    ? base64.Substring("dpapi:".Length)
                    : base64;

                if (!OperatingSystem.IsWindows())
                    return null;

                var encrypted = Convert.FromBase64String(encryptedBlob);
                var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                _store.Remove(key);
                SaveToDisk();
                return null;
            }
        }

        public void Remove(Guid instanceId)
        {
            var prefix = instanceId.ToString() + "_";
            var keysToRemove = _store.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
                _store.Remove(key);
            SaveToDisk();
        }

        private static string BuildKey(Guid instanceId, string credentialType)
            => $"{instanceId}_{credentialType}";

        private Dictionary<string, string> LoadFromDisk()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                           ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CredentialStorage] Failed to load: {ex.Message}");
            }
            return new Dictionary<string, string>();
        }

        private void SaveToDisk()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(_store, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CredentialStorage] Failed to save: {ex.Message}");
            }
        }
    }
}
