using System.Windows.Media;

namespace ACRLiverManager.App.ViewModels;

public sealed class LiveryVm
{
    public required string CarId { get; init; }
    public required string LiveryId { get; init; }
    public required string DisplayName { get; init; }
    public required string BaseName { get; init; }
    public required string SourceFolder { get; init; }

    public string? PreviewPath { get; init; }
    public ImageSource? PreviewImage { get; set; }

    public InstallStateVm InstallState { get; } = new();
}