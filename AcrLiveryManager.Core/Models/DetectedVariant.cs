namespace AcrLiveryManager.Core.Models;

public sealed record DetectedVariant(
    string VariantId,                 // "with-numbers", "no-numbers", "default"
    string DisplayName,               // "With numbers", ...
    IReadOnlyList<FileTriple> Triples,
    string SourceHint                // for UI/debug e.g. "Withnum/acr/Content/Paks"
);