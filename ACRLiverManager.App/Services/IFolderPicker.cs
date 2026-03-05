namespace ACRLiverManager.App.Services;

public interface IFolderPicker
{
    string? PickFolder(string title, string? initialPath = null);
}