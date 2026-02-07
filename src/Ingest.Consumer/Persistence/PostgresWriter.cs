using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared.Contracts;
using Ingest.Consumer.Persistence.Schema;

namespace Ingest.Consumer.Persistence;

public interface IPostgresWriter
{
    Task<bool> WriteEventAsync(SensorEvent sensorEvent, CancellationToken cancellationToken = default);
}

public class PostgresWriter : IPostgresWriter
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresWriter> _logger;

    public PostgresWriter(IConfiguration configuration, ILogger<PostgresWriter> logger)
    {
        _connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Postgres connection string not configured");
        _logger = logger;
    }

    public async Task<bool> WriteEventAsync(SensorEvent sensorEvent, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var transaction = await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            // UPSERT sensors table
            await using (var cmd = new NpgsqlCommand(SqlStatements.UpsertSensor, conn, transaction))
            {
                cmd.Parameters.AddWithValue("sensor_id", (object?)sensorEvent.SensorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("source_type", (object?)sensorEvent.SourceType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("display_name", (object?)sensorEvent.SensorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("lat", DBNull.Value);
                cmd.Parameters.AddWithValue("lon", DBNull.Value);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // INSERT sensor_events (ignore duplicates)
            await using (var cmd = new NpgsqlCommand(SqlStatements.InsertSensorEvent, conn, transaction))
            {
                cmd.Parameters.AddWithValue("event_id", (object?)sensorEvent.EventId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("sensor_id", (object?)sensorEvent.SensorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("source_type", (object?)sensorEvent.SourceType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("payload_type", (object?)sensorEvent.PayloadType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("payload_size_b", sensorEvent.PayloadSizeBytes);
                cmd.Parameters.AddWithValue("observed_at", sensorEvent.ObservedAt);
                cmd.Parameters.AddWithValue("received_at", sensorEvent.ReceivedAt);
                cmd.Parameters.AddWithValue("status_level", sensorEvent.StatusLevel.ToString());
                cmd.Parameters.AddWithValue("status_message", (object?)sensorEvent.StatusMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("measurements", JsonSerializer.Serialize(sensorEvent.Measurements));

                var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                if (rowsAffected > 0)
                {
                    _logger.LogDebug("Event written to database: eventId={EventId}, sensorId={SensorId}",
                        sensorEvent.EventId, sensorEvent.SensorId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Event insert returned 0 rows (likely duplicate): eventId={EventId}",
                        sensorEvent.EventId);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to write event to database: eventId={EventId}", sensorEvent.EventId);
            throw;
        }
    }
}
