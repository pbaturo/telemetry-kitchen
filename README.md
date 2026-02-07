# Telemetry Kitchen

On-prem IoT observability & performance lab built with .NET 10, RabbitMQ, PostgreSQL, Prometheus/Grafana, Metabase, and Azure-Blob-compatible storage. Uses real public sensors first, then simulators, to empirically study ingestion reliability, database behaviour, and anomaly detection.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                       Telemetry Kitchen                              │
└─────────────────────────────────────────────────────────────────────┘

  ┌──────────────┐         ┌────────────┐         ┌──────────────┐
  │ Public APIs  │◄────────│  Gateway   │────────►│  RabbitMQ    │
  │ (Open-Meteo) │  REST   │  (.NET 10) │  Pub    │  (Exchange)  │
  └──────────────┘         └────┬───────┘         └──────┬───────┘
                                │                        │
                                │ Metrics                │ Sub
                                ▼                        ▼
  ┌──────────────┐         ┌────────────┐         ┌──────────────┐
  │  Prometheus  │◄────────│  Consumer  │────────►│  PostgreSQL  │
  │              │ Scrape  │  (.NET 10) │  Write  │  (Database)  │
  └──────┬───────┘         └────────────┘         └──────┬───────┘
         │                                               │
         │ Query                                         │ Query
         ▼                                               ▼
  ┌──────────────┐                               ┌──────────────┐
  │   Grafana    │                               │   Metabase   │
  │  (Ops Viz)   │                               │  (Analytics) │
  └──────────────┘                               └──────────────┘

  ┌──────────────┐
  │   Azurite    │◄────────────────────────┐
  │ (Blob Store) │                         │ (Future: Images)
  └──────────────┘                         │
                                    ┌──────┴───────┐
                                    │   Gateway    │
                                    └──────────────┘
```

### Core Services
- **Gateway Service** (.NET 10): Polls real public sensors via REST APIs, publishes critical streams to RabbitMQ
- **Consumer Service** (.NET 10): Consumes messages from RabbitMQ and persists data to PostgreSQL

### Infrastructure Services
- **RabbitMQ**: Message broker for reliable telemetry data streaming
- **PostgreSQL**: Database for persistent storage of sensor readings
- **Azurite**: Azure Blob Storage emulator for image storage
- **Prometheus**: Metrics collection and monitoring
- **Grafana**: Operations dashboard and visualization
- **Metabase**: Analytics and business intelligence

## Quick Start

### Prerequisites
- Docker and Docker Compose
- .NET 10 SDK (for local development)

### Running the Lab

1. Clone the repository:
```bash
git clone https://github.com/pbaturo/telemetry-kitchen.git
cd telemetry-kitchen
```

2. Start all services:
```bash
docker-compose up -d
```

3. Wait for services to be healthy (about 30-60 seconds):
```bash
docker-compose ps
```

### Accessing the Services

- **Gateway API**: http://localhost:8080
- **Gateway Health**: http://localhost:8080/health
- **Gateway Metrics**: http://localhost:9091/metrics
- **Consumer Metrics**: http://localhost:9092/metrics
- **RabbitMQ Management**: http://localhost:15672 (username: `telemetry`, password: `telemetry123`)
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3000 (username: `admin`, password: `telemetry123`)
- **Metabase**: http://localhost:3001
- **PostgreSQL**: localhost:5432 (database: `telemetry_db`, username: `telemetry`, password: `telemetry123`)

## Data Flow

1. **Gateway** polls public sensor APIs (Open-Meteo weather data) every 30 seconds
2. Gateway publishes sensor readings to **RabbitMQ** (exchange: `telemetry.readings`, queue: `sensor.readings`)
3. **Consumer** processes messages from RabbitMQ and persists to **PostgreSQL**
4. Both Gateway and Consumer expose **Prometheus** metrics
5. **Grafana** visualizes operational metrics from Prometheus and PostgreSQL
6. **Metabase** provides analytics capabilities on the PostgreSQL data
7. **Azurite** stores image data (for future sensor image support)

## Database Schema

The initial schema includes:

- `sensor_readings`: Core telemetry data
- `image_metadata`: Image reference data
- `ingestion_metrics`: Reliability metrics
- `anomalies`: Detected anomalies

Views:
- `ingestion_performance`: Latency and throughput analysis
- `hourly_sensor_stats`: Statistical aggregations

## Metrics & Monitoring

### Gateway Metrics
- `telemetry_gateway_polls_total`: Total sensor polls
- `telemetry_gateway_errors_total`: Total errors
- `telemetry_gateway_queue_depth`: RabbitMQ queue depth
- `telemetry_gateway_poll_duration_seconds`: Poll duration histogram

### Consumer Metrics
- `telemetry_consumer_messages_processed_total`: Total messages processed
- `telemetry_consumer_errors_total`: Total errors
- `telemetry_consumer_db_write_duration_seconds`: Database write latency
- `telemetry_consumer_queue_depth`: Queue depth

## Development

### Building Services Locally

Gateway:
```bash
cd src/Gateway
dotnet restore
dotnet build
dotnet run
```

Consumer:
```bash
cd src/Consumer
dotnet restore
dotnet build
dotnet run
```

### Configuration

Configuration can be customized via environment variables in `docker-compose.yml`:

- `Polling__IntervalSeconds`: Gateway polling interval (default: 30 seconds)
- `RabbitMQ__*`: RabbitMQ connection settings
- `Database__ConnectionString`: PostgreSQL connection string
- `Azurite__ConnectionString`: Azurite connection string

## Monitoring & Observability

### Grafana Dashboard

The pre-configured operations dashboard shows:
- Sensor polling rates
- Message processing rates
- Database ingestion latency (p50, p95)
- RabbitMQ queue depth
- Error rates

### Database Queries

Example queries for analysis:

```sql
-- View recent sensor readings
SELECT * FROM sensor_readings 
ORDER BY timestamp DESC 
LIMIT 100;

-- Hourly statistics
SELECT * FROM hourly_sensor_stats 
WHERE hour > NOW() - INTERVAL '24 hours'
ORDER BY hour DESC;

-- Ingestion performance
SELECT * FROM ingestion_performance
WHERE minute > NOW() - INTERVAL '1 hour'
ORDER BY minute DESC;
```

## Data Sources

Currently using **Open-Meteo** public weather API for real sensor data:
- London, UK temperature
- New York, USA temperature  
- Tokyo, Japan temperature

This can be extended with other public sensor APIs or replaced with simulated data.

## Performance Goals

The lab is designed to measure:

1. **Ingestion Reliability**: Message delivery success rate, retry behavior
2. **Database Performance**: Write throughput, query latency, index effectiveness
3. **Anomaly Detection**: Statistical deviation detection (to be implemented)

## Future Enhancements

- [ ] Add anomaly detection algorithms
- [ ] Implement alert rules in Prometheus
- [ ] Add more sensor types (humidity, pressure, air quality)
- [ ] Schema evolution and migration tools
- [ ] Performance benchmarking tools
- [ ] Add sensor simulators for high-volume testing
- [ ] Implement image ingestion pipeline

## Troubleshooting

### Services not starting
```bash
docker-compose logs [service-name]
```

### Reset everything
```bash
docker-compose down -v
docker-compose up -d
```

### Check RabbitMQ queues
Visit http://localhost:15672 and check the Queues tab

### Check PostgreSQL data
```bash
docker exec -it telemetry-postgres psql -U telemetry -d telemetry_db
```

## License

See LICENSE file for details.
