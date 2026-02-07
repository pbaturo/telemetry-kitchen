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
    private readonly IModel _channel;
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

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Ensure queue exists
        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        _logger.LogInformation("RabbitMQ consumer initialized");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message consumer service started");

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
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
                    
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    
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
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);

        // Keep the service running
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
