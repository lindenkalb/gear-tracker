namespace GearTracker.Api.Models;

/// <summary>
/// What kind of part a component is. Parts get installed on gear: brake pads on a
/// bike, insoles in shoes, bindings on skis.
/// </summary>
public enum ComponentKind
{
    Sole,
    Insole,
    Cleats,
    SkiBindings,
    BootLiners,
    ClimbingSkins,
    FrontBrakePads,
    RearBrakePads,
    Chain,
    Cassette,
    FrontTire,
    RearTire,
    Fork,
    RearShock,
    FrontShock,
    Other
}

/// <summary>
/// A replaceable part, like a set of brake pads or a tire. A part has no miles of its
/// own: its miles come from the workouts of whatever gear it was installed on, during
/// each of its install windows (see ComponentInstall and
/// WearService.GetComponentMetersAsync).
/// </summary>
public class Component
{
    // Primary key, numbered automatically by Postgres.
    public int Id { get; set; }

    // What you call it, like "SRAM Code pads, front".
    public string Name { get; set; } = "";

    public ComponentKind Kind { get; set; }

    // Replace at this distance, in meters. Null for parts measured another way,
    // like suspension by ride hours (that comes with the wear models in Phase 6).
    public double? ReplaceAtMeters { get; set; }

    // Set when the part is thrown away. Its history stays.
    public DateOnly? RetiredOn { get; set; }

    // Every install window this part has had. Each one is a stretch of time
    // on one piece of gear.
    public List<ComponentInstall> Installs { get; set; } = [];
}