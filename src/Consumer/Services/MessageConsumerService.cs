using System.Text;
using System.Text.Json;
using Consumer.Models;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consumer.Services;

public class MessageConsumerService : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly IDatabaseWriter _databaseWriter;
    private readonly MetricsService _metricsService;
    private readonly ILogger<MessageConsumerService> _logger;
    private const string QueueName = "sensor.readings";

    public MessageConsumerService(
        IOptions<RabbitMqConfiguration> config,
        IDatabaseWriter databaseWriter,
        MetricsService metricsService,
        ILogger<MessageConsumerService> logger)
    {
        _databaseWriter = databaseWriter;
        _metricsService = metricsService;
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

        // Ensure queue exists
        _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false).GetAwaiter().GetResult();

        _logger.LogInformation("RabbitMQ consumer initialized");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message consumer service started");

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                _logger.LogDebug("Received message: {Message}", message);

                var reading = JsonSerializer.Deserialize<SensorReading>(message);
                
                if (reading != null)
                {
                    await _databaseWriter.WriteSensorReadingAsync(reading, stoppingToken);
                    
                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                    
                    _metricsService.RecordMessageProcessed(QueueName, true);
                    _logger.LogInformation("Processed reading from sensor {SensorId}", reading.SensorId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                _metricsService.RecordMessageProcessed(QueueName, false);
                _metricsService.RecordError("message_processing_failed");
                
                // Reject and requeue the message
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
