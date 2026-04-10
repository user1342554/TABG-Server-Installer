using System;
using System.Threading;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services;

public interface ICrashReportService
{
    Task<bool> ReportCrashAsync(Exception exception, CancellationToken ct = default);
}
