-- Add raw payload JSON storage
ALTER TABLE IF EXISTS sensor_events
  ADD COLUMN IF NOT EXISTS payload_json JSONB NULL;
