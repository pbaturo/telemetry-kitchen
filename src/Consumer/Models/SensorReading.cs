namespace Consumer.Models;

public class SensorReading
{
    public string SensorId { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal Value { get; set; }
    public string? Unit { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
