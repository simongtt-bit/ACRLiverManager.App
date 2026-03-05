namespace ACRLiverManager.App.ViewModels;

public sealed class InstallStateVm
{
    public bool IsInstalled { get; set; }
    public bool IsEnabled { get; set; }

    public string StateText =>
        !IsInstalled ? "Not installed" :
        IsEnabled ? "Installed (Enabled)" :
        "Installed (Disabled)";
}