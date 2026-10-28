using Microsoft.EntityFrameworkCore;

namespace GearTracker.Api.Services;

public class WearService(AppDbContext db)
{
    public async Task<double> GetGearMetersAsync(int gearId)
    {
        return await db.Workouts
            .Where(w => w.DeletedAt == null)
            .Where(w => w.GearUsed.Any(g => g.Id == gearId))
            .SumAsync(w => w.DistanceMeters);
    }

    public async Task<double> GetComponentMetersAsync(int componentId)
    {
        var installs = await db.ComponentInstalls
            .Where(i => i.ComponentId == componentId)
            .ToListAsync();

        double total = 0;

        foreach (var install in installs)
        {
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