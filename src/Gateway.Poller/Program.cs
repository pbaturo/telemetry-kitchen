using Gateway.Poller.Metrics;
using Gateway.Poller.Options;
using Gateway.Poller.Publishing;
using Gateway.Poller.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    try
    {
        configuration.ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId();
        
        Console.WriteLine("[Serilog] Configured with Console and Loki sinks");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Serilog] Configuration error: {ex.Message}");
        throw;
    }
});

// Add services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IRabbitMqPublisher>(sp =>
    new RabbitMqPublisher(builder.Configuration, sp.GetRequiredService<ILogger<RabbitMqPublisher>>()));
builder.Services.AddHostedService<PollerService>();
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.Configure<Scenario1Config>(builder.Configuration.GetSection("Scenario1"));

var app = builder.Build();

// Map Prometheus metrics endpoint
app.MapMetrics();

await app.RunAsync();
