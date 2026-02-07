#!/bin/bash
# Telemetry Kitchen - System Verification Script

set -e

echo "🔍 Telemetry Kitchen - System Verification"
echo "=========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to check if a service is responding
check_service() {
    local name=$1
    local url=$2
    local expected=$3
    
    echo -n "  Checking $name... "
    
    if curl -sf "$url" > /dev/null 2>&1; then
        echo -e "${GREEN}✓ OK${NC}"
        return 0
    else
        echo -e "${RED}✗ FAILED${NC}"
        return 1
    fi
}

# Function to check HTTP response contains text
check_service_response() {
    local name=$1
    local url=$2
    local expected=$3
    
    echo -n "  Checking $name... "
    
    response=$(curl -sf "$url" 2>/dev/null || echo "")
    
    if [[ "$response" == *"$expected"* ]]; then
        echo -e "${GREEN}✓ OK${NC}"
        return 0
    else
        echo -e "${RED}✗ FAILED${NC}"
        echo "    Expected: $expected"
        echo "    Got: ${response:0:100}"
        return 1
    fi
}

# Check if services are running
echo "1. Checking if services are running..."
if ! docker compose ps | grep -q "Up"; then
    echo -e "${RED}✗ Services are not running. Run 'docker compose up -d' first.${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Services are running${NC}"
echo ""

# Check individual service health
echo "2. Checking service health endpoints..."
check_service_response "Gateway Health" "http://localhost:8080/health" "healthy"
echo ""

# Check metrics endpoints
echo "3. Checking metrics endpoints..."
check_service "Gateway Metrics" "http://localhost:9091/metrics"
check_service "Consumer Metrics" "http://localhost:9092/metrics"
echo ""

# Check infrastructure services
echo "4. Checking infrastructure services..."
check_service "RabbitMQ Management" "http://localhost:15672"
check_service "Prometheus" "http://localhost:9090"
check_service "Grafana" "http://localhost:3000/api/health"
echo ""

# Check if Gateway is collecting metrics
echo "5. Verifying Gateway metrics..."
if curl -sf http://localhost:9091/metrics | grep -q "telemetry_gateway_polls_total"; then
    echo -e "  ${GREEN}✓ Gateway poll metrics found${NC}"
else
    echo -e "  ${YELLOW}⚠ Gateway poll metrics not found yet (service may be starting)${NC}"
fi
echo ""

# Check if Consumer is collecting metrics
echo "6. Verifying Consumer metrics..."
if curl -sf http://localhost:9092/metrics | grep -q "telemetry_consumer"; then
    echo -e "  ${GREEN}✓ Consumer metrics found${NC}"
else
    echo -e "  ${YELLOW}⚠ Consumer metrics not found yet (service may be starting)${NC}"
fi
echo ""

# Check database connectivity
echo "7. Checking database..."
if docker exec telemetry-postgres psql -U telemetry -d telemetry_db -c "SELECT 1;" > /dev/null 2>&1; then
    echo -e "  ${GREEN}✓ PostgreSQL is accessible${NC}"
    
    # Check if tables exist
    tables=$(docker exec telemetry-postgres psql -U telemetry -d telemetry_db -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';")
    echo "  Found $tables tables"
    
    # Check for sensor readings (may be 0 if just started)
    count=$(docker exec telemetry-postgres psql -U telemetry -d telemetry_db -t -c "SELECT COUNT(*) FROM sensor_readings;" 2>/dev/null || echo "0")
    echo "  Sensor readings in database: $count"
else
    echo -e "  ${RED}✗ PostgreSQL is not accessible${NC}"
fi
echo ""

# Check RabbitMQ queues
echo "8. Checking RabbitMQ queues..."
if docker exec telemetry-rabbitmq rabbitmqctl list_queues > /dev/null 2>&1; then
    echo -e "  ${GREEN}✓ RabbitMQ is accessible${NC}"
    docker exec telemetry-rabbitmq rabbitmqctl list_queues 2>/dev/null | grep sensor || echo "  Queue 'sensor.readings' may not have messages yet"
else
    echo -e "  ${RED}✗ RabbitMQ is not accessible${NC}"
fi
echo ""

echo "=========================================="
echo -e "${GREEN}✨ Verification Complete!${NC}"
echo ""
echo "📖 Next Steps:"
echo "  - View Gateway logs:    docker compose logs -f gateway"
echo "  - View Consumer logs:   docker compose logs -f consumer"
echo "  - Query database:       docker exec -it telemetry-postgres psql -U telemetry -d telemetry_db"
echo "  - View Grafana:         http://localhost:3000 (admin/telemetry123)"
echo ""
