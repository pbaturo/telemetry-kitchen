# Metabase Setup Guide

**Metabase** is a self-hosted, open-source analytics and BI (Business Intelligence) platform providing SQL-based querying, interactive dashboards, and export capabilities.

## Quick Start

### 1. Access Metabase

After docker-compose starts, Metabase is available at:
```
http://localhost:3001
```

**Initial credentials:**
- Email: `admin@example.com`
- Password: `admin`

### 2. Connect to PostgreSQL Data Source

Metabase automatically discovers the PostgreSQL database during container startup.

**Data Source:** telemetry_kitchen  
**Host:** postgres  
**Port:** 5432  
**Database:** telemetry_kitchen  
**Username:** tk

Once connected, you'll see the `sensor_events` table and all related data available for querying.

---

## Features & Use Cases

### 🔍 SQL Query Interface

Write custom SQL queries to explore sensor data:

```sql
-- Recent sensor events with status
SELECT 
  sensor_id,
  observed_at,
  status_level,
  measurements->0->>'Name' as measurement_type,
  measurements->0->>'Value' as measurement_value
FROM sensor_events
WHERE observed_at > NOW() - INTERVAL '1 hour'
ORDER BY observed_at DESC
LIMIT 100;
```

### 📊 Interactive Dashboards

Create visual dashboards combining:
- Time-series charts (sensor trends over time)
- Bar/pie charts (sensor distribution by type)
- Gauge charts (current status and thresholds)
- Tables (raw event data)

**Example Dashboard Components:**
1. **Sensor Coverage** — Map of active sensors by country (from Lat/Lon)
2. **Event Throughput** — Events per minute over 24 hours
3. **Data Quality** — Success vs. warning/error status ratio
4. **Source Comparison** — OpenSenseMap vs. Sensor.Community event volume

### 📤 Export & Reporting

- Export query results to CSV/JSON
- Auto-refresh dashboards at configurable intervals
- Embed dashboards in external apps
- Create saved questions for frequent queries

### 🔔 Alerts (Premium Feature - Not Available in Open-Source)

- Alert on anomalies or thresholds
- Email notifications

---

## Sample Queries

### 1. Sensors by Source Type

```sql
SELECT 
  source_type,
  COUNT(DISTINCT sensor_id) as unique_sensors,
  COUNT(*) as total_events,
  MAX(received_at) as latest_event
FROM sensor_events
GROUP BY source_type
ORDER BY total_events DESC;
```

### 2. Sensor Event Quality Over Time

```sql
SELECT 
  DATE_TRUNC('hour', observed_at) as hour,
  status_level,
  COUNT(*) as event_count
FROM sensor_events
WHERE observed_at > NOW() - INTERVAL '24 hours'
GROUP BY DATE_TRUNC('hour', observed_at), status_level
ORDER BY hour DESC, status_level;
```

### 3. Measurement Value Distribution (e.g., Temperature)

```sql
WITH measurements_extracted AS (
  SELECT 
    sensor_id,
    observations->>'Name' as measurement_name,
    (observations->>'Value')::numeric as value_numeric
  FROM sensor_events,
  LATERAL jsonb_array_elements(measurements) as observations
  WHERE observations->>'Name' LIKE '%temperature%'
)
SELECT 
  measurement_name,
  MIN(value_numeric) as min_value,
  AVG(value_numeric) as avg_value,
  MAX(value_numeric) as max_value,
  STDDEV(value_numeric) as std_dev,
  COUNT(*) as sample_count
FROM measurements_extracted
GROUP BY measurement_name;
```

### 4. Sensor Inactivity Detection

```sql
SELECT 
  sensor_id,
  MAX(received_at) as last_event,
  NOW() - MAX(received_at) as time_since_last_event,
  COUNT(*) as total_events
FROM sensor_events
GROUP BY sensor_id
HAVING NOW() - MAX(received_at) > INTERVAL '1 hour'
ORDER BY time_since_last_event DESC;
```

---

## Dashboard Examples

### Example 1: Real-Time Monitoring Dashboard

| Component | Chart Type | Query |
|-----------|-----------|-------|
| Active Sensors | Number card | `SELECT COUNT(DISTINCT sensor_id) FROM sensor_events WHERE received_at > NOW() - INTERVAL '5 minutes'` |
| Events Last Hour | Number card | `SELECT COUNT(*) FROM sensor_events WHERE observed_at > NOW() - INTERVAL '1 hour'` |
| Error Rate | Gauge | `SELECT ROUND(100.0 * COUNT(*) FILTER (WHERE status_level IN ('WARN', 'ERROR')) / NULLIF(COUNT(*), 0), 2) as error_percent FROM sensor_events WHERE observed_at > NOW() - INTERVAL '1 hour'` |
| Hourly Throughput | Line chart | Time-series of event counts per hour (last 24h) |
| Status Distribution | Pie chart | Event count by status_level |

### Example 2: Data Source Comparison

| Metric | OpenSenseMap | Sensor.Community |
|--------|--------------|------------------|
| Total Events | Query filtered by source_type='public-environment' | Query filtered by source_type='sensor-community' |
| Unique Sensors | `COUNT(DISTINCT sensor_id)` | `COUNT(DISTINCT sensor_id)` |
| Event Success Rate | `COUNT(*) FILTER (WHERE status_level='INFO')` | `COUNT(*) FILTER (WHERE status_level='INFO')` |
| Measurement Types | Group measurements->>'Name' | Group measurements->>'Name' |

---

## Integration with Other Components

### Grafana vs. Metabase

| Aspect | Grafana | Metabase |
|--------|---------|----------|
| **Primary Use** | Real-time ops monitoring (metrics/logs) | One-off analysis & BI (SQL queries) |
| **Data Sources** | Prometheus, Loki, InfluxDB, etc. | SQL databases (PostgreSQL, MySQL, etc.) |
| **Dashboard Type** | Metric-based (gauges, graphs) | Query-based (tables, charts) |
| **Query Language** | Metric expressions, LogQL | Native SQL |
| **Best For** | DevOps, infrastructure monitoring | Business analytics, data exploration |

**Recommendation:** Use both!
- **Grafana** for live ops dashboards (infrastructure health, ingest pipeline)
- **Metabase** for exploratory analytics and business questions

---

## Troubleshooting

### Metabase Connection Issues

**Problem:** "Unable to connect to database"

**Solution:**
1. Verify PostgreSQL is running: `docker ps | grep tk-postgres`
2. Check PostgreSQL health: `docker logs tk-postgres | tail -20`
3. Verify connection string in Metabase (Settings → Admin → Databases) matches `postgres:5432`

### Slow Queries

**Problem:** Dashboard queries timeout or are slow

**Solution:**
1. Add database indexes on frequently queried columns:
   ```sql
   CREATE INDEX idx_sensor_events_sensor_id ON sensor_events(sensor_id);
   CREATE INDEX idx_sensor_events_status_level ON sensor_events(status_level);
   CREATE INDEX idx_sensor_events_observed_at ON sensor_events(observed_at DESC);
   ```
2. Use date range filters to limit result sets
3. Avoid complex JSON operations on large datasets

### Permission Errors

**Problem:** "You don't have permission to view this question/dashboard"

**Solution:**
- Metabase defaults to admin user having full permissions
- For multi-user access, configure roles in Admin → People & Permissions

---

## Next Steps

1. ✅ Start Metabase with Docker Compose
2. ✅ Connect to PostgreSQL data source
3. ✅ Create your first custom SQL query
4. ✅ Build interactive dashboards
5. ✅ Share dashboards with team members
6. (Optional) Set up scheduled exports or email reports

---

## Documentation References

- **Metabase Docs:** https://www.metabase.com/docs/
- **PostgreSQL Query Guide:** See [local-dev.md](runbooks/local-dev.md)
- **Sample Queries:** [METABASE-QUERIES.md](METABASE-QUERIES.md) (coming soon)

---

**Ready to explore your data with Metabase!** 📊
