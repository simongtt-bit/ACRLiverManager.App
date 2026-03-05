namespace AcrLiveryManager.Core.Models;

public sealed record InstallRegistry
{
    public string? GameRoot { get; set; }
    public List<InstalledLiveryEntry> Installed { get; set; } = [];
}