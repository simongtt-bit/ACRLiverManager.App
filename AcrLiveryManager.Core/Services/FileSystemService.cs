using AcrLiveryManager.Core.Services;

namespace AcrLiveryManager.Core.Services;

public sealed class FileSystemService : IFileSystemService
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path)
        => Directory.Exists(path) ? Directory.EnumerateDirectories(path) : Enumerable.Empty<string>();

    public IEnumerable<string> EnumerateFiles(string path)
        => Directory.Exists(path) ? Directory.EnumerateFiles(path) : Enumerable.Empty<string>();

    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    public async Task CopyFileAsync(string sourcePath, string destPath, bool overwrite, CancellationToken ct)
    {
        EnsureDirectory(Path.GetDirectoryName(destPath) ?? throw new InvalidOperationException("Invalid destination path"));

        var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        await using var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var dst = new FileStream(destPath, mode, FileAccess.Write, FileShare.None);
        await src.CopyToAsync(dst, 81920, ct);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    public void MoveFile(string sourcePath, string destPath, bool overwrite)
    {
        if (!overwrite && File.Exists(destPath))
            throw new IOException($"Destination exists: {destPath}");

        if (overwrite && File.Exists(destPath))
            File.Delete(destPath);

        File.Move(sourcePath, destPath);
    }
}