using Ookii.Dialogs.Wpf;

namespace ACRLiverManager.App.Services;

public sealed class FolderPicker : IFolderPicker
{
    public string? PickFolder(string title, string? initialPath = null)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            SelectedPath = string.IsNullOrWhiteSpace(initialPath) ? null : initialPath,
            ShowNewFolderButton = true
        };

        return dialog.ShowDialog() == true ? dialog.SelectedPath : null;
    }
}