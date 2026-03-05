using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public interface ILiveryInstallerService
{
    Task<(bool IsInstalled, bool IsEnabled)> GetStateAsync(LiveryPackage pkg, string gameRoot,
        CancellationToken ct = default);

    Task InstallAsync(LiveryPackage pkg, string gameRoot, bool enableAfterInstall, CancellationToken ct = default);
    Task UninstallAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default);

    Task EnableAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default);
    Task DisableAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default);

    Task ActivateForCarAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default);
}