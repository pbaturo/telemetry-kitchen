# System Health & Data Flow Diagnostics

**Current Date:** 2026-02-08  
**Status:** ✅ **SYSTEM HEALTHY** — All services operational, data flowing continuously

---

## Quick Status Check

```bash
# Check all services running
docker-compose -f infra/compose/docker-compose.yml ps

# Check latest data in PostgreSQL
docker exec tk-postgres psql -U tk -d telemetry_kitchen -c "
  SELECT COUNT(*) as total_events, 
         MAX(received_at) as latest_event, 
         COUNT(DISTINCT sensor_id) as unique_sensors
  FROM sensor_events 
  WHERE received_at > NOW() - INTERVAL '1 hour';"

# Check active sensors
docker exec tk-postgres psql -U tk -d telemetry_kitchen -c "
  SELECT sensor_id, MAX(received_at) as last_event 
  FROM sensor_events 
  GROUP BY sensor_id 
  ORDER BY last_event DESC 
  LIMIT 20;"
```

---

## 1. SERVICE STATUS

All Docker Compose services should be running:

| Service | Port | Health Check |
|---------|------|--------------|
| PostgreSQL | 5432 | `docker logs tk-postgres` |
| RabbitMQ | 5672, 15672 | `docker logs tk-rabbitmq` |
| Gateway.Poller | 9090 | `docker logs tk-gateway-poller` |
| Ingest.Consumer | — | `docker logs tk-ingest-consumer` |
| Prometheus | 9091 | `docker logs tk-prometheus` |
| Grafana | 3000 | `docker logs tk-grafana` |
| Loki | 3100 | `docker logs tk-loki` |
| Web.Mvc | 5000 | `docker logs tk-web-mvc` |
| Metabase | 3001 | `docker logs tk-metabase` |

---

## 2. DATA FRESHNESS VERIFICATION

### Check if data is flowing

**PostgreSQL Query:**

```sql
SELECT COUNT(*) as total_events, 
       MAX(received_at) as latest_event, 
       COUNT(DISTINCT sensor_id) as unique_sensors
FROM sensor_events 
WHERE received_at > NOW() - INTERVAL '1 hour';
```

**Expected Result:** Should show events from the last hour.

**Run via Docker:**

```bash
docker exec tk-postgres psql -U tk -d telemetry_kitchen -c "
  SELECT COUNT(*), MAX(received_at), COUNT(DISTINCT sensor_id) 
  FROM sensor_events 
  WHERE received_at > NOW() - INTERVAL '1 hour';"
```

---

## 3. SENSOR HEALTH CHECK

### Check which sensors are actively reporting

```sql
SELECT 
  sensor_id,
  COUNT(*) as total_events,
  MAX(received_at) as last_event,
  NOW() - MAX(received_at) as time_since_event,
  CASE 
    WHEN NOW() - MAX(received_at) < INTERVAL '5 minutes' THEN '✅ ACTIVE'
    WHEN NOW() - MAX(received_at) < INTERVAL '1 hour' THEN '⚠️ IDLE'
    ELSE '❌ OFFLINE'
  END as status
FROM sensor_events
GROUP BY sensor_id
ORDER BY last_event DESC
LIMIT 20;
```

**Run via Docker:**

```bash
docker exec tk-postgres psql -U tk -d telemetry_kitchen -c "
  SELECT sensor_id, COUNT(*), MAX(received_at), 
         CASE WHEN NOW() - MAX(received_at) < INTERVAL '5 minutes' THEN 'ACTIVE' 
              ELSE 'IDLE' END as status
  FROM sensor_events
  GROUP BY sensor_id
  ORDER BY last_event DESC LIMIT 20;"
```

---

## 4. POLLING STATUS

### Check Gateway.Poller logs

```bash
docker logs tk-gateway-poller --tail 50
```

**Expected Patterns:**
- `[INF] Poll completed: sensorId=...` → Successful polls
- `[WRN] Poll failed: httpStatus=500` → Transient external API errors (normal)
- Recent timestamps → Polling is active

### Check publish to RabbitMQ

Look for lines like:
```
publishDurationMs=1
```

This shows messages are flowing to the queue.

---

## 5. PROMETHEUS METRICS

### Check if Prometheus is scraping

Visit: **http://localhost:9091/targets**

All targets should show green checkmarks ✅

### Query specific metrics

```bash
# Test Gateway.Poller metrics
curl -s http://localhost:9090/metrics | grep "poll"

# Test Ingest.Consumer metrics  
curl -s http://localhost:9091/api/v1/query?query=up | jq .
```

---

## 6. GRAFANA DASHBOARDS

### Access Grafana

**URL:** http://localhost:3000  
**User:** admin  
**Password:** admin

### Common Issues & Fixes

| Issue | Cause | Solution |
|-------|-------|----------|
| Dashboard shows old data | Time range filter | Click time picker (top right) → select "Last 1 hour" |
| Metrics not updating | Grafana not auto-refresh | Manually press F5 or set dashboard refresh interval |
| Data missing completely | Prometheus not scraping | Check http://localhost:9091/targets |

---

## 7. WEB.MVC SENSOR DETAIL

### Test the enhanced UI

**URL:** http://localhost:5000/Sensors/Detail?sensorId=env-01

**Verify:**
- ✅ All parameter names are in English (not German)
- ✅ Hover over parameter names → tooltips appear
- ✅ Each parameter has emoji + description + unit
- ✅ Status badges show color-coded meanings

**Parameter Examples:**
- 🌡️ Temperature = Ambient Temperature (Celsius °C)
- 💧 Humidity = Relative Humidity (Percentage %)
- 🫁 PM2.5 = Particulate Matter 2.5μm (µg/m³)
- 🔽 Pressure = Atmospheric Pressure (hPa)

---

## 8. TROUBLESHOOTING GUIDE

### Problem: "I don't see new data in Grafana"

**Step 1 - Check PostgreSQL**

```bash
docker exec tk-postgres psql -U tk -d telemetry_kitchen -c "
  SELECT COUNT(*) FROM sensor_events 
  WHERE received_at > NOW() - INTERVAL '5 minutes';"
```

If result > 0, data IS flowing. If 0, check Gateway.Poller:

```bash
docker logs tk-gateway-poller --tail 100 | grep -i "error\|warn"
```

**Step 2 - Check Prometheus targets**

Visit: http://localhost:9091/targets  
Look for red ❌ marks → those targets are down

**Step 3 - Fix Grafana dashboard**

1. Open http://localhost:3000 → click dashboard
2. Click time picker (top right) → select "Last 1 hour"
3. Click refresh button or press F5

### Problem: "I see German text on Web.Mvc"

**Step 1 - Verify code has English**

```bash
grep -r "German\|Deutsch" src/Web.Mvc/Views/Sensors/Detail.cshtml
```

Should return nothing (no German text in code).

**Step 2 - Rebuild Docker image**

```bash
docker-compose -f infra/compose/docker-compose.yml build web-mvc
docker-compose -f infra/compose/docker-compose.yml up -d web-mvc
```

**Step 3 - Clear browser cache**

Press Ctrl+Shift+Delete in browser, clear recent 1 hour, reload page.

### Problem: "Prometheus showing no targets"

```bash
# Check prometheus.yml is valid
docker exec tk-prometheus cat /etc/prometheus/prometheus.yml | head -20

# Restart Prometheus
docker-compose -f infra/compose/docker-compose.yml restart prometheus

# Check logs
docker logs tk-prometheus | tail -20
```

### Problem: "Services keep restarting"

```bash
# Check service logs for errors
docker logs tk-gateway-poller
docker logs tk-ingest-consumer
docker logs tk-web-mvc

# Check compose health
docker-compose -f infra/compose/docker-compose.yml ps
```

---

## 9. LOKI LOG QUERIES

### Query logs in Grafana

1. Open http://localhost:3000
2. Click Explorer (left sidebar)
3. Select "Loki" data source
4. Enter query:

```logql
{application="gateway-poller"}
{application="ingest-consumer"}
{level="error"}
```

### Test Loki directly

```bash
curl -s "http://localhost:3100/loki/api/v1/query_range" \
  -G -d 'query={application="gateway-poller"}' \
  -d 'start=<unix_timestamp>' \
  -d 'end=<unix_timestamp>' \
  -d 'limit=10'
```

---

## 10. PERFORMING A FULL SYSTEM RESTART

If services are unstable:

```bash
# Stop all services
docker-compose -f infra/compose/docker-compose.yml down

# Wait 5 seconds
sleep 5

# Start all services
docker-compose -f infra/compose/docker-compose.yml up -d

# Check status
docker-compose -f infra/compose/docker-compose.yml ps

# Wait for health checks (30 seconds)
docker-compose -f infra/compose/docker-compose.yml ps --no-trunc | grep -i "healthy\|running"
```

---

## 11. REBUILDING INDIVIDUAL SERVICES

### Rebuild Web.Mvc (with latest Detail.cshtml changes)

```bash
docker-compose -f infra/compose/docker-compose.yml build web-mvc
docker-compose -f infra/compose/docker-compose.yml up -d web-mvc
docker logs tk-web-mvc --tail 20
```

### Rebuild Gateway.Poller

```bash
docker-compose -f infra/compose/docker-compose.yml build gateway-poller
docker-compose -f infra/compose/docker-compose.yml up -d gateway-poller
docker logs tk-gateway-poller --tail 20
```

### Rebuild Ingest.Consumer

```bash
docker-compose -f infra/compose/docker-compose.yml build ingest-consumer
docker-compose -f infra/compose/docker-compose.yml up -d ingest-consumer
docker logs tk-ingest-consumer --tail 20
```

---

## 12. POSTGRESQL DIAGNOSTICS

### Connect to database via command line

```bash
docker exec -it tk-postgres psql -U tk -d telemetry_kitchen
```

### Check materialized views

```sql
SELECT schemaname, matviewname FROM pg_matviews;
```

### Check indexes

```sql
SELECT tablename, indexname FROM pg_indexes 
WHERE schemaname = 'public';
```

### Check table sizes

```sql
SELECT schemaname, tablename, 
       pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
FROM pg_catalog.pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

---

## 13. QUICK REFERENCE - URLS

| Service | URL | User | Password |
|---------|-----|------|----------|
| Grafana | http://localhost:3000 | admin | admin |
| Prometheus | http://localhost:9091 | — | — |
| Loki | http://localhost:3100 | — | — |
| Metabase | http://localhost:3001 | admin@example.com | admin |
| PGAdmin | http://localhost:5050 | admin@example.com | admin |
| RabbitMQ | http://localhost:15672 | tk | tk |
| Web.Mvc | http://localhost:5000 | — | — |

---

## 14. NEXT STEPS IF ISSUES PERSIST

1. **Check compose file is valid:**
   ```bash
   docker-compose -f infra/compose/docker-compose.yml config
   ```

2. **Check Docker daemon is running:**
   ```bash
   docker ps
   ```

3. **Look for resource constraints:**
   ```bash
   docker stats
   ```

4. **Review system logs:**
   ```bash
   docker events --filter type=container
   ```

5. **Force restart everything:**
   ```bash
   docker-compose -f infra/compose/docker-compose.yml down -v
   docker-compose -f infra/compose/docker-compose.yml up -d
   ```
