using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.ViewModels
{
    public enum PluginInstallStatus
    {
        Available,
        Installed,
        UpdateAvailable,
        Incompatible
    }

    public partial class PluginCardViewModel : ObservableObject
    {
        public PluginManifest Manifest { get; }

        [ObservableProperty] private PluginInstallStatus _installStatus;
        [ObservableProperty] private string _actionButtonText = "";
        [ObservableProperty] private bool _isActionEnabled;
        [ObservableProperty] private bool _isPinned;
        [ObservableProperty] private string _incompatibilityReason = "";
        [ObservableProperty] private bool _isInstalled;

        public string TypeBadge => Manifest.Type switch
        {
            "server" => "Server",
            "client" => "Client",
            "both" => "Both",
            _ => Manifest.Type
        };

        public bool HasDependencies => Manifest.Dependencies.Length > 0;
        public string DependenciesText => string.Join(", ", Manifest.Dependencies);
        public bool HasChangelog => !string.IsNullOrEmpty(Manifest.Changelog);

        public PluginCardViewModel(PluginManifest manifest, InstalledPluginEntry? installed, string currentInstallerVersion)
        {
            Manifest = manifest;
            _isPinned = installed?.Pinned ?? false;
            _isInstalled = installed != null;

            // Determine compatibility
            if (!IsCompatible(manifest, currentInstallerVersion, out var reason))
            {
                _installStatus = PluginInstallStatus.Incompatible;
                _actionButtonText = "Incompatible";
                _isActionEnabled = false;
                _incompatibilityReason = reason;
                return;
            }

            if (installed == null)
            {
                _installStatus = PluginInstallStatus.Available;
                _actionButtonText = "Install";
                _isActionEnabled = true;
            }
            else if (_isPinned || CompareVersions(manifest.Version, installed.InstalledVersion) <= 0)
            {
                _installStatus = PluginInstallStatus.Installed;
                _actionButtonText = "Installed \u2713";
                _isActionEnabled = false;
            }
            else
            {
                _installStatus = PluginInstallStatus.UpdateAvailable;
                _actionButtonText = $"Update ({installed.InstalledVersion} \u2192 {manifest.Version})";
                _isActionEnabled = true;
            }
        }

        public bool MatchesSearch(string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;

            var q = query.ToLowerInvariant();
            return Manifest.Name.ToLowerInvariant().Contains(q)
                || Manifest.Description.ToLowerInvariant().Contains(q)
                || Manifest.Author.ToLowerInvariant().Contains(q)
                || Manifest.Tags.Any(t => t.ToLowerInvariant().Contains(q));
        }

        private static bool IsCompatible(PluginManifest manifest, string currentInstallerVersion, out string reason)
        {
            reason = "";
            if (CompareVersions(currentInstallerVersion, manifest.MinInstallerVersion) < 0)
            {
                reason = $"Requires installer version {manifest.MinInstallerVersion} or newer.";
                return false;
            }
            return true;
        }

        private static int CompareVersions(string a, string b)
        {
            var aParts = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
            var bParts = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
            var len = Math.Max(aParts.Length, bParts.Length);

            for (int i = 0; i < len; i++)
            {
                var av = i < aParts.Length ? aParts[i] : 0;
                var bv = i < bParts.Length ? bParts[i] : 0;
                if (av != bv) return av - bv;
            }
            return 0;
        }
    }
}
