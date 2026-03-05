namespace AcrLiveryManager.Core.Models;

public sealed record FileTriple(
    string BaseName,
    string PakPath,
    string UtocPath,
    string UcasPath
);