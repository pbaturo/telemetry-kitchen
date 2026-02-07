using System.Text.Json;
using Gateway.Models;
using Microsoft.Extensions.Options;

namespace Gateway.Services;

public class SensorPollingService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMessagePublisher _messagePublisher;
    private readonly MetricsService _metricsService;
    private readonly ILogger<SensorPollingService> _logger;
    private readonly int _intervalSeconds;

    // Using OpenWeatherMap as an example public sensor API
    // For production, you would use actual IoT sensor APIs
    private readonly List<SensorConfig> _sensorConfigs = new()
    {
        new SensorConfig
        {
            Id = "openweather-london",
            Type = "temperature",
            Url = "https://api.open-meteo.com/v1/forecast?latitude=51.5074&longitude=-0.1278&current=temperature_2m",
            Location = "London, UK"
        },
        new SensorConfig
        {
            Id = "openweather-newyork",
            Type = "temperature",
            Url = "https://api.open-meteo.com/v1/forecast?latitude=40.7128&longitude=-74.0060&current=temperature_2m",
            Location = "New York, USA"
        },
        new SensorConfig
        {
            Id = "openweather-tokyo",
            Type = "temperature",
            Url = "https://api.open-meteo.com/v1/forecast?latitude=35.6762&longitude=139.6503&current=temperature_2m",
            Location = "Tokyo, Japan"
        }
    };

    public SensorPollingService(
        IHttpClientFactory httpClientFactory,
        IMessagePublisher messagePublisher,
        MetricsService metricsService,
        IOptions<PollingConfiguration> pollingConfig,
        ILogger<SensorPollingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _messagePublisher = messagePublisher;
        _metricsService = metricsService;
        _logger = logger;
        _intervalSeconds = pollingConfig.Value.IntervalSeconds;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sensor polling service started. Polling interval: {Interval} seconds", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollSensorsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }

    private async Task PollSensorsAsync(CancellationToken cancellationToken)
    {
        var tasks = _sensorConfigs.Select(config => PollSensorAsync(config, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task PollSensorAsync(SensorConfig config, CancellationToken cancellationToken)
    {
        using var timer = _metricsService.MeasurePollDuration(config.Type);
        
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetStringAsync(config.Url, cancellationToken);
            
            // Parse the response (Open-Meteo API format)
            var data = JsonSerializer.Deserialize<OpenMeteoResponse>(response);
            
            if (data?.Current != null)
            {
                var reading = new SensorReading
                {
                    SensorId = config.Id,
                    SensorType = config.Type,
                    Timestamp = DateTime.UtcNow,
                    Value = (decimal)data.Current.Temperature2m,
                    Unit = "celsius",
                    Location = config.Location,
                    Metadata = new Dictionary<string, object>
                    {
                        { "source", "open-meteo" },
                        { "api_version", "v1" }
                    }
                };

                await _messagePublisher.PublishAsync(reading, cancellationToken);
                
                _metricsService.RecordPoll(config.Type, true);
                _logger.LogInformation("Polled sensor {SensorId}: {Value}{Unit}", 
                    config.Id, reading.Value, reading.Unit);
            }
        }
        catch (Exception ex)
        {
            _metricsService.RecordPoll(config.Type, false);
            _metricsService.RecordError("poll_failed");
            _logger.LogError(ex, "Failed to poll sensor {SensorId}", config.Id);
        }
    }

    private class SensorConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    private class OpenMeteoResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("current")]
        public CurrentWeather? Current { get; set; }
    }

    private class CurrentWeather
    {
        [System.Text.Json.Serialization.JsonPropertyName("temperature_2m")]
        public double Temperature2m { get; set; }
    }
}
