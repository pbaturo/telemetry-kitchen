# Telemetry Kitchen

![Architecture](docs/architecture/e2e-architecture.mmd)

On-prem IoT observability & performance lab built to **learn by measurement**:
- Gateway polls real public sensors via REST (GET) and normalizes events
- Critical streams go through **RabbitMQ** (durability gate)
- Consumers persist into **vanilla PostgreSQL** (start naive → evolve)
- **Prometheus + Grafana + Loki** for ops observability and logging
- **Metabase** for self-hosted analytics/BI (Phase 2)
- **Azurite** for Azure Blob–compatible local object storage (Phase 2)

**Target runtime:** Docker Compose on a laptop  
**Code:** .NET 9  
**Philosophy:** Start naive → measure → improve → compare

---

## 📋 Table of Contents

- [Status & Features](#status--features)  
- [Quick Start](#quick-start)  
- [Architecture](#architecture)  
- [Documentation](#documentation)  
- [Operations](#operations)
- [Development](#development)  
- [Roadmap](#roadmap)

---

## Status & Features

### ✅ Phase 1 Complete (Current)

**Core Data Pipeline:**
- ✅ Gateway.Poller — polls 10 real OpenSenseMap sensors → RabbitMQ
- ✅ Ingest.Consumer — RabbitMQ → PostgreSQL (idempotent, with deduplication)
- ✅ Shared contracts — SensorEvent, StatusLevel, validation utilities

**Infrastructure:**
- ✅ PostgreSQL 17 — vanilla instance with naive schema (sensor_events table)
- ✅ RabbitMQ — durable message broker with management UI + Prometheus plugin
- ✅ Prometheus — metrics collection (10s scrape interval)
- ✅ Loki — centralized log aggregation with Serilog integration
- ✅ Grafana — operational dashboards and log explorer
- ✅ PGAdmin — database administration UI

**Observability:**
- ✅ postgres_exporter — Database metrics (connections, TPS, cache hit ratio)
- ✅ node_exporter — System metrics (CPU, memory, disk, network)
- ✅ Operational Monitoring dashboard — Health, throughput, resource utilization, errors
- ✅ Sensor Overview dashboard — Business metrics and data visualization
- ✅ Structured JSON logging with correlation IDs

**Testing & Development:**
- ✅ 100k seed data script — Performance testing baseline
- ✅ Local development runbook — Step-by-step setup guide
- ✅ Operators Manual — Day-to-day operations and troubleshooting

---

## Quick Start

### Prerequisites

- **Docker Desktop** (with Docker Compose)
- **.NET 9 SDK** (optional, for local development)
- **PowerShell** (for Windows) or **Bash** (for Linux/Mac)

### 1. Start Infrastructure

```powershell
# Clone repository
git clone https://github.com/YOUR_ORG/telemetry-kitchen.git
cd telemetry-kitchen

# Start all services
docker-compose -f infra/compose/docker-compose.yml up -d

# Verify services are running
docker ps
```

**Services started:**
- **PostgreSQL** → localhost:5432
- **RabbitMQ** → localhost:5672 (AMQP), localhost:15672 (Management UI)
- **Prometheus** → localhost:9091
- **Grafana** → localhost:3000
- **Loki** → localhost:3100
- **PGAdmin** → localhost:5050
- **Web MVC UI** → http://localhost:5000

### 2. Access Dashboards

| Service | URL | Credentials |
|---------|-----|-------------|
| **Grafana** | http://localhost:3000 | admin / admin |
| **RabbitMQ** | http://localhost:15672 | tk / tk |
| **PGAdmin** | http://localhost:5050 | admin@example.com / admin |
| **Prometheus** | http://localhost:9091 | - |
| **Web MVC UI** | http://localhost:5000 | - |

### 3. View Data Ingestion

**Grafana Dashboard:**
1. Login to http://localhost:3000
2. Navigate to **Dashboards** → **Operational Monitoring - Ingest Reliability**
3. Watch metrics populate in real-time

**Web MVC Dashboard:**
1. Login to http://localhost:3000
2. Navigate to **Dashboards** → **Web MVC - HTTP Metrics**
3. Verify request rate, latency, and 5xx errors

**Database Queries (PGAdmin):**
1. Login to http://localhost:5050
2. Add server: Host=`tk-postgres`, Port=`5432`, DB=`telemetry_kitchen`, User=`tk`, Password=`tk`
3. Query recent events:
```sql
SELECT sensor_id, observed_at, status_level 
FROM sensor_events 
ORDER BY observed_at DESC 
LIMIT 20;
```

### 4. View Logs

**Grafana Loki:**
1. Navigate to **Explore** (compass icon)
2. Select **Loki** datasource
3. Query: `{application=~"gateway-poller|ingest-consumer"}`

**Docker Logs:**
```powershell
docker logs -f tk-gateway-poller
docker logs -f tk-ingest-consumer
```

### 5. Metrics Endpoints

| Component | Endpoint | Notes |
| --- | --- | --- |
| Gateway.Poller | http://localhost:9090/metrics | Prometheus metrics |
| Web MVC | http://localhost:5000/metrics | Prometheus metrics |
| RabbitMQ | http://localhost:15692/metrics | Prometheus plugin |
| Postgres Exporter | http://localhost:9187/metrics | DB metrics |
| Node Exporter | http://localhost:9100/metrics | Host metrics |
| Prometheus UI | http://localhost:9091 | Query and graph metrics |
| Grafana | http://localhost:3000 | Dashboards and logs |

---

## Architecture

### System Overview

```
🌍 External APIs → 🔄 Gateway.Poller → 💾 RabbitMQ → 📥 Ingest.Consumer → 🗄️ PostgreSQL
                                ↓
                          📊 Observability Stack
                          (Prometheus + Loki + Grafana)
```

**Detailed Architecture:** See [docs/architecture/e2e-architecture.mmd](docs/architecture/e2e-architecture.mmd)

**Key Components:**
- **Data Sources (Blue):** External public sensor APIs (OpenSenseMap, Sensor.Community, USGS)
- **Ingestion Process (Green):** Gateway → Queue → Consumer → Database
- **Observability Stack (Purple):** Metrics collection, time-series storage, visualization
- **Client Area (Orange):** Web UI, Analytics (Phase 2)

**Design Principles:**
1. **Start Naive:** Single PostgreSQL instance, simple schema (one big table)
2. **Measure Everything:** Prometheus metrics, Loki logs, Grafana dashboards
3. **Iterate:** Identify bottlenecks, optimize, measure again
4. **Compare:** Before/after performance analysis

---

## Documentation

### 📘 For Operators
- **[Operators Manual](docs/OPERATORS-MANUAL.md)** — Complete operations guide
  - Daily health checks
  - Using Grafana, PGAdmin, RabbitMQ
  - Viewing logs and metrics
  - Troubleshooting scenarios
  - Performance baselines

### 📗 For Developers
- **[Local Development Runbook](docs/runbooks/local-dev.md)** — Setup and development guide
  - Running services locally
  - Database schema
  - Configuration options
  - Troubleshooting development issues

### 📙 Architecture
- **[Architecture Overview](docs/architecture/overview.md)** — High-level design
- **[Architecture Decisions](docs/architecture/decisions.md)** — Design rationale
- **[E2E Architecture Diagram](docs/architecture/e2e-architecture.mmd)** — Visual architecture
- **[Contracts](docs/architecture/contracts/)** — Data contracts and schemas

---

## Operations

### Daily Health Check

```powershell
# 1. Verify services running
docker ps

# 2. Check Grafana dashboards
# http://localhost:3000 → "Operational Monitoring - Ingest Reliability"
# Look for green health indicators

# 3. Quick database check (PGAdmin)
SELECT COUNT(*) as events_last_hour
FROM sensor_events
WHERE observed_at > NOW() - INTERVAL '1 hour';
```

### Common Tasks

**Restart Services:**
```powershell
docker-compose -f infra/compose/docker-compose.yml restart SERVICE_NAME
```

**View Logs:**
```powershell
docker logs -f tk-SERVICE-NAME
```

**Backup Database:**
```powershell
docker exec tk-postgres pg_dump -U tk telemetry_kitchen > backup.sql
```

**Load Seed Data (100k events):**
```sql
-- Execute in PGAdmin
-- File: infra/postgres/init/002_seed.sql
```

**Full Operations Guide:** See [OPERATORS-MANUAL.md](docs/OPERATORS-MANUAL.md)

---

## Development

### Project Structure

```
telemetry-kitchen/
├── src/
│   ├── Gateway.Poller/       # REST API poller (.NET 9)
│   ├── Ingest.Consumer/      # Queue consumer (.NET 9)
│   ├── Shared/               # Common contracts & utilities
│   └── Web.Mvc/              # Web UI (Phase 2)
├── infra/
│   ├── compose/              # Docker Compose stack
│   ├── grafana/              # Dashboards & datasources
│   ├── postgres/             # Schema & seed data
│   ├── prometheus/           # Scrape configs
│   └── rabbitmq/             # Queue definitions
├── docs/
│   ├── architecture/         # Design docs & diagrams
│   ├── runbooks/             # Operational procedures
│   └── OPERATORS-MANUAL.md   # Complete ops guide
└── tools/
    └── scripts/              # Utility scripts
```

### Building Locally

```powershell
# Build .NET services
dotnet build telemetry-kitchen.sln

# Run Gateway.Poller locally
dotnet run --project src/Gateway.Poller

# Run Ingest.Consumer locally
dotnet run --project src/Ingest.Consumer

# Run tests
dotnet test
```

### Configuration

**Gateway.Poller:** `src/Gateway.Poller/appsettings.json`
- Sensor URLs and poll intervals
- RabbitMQ connection
- Prometheus metrics port

**Ingest.Consumer:** `src/Ingest.Consumer/appsettings.json`
- RabbitMQ connection
- PostgreSQL connection
- Batch size and concurrency

---

## Roadmap

### 🔄 Phase 2 (Planned)

**Web UI & Analytics:**
- [ ] `src/Web.Mvc` — ASP.NET Core MVC read-only UI
  - Sensor status dashboard
  - Event history views
  - Real-time metrics display
- [ ] Metabase integration — self-hosted analytics/BI
  - Custom SQL queries
  - Interactive dashboards
  - Export reports

**Extended Data Sources:**
- [ ] Sensor.Community API integration
- [ ] USGS Water Services API integration
- [ ] Custom sensor simulator for load testing

**Object Storage:**
- [ ] Azurite setup — Azure Blob-compatible local storage
- [ ] Camera snapshot ingestion
- [ ] Media file references in PostgreSQL

### 🎯 Phase 3 (Future)

**Performance Optimization:**
- [ ] PostgreSQL schema evolution (partitioning, indexing)
- [ ] Time-series optimizations
- [ ] Query performance analysis
- [ ] Horizontal scaling experiments

**Advanced Observability:**
- [ ] Grafana alerting rules
- [ ] Automated anomaly detection
- [ ] Distributed tracing (OpenTelemetry)
- [ ] Custom metric exporters

---

## Logging (Loki)

All .NET services log to Grafana Loki in structured JSON format.

**Query Examples:**

```logql
# All application logs
{application=~"gateway-poller|ingest-consumer"}

# Only errors
{application=~".*"} | json | level="Error"

# Specific sensor polling
{application="gateway-poller"} |= "sensor_id"
```

**Access:** Grafana → Explore → Select "Loki" datasource

---

## Contributing

This is a learning/experimental project. Contributions welcome!

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

---

## License

[Apache License 2.0](LICENSE)

---

## Support & Contact

**Documentation Issues:** Open GitHub issue  
**Operations Questions:** See [OPERATORS-MANUAL.md](docs/OPERATORS-MANUAL.md)  
**Development Questions:** See [local-dev.md](docs/runbooks/local-dev.md)

---

**Philosophy:** Start naive → measure → improve → compare ✨



