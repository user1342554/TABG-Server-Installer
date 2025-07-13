using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TabgInstaller.Core.Services
{
    public interface ISecureKeyStore
    {
        void SaveKey(string provider, string key);
        string? GetKey(string provider);
        void DeleteKey(string provider);
        bool HasKey(string provider);
    }

    public class SecureKeyStore : ISecureKeyStore
    {
        private readonly string _keyStorePath;

        public SecureKeyStore()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var tabgDir = Path.Combine(appData, "TABGInstaller");
            Directory.CreateDirectory(tabgDir);
            _keyStorePath = Path.Combine(tabgDir, "keys");
            Directory.CreateDirectory(_keyStorePath);
        }

        public void SaveKey(string provider, string key)
        {
            var keyFile = Path.Combine(_keyStorePath, $"{provider}.key");
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var encryptedBytes = ProtectedData.Protect(keyBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(keyFile, encryptedBytes);
        }

        public string? GetKey(string provider)
        {
            var keyFile = Path.Combine(_keyStorePath, $"{provider}.key");
            if (!File.Exists(keyFile))
                return null;

            try
            {
                var encryptedBytes = File.ReadAllBytes(keyFile);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return null;
            }
        }

        public void DeleteKey(string provider)
        {
            var keyFile = Path.Combine(_keyStorePath, $"{provider}.key");
            if (File.Exists(keyFile))
                File.Delete(keyFile);
        }

        public bool HasKey(string provider)
        {
            var keyFile = Path.Combine(_keyStorePath, $"{provider}.key");
            return File.Exists(keyFile);
        }
    }
} 