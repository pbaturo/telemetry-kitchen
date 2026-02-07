using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Gateway.Services;

public class AzuriteBlobStorage : IBlobStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzuriteBlobStorage> _logger;
    private const string ContainerName = "sensor-images";

    public AzuriteBlobStorage(
        IOptions<AzuriteConfiguration> config,
        ILogger<AzuriteBlobStorage> logger)
    {
        _logger = logger;
        _blobServiceClient = new BlobServiceClient(config.Value.ConnectionString);
        _containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        
        // Create container if it doesn't exist
        _containerClient.CreateIfNotExistsAsync().GetAwaiter().GetResult();
        
        _logger.LogInformation("Azurite blob storage initialized");
    }

    public async Task<string> UploadImageAsync(
        string sensorId,
        byte[] imageData,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobName = $"{sensorId}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}.jpg";
            var blobClient = _containerClient.GetBlobClient(blobName);

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
