using System.Text;
using System.Text.Json;
using Gateway.Models;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Gateway.Services;

public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private const string ExchangeName = "telemetry.readings";
    private const string QueueName = "sensor.readings";

    public RabbitMqPublisher(
        IOptions<RabbitMqConfiguration> config,
        ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = config.Value.Host,
            Port = config.Value.Port,
            UserName = config.Value.Username,
            Password = config.Value.Password
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare exchange and queue
        _channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true);

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.QueueBind(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "sensor.*");

        _logger.LogInformation("RabbitMQ publisher initialized");
    }

    public Task PublishAsync(SensorReading reading, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = JsonSerializer.Serialize(reading);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var routingKey = $"sensor.{reading.SensorType}";

            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogDebug("Published reading from sensor {SensorId}", reading.SensorId);
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message for sensor {SensorId}", reading.SensorId);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
