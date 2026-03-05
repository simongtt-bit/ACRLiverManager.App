namespace AcrLiveryManager.Core.Models;

public sealed record ImportArchiveResult(
    string ArchivePath,
    IReadOnlyList<DetectedVariant> Variants
);