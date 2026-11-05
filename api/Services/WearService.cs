using Microsoft.EntityFrameworkCore;

namespace GearTracker.Api.Services;

/// <summary>
/// Answers "how worn is this?" by adding up distance from the workouts every time it's
/// asked. No mileage counter is stored anywhere, so totals stay correct even after
/// workouts are deleted or gear is reassigned. It only reads data and never changes it.
/// </summary>
public class WearService(AppDbContext db)
{
    // "db" comes from the primary constructor above. ASP.NET passes in an AppDbContext
    // when it creates a WearService (a test passes in its own), and every method below
    // uses it to reach the database.

    /// <summary>
    /// Total meters on a piece of gear, like a pair of shoes: the distance of every
    /// workout that used it and hasn't been deleted.
    /// </summary>
    public async Task<double> GetGearMetersAsync(int gearId)
    {
        // Each Where keeps only the workouts that pass its rule, and stacked Wheres
        // must all pass. Any() checks whether at least one gear in the workout's list
        // has this Id. Nothing touches the database until SumAsync: it turns the whole
        // query into SQL, runs it in Postgres, and returns the total (0 if none match).
        return await db.Workouts
            .Where(w => w.DeletedAt == null)
            .Where(w => w.GearUsed.Any(g => g.Id == gearId))
            .SumAsync(w => w.DistanceMeters);
    }

    /// <summary>
    /// Total meters on a part, across every install window it has had. A workout counts
    /// toward a window if it used that window's gear and started inside the window: at or
    /// after InstalledAt, and before RemovedAt (or any time after, if it's still installed).
    /// </summary>
    public async Task<double> GetComponentMetersAsync(int componentId)
    {
        // Load every install window for this part into memory (usually only a few rows).
        var installs = await db.ComponentInstalls
            .Where(i => i.ComponentId == componentId)
            .ToListAsync();

        // The running total across all windows. It's declared outside the loop,
        // so it isn't reset on each pass.
        double total = 0;

        // Add up each window separately, then combine them. That's what lets a part that
        // moved between bikes, or came off and went back on, keep exactly its own miles.
        foreach (var install in installs)
        {
            // The same query as GetGearMetersAsync, plus the time window. The start uses
            // >= and the end uses <, so a workout that starts at the exact moment of a swap
            // counts for the new part only. (A range like that is a "half-open interval.")
            // When RemovedAt is null, the left side of || is true, so the window has no end.
            double windowMeters = await db.Workouts
                .Where(w => w.DeletedAt == null)
                .Where(w => w.GearUsed.Any(g => g.Id == install.GearId))
                .Where(w => w.StartTime >= install.InstalledAt)
                .Where(w => install.RemovedAt == null || w.StartTime < install.RemovedAt)
                .SumAsync(w => w.DistanceMeters);

            total += windowMeters;
        }

        return total;
    }
}