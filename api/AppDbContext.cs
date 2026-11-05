using Microsoft.EntityFrameworkCore;
using GearTracker.Api.Models;

namespace GearTracker.Api;

/// <summary>
/// The app's connection to the database. It lists which tables exist (the DbSet
/// properties) and adds the database rules EF Core can't work out from the classes alone.
/// The options (which database, and how to reach it) are passed in through the
/// primary constructor: from AddDbContext in Program.cs in the real app, and from
/// TestDb in the tests.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // One DbSet per table. Each one is how you query that table, for example
    // db.Workouts.Where(...). "=> Set<Workout>()" makes each a read-only property
    // that asks EF Core for that table's set.
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<Gear> Gear => Set<Gear>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<ComponentInstall> ComponentInstalls => Set<ComponentInstall>();
    public DbSet<Replacement> Replacements => Set<Replacement>();

    // EF Core calls this method itself (it overrides DbContext's empty version) when it
    // builds its picture of the database: the first time the app uses the database, and
    // when you run "dotnet ef migrations add". You never call it yourself.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // No duplicate imports: no two workouts can have the same Source and SourceKey.
        // Postgres refuses the insert if they would, and SaveChangesAsync throws a
        // DbUpdateException.
        modelBuilder.Entity<Workout>()
            .HasIndex(w => new { w.Source, w.SourceKey })
            .IsUnique();

        // A part can only be on one piece of gear at a time: at most one install per
        // component with no RemovedAt. The filter makes this rule cover only open
        // installs, so past installs of the same part are still allowed.
        modelBuilder.Entity<ComponentInstall>()
            .HasIndex(ci => ci.ComponentId)
            .IsUnique()
            .HasFilter("\"RemovedAt\" IS NULL");

        // A replacement points at exactly one thing, gear or a part, never both or neither.
        // In SQL, "<>" means "not equal", so the rule is: exactly one of the two IDs is null.
        modelBuilder.Entity<Replacement>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_Replacements_GearOrComponent",
                "(\"GearId\" IS NULL) <> (\"ComponentId\" IS NULL)"));
    }

    // EF Core calls this before OnModelCreating, for rules that apply across every table.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable text ("Run") instead of numbers (0).
        // This also means reordering an enum later can't change what old rows mean.
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }
}
