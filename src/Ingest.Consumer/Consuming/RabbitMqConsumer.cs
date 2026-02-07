using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;

namespace Ingest.Consumer.Consuming;

public interface IRabbitMqConsumer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync();
}

public delegate Task<bool> EventProcessedHandler(SensorEvent sensorEvent, CancellationToken cancellationToken);

public class RabbitMqConsumer : IRabbitMqConsumer
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName;
    private readonly EventProcessedHandler _eventHandler;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private EventingBasicConsumer? _consumer;
    private string? _consumerTag;

    public RabbitMqConsumer(
        IConfiguration configuration,
        EventProcessedHandler eventHandler,
        ILogger<RabbitMqConsumer> logger)
    {
        _logger = logger;
        _eventHandler = eventHandler;
        _queueName = "ingest-events";

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

        // Ensure queue exists (idempotent)
        _channel.QueueDeclare(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                { "x-queue-mode", "lazy" }
            });

        // Set QoS
        _channel.BasicQos(0, 10, false);

        _logger.LogInformation("RabbitMQ consumer initialized: {Host}:{Port}, queue={Queue}",
            factory.HostName, factory.Port, _queueName);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += (model, evnt) => OnMessageReceived(evnt, cancellationToken);

            _consumerTag = _channel.BasicConsume(
                queue: _queueName,
                autoAck: false,
                consumerTag: $"consumer-{Environment.MachineName}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                consumer: _consumer);

            _logger.LogInformation("Consumer started: consumerTag={ConsumerTag}", _consumerTag);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start consumer");
            throw;
        }
    }

    private void OnMessageReceived(BasicDeliverEventArgs evnt, CancellationToken cancellationToken)
    {
        // Fire and forget with error handling
        _ = Task.Run(async () => await OnMessageReceivedAsync(evnt, cancellationToken), cancellationToken);
    }

    private async Task OnMessageReceivedAsync(BasicDeliverEventArgs evnt, CancellationToken cancellationToken)
    {
        var body = evnt.Body.ToArray();
        var json = Encoding.UTF8.GetString(body);
        var deliveryTag = evnt.DeliveryTag;

        try
        {
            var sensorEvent = JsonSerializer.Deserialize<SensorEvent>(json);
            if (sensorEvent == null)
            {
                _logger.LogWarning("Failed to deserialize event: {Json}", json);
                _channel.BasicNack(deliveryTag, false, false);
                return;
            }

            _logger.LogDebug("Processing event: eventId={EventId}, sensorId={SensorId}",
                sensorEvent.EventId, sensorEvent.SensorId);

            var processed = await _eventHandler(sensorEvent, cancellationToken);

            if (processed)
            {
                _channel.BasicAck(deliveryTag, false);
                _logger.LogDebug("Event acknowledged: eventId={EventId}", sensorEvent.EventId);
            }
            else
            {
                // Requeue on transient failure
                _channel.BasicNack(deliveryTag, false, true);
                _logger.LogWarning("Event requeued: eventId={EventId}", sensorEvent.EventId);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed: {Json}", json);
            _channel.BasicNack(deliveryTag, false, false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Processing cancelled for deliveryTag={DeliveryTag}", deliveryTag);
            _channel.BasicNack(deliveryTag, false, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing event: deliveryTag={DeliveryTag}", deliveryTag);
            _channel.BasicNack(deliveryTag, false, true);
        }
    }

    public async ValueTask StopAsync()
    {
        if (_consumerTag != null && _channel != null)
        {
            try
            {
                _channel.BasicCancel(_consumerTag);
                _logger.LogInformation("Consumer cancelled: {ConsumerTag}", _consumerTag);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling consumer");
            }
        }

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

        _logger.LogInformation("RabbitMQ consumer disposed");
        await ValueTask.CompletedTask;
    }
}
