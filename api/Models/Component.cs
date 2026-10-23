namespace GearTracker.Api.Models;

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

public class Component
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public ComponentKind Kind { get; set; }

    public double? ReplaceAtMeters { get; set; }

    public DateOnly? RetiredOn { get; set; }

    public List<ComponentInstall> Installs { get; set; } = [];
}