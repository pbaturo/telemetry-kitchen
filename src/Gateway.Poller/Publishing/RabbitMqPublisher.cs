using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.Contracts;

namespace Gateway.Poller.Publishing;

public interface IRabbitMqPublisher
{
    Task PublishAsync(SensorEvent sensorEvent, CancellationToken cancellationToken = default);
    ValueTask DisposeAsync();
}

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        _exchangeName = "sensor-events";

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "tk",
            Password = configuration["RabbitMQ:Password"] ?? "tk",
            VirtualHost = "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Ensure exchange exists (idempotent)
        _channel.ExchangeDeclare(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        _logger.LogInformation("RabbitMQ publisher initialized: {Host}:{Port}", 
            factory.HostName, factory.Port);
    }

    public async Task PublishAsync(SensorEvent sensorEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(sensorEvent);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.ContentType = "application/json";
            properties.DeliveryMode = 2; // Persistent
            properties.MessageId = sensorEvent.EventId;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var routingKey = $"sensor.{sensorEvent.SensorId}.{sensorEvent.SourceType}";

            _channel.BasicPublish(
                exchange: _exchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogDebug(
                "Published event: eventId={EventId}, sensorId={SensorId}, routingKey={RoutingKey}, bytes={Bytes}",
                sensorEvent.EventId, sensorEvent.SensorId, routingKey, body.Length);

            // Allow async callers to switch context
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventId} for sensor {SensorId}",
                sensorEvent.EventId, sensorEvent.SensorId);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            _channel.Close();
            _channel.Dispose();
        }

        if (_connection != null)
        {
            _connection.Close();
            _connection.Dispose();
        }

        _logger.LogInformation("RabbitMQ publisher disposed");
        await ValueTask.CompletedTask;
    }
}
