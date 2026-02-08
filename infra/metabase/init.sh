#!/bin/bash
set -e

echo "⏳ Waiting for Metabase to be ready..."
for i in {1..60}; do
  if curl -s http://metabase:3000/api/health > /dev/null 2>&1; then
    echo "✅ Metabase is ready!"
    break
  fi
  echo "  Attempt $i/60..."
  sleep 2
done

sleep 3

echo ""
echo "🔧 Starting Metabase auto-setup..."

# Get the setup token from the page (it's embedded in the HTML)
# Since we can't easily extract it, we'll try the setup endpoint with a reasonable approach

# First, check if already set up (if this fails, setup is needed)
HEALTH=$(curl -s http://metabase:3000/api/user/current -H "X-Metabase-Session: test" 2>/dev/null || echo "")

if echo "$HEALTH" | grep -q "unauthorized"; then
  echo "✅ Metabase needs setup (no session yet) - proceeding..."
  
  # Call setup with password that meets requirements (>6 chars, not common)
  SETUP_RESULT=$(curl -s -X POST http://metabase:3000/api/setup \
    -H "Content-Type: application/json" \
    -d '{
      "token": "",
      "user": {
        "first_name": "Admin",
        "last_name": "Administrator",
        "email": "admin@example.com",
        "password": "TelemetryKitchen2026!"
      },
      "database": {
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
        }
      },
      "prefs": {
        "site_name": "Telemetry Kitchen",
        "site_locale": "en"
      }
    }' 2>&1)
  
  if echo "$SETUP_RESULT" | grep -q '"id"'; then
    echo "✅ Setup completed successfully!"
    echo ""
    echo "🎉 Metabase is ready:"
    echo "   📍 http://localhost:3001"
    echo "   👤 Email: admin@example.com"
    echo "   🔑 Password: TelemetryKitchen2026!"
    echo "   🗄️  Database: Telemetry Kitchen (PostgreSQL)"
  else
    echo "⚠️  Setup response:"
    echo "$SETUP_RESULT"
  fi
else
  echo "✅ Metabase is already configured"
fi

echo ""
echo "✅ Initialization complete, exiting..."
