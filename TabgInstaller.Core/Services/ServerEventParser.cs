using System.Text.RegularExpressions;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public static class ServerEventParser
    {
        // [LandLog] - Player: 0 Name: Jon_ass : Assigning EPic ID: 0002679463fd49ffab724df634f46418
        private static readonly Regex PlayerJoinedPattern = new(
            @"\[LandLog\]\s*-\s*Player:\s*(\d+)\s+Name:\s*(.+?)\s*:\s*Assigning EPic ID:\s*(\S+)",
            RegexOptions.Compiled);

        // [LandLog] - Player left: Jon_ass
        private static readonly Regex PlayerLeftPattern = new(
            @"\[LandLog\]\s*-\s*Player left:\s*(.+)$",
            RegexOptions.Compiled);

        // [LandLog] - Client: 0 disconnected from server
        private static readonly Regex ClientDisconnectedPattern = new(
            @"\[LandLog\]\s*-\s*Client:\s*(\d+)\s+disconnected from server",
            RegexOptions.Compiled);

        // [LandLog] - Host - Got join code: FWJTKK
        private static readonly Regex JoinCodePattern = new(
            @"\[LandLog\]\s*-\s*Host\s*-\s*Got join code:\s*(\S+)",
            RegexOptions.Compiled);

        // <process exited>
        private static readonly Regex ProcessExitedPattern = new(
            @"^<process exited>$",
            RegexOptions.Compiled);

        public static ServerEvent? TryParse(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            var match = PlayerJoinedPattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.PlayerJoined,
                    PlayerIndex = int.Parse(match.Groups[1].Value),
                    PlayerName = match.Groups[2].Value.Trim(),
                    EpicId = match.Groups[3].Value,
                };
            }

            match = PlayerLeftPattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.PlayerLeft,
                    PlayerName = match.Groups[1].Value.Trim(),
                };
            }

            match = ClientDisconnectedPattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.PlayerLeft,
                    PlayerIndex = int.Parse(match.Groups[1].Value),
                };
            }

            match = JoinCodePattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.JoinCodeReceived,
                    JoinCode = match.Groups[1].Value,
                };
            }

            match = ProcessExitedPattern.Match(line);
            if (match.Success)
            {
                return new ServerEvent
                {
                    Type = ServerEventType.ProcessExited,
                };
            }

            return null;
        }
    }
}
