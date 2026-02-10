# Grafana Dashboard Verification & Testing Guide

## Overview
This guide provides step-by-step instructions for testing and verifying the updated Grafana dashboards for the Telemetry Kitchen system.

---

## 🚀 Prerequisites

Before testing, ensure all services are running:

```powershell
# Start all services using Docker Compose
cd infra/compose
docker-compose up -d

# Verify all containers are running
docker-compose ps
```

Expected services:
- ✅ gateway-poller (port 9090)
- ✅ ingest-consumer (port 9092)
- ✅ web-mvc (port 9094)
- ✅ prometheus (port 9091)  
- ✅ grafana (port 3000)
- ✅ postgres
- ✅ rabbitmq

---

## 🔍 Step 1: Verify Prometheus Targets

1. **Open Prometheus UI**
   ```
   http://localhost:9091/targets
   ```

2. **Verify All Targets are UP**
   - ✅ `gateway-poller` (http://gateway-poller:9090/metrics) - State: UP
   - ✅ `ingest-consumer` (http://ingest-consumer:9092/metrics) - State: UP
   - ✅ `web-mvc` (http://web-mvc:9094/metrics) - State: UP
   - ✅ `postgres-exporter` - State: UP
   - ✅ `rabbitmq` - State: UP

3. **If any target shows DOWN:**
   ```powershell
   # Check service logs
   docker logs gateway-poller
   docker logs ingest-consumer
   docker logs web-mvc
   
   # Restart if needed
   docker restart <service-name>
   ```

---

## 📊 Step 2: Access Grafana

1. **Open Grafana**
   ```
   http://localhost:3000
   ```

2. **Login Credentials** (default)
   - Username: `admin`
   - Password: `admin` (change on first login if prompted)

3. **Navigate to Dashboards**
   - Click "Dashboards" in left sidebar
   - Or go to: http://localhost:3000/dashboards

---

## ✅ Step 3: Test Operational Monitoring Dashboard

### Access Dashboard
```
http://localhost:3000/d/operational-monitoring
```
or search for: **"Operational Monitoring - Telemetry Kitchen"**

### Verify Panels

#### 🎯 Application Health Row
1. **Gateway Poll Rate**
   - Should show value > 0 (e.g., 0.5 - 2 polls/sec)
   - Green gauge indicates active polling

2. **Event Publish Rate**
   - Should match or be close to poll rate
   - Indicates successful data publishing

3. **Event Consume Rate**
   - Should match publish rate
   - Shows consumer is processing messages

4. **Event Process Rate**
   - Should equal consume rate
   - Indicates successful processing

5. **Consumer Lag**
   - Should be < 100 under normal load
   - Yellow/Red if > 100/1000 messages

6. **Web Requests/sec**
   - May be 0 if no one is accessing the web UI
   - Will show activity when browsing sensor data

#### 📊 End-to-End Pipeline Flow Graph
- **Verify:** All 4 lines (Polls, Published, Consumed, Processed) should be visible
- **Expected:** Lines should track closely together
- **Concern:** If lines diverge significantly, investigate bottleneck

#### ⏱️ Latency Graphs
**Gateway.Poller - HTTP Poll Latency:**
- P95 should be < 2000ms
- P50 (median) typically 200-800ms depending on API

**Ingest.Consumer - Processing & DB Latency:**
- P95 processing should be < 500ms
- P95 DB write should be < 200ms

#### ⚠️ Application Error Rates
- All lines should be at or near 0
- Spikes indicate issues requiring investigation

#### 🎯 Gateway.Poller Success Rate
- Should be > 99% (green gauge)
- Yellow < 99%, Red < 90%

### Test Interactivity
1. **Change Time Range** (top-right)
   - Try "Last 5 minutes", "Last 1 hour"
   - Data should update accordingly

2. **Hover Over Graphs**
   - Tooltips should show multiple metrics
   - Values should be reasonable (not NaN or Infinity)

3. **Check Auto-Refresh**
   - Dashboard refreshes every 10 seconds
   - Watch metrics update in real-time

---

## 🔌 Step 4: Test Gateway.Poller Dashboard

### Access Dashboard
```
http://localhost:3000/d/gateway-poller
```
or search for: **"Gateway.Poller - Sensor Data Collection"**

### Key Verifications

#### 🔌 Gateway.Poller Health Row
1. **Poll Rate** - Should be > 0
2. **Publish Rate** - Should match poll rate
3. **Success Rate** - Should be > 95%
4. **Failure Rate** - Should be near 0

#### 📊 Poll & Publish Rate Graph
- Two lines tracking together
- "Polls/sec" and "Published/sec"

#### 📊 Poll Success vs Failures
- Green "Successful" line should be dominant
- Red "Failed" line should be minimal or zero

#### ⏱️ HTTP Poll Latency (Percentiles)
- Shows P99, P95, P50 latencies
- Verify P95 < 2 seconds

#### ⏱️ RabbitMQ Publish Latency
- Should be very low (< 50ms typically)
- Spikes indicate RabbitMQ issues

#### 📡 Sensor Statistics Table
- Lists all configured sensors
- Shows total polls, events published, failed polls
- Verify your sensors appear here

#### 📋 Gateway.Poller Logs
- Recent logs from the service
- Filter for errors: `|= "error"` or `|= "ERROR"`

---

## 💾 Step 5: Test Ingest.Consumer Dashboard

### Access Dashboard
```
http://localhost:3000/d/ingest-consumer
```
or search for: **"Ingest.Consumer - Event Processing"**

### Key Verifications

#### 💾 Ingest.Consumer Health Row
1. **Consume Rate** - Should be > 0
2. **Process Rate** - Should match consume rate
3. **Consumer Lag** - Should be < 100
4. **Failure Rate** - Should be near 0

#### 📊 Event Consumption & Processing
- Two lines: "Consumed" and "Processed"
- Should track very closely (nearly overlapping)

#### 📊 Consumer Lag Over Time
- Should remain relatively flat and low
- Spikes indicate temporary backlogs

#### 📊 Event Failures & Duplicates
- "Failed" line should stay at 0
- "Duplicates" may show occasional events (this is normal)

#### ⏱️ Event Processing Latency
- P95 should be < 500ms
- Shows total processing time

#### ⏱️ Database Write Latency
- P95 should be < 200ms
- Indicates DB performance

#### 📋 Total Event Counters (Bar Gauge)
- Shows cumulative counts
- "Processed" should be largest/equal to "Consumed"
- "Failed" should be minimal

#### 📋 Ingest.Consumer Logs
- Recent processing logs
- Check for validation errors or DB issues

---

## 🌐 Step 6: Test Web.Mvc Dashboard

### Access Dashboard
```
http://localhost:3000/d/web-mvc-http
```
or search for: **"Web MVC - HTTP Metrics"**

### Key Verifications

#### Web MVC Health Row
1. **Requests/sec** 
   - May be 0 if no one is using the web UI
   - Generate traffic by browsing: http://localhost:5001

2. **5xx Errors/sec**
   - Should be 0
   - Non-zero indicates application errors

3. **P95 Request Duration**
   - Should be < 1 second for most requests

4. **In-Flight Requests**
   - Current active requests
   - Typically 0-5

#### Generate Test Traffic
```powershell
# Open web UI in browser
Start-Process "http://localhost:5001"

# Or use curl to generate requests
for ($i=1; $i -le 10; $i++) { 
    Invoke-WebRequest -Uri "http://localhost:5001" -UseBasicParsing
    Start-Sleep -Milliseconds 500
}
```

After generating traffic, verify:
- Requests/sec metric increases
- Status code breakdown shows 200 responses

---

## 🧪 Step 7: Manual PromQL Testing

### Test Queries in Prometheus

1. **Open Prometheus**
   ```
   http://localhost:9091/graph
   ```

2. **Test Each Metric Type**

#### Gateway.Poller Metrics
```promql
# Should return current poll rate
rate(tk_polls_total[1m])

# Should return values for all sensors
tk_polls_total

# Should return latency data
histogram_quantile(0.95, sum(rate(tk_poll_duration_ms_bucket[5m])) by (le))
```

#### Ingest.Consumer Metrics
```promql
# Should return consume rate
rate(tk_events_consumed_total[1m])

# Should return lag value
tk_consumer_lag

# Should return processing latency
histogram_quantile(0.95, sum(rate(tk_event_processing_duration_ms_bucket[5m])) by (le))
```

#### Verify Metric Labels
```promql
# Check all tk_* metrics have proper labels
{__name__=~"tk_.*"}
```

Expected result: Should show multiple time series

---

## 🔥 Step 8: Stress Testing

### Generate High Load

```powershell
# Add more sensors to gateway-poller config (optional)
# Restart services to apply
docker restart gateway-poller

# Monitor dashboards during restart
# Verify metrics recover after restart completes
```

### Monitor During Load

1. **Watch Consumer Lag**
   - Should remain < 100 even under load
   - If it grows continuously, consumer can't keep up

2. **Check Latency Percentiles**
   - Should remain within acceptable ranges
   - Temporary spikes during load are normal

3. **Verify Auto-scaling** (if configured)
   - Consumer should process backlog efficiently

---

## 🐛 Troubleshooting Common Issues

### No Data in Dashboards

**Problem:** All panels show "No data"

**Solutions:**
1. Check Prometheus targets are UP
2. Verify services are exposing /metrics endpoints:
   ```powershell
   # Test each endpoint
   Invoke-WebRequest http://localhost:9090/metrics
   Invoke-WebRequest http://localhost:9092/metrics
   Invoke-WebRequest http://localhost:9094/metrics
   ```
3. Restart Prometheus:
   ```powershell
   docker restart prometheus
   ```

### Metrics Show Old Data

**Problem:** Dashboards show stale data

**Solutions:**
1. Check Prometheus scrape interval (should be 15s)
2. Verify time range in Grafana (top-right)
3. Force refresh dashboard (Ctrl+R or refresh button)

### "Histogram_quantile" Returns No Data

**Problem:** Latency panels are empty

**Solutions:**
1. Verify histogram buckets exist:
   ```promql
   tk_poll_duration_ms_bucket
   ```
2. Check sufficient data points exist (wait 2-3 minutes)
3. Try lower percentile (P50 instead of P99)

### Consumer Lag Growing Continuously

**Problem:** `tk_consumer_lag` steadily increasing

**Solutions:**
1. Check consumer logs for errors:
   ```powershell
   docker logs ingest-consumer --tail 100
   ```
2. Verify database connectivity
3. Check for processing errors:
   ```promql
   rate(tk_events_failed_total[1m])
   ```

### Poll Success Rate < 95%

**Problem:** Gateway.Poller failing frequently

**Solutions:**
1. Check external API availability
2. Review gateway logs:
   ```powershell
   docker logs gateway-poller --tail 100
   ```
3. Verify sensor configurations in appsettings.json

---

## ✅ Success Criteria Checklist

- [ ] All Prometheus targets show "UP" status
- [ ] Operational Monitoring dashboard loads without errors
- [ ] Application Health metrics show values > 0
- [ ] End-to-End Pipeline graph displays all 4 stages
- [ ] Gateway.Poller dashboard shows poll activity
- [ ] Ingest.Consumer dashboard shows processing activity
- [ ] Web.Mvc dashboard responds to generated traffic
- [ ] All latency graphs show percentile lines (P50, P95, P99)
- [ ] Consumer lag remains < 100 under normal load
- [ ] Poll success rate > 95%
- [ ] No continuous errors in service logs
- [ ] Dashboards auto-refresh every 10 seconds
- [ ] Time range selector works correctly
- [ ] Prometheus can query all `tk_*` metrics successfully

---

## 📸 Expected Visual Results

### Operational Monitoring - Healthy State
```
🎯 Application Health
[██████████] Poll Rate: 1.2/s      [██████████] Publish Rate: 1.2/s
[██████████] Consume Rate: 1.2/s   [██████████] Process Rate: 1.2/s
[██] Consumer Lag: 5                [█] Web Requests: 0.1/s

📊 Pipeline Flow: 4 smooth lines tracking together
⏱️ Latencies: P95 poll <2000ms, P95 processing <500ms
⚠️ Errors: Flat lines at 0
```

### Gateway.Poller - Healthy State
```
🔌 Health
[█████████] Poll Rate: 1.2/s       [█████████] Success Rate: 99.8%
[█████████] Publish Rate: 1.2/s    [█] Failure Rate: 0.002/s

📊 Poll graph shows steady throughput
⏱️ P95 latency 500-1500ms range
📡 All sensors listed with poll counts
```

### Ingest.Consumer - Healthy State
```
💾 Health
[█████████] Consume: 1.2/s         [█████████] Process: 1.2/s
[██] Lag: 12                       [█] Failures: 0/s

📊 Consume/Process lines overlapping
⏱️ P95 processing <200ms, P95 DB write <100ms
📋 Processed count > Failed count by large margin
```

---

## �️ Step 9: Test PostgreSQL DBA Dashboard

### Access Dashboard
```
http://localhost:3000/d/postgresql-dba
```
or search for: **"PostgreSQL - DBA & Performance"**

### Key Verifications

#### 🗄️ Database Health Overview Row
1. **Connection Usage**
   - Shows percentage of max_connections used
   - **Green:** < 70%, **Yellow:** 70-85%, **Red:** > 85%
   - Typical value: 5-20% for dev environment

2. **Cache Hit Ratio**
   - **Target:** > 95% (green)
   - **Yellow:** 90-95%, **Red:** < 90%
   - Low values indicate insufficient memory

3. **Transactions Per Second (TPS)**
   - Shows transaction throughput
   - **Green:** < 100, **Yellow:** 100-500, **Red:** > 500
   - Should correlate with event processing rate

4. **Database Uptime**
   - Time since PostgreSQL started
   - Format: Days/hours

5. **Active Locks**
   - Number of concurrent locks
   - **Green:** < 10, **Yellow:** 10-50, **Red:** > 50
   - Spikes may indicate lock contention

6. **Disk Usage**
   - Database size as % of available disk
   - **Green:** < 75%, **Yellow:** 75-90%, **Red:** > 90%

#### 📊 Connection & Activity Metrics

**Connections by State:**
- Shows active, idle, idle_in_transaction connections
- **Expected:** Most connections should be "idle" between requests
- **Alert:** Many "idle in transaction" = connection leaks

**Total Connections vs Max:**
- Line graph showing connection usage over time
- Red threshold line at max_connections limit
- Should never reach the max

#### 📊 Query Performance & Throughput

**Transaction Rate:**
- Commits/sec (green) vs Rollbacks/sec (red)
- **Healthy:** Rollbacks should be << Commits
- Spike in rollbacks may indicate application errors

**Database Operations:**
- Stacked graph: Inserts, Updates, Deletes, Fetches
- Inserts should be highest for telemetry ingestion workload
- Pattern should match event processing rate

#### 📊 Cache & Memory Efficiency

**Buffer Cache Hit Ratio:**
- Should stay > 95%
- Dips indicate data not in cache (cold start or memory pressure)

**Block Operations:**
- "Blocks Hit" should be much higher than "Blocks Read"
- Large "Blocks Read" spikes = cache misses

#### 📊 Disk I/O & Storage

**Database Size Over Time:**
- Shows growth trend
- Use for capacity planning

**Deadlocks & Conflicts:**
- Should be zero or near-zero
- Any deadlock requires application investigation

#### 📊 Maintenance & Vacuum Operations

**Vacuum Activity:**
- Shows autovacuum and manual vacuum operations
- Should see regular autovacuum activity
- Lack of vacuum = bloat accumulation

**Table Bloat (Dead vs Live Tuples):**
- Shows ratio of dead to live tuples
- High dead tuples = tables need vacuuming
- **Alert:** > 10% dead tuples

#### 📊 Backup & WAL Management

**WAL Generation Rate:**
- Write-Ahead Log records per second
- Indicates write intensity
- Use for replication lag estimation

#### 📋 Top Tables by Size & Activity

**Table Statistics:**
- Lists largest tables
- Shows row counts and sizes
- Use for schema optimization

### Test Scenarios

#### Scenario 1: Normal Operation
```powershell
# Generate activity by consuming events
# Watch dashboard update in real-time
```

**Expected Results:**
- Connection usage: 5-20%
- Cache hit ratio: > 95%
- TPS: 1-10 transactions/sec
- Locks: < 10
- Deadlocks: 0

#### Scenario 2: High Load
```powershell
# Restart ingest-consumer to process backlog
docker restart ingest-consumer

# Watch metrics spike
```

**Expected Changes:**
- TPS increases
- Connections increase temporarily
- Locks may spike briefly
- Cache hit ratio should remain > 95%

#### Scenario 3: Connection Pool Monitoring
```powershell
# Open multiple connections (simulate leak)
# Monitor "Connections by State" panel
```

**Alert Conditions:**
- Many "idle in transaction" connections
- Connection usage > 85%

---

## 💻 Step 10: Test OS Performance Monitor Dashboard

### Access Dashboard
```
http://localhost:3000/d/postgresql-host-os
```
or search for: **"PostgreSQL Host - OS Performance"**

### Key Verifications

#### 💻 System Health Overview Row
1. **CPU Usage**
   - Total CPU utilization percentage
   - **Green:** < 70%, **Yellow:** 70-90%, **Red:** > 90%
   - Dev environment typically: 5-20%

2. **Memory Usage**
   - RAM utilization percentage
   - **Green:** < 80%, **Yellow:** 80-95%, **Red:** > 95%
   - PostgreSQL will use available memory for cache

3. **Disk Usage**
   - Root filesystem utilization
   - **Green:** < 75%, **Yellow:** 75-90%, **Red:** > 90%

4. **Load Average**
   - 1-minute system load
   - **Green:** < 2, **Yellow:** 2-4, **Red:** > 4
   - Compare to number of CPUs

#### 📊 CPU Performance Metrics

**CPU Usage by Mode:**
- **User:** Application CPU time (expect 5-15%)
- **System:** Kernel CPU time (expect < 5%)
- **I/O Wait:** Waiting for disk (expect < 10%, **alert if > 20%**)
- **Idle:** Unused CPU (expect > 70%)

**System Load Average:**
- Shows 1, 5, 15 minute load averages
- Trending upward = increasing system load
- Load > number of CPUs = potential bottleneck

#### 📊 Memory Performance

**Memory Breakdown:**
- **Used:** Active memory (PostgreSQL, apps)
- **Buffers:** File system metadata cache
- **Cached:** File system data cache  
- **Available:** Free for new processes

**Expected Pattern:**
- PostgreSQL uses most available RAM for cache
- "Cached" should be large (good for DB performance)
- "Available" should stay > 20% for safety

**Swap Usage:**
- Should be zero or very low
- **Alert:** Any swap usage indicates memory pressure
- PostgreSQL performance degrades with swap

#### 📊 Disk I/O Performance

**Disk IOPS:**
- Read operations/sec and Write operations/sec
- PostgreSQL workload: Writes > Reads (WAL, data files)
- Spikes during vacuum or large inserts

**Disk Throughput:**
- MB/s for reads and writes
- Correlates with database activity
- Sustained high throughput = heavy I/O load

**Disk I/O Latency:**
- Average read/write latency in milliseconds
- **Green:** < 10ms, **Yellow:** 10-50ms, **Red:** > 50ms
- High latency = disk bottleneck (affects DB performance)

**Disk Space Usage:**
- Shows used vs available disk space
- Should match "Database Size" from DBA dashboard

#### 📊 Network Performance

**Network Traffic:**
- Receive/Transmit throughput in MB/s
- PostgreSQL network I/O (client connections, replication)
- Spikes during high query activity

**Network Errors & Drops:**
- Should always be zero
- Non-zero indicates network hardware issues

#### 📊 System Resources & Indicators

**Context Switches & Interrupts:**
- **Normal:** 1000-10000 switches/sec
- **High:** > 100000/sec = performance issue
- Indicates high concurrency or I/O activity

**File Descriptors Usage:**
- Open file handles (DB connections, data files)
- **Alert:** > 80% of maximum
- PostgreSQL uses many file descriptors

### Test Scenarios

#### Scenario 1: Baseline Monitoring (Idle)
**Expected Values:**
- CPU: 5-10% (mostly idle)
- Memory: 40-60% (PostgreSQL cache)
- Disk I/O: Very low (< 10 IOPS)
- Swap: 0%
- Load Average < 1

#### Scenario 2: Active Database Load
```powershell
# Run ingest-consumer to process events
# Generate database writes
```

**Expected Changes:**
- CPU +5-10% (system mode)
- Disk writes increase (WAL activity)
- I/O wait may increase slightly
- Network transmit +1-5 MB/s
- Load average increases to 1-2

#### Scenario 3: Disk I/O Stress
```powershell
# Force database vacuum
docker exec tk-postgres psql -U postgres -d telemetry_kitchen -c "VACUUM FULL VERBOSE;"

# Watch disk metrics
```

**Expected Changes:**
- Disk IOPS spike significantly
- Disk latency may increase
- CPU I/O wait increases
- System load increases

#### Scenario 4: Memory Pressure Detection
**Check for Swap Usage:**
```promql
node_memory_SwapTotal_bytes - node_memory_SwapFree_bytes
```

**Alert Conditions:**
- Swap used > 0 (indicates not enough RAM)
- Memory available < 10% (OOM risk)

---

## 📈 Step 11: Test Capacity Planning Dashboard

### Access Dashboard
```
http://localhost:3000/d/postgresql-capacity
```
or search for: **"PostgreSQL - Capacity Planning & Trends"**

**Note:** This dashboard shows 30-day trends. In a new environment, some panels may have limited data.

### Key Verifications

#### 📊 Resource Headroom Overview Row
1. **Connection Headroom**
   - Remaining connection capacity (100% - usage%)
   - **Green:** > 40%, **Yellow:** 20-40%, **Red:** < 20%
   - Use for connection pool sizing decisions

2. **CPU Headroom**
   - Remaining CPU capacity
   - **Green:** > 40% available
   - Lower headroom = consider scaling up

3. **Memory Headroom**
   - Remaining RAM capacity
   - **Green:** > 20% available
   - PostgreSQL benefits from more RAM

4. **Disk Headroom**
   - Remaining disk space
   - **Green:** > 40% available
   - Plan upgrades if < 25%

#### 📈 Database Growth Trends (30 Days)

**Database Size Growth:**
- Line graph showing total database size over 30 days
- Use for forecasting future storage needs
- **New Environment:** May show short-term spike as data populates

**Database Growth Rate:**
- Shows bytes/day growth rate
- Helps estimate monthly storage consumption
- Calculate: GB/month = (growth rate * 30) / 1024^3

**Days Until 90% Disk Full:**
- Forecast gauge using linear regression
- **Green:** > 90 days, **Yellow:** 30-90 days, **Red:** < 30 days
- **New Environment:** May show inaccurate values initially

#### 🔌 Connection Trends

**Peak Connections Trend:**
- Shows maximum daily connection usage over 30 days
- Identifies growth in database clients
- Use for max_connections tuning

**Connection Utilization %:**
- Trend of connection pool usage
- Increasing trend = add more connections or connection pooling

#### 🖥️ CPU & Memory Trends

**CPU Peak Usage Trend:**
- Daily maximum CPU usage over 30 days
- Identifies if you're approaching CPU limits
- Steady increase = plan for CPU upgrade

**Memory Peak Usage Trend:**
- Daily maximum memory usage over 30 days
- PostgreSQL typically uses all available RAM (good)
- Approaching 95% with swap usage = add RAM

#### ⚡ Transaction Load Trends

**Transaction Rate Trend:**
- Shows average and peak TPS over 30 days
- Identifies traffic patterns and growth
- Use for scaling decisions

**Database Operations Trend:**
- Breakdown of inserts, updates, deletes over time
- Telemetry workload = mostly inserts
- Pattern changes may indicate schema changes

#### 🔍 Performance Degradation Detection

**Cache Hit Ratio Trend:**
- Should remain stable above 95%
- Declining trend = insufficient memory for working set
- Sudden drops = cold start or schema changes

**Disk I/O Latency Trend:**
- Monitors if disk performance is degrading
- Increasing trend = disk aging or saturation
- Spikes correlate with high I/O operations

### Test Scenarios

#### Scenario 1: Fresh Installation (< 7 Days Data)
**Expected:**
- Some panels may show "Insufficient data"
- Growth forecasts may be inaccurate
- Trends need 7-30 days to stabilize

**Action:** Revisit dashboard after 7 days of operation

#### Scenario 2: Capacity Planning Review (30+ Days Data)
**Questions to Answer:**
1. **When will disk be full?**
   - Check "Days Until 90% Disk Full" gauge
   - Verify against database growth graph

2. **Do we need more connections?**
   - Check peak connection trend
   - If approaching 70% regularly, increase pool

3. **Is CPU sufficient?**
   - Check CPU peak usage trend
   - If regularly > 70%, consider upgrade

4. **Is memory adequate?**
   - Check cache hit ratio (should stay > 95%)
   - Check memory peak usage (swap = 0)

5. **Is performance degrading?**
   - Check disk latency trend (should be flat)
   - Check cache hit ratio (should be stable)

#### Scenario 3: Growth Forecasting
```promql
# Calculate monthly data growth
(rate(pg_database_size_bytes{datname="telemetry_kitchen"}[30d]) * 86400 * 30) / 1024 / 1024 / 1024
```

**Use Cases:**
- Budget planning for storage upgrades
- When to archive old data
- Scaling timeline decisions

---

## 🧪 Step 12: Validate PostgreSQL Metrics in Prometheus

### Access Prometheus
```
http://localhost:9091/graph
```

### Test PostgreSQL Exporter Queries

#### Connection Metrics
```promql
# Total connections
sum(pg_stat_activity_count{datname="telemetry_kitchen"})

# Connections by state
pg_stat_activity_count{datname="telemetry_kitchen"}

# Max connections
pg_settings_max_connections
```

**Expected:** Values > 0, multiple states (active, idle)

#### Performance Metrics
```promql
# Transaction rate
rate(pg_stat_database_xact_commit{datname="telemetry_kitchen"}[5m])

# Cache hit ratio
100 * (sum(rate(pg_stat_database_blks_hit{datname="telemetry_kitchen"}[5m])) / 
       (sum(rate(pg_stat_database_blks_hit{datname="telemetry_kitchen"}[5m])) + 
        sum(rate(pg_stat_database_blks_read{datname="telemetry_kitchen"}[5m]))))
```

**Expected:** TPS > 0 during activity, cache ratio > 90%

#### Database Size
```promql
# Size in bytes
pg_database_size_bytes{datname="telemetry_kitchen"}

# Size in GB
pg_database_size_bytes{datname="telemetry_kitchen"} / 1024 / 1024 / 1024
```

**Expected:** Growing value as data is ingested

### Test Node Exporter Queries

#### CPU Metrics
```promql
# CPU usage percentage
100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# I/O wait percentage
avg(rate(node_cpu_seconds_total{mode="iowait"}[5m])) * 100

# Load average
node_load1
node_load5
node_load15
```

**Expected:** All queries return values

#### Memory Metrics
```promql
# Total memory
node_memory_MemTotal_bytes

# Available memory
node_memory_MemAvailable_bytes

# Memory usage percentage
100 * (1 - ((node_memory_MemAvailable_bytes or node_memory_MemFree_bytes) / node_memory_MemTotal_bytes))

# Swap usage
node_memory_SwapTotal_bytes - node_memory_SwapFree_bytes
```

**Expected:** Values present, swap usage = 0

#### Disk Metrics
```promql
# Disk IOPS
rate(node_disk_reads_completed_total[5m])
rate(node_disk_writes_completed_total[5m])

# Disk latency
rate(node_disk_read_time_seconds_total[5m]) / rate(node_disk_reads_completed_total[5m]) * 1000

# Disk usage
100 * (1 - (node_filesystem_avail_bytes{mountpoint="/",fstype!="rootfs"} / 
            node_filesystem_size_bytes{mountpoint="/",fstype!="rootfs"}))
```

**Expected:** All metrics available

---

## 🐛 Troubleshooting PostgreSQL Dashboards

### Problem: PostgreSQL Panels Show "No Data"

**Possible Causes:**
1. PostgreSQL exporter not running
2. Prometheus not scraping postgres-exporter
3. Wrong database name in queries

**Solutions:**

```powershell
# Check if postgres-exporter is running
docker ps | Select-String postgres-exporter

# Check postgres-exporter logs
docker logs postgres-exporter

# Verify exporter is exposing metrics
Invoke-WebRequest http://localhost:9187/metrics

# Check Prometheus target
# Visit: http://localhost:9091/targets
# Look for "postgres-exporter" - should be UP
```

**Expected postgres-exporter metrics:**
```
pg_database_size_bytes
pg_stat_activity_count
pg_stat_database_xact_commit
```

### Problem: OS/Node Panels Show "No Data"

**Possible Causes:**
1. Node exporter not running
2. Prometheus not scraping node-exporter

**Solutions:**

```powershell
# Check if node-exporter is running
docker ps | Select-String node-exporter

# Check node-exporter logs
docker logs node-exporter

# Verify exporter is exposing metrics
Invoke-WebRequest http://localhost:9100/metrics

# Check Prometheus target
# Visit: http://localhost:9091/targets
# Look for "node-exporter" - should be UP
```

**Expected node-exporter metrics:**
```
node_cpu_seconds_total
node_memory_MemTotal_bytes
node_disk_reads_completed_total
node_network_receive_bytes_total
```

### Problem: Cache Hit Ratio Shows 0% or NaN

**Cause:** No database activity (no reads or writes)

**Solution:**
```powershell
# Generate database activity
docker restart ingest-consumer

# Wait 1-2 minutes for metrics to populate
```

### Problem: Capacity Planning Shows "Insufficient Data"

**Cause:** Dashboard uses 30-day windows, not enough historical data

**Solution:**
- Normal for new installations
- Panels will populate after 7-30 days
- Use shorter time ranges temporarily:
  ```promql
  # Change [30d] to [7d] in queries
  ```

### Problem: Disk Forecast Shows Negative Days

**Cause:** Database shrinking or deleted data

**Solution:**
- Normal after VACUUM FULL or data cleanup
- Forecast assumes linear growth
- Ignore forecasts after major cleanup operations

---

## ✅ PostgreSQL Dashboard Success Checklist

### PostgreSQL DBA Dashboard
- [ ] All 6 health gauges show values (not NaN)
- [ ] Connection usage < 70% (green)
- [ ] Cache hit ratio > 95% (green)
- [ ] TPS > 0 when system is active
- [ ] Lock count is reasonable (< 10)
- [ ] Transaction graph shows commit activity
- [ ] Database size is displayed
- [ ] Table statistics panel populates

### OS Performance Monitor Dashboard
- [ ] All 4 system health gauges show values
- [ ] CPU usage < 70% (green)
- [ ] Memory usage reasonable (40-80%)
- [ ] Disk usage < 75% (green)
- [ ] Load average < 2 in idle state
- [ ] Swap usage = 0
- [ ] Disk I/O latency < 10ms (green)
- [ ] Network metrics show data (not NaN)
- [ ] File descriptors < 80% of max

### Capacity Planning Dashboard
- [ ] Headroom gauges show percentages
- [ ] Database size growth graph displays
- [ ] Growth rate calculated (may be small initially)
- [ ] Connection trends visible
- [ ] CPU/Memory peak usage trends show data
- [ ] Transaction rate trends display
- [ ] Cache hit ratio trend > 95%
- [ ] (Optional) Forecast panels show values (requires 7+ days data)

### Integration Checks
- [ ] PostgreSQL exporter target UP in Prometheus
- [ ] Node exporter target UP in Prometheus
- [ ] All pg_* metrics queryable in Prometheus
- [ ] All node_* metrics queryable in Prometheus
- [ ] Database activity correlates with app metrics
- [ ] OS metrics correlate with database load
- [ ] Dashboards refresh every 5-10 seconds automatically

---

## �🔗 Next Steps

After verification:
1. Configure alerting rules (see PROMQL-QUERIES.md)
2. Set up notification channels (Slack, email)
3. Create custom dashboards for specific use cases
4. Export dashboards as JSON for version control
5. Document any custom threshold adjustments

---

## 📚 Related Documentation

- [PromQL Query Reference](./PROMQL-QUERIES.md)
- [Operators Manual](./OPERATORS-MANUAL.md)
- [Grafana Setup Guide](./dashboards/grafana/README.md)
