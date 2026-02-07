using Serilog;
using Web.Mvc.Data;
using Web.Mvc.Repositories;
using Prometheus;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId();
});

// Add services to the container
builder.Services.AddDbContext<TelemetryDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL") 
        ?? "Host=postgres;Port=5432;Database=telemetry;Username=telemetry;Password=telemetry"));

builder.Services.AddScoped<ISensorRepository, SensorRepository>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();

// Add Prometheus metrics
builder.Services.AddSingleton<ICollectorRegistry>(Metrics.DefaultRegistry);

var app = builder.Build();

// Prometheus metric endpoint
app.MapMetrics();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Sensors}/{action=Index}/{id?}");

app.MapRazorPages();

// Verify database connection
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    try
    {
        await db.Database.CanConnectAsync();
        app.Logger.LogInformation("✓ Database connection verified");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "✗ Database connection failed");
    }
}

app.Run();
