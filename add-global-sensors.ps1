# Script to fetch 90 real OpenSenseMap sensors and add them to appsettings.json

$ErrorActionPreference = "Stop"

Write-Host "Fetching 90 real OpenSenseMap sensors..."

try {
    $allSensors = @()
    $skip = 0
    
    # Fetch 5 batches of 20 sensors
    for ($i = 0; $i -lt 5; $i++) {
        Write-Host "  Batch $($i+1)/5..."
        $uri = "https://api.opensensemap.org/boxes?limit=20&skip=$skip&minimal=true"
        $response = Invoke-RestMethod -Uri $uri -Method Get
        
        if ($response.data) {
            $sensors = $response.data | Where-Object { $null -ne $_.LOC }
            $allSensors += $sensors
            Write-Host "    - Got $($sensors.Count) sensors (total: $($allSensors.Count))"
        }
        
        $skip += 20
        Start-Sleep -Seconds 1
    }
    
    Write-Host "`nTotal fetched: $($allSensors.Count) sensors"
    
    # Take first 90
    $selectedSensors = $allSensors[0..89]
    Write-Host "Using first 90 sensors"
    
    # Load current config
    $configPath = "src/Gateway.Poller/appsettings.json"
    $config = Get-Content $configPath | ConvertFrom-Json
    
    # Create Scenario2 stations
    $scenario2Stations = @()
    for ($i = 0; $i -lt $selectedSensors.Count; $i++) {
        $sensor = $selectedSensors[$i]
        $sensorId = "global-{0:D3}" -f ($i + 1)
        
        $station = @{
            SensorId = $sensorId
            Name = $sensor.name
            Url = "https://api.opensensemap.org/boxes/$($sensor.id)"
            PollIntervalSeconds = 20
            Lat = [double]$sensor.LOC[1]
            Lon = [double]$sensor.LOC[0]
        }
        
        $scenario2Stations += $station
    }
    
    # Add to config
    $config | Add-Member -Name "Scenario2" -Value @{ Stations = $scenario2Stations } -MemberType NoteProperty -Force
    
    # Save
    $config | ConvertTo-Json -Depth 10 | Out-File -FilePath $configPath -Encoding UTF8
    
    Write-Host "`nSuccess! Updated appsettings.json"
    Write-Host ""
    Write-Host "Configuration Summary:"
    Write-Host "  Scenario1: 10 sensors - 60 second intervals"
    Write-Host "    600 events per hour"
    Write-Host ""
    Write-Host "  Scenario2: 90 sensors - 20 second intervals"  
    Write-Host "    16,200 events per hour"
    Write-Host ""
    Write-Host "  Combined (both scenarios):"
    Write-Host "    16,800 events per hour"
    Write-Host "    403,200 events per day"
    Write-Host ""
    Write-Host "First 10 sensors in Scenario2:"
    for ($i = 0; $i -lt 10; $i++) {
        $s = $scenario2Stations[$i]
        Write-Host "  $($s.SensorId) - $($s.Name) (interval: $($s.PollIntervalSeconds)s)"
    }
    
} catch {
    $errorMsg = $_.ToString()
    Write-Host "ERROR: $errorMsg"
    exit 1
}
