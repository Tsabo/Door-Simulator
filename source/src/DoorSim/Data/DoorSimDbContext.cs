using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DoorSim.Data;

public class DoorSimDbContext(DbContextOptions<DoorSimDbContext> options) : DbContext(options)
{
    // SQLite stores DateTimeOffset as TEXT, which EF cannot translate in range filters or
    // ExecuteDelete. Telemetry timestamps are always UTC, so persist them as ticks instead.
    private static readonly ValueConverter<DateTimeOffset, long> UtcTicksConverter = new(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    public DbSet<CardEntity> Cards => Set<CardEntity>();
    public DbSet<DoorConfigEntity> Doors => Set<DoorConfigEntity>();
    public DbSet<SimulationSettingsEntity> SimulationSettings => Set<SimulationSettingsEntity>();
    public DbSet<SimulationEventEntity> SimulationEvents => Set<SimulationEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CardEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Format).HasConversion<string>(); // stored as string in DB
        });

        modelBuilder.Entity<DoorConfigEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Protocol).IsRequired();
        });

        modelBuilder.Entity<SimulationSettingsEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SimulationEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RawBits).HasMaxLength(256);
            entity.Property(e => e.Error).HasMaxLength(500);
            entity.Property(e => e.Protocol).HasConversion<string>();
            entity.Property(e => e.Kind).HasConversion<string>();
            entity.Property(e => e.Outcome).HasConversion<string>();
            entity.Property(e => e.Format).HasConversion<string>();
            entity.Property(e => e.EnqueuedAt).HasConversion(UtcTicksConverter);
            entity.Property(e => e.StartedAt).HasConversion(UtcTicksConverter);
            entity.Property(e => e.CompletedAt).HasConversion(UtcTicksConverter);
            entity.HasIndex(e => e.CompletedAt);
            entity.HasIndex(e => new { e.DoorId, e.CompletedAt });
        });
    }
}
