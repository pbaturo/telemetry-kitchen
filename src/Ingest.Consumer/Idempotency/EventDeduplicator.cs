using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ingest.Consumer.Idempotency;

public interface IEventDeduplicator
{
    Task<bool> IsNewEventAsync(string eventId, CancellationToken cancellationToken = default);
}

public class EventDeduplicator : IEventDeduplicator
{
    private readonly string _connectionString;
    private readonly ILogger<EventDeduplicator> _logger;

    public EventDeduplicator(IConfiguration configuration, ILogger<EventDeduplicator> logger)
    {
        _connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Postgres connection string not configured");
        _logger = logger;
    }

    public async Task<bool> IsNewEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new NpgsqlCommand(
                "SELECT 1 FROM sensor_events WHERE event_id = @event_id LIMIT 1",
                conn);
            cmd.Parameters.AddWithValue("event_id", eventId);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            var isNew = result == null;

            if (!isNew)
            {
                _logger.LogWarning("Duplicate event detected: {EventId}", eventId);
            }

            return isNew;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking event duplication for {EventId}", eventId);
            throw;
        }
    }
}
