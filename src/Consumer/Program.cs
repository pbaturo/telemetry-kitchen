using Consumer.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// Configuration
builder.Services.Configure<RabbitMqConfiguration>(
    builder.Configuration.GetSection("RabbitMQ"));
builder.Services.Configure<DatabaseConfiguration>(
    builder.Configuration.GetSection("Database"));

// Register services
builder.Services.AddSingleton<IDatabaseWriter, PostgresDatabaseWriter>();
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddHostedService<MessageConsumerService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.UseHttpMetrics();

app.MapControllers();
app.MapMetrics();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "consumer" }));

app.Run();

// Configuration classes
public class RabbitMqConfiguration
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public class DatabaseConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
}
