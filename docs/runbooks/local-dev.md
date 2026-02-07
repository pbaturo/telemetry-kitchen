# Local Development Runbook

**Purpose:** Technical setup guide for developers working on Telemetry Kitchen  
**Target Audience:** Software Engineers, Contributors  
**Related Documentation:**  
- 📘 **[Operators Manual](../OPERATORS-MANUAL.md)** — Day-to-day operations (Grafana, logs, troubleshooting)  
- 📙 **[Architecture Overview](../architecture/overview.md)** — System design and rationale

---

## Prerequisites

- .NET 9.0 SDK
- Docker Desktop (with Docker Compose)
- PostgreSQL client (optional, for manual queries)
- PowerShell (Windows) or Bash (Linux/Mac)

## Quick Start

### 1. Start Infrastructure (Docker Compose)

```powershell
cd infra/compose
docker-compose up -d
```

This starts:
- PostgreSQL (port 5432)
- pgAdmin (port 5050 - http://localhost:5050)
- Prometheus (port 9091 - http://localhost:9091)
- Grafana (port 3000 - http://localhost:3000)

**Credentials:**

pgAdmin:
- Email: `admin@example.com`
- Password: `admin`

Grafana:
- Username: `admin`
- Password: `admin`

### 2. Run Gateway.Poller

From the repository root:

```powershell
dotnet run --project src/Gateway.Poller
```

The Gateway.Poller will:
- Poll 10 OpenSenseMap environmental stations every 60 seconds
- Parse temperature, humidity, PM10, PM2.5 and other measurements
- Write events to PostgreSQL (`sensor_events` table)
- Expose Prometheus metrics at http://localhost:9090/metrics
- Run as an ASP.NET Core web application (no admin rights needed)

**Architecture:**
```
Gateway.Poller (port 9090/metrics) 
    ↓ scrapes every 10s
Prometheus (port 9091) 
    ↓ queries
Grafana (port 3000) - visualizes metrics
```

Gateway.Poller also writes sensor events directly to PostgreSQL.

### 3. Verify Data Ingestion

#### Option A: Using pgAdmin (Web UI)

1. Open http://localhost:5050
2. Login with credentials above
3. Add a new server:
   - Name: `Telemetry Kitchen`
   - Host: `tk-postgres` (or `host.docker.internal` on Windows/Mac)
   - Port: `5432`
   - Username: `tk`
   - Password: `tk`
   - Database: `telemetry_kitchen`
4. Run queries:

```sql
-- Count total events
SELECT count(*) FROM sensor_events;

-- View latest events per sensor
SELECT 
  sensor_id, 
  observed_at, 
  status_level, 
  measurements 
FROM sensor_events 
ORDER BY observed_at DESC 
LIMIT 20;

-- Check sensor station registry
SELECT * FROM sensors;

-- View measurements by sensor
SELECT 
  sensor_id,
  jsonb_array_length(measurements) as measurement_count,
  measurements
FROM sensor_events
WHERE sensor_id = 'env-01'
ORDER BY observed_at DESC
LIMIT 5;
```

#### Option B: Using psql (Command Line)

```powershell
psql -h localhost -p 5432 -U tk -d telemetry_kitchen
# Password: tk
```

Then run the SQL queries above.

## Scenario 1: Environmental Stations Baseline

**Goal:** Poll ~10 external public environmental sensor stations to establish baseline PostgreSQL write and query performance.

**Configuration:** `src/Gateway.Poller/appsettings.json`
- 10 OpenSenseMap stations (real API endpoints)
- Poll interval: 60 seconds (configurable per station)
**View metrics:**
- Raw metrics endpoint: http://localhost:9090/metrics (Gateway.Poller)
- Prometheus UI: http://localhost:9091 (query and graph metrics)
- Grafana: http://localhost:3000 (dashboards - login: admin/admin)

**Example Prometheus queries:**
```promql
# Poll rate per second
rate(tk_polls_total[1m])

# Success rate
rate(tk_events_inserted_total[5m]) / rate(tk_polls_total[5m])

# P95 poll duration
histogram_quantile(0.95, rate(tk_poll_duration_ms_bucket[5m]))

# Events inserted per sensor
sum by (sensorId) (increase(tk_events_inserted_total[1h]))
```asurements array

**Key Metrics (Prometheus):**
- `tk_polls_total` - Total polls attempted
- `tk_polls_failed_total` - Failed polls (HTTP errors)
- `tk_events_inserted_total` - Successfully inserted events
- `tk_duplicates_total` - Duplicate event detections (idempotency working)
- `tk_poll_duration_ms` - HTTP request duration histogram
- `tk_db_insert_duration_ms` - Database insert duration histogram
- `tk_last_success_unixtime{sensorId="..."}` - Last successful poll per station

View metrics: http://localhost:9090/metrics

**Expected Behavior:**
- Each station polls independently on its configured interval
- Exponential backoff on failures (5s → 10s → 20s → ... → 300s max)
- Deterministic event IDs ensure idempotency (duplicate polls don't create duplicate rows)
- Per-station cancellation tokens for graceful shutdown

## Stopping Services

```powershell
# Stop Gateway.Poller
Ctrl+C

# Stop Docker infrastructure
cd infra/compose
docker-compose down

# Stop and remove volumes (clears database)
docker-compose down -v
```

## Troubleshooting

### Gateway can't connect to Postgres

**Symptom:** `Npgsql.NpgsqlException: Connection refused`

**Solution:** 
1. Verify Postgres is running: `docker ps | grep tk-postgres`
2. Check connection string in `appsettings.json` - should be `Host=localhost;Port=5432`
3. For Linux/WSL, you may need to use `host.docker.internal` instead of `localhost`

### No measurements in database

**Symptom:** Events inserted but `measurements` field is empty or contains `"raw"`

**Possible causes:**
1. OpenSenseMap station has no recent data (check `lastMeasurement` is not null in API response)
2. Station URL invalid or changed
3. API response format changed

**Debug:**
- Check logs for `statusLevel=WARN` and `statusMessage` field
- Verify raw API response: `curl https://api.opensensemap.org/boxes/5c6ec1ae15451500198f5abe`
- If measurements are null, that's expected - not all stations report continuously

### High number of duplicates

**Symptom:** `tk_duplicates_total` increasing rapidly

**Expected behavior:** Duplicates are normal if:
- Poller restarts and re-polls same time window
- External API returns same measurement timestamp on multiple polls
- This proves idempotency is working correctly

### Slow database inserts

**Symptom:** `tk_db_insert_duration_ms` > 100ms consistently

**Possible causes:**
1. Docker resource constraints (increase CPU/RAM in Docker Desktop settings)
2. Disk I/O bottleneck (check Docker volume performance)
3. Missing indexes (verify `001_schema.sql` was executed)

**Verify indexes:**
```sql
SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'sensor_events';
```

---

## Operational Tasks

For day-to-day operations, monitoring, and troubleshooting, see the **[Operators Manual](../OPERATORS-MANUAL.md)**, which covers:

- ✅ **Using Grafana** — Dashboards, metrics, exploration
- ✅ **Using PGAdmin** — Database queries, performance monitoring
- ✅ **Viewing Logs** — Loki queries, Docker logs, log analysis
- ✅ **Monitoring HTTP Sensors** — External API health, polling status
- ✅ **Operating RabbitMQ** — Queue management, message inspection
- ✅ **Troubleshooting** — Common scenarios, performance baselines

---

## Next Steps

**Phase 1 Complete!** ✅

Current system includes:
- ✅ Gateway.Poller polling real sensors
- ✅ RabbitMQ durability gate
- ✅ Ingest.Consumer with idempotency
- ✅ PostgreSQL with 100k seed data
- ✅ Grafana dashboards (Operational + Sensor Overview)
- ✅ Loki centralized logging
- ✅ Full observability stack (Prometheus + exporters)

**Phase 2 — Coming Soon:**
1. Web.Mvc read-only UI (sensor status, history views)
2. Metabase analytics integration
3. Additional external APIs (USGS, Sensor.Community)
4. Azurite object storage for media files

**Performance Evolution:**
1. Measure baseline performance with current naive schema
2. Identify bottlenecks using Grafana dashboards
3. Optimize (indexing, partitioning, caching)
4. Re-measure and compare results

**Philosophy:** Start naive → measure → improve → compare ✨
