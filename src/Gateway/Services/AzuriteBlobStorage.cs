using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Gateway.Services;

public class AzuriteBlobStorage : IBlobStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzuriteBlobStorage> _logger;
    private const string ContainerName = "sensor-images";
    private bool _containerInitialized = false;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    public AzuriteBlobStorage(
        IOptions<AzuriteConfiguration> config,
        ILogger<AzuriteBlobStorage> logger)
    {
        _logger = logger;
        _blobServiceClient = new BlobServiceClient(config.Value.ConnectionString);
        
        _logger.LogInformation("Azurite blob storage initialized");
    }

    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (_containerInitialized)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (!_containerInitialized)
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                _containerInitialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> UploadImageAsync(
        string sensorId,
        byte[] imageData,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureContainerExistsAsync(cancellationToken);

            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobName = $"{sensorId}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}.jpg";
            var blobClient = containerClient.GetBlobClient(blobName);

            using var stream = new MemoryStream(imageData);
            
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            };

            await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

            _logger.LogInformation("Uploaded image for sensor {SensorId}: {BlobName}", sensorId, blobName);
            
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image for sensor {SensorId}", sensorId);
            throw;
        }
    }
}
