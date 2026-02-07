# Telemetry Kitchen

On-prem IoT observability & performance lab built to **learn by measurement**:
- Gateway polls real public sensors via REST (GET) and normalizes events
- Critical streams go through **RabbitMQ** (durability gate)
- Consumers persist into **vanilla PostgreSQL** (start naive → evolve)
- **Prometheus + Grafana** for ops observability
- **Metabase** for self-hosted analytics/BI
- **Azurite** for Azure Blob–compatible local object storage (camera snapshots)

Target runtime: **Docker Compose** on a laptop. Code: **.NET 10**.

> Philosophy: start naive → measure → improve → compare.

## What’s inside (planned structure)

- `src/Gateway.Poller` — polls external sensors → RabbitMQ
- `src/Ingest.Consumer` — RabbitMQ → PostgreSQL (idempotent)
- `src/Web.Mvc` — ASP.NET Core MVC UI (sensors, status, history)
- `infra/compose` — docker-compose stack (postgres, rabbitmq, prometheus, grafana, metabase, azurite)
- `docs/` — architecture notes + exported dashboards

## Quick start (placeholder)

Prereqs:
- Docker + Docker Compose
- .NET 10 SDK

Run infra:
```bash
cp infra/compose/.env.example infra/compose/.env
docker compose -f infra/compose/docker-compose.yml up -d
