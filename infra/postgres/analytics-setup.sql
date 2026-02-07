-- PostgreSQL Analytics Optimization & Setup
-- Run this in pgAdmin (localhost:5050) or psql to prepare the database for Metabase analytics

-- ============================================================================
-- 1. CREATE INDEXES FOR ANALYTICS PERFORMANCE
-- ============================================================================
-- These indexes speed up common Metabase queries significantly

CREATE INDEX IF NOT EXISTS idx_sensor_events_received_at 
  ON sensor_events(received_at DESC);

CREATE INDEX IF NOT EXISTS idx_sensor_events_status 
  ON sensor_events(status_level, received_at DESC);

CREATE INDEX IF NOT EXISTS idx_sensor_events_source_time 
  ON sensor_events(source_type, received_at DESC);

CREATE INDEX IF NOT EXISTS idx_sensor_events_observed_at 
  ON sensor_events(observed_at DESC);

-- For JSONB queries (measurements analysis)
CREATE INDEX IF NOT EXISTS idx_sensor_events_measurements_gin 
  ON sensor_events USING GIN(measurements);

-- ============================================================================
-- 2. CREATE MATERIALIZED VIEWS FOR COMMON QUERIES
-- ============================================================================
-- These pre-compute expensive aggregations for faster dashboards

-- Temperature Summary (refreshed weekly)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_temperature_hourly AS
WITH temperature_measurements AS (
  SELECT
    se.sensor_id,
    se.source_type,
    DATE_TRUNC('hour', se.observed_at) as measurement_hour,
    (m->>'value')::numeric as temp_value
  FROM sensor_events se,
  LATERAL jsonb_array_elements(se.measurements) as m
  WHERE m->>'name' LIKE '%temperature%'
    AND se.status_level = 'INFO'
    AND (m->>'value')::numeric BETWEEN -50 AND 60
    AND se.observed_at > NOW() - INTERVAL '90 days'
)
SELECT
  source_type,
  measurement_hour,
  COUNT(DISTINCT sensor_id) as active_sensors,
  ROUND(AVG(temp_value)::numeric, 2) as avg_temperature_c,
  ROUND(MIN(temp_value)::numeric, 2) as min_temp,
  ROUND(MAX(temp_value)::numeric, 2) as max_temp,
  COUNT(*) as measurement_count
FROM temperature_measurements
GROUP BY source_type, measurement_hour;

CREATE INDEX IF NOT EXISTS idx_mv_temperature_hourly_hour 
  ON mv_temperature_hourly(measurement_hour DESC);

-- Source Comparison (daily stats)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_source_daily_stats AS
SELECT
  source_type,
  DATE_TRUNC('day', received_at) as event_date,
  COUNT(DISTINCT sensor_id) as unique_sensors,
  COUNT(*) as total_events,
  COUNT(*) FILTER (WHERE status_level = 'INFO') as successful_events,
  COUNT(*) FILTER (WHERE status_level IN ('WARN', 'ERROR')) as failed_events,
  ROUND(100.0 * COUNT(*) FILTER (WHERE status_level = 'INFO') / 
        NULLIF(COUNT(*), 0), 2) as success_rate_percent
FROM sensor_events
WHERE received_at > NOW() - INTERVAL '90 days'
GROUP BY source_type, DATE_TRUNC('day', received_at);

CREATE INDEX IF NOT EXISTS idx_mv_source_daily_date 
  ON mv_source_daily_stats(event_date DESC);

-- Data Quality Summary
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_data_quality_summary AS
SELECT
  source_type,
  DATE_TRUNC('day', observed_at) as quality_date,
  status_level,
  COUNT(*) as event_count,
  ROUND(100.0 * COUNT(*) / 
        (SELECT COUNT(*) FROM sensor_events 
         WHERE DATE_TRUNC('day', observed_at) = DATE_TRUNC('day', se.observed_at)
           AND source_type = se.source_type), 2) as percent_of_source
FROM sensor_events se
WHERE observed_at > NOW() - INTERVAL '90 days'
GROUP BY source_type, DATE_TRUNC('day', observed_at), status_level;

-- Sensor Activity Summary
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_sensor_activity AS
SELECT
  s.sensor_id,
  s.source_type,
  s.display_name,
  s.lat,
  s.lon,
  COUNT(*) as total_events,
  COUNT(*) FILTER (WHERE se.status_level = 'INFO') as successful_events,
  MAX(se.received_at) as last_event,
  NOW() - MAX(se.received_at) as time_since_last_event,
  ROUND(100.0 * COUNT(*) FILTER (WHERE se.status_level = 'INFO') / 
        NULLIF(COUNT(*), 0), 2) as success_rate_percent
FROM sensors s
LEFT JOIN sensor_events se ON s.sensor_id = se.sensor_id
  AND se.received_at > NOW() - INTERVAL '7 days'
GROUP BY s.sensor_id, s.source_type, s.display_name, s.lat, s.lon;

-- ============================================================================
-- 3. REFRESH MATERIALIZED VIEWS (Run weekly)
-- ============================================================================

-- Refresh Temperature view
REFRESH MATERIALIZED VIEW CONCURRENTLY mv_temperature_hourly;

-- Refresh Source comparison view
REFRESH MATERIALIZED VIEW CONCURRENTLY mv_source_daily_stats;

-- Refresh Data quality view
REFRESH MATERIALIZED VIEW CONCURRENTLY mv_data_quality_summary;

-- Refresh Sensor activity view
REFRESH MATERIALIZED VIEW CONCURRENTLY mv_sensor_activity;

-- ============================================================================
-- 4. VALIDATE DATA FOR METABASE
-- ============================================================================

-- Check data volume
SELECT 
  'Total Events' as metric,
  COUNT(*)::text as value
FROM sensor_events
UNION ALL
SELECT 
  'Unique Sensors',
  COUNT(DISTINCT sensor_id)::text
FROM sensor_events
UNION ALL
SELECT 
  'OpenSenseMap Events',
  COUNT(*)::text
FROM sensor_events
WHERE source_type = 'public-environment'
UNION ALL
SELECT 
  'Sensor.Community Events',
  COUNT(*)::text
FROM sensor_events
WHERE source_type = 'sensor-community'
UNION ALL
SELECT 
  'Last 24h Events',
  COUNT(*)::text
FROM sensor_events
WHERE received_at > NOW() - INTERVAL '1 day'
UNION ALL
SELECT 
  'Avg Events/Hour',
  (COUNT(*) / (
    (EXTRACT(EPOCH FROM (MAX(received_at) - MIN(received_at))) / 3600)::int + 1
  ))::text
FROM sensor_events;

-- ============================================================================
-- 5. VERIFY ANALYSIS QUERIES
-- ============================================================================

-- Test 1: Temperature analysis
SELECT 
  source_type,
  COUNT(*) as measurement_count,
  ROUND(AVG((m->>'value')::numeric), 2) as avg_temp
FROM sensor_events se,
LATERAL jsonb_array_elements(se.measurements) m
WHERE m->>'name' LIKE '%temperature%'
  AND se.status_level = 'INFO'
GROUP BY source_type
LIMIT 5;

-- Test 2: Source comparison
SELECT 
  source_type,
  COUNT(DISTINCT sensor_id) as unique_sensors,
  COUNT(*) as total_events,
  ROUND(100.0 * COUNT(*) FILTER (WHERE status_level = 'INFO') / 
        NULLIF(COUNT(*), 0), 2) as success_rate
FROM sensor_events
WHERE received_at > NOW() - INTERVAL '7 days'
GROUP BY source_type;

-- Test 3: Data quality
SELECT 
  status_level,
  COUNT(*) as count,
  ROUND(100.0 * COUNT(*) / 
        (SELECT COUNT(*) FROM sensor_events 
         WHERE received_at > NOW() - INTERVAL '7 days'), 2) as percent
FROM sensor_events
WHERE received_at > NOW() - INTERVAL '7 days'
GROUP BY status_level
ORDER BY count DESC;

-- ============================================================================
-- 6. CLEANUP (If needed, remove views)
-- ============================================================================
-- Uncomment to remove:
-- DROP MATERIALIZED VIEW IF EXISTS mv_temperature_hourly CASCADE;
-- DROP MATERIALIZED VIEW IF EXISTS mv_source_daily_stats CASCADE;
-- DROP MATERIALIZED VIEW IF EXISTS mv_data_quality_summary CASCADE;
-- DROP MATERIALIZED VIEW IF EXISTS mv_sensor_activity CASCADE;

COMMIT;
