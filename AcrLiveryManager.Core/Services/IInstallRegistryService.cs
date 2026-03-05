using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public interface IInstallRegistryService
{
    Task<InstallRegistry> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(InstallRegistry registry, CancellationToken ct = default);
    string GetRegistryPath();
}