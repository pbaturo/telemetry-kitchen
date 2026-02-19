using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gateway.Poller.ExternalSources;
using Gateway.Poller.Metrics;
using Gateway.Poller.Options;
using Gateway.Poller.Publishing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using Shared.Contracts;

namespace Gateway.Poller.Services;

public class PollerService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PollerService> _logger;
    private readonly MetricsCollector _metrics;
    private readonly IRabbitMqPublisher _publisher;
    private readonly List<StationConfig> _stations;
    private const int MaxHttpAttempts = 3;

    public PollerService(
        IHttpClientFactory httpClientFactory,
        ILogger<PollerService> logger,
        MetricsCollector metrics,
        IRabbitMqPublisher publisher,
        IOptions<Scenario1Config> scenarioConfig)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _metrics = metrics;
        _publisher = publisher;
        _stations = scenarioConfig.Value.Stations ?? new();
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
        const int maxRetryDelay = 300;

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await PollStation(station, cts.Token);
                retryDelay = TimeSpan.FromSeconds(5);
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
                retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, maxRetryDelay));
            }
        }
    }

    private async Task PollStation(StationConfig station, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "TelemetryKitchen/1.0 (+https://github.com/telemetry-kitchen)");

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

            var responseBody = await FetchResponseBodyWithRetriesAsync(
                httpClient,
                station,
                cancellationToken,
                status => httpStatus = status);
            var payloadBytes = Encoding.UTF8.GetByteCount(responseBody);

            sw.Stop();
            var pollDurationMs = sw.ElapsedMilliseconds;
            _metrics.PollDuration.Observe(pollDurationMs);

            try
            {
                if (sourceType.Equals("sensor-community", StringComparison.OrdinalIgnoreCase) ||
                    sourceType.Equals("sensorcommunity", StringComparison.OrdinalIgnoreCase) ||
                    sourceType.Equals("sensor_community", StringComparison.OrdinalIgnoreCase))
                {
                    SensorCommunityParser.Parse(
                        responseBody, measurements, ref observedAt, ref statusLevel, ref statusMessage);
                }
                else
                {
                    OpenSenseMapParser.Parse(
                        responseBody, measurements, ref observedAt, ref statusLevel, ref statusMessage);
                }
            }
            catch (JsonException ex)
            {
                statusLevel = StatusLevel.WARN;
                statusMessage = $"Parse error: {ex.Message}".Substring(0, Math.Min(250, $"Parse error: {ex.Message}".Length));
                measurements.Add(new Measurement
                {
                    Name = "raw",
                    Value = responseBody.Length > 200 ? responseBody[..200] : responseBody,
                    Unit = null
                });
            }

            var receivedAt = DateTime.UtcNow;
            var effectiveObservedAt = observedAt ?? receivedAt;
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

            var publishSw = Stopwatch.StartNew();
            await _publisher.PublishAsync(sensorEvent, cancellationToken);
            publishSw.Stop();
            var publishDurationMs = publishSw.ElapsedMilliseconds;

            _metrics.EventsPublished.Inc();
            _metrics.PublishDuration.Observe(publishDurationMs);
            _metrics.LastSuccessTime.WithLabels(station.SensorId).SetToCurrentTimeUtc();

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

    private static string GenerateEventId(string sensorId, DateTime observedAt, int payloadBytes, string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{sensorId}|{observedAt:O}|{payloadBytes}|{payload}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<string> FetchResponseBodyWithRetriesAsync(
        HttpClient httpClient,
        StationConfig station,
        CancellationToken cancellationToken,
        Action<int> setStatusCode)
    {
        for (var attempt = 1; attempt <= MaxHttpAttempts; attempt++)
        {
            using var response = await httpClient.GetAsync(station.Url, cancellationToken);

            var statusCode = (int)response.StatusCode;
            setStatusCode(statusCode);

            if (statusCode == 429)
            {
                _metrics.PollsFailedHttp429.Inc();
            }
            else if (statusCode >= 500)
            {
                _metrics.PollsFailedHttp5xx.Inc();
            }

            var shouldRetry = (statusCode == 429 || statusCode >= 500) && attempt < MaxHttpAttempts;
            if (!shouldRetry)
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }

            _metrics.PollRetries.Inc();
            var retryDelay = CalculateRetryDelayWithJitter(attempt);
            _logger.LogDebug(
                "Transient HTTP failure for {SensorId} (status={StatusCode}, attempt={Attempt}/{MaxAttempts}); retrying in {DelayMs}ms",
                station.SensorId, statusCode, attempt, MaxHttpAttempts, (int)retryDelay.TotalMilliseconds);
            await Task.Delay(retryDelay, cancellationToken);
        }

        // Defensive fallback: loop should always return or throw before this.
        throw new InvalidOperationException($"Failed to fetch response for {station.SensorId}");
    }

    private static TimeSpan CalculateRetryDelayWithJitter(int attempt)
    {
        var exponentialMs = Math.Min(500 * (int)Math.Pow(2, attempt - 1), 5000);
        var jitterMs = Random.Shared.Next(100, 500);
        return TimeSpan.FromMilliseconds(exponentialMs + jitterMs);
    }
}
