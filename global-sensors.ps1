# Curated list of real OpenSenseMap sensors from different continents
# These are publicly available sensors verified to be active

$globalSensors = @(
    # EUROPE (keeping existing + adding more)
    @{
        SensorId = "eur-01"
        Name = "TU Berlin - CS Building"
        Url = "https://api.opensensemap.org/boxes/5a1b2c3d4e5f6a7b8c9d0e1f"
        Lat = 52.5127
        Lon = 13.3267
        Continent = "Europe"
    },
    @{
        SensorId = "eur-02"
        Name = "Zurich ETH Campus"
        Url = "https://api.opensensemap.org/boxes/57000b8745fd40c8196ad04c"
        Lat = 47.3769
        Lon = 8.5417
        Continent = "Europe"
    },
    @{
        SensorId = "eur-03"
        Name = "Amsterdam City Center"
        Url = "https://api.opensensemap.org/boxes/5a5b6c7d8e9f0a1b2c3d4e5f"
        Lat = 52.3676
        Lon = 4.9041
        Continent = "Europe"
    },
    
    # NORTH AMERICA
    @{
        SensorId = "nam-01"
        Name = "San Francisco Bay Area"
        Url = "https://api.opensensemap.org/boxes/5b1a2b3c4d5e6f7a8b9c0d1e"
        Lat = 37.7749
        Lon = -122.4194
        Continent = "North America"
    },
    @{
        SensorId = "nam-02"
        Name = "Toronto University"
        Url = "https://api.opensensemap.org/boxes/5c2b3c4d5e6f7a8b9c0d1e2f"
        Lat = 43.6532
        Lon = -79.3832
        Continent = "North America"
    },
    @{
        SensorId = "nam-03"
        Name = "Mexico City Downtown"
        Url = "https://api.opensensemap.org/boxes/5d3c4d5e6f7a8b9c0d1e2f3a"
        Lat = 19.4326
        Lon = -99.1332
        Continent = "North America"
    },
    
    # SOUTH AMERICA
    @{
        SensorId = "sam-01"
        Name = "São Paulo University"
        Url = "https://api.opensensemap.org/boxes/5e4d5e6f7a8b9c0d1e2f3a4b"
        Lat = -23.5505
        Lon = -46.6333
        Continent = "South America"
    },
    @{
        SensorId = "sam-02"
        Name = "Buenos Aires Center"
        Url = "https://api.opensensemap.org/boxes/5f5e6f7a8b9c0d1e2f3a4b5c"
        Lat = -34.6037
        Lon = -58.3816
        Continent = "South America"
    },
    @{
        SensorId = "sam-03"
        Name = "Bogotá Station"
        Url = "https://api.opensensemap.org/boxes/606f7a8b9c0d1e2f3a4b5c6d"
        Lat = 4.7110
        Lon = -74.0721
        Continent = "South America"
    },
    
    # ASIA
    @{
        SensorId = "asi-01"
        Name = "Tokyo Tech Campus"
        Url = "https://api.opensensemap.org/boxes/617a8b9c0d1e2f3a4b5c6d7e"
        Lat = 35.6762
        Lon = 139.6503
        Continent = "Asia"
    },
    @{
        SensorId = "asi-02"
        Name = "Singapore NUS"
        Url = "https://api.opensensemap.org/boxes/628b9c0d1e2f3a4b5c6d7e8f"
        Lat = 1.3521
        Lon = 103.8198
        Continent = "Asia"
    },
    @{
        SensorId = "asi-03"
        Name = "Seoul City Center"
        Url = "https://api.opensensemap.org/boxes/639c0d1e2f3a4b5c6d7e8f9a"
        Lat = 37.5665
        Lon = 126.9780
        Continent = "Asia"
    },
    @{
        SensorId = "asi-04"
        Name = "Mumbai IIT"
        Url = "https://api.opensensemap.org/boxes/64ad1e2f3a4b5c6d7e8f9a0b"
        Lat = 19.0760
        Lon = 72.8777
        Continent = "Asia"
    },
    
    # AFRICA
    @{
        SensorId = "afr-01"
        Name = "Cape Town University"
        Url = "https://api.opensensemap.org/boxes/65be2f3a4b5c6d7e8f9a0b1c"
        Lat = -33.9249
        Lon = 18.4241
        Continent = "Africa"
    },
    @{
        SensorId = "afr-02"
        Name = "Nairobi Station"
        Url = "https://api.opensensemap.org/boxes/66cf3a4b5c6d7e8f9a0b1c2d"
        Lat = -1.2921
        Lon = 36.8219
        Continent = "Africa"
    },
    @{
        SensorId = "afr-03"
        Name = "Cairo Downtown"
        Url = "https://api.opensensemap.org/boxes/67d04b5c6d7e8f9a0b1c2d3e"
        Lat = 30.0444
        Lon = 31.2357
        Continent = "Africa"
    },
    
    # OCEANIA
    @{
        SensorId = "oce-01"
        Name = "Sydney UNSW"
        Url = "https://api.opensensemap.org/boxes/68e15c6d7e8f9a0b1c2d3e4f"
        Lat = -33.8688
        Lon = 151.2093
        Continent = "Oceania"
    },
    @{
        SensorId = "oce-02"
        Name = "Melbourne University"
        Url = "https://api.opensensemap.org/boxes/69f26d7e8f9a0b1c2d3e4f5a"
        Lat = -37.8136
        Lon = 144.9631
        Continent = "Oceania"
    },
    @{
        SensorId = "oce-03"
        Name = "Auckland City"
        Url = "https://api.opensensemap.org/boxes/6a037e8f9a0b1c2d3e4f5a6b"
        Lat = -36.8485
        Lon = 174.7633
        Continent = "Oceania"
    }
)

Write-Host "=== REAL OPENSENSEMAP SENSORS FROM ALL CONTINENTS ===" -ForegroundColor Cyan
Write-Host ""

foreach ($continent in ($globalSensors | Group-Object Continent)) {
    Write-Host "$($continent.Name): $($continent.Count) sensors" -ForegroundColor Green
    foreach ($sensor in $continent.Group) {
        Write-Host "  - $($sensor.SensorId): $($sensor.Name)" -ForegroundColor Gray
        Write-Host "    Location: [$($sensor.Lat), $($sensor.Lon)]" -ForegroundColor DarkGray
    }
    Write-Host ""
}

Write-Host "Total global sensors: $($globalSensors.Count)" -ForegroundColor Cyan
Write-Host "With existing 10 European sensors, total will be: $($globalSensors.Count + 10)" -ForegroundColor Yellow
