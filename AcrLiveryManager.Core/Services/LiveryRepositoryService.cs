using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public sealed class LiveryRepositoryService : ILiveryRepositoryService
{
    public async Task<IReadOnlyList<Car>> ScanCarsAsync(string installerRoot, CancellationToken ct = default)
    {
        if (!Directory.Exists(installerRoot))
            return Array.Empty<Car>();

        var cars = new List<Car>();

        foreach (var carDir in Directory.EnumerateDirectories(installerRoot))
        {
            ct.ThrowIfCancellationRequested();

            var carId = Path.GetFileName(carDir);
            cars.Add(new Car(CarId: carId, DisplayName: carId));
        }

        await Task.CompletedTask;
        return cars;
    }

    public async Task<IReadOnlyList<LiveryPackage>> ScanLiveriesForCarAsync(string installerRoot, string carId, CancellationToken ct = default)
    {
        var carPath = Path.Combine(installerRoot, carId);
        if (!Directory.Exists(carPath))
            return Array.Empty<LiveryPackage>();

        var results = new List<LiveryPackage>();

        // liveryId folders
        foreach (var liveryDir in Directory.EnumerateDirectories(carPath))
        {
            ct.ThrowIfCancellationRequested();

            var liveryId = Path.GetFileName(liveryDir);

            // Case A: direct files in liveryDir (old layout)
            var direct = TryBuildPackageFromFolder(carId, liveryId, liveryDir, variantId: null);
            if (direct is not null)
            {
                results.Add(direct);
                continue;
            }

            // Case B: variants under liveryDir (new layout)
            foreach (var variantDir in Directory.EnumerateDirectories(liveryDir))
            {
                ct.ThrowIfCancellationRequested();

                var variantId = Path.GetFileName(variantDir);

                var pkg = TryBuildPackageFromFolder(carId, liveryId, variantDir, variantId);
                if (pkg is not null)
                    results.Add(pkg);
            }
        }

        await Task.CompletedTask;
        return results;
    }

    private static LiveryPackage? TryBuildPackageFromFolder(string carId, string liveryId, string folder, string? variantId)
    {
        var pak = Directory.EnumerateFiles(folder, "*.pak").FirstOrDefault();
        var utoc = Directory.EnumerateFiles(folder, "*.utoc").FirstOrDefault();
        var ucas = Directory.EnumerateFiles(folder, "*.ucas").FirstOrDefault();

        if (pak is null || utoc is null || ucas is null)
            return null;

        var baseName = Path.GetFileNameWithoutExtension(pak);

        // Optional preview (you already support it)
        var preview = Directory.EnumerateFiles(folder, "*.*")
            .FirstOrDefault(f =>
                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

        var displayName = variantId is null
            ? liveryId
            : $"{liveryId} ({VariantDisplay(variantId)})";

        // Important: keep LiveryId stable but unique if variants exist.
        // Your VM uses SelectedLivery.LiveryId.
        // Easiest MVP: encode variant into LiveryId so selection works without more refactors.
        var uniqueLiveryId = variantId is null ? liveryId : $"{liveryId}/{variantId}";

        return new LiveryPackage(
            CarId: carId,
            LiveryId: uniqueLiveryId,
            DisplayName: displayName,
            SourceFolder: folder,
            BaseName: baseName,
            PakPath: pak,
            UtocPath: utoc,
            UcasPath: ucas,
            PreviewPath: preview
        );
    }

    private static string VariantDisplay(string variantId) => variantId switch
    {
        "with-numbers" => "With numbers",
        "no-numbers" => "Without numbers",
        _ => variantId
    };
}