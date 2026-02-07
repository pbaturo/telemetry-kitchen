# Telemetry Kitchen

On-prem IoT observability & performance lab built to **learn by measurement**:
- Gateway polls real public sensors via REST (GET) and normalizes events
- Critical streams go through **RabbitMQ** (durability gate)
- Consumers persist into **vanilla PostgreSQL** (start naive → evolve)
- **Prometheus + Grafana** for ops observability
- **Metabase** for self-hosted analytics/BI
- **Azurite** for Azure Blob–compatible local object storage (camera snapshots)

Target runtime: **Docker Compose** on a laptop. Code: **.NET 9**.

> Philosophy: start naive → measure → improve → compare.

## Status: Phase 1 Complete ✅

**Implemented & Running:**
- ✅ `src/Gateway.Poller` — polls 10 real OpenSenseMap sensors → RabbitMQ
- ✅ `src/Ingest.Consumer` — RabbitMQ → PostgreSQL (idempotent, with deduplication)
- ✅ `src/Shared` — shared contracts and utilities
- ✅ `infra/compose` — docker-compose with 7 services (postgres, rabbitmq, gateway-poller, ingest-consumer, prometheus, grafana, pgadmin)
- ✅ `docs/` — architecture notes + Grafana dashboard
- ✅ Prometheus metrics collection and scraping

## Roadmap: Phase 2+ (Future)

- 🔄 `src/Web.Mvc` — ASP.NET Core MVC UI for sensors, status dashboard, history views
- 🔄 Metabase integration — self-hosted analytics/BI dashboards
- 🔄 Azurite — local object storage for camera snapshots and media
- 🔄 Additional external APIs (USGS, Weather.com, Sensor.Community)

## Quick start (placeholder)

Prereqs:
- Docker + Docker Compose
- .NET 10 SDK

Run infra:
```bash
cp infra/compose/.env.example infra/compose/.env
docker compose -f infra/compose/docker-compose.yml up -d
