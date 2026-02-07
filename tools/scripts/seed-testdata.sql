-- Seed test data for telemetry-kitchen lab

-- Insert test sensors
INSERT INTO sensors (sensor_id, source_type, display_name, lat, lon, created_at, updated_at)
VALUES 
  ('lab-temp-01', 'synthetic-lab', 'Lab Temperature Sensor A', 52.5200, 13.4050, now(), now()),
  ('lab-temp-02', 'synthetic-lab', 'Lab Temperature Sensor B', 52.5210, 13.4060, now(), now()),
  ('lab-humidity-01', 'synthetic-lab', 'Lab Humidity Sensor', 52.5190, 13.4040, now(), now()),
  ('lab-pressure-01', 'synthetic-lab', 'Lab Pressure Sensor', 52.5200, 13.4050, now(), now())
ON CONFLICT (sensor_id) DO NOTHING;

-- Insert test events for each sensor (simple deterministic IDs)
DO $$
DECLARE
  v_time TIMESTAMPTZ;
  v_eventid TEXT;
  i INT;
  v_temp_val NUMERIC;
  v_humid_val NUMERIC;
  v_press_val NUMERIC;
BEGIN
  -- Generate 6 events per sensor (10-minute intervals for 1 hour)
  FOR i IN 0..5 LOOP
    v_time := now() - INTERVAL '1 hour' + (i * INTERVAL '10 minutes');
    v_temp_val := 20.0 + (i * 0.8);
    v_humid_val := 40.0 + (i * 3.3);
    v_press_val := 1010.0 + (i * 1.0);
    
    -- Lab Temp 01 
    v_eventid := 'lab-temp-01-' || to_char(v_time, 'YYYYMMDDHH24MISS') || '-' || i;
    INSERT INTO sensor_events (event_id, sensor_id, source_type, payload_type, payload_size_b, observed_at, received_at, status_level, status_message, measurements)
    VALUES (v_eventid, 'lab-temp-01', 'synthetic-lab', 'json', 45, v_time, now(), 'INFO', 'Test data', 
            jsonb_build_array(jsonb_build_object('name','temperature','value',v_temp_val::text,'unit','C')))
    ON CONFLICT (event_id) DO NOTHING;
    
    -- Lab Temp 02
    v_eventid := 'lab-temp-02-' || to_char(v_time, 'YYYYMMDDHH24MISS') || '-' || i;
    INSERT INTO sensor_events (event_id, sensor_id, source_type, payload_type, payload_size_b, observed_at, received_at, status_level, status_message, measurements)
    VALUES (v_eventid, 'lab-temp-02', 'synthetic-lab', 'json', 45, v_time, now(), 'INFO', 'Test data',
            jsonb_build_array(jsonb_build_object('name','temperature','value',(21.0 + (i * 0.8))::text,'unit','C')))
    ON CONFLICT (event_id) DO NOTHING;
    
    -- Lab Humidity
    v_eventid := 'lab-humidity-01-' || to_char(v_time, 'YYYYMMDDHH24MISS') || '-' || i;
    INSERT INTO sensor_events (event_id, sensor_id, source_type, payload_type, payload_size_b, observed_at, received_at, status_level, status_message, measurements)
    VALUES (v_eventid, 'lab-humidity-01', 'synthetic-lab', 'json', 48, v_time, now(), 'INFO', 'Test data',
            jsonb_build_array(jsonb_build_object('name','humidity','value',v_humid_val::text,'unit','percent')))
    ON CONFLICT (event_id) DO NOTHING;
    
    -- Lab Pressure
    v_eventid := 'lab-pressure-01-' || to_char(v_time, 'YYYYMMDDHH24MISS') || '-' || i;
    INSERT INTO sensor_events (event_id, sensor_id, source_type, payload_type, payload_size_b, observed_at, received_at, status_level, status_message, measurements)
    VALUES (v_eventid, 'lab-pressure-01', 'synthetic-lab', 'json', 50, v_time, now(), 
            CASE WHEN i = 5 THEN 'WARN' ELSE 'INFO' END, 'Test data',
            jsonb_build_array(jsonb_build_object('name','pressure','value',v_press_val::text,'unit','hPa')))
    ON CONFLICT (event_id) DO NOTHING;
  END LOOP;
END $$;

-- Verify data
SELECT 'Sensors:' as result, count(*) as count FROM sensors
UNION ALL
SELECT 'Events:' as result, count(*) as count FROM sensor_events;
