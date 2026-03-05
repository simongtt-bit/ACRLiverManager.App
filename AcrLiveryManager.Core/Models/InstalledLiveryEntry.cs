namespace AcrLiveryManager.Core.Models;

public sealed record InstalledLiveryEntry
{
    public required string CarId { get; init; }
    public required string LiveryId { get; init; }
    public required string BaseName { get; init; }
    public required string[] Files { get; init; }
    public required bool IsEnabled { get; set; }
    public required DateTime InstalledUtc { get; init; }
}