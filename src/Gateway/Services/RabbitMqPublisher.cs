using System.Text;
using System.Text.Json;
using Gateway.Models;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Gateway.Services;

public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
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

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        // Declare exchange and queue
        _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true).GetAwaiter().GetResult();

        _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false).GetAwaiter().GetResult();

        _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "sensor.*").GetAwaiter().GetResult();

        _logger.LogInformation("RabbitMQ publisher initialized");
    }

    public async Task PublishAsync(SensorReading reading, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = JsonSerializer.Serialize(reading);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            var routingKey = $"sensor.{reading.SensorType}";

            await _channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Published reading from sensor {SensorId}", reading.SensorId);
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
