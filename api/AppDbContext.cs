using Microsoft.EntityFrameworkCore;
using GearTracker.Api.Models;

namespace GearTracker.Api;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<Gear> Gear => Set<Gear>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<ComponentInstall> ComponentInstalls => Set<ComponentInstall>();
    public DbSet<Replacement> Replacements => Set<Replacement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComponentInstall>()
            .HasIndex(ci => ci.ComponentId)
            .IsUnique()
            .HasFilter("\"RemovedAt\" IS NULL");

        modelBuilder.Entity<Replacement>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_Replacements_GearOrComponent",
                "(\"GearId\" IS NULL) <> (\"ComponentId\" IS NULL)"));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable text ("Run") instead of numbers (0).
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }
}
