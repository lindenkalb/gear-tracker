namespace GearTracker.Api.Models;

/// <summary>
/// Why something was replaced. Only WornOut replacements will be used to learn how
/// long things really last (Phase 6), since early or damaged replacements would drag
/// the average lifespan down.
/// </summary>
public enum ReplacementReason
{
    // It used up its life.
    WornOut,
    // It broke or got cut before wearing out.
    Damaged,
    // It still had life left, like fresh shoes before a race.
    ReplacedEarly,
    Other
}

/// <summary>
/// A record of replacing a worn item: when, why, and what the new one cost. It points
/// at either a whole piece of gear (shoes, skis) or a part (brake pads, a tire), never
/// both. A check constraint in AppDbContext enforces that rule in the database itself.
/// </summary>
public class Replacement
{
    // Primary key, numbered automatically by Postgres.
    public int Id { get; set; }

    // Set when a whole piece of gear was replaced, like a pair of shoes.
    // Both are nullable (?) because the replacement may be about a part instead.
    public int? GearId { get; set; }
    public Gear? Gear { get; set; }

    // Set when a part was replaced, like a set of brake pads.
    public int? ComponentId { get; set; }
    public Component? Component { get; set; }

    // When it was replaced, in UTC.
    public DateTime ReplacedAt { get; set; }

    public ReplacementReason Reason { get; set; }

    // What the new one cost, for cost-per-mile later. decimal (not double) stores
    // money exactly, so $89.99 stays $89.99. Postgres stores it as "numeric".
    public decimal? Cost { get; set; }

    // Anything worth remembering, like "front pads worn down to the metal."
    public string? Notes { get; set; }
}