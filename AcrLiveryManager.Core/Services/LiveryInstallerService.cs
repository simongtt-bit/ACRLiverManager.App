using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public sealed class LiveryInstallerService(
    IGamePathService gamePaths,
    IInstallRegistryService registryService,
    IFileSystemService fs) : ILiveryInstallerService
{
    private const string StashPrefix = "__ACRLM__";

    public async Task<(bool IsInstalled, bool IsEnabled)> GetStateAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
    {
        var reg = await registryService.LoadAsync(ct);
        var paks = gamePaths.GetPaksPath(gameRoot);

        var entry = reg.Installed.FirstOrDefault(x => x.CarId == pkg.CarId && x.LiveryId == pkg.LiveryId);
        if (entry is null)
            return (false, false);

        var active = ActiveNames(entry.BaseName).All(n => File.Exists(Path.Combine(paks, n)));
        var stash = entry.StashFiles.All(n => File.Exists(Path.Combine(paks, n)));

        var isInstalled = active || stash;
        var isEnabled = active && entry.IsEnabled;

        return (isInstalled, isEnabled);
    }

    public async Task InstallAsync(LiveryPackage pkg, string gameRoot, bool enableAfterInstall,
        CancellationToken ct = default)
    {
        var paks = gamePaths.GetPaksPath(gameRoot);
        fs.EnsureDirectory(paks);

        var stash = StashNames(pkg.CarId, pkg.LiveryId, pkg.BaseName);
        var sources = new[] { pkg.PakPath, pkg.UtocPath, pkg.UcasPath };

        for (var i = 0; i < stash.Length; i++)
        {
            var dst = Path.Combine(paks, stash[i]);
            await fs.CopyFileAsync(sources[i], dst, overwrite: true, ct);
        }

        await UpsertRegistryAsync(pkg, gameRoot, isEnabled: false, stashFiles: stash, ct);

        if (enableAfterInstall)
            await ActivateForCarAsync(pkg, gameRoot, ct);
    }

    public async Task UninstallAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
    {
        var reg = await registryService.LoadAsync(ct);
        var paks = gamePaths.GetPaksPath(gameRoot);

        var entry = reg.Installed.FirstOrDefault(x => x.CarId == pkg.CarId && x.LiveryId == pkg.LiveryId);
        if (entry is null) return;

        if (entry.IsEnabled)
            await SwapActiveIntoStashAsync(entry, paks);

        foreach (var stashFile in entry.StashFiles)
            fs.DeleteFile(Path.Combine(paks, stashFile));

        reg.Installed.RemoveAll(x => x.CarId == pkg.CarId && x.LiveryId == pkg.LiveryId);
        await registryService.SaveAsync(reg, ct);
    }

    public Task EnableAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
        => RenameDisabledSetAsync(pkg, gameRoot, enable: true);

    public async Task DisableAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
    {
        var reg = await registryService.LoadAsync(ct);
        var paks = gamePaths.GetPaksPath(gameRoot);

        var entry = reg.Installed.FirstOrDefault(x => x.CarId == pkg.CarId && x.LiveryId == pkg.LiveryId);
        if (entry is null) return;

        if (!entry.IsEnabled) return;

        await SwapActiveIntoStashAsync(entry, paks);
        entry.IsEnabled = false;

        await registryService.SaveAsync(reg, ct);
    }

    public async Task ActivateForCarAsync(LiveryPackage pkg, string gameRoot, CancellationToken ct = default)
    {
        var reg = await registryService.LoadAsync(ct);
        var paks = gamePaths.GetPaksPath(gameRoot);

        var target = reg.Installed.FirstOrDefault(x => x.CarId == pkg.CarId && x.LiveryId == pkg.LiveryId);
        if (target is null)
            throw new InvalidOperationException("Livery is not installed. Install it before activating.");

        // 1) If another livery is enabled for this car, swap its active files back into its stash
        var currentlyEnabled =
            reg.Installed.FirstOrDefault(x => x.CarId == pkg.CarId && x.IsEnabled && x.LiveryId != pkg.LiveryId);
        if (currentlyEnabled is not null)
        {
            await SwapActiveIntoStashAsync(currentlyEnabled, paks);
            currentlyEnabled.IsEnabled = false;
        }

        // 2) Now swap target stash into active
        await SwapStashIntoActiveAsync(target, paks);
        target.IsEnabled = true;

        await registryService.SaveAsync(reg, ct);
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

    private async Task UpsertRegistryAsync(LiveryPackage pkg, string gameRoot, bool isEnabled, string[] stashFiles,
        CancellationToken ct)
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
                StashFiles = stashFiles,
                IsEnabled = isEnabled,
                InstalledUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.IsEnabled = isEnabled;
            // Keep stash in sync in case ids changed
            existing = existing with { StashFiles = stashFiles, BaseName = pkg.BaseName };
        }

        await registryService.SaveAsync(reg, ct);
    }

    private static string[] ActiveNames(string baseName) => new[]
    {
        baseName + ".pak",
        baseName + ".utoc",
        baseName + ".ucas"
    };

    private static string[] DisabledActiveNames(string baseName) => new[]
    {
        baseName + ".pak.disabled",
        baseName + ".utoc.disabled",
        baseName + ".ucas.disabled"
    };

    private static string[] StashNames(string carId, string liveryId, string baseName) => new[]
    {
        $"{StashPrefix}{carId}__{liveryId}__{baseName}.pak.disabled",
        $"{StashPrefix}{carId}__{liveryId}__{baseName}.utoc.disabled",
        $"{StashPrefix}{carId}__{liveryId}__{baseName}.ucas.disabled"
    };

    private Task SwapActiveIntoStashAsync(InstalledLiveryEntry entry, string paks)
    {
        var active = ActiveNames(entry.BaseName);
        for (var i = 0; i < active.Length; i++)
        {
            var activePath = Path.Combine(paks, active[i]);
            var stashPath = Path.Combine(paks, entry.StashFiles[i]);

            if (File.Exists(activePath))
                fs.MoveFile(activePath, stashPath, overwrite: true);
        }

        return Task.CompletedTask;
    }

    private Task SwapStashIntoActiveAsync(InstalledLiveryEntry entry, string paks)
    {
        var active = ActiveNames(entry.BaseName);
        for (var i = 0; i < active.Length; i++)
        {
            var stashPath = Path.Combine(paks, entry.StashFiles[i]);
            var activePath = Path.Combine(paks, active[i]);

            if (!File.Exists(stashPath))
                throw new FileNotFoundException($"Missing stash file: {stashPath}");

            fs.MoveFile(stashPath, activePath, overwrite: true);
        }

        return Task.CompletedTask;
    }
}