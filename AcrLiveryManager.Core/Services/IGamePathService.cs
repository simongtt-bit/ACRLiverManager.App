namespace AcrLiveryManager.Core.Services;

public interface IGamePathService
{
    bool IsValidGameRoot(string root);
    string GetPaksPath(string gameRoot);
    bool CanWriteToPaks(string gameRoot, out string? reason);
}