using System;
namespace TabgInstaller.Core;

public static class LogExtensions
{
    /// <summary>
    /// Logs an exception with rich details (type, message, stack trace and optional inner exception).
    /// This helps to diagnose unexpected failures without relying solely on ex.Message which often hides
    /// important context.
    /// </summary>
    /// <param name="log">The progress logger to write to.</param>
    /// <param name="context">Short textual description of where the exception happened.</param>
    /// <param name="ex">The exception instance.</param>
    public static void LogException(this IProgress<string> log, string context, Exception ex)
    {
        if (log == null) return;

        // Header line – keep it concise so the GUI remains readable.
        log.Report($"[ERROR] {context}: {ex.GetType().Name}: {ex.Message}");

        // StackTrace can be null (e.g., in some AOT scenarios) – guard against that.
        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            log.Report(ex.StackTrace!);
        }

        // Log inner exceptions (recursively) to help drill down root cause.
        var inner = ex.InnerException;
        while (inner != null)
        {
            log.Report($"[INNER] {inner.GetType().Name}: {inner.Message}");
            if (!string.IsNullOrWhiteSpace(inner.StackTrace))
            {
                log.Report(inner.StackTrace!);
            }
            inner = inner.InnerException;
        }
    }
} 