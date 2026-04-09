using System;

namespace TabgInstaller.Core.Services
{
    /// <summary>
    /// Legacy global singleton for the server path.
    /// All new code should use <see cref="TabgInstaller.Core.IServerPathProvider"/> via DI instead.
    /// TODO: Remove once AppState (the only remaining caller) is deleted.
    /// </summary>
    [Obsolete("Use IServerPathProvider via DI instead.")]
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
