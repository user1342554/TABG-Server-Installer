using System;

namespace TabgInstaller.Core.Services
{
    public interface ICredentialStorageService
    {
        void Store(Guid instanceId, string credentialType, string value);
        string? Retrieve(Guid instanceId, string credentialType);
        void Remove(Guid instanceId);
    }
}
