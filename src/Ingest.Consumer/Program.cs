using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using Serilog;
using Shared.Contracts;
using Ingest.Consumer.Consuming;
using Ingest.Consumer.Idempotency;
using Ingest.Consumer.Metrics;
using Ingest.Consumer.Persistence;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", false, true);
        config.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json", true, true);
        config.AddEnvironmentVariables();
    })
    .UseSerilog((context, configuration) =>
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
    })
    .ConfigureServices((context, services) =>
    {
        // Persistence
        services.AddSingleton<IEventDeduplicator, EventDeduplicator>();
        services.AddSingleton<IPostgresWriter, PostgresWriter>();

        // Metrics
        services.AddSingleton<ConsumerMetrics>();

        // Hosted services
        services.AddHostedService<ConsumerHostedService>();
    })
    .Build();

// Expose Prometheus metrics on port 9091 (for this app's internal metrics)
var metricsServer = new KestrelMetricServer(port: 9092);
metricsServer.Start();

Console.WriteLine("Ingest.Consumer starting...");
await host.RunAsync();

// Hosted service that manages consumer lifecycle
public class ConsumerHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConsumerHostedService> _logger;
    private readonly ConsumerMetrics _metrics;
    private readonly IServiceProvider _serviceProvider;
    private IRabbitMqConsumer? _consumer;

    public ConsumerHostedService(
        IConfiguration configuration,
        ILogger<ConsumerHostedService> logger,
        ConsumerMetrics metrics,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _metrics = metrics;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("ConsumerHostedService starting");

            // Create and start consumer
            _consumer = new RabbitMqConsumer(
                _configuration,
                HandleEventAsync,
                _serviceProvider.GetRequiredService<ILogger<RabbitMqConsumer>>());

            await _consumer.StartAsync(stoppingToken);

            _logger.LogInformation("Consumer started successfully");

            // Keep the service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumer shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in consumer service");
            throw;
        }
        finally
        {
            if (_consumer != null)
            {
                await _consumer.StopAsync();
            }
        }
    }

    private async Task<bool> HandleEventAsync(SensorEvent sensorEvent, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _metrics.EventsConsumed.Inc();

            // Check for duplicates
            var deduplicator = _serviceProvider.GetRequiredService<IEventDeduplicator>();
            var isNew = await deduplicator.IsNewEventAsync(sensorEvent.EventId, cancellationToken);

            if (!isNew)
            {
                _metrics.DuplicateEvents.Inc();
                _logger.LogWarning("Rejected duplicate event: {EventId}", sensorEvent.EventId);
                return true; // Return true to ACK (don't requeue)
            }

            // Write to database
            var writer = _serviceProvider.GetRequiredService<IPostgresWriter>();
            var written = await writer.WriteEventAsync(sensorEvent, cancellationToken);

            sw.Stop();
            _metrics.DbWriteDuration.Observe(sw.ElapsedMilliseconds);

            if (written)
            {
                _metrics.EventsProcessed.Inc();
                _logger.LogInformation(
                    "Event persisted: eventId={EventId}, sensorId={SensorId}, durationMs={DurationMs}",
                    sensorEvent.EventId, sensorEvent.SensorId, sw.ElapsedMilliseconds);
                return true;
            }
            else
            {
                _logger.LogWarning("Event write returned false (possible duplicate): {EventId}",
                    sensorEvent.EventId);
                return true; // Still ACK - it's a duplicate
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.EventsFailed.Inc();
            _logger.LogError(ex,
                "Failed to process event: eventId={EventId}, sensorId={SensorId}",
                sensorEvent.EventId, sensorEvent.SensorId);
            return false; // Requeue on error
        }
    }
}
