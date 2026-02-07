namespace Shared.Contracts;

public record SensorEvent
{
    public required string EventId { get; init; }
    public required string SensorId { get; init; }
    public required string SourceType { get; init; }
    public required string PayloadType { get; init; }
    public required int PayloadSizeBytes { get; init; }
    public string? PayloadJson { get; init; }
    public required DateTime ObservedAt { get; init; }
    public required DateTime ReceivedAt { get; init; }
    public required StatusLevel StatusLevel { get; init; }
    public string? StatusMessage { get; init; }
    public required IReadOnlyList<Measurement> Measurements { get; init; }
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public string? BlobUri { get; init; }
    public string? BlobSha256 { get; init; }
    public long? BlobBytes { get; init; }
}

public record Measurement
{
    public required string Name { get; init; }
    public required string Value { get; init; }
    public string? Unit { get; init; }
}
