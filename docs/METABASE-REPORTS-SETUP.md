# Metabase Reports Setup Guide

**Purpose:** Create BI/analytics dashboards in Metabase to fulfill the "Tableau alternative" requirement outlined in the Master Prompt.

**Access:** http://localhost:3001  
**Credentials:** admin@example.com / admin  
**Database:** telemetry_kitchen (PostgreSQL)

---

## Quick Setup Summary

This guide will help you create:

1. ✅ **Average Temperature Report** — Time-series comparison of temperature measurements by source
2. ✅ **Sensors by Source Report** — Active sensor count and event volume by source type
3. ✅ **Data Quality Dashboard** — View status distribution and error rates
4. ✅ **Sensor Inactivity Report** — Detect sensors that haven't reported recently

---

## Initial Setup (First Time Only)

### 1. Access Metabase Admin

1. Open **http://localhost:3001** in your browser
2. On the welcome screen, click **"Let's get started"**
3. Enter setup credentials:
   - Email: `admin@example.com`
   - Password: `admin`
   - Company name: `Telemetry Kitchen`
4. Click **"Next"** to proceed
5. When prompted for database connection, **skip** (PostgreSQL should auto-connect)
6. If needed, manually add PostgreSQL:
   - Host: `postgres`
   - Port: `5432`
   - Database: `telemetry_kitchen`
   - Username: `tk`
   - Password: `tk`

### 2. Verify PostgreSQL Connection

1. Click **Settings** (gear icon, top right)
2. Select **Admin → Databases**
3. Verify `telemetry_kitchen` appears in the list
4. If not present, click **+ Add database** and enter credentials above

---

## Report 1: Average Temperature by Source

**Purpose:** Track temperature trends over time, comparing OpenSenseMap vs. Sensor.Community data quality.

### Create the Saved Question

1. Click **+ New** → **Question**
2. Select **Native query** (SQL)
3. Paste this SQL query:

```sql
-- Average Temperature by Source (Hourly)
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
    AND (m->>'value')::numeric BETWEEN -50 AND 60  -- Filter realistic temps
    AND se.observed_at > NOW() - INTERVAL '7 days'
)
SELECT
  source_type,
  measurement_hour,
  COUNT(DISTINCT sensor_id) as active_sensors,
  ROUND(AVG(temp_value)::numeric, 2) as avg_temperature_c,
  ROUND(MIN(temp_value)::numeric, 2) as min_temp,
  ROUND(MAX(temp_value)::numeric, 2) as max_temp,
  STDDEV(temp_value)::numeric as temp_stddev,
  COUNT(*) as measurement_count
FROM temperature_measurements
GROUP BY source_type, measurement_hour
ORDER BY measurement_hour DESC, source_type;
```

### Configure Visualization

1. Click **Visualize**
2. Click **Visualization settings** (chart icon)
3. Select **Time series** (line chart)
4. X-axis: `measurement_hour`
5. Y-axis: `avg_temperature_c`
6. Series breakdown: `source_type` (creates two lines: one per source)
7. Click **Done**

### Save the Question

1. Click **Save** (top right)
2. Enter name: **"Average Temperature by Source (Hourly)"**
3. Click **Save**

---

## Report 2: Sensors by Source Comparison

**Purpose:** Compare active sensor counts, event volumes, and data quality between OpenSenseMap and Sensor.Community.

### Create the Saved Question

1. Click **+ New** → **Question**
2. Select **Native query** (SQL)
3. Paste this SQL query:

```sql
-- Sensors by Source Comparison (Daily Aggregation)
WITH daily_stats AS (
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
  WHERE received_at > NOW() - INTERVAL '30 days'
  GROUP BY source_type, DATE_TRUNC('day', received_at)
)
SELECT
  source_type,
  event_date,
  unique_sensors,
  total_events,
  successful_events,
  failed_events,
  success_rate_percent
FROM daily_stats
ORDER BY event_date DESC, source_type;
```

### Configure Visualization

1. Click **Visualize**
2. Select **Table** visualization
3. Format columns:
   - `event_date`: Short date format
   - `success_rate_percent`: Number with 2 decimals
   - `unique_sensors`: Integer
4. Click **Done**

### Save the Question

1. Click **Save**
2. Enter name: **"Sensors by Source — Daily Statistics"**
3. Click **Save**

---

## Report 3: Data Quality Dashboard Summary

**Purpose:** View overall data quality metrics: success rate, error count, status distribution.

### Create the Saved Question

1. Click **+ New** → **Question**
2. Select **Native query** (SQL)
3. Paste this SQL query:

```sql
-- Overall Data Quality Summary (Last 7 Days)
SELECT
  source_type,
  status_level,
  COUNT(*) as event_count,
  ROUND(100.0 * COUNT(*) / 
        (SELECT COUNT(*) FROM sensor_events 
         WHERE received_at > NOW() - INTERVAL '7 days'), 2) as percent_of_total
FROM sensor_events
WHERE received_at > NOW() - INTERVAL '7 days'
GROUP BY source_type, status_level
ORDER BY source_type, status_level;
```

### Configure Visualization

1. Click **Visualize**
2. Select **Pie** chart
3. Count by: `event_count`
4. Breakdown: `status_level`
5. Click **Done**

### Save the Question

1. Click **Save**
2. Enter name: **"Data Quality Summary (7 Days)"**
3. Click **Save**

---

## Report 4: Active Sensors Real-Time Count

**Purpose:** Quick metric showing how many sensors have reported in the last 5 minutes.

### Create the Saved Question

1. Click **+ New** → **Question**
2. Select **Simple question**
3. Pick data: **sensor_events**
4. Click **Count** → **Summarize**
5. In the modal, click **Add a filter** → **received_at** → **Relative dates** → **Last 5 minutes**
6. Summarize: **Distinct values of sensor_id**
7. Click **Visualize**
8. Select **Number** visualization
9. Click **Done**

### Save the Question

1. Click **Save**
2. Enter name: **"Active Sensors (Last 5 Minutes)"**
3. Click **Save**

---

## Report 5: Sensor Inactivity Detection

**Purpose:** Find sensors that haven't reported recently (potential issues).

### Create the Saved Question

1. Click **+ New** → **Question**
2. Select **Native query** (SQL)
3. Paste this SQL query:

```sql
-- Sensors That Are Inactive (Last Report > 1 Hour Ago)
SELECT
  s.sensor_id,
  s.source_type,
  s.display_name,
  MAX(se.received_at) as last_event,
  NOW() - MAX(se.received_at) as hours_since_last_event,
  COUNT(*) as total_events_ever
FROM sensors s
LEFT JOIN sensor_events se ON s.sensor_id = se.sensor_id
GROUP BY s.sensor_id, s.source_type, s.display_name
HAVING NOW() - MAX(se.received_at) > INTERVAL '1 hour'
   OR MAX(se.received_at) IS NULL
ORDER BY last_event DESC NULLS LAST;
```

### Configure Visualization

1. Click **Visualize**
2. Select **Table**
3. Sort by: `last_event` (descending)
4. Click **Done**

### Save the Question

1. Click **Save**
2. Enter name: **"Inactive Sensors (> 1 Hour No Report)"**
3. Click **Save**

---

## Creating a Combined Dashboard

### 1. Create a New Dashboard

1. Click **+ New** → **Dashboard**
2. Enter title: **"Sensor Data Quality & Analytics"**
3. Click **Create dashboard**

### 2. Add Saved Questions to Dashboard

1. Click **+ Add a card**
2. Select **"Average Temperature by Source (Hourly)"**
3. Click to add it
4. Repeat for each saved question:
   - **"Sensors by Source — Daily Statistics"**
   - **"Data Quality Summary (7 Days)"**
   - **"Active Sensors (Last 5 Minutes)"**
   - **"Inactive Sensors (> 1 Hour No Report)"**

### 3. Arrange Dashboard Layout

1. Drag cards to arrange (typically 2 per row)
2. Resize by dragging card corners
3. Suggested layout:
   ```
   Row 1: Active Sensors (metric) | Data Quality Summary (pie)
   Row 2: Temperature Trends (time series)
   Row 3: Daily Statistics (table)
   Row 4: Inactive Sensors (table)
   ```

### 4. Save Dashboard

1. Click **Save** (top right)
2. Enter name: **"Sensor Data Quality & Analytics"**
3. Click **Save**

### 5. Set Auto-Refresh (Optional)

1. Click **Dashboard info** (ⓘ icon)
2. Under **Auto-refresh**, select **Every 5 minutes**
3. Click **Done**

---

## Scheduled Refreshes & Exports

### Export Query Results

1. From any saved question, click **Export** (bottom right)
2. Choose format: **CSV**, **JSON**, or **Excel**
3. Download file

### Email Reports (Premium Feature)

_Note: Available in Metabase Pro. Open-source version does not support scheduled email distribution._

For now, manually export or share dashboard links:
- Click **Share** in dashboard or question
- Copy URL and share with team
- Public links can be embedded in wikis or shared drives

---

## SQL Query Reference

### Get Measurement Names in Your Data

```sql
SELECT DISTINCT m->>'name' as measurement_name, COUNT(*) as count
FROM sensor_events
CROSS JOIN LATERAL jsonb_array_elements(measurements) m
GROUP BY m->>'name'
ORDER BY count DESC;
```

### Find Specific Sensors by Source

```sql
SELECT sensor_id, source_type, display_name, COUNT(*) as events
FROM sensor_events
WHERE source_type = 'public-environment'  -- Change to 'sensor-community' for SC
GROUP BY sensor_id, source_type, display_name
ORDER BY events DESC
LIMIT 20;
```

### Check Measurement Value Range

```sql
SELECT 
  m->>'name' as measurement_name,
  MIN((m->>'value')::numeric) as min_value,
  AVG((m->>'value')::numeric) as avg_value,
  MAX((m->>'value')::numeric) as max_value
FROM sensor_events
CROSS JOIN LATERAL jsonb_array_elements(measurements) m
WHERE m->>'name' = 'temperature'  -- Change as needed
GROUP BY m->>'name';
```

---

## Troubleshooting

### Query Times Out

- Reduce time range: Change `NOW() - INTERVAL '30 days'` to smaller window
- Add more specific filters (e.g., source_type = 'public-environment')
- Consider creating indexes (see below)

### No Data Appears

- Verify PostgreSQL connection (Settings → Admin → Databases)
- Check table has data: `SELECT COUNT(*) FROM sensor_events;` in pgAdmin
- Ensure `observed_at` is in UTC timezone

### Slow Performance

Create PostgreSQL indexes for analytics queries:

```sql
-- From PostgreSQL (pgAdmin or psql)
CREATE INDEX IF NOT EXISTS idx_sensor_events_received_at ON sensor_events(received_at DESC);
CREATE INDEX IF NOT EXISTS idx_sensor_events_status ON sensor_events(status_level, received_at DESC);
CREATE INDEX IF NOT EXISTS idx_sensor_events_source ON sensor_events(source_type, received_at DESC);
```

---

## Next Steps

After setting up these reports:

1. **Share dashboards** with stakeholders
2. **Set up auto-refresh** for live monitoring
3. **Create alerts** based on data quality thresholds
4. **Export weekly reports** (CSV/PDF) for leadership
5. **Explore additional metrics:**
   - Sensor coverage by geography (lat/lon)
   - Measurement drift detection
   - API response time analysis
   - Error message frequency analysis

---

## Related Documentation

- [Main README](../README.md) — Project overview
- [Local Dev Runbook](./local-dev.md) — How to run locally
- [Metabase Setup (Original)](./METABASE-SETUP.md) — First-time connection setup
- [Architecture Decisions](./architecture/decisions.md) — Design rationale

