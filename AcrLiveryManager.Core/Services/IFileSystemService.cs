namespace AcrLiveryManager.Core.Services;

public interface IFileSystemService
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    IEnumerable<string> EnumerateDirectories(string path);
    IEnumerable<string> EnumerateFiles(string path);
    void EnsureDirectory(string path);

    Task CopyFileAsync(string sourcePath, string destPath, bool overwrite, CancellationToken ct);
    void DeleteFile(string path);

    void MoveFile(string sourcePath, string destPath, bool overwrite);
}