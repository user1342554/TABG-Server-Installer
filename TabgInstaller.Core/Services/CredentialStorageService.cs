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
            var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            _store[key] = Convert.ToBase64String(encrypted);
            SaveToDisk();
        }

        public string? Retrieve(Guid instanceId, string credentialType)
        {
            var key = BuildKey(instanceId, credentialType);
            if (!_store.TryGetValue(key, out var base64))
                return null;

            try
            {
                var encrypted = Convert.FromBase64String(base64);
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
