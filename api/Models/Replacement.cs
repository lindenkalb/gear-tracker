namespace GearTracker.Api.Models;

public enum ReplacementReason
{
    WornOut,
    Damaged,
    ReplacedEarly,
    Other
}

public class Replacement
{
    public int Id { get; set; }

    public int? GearId { get; set; }
    public Gear? Gear { get; set; }

    public int? ComponentId { get; set; }
    public Component? Component { get; set; }

    public DateTime ReplacedAt { get; set; }

    public ReplacementReason Reason { get; set; }

    public decimal? Cost { get; set; }

    public string? Notes { get; set; }
}