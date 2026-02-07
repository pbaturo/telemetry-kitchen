using Gateway.Models;

namespace Gateway.Services;

public interface IMessagePublisher
{
    Task PublishAsync(SensorReading reading, CancellationToken cancellationToken = default);
}
