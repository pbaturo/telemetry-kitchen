namespace Gateway.Services;

public interface IBlobStorage
{
    Task<string> UploadImageAsync(string sensorId, byte[] imageData, string contentType, CancellationToken cancellationToken = default);
}
