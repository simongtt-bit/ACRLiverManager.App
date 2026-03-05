using System.Text.Json;
using AcrLiveryManager.Core.Models;

namespace AcrLiveryManager.Core.Services;

public sealed class InstallRegistryService : IInstallRegistryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string GetRegistryPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AcrLiveryManager");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "installed.json");
    }

    public async Task<InstallRegistry> LoadAsync(CancellationToken ct = default)
    {
        var path = GetRegistryPath();
        if (!File.Exists(path)) return new InstallRegistry();

        await using var fs = File.OpenRead(path);
        var registry = await JsonSerializer.DeserializeAsync<InstallRegistry>(fs, JsonOptions, ct);
        return registry ?? new InstallRegistry();
    }

    public async Task SaveAsync(InstallRegistry registry, CancellationToken ct = default)
    {
        var path = GetRegistryPath();
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, registry, JsonOptions, ct);
    }
}