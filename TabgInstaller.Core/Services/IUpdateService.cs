using System;
using System.Threading.Tasks;

namespace TabgInstaller.Core.Services
{
    public interface IUpdateService
    {
        Task<UpdateInfo?> CheckForUpdateAsync();
        Task<bool> ApplyUpdateAsync(string downloadUrl, IProgress<string>? log = null);
    }
}
