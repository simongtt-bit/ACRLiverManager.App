using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public interface IArchiveImportService
{
    Task<ImportArchiveResult> AnalyzeArchiveAsync(string archivePath, CancellationToken ct);
    Task ImportAsync(ImportArchiveResult analysis, string installerRoot, ImportSelection selection, CancellationToken ct);
}