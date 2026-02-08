#!/bin/bash
set -e

METABASE_HOST=${METABASE_HOST:-metabase}
METABASE_PORT=${METABASE_PORT:-3000}
METABASE_URL="http://${METABASE_HOST}:${METABASE_PORT}"

ADMIN_EMAIL=${ADMIN_EMAIL:-admin@example.com}
ADMIN_PASSWORD=${ADMIN_PASSWORD:-TelemetryKitchen2026!}
ADMIN_FIRST_NAME=${ADMIN_FIRST_NAME:-Admin}
ADMIN_LAST_NAME=${ADMIN_LAST_NAME:-Administrator}

DB_HOST=${DB_HOST:-postgres}
DB_PORT=${DB_PORT:-5432}
DB_NAME=${DB_NAME:-telemetry_kitchen}
DB_USER=${DB_USER:-tk}
DB_PASSWORD=${DB_PASSWORD:-tk}

echo "Waiting for Metabase to be ready..."
for i in {1..60}; do
  if curl -s "${METABASE_URL}/api/health" > /dev/null 2>&1; then
    echo "Metabase is ready."
    break
  fi
  echo "  Attempt ${i}/60..."
  sleep 2
done

sleep 3

echo ""
echo "Starting Metabase auto-setup..."

PROPS=$(curl -s "${METABASE_URL}/api/session/properties" || true)
SETUP_TOKEN=$(echo "$PROPS" | grep -o 'setup-token":"[^"]*' | head -1 | cut -d'"' -f3)

if [ -z "$SETUP_TOKEN" ]; then
  echo "Metabase is already configured (no setup token present)."
  echo "Initialization complete, exiting."
  exit 0
fi

SETUP_RESULT=$(curl -s -X POST "${METABASE_URL}/api/setup" \
  -H "Content-Type: application/json" \
  -d '{
    "token": "'"${SETUP_TOKEN}"'",
    "user": {
      "first_name": "'"${ADMIN_FIRST_NAME}"'",
      "last_name": "'"${ADMIN_LAST_NAME}"'",
      "email": "'"${ADMIN_EMAIL}"'",
      "password": "'"${ADMIN_PASSWORD}"'"
    },
    "database": {
      "name": "Telemetry Kitchen",
      "engine": "postgres",
      "details": {
        "host": "'"${DB_HOST}"'",
        "port": '"${DB_PORT}"',
        "dbname": "'"${DB_NAME}"'",
        "user": "'"${DB_USER}"'",
        "password": "'"${DB_PASSWORD}"'",
        "ssl": false,
        "tunnel-enabled": false
      }
    },
    "prefs": {
      "site_name": "Telemetry Kitchen",
      "site_locale": "en"
    }
  }' 2>&1)

if echo "$SETUP_RESULT" | grep -Eq '"id"|"status"[[:space:]]*:[[:space:]]*"ok"'; then
  echo "Setup completed successfully."
  echo ""
  echo "Metabase is ready: http://localhost:3001"
  echo "Login: ${ADMIN_EMAIL} / ${ADMIN_PASSWORD}"
  echo "Database: Telemetry Kitchen (PostgreSQL)"
else
  echo "Setup failed. Response:"
  echo "$SETUP_RESULT"
  exit 1
fi

echo ""
echo "Initialization complete, exiting."
