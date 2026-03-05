namespace AcrLiveryManager.Core.Models;

public sealed record ImportSelection(
    string CarId,
    string LiveryId,
    IReadOnlyList<string> VariantIdsToImport
);