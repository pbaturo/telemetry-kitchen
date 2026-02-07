using Consumer.Models;

namespace Consumer.Services;

public interface IDatabaseWriter
{
    Task WriteSensorReadingAsync(SensorReading reading, CancellationToken cancellationToken = default);
}
