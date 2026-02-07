using Microsoft.EntityFrameworkCore;
using Web.Mvc.Data;
using Web.Mvc.Models;

namespace Web.Mvc.Repositories;

public interface ISensorRepository
{
    Task<List<SensorSummary>> GetAllSensorsAsync();
    Task<SensorDetail?> GetSensorDetailAsync(string sensorId);
    Task<List<SensorMapPoint>> GetMapPointsAsync();
}

public class SensorRepository : ISensorRepository
{
    private readonly TelemetryDbContext _context;

    public SensorRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task<List<SensorSummary>> GetAllSensorsAsync()
    {
        return await _context.Sensors
            .Select(s => new SensorSummary
            {
                SensorId = s.SensorId,
                DisplayName = s.DisplayName ?? s.SensorId,
                SourceType = s.SourceType,
                EventCount = s.Events.Count,
                LastEventTime = s.Events.OrderByDescending(e => e.ObservedAt).FirstOrDefault()!.ObservedAt,
                Lat = s.Lat,
                Lon = s.Lon
            })
            .OrderBy(s => s.DisplayName)
            .ToListAsync();
    }

    public async Task<SensorDetail?> GetSensorDetailAsync(string sensorId)
    {
        var sensor = await _context.Sensors
            .FirstOrDefaultAsync(s => s.SensorId == sensorId);

        if (sensor == null) return null;

        // Get recent events separately (EF Core can't translate complex include expressions)
        var recentEvents = await _context.SensorEvents
            .Where(e => e.SensorId == sensorId)
            .OrderByDescending(e => e.ObservedAt)
            .Take(50)
            .ToListAsync();

        var totalEventCount = await _context.SensorEvents
            .CountAsync(e => e.SensorId == sensorId);

        return new SensorDetail
        {
            SensorId = sensor.SensorId,
            DisplayName = sensor.DisplayName ?? sensor.SensorId,
            SourceType = sensor.SourceType,
            Lat = sensor.Lat,
            Lon = sensor.Lon,
            CreatedAt = sensor.CreatedAt,
            UpdatedAt = sensor.UpdatedAt,
            RecentEvents = recentEvents
                .Select(e => new EventSummary
                {
                    EventId = e.EventId,
                    ObservedAt = e.ObservedAt,
                    StatusLevel = e.StatusLevel,
                    StatusMessage = e.StatusMessage,
                    Measurements = e.Measurements?.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                        ? e.Measurements.RootElement.EnumerateArray()
                            .Where(item => item.TryGetProperty("name", out _))
                            .Select(item => new KeyValuePair<string, object>(
                                item.GetProperty("name").GetString() ?? "",
                                item.TryGetProperty("value", out var val) ? val.ToString() : ""
                            ))
                            .ToList()
                        : []
                })
                .ToList(),
            TotalEventCount = totalEventCount
        };
    }

    public async Task<List<SensorMapPoint>> GetMapPointsAsync()
    {
        var sensors = await _context.Sensors
            .Where(s => s.Lat != null && s.Lon != null)
            .Select(s => new SensorMapPoint
            {
                SensorId = s.SensorId,
                DisplayName = s.DisplayName ?? s.SensorId,
                Lat = s.Lat!.Value,
                Lon = s.Lon!.Value,
                EventCount = s.Events.Count,
                LastEventTime = s.Events.OrderByDescending(e => e.ObservedAt).FirstOrDefault()!.ObservedAt
            })
            .OrderBy(s => s.DisplayName)
            .ToListAsync();

        foreach (var sensor in sensors)
        {
            var lastEvent = await _context.SensorEvents
                .Where(e => e.SensorId == sensor.SensorId)
                .OrderByDescending(e => e.ObservedAt)
                .FirstOrDefaultAsync();

            if (lastEvent?.Measurements?.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                sensor.LastMeasurements = lastEvent.Measurements.RootElement
                    .EnumerateArray()
                    .Where(item => item.TryGetProperty("name", out _))
                    .Select(item => new MeasurementItem
                    {
                        Name = item.GetProperty("name").GetString() ?? "",
                        Value = item.TryGetProperty("value", out var val) ? val.ToString() : "",
                        Unit = item.TryGetProperty("unit", out var unit) ? unit.ToString() : ""
                    })
                    .ToList();
            }

            if (lastEvent != null)
            {
                sensor.LastEventTime = lastEvent.ObservedAt;
            }
        }

        return sensors;
    }
}

// DTOs for UI
public class SensorSummary
{
    public string SensorId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public DateTime? LastEventTime { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
}

public class SensorDetail
{
    public string SensorId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TotalEventCount { get; set; }
    public List<EventSummary> RecentEvents { get; set; } = [];
}

public class EventSummary
{
    public string EventId { get; set; } = string.Empty;
    public DateTime ObservedAt { get; set; }
    public string StatusLevel { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public List<KeyValuePair<string, object>> Measurements { get; set; } = [];
}

public class SensorMapPoint
{
    public string SensorId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int EventCount { get; set; }
    public DateTime? LastEventTime { get; set; }
    public List<MeasurementItem> LastMeasurements { get; set; } = [];
}

public class MeasurementItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}
