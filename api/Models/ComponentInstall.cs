namespace GearTracker.Api.Models;

/// <summary>
/// One install window: a stretch of time that one part spent on one piece of gear.
/// Moving a part to another bike, or taking it off and putting it back on, adds a
/// new row, and the old rows stay. That's how a part keeps an exact history.
/// </summary>
public class ComponentInstall
{
    // Primary key. An install needs its own Id (instead of using the part + gear pair)
    // because the same part can go on the same bike more than once.
    public int Id { get; set; }

    // The part that was installed. ComponentId is the actual column in the table
    // (a foreign key pointing at the Components table). Component is the linked
    // object, which EF Core fills in when it loads the row.
    // "= null!" tells the compiler "this won't be null by the time it's used."
    public int ComponentId { get; set; }
    public Component Component { get; set; } = null!;

    // The gear the part was on, stored the same way: the GearId column plus the
    // linked Gear object.
    public int GearId { get; set; }
    public Gear Gear { get; set; } = null!;

    // When the part went on, in UTC. Workouts that start at or after this count.
    public DateTime InstalledAt { get; set; }

    // Null while the component is still on the bike.
    // Workouts that start before this count. A workout that starts exactly at
    // RemovedAt counts for whatever was installed next instead.
    // A unique index in AppDbContext allows only one install per part with this
    // still null, so a part can never be on two pieces of gear at once.
    public DateTime? RemovedAt { get; set; }
}