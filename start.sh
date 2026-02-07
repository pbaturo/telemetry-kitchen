#!/bin/bash
# Telemetry Kitchen - Quick Start Script

set -e

echo "🏗️  Starting Telemetry Kitchen..."
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker and try again."
    exit 1
fi

echo "✅ Docker is running"
echo ""

# Stop and remove any existing containers
echo "🧹 Cleaning up any existing containers..."
docker compose down -v 2>/dev/null || true
echo ""

# Start all services
echo "🚀 Starting all services..."
docker compose up -d
echo ""

# Wait for services to be healthy
echo "⏳ Waiting for services to be ready..."
sleep 5

# Check service health
echo ""
echo "📊 Service Status:"
docker compose ps
echo ""

# Display access information
echo "✨ Telemetry Kitchen is ready!"
echo ""
echo "📍 Access Points:"
echo "  Gateway API:         http://localhost:8080/health"
echo "  Gateway Metrics:     http://localhost:9091/metrics"
echo "  Consumer Metrics:    http://localhost:9092/metrics"
echo "  RabbitMQ Management: http://localhost:15672 (telemetry/telemetry123)"
echo "  Prometheus:          http://localhost:9090"
echo "  Grafana:             http://localhost:3000 (admin/telemetry123)"
echo "  Metabase:            http://localhost:3001"
echo "  PostgreSQL:          localhost:5432 (telemetry_db/telemetry/telemetry123)"
echo ""
echo "📖 View logs: docker compose logs -f [service-name]"
echo "🛑 Stop all:  docker compose down"
echo ""
