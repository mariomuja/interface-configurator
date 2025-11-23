namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for file system operations (abstraction for testing)
/// </summary>
public interface IFileSystemService
{
    Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default);
    Task<string> ReadFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task WriteFileAsync(string filePath, string content, CancellationToken cancellationToken = default);
    Task<List<string>> FindFilesAsync(string pattern, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation using actual file system
/// </summary>
public class FileSystemService : IFileSystemService
{
    private readonly string _basePath;

    public FileSystemService()
    {
        // Default to project root (adjust based on deployment)
        _basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    public FileSystemService(string basePath)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
    }

    public Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_basePath, filePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<string> ReadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_basePath, filePath);
        return File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    public Task WriteFileAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_basePath, filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }

    public Task<List<string>> FindFilesAsync(string pattern, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        try
        {
            var files = Directory.GetFiles(_basePath, pattern, SearchOption.AllDirectories);
            results.AddRange(files.Select(f => Path.GetRelativePath(_basePath, f).Replace("\\", "/")));
        }
        catch
        {
            // Ignore errors
        }
        return Task.FromResult(results);
    }
}

