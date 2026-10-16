namespace NutriCare.Infrastructure.Storage;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using NutriCare.Domain.Interfaces;
using DomainBlobDownloadResult = NutriCare.Domain.Interfaces.BlobDownloadResult;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureBlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("AzureBlobStorage:ConnectionString is not configured");

        _containerName = configuration["AzureBlobStorage:MedicalReportsContainer"] ?? "medical-reports";
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<BlobUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var blobName = $"{folder.Trim('/').ToLowerInvariant()}/{DateTime.UtcNow:yyyy/MM}/{storedFileName}";
        var blob = container.GetBlobClient(blobName);

        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);
        return new BlobUploadResult(_containerName, blobName, storedFileName, blob.Uri);
    }

    public async Task<DomainBlobDownloadResult> DownloadAsync(
        string containerName,
        string blobName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var blob = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new DomainBlobDownloadResult(response.Value.Content, contentType);
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var blob = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
