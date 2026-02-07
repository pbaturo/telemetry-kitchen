-- Initialize Telemetry Kitchen Database Schema
-- This is a naive schema that will be evolved later

-- Sensor readings table
CREATE TABLE IF NOT EXISTS sensor_readings (
    id BIGSERIAL PRIMARY KEY,
    sensor_id VARCHAR(100) NOT NULL,
    sensor_type VARCHAR(50) NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    value NUMERIC(10, 2) NOT NULL,
    unit VARCHAR(20),
    location VARCHAR(200),
    metadata JSONB,
    ingested_at TIMESTAMPTZ DEFAULT NOW(),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Index for time-based queries
CREATE INDEX idx_sensor_readings_timestamp ON sensor_readings(timestamp DESC);
CREATE INDEX idx_sensor_readings_sensor_id ON sensor_readings(sensor_id);
CREATE INDEX idx_sensor_readings_sensor_type ON sensor_readings(sensor_type);
CREATE INDEX idx_sensor_readings_ingested_at ON sensor_readings(ingested_at DESC);

-- Image metadata table
CREATE TABLE IF NOT EXISTS image_metadata (
    id BIGSERIAL PRIMARY KEY,
    sensor_id VARCHAR(100) NOT NULL,
    blob_name VARCHAR(500) NOT NULL,
    blob_url TEXT NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    file_size BIGINT,
    content_type VARCHAR(100),
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_image_metadata_sensor_id ON image_metadata(sensor_id);
CREATE INDEX idx_image_metadata_timestamp ON image_metadata(timestamp DESC);

-- Ingestion metrics table for tracking reliability
CREATE TABLE IF NOT EXISTS ingestion_metrics (
    id BIGSERIAL PRIMARY KEY,
    metric_name VARCHAR(100) NOT NULL,
    metric_value NUMERIC(15, 4) NOT NULL,
    sensor_id VARCHAR(100),
    timestamp TIMESTAMPTZ NOT NULL,
    tags JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_ingestion_metrics_timestamp ON ingestion_metrics(timestamp DESC);
CREATE INDEX idx_ingestion_metrics_metric_name ON ingestion_metrics(metric_name);

-- Anomaly detection table
CREATE TABLE IF NOT EXISTS anomalies (
    id BIGSERIAL PRIMARY KEY,
    sensor_id VARCHAR(100) NOT NULL,
    sensor_type VARCHAR(50) NOT NULL,
    anomaly_type VARCHAR(50) NOT NULL,
    severity VARCHAR(20) NOT NULL,
    detected_value NUMERIC(10, 2),
    expected_range_min NUMERIC(10, 2),
    expected_range_max NUMERIC(10, 2),
    timestamp TIMESTAMPTZ NOT NULL,
    description TEXT,
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_anomalies_timestamp ON anomalies(timestamp DESC);
CREATE INDEX idx_anomalies_sensor_id ON anomalies(sensor_id);
CREATE INDEX idx_anomalies_severity ON anomalies(severity);

-- View for performance analysis
CREATE OR REPLACE VIEW ingestion_performance AS
SELECT 
    DATE_TRUNC('minute', ingested_at) as minute,
    sensor_type,
    COUNT(*) as reading_count,
    AVG(EXTRACT(EPOCH FROM (ingested_at - timestamp))) as avg_latency_seconds
FROM sensor_readings
GROUP BY DATE_TRUNC('minute', ingested_at), sensor_type;

-- View for hourly statistics
CREATE OR REPLACE VIEW hourly_sensor_stats AS
SELECT 
    DATE_TRUNC('hour', timestamp) as hour,
    sensor_id,
    sensor_type,
    COUNT(*) as reading_count,
    AVG(value) as avg_value,
    MIN(value) as min_value,
    MAX(value) as max_value,
    STDDEV(value) as stddev_value
FROM sensor_readings
GROUP BY DATE_TRUNC('hour', timestamp), sensor_id, sensor_type;

-- Grant privileges
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO telemetry;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO telemetry;
