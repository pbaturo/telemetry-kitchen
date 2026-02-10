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

## 🗄️ PostgreSQL Database Metrics

### Connection Health

#### Total Connections
```promql
sum(pg_stat_activity_count{datname="telemetry_kitchen"})
```
**Purpose:** Current active connections to database  
**Use Case:** Monitor connection pool usage  
**Expected Value:** < 100 (default max_connections)  

#### Connection Usage Percentage
```promql
100 * (sum(pg_stat_activity_count{datname="telemetry_kitchen"}) / pg_settings_max_connections)
```
**Purpose:** Connection pool utilization  
**Threshold:** Alert if > 85%  
**Expected Value:** < 70% for healthy headroom  

#### Connections by State
```promql
pg_stat_activity_count{datname="telemetry_kitchen"}
```
**Purpose:** Breakdown of connections (active, idle, idle in transaction)  
**Use Case:** Identify connection leaks or long-running queries  
**Alert:** High `idle in transaction` indicates application issues  

#### Max Connections Limit
```promql
pg_settings_max_connections
```
**Purpose:** PostgreSQL max_connections setting  
**Use Case:** Capacity planning reference  

---

### Query Performance & Throughput

#### Transaction Rate (Commits)
```promql
rate(pg_stat_database_xact_commit{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Successful transactions per second  
**Use Case:** Monitor database write activity  

#### Transaction Rate (Rollbacks)
```promql
rate(pg_stat_database_xact_rollback{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Failed/rolled back transactions per second  
**Threshold:** Alert if rollback rate > 5% of commit rate  

#### Transactions Per Second (TPS)
```promql
rate(pg_stat_database_xact_commit{datname="telemetry_kitchen"}[5m]) + 
rate(pg_stat_database_xact_rollback{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Total transaction throughput  
**Use Case:** Database load indicator  

#### Tuple Operations (Inserts)
```promql
rate(pg_stat_database_tup_inserted{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Row inserts per second  
**Use Case:** Write load monitoring  

#### Tuple Operations (Updates)
```promql
rate(pg_stat_database_tup_updated{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Row updates per second  
**Use Case:** Update-heavy workload detection  

#### Tuple Operations (Deletes)
```promql
rate(pg_stat_database_tup_deleted{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Row deletes per second  
**Use Case:** Data retention/cleanup monitoring  

#### Tuple Operations (Fetches)
```promql
rate(pg_stat_database_tup_fetched{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Rows read per second  
**Use Case:** Read workload monitoring  

---

### Cache & Memory Efficiency

#### Buffer Cache Hit Ratio
```promql
100 * (sum(rate(pg_stat_database_blks_hit{datname="telemetry_kitchen"}[5m])) / 
       (sum(rate(pg_stat_database_blks_hit{datname="telemetry_kitchen"}[5m])) + 
        sum(rate(pg_stat_database_blks_read{datname="telemetry_kitchen"}[5m]))))
```
**Purpose:** Percentage of data served from cache vs disk  
**Threshold:** Alert if < 90% (target: > 95%)  
**Use Case:** Memory sizing validation  

#### Blocks Read from Disk
```promql
rate(pg_stat_database_blks_read{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Disk reads per second (cache misses)  
**Use Case:** Identify when working set exceeds RAM  

#### Blocks Hit in Cache
```promql
rate(pg_stat_database_blks_hit{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Cache hits per second  
**Use Case:** Validate cache effectiveness  

---

### Disk I/O & Storage

#### Database Size
```promql
pg_database_size_bytes{datname="telemetry_kitchen"}
```
**Purpose:** Total database disk usage  
**Use Case:** Capacity planning, growth tracking  

#### Database Size (Human Readable)
```promql
pg_database_size_bytes{datname="telemetry_kitchen"} / 1024 / 1024 / 1024
```
**Purpose:** Database size in GB  
**Unit:** GB  

#### Database Growth Rate
```promql
deriv(pg_database_size_bytes{datname="telemetry_kitchen"}[7d])
```
**Purpose:** Growth rate in bytes per day  
**Use Case:** Forecast disk usage, plan upgrades  

#### Deadlocks
```promql
rate(pg_stat_database_deadlocks{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Deadlock occurrences per second  
**Threshold:** Alert if > 0 (any deadlock is concerning)  
**Use Case:** Application logic issues, transaction design  

#### Conflicts
```promql
rate(pg_stat_database_conflicts{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Query conflicts per second (relevant for hot standby)  
**Use Case:** Replication health monitoring  

---

### Maintenance & Vacuum Operations

#### Autovacuum Operations
```promql
rate(pg_stat_database_tup_autovacuumed{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Tuples cleaned by autovacuum per second  
**Use Case:** Autovacuum activity verification  

#### Manual Vacuum Operations
```promql
rate(pg_stat_database_tup_vacuumed{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Tuples cleaned by manual vacuum per second  
**Use Case:** Maintenance operation tracking  

#### Analyze Operations
```promql
rate(pg_stat_database_tup_analyzed{datname="telemetry_kitchen"}[5m])
```
**Purpose:** Tuples analyzed per second  
**Use Case:** Statistics update frequency  

#### Dead Tuples Ratio
```promql
100 * (pg_stat_user_tables_n_dead_tup / 
       (pg_stat_user_tables_n_live_tup + pg_stat_user_tables_n_dead_tup))
```
**Purpose:** Percentage of dead tuples (bloat indicator)  
**Threshold:** Alert if > 10%  
**Use Case:** Table health, vacuum effectiveness  

---

### Backup & WAL Management

#### WAL Generation Rate
```promql
rate(pg_stat_wal_records_total[5m])
```
**Purpose:** Write-Ahead-Log records per second  
**Use Case:** Write activity baseline, replication lag prediction  

---

### Locks & Concurrency

#### Active Locks
```promql
sum(pg_locks_count{datname="telemetry_kitchen"})
```
**Purpose:** Total active database locks  
**Threshold:** Alert if > 50 locks  
**Use Case:** Concurrency bottleneck detection  

#### Lock Wait Time
```promql
pg_stat_activity_max_tx_duration{datname="telemetry_kitchen",state="active"}
```
**Purpose:** Longest running transaction duration  
**Threshold:** Alert if > 300 seconds  
**Use Case:** Long-running query detection  

---

## 💻 OS & Host Performance Metrics (Node Exporter)

### CPU Performance

#### CPU Usage by Mode
```promql
100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)
```
**Purpose:** Total CPU usage percentage  
**Threshold:** Alert if > 90%  

#### CPU User Time
```promql
avg(rate(node_cpu_seconds_total{mode="user"}[5m])) * 100
```
**Purpose:** CPU time spent in user space  
**Use Case:** Application CPU usage  

#### CPU System Time
```promql
avg(rate(node_cpu_seconds_total{mode="system"}[5m])) * 100
```
**Purpose:** CPU time spent in kernel  
**Use Case:** System call overhead, I/O wait  

#### CPU I/O Wait
```promql
avg(rate(node_cpu_seconds_total{mode="iowait"}[5m])) * 100
```
**Purpose:** CPU time waiting for disk I/O  
**Threshold:** Alert if > 20%  
**Use Case:** Disk bottleneck indicator  

#### System Load Average (1 min)
```promql
node_load1
```
**Purpose:** 1-minute load average  
**Threshold:** Alert if > number of CPU cores  

#### System Load Average (5 min)
```promql
node_load5
```
**Purpose:** 5-minute load average  
**Use Case:** Medium-term load trending  

#### System Load Average (15 min)
```promql
node_load15
```
**Purpose:** 15-minute load average  
**Use Case:** Long-term load baseline  

---

### Memory Performance

#### Total Memory
```promql
node_memory_MemTotal_bytes
```
**Purpose:** Total system RAM  
**Unit:** Bytes  

#### Memory Available
```promql
node_memory_MemAvailable_bytes
```
**Purpose:** Available memory for applications  
**Use Case:** OOM risk assessment  

#### Memory Usage Percentage
```promql
100 * (1 - ((node_memory_MemAvailable_bytes or node_memory_MemFree_bytes) / node_memory_MemTotal_bytes))
```
**Purpose:** Memory utilization  
**Threshold:** Alert if > 95%  

#### Memory Used
```promql
node_memory_MemTotal_bytes - (node_memory_MemAvailable_bytes or node_memory_MemFree_bytes)
```
**Purpose:** Actively used memory  
**Unit:** Bytes  

#### Buffers
```promql
node_memory_Buffers_bytes
```
**Purpose:** Memory used for file system buffers  
**Use Case:** Cache efficiency analysis  

#### Cached Memory
```promql
node_memory_Cached_bytes
```
**Purpose:** Memory used for page cache  
**Use Case:** File system cache effectiveness  

#### Swap Total
```promql
node_memory_SwapTotal_bytes
```
**Purpose:** Total swap space configured  

#### Swap Used
```promql
node_memory_SwapTotal_bytes - node_memory_SwapFree_bytes
```
**Purpose:** Active swap usage  
**Threshold:** Alert if swap used > 0 (indicates memory pressure)  

#### Swap Usage Percentage
```promql
100 * ((node_memory_SwapTotal_bytes - node_memory_SwapFree_bytes) / node_memory_SwapTotal_bytes)
```
**Purpose:** Swap utilization  
**Threshold:** Alert if > 10%  

---

### Disk I/O Performance

#### Disk IOPS (Reads)
```promql
rate(node_disk_reads_completed_total[5m])
```
**Purpose:** Read operations per second  
**Use Case:** Disk read load monitoring  

#### Disk IOPS (Writes)
```promql
rate(node_disk_writes_completed_total[5m])
```
**Purpose:** Write operations per second  
**Use Case:** Disk write load monitoring  

#### Disk Read Throughput
```promql
rate(node_disk_read_bytes_total[5m]) / 1024 / 1024
```
**Purpose:** Disk read MB/s  
**Unit:** MB/s  

#### Disk Write Throughput
```promql
rate(node_disk_written_bytes_total[5m]) / 1024 / 1024
```
**Purpose:** Disk write MB/s  
**Unit:** MB/s  

#### Disk Read Latency
```promql
rate(node_disk_read_time_seconds_total[5m]) / rate(node_disk_reads_completed_total[5m]) * 1000
```
**Purpose:** Average read latency in milliseconds  
**Threshold:** Alert if > 50ms  
**Use Case:** Disk performance degradation  

#### Disk Write Latency
```promql
rate(node_disk_write_time_seconds_total[5m]) / rate(node_disk_writes_completed_total[5m]) * 1000
```
**Purpose:** Average write latency in milliseconds  
**Threshold:** Alert if > 50ms  

#### Disk Space Used
```promql
node_filesystem_size_bytes{mountpoint="/",fstype!="rootfs"} - node_filesystem_avail_bytes{mountpoint="/",fstype!="rootfs"}
```
**Purpose:** Used disk space on root filesystem  
**Unit:** Bytes  

#### Disk Space Usage Percentage
```promql
100 * (1 - (node_filesystem_avail_bytes{mountpoint="/",fstype!="rootfs"} / node_filesystem_size_bytes{mountpoint="/",fstype!="rootfs"}))
```
**Purpose:** Disk utilization percentage  
**Threshold:** Alert if > 90%  

#### Days Until Disk Full (Forecast)
```promql
((node_filesystem_size_bytes{mountpoint="/",fstype!="rootfs"} * 0.9) - 
 (node_filesystem_size_bytes{mountpoint="/",fstype!="rootfs"} - node_filesystem_avail_bytes{mountpoint="/",fstype!="rootfs"})) / 
 (deriv(pg_database_size_bytes{datname="telemetry_kitchen"}[7d]) * 86400)
```
**Purpose:** Estimated days until disk reaches 90% full  
**Threshold:** Alert if < 30 days  
**Use Case:** Capacity planning  

---

### Network Performance

#### Network Receive Throughput
```promql
rate(node_network_receive_bytes_total{device!="lo"}[5m]) / 1024 / 1024
```
**Purpose:** Network inbound MB/s  
**Unit:** MB/s  

#### Network Transmit Throughput
```promql
rate(node_network_transmit_bytes_total{device!="lo"}[5m]) / 1024 / 1024
```
**Purpose:** Network outbound MB/s  
**Unit:** MB/s  

#### Network Receive Errors
```promql
rate(node_network_receive_errs_total{device!="lo"}[5m])
```
**Purpose:** Inbound network errors per second  
**Threshold:** Alert if > 0  

#### Network Transmit Errors
```promql
rate(node_network_transmit_errs_total{device!="lo"}[5m])
```
**Purpose:** Outbound network errors per second  
**Threshold:** Alert if > 0  

#### Network Receive Drops
```promql
rate(node_network_receive_drop_total{device!="lo"}[5m])
```
**Purpose:** Inbound packet drops per second  
**Threshold:** Alert if > 10  

#### Network Transmit Drops
```promql
rate(node_network_transmit_drop_total{device!="lo"}[5m])
```
**Purpose:** Outbound packet drops per second  
**Threshold:** Alert if > 10  

---

### System Resources & Indicators

#### Context Switches
```promql
rate(node_context_switches_total[5m])
```
**Purpose:** Context switches per second  
**Use Case:** System load indicator, excessive switching = performance issue  
**Threshold:** Alert if > 100000/sec  

#### Interrupts
```promql
rate(node_intr_total[5m])
```
**Purpose:** Hardware interrupts per second  
**Use Case:** I/O activity baseline  

#### File Descriptors Used
```promql
node_filefd_allocated
```
**Purpose:** Open file descriptors  
**Use Case:** Resource leak detection  

#### File Descriptors Max
```promql
node_filefd_maximum
```
**Purpose:** Maximum allowed file descriptors  
**Threshold:** Alert if used > 80% of max  

#### File Descriptor Usage Percentage
```promql
100 * (node_filefd_allocated / node_filefd_maximum)
```
**Purpose:** File descriptor utilization  
**Threshold:** Alert if > 80%  

---

## 📈 Capacity Planning Queries

### Resource Headroom

#### Connection Headroom
```promql
100 - (100 * (sum(pg_stat_activity_count{datname="telemetry_kitchen"}) / pg_settings_max_connections))
```
**Purpose:** Available connection capacity  
**Target:** > 40% for healthy operation  

#### CPU Headroom
```promql
avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100
```
**Purpose:** Available CPU capacity  
**Target:** > 40%  

#### Memory Headroom
```promql
100 * ((node_memory_MemAvailable_bytes or node_memory_MemFree_bytes) / node_memory_MemTotal_bytes)
```
**Purpose:** Available memory capacity  
**Target:** > 20%  

#### Disk Headroom
```promql
(node_filesystem_avail_bytes{mountpoint="/",fstype!="rootfs"} / node_filesystem_size_bytes{mountpoint="/",fstype!="rootfs"}) * 100
```
**Purpose:** Available disk capacity  
**Target:** > 25%  

---

### Long-Term Trends (30-Day Windows)

#### Peak Daily Connections
```promql
max_over_time(sum(pg_stat_activity_count{datname="telemetry_kitchen"})[1d:5m])
```
**Purpose:** Maximum connections in any 5-minute window per day  
**Use Case:** Connection pool sizing  

#### Peak Daily CPU Usage
```promql
100 - (avg(max_over_time(rate(node_cpu_seconds_total{mode="idle"}[5m])[1d:5m])) * 100)
```
**Purpose:** Peak CPU usage per day  
**Use Case:** Capacity planning  

#### Peak Daily Memory Usage
```promql
100 - (100 * min_over_time(((node_memory_MemAvailable_bytes or node_memory_MemFree_bytes) / node_memory_MemTotal_bytes)[1d:5m]))
```
**Purpose:** Peak memory usage per day  
**Use Case:** Memory sizing decisions  

#### Average Daily TPS
```promql
avg_over_time(rate(pg_stat_database_xact_commit{datname="telemetry_kitchen"}[5m])[1d:5m])
```
**Purpose:** Average transaction rate per day  
**Use Case:** Database load trending  

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
