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

## 🔗 Next Steps

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
