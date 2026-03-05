namespace AcrLiveryManager.Core.Services;

public sealed class GamePathService : IGamePathService
{
    public bool IsValidGameRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return false;
        var paks = GetPaksPath(root);
        return Directory.Exists(paks);
    }

    public string GetPaksPath(string gameRoot)
        => Path.Combine(gameRoot, "acr", "Content", "Paks");

    public bool CanWriteToPaks(string gameRoot, out string? reason)
    {
        reason = null;
        var paks = GetPaksPath(gameRoot);

        if (!Directory.Exists(paks))
        {
            reason = "Paks folder not found under the selected game root.";
            return false;
        }

        try
        {
            var testFile = Path.Combine(paks, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "No write permission to the Paks folder. If the game is under Program Files, run the app as Administrator.";
            return false;
        }
        catch (Exception ex)
        {
            reason = $"Cannot write to Paks folder: {ex.Message}";
            return false;
        }
    }
}