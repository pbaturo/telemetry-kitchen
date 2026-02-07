namespace Ingest.Consumer.Persistence.Schema;

public static class SqlStatements
{
    public const string UpsertSensor = @"
        INSERT INTO sensors (sensor_id, source_type, display_name, lat, lon, created_at, updated_at)
        VALUES (@sensor_id, @source_type, @display_name, @lat, @lon, now(), now())
        ON CONFLICT (sensor_id) 
        DO UPDATE SET 
            source_type = EXCLUDED.source_type,
            display_name = EXCLUDED.display_name,
            lat = EXCLUDED.lat,
            lon = EXCLUDED.lon,
            updated_at = now()";

    public const string InsertSensorEvent = @"
        INSERT INTO sensor_events 
        (event_id, sensor_id, source_type, payload_type, payload_size_b, payload_json,
         observed_at, received_at, status_level, status_message, measurements)
        VALUES 
        (@event_id, @sensor_id, @source_type, @payload_type, @payload_size_b, @payload_json::jsonb,
         @observed_at, @received_at, @status_level, @status_message, @measurements::jsonb)
        ON CONFLICT (event_id) DO NOTHING";

    public const string CheckEventExists = @"
        SELECT 1 FROM sensor_events WHERE event_id = @event_id LIMIT 1";
}
