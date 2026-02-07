# Telemetry Kitchen - Operators Manual

**Version:** 1.0  
**Target Audience:** System operators, DevOps engineers, developers  
**Purpose:** Day-to-day operations guide for monitoring, troubleshooting, and maintaining the Telemetry Kitchen platform

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Daily Operations Checklist](#daily-operations-checklist)
3. [Grafana - Observability Platform](#grafana---observability-platform)
4. [PGAdmin - Database Management](#pgadmin---database-management)
5. [Logs - Viewing and Analysis](#logs---viewing-and-analysis)
6. [HTTP Sensors - External API Monitoring](#http-sensors---external-api-monitoring)
7. [RabbitMQ - Message Broker Operations](#rabbitmq---message-broker-operations)
8. [Common Troubleshooting Scenarios](#common-troubleshooting-scenarios)
9. [Performance Baselines](#performance-baselines)
10. [Emergency Contacts & Escalation](#emergency-contacts--escalation)

---

## System Overview

Telemetry Kitchen is an IoT data ingestion and observability platform running as a Docker Compose stack.

**Architecture Layers:**
- **🌍 Data Sources:** External public sensor APIs (OpenSenseMap, Sensor.Community, USGS)
- **⚙️ Ingestion:** Gateway.Poller → RabbitMQ → Ingest.Consumer → PostgreSQL
- **📊 Observability:** Prometheus, Loki, Grafana + exporters
- **👥 Client Area:** Web.Mvc (Phase 2), Metabase (Phase 2)

**Running Services:**
```
tk-postgres           :5432   PostgreSQL database
tk-postgres-exporter  :9187   PostgreSQL metrics
tk-rabbitmq           :5672   AMQP broker
tk-rabbitmq           :15672  Management UI
tk-rabbitmq           :15692  Prometheus metrics
tk-prometheus         :9091   Metrics storage
tk-loki               :3100   Log aggregation
tk-grafana            :3000   Visualization
tk-pgadmin            :5050   DB admin UI
tk-node-exporter      :9100   System metrics
tk-gateway-poller     :9090   Metrics endpoint
tk-ingest-consumer    (internal)
```

---

## Daily Operations Checklist

### Morning Health Check (5 minutes)

**1. Verify All Services Running**
```powershell
docker ps --format "table {{.Names}}\t{{.Status}}"
```
✅ All containers should show "Up" status  
⚠️ If any show "Restarting", investigate logs immediately

**2. Check Grafana Dashboards**
- Navigate to http://localhost:3000
- Login: `admin` / `admin`
- View **"Operational Monitoring - Ingest Reliability"** dashboard
- Verify:
  - ✅ DB Transactions/sec: > 0 (green)
  - ✅ Queue Depth: < 1000 (green/yellow)
  - ✅ .NET Error Rate: 0 (green)
  - ✅ CPU Usage: < 70% (yellow)
  - ✅ Memory Usage: < 75% (yellow)

**3. Check for Alerts**
- Grafana alerts (if configured)
- System event log

**4. Quick Database Check**
```sql
-- Run in PGAdmin (http://localhost:5050)
SELECT 
  COUNT(*) as total_events,
  MAX(observed_at) as latest_event,
  COUNT(DISTINCT sensor_id) as active_sensors
FROM sensor_events
WHERE observed_at > NOW() - INTERVAL '1 hour';
```
Expected: Events from last hour, 10+ active sensors

---

## Grafana - Observability Platform

### Access
- **URL:** http://localhost:3000
- **Username:** `admin`
- **Password:** `admin`
- **First Login:** You'll be prompted to change password (optional for local dev)

### Available Dashboards

#### 1. **Operational Monitoring - Ingest Reliability**
**Purpose:** Real-time health of ingestion pipeline

**Key Panels:**

**🚦 Health Status Row**
- **DB Transactions/sec:** Should be steady, spikes indicate batch processing
  - Green: < 500 tps  
  - Yellow: 500-1000 tps  
  - Red: > 1000 tps (approaching limits)
  
- **Queue Depth:** Number of messages waiting in RabbitMQ
  - Green: < 1000 messages
  - Yellow: 1000-5000 messages (consumers falling behind)
  - Red: > 5000 messages (critical backlog)
  
- **.NET Error Rate:** Application exceptions
  - Green: 0 errors/sec
  - Yellow: 1-10 errors/sec (investigate)
  - Red: > 10 errors/sec (critical failure)

**📈 Throughput & Latency Row**
- **PostgreSQL Transaction Rate:** Commits vs Rollbacks (should be 99%+ commits)
- **RabbitMQ Message Throughput:** Publish rate should match consume rate
- **PostgreSQL Query Latency:** p95 should be < 100ms, p99 < 500ms

**🔧 Resource Utilization Row**
- **PostgreSQL Connections:** Monitor "Active" connections (spikes = query bottleneck)
- **CPU Usage Breakdown:** Watch "IO Wait" - high values = disk bottleneck
- **Disk I/O Throughput:** Spikes correlate with ingestion rate

**⚠️ Errors & Reliability Row**
- **Cache Hit Ratio:** Should stay > 95% (lower = more disk reads)
- **PostgreSQL Locks:** Spikes indicate contention
- **.NET Exceptions:** Should be zero or near-zero
- **RabbitMQ Failed Messages:** Dead letters + redeliveries

**Time Range Controls:**
- Default: Last 15 minutes (live view)
- Adjust: Top-right corner, click clock icon
- Refresh: Auto-refresh dropdown (5s, 10s, 30s, 1m intervals)

#### 2. **Sensor Overview Dashboard**
**Purpose:** Business-level sensor data visualization

**Panels:**
- **Temperature Trends:** Time-series of lab sensors
- **Event Status Distribution:** Pie chart of OK/WARN/ERROR events
- **Recent Events:** Table of last 50 events with details
- **Service Logs:** Loki logs from Gateway.Poller and Ingest.Consumer

### Exploring Logs in Grafana

**Method 1: Via Dashboard**
1. Open "Sensor Overview" dashboard
2. Scroll to "Service Logs" panel at bottom
3. Click any log line to expand JSON details

**Method 2: Explore Tab (Advanced)**
1. Click "Explore" icon (compass) in left sidebar
2. Select "Loki" datasource from dropdown
3. Use LogQL queries:

```logql
{application="gateway-poller"}
```
All Gateway.Poller logs

```logql
{application="ingest-consumer"} |= "error"
```
Consumer errors only

```logql
{application=~"gateway-poller|ingest-consumer"} | json | level="Error"
```
All errors from both services

**Useful Filters:**
- `| json` - Parse JSON logs
- `|= "text"` - Contains text
- `|~ "regex"` - Regex match
- `level="Error"` - Filter by log level
- `rate([5m])` - Events per second over 5min

### Creating Custom Dashboards

1. Click **"+"** → **"Dashboard"**
2. Click **"Add visualization"**
3. Select datasource (Prometheus or Loki)
4. Build query using visual editor or PromQL
5. Configure visualization type (Time series, Gauge, Table, etc.)
6. Save dashboard

**Example PromQL Queries:**
```promql
# Total events inserted in last 5 minutes
sum(increase(pg_stat_database_tup_inserted{datname="telemetry_kitchen"}[5m]))

# RabbitMQ queue size
rabbitmq_queue_messages{queue="ingest-events"}

# CPU usage percent
avg(rate(node_cpu_seconds_total{mode!="idle"}[1m])) * 100

# Memory available
node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes * 100
```

---

## PGAdmin - Database Management

### Access
- **URL:** http://localhost:5050
- **Email:** `admin@example.com`
- **Password:** `admin`

### First-Time Setup

**Add Server Connection:**
1. Right-click "Servers" → "Register" → "Server"
2. Fill in **General tab:**
   - Name: `Telemetry Kitchen`
3. Fill in **Connection tab:**
   - Host: `tk-postgres` (Docker internal name)
   - Port: `5432`
   - Maintenance database: `telemetry_kitchen`
   - Username: `tk`
   - Password: `tk`
4. Click **"Save"**

### Common Database Operations

#### View Recent Sensor Events
```sql
SELECT 
  event_id,
  sensor_id,
  observed_at,
  status_level,
  status_message,
  measurements
FROM sensor_events
ORDER BY observed_at DESC
LIMIT 50;
```

#### Count Events by Sensor
```sql
SELECT 
  sensor_id,
  COUNT(*) as event_count,
  MIN(observed_at) as first_event,
  MAX(observed_at) as last_event
FROM sensor_events
GROUP BY sensor_id
ORDER BY event_count DESC;
```

#### Check for Gaps in Data
```sql
WITH time_gaps AS (
  SELECT 
    sensor_id,
    observed_at,
    LEAD(observed_at) OVER (PARTITION BY sensor_id ORDER BY observed_at) as next_observed,
    LEAD(observed_at) OVER (PARTITION BY sensor_id ORDER BY observed_at) - observed_at AS gap
  FROM sensor_events
)
SELECT 
  sensor_id,
  observed_at,
  next_observed,
  gap
FROM time_gaps
WHERE gap > INTERVAL '10 minutes'
ORDER BY gap DESC
LIMIT 20;
```

#### View Sensor Measurements (JSON Extract)
```sql
SELECT 
  sensor_id,
  observed_at,
  measurements->0->>'phenomenon' as measurement_type,
  measurements->0->>'value' as value,
  measurements->0->>'unit' as unit
FROM sensor_events
WHERE sensor_id = 'YOUR_SENSOR_ID'
ORDER BY observed_at DESC
LIMIT 20;
```

#### Database Size and Table Stats
```sql
-- Database size
SELECT pg_size_pretty(pg_database_size('telemetry_kitchen'));

-- Table sizes
SELECT 
  schemaname,
  tablename,
  pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;

-- Row counts
SELECT 
  schemaname,
  relname as table_name,
  n_live_tup as row_count
FROM pg_stat_user_tables
ORDER BY n_live_tup DESC;
```

#### Performance Monitoring
```sql
-- Active queries
SELECT 
  pid,
  state,
  query_start,
  NOW() - query_start as duration,
  query
FROM pg_stat_activity
WHERE state != 'idle'
ORDER BY query_start;

-- Long-running queries (> 30 seconds)
SELECT 
  pid,
  NOW() - query_start as duration,
  query
FROM pg_stat_activity
WHERE (NOW() - query_start) > INTERVAL '30 seconds'
  AND state != 'idle';

-- Kill a long-running query (if needed)
-- SELECT pg_terminate_backend(PID);
```

#### Index Usage
```sql
SELECT 
  schemaname,
  tablename,
  indexname,
  idx_scan as index_scans,
  idx_tup_read as tuples_read,
  idx_tup_fetch as tuples_fetched
FROM pg_stat_user_indexes
ORDER BY idx_scan DESC;
```

### Backup and Restore

**Create Backup (via Docker):**
```powershell
docker exec tk-postgres pg_dump -U tk telemetry_kitchen > backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').sql
```

**Restore Backup:**
```powershell
docker exec -i tk-postgres psql -U tk -d telemetry_kitchen < backup_20260207_143000.sql
```

---

## Logs - Viewing and Analysis

### Log Locations and Methods

#### 1. **Grafana Loki** (Recommended - Centralized)

**Access via Grafana:**
- URL: http://localhost:3000
- Navigate: Explore → Select "Loki" datasource

**Query Examples:**
```logql
# All application logs
{application=~"gateway-poller|ingest-consumer"}

# Only errors
{application=~"gateway-poller|ingest-consumer"} | json | level="Error"

# Specific sensor polling
{application="gateway-poller"} |= "sensor_id"

# Database operations
{application="ingest-consumer"} |= "INSERT"

# Rate of errors (errors per second)
rate({application=~".*"} | json | level="Error" [1m])
```

**Log Levels:**
- `Trace` - Very detailed, usually disabled
- `Debug` - Development diagnostics
- `Information` - Normal operations
- `Warning` - Potential issues, not critical
- `Error` - Failures, exceptions
- `Critical` - System-wide failures

#### 2. **Docker Logs** (Container Output)

**View logs for specific service:**
```powershell
# Gateway Poller logs
docker logs tk-gateway-poller

# Follow logs in real-time
docker logs -f tk-gateway-poller

# Last 100 lines
docker logs --tail 100 tk-gateway-poller

# Since specific time
docker logs --since "2026-02-07T14:00:00" tk-gateway-poller

# Ingest Consumer
docker logs -f tk-ingest-consumer

# RabbitMQ
docker logs -f tk-rabbitmq

# PostgreSQL
docker logs -f tk-postgres
```

**Export logs to file:**
```powershell
docker logs tk-gateway-poller > gateway-poller_$(Get-Date -Format 'yyyyMMdd_HHmmss').log
```

#### 3. **Application Logs** (JSON Format)

Gateway.Poller and Ingest.Consumer log in structured JSON format:

**Example log entry:**
```json
{
  "@t": "2026-02-07T14:23:45.1234567Z",
  "@mt": "Polling sensor {SensorId} from {ApiUrl}",
  "@l": "Information",
  "SensorId": "env-01",
  "ApiUrl": "https://api.opensensemap.org/boxes/...",
  "SourceContext": "Gateway.Poller.ExternalSources.OpenSenseMapClient",
  "MachineName": "DESKTOP-XYZ",
  "ThreadId": 7,
  "application": "gateway-poller",
  "environment": "Development"
}
```

**Searching JSON Logs in Loki:**
```logql
# Extract and filter by field
{application="gateway-poller"} | json | SensorId="env-01"

# Filter by source context
{application="ingest-consumer"} | json | SourceContext="Ingest.Consumer.Persistence.PostgresWriter"

# Regex on fields
{application=~".*"} | json | line_format "{{.SensorId}}: {{.message}}"
```

### Log Analysis Scenarios

**Scenario: Find all errors in last hour**
```logql
{application=~"gateway-poller|ingest-consumer"} | json | level="Error" | __timestamp__ > now() - 1h
```

**Scenario: Track sensor polling success rate**
```logql
# Success logs
rate({application="gateway-poller"} |= "Successfully polled" [5m])

# vs Failures
rate({application="gateway-poller"} |= "Failed to poll" [5m])
```

**Scenario: Database write performance**
```logql
{application="ingest-consumer"} | json | line_format "INSERT took {{.ElapsedMs}}ms" | pattern `<_> took <ms>ms`
```

---

## HTTP Sensors - External API Monitoring

### Sensor Polling Architecture

Gateway.Poller polls external REST APIs on configurable intervals:
- **OpenSenseMap:** Environmental sensor network
- **Sensor.Community:** Air quality stations
- **USGS:** Water services and hydrology

### Viewing Sensor HTTP Requests

#### Method 1: Grafana Metrics (Real-time)

**Access Prometheus:**
- URL: http://localhost:9091
- Navigate: Graph tab

**Key Metrics:**
```promql
# Poll attempts per sensor
tk_polls_total{sensorId="env-01"}

# Failed polls
tk_polls_failed_total

# HTTP duration percentiles
histogram_quantile(0.95, rate(tk_poll_duration_ms_bucket[5m]))

# Success rate
rate(tk_events_inserted_total[5m]) / rate(tk_polls_total[5m])
```

**In Grafana:**
1. Open "Sensor Overview" dashboard
2. Look for custom sensor metrics (if added)
3. Or create ad-hoc query in Explore

#### Method 2: Application Logs

**View HTTP request details:**
```logql
{application="gateway-poller"} |= "Polling sensor"
```

**Example log output:**
```json
{
  "level": "Information",
  "message": "Polling sensor env-01",
  "SensorId": "env-01",
  "ApiUrl": "https://api.opensensemap.org/boxes/ABC123",
  "Interval": "00:01:00"
}
```

**After poll completion:**
```json
{
  "level": "Information",
  "message": "Successfully polled sensor",
  "SensorId": "env-01",
  "StatusCode": 200,
  "ElapsedMs": 234,
  "MeasurementCount": 5
}
```

**On failure:**
```json
{
  "level": "Warning",
  "message": "Failed to poll sensor",
  "SensorId": "env-01",
  "StatusCode": 503,
  "Error": "Service Temporarily Unavailable"
}
```

#### Method 3: Database - Check Latest Events

```sql
-- See what sensors are actively sending data
SELECT 
  sensor_id,
  MAX(observed_at) as last_event,
  COUNT(*) as events_last_hour,
  MAX(status_level) as last_status
FROM sensor_events
WHERE observed_at > NOW() - INTERVAL '1 hour'
GROUP BY sensor_id
ORDER BY last_event DESC;
```

### Testing Sensor APIs Manually

**Test OpenSenseMap API:**
```powershell
# Example sensor (replace with actual ID from appsettings.json)
Invoke-WebRequest -Uri "https://api.opensensemap.org/boxes/5c6ec1ae15451500198f5abe" | ConvertFrom-Json | ConvertTo-Json -Depth 5
```

**Check sensor configuration:**
```powershell
# View Gateway.Poller config
Get-Content src/Gateway.Poller/appsettings.json | ConvertFrom-Json | Select-Object -ExpandProperty Sensors
```

### Sensor Health Dashboard (Manual Check)

**Create this view in PGAdmin:**
```sql
CREATE OR REPLACE VIEW sensor_health AS
SELECT 
  sensor_id,
  MAX(observed_at) as last_seen,
  NOW() - MAX(observed_at) as time_since_last,
  COUNT(*) as events_today,
  COUNT(*) FILTER (WHERE status_level = 'ERROR') as errors_today,
  CASE 
    WHEN MAX(observed_at) < NOW() - INTERVAL '10 minutes' THEN 'OFFLINE'
    WHEN COUNT(*) FILTER (WHERE status_level = 'ERROR') > 5 THEN 'DEGRADED'
    ELSE 'HEALTHY'
  END as health_status
FROM sensor_events
WHERE observed_at > CURRENT_DATE
GROUP BY sensor_id;

-- Query it
SELECT * FROM sensor_health ORDER BY health_status, last_seen DESC;
```

---

## RabbitMQ - Message Broker Operations

### Access Management UI
- **URL:** http://localhost:15672
- **Username:** `tk`
- **Password:** `tk`

### Management UI Overview

#### **Overview Tab**
- **Cluster Status:** Should show single node
- **Message Rate:** Publish/deliver/ack rates (should be balanced)
- **Connections:** Gateway.Poller (publisher) + Ingest.Consumer (consumer)
- **Queued Messages:** Should trend toward zero (consumers keeping up)

#### **Connections Tab**
Shows active AMQP connections:
- **Gateway.Poller:** 1 connection for publishing
- **Ingest.Consumer:** 1-N connections for consuming

**Healthy Connection:**
- State: `running`
- Channels: 1+
- No errors in "Details"

**Troubleshooting:**
- If connection missing → service crashed or network issue
- If state != `running` → check application logs
- Click connection name → View channels and recent activity

#### **Channels Tab**
Each connection has 1+ channels (lightweight virtual connections):
- **Publisher channel:** Confirms enabled, publish rate visible
- **Consumer channel:** Prefetch count (default 10), consume rate

#### **Queues Tab**
**Primary Queue:** `ingest-events`

**Healthy Queue Metrics:**
- **Ready:** 0-100 messages (consumers keeping up)
- **Unacked:** 0-50 messages (being processed)
- **Total:** Sum of Ready + Unacked
- **Incoming rate ≈ Outgoing rate** (steady state)

**Warning Signs:**
- Ready > 1000 → Consumers falling behind
- Unacked growing → Consumer stuck/slow
- Incoming >> Outgoing → Add more consumers or investigate consumer performance

**Click queue name** → Detailed view:
- **Get Messages:** Manually inspect messages (non-destructive peek)
- **Purge:** Delete all messages (⚠️ use only in dev/testing)
- **Delete:** Remove queue entirely (⚠️ dangerous)

#### **Exchanges Tab**
**Primary Exchange:** `sensor-events` (topic exchange)

- **Type:** topic (routing key-based)
- **Durability:** Durable (survives broker restart)
- **Bindings:** Connected to `ingest-events` queue

**Publish Test:**
1. Click exchange name
2. Scroll to "Publish message"
3. Routing key: `sensor.event`
4. Payload (JSON):
```json
{
  "eventId": "test-001",
  "sensorId": "test-sensor",
  "observedAt": "2026-02-07T14:00:00Z",
  "statusLevel": 0,
  "statusMessage": "Test event",
  "measurements": [
    {"phenomenon": "temperature", "value": "22.5", "unit": "°C"}
  ]
}
```
5. Click "Publish message"
6. Check queue for message arrival

### RabbitMQ Metrics in Prometheus

**Query Prometheus:**
```promql
# Queue depth
rabbitmq_queue_messages{queue="ingest-events"}

# Publish rate
rate(rabbitmq_channel_messages_published_total[1m])

# Consume rate
rate(rabbitmq_channel_messages_delivered_total[1m])

# Unacked messages
rabbitmq_queue_messages_unacknowledged{queue="ingest-events"}

# Consumer count
rabbitmq_queue_consumers{queue="ingest-events"}

# Memory usage
rabbitmq_process_resident_memory_bytes / 1024 / 1024
```

**View in Grafana:**
- Dashboard: "Operational Monitoring - Ingest Reliability"
- Section: "📈 Throughput & Latency"
- Panel: "RabbitMQ Message Throughput" and "RabbitMQ Queue Depth"

### Common RabbitMQ Operations

#### Graceful Restart
```powershell
docker-compose -f infra/compose/docker-compose.yml restart rabbitmq
```
- Messages in durable queues are preserved
- Connections will reconnect automatically

#### Force Stop (Emergency)
```powershell
docker stop tk-rabbitmq
docker start tk-rabbitmq
```

#### View Detailed Logs
```powershell
docker logs -f tk-rabbitmq
```

#### Export Queue Definitions (Backup)
Queue definitions are stored in `infra/rabbitmq/definitions.json`

#### Enable Additional Plugins
```powershell
# Example: Enable tracing plugin
docker exec tk-rabbitmq rabbitmq-plugins enable rabbitmq_tracing

# List available plugins
docker exec tk-rabbitmq rabbitmq-plugins list
```

### Message Flow Debugging

**End-to-end message trace:**

1. **Publisher (Gateway.Poller):**
```logql
{application="gateway-poller"} |= "Published event"
```
Look for: `eventId`, `sensorId`, publish confirmation

2. **Queue (RabbitMQ UI):**
- Check queue depth increased
- Peek at message content

3. **Consumer (Ingest.Consumer):**
```logql
{application="ingest-consumer"} |= "Received event"
```
Look for: `eventId` matching publisher

4. **Database (PGAdmin):**
```sql
SELECT * FROM sensor_events WHERE event_id = 'YOUR_EVENT_ID';
```

---

## Common Troubleshooting Scenarios

### Scenario 1: Service Not Starting

**Symptoms:**
- Container status: "Restarting" or "Exited"
- Service not responding

**Diagnosis:**
```powershell
# Check container status
docker ps -a | Select-String "tk-"

# View logs for failed service
docker logs tk-SERVICE-NAME
```

**Common Causes:**
- **Port conflict:** Another service using the same port
  - Solution: Stop conflicting service or change port in docker-compose.yml
- **Dependency not ready:** Service starts before database/rabbitmq
  - Solution: Check `depends_on` and `healthcheck` in docker-compose.yml
- **Configuration error:** Invalid connection string or missing env var
  - Solution: Check appsettings.json and docker-compose.yml environment variables

### Scenario 2: Queue Backing Up (High Queue Depth)

**Symptoms:**
- RabbitMQ queue depth > 1000
- "Ready" messages increasing
- Consumer lag visible in Grafana

**Diagnosis:**
```powershell
# Check consumer is running
docker ps | Select-String "ingest-consumer"

# View consumer logs
docker logs tk-ingest-consumer --tail 50

# Check database performance
# Run in PGAdmin - Active queries
SELECT * FROM pg_stat_activity WHERE state != 'idle';
```

**Solutions:**
1. **Slow database writes:** 
   - Check PostgreSQL connections and locks in PGAdmin
   - Look for long-running queries
   - Consider vacuuming tables: `VACUUM ANALYZE sensor_events;`

2. **Consumer crashed:**
   - Restart consumer: `docker-compose restart ingest-consumer`

3. **Database connection pool exhausted:**
   - Check connection count in Grafana
   - Increase pool size in appsettings.json (MaxPoolSize)

### Scenario 3: No Data Ingesting

**Symptoms:**
- DB Transactions/sec = 0
- No recent events in database
- Queue depth = 0

**Diagnosis Steps:**
1. **Check Gateway.Poller is running:**
```powershell
docker ps | Select-String "gateway-poller"
docker logs tk-gateway-poller --tail 50
```

2. **Check external API connectivity:**
```powershell
# Test OpenSenseMap API
Invoke-WebRequest -Uri "https://api.opensensemap.org/boxes/5c6ec1ae15451500198f5abe"
```

3. **Check RabbitMQ connectivity:**
```logql
{application="gateway-poller"} |= "RabbitMQ"
```
Look for connection errors

**Solutions:**
- **Network issues:** Check internet connectivity, DNS resolution
- **API rate limiting:** External API may be throttling requests (check response headers)
- **Service stopped:** Restart Gateway.Poller

### Scenario 4: High Error Rate

**Symptoms:**
- .NET Error Rate > 1/sec in Grafana
- Error logs in Loki

**Diagnosis:**
```logql
# Find error details
{application=~"gateway-poller|ingest-consumer"} | json | level="Error"
```

**Common Errors:**
- **Database deadlocks:** Multiple transactions conflicting
  - Solution: Check queries, add indexes, review transaction isolation
- **Duplicate key violations:** Idempotency ID collision (rare)
  - Solution: Check event_id generation logic
- **NullReferenceException:** Missing data from external API
  - Solution: Add null checks, improve error handling

### Scenario 5: Dashboard Not Updating

**Symptoms:**
- Grafana panels show "No data"
- Old data visible but not updating

**Diagnosis:**
1. **Check Prometheus scraping:**
```powershell
# View Prometheus targets
Invoke-WebRequest -Uri "http://localhost:9091/api/v1/targets" -UseBasicParsing | ConvertFrom-Json | Select-Object -ExpandProperty data | Select-Object -ExpandProperty activeTargets | Select-Object scrapePool, health
```

2. **Check exporter endpoints:**
```powershell
# Test postgres-exporter
Invoke-WebRequest -Uri "http://localhost:9187/metrics" -UseBasicParsing

# Test node-exporter
Invoke-WebRequest -Uri "http://localhost:9100/metrics" -UseBasicParsing
```

**Solutions:**
- **Scrape failing:** Restart Prometheus: `docker-compose restart prometheus`
- **Exporter down:** Restart exporter container
- **Query syntax error:** Check PromQL query in panel editor

---

## Performance Baselines

### Expected Throughput (Single Instance)

**Normal Operations:**
- **Sensor polls:** ~10 sensors × 1 poll/min = 10 polls/min
- **Events ingested:** ~10-50 events/min (depends on sensor update frequency)
- **Database writes:** ~10-50 rows/min
- **Queue latency:** < 1 second (message publish → consume)
- **End-to-end latency:** < 5 seconds (API poll → DB write)

**Load Testing (100k events seed):**
- **Insert rate:** Up to 10,000 rows/sec (bulk insert)
- **Query response:** < 100ms for simple SELECT with LIMIT
- **Index scan:** < 500ms for time-range queries (1 day window)

### Resource Usage Baselines

**Idle State (No Active Polling):**
- CPU: < 5%
- Memory: ~2 GB total (all containers)
- Disk I/O: < 1 MB/s
- Network: < 100 KB/s

**Active Polling (10 sensors, 1/min):**
- CPU: 10-20%
- Memory: ~2.5 GB
- Disk I/O: 5-10 MB/s (depends on database writes)
- Network: 500 KB/s - 1 MB/s

**Alert Thresholds:**
- CPU > 70% for > 5 minutes → Investigate
- Memory > 75% → Check for leaks, consider scaling
- Disk I/O > 100 MB/s sustained → Bottleneck risk
- Queue depth > 1000 → Consumer falling behind

---

## Emergency Contacts & Escalation

### Severity Levels

**P0 - Critical (Immediate Response)**
- Complete system outage
- Data loss occurring
- Security breach

**P1 - High (Response within 1 hour)**
- Service degraded but functional
- High error rates
- Queue backing up critically

**P2 - Medium (Response within 4 hours)**
- Performance degradation
- Non-critical errors
- Monitoring gaps

**P3 - Low (Response within 24 hours)**
- Minor issues
- Feature requests
- Documentation updates

### Escalation Path

1. **First Response:** Check this Operators Manual
2. **Self-Service:** Review [Runbook](./runbooks/local-dev.md) for common scenarios
3. **Team Lead:** Contact project maintainer
4. **Engineering:** Escalate to development team

### Quick Recovery Actions

**Nuclear Option (Last Resort):**
```powershell
# Stop all services
docker-compose -f infra/compose/docker-compose.yml down

# Remove volumes (⚠️ DATA LOSS)
docker-compose -f infra/compose/docker-compose.yml down -v

# Rebuild and restart
docker-compose -f infra/compose/docker-compose.yml up -d --build
```

**Preserve Data Recovery:**
```powershell
# Backup database first
docker exec tk-postgres pg_dump -U tk telemetry_kitchen > emergency_backup.sql

# Then restart services
docker-compose -f infra/compose/docker-compose.yml restart
```

---

## Appendix: Quick Reference

### Service URLs
| Service | URL | Credentials |
|---------|-----|-------------|
| Grafana | http://localhost:3000 | admin / admin |
| PGAdmin | http://localhost:5050 | admin@example.com / admin |
| RabbitMQ Management | http://localhost:15672 | tk / tk |
| Prometheus | http://localhost:9091 | - |
| PostgreSQL | localhost:5432 | tk / tk |

### Log Queries Cheat Sheet
```logql
# All errors
{application=~".*"} | json | level="Error"

# Specific service
{application="gateway-poller"}

# Contains text
{application=~".*"} |= "sensor_id"

# Regex filter
{application=~".*"} |~ "ERROR|WARN"

# JSON field filter
{application=~".*"} | json | SensorId="env-01"

# Rate calculation
rate({application=~".*"} [1m])
```

### PromQL Cheat Sheet
```promql
# Rate of increase (counter metrics)
rate(metric_total[1m])

# Current value (gauge metrics)
metric_name

# Aggregation
sum(metric_name)
avg(metric_name)
max(metric_name)

# Filtering
metric_name{label="value"}

# Percentiles (histogram)
histogram_quantile(0.95, rate(metric_bucket[5m]))
```

### Docker Commands Cheat Sheet
```powershell
# List running containers
docker ps

# View logs
docker logs -f CONTAINER_NAME

# Restart service
docker-compose -f infra/compose/docker-compose.yml restart SERVICE_NAME

# Stop all
docker-compose -f infra/compose/docker-compose.yml down

# Start all
docker-compose -f infra/compose/docker-compose.yml up -d

# Rebuild service
docker-compose -f infra/compose/docker-compose.yml up -d --build SERVICE_NAME
```

---

**Document Version:** 1.0  
**Last Updated:** February 7, 2026  
**Maintained By:** Telemetry Kitchen Team
