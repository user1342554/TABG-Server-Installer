using System.Collections.Generic;

namespace TabgInstaller.Core.Services
{
    public interface IKnownPlayersService
    {
        IReadOnlyList<KnownPlayer> Players { get; }
        int ScanGuestbooks(string serverDir);
        string? ResolveEpicId(string playerName);
        List<string> GetPlayerNames();
    }
}
