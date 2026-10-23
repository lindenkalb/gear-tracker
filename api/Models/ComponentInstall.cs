namespace GearTracker.Api.Models;

public class ComponentInstall
{
    public int Id { get; set; }

    public int ComponentId { get; set; }
    public Component Component { get; set; } = null!;

    public int GearId { get; set; }
    public Gear Gear { get; set; } = null!;

    public DateTime InstalledAt { get; set; }

    // Null while the component is still on the bike.
    public DateTime? RemovedAt { get; set; }
}