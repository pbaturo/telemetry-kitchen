namespace Web.Mvc.Models;

public class Sensor
{
    public string SensorId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ICollection<SensorEvent> Events { get; set; } = [];
}
