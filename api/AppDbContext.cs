using Microsoft.EntityFrameworkCore;
using GearTracker.Api.Models;

namespace GearTracker.Api;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<Gear> Gear => Set<Gear>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<ComponentInstall> ComponentInstalls => Set<ComponentInstall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComponentInstall>()
            .HasIndex(ci => ci.ComponentId)
            .IsUnique()
            .HasFilter("\"RemovedAt\" IS NULL");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable text ("Run") instead of numbers (0).
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }
}
