namespace Web.Mvc.Models;

public class SensorEvent
{
    public string EventId { get; set; } = string.Empty;
    public string SensorId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string PayloadType { get; set; } = string.Empty;
    public int PayloadSizeB { get; set; }

    public DateTime ObservedAt { get; set; }
    public DateTime ReceivedAt { get; set; }

    public string StatusLevel { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }

    public Dictionary<string, object>? Measurements { get; set; }

    public string? BlobUri { get; set; }
    public string? BlobSha256 { get; set; }
    public long? BlobBytes { get; set; }

    public DateTime InsertedAt { get; set; }

    // Navigation
    public Sensor? Sensor { get; set; }
}
