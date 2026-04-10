using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace TabgInstaller.Core.Services;

public class CrashReportService : ICrashReportService
{
    private const string RepoOwner = "user1342554";
    private const string RepoName = "TABG-Server-Installer";
    private const string CrashLabel = "crash-report";

    private readonly GitHubClient _client;
    private readonly string _appVersion;
    private bool _hasReportedThisSession;

    public CrashReportService(string? githubToken = null)
    {
        _client = new GitHubClient(new ProductHeaderValue("TabgInstaller-CrashReporter"));

        if (!string.IsNullOrEmpty(githubToken))
            _client.Credentials = new Credentials(githubToken);

        var asm = Assembly.GetEntryAssembly();
        _appVersion = asm?.GetName().Version?.ToString() ?? "unknown";
    }

    public async Task<bool> ReportCrashAsync(Exception exception, CancellationToken ct = default)
    {
        if (_hasReportedThisSession) return false;

        try
        {
            var fingerprint = ComputeFingerprint(exception);
            var existingIssue = await FindExistingIssue(fingerprint);

            if (existingIssue != null)
            {
                try
                {
                    await _client.Reaction.Issue.Create(
                        RepoOwner, RepoName, existingIssue.Number,
                        new NewReaction(ReactionType.Plus1));
                }
                catch { /* Reaction may already exist */ }
                _hasReportedThisSession = true;
                return true;
            }

            var title = $"[Crash] {exception.GetType().Name}: {Truncate(exception.Message, 80)}";
            var body = FormatIssueBody(exception, fingerprint);

            var newIssue = new NewIssue(title) { Body = body };
            newIssue.Labels.Add(CrashLabel);

            await _client.Issue.Create(RepoOwner, RepoName, newIssue);
            _hasReportedThisSession = true;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CrashReport] Failed to report crash: {ex.Message}");
            return false;
        }
    }

    private async Task<Issue?> FindExistingIssue(string fingerprint)
    {
        try
        {
            var request = new SearchIssuesRequest($"repo:{RepoOwner}/{RepoName} label:{CrashLabel} \"{fingerprint}\" is:open");
            var result = await _client.Search.SearchIssues(request);
            return result.Items.FirstOrDefault();
        }
        catch { return null; }
    }

    private string FormatIssueBody(Exception exception, string fingerprint)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Crash Report");
        sb.AppendLine();
        sb.AppendLine($"**App Version:** {_appVersion}");
        sb.AppendLine($"**OS:** {Environment.OSVersion}");
        sb.AppendLine($"**.NET Runtime:** {Environment.Version}");
        sb.AppendLine($"**Timestamp (UTC):** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Exception");
        sb.AppendLine();
        sb.AppendLine($"**Type:** `{exception.GetType().FullName}`");
        sb.AppendLine($"**Message:** {exception.Message}");
        sb.AppendLine();
        sb.AppendLine("## Stack Trace");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(exception.ToString());
        sb.AppendLine("```");

        if (exception.InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Inner Exception");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(exception.InnerException.ToString());
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine($"<!-- fingerprint: {fingerprint} -->");

        return sb.ToString();
    }

    private static string ComputeFingerprint(Exception exception)
    {
        var sb = new StringBuilder();
        sb.Append(exception.GetType().FullName);

        var stackTrace = new System.Diagnostics.StackTrace(exception, false);
        var frames = stackTrace.GetFrames();
        if (frames != null)
        {
            foreach (var frame in frames.Take(3))
            {
                var method = frame.GetMethod();
                if (method != null)
                    sb.Append($"|{method.DeclaringType?.FullName}.{method.Name}");
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
