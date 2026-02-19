using Microsoft.EntityFrameworkCore;
using Web.Mvc.Models;

namespace Web.Mvc.Data;

public class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : base(options)
    {
    }

    public DbSet<Sensor> Sensors { get; set; } = null!;
    public DbSet<SensorEvent> SensorEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Sensor configuration
        modelBuilder.Entity<Sensor>(entity =>
        {
            entity.HasKey(s => s.SensorId);
            entity.Property(s => s.SensorId).HasColumnName("sensor_id").HasMaxLength(255);
            entity.Property(s => s.SourceType).HasColumnName("source_type").HasMaxLength(100);
            entity.Property(s => s.DisplayName).HasColumnName("display_name").HasMaxLength(500);
            entity.Property(s => s.Lat).HasColumnName("lat");
            entity.Property(s => s.Lon).HasColumnName("lon");
            entity.Property(s => s.CreatedAt).HasColumnName("created_at");
            entity.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            entity.ToTable("sensors");
        });

        // SensorEvent configuration
        modelBuilder.Entity<SensorEvent>(entity =>
        {
            entity.HasKey(se => se.EventId);
            entity.Property(se => se.EventId).HasColumnName("event_id").HasMaxLength(255);
            entity.Property(se => se.SensorId).HasColumnName("sensor_id").HasMaxLength(255);
            entity.Property(se => se.SourceType).HasColumnName("source_type").HasMaxLength(100);
            entity.Property(se => se.PayloadType).HasColumnName("payload_type").HasMaxLength(50);
            entity.Property(se => se.PayloadSizeB).HasColumnName("payload_size_b");
            entity.Property(se => se.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");

            entity.Property(se => se.ObservedAt).HasColumnName("observed_at");
            entity.Property(se => se.ReceivedAt).HasColumnName("received_at");

            entity.Property(se => se.StatusLevel).HasColumnName("status_level").HasMaxLength(50);
            entity.Property(se => se.StatusMessage).HasColumnName("status_message").HasMaxLength(500);

            entity.Property(se => se.Measurements)
                .HasColumnName("measurements")
                .HasColumnType("jsonb");

            entity.Property(se => se.BlobUri).HasColumnName("blob_uri").HasMaxLength(2048);
            entity.Property(se => se.BlobSha256).HasColumnName("blob_sha256").HasMaxLength(64);
            entity.Property(se => se.BlobBytes).HasColumnName("blob_bytes");

            entity.Property(se => se.InsertedAt).HasColumnName("inserted_at");

            entity.HasOne(se => se.Sensor)
                .WithMany(s => s.Events)
                .HasForeignKey(se => se.SensorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable("sensor_events");
        });
    }
}
