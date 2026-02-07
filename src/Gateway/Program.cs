using Gateway.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Configuration
builder.Services.Configure<RabbitMqConfiguration>(
    builder.Configuration.GetSection("RabbitMQ"));
builder.Services.Configure<AzuriteConfiguration>(
    builder.Configuration.GetSection("Azurite"));
builder.Services.Configure<PollingConfiguration>(
    builder.Configuration.GetSection("Polling"));

// Register services
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<IBlobStorage, AzuriteBlobStorage>();
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddHostedService<SensorPollingService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.UseHttpMetrics();

app.MapControllers();
app.MapMetrics();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));

app.Run();

// Configuration classes
public class RabbitMqConfiguration
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public class AzuriteConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class PollingConfiguration
{
    public int IntervalSeconds { get; set; } = 60;
}
