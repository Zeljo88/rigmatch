using Microsoft.Extensions.Options;

namespace RigMatch.Api.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootDirectory;

    public LocalFileStorageService(IWebHostEnvironment environment, IOptions<FileStorageOptions> options)
    {
        var localRoot = options.Value.LocalRoot;
        var relativeRoot = string.IsNullOrWhiteSpace(localRoot) ? "uploads" : localRoot.Trim().Replace('\\', '/');
        _rootDirectory = Path.Combine(environment.ContentRootPath, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
    }

    public async Task<string> SaveAsync(
        string sourceFilePath,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootDirectory);

        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.Combine(_rootDirectory, storedFileName);

        await using var sourceStream = File.OpenRead(sourceFilePath);
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        return $"{Path.GetFileName(_rootDirectory)}/{storedFileName}".Replace('\\', '/');
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveAbsolutePath(storagePath);
        if (!File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = File.OpenRead(absolutePath);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveAbsolutePath(storagePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveAbsolutePath(string storagePath)
    {
        var normalized = storagePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(Path.GetDirectoryName(_rootDirectory) ?? _rootDirectory, normalized);
    }
}
