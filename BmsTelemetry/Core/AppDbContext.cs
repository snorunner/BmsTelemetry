using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<TelemetryRecord> Telemetry { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TelemetryRecord>(entity =>
        {
            entity.HasKey(e => new { e.Ip, e.DeviceKey, e.DataKey });

            entity.Property(e => e.Ip)
                  .IsRequired();

            entity.Property(e => e.DeviceKey)
                  .IsRequired();

            entity.Property(e => e.DataKey)
                  .IsRequired();

            entity.Property(e => e.DataValue)
                  .IsRequired();

            entity.Property(e => e.Source)
                  .IsRequired();

            entity.Property(e => e.Timestamp)
                  .IsRequired();

            // Useful indexes for telemetry workloads
            // entity.HasIndex(e => e.Timestamp);
            // entity.HasIndex(e => new { e.DeviceKey, e.DataKey });
        });
    }
}
