using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public interface IAppSettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}