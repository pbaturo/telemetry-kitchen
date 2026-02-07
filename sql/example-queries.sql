-- Telemetry Kitchen - Example SQL Queries
-- Connect to database: docker exec -it telemetry-postgres psql -U telemetry -d telemetry_db

-- ============================================
-- Basic Data Exploration
-- ============================================

-- View recent sensor readings
SELECT 
    sensor_id,
    sensor_type,
    value,
    unit,
    location,
    timestamp,
    ingested_at
FROM sensor_readings 
ORDER BY timestamp DESC 
LIMIT 20;

-- Count readings by sensor
SELECT 
    sensor_id,
    sensor_type,
    COUNT(*) as reading_count,
    MIN(timestamp) as first_reading,
    MAX(timestamp) as last_reading
FROM sensor_readings
GROUP BY sensor_id, sensor_type
ORDER BY reading_count DESC;

-- View sensor reading distribution
SELECT 
    sensor_type,
    COUNT(*) as total_readings,
    AVG(value) as avg_value,
    MIN(value) as min_value,
    MAX(value) as max_value,
    STDDEV(value) as stddev_value
FROM sensor_readings
GROUP BY sensor_type;

-- ============================================
-- Performance Analysis
-- ============================================

-- Ingestion latency analysis
SELECT 
    sensor_id,
    AVG(EXTRACT(EPOCH FROM (ingested_at - timestamp))) as avg_latency_seconds,
    MAX(EXTRACT(EPOCH FROM (ingested_at - timestamp))) as max_latency_seconds,
    COUNT(*) as sample_count
FROM sensor_readings
WHERE ingested_at > NOW() - INTERVAL '1 hour'
GROUP BY sensor_id
ORDER BY avg_latency_seconds DESC;

-- Hourly ingestion rate
SELECT 
    DATE_TRUNC('hour', ingested_at) as hour,
    COUNT(*) as readings_per_hour
FROM sensor_readings
WHERE ingested_at > NOW() - INTERVAL '24 hours'
GROUP BY DATE_TRUNC('hour', ingested_at)
ORDER BY hour DESC;

-- Database performance - using the view
SELECT * FROM ingestion_performance
WHERE minute > NOW() - INTERVAL '1 hour'
ORDER BY minute DESC;

-- ============================================
-- Statistical Analysis
-- ============================================

-- Hourly statistics by sensor
SELECT * FROM hourly_sensor_stats
WHERE hour > NOW() - INTERVAL '24 hours'
ORDER BY hour DESC, sensor_id;

-- Temperature trends by location
SELECT 
    location,
    DATE_TRUNC('hour', timestamp) as hour,
    AVG(value) as avg_temperature,
    MIN(value) as min_temperature,
    MAX(value) as max_temperature
FROM sensor_readings
WHERE sensor_type = 'temperature'
    AND timestamp > NOW() - INTERVAL '24 hours'
GROUP BY location, DATE_TRUNC('hour', timestamp)
ORDER BY hour DESC, location;

-- ============================================
-- Reliability Metrics
-- ============================================

-- Expected vs actual reading counts (30 second intervals)
SELECT 
    sensor_id,
    COUNT(*) as actual_readings,
    EXTRACT(EPOCH FROM (MAX(timestamp) - MIN(timestamp))) / 30 as expected_readings,
    ROUND(COUNT(*) * 100.0 / NULLIF(EXTRACT(EPOCH FROM (MAX(timestamp) - MIN(timestamp))) / 30, 0), 2) as reliability_pct
FROM sensor_readings
WHERE timestamp > NOW() - INTERVAL '1 hour'
GROUP BY sensor_id;

-- Missing data intervals
WITH reading_intervals AS (
    SELECT 
        sensor_id,
        timestamp,
        LAG(timestamp) OVER (PARTITION BY sensor_id ORDER BY timestamp) as prev_timestamp
    FROM sensor_readings
    WHERE timestamp > NOW() - INTERVAL '24 hours'
)
SELECT 
    sensor_id,
    prev_timestamp,
    timestamp,
    EXTRACT(EPOCH FROM (timestamp - prev_timestamp)) as gap_seconds
FROM reading_intervals
WHERE EXTRACT(EPOCH FROM (timestamp - prev_timestamp)) > 60
ORDER BY gap_seconds DESC;

-- ============================================
-- Anomaly Detection (Simple)
-- ============================================

-- Readings outside 3 standard deviations
WITH stats AS (
    SELECT 
        sensor_id,
        sensor_type,
        AVG(value) as mean_value,
        STDDEV(value) as stddev_value
    FROM sensor_readings
    WHERE timestamp > NOW() - INTERVAL '7 days'
    GROUP BY sensor_id, sensor_type
)
SELECT 
    r.sensor_id,
    r.timestamp,
    r.value,
    s.mean_value,
    s.stddev_value,
    (r.value - s.mean_value) / NULLIF(s.stddev_value, 0) as z_score
FROM sensor_readings r
JOIN stats s ON r.sensor_id = s.sensor_id
WHERE ABS((r.value - s.mean_value) / NULLIF(s.stddev_value, 0)) > 3
    AND r.timestamp > NOW() - INTERVAL '24 hours'
ORDER BY r.timestamp DESC;

-- Sudden value changes
WITH value_changes AS (
    SELECT 
        sensor_id,
        timestamp,
        value,
        LAG(value) OVER (PARTITION BY sensor_id ORDER BY timestamp) as prev_value
    FROM sensor_readings
    WHERE timestamp > NOW() - INTERVAL '24 hours'
)
SELECT 
    sensor_id,
    timestamp,
    prev_value,
    value,
    ABS(value - prev_value) as change_magnitude
FROM value_changes
WHERE ABS(value - prev_value) > 10
ORDER BY change_magnitude DESC;

-- ============================================
-- Image Metadata (if images are stored)
-- ============================================

-- View recent images
SELECT 
    sensor_id,
    blob_name,
    timestamp,
    file_size,
    content_type
FROM image_metadata
ORDER BY timestamp DESC
LIMIT 20;

-- Image storage statistics
SELECT 
    sensor_id,
    COUNT(*) as image_count,
    SUM(file_size) as total_bytes,
    ROUND(SUM(file_size) / 1024.0 / 1024.0, 2) as total_mb
FROM image_metadata
GROUP BY sensor_id;

-- ============================================
-- Data Cleanup (Use with caution!)
-- ============================================

-- Delete old readings (older than 90 days)
-- DELETE FROM sensor_readings WHERE timestamp < NOW() - INTERVAL '90 days';

-- Vacuum to reclaim space
-- VACUUM ANALYZE sensor_readings;
