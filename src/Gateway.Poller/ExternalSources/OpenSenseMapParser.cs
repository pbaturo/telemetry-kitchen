using System.Text.Json;
using Shared.Contracts;

namespace Gateway.Poller.ExternalSources;

public static class OpenSenseMapParser
{
    public static void Parse(
        string responseBody,
        List<Measurement> measurements,
        ref DateTime? observedAt,
        ref StatusLevel statusLevel,
        ref string statusMessage)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var openSenseData = JsonSerializer.Deserialize<OpenSenseMapResponse>(responseBody, options);
        if (openSenseData?.Sensors != null)
        {
            foreach (var sensor in openSenseData.Sensors)
            {
                if (sensor.LastMeasurement?.Value != null)
                {
                    measurements.Add(new Measurement
                    {
                        Name = sensor.Title ?? "unknown",
                        Value = sensor.LastMeasurement.Value,
                        Unit = sensor.Unit
                    });

                    if (DateTime.TryParse(sensor.LastMeasurement.CreatedAt, out var measurementTime))
                    {
                        if (observedAt == null || measurementTime > observedAt)
                        {
                            observedAt = measurementTime;
                        }
                    }
                }
            }

            if (measurements.Count == 0)
            {
                statusLevel = StatusLevel.WARN;
                statusMessage = "No measurements available";
                measurements.Add(new Measurement
                {
                    Name = "raw",
                    Value = responseBody.Length > 200 ? responseBody[..200] : responseBody,
                    Unit = null
                });
            }
        }
        else
        {
            statusLevel = StatusLevel.WARN;
            statusMessage = "Invalid response structure";
        }
    }
}

public class OpenSenseMapResponse
{
    public string? _id { get; set; }
    public string? Name { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public LocationInfo? CurrentLocation { get; set; }
    public List<SensorInfo>? Sensors { get; set; }
    public string? LastMeasurementAt { get; set; }
}

public class LocationInfo
{
    public List<double>? Coordinates { get; set; }
    public string? Type { get; set; }
}

public class SensorInfo
{
    public string? Title { get; set; }
    public string? Unit { get; set; }
    public string? SensorType { get; set; }
    public string? Icon { get; set; }
    public string? _id { get; set; }
    public LastMeasurementInfo? LastMeasurement { get; set; }
}

public class LastMeasurementInfo
{
    public string? Value { get; set; }
    public string? CreatedAt { get; set; }
}
