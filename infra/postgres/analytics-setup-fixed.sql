-- PostgreSQL Analytics Setup (Fixed Version)
-- This script fixes issues from the first run and creates working views

-- ============================================================================
-- 1. TEMPERATURE HOURLY MATERIALIZED VIEW (SIMPLIFIED)
-- ============================================================================

DROP MATERIALIZED VIEW IF EXISTS mv_temperature_hourly CASCADE;

CREATE MATERIALIZED VIEW mv_temperature_hourly AS
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

-- Create unique index for concurrent refresh
CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_temperature_unique 
  ON mv_temperature_hourly(source_type, measurement_hour);

-- ============================================================================
-- 2. SOURCE DAILY STATS MATERIALIZED VIEW
-- ============================================================================

DROP MATERIALIZED VIEW IF EXISTS mv_source_daily_stats CASCADE;

CREATE MATERIALIZED VIEW mv_source_daily_stats AS
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

-- Create unique index for concurrent refresh
CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_source_daily_unique 
  ON mv_source_daily_stats(source_type, event_date);

-- ============================================================================
-- 3. SENSOR ACTIVITY MATERIALIZED VIEW
-- ============================================================================

DROP MATERIALIZED VIEW IF EXISTS mv_sensor_activity CASCADE;

CREATE MATERIALIZED VIEW mv_sensor_activity AS
SELECT
  s.sensor_id,
  s.source_type,
  s.display_name,
  s.lat,
  s.lon,
  COUNT(se.event_id) as total_events,
  COUNT(CASE WHEN se.status_level = 'INFO' THEN 1 END) as successful_events,
  MAX(se.received_at) as last_event,
  CASE 
    WHEN MAX(se.received_at) IS NULL THEN 'NEW'::text
    WHEN NOW() - MAX(se.received_at) > INTERVAL '1 hour' THEN 'INACTIVE'::text
    ELSE 'ACTIVE'::text
  END as status,
  ROUND(100.0 * COUNT(CASE WHEN se.status_level = 'INFO' THEN 1 END) / 
        NULLIF(COUNT(se.event_id), 0), 2) as success_rate_percent
FROM sensors s
LEFT JOIN sensor_events se ON s.sensor_id = se.sensor_id
  AND se.received_at > NOW() - INTERVAL '7 days'
GROUP BY s.sensor_id, s.source_type, s.display_name, s.lat, s.lon;

-- Create unique index for concurrent refresh
CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_sensor_activity_unique 
  ON mv_sensor_activity(sensor_id);

-- ============================================================================
-- 4. MEASUREMENT TYPES MATERIALIZED VIEW
-- ============================================================================

DROP MATERIALIZED VIEW IF EXISTS mv_measurement_types CASCADE;

CREATE MATERIALIZED VIEW mv_measurement_types AS
SELECT
  m->>'name' as measurement_name,
  COUNT(DISTINCT se.sensor_id) as sensor_count,
  COUNT(se.event_id) as measurement_count,
  ROUND(MIN((m->>'value')::numeric), 2) as min_value,
  ROUND(AVG((m->>'value')::numeric), 2) as avg_value,
  ROUND(MAX((m->>'value')::numeric), 2) as max_value,
  m->>'unit' as unit
FROM sensor_events se,
LATERAL jsonb_array_elements(se.measurements) as m
WHERE se.observed_at > NOW() - INTERVAL '7 days'
GROUP BY m->>'name', m->>'unit';

-- Create unique index for concurrent refresh
CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_measurement_types_unique 
  ON mv_measurement_types(measurement_name);

-- ============================================================================
-- 5. REFRESH ALL VIEWS
-- ============================================================================

REFRESH MATERIALIZED VIEW CONCURRENTLY mv_temperature_hourly;
REFRESH MATERIALIZED VIEW CONCURRENTLY mv_source_daily_stats;
REFRESH MATERIALIZED VIEW CONCURRENTLY mv_sensor_activity;
REFRESH MATERIALIZED VIEW CONCURRENTLY mv_measurement_types;

-- ============================================================================
-- 6. VERIFY DATA AVAILABILITY
-- ============================================================================

-- Sample data from views
SELECT 'Temperature View' as view_name, COUNT(*) as rows FROM mv_temperature_hourly
UNION ALL
SELECT 'Source Daily Stats', COUNT(*) FROM mv_source_daily_stats
UNION ALL
SELECT 'Sensor Activity', COUNT(*) FROM mv_sensor_activity
UNION ALL
SELECT 'Measurement Types', COUNT(*) FROM mv_measurement_types;

COMMIT;
