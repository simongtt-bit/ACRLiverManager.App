using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public sealed class LiveryRepositoryService(IFileSystemService fs) : ILiveryRepositoryService
{
    private static readonly string[] PreviewNames =
    [
        "preview.png", "preview.jpg", "preview.jpeg"
    ];

    public Task<IReadOnlyList<Car>> ScanCarsAsync(string installerRoot, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(installerRoot) || !fs.DirectoryExists(installerRoot))
            return Task.FromResult<IReadOnlyList<Car>>([]);

        var cars = fs.EnumerateDirectories(installerRoot)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(id => new Car(id!, id!))
            .OrderBy(c => c.DisplayName)
            .ToList();

        return Task.FromResult<IReadOnlyList<Car>>(cars);
    }

    public Task<IReadOnlyList<LiveryPackage>> ScanLiveriesForCarAsync(string installerRoot, string carId, CancellationToken ct = default)
    {
        var carPath = Path.Combine(installerRoot, carId);
        if (!fs.DirectoryExists(carPath))
            return Task.FromResult<IReadOnlyList<LiveryPackage>>([]);

        var liveries = new List<LiveryPackage>();

        foreach (var liveryDir in fs.EnumerateDirectories(carPath))
        {
            ct.ThrowIfCancellationRequested();

            var liveryId = Path.GetFileName(liveryDir) ?? "";
            if (string.IsNullOrWhiteSpace(liveryId)) continue;

            var files = fs.EnumerateFiles(liveryDir).ToList();

            string? pak = files.FirstOrDefault(f => f.EndsWith(".pak", StringComparison.OrdinalIgnoreCase));
            string? utoc = files.FirstOrDefault(f => f.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase));
            string? ucas = files.FirstOrDefault(f => f.EndsWith(".ucas", StringComparison.OrdinalIgnoreCase));

            if (pak is null || utoc is null || ucas is null)
                continue;

            var basePak = Path.GetFileNameWithoutExtension(pak);
            var baseUtoc = Path.GetFileNameWithoutExtension(utoc);
            var baseUcas = Path.GetFileNameWithoutExtension(ucas);

            // Strict: must match exactly and only one bundle supported for MVP.
            if (!string.Equals(basePak, baseUtoc, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(basePak, baseUcas, StringComparison.OrdinalIgnoreCase))
                continue;

            var preview = FindPreview(liveryDir, files);

            liveries.Add(new LiveryPackage(
                CarId: carId,
                LiveryId: liveryId,
                DisplayName: liveryId,
                SourceFolder: liveryDir,
                BaseName: basePak,
                PakPath: pak,
                UtocPath: utoc,
                UcasPath: ucas,
                PreviewPath: preview
            ));
        }

        liveries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<LiveryPackage>>(liveries);
    }

    private static string? FindPreview(string liveryDir, List<string> files)
    {
        foreach (var name in PreviewNames)
        {
            var p = Path.Combine(liveryDir, name);
            if (File.Exists(p)) return p;
        }

        // fallback: first image
        return files.FirstOrDefault(f =>
            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
    }
}