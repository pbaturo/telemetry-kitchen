# Architecture Decisions (A/B/C)

This file lists only the hard decisions that define the Phase 1 starting point.

## Decision A — Ingestion boundary uses a Gateway/Poller (Phase 1)
We ingest “real” sensor-like data by polling public sources via REST GET (e.g., environmental / water / air-quality APIs).
A .NET 10 Gateway/Poller normalizes incoming readings into a canonical `SensorEvent` and publishes to RabbitMQ.

Rationale:
- Faster realism with minimal simulator work
- Preserves internal event-driven pipeline for later push-based devices

## Decision B — PostgreSQL starts naïve (junior baseline)
PostgreSQL is vanilla, single instance. Baseline persistence uses a naïve schema:
- no partitioning
- no sharding
- no time-series extensions (e.g., TimescaleDB)
- minimal indexing (only what is required for correctness, e.g., idempotency)

Rationale:
- We want a measurable baseline (start naive → observe → improve → compare)

## Decision C — RabbitMQ is the durability gate
For critical ingestion, the system treats RabbitMQ as the durability boundary:
- at-least-once delivery is assumed
- consumers must be idempotent
- `eventId` is required and must be unique (enforced by DB constraint)

Rationale:
- Decouple ingestion from database writes
- Learn real backpressure and consumer-lag operational behavior
