# Architecture Overview

Telemetry Kitchen is an on-prem IoT observability and performance lab built to **learn by measurement**.
We intentionally start with a simple baseline (including a naïve database schema) and evolve the design based on observed bottlenecks.

## End-to-end architecture

See: `docs/architecture/e2e-architecture.mmd`

```mermaid
flowchart LR
  subgraph EXT[External Public Sensors (REST GET)]
    OS[openSenseMap API]
    SC[Sensor.Community API]
    USGS[USGS Water Services API]
  end

  subgraph LAB[Telemetry Kitchen (On-Prem / Docker Compose)]
    GP[Gateway.Poller (.NET 10)]
    MQ[(RabbitMQ)\nDurability gate]
    IC[Ingest.Consumer (.NET 10)]
    DB[(PostgreSQL - vanilla)\nSingle instance\nNaive baseline schema]
    PROM[(Prometheus)]
    GRAF[Grafana]
    META[Metabase]
    MVC[Web.Mvc (.NET 10 MVC)]
    AZ[(Azurite)\n(Phase 2 blobs)]
    PGX[postgres_exporter]
    RMQX[rabbitmq_exporter]
    CAD[cAdvisor/node-exporter]
  end

  OS -->|HTTP GET| GP
  SC -->|HTTP GET| GP
  USGS -->|HTTP GET| GP

  GP -->|Publish SensorEvent| MQ
  MQ -->|Consume| IC
  IC -->|INSERT (idempotent)| DB

  MVC --> DB
  META --> DB

  GP -->|/metrics| PROM
  IC -->|/metrics| PROM
  PGX --> PROM
  RMQX --> PROM
  CAD --> PROM
  PROM --> GRAF

  DB --> PGX
  MQ --> RMQX

  GP -.-> AZ
  IC -.-> DB
