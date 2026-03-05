namespace AcrLiveryManager.Core.Models;

public sealed record InstalledLiveryEntry
{
    public required string CarId { get; init; }
    public required string LiveryId { get; init; }

    // The game-facing base name (e.g. "FabiaCherain_p")
    public required string BaseName { get; init; }

    // Stash files in Paks (filenames only, each ends with ".disabled")
    public required string[] StashFiles { get; init; }

    public required bool IsEnabled { get; set; }
    public required DateTime InstalledUtc { get; init; }
}