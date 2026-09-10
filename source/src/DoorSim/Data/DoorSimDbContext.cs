namespace DoorSim.Data;

public class DoorSimDbContext(DbContextOptions<DoorSimDbContext> options) : DbContext(options)
{
    public DbSet<CardEntity> Cards => Set<CardEntity>();
    public DbSet<DoorConfigEntity> Doors => Set<DoorConfigEntity>();
    public DbSet<SimulationSettingsEntity> SimulationSettings => Set<SimulationSettingsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CardEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Format).HasConversion<string>();  // stored as string in DB
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
    }
}
