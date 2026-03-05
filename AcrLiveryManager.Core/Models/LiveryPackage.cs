namespace AcrLiveryManager.Core.Models;

public sealed record LiveryPackage(
    string CarId,
    string LiveryId,
    string DisplayName,
    string SourceFolder,
    string BaseName,
    string PakPath,
    string UtocPath,
    string UcasPath,
    string? PreviewPath
);