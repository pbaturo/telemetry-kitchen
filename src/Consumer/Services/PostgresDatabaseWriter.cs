using System.Text.Json;
using Consumer.Models;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Consumer.Services;

public class PostgresDatabaseWriter : IDatabaseWriter
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresDatabaseWriter> _logger;
    private readonly MetricsService _metricsService;

    public PostgresDatabaseWriter(
        IOptions<DatabaseConfiguration> config,
        ILogger<PostgresDatabaseWriter> logger,
        MetricsService metricsService)
    {
        _connectionString = config.Value.ConnectionString;
        _logger = logger;
        _metricsService = metricsService;
    }

    public async Task WriteSensorReadingAsync(SensorReading reading, CancellationToken cancellationToken = default)
    {
        using var timer = _metricsService.MeasureDbWrite("sensor_readings");

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                INSERT INTO sensor_readings 
                (sensor_id, sensor_type, timestamp, value, unit, location, metadata)
                VALUES 
                (@sensor_id, @sensor_type, @timestamp, @value, @unit, @location, @metadata::jsonb)";

            await using var command = new NpgsqlCommand(sql, connection);
            
            command.Parameters.AddWithValue("sensor_id", reading.SensorId);
            command.Parameters.AddWithValue("sensor_type", reading.SensorType);
            command.Parameters.AddWithValue("timestamp", reading.Timestamp);
            command.Parameters.AddWithValue("value", reading.Value);
            command.Parameters.AddWithValue("unit", reading.Unit ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("location", reading.Location ?? (object)DBNull.Value);
            
            var metadataJson = reading.Metadata != null 
                ? JsonSerializer.Serialize(reading.Metadata)
                : null;
            command.Parameters.AddWithValue("metadata", metadataJson ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Wrote sensor reading for {SensorId} to database", reading.SensorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write sensor reading for {SensorId} to database", reading.SensorId);
            throw;
        }
    }
}
