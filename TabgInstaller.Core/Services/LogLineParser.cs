using System;
using System.Text.RegularExpressions;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Core.Services
{
    public static class LogLineParser
    {
        // Prefix patterns to strip from Message (bracket-style and colon-style)
        private static readonly Regex PrefixPattern = new(
            @"^\s*(?:\[(INFO|ERROR|WARNING|WARN)\]|(?:ERROR|WARNING|WARN|INFO):)\s*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Error detection patterns (case-insensitive)
        private static readonly Regex ErrorPattern = new(
            @"\[error\]|exception|nullreference|stacktrace|fatal|^ERROR:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Warning detection patterns (case-insensitive)
        private static readonly Regex WarningPattern = new(
            @"\[warn|warning|^WARNING:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static LogEntry Parse(string rawLine, bool isStderr = false, DateTime? timestamp = null)
        {
            var ts = timestamp ?? DateTime.Now;
            var line = rawLine ?? "";

            var severity = DetectSeverity(line, isStderr);
            var message = StripPrefix(line);

            return new LogEntry
            {
                Timestamp = ts,
                Severity = severity,
                RawText = line,
                Message = message
            };
        }

        private static LogSeverity DetectSeverity(string line, bool isStderr)
        {
            // Explicit prefixes take precedence over isStderr, so an [INFO] line
            // on stderr is still classified as Info (per spec).
            if (ErrorPattern.IsMatch(line))
                return LogSeverity.Error;

            if (WarningPattern.IsMatch(line))
                return LogSeverity.Warning;

            // Stderr lines without an explicit marker default to Error
            return isStderr ? LogSeverity.Error : LogSeverity.Info;
        }

        private static string StripPrefix(string line)
        {
            var match = PrefixPattern.Match(line);
            if (match.Success)
                return line.Substring(match.Length);

            return line;
        }
    }
}
