namespace ACRLiverManager.App.ViewModels;

public sealed class CarVm
{
    public required string CarId { get; init; }
    public required string DisplayName { get; init; }

    public int AvailableCount { get; set; }
    public int InstalledCount { get; set; }
    public bool HasEnabledLivery { get; set; }
}