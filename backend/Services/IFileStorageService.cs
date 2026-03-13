namespace RigMatch.Api.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(
        string sourceFilePath,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
