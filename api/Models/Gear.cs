namespace GearTracker.Api.Models;

/// <summary>
/// What kind of physical item a piece of gear is. Which activity it's used for is a
/// separate property (DefaultFor), so this list stays short: road shoes are
/// Shoes + Run, and cleats are Shoes + Soccer.
/// </summary>
public enum GearKind
{
    Shoes,
    Boots,
    Bike,
    Skis,
    SkiBoots,
    Bat,
    Other
}

/// <summary>
/// A physical item used in workouts, like a pair of shoes or a bike. Each real item
/// gets its own row, so two pairs of road shoes are two Gear objects with their own miles.
/// Parts that get installed on gear, like brake pads, are Components instead.
/// </summary>
public class Gear
{
    // Primary key, numbered automatically by Postgres.
    public int Id { get; set; }

    // What you call it, like "Pegasus 41 (blue)" or "Stumpjumper".
    public string Name { get; set; } = "";

    public GearKind Kind { get; set; }

    // The activity this gear is used for by default, like Run for road shoes.
    // Several items can share one default: a bike and cycling shoes can both be
    // MountainRide. Null means no default.
    public ActivityType? DefaultFor { get; set; }

    // Replace at this distance, in meters (mostly for shoes).
    // Null if this gear has no distance limit.
    public double? ReplaceAtMeters { get; set; }

    // Set when you stop using it. Retired gear keeps all its history.
    // DateOnly is a date with no time of day, so the UTC rule doesn't apply to it.
    public DateOnly? RetiredOn { get; set; }

    // Every workout that used this gear: the other side of Workout.GearUsed.
    public List<Workout> Workouts { get; set; } = [];

    // Every part ever installed on this gear, past and present (mostly for bikes).
    public List<ComponentInstall> ComponentInstalls { get; set; } = [];
}