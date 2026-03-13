using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RigMatch.Api.Services;

public sealed class AzureBlobFileStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobFileStorageService(IOptions<FileStorageOptions> options)
    {
        var storageOptions = options.Value;
        if (string.IsNullOrWhiteSpace(storageOptions.ContainerName))
        {
            throw new InvalidOperationException("FileStorage:ContainerName must be configured for Azure Blob storage.");
        }

        if (!string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
        {
            _containerClient = new BlobContainerClient(storageOptions.ConnectionString, storageOptions.ContainerName);
            return;
        }

        if (string.IsNullOrWhiteSpace(storageOptions.AccountUrl))
        {
            throw new InvalidOperationException("Either FileStorage:ConnectionString or FileStorage:AccountUrl must be configured for Azure Blob storage.");
        }

        _containerClient = new BlobContainerClient(new Uri(storageOptions.AccountUrl), new DefaultAzureCredential());
    }

    public async Task<string> SaveAsync(
        string sourceFilePath,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var extension = Path.GetExtension(originalFileName);
        var blobName = $"uploads/{Guid.NewGuid():N}{extension}".Replace('\\', '/');
        var blobClient = _containerClient.GetBlobClient(blobName);

        await using var stream = File.OpenRead(sourceFilePath);
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
                }
            },
            cancellationToken);

        return blobName;
    }

    public async Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storagePath);
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var response = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        return response;
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storagePath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
