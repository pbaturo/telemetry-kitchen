#!/bin/bash

# Metabase initialization script - configures PostgreSQL data source and basic setup

METABASE_URL="http://localhost:3000"
ADMIN_EMAIL="admin@example.com"
ADMIN_PASSWORD="admin"

echo "⏳ Waiting for Metabase to be ready..."
for i in {1..60}; do
  if curl -s "$METABASE_URL/api/health" > /dev/null; then
    echo "✅ Metabase is ready!"
    break
  fi
  echo "  Waiting... ($i/60)"
  sleep 2
done

echo ""
echo "🔐 Attempting to login as admin..."

# Get session token
SESSION_RESPONSE=$(curl -s -X POST "$METABASE_URL/api/session" \
  -H "Content-Type: application/json" \
  -d "{
    \"username\": \"$ADMIN_EMAIL\",
    \"password\": \"$ADMIN_PASSWORD\"
  }")

SESSION_TOKEN=$(echo "$SESSION_RESPONSE" | grep -o '"id":"[^"]*' | cut -d'"' -f4)

if [ -z "$SESSION_TOKEN" ]; then
  echo "❌ Failed to get session token"
  echo "Response: $SESSION_RESPONSE"
  exit 1
fi

echo "✅ Session token obtained: ${SESSION_TOKEN:0:20}..."

echo ""
echo "🗄️  Adding PostgreSQL data source..."

# Add PostgreSQL database as data source
DB_RESPONSE=$(curl -s -X POST "$METABASE_URL/api/database" \
  -H "Content-Type: application/json" \
  -H "X-Metabase-Session: $SESSION_TOKEN" \
  -d '{
    "name": "Telemetry Kitchen",
    "engine": "postgres",
    "details": {
      "host": "postgres",
      "port": 5432,
      "dbname": "telemetry_kitchen",
      "user": "tk",
      "password": "tk",
      "ssl": false,
      "tunnel-enabled": false
    },
    "auto_run_queries": true,
    "caching_ttl": null,
    "cache_strategy": null
  }')

DB_ID=$(echo "$DB_RESPONSE" | grep -o '"id":[0-9]*' | head -1 | grep -o '[0-9]*')

if [ -z "$DB_ID" ]; then
  echo "❌ Failed to add database"
  echo "Response: $DB_RESPONSE"
  exit 1
fi

echo "✅ PostgreSQL database added with ID: $DB_ID"

echo ""
echo "📊 Performing initial sync of database metadata..."

# Trigger metadata sync
curl -s -X POST "$METABASE_URL/api/database/$DB_ID/sync_schema" \
  -H "X-Metabase-Session: $SESSION_TOKEN" > /dev/null

echo "✅ Metadata sync initiated"

echo ""
echo "🎉 Metabase setup complete!"
echo "   📍 Access at: http://localhost:3001"
echo "   👤 Login: admin@example.com / admin"
echo "   🗄️  Database: Telemetry Kitchen (PostgreSQL)"
