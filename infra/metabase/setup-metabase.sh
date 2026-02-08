#!/bin/bash

# Wait for Metabase to be ready
echo "⏳ Waiting for Metabase API..."
for i in {1..60}; do
  if curl -s http://localhost:3000/api/health > /dev/null 2>&1; then
    echo "✅ Metabase is ready"
    break
  fi
  sleep 1
done

sleep 2

echo "📝 Setting up initial admin user..."

# Call setup endpoint to create admin account and skip setup wizard
SETUP_RESPONSE=$(curl -s -X POST http://localhost:3000/api/setup \
  -H "Content-Type: application/json" \
  -d '{
    "user": {
      "first_name": "Admin",
      "last_name": "User",
      "email": "admin@example.com",
      "password": "admin"
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
    "invite_user_to_setup": false
  }')

if echo "$SETUP_RESPONSE" | grep -q '"id"'; then
  echo "✅ Setup complete!"
  echo "   📍 http://localhost:3001"
  echo "   👤 Login: admin@example.com / admin"
else
  echo "⚠️  Setup response:"
  echo "$SETUP_RESPONSE"
fi
