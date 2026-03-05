using System.Text.Json;
using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string GetPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ACRLiveryManager");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        var path = GetPath();
        if (!File.Exists(path)) return new AppSettings();

        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AppSettings>(fs, JsonOptions, ct)
               ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var path = GetPath();
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, settings, JsonOptions, ct);
    }
}