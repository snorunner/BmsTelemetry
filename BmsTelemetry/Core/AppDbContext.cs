using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<TelemetryRecord> Telemetry { get; set; } = null!;
    public DbSet<TelemetrySnapshotRecord> TelemetrySnapshot { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Telemetry (live table)
        modelBuilder.Entity<TelemetryRecord>(entity =>
        {
            entity.ToTable("Telemetry");

            entity.HasKey(e => new { e.Ip, e.DeviceKey, e.DataKey });

            entity.Property(e => e.Ip).IsRequired();
            entity.Property(e => e.DeviceKey).IsRequired();
            entity.Property(e => e.DataKey).IsRequired();
            entity.Property(e => e.DataValue).IsRequired();
            entity.Property(e => e.Source).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
        });

        // Snapshot table
        modelBuilder.Entity<TelemetrySnapshotRecord>(entity =>
        {
            entity.ToTable("TelemetrySnapshot");

            entity.HasKey(e => new { e.Ip, e.DeviceKey, e.DataKey });

            entity.Property(e => e.Ip).IsRequired();
            entity.Property(e => e.DeviceKey).IsRequired();
            entity.Property(e => e.DataKey).IsRequired();
            entity.Property(e => e.DataValue).IsRequired();
            entity.Property(e => e.Source).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
        });
    }
}
