# Contributing to Telemetry Kitchen

Thank you for your interest in contributing to Telemetry Kitchen!

## Development Setup

### Prerequisites
- .NET 10 SDK
- Docker and Docker Compose
- Git

### Local Development

1. **Clone the repository:**
```bash
git clone https://github.com/pbaturo/telemetry-kitchen.git
cd telemetry-kitchen
```

2. **Start infrastructure services only:**
```bash
docker compose up -d rabbitmq postgres azurite prometheus grafana metabase
```

3. **Run Gateway locally:**
```bash
cd src/Gateway
dotnet run
```

4. **Run Consumer locally (in another terminal):**
```bash
cd src/Consumer
dotnet run
```

## Project Structure

```
telemetry-kitchen/
├── src/
│   ├── Gateway/          # Sensor polling service
│   │   ├── Services/     # Business logic
│   │   ├── Models/       # Data models
│   │   └── Program.cs    # Application entry point
│   └── Consumer/         # Message processing service
│       ├── Services/     # Business logic
│       ├── Models/       # Data models
│       └── Program.cs    # Application entry point
├── config/               # Configuration files
│   ├── prometheus.yml    # Prometheus scraping config
│   └── grafana/          # Grafana datasources and dashboards
├── sql/                  # Database initialization scripts
├── docker-compose.yml    # Infrastructure orchestration
└── README.md
```

## Adding New Sensor Types

To add a new sensor type:

1. Update `SensorPollingService.cs` to add new sensor configurations
2. Add appropriate parsing logic for the sensor's API format
3. Update database schema if needed for new data types
4. Add metrics tracking for the new sensor type

## Testing

### Manual Testing

1. **Check Gateway is polling:**
```bash
curl http://localhost:8080/health
curl http://localhost:9091/metrics | grep telemetry_gateway_polls_total
```

2. **Check Consumer is processing:**
```bash
curl http://localhost:9092/metrics | grep telemetry_consumer_messages_processed_total
```

3. **Query database:**
```bash
docker exec -it telemetry-postgres psql -U telemetry -d telemetry_db -c "SELECT COUNT(*) FROM sensor_readings;"
```

### Integration Testing

Run the full stack:
```bash
./start.sh
docker compose logs -f gateway consumer
```

## Code Style

- Follow standard .NET coding conventions
- Use meaningful variable and method names
- Add comments for complex logic
- Keep methods focused and small

## Pull Request Process

1. Create a feature branch from `main`
2. Make your changes
3. Test locally
4. Update documentation if needed
5. Submit a pull request with a clear description

## Adding New Features

When adding new features, consider:

- **Metrics**: Add Prometheus metrics for observability
- **Logging**: Use structured logging with appropriate log levels
- **Error Handling**: Handle errors gracefully and log appropriately
- **Configuration**: Make features configurable via appsettings.json
- **Documentation**: Update README.md and this guide

## Performance Considerations

- Use async/await for I/O operations
- Implement connection pooling where applicable
- Consider batching for high-volume operations
- Monitor memory usage and prevent leaks

## Questions?

Open an issue on GitHub for questions or suggestions!
