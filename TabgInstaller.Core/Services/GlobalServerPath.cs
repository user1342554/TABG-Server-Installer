using System;

namespace TabgInstaller.Core.Services
{
    /// <summary>
    /// Global singleton to store and access the current server path across the application
    /// </summary>
    public static class GlobalServerPath
    {
        private static string? _serverPath;

        /// <summary>
        /// Gets the current server path, or null if not set
        /// </summary>
        public static string? Current => _serverPath;

        /// <summary>
        /// Sets the current server path
        /// </summary>
        public static void Set(string serverPath)
        {
            _serverPath = serverPath?.Trim();
        }

        /// <summary>
        /// Clears the current server path
        /// </summary>
        public static void Clear()
        {
            _serverPath = null;
        }

        /// <summary>
        /// Gets the current server path, or a fallback if not set
        /// </summary>
        public static string GetOrFallback(string fallback = "")
        {
            return _serverPath ?? fallback;
        }
    }
}
