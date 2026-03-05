using System.Text.RegularExpressions;
using AcrLiveryManager.Core.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace AcrLiveryManager.Core.Services;

public sealed class ArchiveImportService : IArchiveImportService
{
    private static readonly string[] ImageExts = [".png", ".jpg", ".jpeg", ".webp"];

    public async Task<ImportArchiveResult> AnalyzeArchiveAsync(string archivePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archive path is required.", nameof(archivePath));

        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found.", archivePath);

        var extractedRoot = await ExtractToTempAsync(archivePath, ct);
        return await AnalyzeExtractedRootAsync(archivePath, extractedRoot, ct);
    }

    public async Task ImportAsync(ImportArchiveResult analysis, string installerRoot, ImportSelection selection, CancellationToken ct)
    {
        if (analysis.Variants.Count == 0)
            throw new InvalidOperationException("No valid livery variants were detected in the archive.");

        if (string.IsNullOrWhiteSpace(installerRoot))
            throw new ArgumentException("Installer root is required.", nameof(installerRoot));

        Directory.CreateDirectory(installerRoot);

        var carId = SanitizeId(selection.CarId);
        var liveryId = SanitizeId(selection.LiveryId);

        if (string.IsNullOrWhiteSpace(carId))
            throw new ArgumentException("CarId is required.", nameof(selection));

        if (string.IsNullOrWhiteSpace(liveryId))
            throw new ArgumentException("LiveryId is required.", nameof(selection));

        // Re-extract fresh so the file paths definitely exist during the copy stage
        var extractedRoot = await ExtractToTempAsync(analysis.ArchivePath, ct);
        var fresh = await AnalyzeExtractedRootAsync(analysis.ArchivePath, extractedRoot, ct);

        HashSet<string>? allowed = null;
        if (selection.VariantIdsToImport is { Count: > 0 })
            allowed = new HashSet<string>(selection.VariantIdsToImport, StringComparer.OrdinalIgnoreCase);

        foreach (var variant in fresh.Variants)
        {
            ct.ThrowIfCancellationRequested();

            if (allowed is not null && !allowed.Contains(variant.VariantId))
                continue;

            // Put each variant in its own folder to prevent collisions (Withnum/Withoutnum often have identical file names)
            var destDir = Path.Combine(installerRoot, carId, liveryId, variant.VariantId);
            Directory.CreateDirectory(destDir);

            // Copy pak/utoc/ucas (preserve filenames)
            foreach (var triple in variant.Triples)
            {
                ct.ThrowIfCancellationRequested();

                File.Copy(triple.PakPath, Path.Combine(destDir, Path.GetFileName(triple.PakPath)), overwrite: true);
                File.Copy(triple.UtocPath, Path.Combine(destDir, Path.GetFileName(triple.UtocPath)), overwrite: true);
                File.Copy(triple.UcasPath, Path.Combine(destDir, Path.GetFileName(triple.UcasPath)), overwrite: true);
            }

            // Derive the paksDir from the first triple’s pak file location
            var first = variant.Triples.FirstOrDefault();
            if (first is not null)
            {
                var paksDir = Path.GetDirectoryName(first.PakPath);
                if (!string.IsNullOrWhiteSpace(paksDir))
                    CopyPreviewIfFound(extractedRoot, paksDir!, destDir);
            }
        }

        await Task.CompletedTask;
    }

    // ---------------- internals ----------------

    private static async Task<ImportArchiveResult> AnalyzeExtractedRootAsync(string archivePath, string extractedRoot, CancellationToken ct)
    {
        // Detect packs that include the game-ready folder chain: acr/Content/Paks
        var paksDirs = Directory.EnumerateDirectories(extractedRoot, "Paks", SearchOption.AllDirectories)
            .Where(d =>
            {
                var norm = NormalizeSlashes(d).ToLowerInvariant();
                return norm.Contains("/acr/") && norm.Contains("/content/") && norm.EndsWith("/paks");
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paksDirs.Count == 0)
            paksDirs.Add(extractedRoot);

        var detected = new List<DetectedVariant>();

        foreach (var paksDir in paksDirs)
        {
            ct.ThrowIfCancellationRequested();

            var triples = FindTriplesInDirectory(paksDir);
            if (triples.Count == 0)
                continue;

            var variantId = GuessVariantIdFromPath(paksDir);
            detected.Add(new DetectedVariant(
                VariantId: variantId,
                DisplayName: VariantIdToDisplay(variantId),
                Triples: triples,
                SourceHint: MakeSourceHint(extractedRoot, paksDir)
            ));
        }

        // Merge if multiple directories map to same variantId
        var merged = detected
            .GroupBy(v => v.VariantId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var allTriples = g.SelectMany(x => x.Triples).ToList();
                var hint = string.Join(" | ", g.Select(x => x.SourceHint).Distinct());
                return first with { Triples = allTriples, SourceHint = hint };
            })
            .ToList();

        await Task.CompletedTask;
        return new ImportArchiveResult(archivePath, merged);
    }

    private static async Task<string> ExtractToTempAsync(string archivePath, CancellationToken ct)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ACRLM", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        // Most common SharpCompress API:
        using var archive = ArchiveFactory.OpenArchive(archivePath);

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            ct.ThrowIfCancellationRequested();

            // Path traversal guard
            var safeKey = entry.Key.Replace('\\', '/');
            safeKey = Regex.Replace(safeKey, @"^\/*", "");
            if (safeKey.Contains("../", StringComparison.Ordinal))
                continue;

            var destPath = Path.Combine(tempRoot, safeKey.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            entry.WriteToFile(destPath, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }

        await Task.CompletedTask;
        return tempRoot;
    }

    private static List<FileTriple> FindTriplesInDirectory(string paksDir)
    {
        var files = Directory.EnumerateFiles(paksDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                var ext = Path.GetExtension(f);
                return ext.Equals(".pak", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".utoc", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".ucas", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var groups = files.GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase);

        var triples = new List<FileTriple>();

        foreach (var g in groups)
        {
            var baseName = g.Key;

            var pak = g.FirstOrDefault(x => x.EndsWith(".pak", StringComparison.OrdinalIgnoreCase));
            var utoc = g.FirstOrDefault(x => x.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase));
            var ucas = g.FirstOrDefault(x => x.EndsWith(".ucas", StringComparison.OrdinalIgnoreCase));

            if (pak is null || utoc is null || ucas is null)
                continue;

            triples.Add(new FileTriple(baseName, pak, utoc, ucas));
        }

        return triples;
    }

    private static string GuessVariantIdFromPath(string path)
    {
        var p = NormalizeSlashes(path).ToLowerInvariant();

        if (p.Contains("/withoutnum") || p.Contains("/without_num") || p.Contains("/no_numbers")
            || p.Contains("/nonumbers") || p.Contains("/plain") || p.Contains("/blank"))
            return "no-numbers";

        if (p.Contains("/withnum") || p.Contains("/with_num") || p.Contains("/numbers") || p.Contains("/numbered"))
            return "with-numbers";

        return "default";
    }

    private static string VariantIdToDisplay(string variantId) => variantId switch
    {
        "with-numbers" => "With numbers",
        "no-numbers" => "Without numbers",
        _ => "Default"
    };

    private static string MakeSourceHint(string extractedRoot, string paksDir)
    {
        var rel = Path.GetRelativePath(extractedRoot, paksDir);
        return NormalizeSlashes(rel);
    }

    private static string NormalizeSlashes(string s) => s.Replace('\\', '/');

    private static string SanitizeId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var s = input.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, @"[^a-z0-9\-_\.]+", "");
        s = s.Trim('-', '_', '.');

        return s;
    }

    private static string? FindBestPreviewNear(string extractedRoot, string paksDir)
    {
        // Search near the variant root.
        // Start from parent of ".../acr/Content/Paks"
        var candidateRoot = Directory.GetParent(paksDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
        var root = candidateRoot ?? extractedRoot;

        var images = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => ImageExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (images.Count == 0) return null;

        static int Score(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            var score = 0;
            if (name.Contains("preview")) score += 50;
            if (name.Contains("thumb")) score += 40;
            if (name.Contains("thumbnail")) score += 40;
            if (name.Contains("shot")) score += 20;
            if (name.Contains("screenshot")) score += 20;
            if (name.Contains("livery")) score += 20;
            return score;
        }

        return images
            .Select(p => new { Path = p, Score = Score(p), Size = new FileInfo(p).Length })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Size)
            .First()
            .Path;
    }

    private static void CopyPreviewIfFound(string extractedRoot, string paksDir, string destDir)
    {
        var best = FindBestPreviewNear(extractedRoot, paksDir);
        if (best is null) return;

        var ext = Path.GetExtension(best);
        var dest = Path.Combine(destDir, "preview" + ext);

        if (File.Exists(dest)) return;

        File.Copy(best, dest, overwrite: false);
    }
}