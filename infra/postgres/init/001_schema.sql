-- Telemetry Kitchen - Phase 1 baseline schema (naive on purpose)

CREATE TABLE IF NOT EXISTS sensors (
  sensor_id        TEXT PRIMARY KEY,
  source_type      TEXT NOT NULL,
  display_name     TEXT NULL,
  lat              DOUBLE PRECISION NULL,
  lon              DOUBLE PRECISION NULL,
  created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS sensor_events (
  event_id         TEXT PRIMARY KEY,         -- idempotency key
  sensor_id        TEXT NOT NULL REFERENCES sensors(sensor_id),
  source_type      TEXT NOT NULL,
  payload_type     TEXT NOT NULL,             -- json | xml | imageRef
  payload_size_b   INTEGER NOT NULL DEFAULT 0,

  observed_at      TIMESTAMPTZ NOT NULL,      -- time at source
  received_at      TIMESTAMPTZ NOT NULL,      -- time at gateway

  status_level     TEXT NOT NULL,             -- INFO | WARN | ERROR
  status_message   TEXT NULL,                 -- <= 250 chars (validated in app)

  measurements     JSONB NOT NULL DEFAULT '[]'::jsonb, -- list of name/value/unit/etc.

  blob_uri         TEXT NULL,
  blob_sha256      TEXT NULL,
  blob_bytes       BIGINT NULL,

  inserted_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Helpful index for typical reads (latest events per sensor)
CREATE INDEX IF NOT EXISTS ix_sensor_events_sensor_observed
  ON sensor_events(sensor_id, observed_at DESC);

-- Helpful index for time slicing (baseline)
CREATE INDEX IF NOT EXISTS ix_sensor_events_observed
  ON sensor_events(observed_at DESC);
