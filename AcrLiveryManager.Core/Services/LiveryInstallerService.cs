using AcrLiveryManager.Core.Models;
using AcrLiveryManager.Core.Services;

namespace AcrLiveryManager.Core.Services;

public sealed class LiveryInstallerService(
    IGamePathService gamePaths,
    IInstallRegistryService registryService,
    IFileSystemService fs) : ILiveryInstallerService
{
    public async Task<(bool IsInstalled, bool IsEnabled)> GetStateAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
    {
        var paks = gamePaths.GetPaksPath(gameRoot);
        var names = GetTargetNames(pkg);

        var anyEnabled = names.All(n => File.Exists(Path.Combine(paks, n)));
        var anyDisabled = names.All(n => File.Exists(Path.Combine(paks, n + ".disabled")));

        // If mixed, treat as installed but disabled (and you can show “broken state” later).
        var isInstalled = anyEnabled || anyDisabled || names.Any(n => File.Exists(Path.Combine(paks, n)) || File.Exists(Path.Combine(paks, n + ".disabled")));
        var isEnabled = anyEnabled && !anyDisabled;

        return (isInstalled, isEnabled);
    }

    public async Task InstallAsync(LiveryPackage pkg, string gameRoot, bool enableAfterInstall, CancellationToken ct = default)
    {
        var paks = gamePaths.GetPaksPath(gameRoot);
        fs.EnsureDirectory(paks);

        // Copy as enabled by default; we can disable after if needed.
        var targetNames = GetTargetNames(pkg);
        var sourcePaths = GetSourcePaths(pkg);

        for (var i = 0; i < targetNames.Length; i++)
        {
            var dst = Path.Combine(paks, targetNames[i]);
            await fs.CopyFileAsync(sourcePaths[i], dst, overwrite: true, ct);
        }

        if (!enableAfterInstall)
            await DisableAsync(pkg, gameRoot, ct);

        await UpsertRegistryAsync(pkg, gameRoot, enableAfterInstall, ct);
    }

    public async Task UninstallAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
    {
        var paks = gamePaths.GetPaksPath(gameRoot);
        var targetNames = GetTargetNames(pkg);

        foreach (var n in targetNames)
        {
            fs.DeleteFile(Path.Combine(paks, n));
            fs.DeleteFile(Path.Combine(paks, n + ".disabled"));
        }

        var reg = await registryService.LoadAsync(ct);
        reg.Installed.RemoveAll(x => x.CarId == pkg.CarId && x.LiveryId == pkg.LiveryId);
        await registryService.SaveAsync(reg, ct);
    }

    public Task EnableAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
        => RenameDisabledSetAsync(pkg, gameRoot, enable: true);

    public Task DisableAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
        => RenameDisabledSetAsync(pkg, gameRoot, enable: false);

    public async Task ActivateForCarAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
    {
        // Disable all other installed packages for the same car (from registry).
        var reg = await registryService.LoadAsync(ct);
        var paks = gamePaths.GetPaksPath(gameRoot);

        foreach (var other in reg.Installed.Where(x => x.CarId == pkg.CarId).ToList())
        {
            if (other.LiveryId == pkg.LiveryId) continue;

            var files = other.Files;
            foreach (var file in files)
            {
                var enabled = Path.Combine(paks, file);
                var disabled = Path.Combine(paks, file + ".disabled");

                if (File.Exists(enabled))
                    fs.MoveFile(enabled, disabled, overwrite: true);
            }

            other.IsEnabled = false;
        }

        await registryService.SaveAsync(reg, ct);

        // Now enable target
        await EnableAsync(pkg, gameRoot, ct);

        // Update registry enabled state
        reg = await registryService.LoadAsync(ct);
        var entry = reg.Installed.FirstOrDefault(x => x.CarId == pkg.CarId && x.LiveryId == pkg.LiveryId);
        if (entry is not null)
        {
            entry.IsEnabled = true;
            await registryService.SaveAsync(reg, ct);
        }
    }

    private async Task RenameDisabledSetAsync(LiveryPackage pkg, string gameRoot, bool enable)
    {
        var paks = gamePaths.GetPaksPath(gameRoot);
        var targetNames = GetTargetNames(pkg);

        // enable: move *.disabled -> *
        // disable: move * -> *.disabled
        foreach (var n in targetNames)
        {
            var from = enable ? Path.Combine(paks, n + ".disabled") : Path.Combine(paks, n);
            var to = enable ? Path.Combine(paks, n) : Path.Combine(paks, n + ".disabled");

            if (File.Exists(from))
                fs.MoveFile(from, to, overwrite: true);
        }
    }

    private static string[] GetTargetNames(LiveryPackage pkg)
        => new[]
        {
            pkg.BaseName + ".pak",
            pkg.BaseName + ".utoc",
            pkg.BaseName + ".ucas"
        };

    private static string[] GetSourcePaths(LiveryPackage pkg)
        => new[]
        {
            pkg.PakPath,
            pkg.UtocPath,
            pkg.UcasPath
        };

    private async Task UpsertRegistryAsync(LiveryPackage pkg, string gameRoot, bool isEnabled, CancellationToken ct)
    {
        var reg = await registryService.LoadAsync(ct);
        reg.GameRoot = gameRoot;

        var existing = reg.Installed.FirstOrDefault(x => x.CarId == pkg.CarId && x.LiveryId == pkg.LiveryId);
        if (existing is null)
        {
            reg.Installed.Add(new InstalledLiveryEntry
            {
                CarId = pkg.CarId,
                LiveryId = pkg.LiveryId,
                BaseName = pkg.BaseName,
                Files = GetTargetNames(pkg),
                IsEnabled = isEnabled,
                InstalledUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.IsEnabled = isEnabled;
        }

        await registryService.SaveAsync(reg, ct);
    }
}