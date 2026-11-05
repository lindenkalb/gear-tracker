using GearTracker.Api.Models;
using GearTracker.Api.Services;

namespace GearTracker.Api.Tests;

public class WearServiceTests
{
    private static DateTime Utc(int month, int day, int hour = 12) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static Workout NewWorkout(ActivityType type, Gear gear, DateTime start, double km) => new()
    {
        Type = type,
        StartTime = start,
        EndTime = start.AddHours(1),
        Duration = TimeSpan.FromHours(1),
        DistanceMeters = km * 1000,
        Source = WorkoutSource.HealthExport,
        SourceKey = Guid.NewGuid().ToString(),
        GearUsed = [gear],
    };

    [Fact]
    public async Task Deleted_workouts_do_not_count()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        var shoes = new Gear { Name = "Road Shoes", Kind = GearKind.Shoes };
        var kept = NewWorkout(ActivityType.Run, shoes, Utc(10, 1), 5);
        var deleted = NewWorkout(ActivityType.Run, shoes, Utc(10, 2), 3);
        deleted.DeletedAt = Utc(10, 3);
        db.Workouts.AddRange(kept,deleted);
        await db.SaveChangesAsync();

        double meters = await wear.GetGearMetersAsync(shoes.Id);

        Assert.Equal(5000, meters);
    }

    [Fact]
    public async Task Ride_at_swap_time_counts_for_new_pads_only()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        // Arrange: one bike with old pads swapped for new ones on Oct 12 at 14:00
        var bike = new Gear { Name = "Bike 1", Kind = GearKind.Bike };
        var oldPads = new Component { Name = "Old brake pads", Kind = ComponentKind.FrontBrakePads };
        var newPads = new Component { Name = "New brake pads", Kind = ComponentKind.FrontBrakePads };

        db.ComponentInstalls.AddRange(
            new ComponentInstall { Component = oldPads, Gear = bike, InstalledAt = Utc(6, 1), RemovedAt = Utc(10, 12, hour: 14) },
            new ComponentInstall { Component = newPads, Gear = bike, InstalledAt = Utc(10, 12, hour: 14) });

        db.Workouts.AddRange(
            NewWorkout(ActivityType.MountainRide, bike, Utc(10, 12, hour: 9), 20),
            NewWorkout(ActivityType.MountainRide, bike, Utc(10, 12, hour: 14), 10),
            NewWorkout(ActivityType.MountainRide, bike, Utc(10, 12, hour: 17), 15));
        
        await db.SaveChangesAsync();

        double oldMeters = await wear.GetComponentMetersAsync(oldPads.Id);
        double newMeters = await wear.GetComponentMetersAsync(newPads.Id);

        Assert.Equal(20000, oldMeters);
        Assert.Equal(25000, newMeters);
    }
}