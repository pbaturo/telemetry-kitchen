using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Contracts;

namespace Gateway.Poller.ExternalSources;

public static class SensorCommunityParser
{
    public static void Parse(
        string responseBody,
        List<Measurement> measurements,
        ref DateTime? observedAt,
        ref StatusLevel statusLevel,
        ref string statusMessage)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var readings = JsonSerializer.Deserialize<List<SensorCommunityReading>>(responseBody, options);
        if (readings == null || readings.Count == 0)
        {
            statusLevel = StatusLevel.WARN;
            statusMessage = "Empty Sensor.Community response";
            return;
        }

        bool foundData = false;
        foreach (var reading in readings)
        {
            var sensorType = NormalizeMeasurementName(reading.Sensor?.SensorType?.Name);
            if (reading.Sensordatavalues == null || reading.Sensordatavalues.Count == 0)
            {
                continue;
            }

            foreach (var value in reading.Sensordatavalues)
            {
                if (string.IsNullOrWhiteSpace(value.ValueType))
                {
                    continue;
                }

                var valueName = NormalizeMeasurementName(value.ValueType);
                var measurementName = string.IsNullOrWhiteSpace(sensorType)
                    ? valueName
                    : $"{sensorType}.{valueName}";

                measurements.Add(new Measurement
                {
                    Name = measurementName,
                    Value = value.Value?.ToString() ?? string.Empty,
                    Unit = null
                });
                foundData = true;
            }

            if (!string.IsNullOrWhiteSpace(reading.Timestamp) &&
                DateTime.TryParse(reading.Timestamp, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var measurementTime))
            {
                if (observedAt == null || measurementTime > observedAt)
                {
                    observedAt = measurementTime;
                }
            }
        }

        if (!foundData)
        {
            statusLevel = StatusLevel.WARN;
            statusMessage = "No measurements available";
        }
    }

    private static string NormalizeMeasurementName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "unknown";
        }

        return name.Trim().ToLowerInvariant().Replace(" ", "_");
    }
}

public class SensorCommunityReading
{
    public long Id { get; set; }
    public SensorCommunityLocation? Location { get; set; }
    public SensorCommunitySensor? Sensor { get; set; }
    [JsonPropertyName("sensordatavalues")]
    public List<SensorCommunityValue>? Sensordatavalues { get; set; }
    public string? Timestamp { get; set; }
}

public class SensorCommunityLocation
{
    public long Id { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public string? Country { get; set; }
    public string? Altitude { get; set; }
    [JsonPropertyName("exact_location")]
    public int? ExactLocation { get; set; }
    public int? Indoor { get; set; }
}

public class SensorCommunitySensor
{
    public string? Pin { get; set; }
    public long? Id { get; set; }
    [JsonPropertyName("sensor_type")]
    public SensorCommunitySensorType? SensorType { get; set; }
}

public class SensorCommunitySensorType
{
    public long? Id { get; set; }
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
}

public class SensorCommunityValue
{
    [JsonPropertyName("value_type")]
    public string? ValueType { get; set; }
    public object? Value { get; set; }
    public long? Id { get; set; }
}
