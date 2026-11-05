using GearTracker.Api.Models;
using GearTracker.Api.Services;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

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

    [Fact]
    public async Task Shoe_miles_come_only_from_that_pair()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        var shoes1 = new Gear { Name = "Running Shoes", Kind = GearKind.Shoes };
        var shoes2 = new Gear { Name = "Unused Shoes", Kind = GearKind.Shoes };
        var Workout1 = NewWorkout(ActivityType.Run, shoes1, Utc(10, 10, hour: 10), 10);
        var Workout2 = NewWorkout(ActivityType.Run, shoes2, Utc(10, 10, hour: 10), 2);

        db.Workouts.Add(Workout1);
        db.Workouts.Add(Workout2);
        db.Gear.Add(shoes1);
        db.Gear.Add(shoes2);
        await db.SaveChangesAsync();

        var shoes1Distance = await wear.GetGearMetersAsync(shoes1.Id);
        var shoes2Distance = await wear.GetGearMetersAsync(shoes2.Id);

        Assert.Equal(10000, shoes1Distance);
        Assert.Equal(2000, shoes2Distance);
    }

    [Fact]
    public async Task Tire_moved_between_bikes_keeps_its_miles()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        var bike1 = new Gear { Name = "Old Bike", Kind = GearKind.Bike };
        var bike2 = new Gear { Name = "New Bike", Kind = GearKind.Bike };
        var Tire = new Component { Name = "Front Bike Tire", Kind = ComponentKind.FrontTire };
        
        db.ComponentInstalls.AddRange(
            new ComponentInstall { Component = Tire, Gear = bike1, InstalledAt = Utc(10, 10, hour: 10), RemovedAt = Utc(12, 10, hour: 10) },
            new ComponentInstall { Component = Tire, Gear = bike2, InstalledAt = Utc( 12, 10, hour: 10) });

        await db.SaveChangesAsync();

        var TireDistance1 = await wear.GetComponentMetersAsync(Tire.Id);

        db.Workouts.Add(NewWorkout(ActivityType.MountainRide, bike1, Utc(10, 11, hour: 10), 10));
        
        await db.SaveChangesAsync();

        var TireDistance2 = await wear.GetComponentMetersAsync(Tire.Id);

        // Adding Workouts to the old and new bikes after the uninstall and reinstall
        db.Workouts.Add(NewWorkout(ActivityType.MountainRide, bike1, Utc(12, 11, hour: 10), 11));
        db.Workouts.Add(NewWorkout( ActivityType.MountainRide, bike2, Utc(12, 11, hour: 10), 10));

        await db.SaveChangesAsync();

        var TireDistance3 = await wear.GetComponentMetersAsync(Tire.Id);

        Assert.Equal(0, TireDistance1);
        Assert.Equal(10000, TireDistance2);
        Assert.Equal(20000, TireDistance3);
    }
}