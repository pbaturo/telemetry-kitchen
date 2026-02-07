.PHONY: help start stop restart logs clean build test

help:
	@echo "Telemetry Kitchen - Available Commands:"
	@echo ""
	@echo "  make start      - Start all services"
	@echo "  make stop       - Stop all services"
	@echo "  make restart    - Restart all services"
	@echo "  make logs       - Show logs from all services"
	@echo "  make clean      - Stop and remove all containers and volumes"
	@echo "  make build      - Build .NET services locally"
	@echo "  make test       - Run tests"
	@echo "  make status     - Show service status"
	@echo ""

start:
	@echo "Starting Telemetry Kitchen..."
	docker compose up -d

stop:
	@echo "Stopping Telemetry Kitchen..."
	docker compose down

restart:
	@echo "Restarting Telemetry Kitchen..."
	docker compose restart

logs:
	docker compose logs -f

clean:
	@echo "Cleaning up Telemetry Kitchen..."
	docker compose down -v
	@echo "All containers and volumes removed"

build:
	@echo "Building Gateway..."
	cd src/Gateway && dotnet build
	@echo "Building Consumer..."
	cd src/Consumer && dotnet build
	@echo "Build complete"

test:
	@echo "Running tests..."
	cd src/Gateway && dotnet test || true
	cd src/Consumer && dotnet test || true

status:
	@echo "Service Status:"
	docker compose ps
