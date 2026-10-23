namespace GearTracker.Api.Models;

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

public class Gear
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public GearKind Kind { get; set; }

    public ActivityType? DefaultFor { get; set; }

    public double? ReplaceAtMeters { get; set; }

    public DateOnly? RetiredOn { get; set; }

    public List<Workout> Workouts { get; set; } = [];

    public List<ComponentInstall> ComponentInstalls { get; set; } = [];
}