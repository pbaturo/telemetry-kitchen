# Telemetry Kitchen - PromQL Query Reference

## Overview
This document provides a comprehensive reference of all PromQL queries used in Grafana dashboards for the Telemetry Kitchen system. These queries monitor the complete telemetry pipeline from data collection through storage.

---

## 🎯 Application Metrics (Custom `tk_*` Metrics)

### Gateway.Poller Metrics

#### Poll Rate
```promql
rate(tk_polls_total[1m])
```
**Purpose:** Station polls per second  
**Use Case:** Monitor polling activity, detect service downtime  
**Expected Value:** > 0 when service is active  

#### Poll Success Rate
```promql
100 * (1 - (rate(tk_polls_failed_total[5m]) / rate(tk_polls_total[5m])))
```
**Purpose:** Percentage of successful polls  
**Use Case:** Service health indicator  
**Threshold:** Alert if < 95%  

#### Poll Failure Rate
```promql
rate(tk_polls_failed_total[1m])
```
**Purpose:** Failed polling attempts per second  
**Use Case:** Detect API failures, network issues  
**Threshold:** Alert if > 0.1/sec  

#### Events Published Rate
```promql
rate(tk_events_published_total[1m])
```
**Purpose:** Events published to RabbitMQ per second  
**Use Case:** Monitor data flow from gateway  
**Expected Value:** Should match successful polls  

#### Poll Latency (P95)
```promql
histogram_quantile(0.95, sum(rate(tk_poll_duration_ms_bucket[5m])) by (le))
```
**Purpose:** 95th percentile HTTP poll latency  
**Use Case:** Monitor external API performance  
**Threshold:** Alert if > 2000ms  

#### Poll Latency (P50)
```promql
histogram_quantile(0.50, sum(rate(tk_poll_duration_ms_bucket[5m])) by (le))
```
**Purpose:** Median HTTP poll latency  
**Use Case:** Typical response time baseline  

#### Publish Latency (P95)
```promql
histogram_quantile(0.95, sum(rate(tk_publish_duration_ms_bucket[5m])) by (le))
```
**Purpose:** 95th percentile RabbitMQ publish latency  
**Use Case:** Monitor message queue performance  
**Threshold:** Alert if > 100ms  

#### Last Successful Poll Time
```promql
tk_last_success_unixtime
```
**Purpose:** Unix timestamp of last successful poll per sensor  
**Use Case:** Detect stale sensors, individual sensor health  
**Alert:** If `time() - tk_last_success_unixtime > 600` (10 minutes)  

---

### Ingest.Consumer Metrics

#### Event Consume Rate
```promql
rate(tk_events_consumed_total[1m])
```
**Purpose:** Events consumed from RabbitMQ per second  
**Use Case:** Monitor consumer throughput  
**Expected Value:** Should match publish rate  

#### Event Process Rate
```promql
rate(tk_events_processed_total[1m])
```
**Purpose:** Successfully processed events per second  
**Use Case:** Monitor processing success  
**Expected Value:** Should match consume rate  

#### Processing Failure Rate
```promql
rate(tk_events_failed_total[1m])
```
**Purpose:** Failed event processing per second  
**Use Case:** Detect data validation or DB issues  
**Threshold:** Alert if > 0.1/sec  

#### Duplicate Event Rate
```promql
rate(tk_duplicate_events_total[1m])
```
**Purpose:** Duplicate events detected per second  
**Use Case:** Monitor idempotency effectiveness  
**Info:** High values may indicate redeliveries  

#### Consumer Lag
```promql
tk_consumer_lag
```
**Purpose:** Number of messages waiting in queue  
**Use Case:** Detect processing bottleneck  
**Threshold:** Alert if > 1000  

#### Processing Latency (P95)
```promql
histogram_quantile(0.95, sum(rate(tk_event_processing_duration_ms_bucket[5m])) by (le))
```
**Purpose:** 95th percentile event processing time  
**Use Case:** Monitor consumer performance  
**Threshold:** Alert if > 500ms  

#### DB Write Latency (P95)
```promql
histogram_quantile(0.95, sum(rate(tk_db_write_duration_ms_bucket[5m])) by (le))
```
**Purpose:** 95th percentile database write time  
**Use Case:** Monitor database performance  
**Threshold:** Alert if > 200ms  

---

### End-to-End Pipeline Monitoring

#### Complete Data Flow
```promql
# 1. Polls attempted
rate(tk_polls_total[1m])

# 2. Events published
rate(tk_events_published_total[1m])

# 3. Events consumed
rate(tk_events_consumed_total[1m])

# 4. Events processed
rate(tk_events_processed_total[1m])
```
**Purpose:** Track data through entire pipeline  
**Use Case:** Identify bottlenecks, data loss  
**Expected:** All rates should be approximately equal  

#### Overall System Health Score
```promql
min(
  100 * (rate(tk_events_processed_total[5m]) / rate(tk_events_published_total[5m])),
  100 * (1 - (rate(tk_polls_failed_total[5m]) / rate(tk_polls_total[5m])))
)
```
**Purpose:** Combined health metric (0-100)  
**Use Case:** Single system health indicator  
**Threshold:** Alert if < 95  

---

## 🚦 Infrastructure Metrics

### PostgreSQL

#### Transaction Rate
```promql
rate(pg_stat_database_xact_commit{datname="telemetry_kitchen"}[1m])
```
**Purpose:** Database commits per second  
**Use Case:** Monitor DB write activity  

#### Active Connections
```promql
pg_stat_activity_count{datname="telemetry_kitchen",state="active"}
```
**Purpose:** Number of active DB connections  
**Threshold:** Alert if > 50  

#### Cache Hit Ratio
```promql
100 * (
  pg_stat_database_blks_hit{datname="telemetry_kitchen"} / 
  (pg_stat_database_blks_hit{datname="telemetry_kitchen"} + 
   pg_stat_database_blks_read{datname="telemetry_kitchen"})
)
```
**Purpose:** PostgreSQL cache efficiency  
**Threshold:** Alert if < 95%  

---

### RabbitMQ

#### Total Queue Depth
```promql
sum(rabbitmq_queue_messages{queue=~".*"})
```
**Purpose:** Total messages across all queues  
**Threshold:** Alert if > 5000  

#### Message Publish Rate
```promql
sum(rate(rabbitmq_channel_messages_published_total[1m]))
```
**Purpose:** Messages published per second  

#### Message Delivery Rate
```promql
sum(rate(rabbitmq_channel_messages_delivered_total[1m]))
```
**Purpose:** Messages delivered per second  

#### Queue Depth by Queue
```promql
rabbitmq_queue_messages{queue=~".*"}
```
**Purpose:** Messages per queue  
**Use Case:** Identify specific queue backlogs  

---

### Web.Mvc

#### Request Rate
```promql
sum(rate(http_requests_received_total{job="web-mvc"}[1m]))
```
**Purpose:** HTTP requests per second  

#### 5xx Error Rate
```promql
sum(rate(http_requests_received_total{job="web-mvc",code=~"5.."}[5m]))
```
**Purpose:** Server errors per second  
**Threshold:** Alert if > 1/sec  

#### Request Latency (P95)
```promql
histogram_quantile(0.95, sum by (le) (rate(http_request_duration_seconds_bucket{job="web-mvc"}[5m])))
```
**Purpose:** 95th percentile request duration  
**Threshold:** Alert if > 1s  

---

## 🔍 Advanced Diagnostic Queries

### Find Stale Sensors
```promql
(time() - tk_last_success_unixtime) > 600
```
**Purpose:** Sensors not updated in 10+ minutes  
**Use Case:** Identify broken sensor configurations  

### Calculate Processing Efficiency
```promql
100 * (
  rate(tk_events_processed_total[5m]) / 
  rate(tk_events_consumed_total[5m])
)
```
**Purpose:** Percentage of consumed events successfully processed  
**Expected:** Should be near 100%  

### Consumer Throughput Capacity
```promql
rate(tk_events_processed_total[1m]) / tk_consumer_lag
```
**Purpose:** Time to clear current backlog (inverse)  
**Use Case:** Estimate catch-up time  

### Average End-to-End Latency
```promql
histogram_quantile(0.50, sum(rate(tk_poll_duration_ms_bucket[5m])) by (le)) +
histogram_quantile(0.50, sum(rate(tk_publish_duration_ms_bucket[5m])) by (le)) +
histogram_quantile(0.50, sum(rate(tk_event_processing_duration_ms_bucket[5m])) by (le)) +
histogram_quantile(0.50, sum(rate(tk_db_write_duration_ms_bucket[5m])) by (le))
```
**Purpose:** Median total time from poll to database  
**Use Case:** End-to-end performance baseline  

---

## 📊 Dashboard-Specific Queries

### Operational Monitoring Dashboard

**Application Health Row:**
- Poll Rate: `rate(tk_polls_total[1m])`
- Publish Rate: `rate(tk_events_published_total[1m])`
- Consume Rate: `rate(tk_events_consumed_total[1m])`
- Process Rate: `rate(tk_events_processed_total[1m])`
- Consumer Lag: `tk_consumer_lag`

**Pipeline Flow Graph:**
```promql
rate(tk_polls_total[1m])           # Step 1
rate(tk_events_published_total[1m]) # Step 2
rate(tk_events_consumed_total[1m])  # Step 3
rate(tk_events_processed_total[1m]) # Step 4
```

**Error Rates:**
```promql
rate(tk_polls_failed_total[1m])      # Poll failures
rate(tk_events_failed_total[1m])     # Processing failures
rate(tk_duplicate_events_total[1m])  # Duplicates
```

### Gateway.Poller Dashboard

**Throughput:**
```promql
rate(tk_polls_total[1m])
rate(tk_events_published_total[1m])
```

**Success vs Failures:**
```promql
rate(tk_polls_total[1m]) - rate(tk_polls_failed_total[1m])  # Successful
rate(tk_polls_failed_total[1m])                              # Failed
```

**Latency Percentiles:**
```promql
histogram_quantile(0.99, sum(rate(tk_poll_duration_ms_bucket[5m])) by (le))
histogram_quantile(0.95, sum(rate(tk_poll_duration_ms_bucket[5m])) by (le))
histogram_quantile(0.50, sum(rate(tk_poll_duration_ms_bucket[5m])) by (le))
```

### Ingest.Consumer Dashboard

**Pipeline Flow:**
```promql
rate(tk_events_consumed_total[1m])   # Consumption
rate(tk_events_processed_total[1m])  # Processing
```

**Failures:**
```promql
rate(tk_events_failed_total[1m])      # Failed processing
rate(tk_duplicate_events_total[1m])   # Duplicates detected
```

**Latency:**
```promql
histogram_quantile(0.95, sum(rate(tk_event_processing_duration_ms_bucket[5m])) by (le))
histogram_quantile(0.95, sum(rate(tk_db_write_duration_ms_bucket[5m])) by (le))
```

---

## ⚡ Alert Rules (Recommended)

### Critical Alerts

```yaml
groups:
  - name: telemetry_kitchen_critical
    interval: 30s
    rules:
      - alert: GatewayPollerDown
        expr: rate(tk_polls_total[2m]) == 0
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Gateway.Poller has stopped polling"
          
      - alert: IngestConsumerDown
        expr: rate(tk_events_consumed_total[2m]) == 0
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Ingest.Consumer has stopped consuming"
          
      - alert: HighConsumerLag
        expr: tk_consumer_lag > 1000
        for: 10m
        labels:
          severity: critical
        annotations:
          summary: "Consumer lag exceeds 1000 messages"
          
      - alert: HighProcessingFailureRate
        expr: rate(tk_events_failed_total[5m]) > 1
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Event processing failure rate > 1/sec"
```

### Warning Alerts

```yaml
  - name: telemetry_kitchen_warning
    interval: 1m
    rules:
      - alert: LowPollSuccessRate
        expr: 100 * (1 - (rate(tk_polls_failed_total[5m]) / rate(tk_polls_total[5m]))) < 95
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Poll success rate below 95%"
          
      - alert: HighPollLatency
        expr: histogram_quantile(0.95, sum(rate(tk_poll_duration_ms_bucket[5m])) by (le)) > 2000
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "P95 poll latency exceeds 2 seconds"
          
      - alert: HighDBWriteLatency
        expr: histogram_quantile(0.95, sum(rate(tk_db_write_duration_ms_bucket[5m])) by (le)) > 200
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "P95 database write latency exceeds 200ms"
```

---

## 🧪 Testing & Validation Queries

### Verify Metrics Are Being Scraped

```promql
# Check if Gateway.Poller metrics exist
up{job="gateway-poller"}

# Check if Ingest.Consumer metrics exist
up{job="ingest-consumer"}

# Check if Web.Mvc metrics exist
up{job="web-mvc"}
```

### Validate Metric Cardinality

```promql
# Count unique tk_* metrics
count(count by (__name__) ({__name__=~"tk_.*"}))

# Should return approximately 11-12 metric types
```

### Check Data Freshness

```promql
# Time since last scrape for each service
time() - timestamp(up{job=~"gateway-poller|ingest-consumer|web-mvc"})

# Should be < 30 seconds
```

---

## 📖 PromQL Best Practices Used

1. **rate() for Counters**: Always use `rate()` over time windows for counter metrics
2. **histogram_quantile() for Latency**: Use histogram buckets for percentile calculations
3. **Appropriate Time Windows**: 
   - `[1m]` for real-time dashboards
   - `[5m]` for percentiles and alerts
4. **Label Filtering**: Use `{job="service-name"}` to isolate metrics
5. **Metric Naming**: Follow `tk_` prefix convention for custom app metrics

---

## 🔗 Related Documentation

- [Prometheus Operator's Manual](../../docs/OPERATORS-MANUAL.md)
- [Grafana Dashboard Setup](../../docs/dashboards/grafana/README.md)
- [Architecture Overview](../../docs/architecture/overview.md)
