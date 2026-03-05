using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public interface ILiveryRepositoryService
{
    Task<IReadOnlyList<Car>> ScanCarsAsync(string installerRoot, CancellationToken ct = default);
    Task<IReadOnlyList<LiveryPackage>> ScanLiveriesForCarAsync(string installerRoot, string carId, CancellationToken ct = default);
}