#!/usr/bin/env pwsh

# Seed test data into PostgreSQL for the telemetry kitchen lab

$connectionString = "Host=localhost;Port=5432;Database=telemetry_kitchen;Username=tk;Password=tk"

# Test sensor configurations
$sensors = @(
    @{sensorId="lab-temp-01"; name="Lab Temperature A"; lat=52.5200; lon=13.4050; sourceType="synthetic-lab"},
    @{sensorId="lab-temp-02"; name="Lab Temperature B"; lat=52.5210; lon=13.4060; sourceType="synthetic-lab"},
    @{sensorId="lab-humidity-01"; name="Lab Humidity A"; lat=52.5190; lon=13.4040; sourceType="synthetic-lab"},
    @{sensorId="lab-pressure-01"; name="Lab Pressure Sensor"; lat=52.5200; lon=13.4050; sourceType="synthetic-lab"}
)

$statusLevels = @("INFO", "INFO", "INFO", "WARN")
$measurements = @(
    @(@{name="temperature"; value="22.5"; unit="°C"}),
    @(@{name="temperature"; value="23.1"; unit="°C"}),
    @(@{name="humidity"; value="45.8"; unit="%"}),
    @(@{name="pressure"; value="1013.25"; unit="hPa"})
)

Write-Host "Seeding test data into telemetry_kitchen database..." -ForegroundColor Green

$conn = New-Object Npgsql.NpgsqlConnection $connectionString
$conn.Open()

try {
    $baseTime = (Get-Date).AddHours(-1)
    
    for ($i = 0; $i -lt $sensors.Count; $i++) {
        $sensor = $sensors[$i]
        $statusLevel = $statusLevels[$i]
        $meas = $measurements[$i]
        
        # Insert sensor record
        $sensorCmd = $conn.CreateCommand()
        $sensorCmd.CommandText = @"
        INSERT INTO sensors (sensor_id, source_type, display_name, lat, lon, created_at, updated_at)
        VALUES (@sensorId, @sourceType, @displayName, @lat, @lon, now(), now())
        ON CONFLICT (sensor_id) DO UPDATE SET updated_at = now()
"@
        $sensorCmd.Parameters.AddWithValue("sensorId", $sensor.sensorId) | Out-Null
        $sensorCmd.Parameters.AddWithValue("sourceType", $sensor.sourceType) | Out-Null
        $sensorCmd.Parameters.AddWithValue("displayName", $sensor.name) | Out-Null
        $sensorCmd.Parameters.AddWithValue("lat", $sensor.lat) | Out-Null
        $sensorCmd.Parameters.AddWithValue("lon", $sensor.lon) | Out-Null
        $sensorCmd.ExecuteNonQuery() | Out-Null
        
        # Insert 6 events per sensor (one every 10 minutes for 1 hour)
        for ($j = 0; $j -lt 6; $j++) {
            $eventTime = $baseTime.AddMinutes($j * 10)
            $eventId = [System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes("$($sensor.sensorId)|$($eventTime:O)|payload")) | ForEach-Object { $_.ToString("x2") } | Join-String
            
            $measJson = ConvertTo-Json $meas -Compress
            
            $eventCmd = $conn.CreateCommand()
            $eventCmd.CommandText = @"
            INSERT INTO sensor_events 
            (event_id, sensor_id, source_type, payload_type, payload_size_b, 
             observed_at, received_at, status_level, status_message, measurements)
            VALUES 
            (@eventId, @sensorId, @sourceType, @payloadType, @payloadSizeB,
             @observedAt, @receivedAt, @statusLevel, @statusMessage, @measurements::jsonb)
            ON CONFLICT (event_id) DO NOTHING
"@
            $eventCmd.Parameters.AddWithValue("eventId", $eventId) | Out-Null
            $eventCmd.Parameters.AddWithValue("sensorId", $sensor.sensorId) | Out-Null
            $eventCmd.Parameters.AddWithValue("sourceType", $sensor.sourceType) | Out-Null
            $eventCmd.Parameters.AddWithValue("payloadType", "json") | Out-Null
            $eventCmd.Parameters.AddWithValue("payloadSizeB", [System.Text.Encoding]::UTF8.GetByteCount($measJson)) | Out-Null
            $eventCmd.Parameters.AddWithValue("observedAt", $eventTime) | Out-Null
            $eventCmd.Parameters.AddWithValue("receivedAt", [System.DateTime]::UtcNow) | Out-Null
            $eventCmd.Parameters.AddWithValue("statusLevel", $statusLevel) | Out-Null
            $eventCmd.Parameters.AddWithValue("statusMessage", "Test data") | Out-Null
            $eventCmd.Parameters.AddWithValue("measurements", $measJson) | Out-Null
            $eventCmd.ExecuteNonQuery() | Out-Null
        }
        
        Write-Host "✓ Inserted $($sensor.name) ($($sensor.sensorId)) with 6 events" -ForegroundColor Cyan
    }
    
    Write-Host ""
    Write-Host "Test data seeding complete!" -ForegroundColor Green
    
    # Verify
    $verifyCmd = $conn.CreateCommand()
    $verifyCmd.CommandText = "SELECT COUNT(*) FROM sensor_events"
    $eventCount = $verifyCmd.ExecuteScalar()
    
    Write-Host "Total events in database: $eventCount" -ForegroundColor Yellow
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    $conn.Close()
}
