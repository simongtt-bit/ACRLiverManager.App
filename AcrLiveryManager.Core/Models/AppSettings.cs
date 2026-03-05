namespace AcrLiveryManager.Core.Models;

public sealed record AppSettings
{
    public string GameRoot { get; set; } = "";
    public string InstallerRoot { get; set; } = "";
    public bool AutoRescanOnStartup { get; set; } = true;
}