using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using Shared.Contracts;
using Gateway.Poller.Publishing;

var builder = WebApplication.CreateBuilder(args);

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
    public int PollIntervalSeconds { get; set; } = 60;
    public double Lat { get; set; }
    public double Lon { get; set; }
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

        HttpResponseMessage? response = null;
        string? responseBody = null;
        int httpStatus = 0;
        StatusLevel statusLevel = StatusLevel.INFO;
        string statusMessage = "OK";
        List<Measurement> measurements = new();
        DateTime? observedAt = null;

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

            // Parse OpenSenseMap response
            try
            {
                var openSenseData = JsonSerializer.Deserialize<OpenSenseMapResponse>(responseBody);
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

                            // Use the most recent measurement timestamp as observedAt
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
                SourceType = "public-environment",
                PayloadType = "json",
                PayloadSizeBytes = payloadBytes,
                ObservedAt = effectiveObservedAt,
                ReceivedAt = receivedAt,
                StatusLevel = statusLevel,
                StatusMessage = statusMessage,
                Measurements = measurements
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
