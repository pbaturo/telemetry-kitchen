# Find Active OpenSenseMap Stations
# This script queries the OpenSenseMap API to find stations with recent measurements

param(
    [int]$Count = 10,
    [int]$DaysRecent = 7
)

Write-Host "Searching for $Count active OpenSenseMap stations with measurements in the last $DaysRecent days..." -ForegroundColor Cyan

try {
    # Query for boxes with recent activity
    $since = (Get-Date).AddDays(-$DaysRecent).ToString("yyyy-MM-ddTHH:mm:ssZ")
    $apiUrl = "https://api.opensensemap.org/boxes?format=json"
    
    Write-Host "Fetching stations from OpenSenseMap API..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri $apiUrl -Method Get -TimeoutSec 30
    
    if ($response -is [array]) {
        $activeStations = $response | Where-Object {
            $_.lastMeasurementAt -and 
            ([DateTime]$_.lastMeasurementAt) -gt (Get-Date).AddDays(-$DaysRecent)
        } | Select-Object -First $Count
        
        Write-Host "`nFound $($activeStations.Count) active stations:`n" -ForegroundColor Green
        
        $index = 1
        foreach ($station in $activeStations) {
            $coords = $station.currentLocation.coordinates
            $lon = $coords[0]
            $lat = $coords[1]
            $lastMeasurement = if ($station.lastMeasurementAt) { ([DateTime]$station.lastMeasurementAt).ToString("yyyy-MM-dd HH:mm") } else { "Never" }
            
            Write-Host "Station $index : $($station.name)" -ForegroundColor White
            Write-Host "  ID: $($station._id)" -ForegroundColor Gray
            Write-Host "  URL: https://api.opensensemap.org/boxes/$($station._id)" -ForegroundColor Gray
            Write-Host "  Coordinates: Lat=$lat, Lon=$lon" -ForegroundColor Gray
            Write-Host "  Last Measurement: $lastMeasurement" -ForegroundColor Gray
            Write-Host ""
            
            $index++
        }
        
        Write-Host "`nJSON Configuration (copy to appsettings.json):" -ForegroundColor Cyan
        Write-Host "================================================================================`n" -ForegroundColor Cyan
        
        $jsonStations = @()
        $index = 1
        foreach ($station in $activeStations) {
            $coords = $station.currentLocation.coordinates
            $jsonStations += @{
                SensorId = "env-$('{0:D2}' -f $index)"
                Name = $station.name
                Url = "https://api.opensensemap.org/boxes/$($station._id)"
                PollIntervalSeconds = 60
                Lat = [math]::Round($coords[1], 6)
                Lon = [math]::Round($coords[0], 6)
            }
            $index++
        }
        
        $config = @{
            Scenario1 = @{
                Stations = $jsonStations
            }
        }
        
        $json = $config | ConvertTo-Json -Depth 10
        Write-Host $json -ForegroundColor White
        Write-Host "`n================================================================================" -ForegroundColor Cyan
        
    } else {
        Write-Host "Unexpected API response format" -ForegroundColor Red
    }
}
catch {
    Write-Host "Error querying OpenSenseMap API: $_" -ForegroundColor Red
    Write-Host "`nAlternative: Manually browse stations at https://opensensemap.org/explore" -ForegroundColor Yellow
    Write-Host "Click on active stations (green markers) and copy their box IDs from the URL" -ForegroundColor Yellow
}
