-- Seed data for development/demo (100 sensors, 100000 events)

BEGIN;

INSERT INTO sensors (sensor_id, source_type, display_name, lat, lon)
SELECT
	format('seed-%s', lpad(i::text, 4, '0')),
	'seed',
	format('Seed Sensor %s', i),
	40 + (i % 50) / 10.0,
	-74 + (i % 50) / 10.0
FROM generate_series(1, 100) AS s(i)
ON CONFLICT (sensor_id) DO NOTHING;

INSERT INTO sensor_events (
	event_id,
	sensor_id,
	source_type,
	payload_type,
	payload_size_b,
	observed_at,
	received_at,
	status_level,
	status_message,
	measurements
)
SELECT
	md5(format('seed-%s-%s', s.sensor_id, e.i)) AS event_id,
	s.sensor_id,
	'seed' AS source_type,
	'json' AS payload_type,
	128 AS payload_size_b,
	now() - (e.i || ' seconds')::interval AS observed_at,
	now() - ((e.i - 1) || ' seconds')::interval AS received_at,
	CASE
		WHEN (e.i % 50) = 0 THEN 'ERROR'
		WHEN (e.i % 10) = 0 THEN 'WARN'
		ELSE 'INFO'
	END AS status_level,
	CASE
		WHEN (e.i % 50) = 0 THEN 'seed error'
		WHEN (e.i % 10) = 0 THEN 'seed warn'
		ELSE 'ok'
	END AS status_message,
	jsonb_build_array(
		jsonb_build_object(
			'name', 'temperature',
			'value', (20 + (e.i % 15))::text,
			'unit', 'C'
		)
	) AS measurements
FROM generate_series(1, 100000) AS e(i)
CROSS JOIN LATERAL (
	SELECT format('seed-%s', lpad(((e.i - 1) % 100 + 1)::text, 4, '0')) AS sensor_id
) AS s;

COMMIT;
