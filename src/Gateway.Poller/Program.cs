using System.Diagnostics;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using Serilog;
using Shared.Contracts;
using Gateway.Poller.Publishing;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    try
    {
        configuration.ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId();
        
        Console.WriteLine("[Serilog] Configured with Console and Loki sinks");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Serilog] Configuration error: {ex.Message}");
        throw;
    }
});

// Add services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IRabbitMqPublisher>(sp =>
    new RabbitMqPublisher(builder.Configuration, sp.GetRequiredService<ILogger<RabbitMqPublisher>>()));
builder.Services.AddHostedService<PollerService>();
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.Configure<Scenario1Config>(builder.Configuration.GetSection("Scenario1"));

var app = builder.Build();

// Map Prometheus metrics endpoint
app.MapMetrics();

await app.RunAsync();

// Configuration models
public class Scenario1Config
{
    public List<StationConfig> Stations { get; set; } = new();
}

public class StationConfig
{
    public required string SensorId { get; set; }
    public required string Name { get; set; }
    public required string Url { get; set; }
    public string SourceType { get; set; } = "opensensemap";
    public int PollIntervalSeconds { get; set; } = 60;
    public double Lat { get; set; }
    public double Lon { get; set; }
    public long? LocationId { get; set; }
}

// Background service for polling stations
public class PollerService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PollerService> _logger;
    private readonly MetricsCollector _metrics;
    private readonly IRabbitMqPublisher _publisher;
    private readonly List<StationConfig> _stations;

    public PollerService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PollerService> logger,
        MetricsCollector metrics,
        IRabbitMqPublisher publisher)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _metrics = metrics;
        _publisher = publisher;
        _stations = configuration.GetSection("Scenario1:Stations").Get<List<StationConfig>>() ?? new();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Gateway.Poller with {Count} stations", _stations.Count);

        var tasks = _stations.Select(station => PollStationLoop(station, stoppingToken)).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task PollStationLoop(StationConfig station, CancellationToken stoppingToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var retryDelay = TimeSpan.FromSeconds(5);
        const int maxRetryDelay = 300; // 5 minutes

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await PollStation(station, cts.Token);
                retryDelay = TimeSpan.FromSeconds(5); // Reset on success
                await Task.Delay(TimeSpan.FromSeconds(station.PollIntervalSeconds), cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling station {SensorId}, retrying in {Delay}s", 
                    station.SensorId, retryDelay.TotalSeconds);
                
                await Task.Delay(retryDelay, cts.Token);
                
                // Exponential backoff
                retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, maxRetryDelay));
            }
        }
    }

    private async Task PollStation(StationConfig station, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // Set User-Agent for Sensor.Community API compatibility
        httpClient.DefaultRequestHeaders.Add("User-Agent", "TelemetryKitchen/1.0 (+https://github.com/telemetry-kitchen)");

        HttpResponseMessage? response = null;
        string? responseBody = null;
        int httpStatus = 0;
        StatusLevel statusLevel = StatusLevel.INFO;
        string statusMessage = "OK";
        List<Measurement> measurements = new();
        DateTime? observedAt = null;
        var sourceType = string.IsNullOrWhiteSpace(station.SourceType)
            ? "opensensemap"
            : station.SourceType.Trim();

        try
        {
            _metrics.PollsTotal.Inc();

            response = await httpClient.GetAsync(station.Url, cancellationToken);
            httpStatus = (int)response.StatusCode;
            response.EnsureSuccessStatusCode();

            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var payloadBytes = Encoding.UTF8.GetByteCount(responseBody);

            sw.Stop();
            var pollDurationMs = sw.ElapsedMilliseconds;
            _metrics.PollDuration.Observe(pollDurationMs);

            // Parse response based on source type
            try
            {
                if (sourceType.Equals("sensor-community", StringComparison.OrdinalIgnoreCase) ||
                    sourceType.Equals("sensorcommunity", StringComparison.OrdinalIgnoreCase) ||
                    sourceType.Equals("sensor_community", StringComparison.OrdinalIgnoreCase))
                {
                    ParseSensorCommunity(responseBody, station, measurements, ref observedAt, ref statusLevel, ref statusMessage);
                }
                else
                {
                    ParseOpenSenseMap(responseBody, measurements, ref observedAt, ref statusLevel, ref statusMessage);
                }
            }
            catch (JsonException ex)
            {
                statusLevel = StatusLevel.WARN;
                statusMessage = $"Parse error: {ex.Message}".Substring(0, Math.Min(250, $"Parse error: {ex.Message}".Length));
                measurements.Add(new Measurement
                {
                    Name = "raw",
                    Value = responseBody.Length > 200 ? responseBody.Substring(0, 200) : responseBody,
                    Unit = null
                });
            }

            var receivedAt = DateTime.UtcNow;
            var effectiveObservedAt = observedAt ?? receivedAt;

            // Create deterministic event ID
            var eventId = GenerateEventId(station.SensorId, effectiveObservedAt, payloadBytes, responseBody);

            var sensorEvent = new SensorEvent
            {
                EventId = eventId,
                SensorId = station.SensorId,
                SourceType = sourceType,
                PayloadType = "json",
                PayloadSizeBytes = payloadBytes,
                PayloadJson = responseBody,
                ObservedAt = effectiveObservedAt,
                ReceivedAt = receivedAt,
                StatusLevel = statusLevel,
                StatusMessage = statusMessage,
                Measurements = measurements,
                Lat = station.Lat,
                Lon = station.Lon
            };

            // Publish to RabbitMQ (durability gate)
            var publishSw = Stopwatch.StartNew();
            await _publisher.PublishAsync(sensorEvent, cancellationToken);
            publishSw.Stop();
            var publishDurationMs = publishSw.ElapsedMilliseconds;

            _metrics.EventsPublished.Inc();
            _metrics.PublishDuration.Observe(publishDurationMs);
            _metrics.LastSuccessTime.WithLabels(station.SensorId).SetToCurrentTimeUtc();

            // Structured logging
            _logger.LogInformation(
                "Poll completed: sensorId={SensorId}, httpStatus={HttpStatus}, durationMs={DurationMs}, " +
                "payloadBytes={PayloadBytes}, statusLevel={StatusLevel}, publishDurationMs={PublishDurationMs}",
                station.SensorId, httpStatus, pollDurationMs, payloadBytes, statusLevel, publishDurationMs);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _metrics.PollsFailed.Inc();
            statusLevel = StatusLevel.ERROR;
            statusMessage = $"HTTP error: {ex.Message}".Substring(0, Math.Min(250, $"HTTP error: {ex.Message}".Length));

            _logger.LogWarning(
                "Poll failed: sensorId={SensorId}, httpStatus={HttpStatus}, durationMs={DurationMs}, " +
                "statusLevel={StatusLevel}, error={Error}",
                station.SensorId, httpStatus, sw.ElapsedMilliseconds, statusLevel, ex.Message);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.PollsFailed.Inc();
            _logger.LogError(ex, "Unexpected error polling {SensorId}", station.SensorId);
            throw;
        }
    }

    private string GenerateEventId(string sensorId, DateTime observedAt, int payloadBytes, string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{sensorId}|{observedAt:O}|{payloadBytes}|{payload}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ParseOpenSenseMap(
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
                    Value = responseBody.Length > 200 ? responseBody.Substring(0, 200) : responseBody,
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

    private static void ParseSensorCommunity(
        string responseBody,
        StationConfig station,
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

        // Collect data from all readings in the bbox area (LocationId used for station identification, not filtering)
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

// Prometheus metrics collector
public class MetricsCollector
{
    public Counter PollsTotal { get; } = Metrics.CreateCounter(
        "tk_polls_total", "Total number of station polls attempted");
    
    public Counter PollsFailed { get; } = Metrics.CreateCounter(
        "tk_polls_failed_total", "Total number of failed station polls");
    
    public Counter EventsPublished { get; } = Metrics.CreateCounter(
        "tk_events_published_total", "Total number of events published to RabbitMQ");
    
    public Histogram PollDuration { get; } = Metrics.CreateHistogram(
        "tk_poll_duration_ms", "Duration of HTTP polls in milliseconds",
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(10, 2, 10) });
    
    public Histogram PublishDuration { get; } = Metrics.CreateHistogram(
        "tk_publish_duration_ms", "Duration of RabbitMQ publishes in milliseconds",
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(1, 2, 10) });
    
    public Gauge LastSuccessTime { get; } = Metrics.CreateGauge(
        "tk_last_success_unixtime", "Unix timestamp of last successful poll",
        new GaugeConfiguration { LabelNames = new[] { "sensorId" } });
}

// OpenSenseMap API response models
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
    public List<double>? Coordinates { get; set; } // [lon, lat, alt]
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

// Sensor.Community API response models
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
