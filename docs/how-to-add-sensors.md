# How to Add More OpenSenseMap Sensors

## Current Status

✅ **10 real working sensors configured** (polling every 20 seconds)
- 9 sensors in Europe (Germany, Switzerland)
- 1 sensor in Asia (Thailand - Koh Mak island)

📊 **Current ingestion rate:** 1,800 events/hour (3× increase from original 600 events/hour)

## Geographic Coverage Note

**Important:** OpenSenseMap is a German-based citizen science project, so the majority of available sensors are located in Europe (particularly Germany). Finding sensors from North America, South America, Africa, and Oceania may be challenging as they have limited coverage in those regions.

## How to Find and Add Real Sensors

### Step 1: Browse OpenSenseMap Website

1. Visit: https://opensensemap.org/explore
2. Use the map to find sensors in your desired locations
3. Click on a sensor to view its details
4. Copy the **box ID** from the URL (24-character hexadecimal string)
   - Example URL: `https://opensensemap.org/explore/578207d56fea661300861f3b`
   - Box ID: `578207d56fea661300861f3b`

### Step 2: Using the API to Search

You can also use the OpenSenseMap API to find sensors programmatically:

```powershell
# Get sensors from a specific bounding box (e.g., Berlin area)
$response = Invoke-RestMethod -Uri "https://api.opensensemap.org/boxes?bbox=13.0,52.3,13.7,52.7&limit=20"

# View sensor details
$response | ForEach-Object {
    Write-Host "ID: $_._id"
    Write-Host "Name: $_.name"
    Write-Host "Coordinates: [$($_.currentLocation.coordinates[1]), $($_.currentLocation.coordinates[0])]"
    Write-Host ""
}
```

**Bounding box format:** `bbox=minLon,minLat,maxLon,maxLat`

**Example regions:**
- **Europe (Germany):** `bbox=6,47,15,55`
- **Europe (UK):** `bbox=-8,50,2,60`
- **Asia (Japan):** `bbox=128,30,146,46`
- **North America (USA East):** `bbox=-80,35,-70,45`

### Step 3: Add Sensor to Configuration

Edit `src/Gateway.Poller/appsettings.json` and add a new entry to the `Scenario1.Stations` array:

```json
{
  "SensorId": "your-unique-id",
  "Name": "Sensor Name from Website",
  "Url": "https://api.opensensemap.org/boxes/[BOX_ID_HERE]",
  "PollIntervalSeconds": 20,
  "Lat": 52.5200,
  "Lon": 13.4050
}
```

**Important:**
- `SensorId`: Must be unique (e.g., `eur-11`, `asi-02`, `nam-01`)
- `Url`: Use the full API URL with the box ID
- `Lat`/`Lon`: Coordinates from the sensor details
- `PollIntervalSeconds`: 20 seconds recommended (can adjust based on load)

### Step 4: Rebuild and Restart

```powershell
# Build the image
docker-compose -f infra/compose/docker-compose.yml build gateway-poller

# Restart the container
docker-compose -f infra/compose/docker-compose.yml restart gateway-poller

# Verify sensors are polling
docker logs tk-gateway-poller --tail 30
```

Look for log lines like:
```
[INFO] Poll completed: sensorId=your-sensor-id, httpStatus=200, durationMs=...
```

### Step 5: Verify in Web UI

1. Open http://localhost:5000/sensors
2. Check that your new sensor appears in the map
3. Click on the sensor to see details and latest measurements

## Calculating Ingestion Rate

**Formula:** `Number of sensors × (60 / PollIntervalSeconds) = Events per minute`

**Examples:**
- 10 sensors @ 20s = 10 × 3 = 30 events/min = 1,800 events/hour
- 25 sensors @ 20s = 25 × 3 = 75 events/min = 4,500 events/hour
- 50 sensors @ 20s = 50 × 3 = 150 events/min = 9,000 events/hour
- 90 sensors @ 20s = 90 × 3 = 270 events/min = 16,200 events/hour

## Tips for PostgreSQL Load Testing

1. **Start small:** Add 5-10 sensors at a time
2. **Monitor performance:** Check Grafana dashboards at http://localhost:3000
3. **Watch for errors:** Use `docker logs tk-ingest-consumer --follow`
4. **Check database:** Monitor connection pool, query duration, disk I/O
5. **Adjust polling:** If system struggles, increase `PollIntervalSeconds` (e.g., 30 or 60)

## Troubleshooting

### Sensor returns 404 Not Found
- The box ID doesn't exist or sensor was removed
- Verify the ID on https://opensensemap.org/explore/[BOX_ID]

### Sensor returns 502/503 errors
- OpenSenseMap API may be temporarily unavailable
- These are logged as WARNING, polling will retry

### No data appearing in database
- Check RabbitMQ is running: `docker ps | grep rabbitmq`
- Check consumer logs: `docker logs tk-ingest-consumer`
- Verify PostgreSQL connection: `docker logs tk-postgres`

## Alternative: Global Sensor Coverage

If you need truly global coverage and OpenSenseMap doesn't have sensors in your target regions, consider:

1. **Sensor.Community** (luftdaten.info successor, similar API)
2. **PurpleAir** (air quality sensors, primarily US-based)
3. **USGS Water Services** (already integrated for groundwater data)
4. **Custom integrations** with regional sensor networks

---

**Last Updated:** February 7, 2026
