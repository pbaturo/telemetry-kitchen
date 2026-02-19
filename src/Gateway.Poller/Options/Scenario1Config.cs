namespace Gateway.Poller.Options;

public class Scenario1Config
{
    public List<StationConfig> Stations { get; set; } = new();
}

public class StationConfig
{
    public required string SensorId { get; set; }
    public required string Name { get; set; }
    public required string Url { get; set; }
    public string SourceType { get; set; } = "opensensemap";
    public int PollIntervalSeconds { get; set; } = 60;
    public double Lat { get; set; }
    public double Lon { get; set; }
    public long? LocationId { get; set; }
}
