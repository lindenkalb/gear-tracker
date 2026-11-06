using System.Diagnostics.CodeAnalysis;
using GearTracker.Api.Models;
using GearTracker.Api.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GearTracker.Api.Tests;

/// <summary>
/// Tests for WearService. Each test follows three steps: Arrange (save some gear, parts
/// and workouts), Act (run a wear method), and Assert (compare the answer to a number
/// worked out by hand). Keep every database test in this one class: xUnit runs tests in
/// the same class one at a time, so they never fight over the shared test database.
/// </summary>
public class WearServiceTests
{
    // Helper: a time on the given day of 2026, in UTC. Postgres only accepts UTC times
    // for these columns, so building it in here means no test can forget. The hour
    // defaults to noon; pass hour: only when the time of day matters.
    private static DateTime Utc(int month, int day, int hour = 12) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    // Helper: builds a one-hour workout of the given distance (in km) that used one piece
    // of gear, and fills in the properties tests don't care about. It only creates the
    // object in memory; a test still has to Add it and call SaveChangesAsync. The random
    // SourceKey (a Guid) keeps every workout unique, so the no-duplicates index accepts it.
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

    // [Fact] marks a method as a test, so "dotnet test" finds and runs it.
    // This one checks that a deleted workout doesn't count toward gear miles:
    // 5 km kept + 3 km deleted should give 5,000 meters.
    [Fact]
    public async Task Deleted_workouts_do_not_count()
    {
        // A fresh, empty database for this test. "await using" closes the connection
        // automatically when the test ends.
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        var shoes = new Gear { Name = "Road Shoes", Kind = GearKind.Shoes };
        var kept = NewWorkout(ActivityType.Run, shoes, Utc(10, 1), 5);
        var deleted = NewWorkout(ActivityType.Run, shoes, Utc(10, 2), 3);
        deleted.DeletedAt = Utc(10, 3);
        // Adding the workouts also saves the shoes, because EF Core follows each
        // workout's link to them. Saving is what gives shoes a real Id.
        db.Workouts.AddRange(kept,deleted);
        await db.SaveChangesAsync();

        double meters = await wear.GetGearMetersAsync(shoes.Id);

        // Assert.Equal(expected, actual): the number you expect goes first.
        Assert.Equal(5000, meters);
    }

    // Checks a ride at the exact moment of a pad swap. The 9:00 ride (20 km) counts for
    // the old pads; the 14:00 ride (10 km) and 17:00 ride (15 km) count for the new pads.
    // If the window's end used <= instead of <, the 14:00 ride would count for both.
    [Fact]
    public async Task Ride_at_swap_time_counts_for_new_pads_only()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        // Arrange: one bike with old pads swapped for new ones on Oct 12 at 14:00
        var bike = new Gear { Name = "Bike 1", Kind = GearKind.Bike };
        var oldPads = new Component { Name = "Old brake pads", Kind = ComponentKind.FrontBrakePads };
        var newPads = new Component { Name = "New brake pads", Kind = ComponentKind.FrontBrakePads };

        // The new pads' install has no RemovedAt, so it's null: still on the bike.
        db.ComponentInstalls.AddRange(
            new ComponentInstall { Component = oldPads, Gear = bike, InstalledAt = Utc(6, 1), RemovedAt = Utc(10, 12, hour: 14) },
            new ComponentInstall { Component = newPads, Gear = bike, InstalledAt = Utc(10, 12, hour: 14) });

        db.Workouts.AddRange(
            NewWorkout(ActivityType.MountainRide, bike, Utc(10, 12, hour: 9), 20),
            NewWorkout(ActivityType.MountainRide, bike, Utc(10, 12, hour: 14), 10),
            NewWorkout(ActivityType.MountainRide, bike, Utc(10, 12, hour: 17), 15));

        // bike, oldPads and newPads were never added directly; they get saved because
        // the installs and workouts link to them.
        await db.SaveChangesAsync();

        double oldMeters = await wear.GetComponentMetersAsync(oldPads.Id);
        double newMeters = await wear.GetComponentMetersAsync(newPads.Id);

        Assert.Equal(20000, oldMeters);
        Assert.Equal(25000, newMeters);
    }

    // Checks that each pair of shoes gets only its own runs: 10 km on the first pair and
    // 2 km on the second, with nothing leaking from one pair into the other.
    [Fact]
    public async Task Shoe_miles_come_only_from_that_pair()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        var shoes1 = new Gear { Name = "Running Shoes", Kind = GearKind.Shoes };
        var shoes2 = new Gear { Name = "Unused Shoes", Kind = GearKind.Shoes };
        var workout1 = NewWorkout(ActivityType.Run, shoes1, Utc(10, 10, hour: 10), 10);
        var workout2 = NewWorkout(ActivityType.Run, shoes2, Utc(10, 10, hour: 10), 2);

        db.Workouts.Add(workout1);
        db.Workouts.Add(workout2);
        db.Gear.Add(shoes1);
        db.Gear.Add(shoes2);
        await db.SaveChangesAsync();

        var shoes1Distance = await wear.GetGearMetersAsync(shoes1.Id);
        var shoes2Distance = await wear.GetGearMetersAsync(shoes2.Id);

        Assert.Equal(10000, shoes1Distance);
        Assert.Equal(2000, shoes2Distance);
    }

    // Checks that a tire moved from one bike to another keeps exactly its own miles.
    // The tire is on the old bike Oct 10 - Dec 10, then on the new bike from Dec 10.
    // It checks the total three times, as rides are added:
    //   1. No rides yet: 0.
    //   2. A ride on the old bike inside the tire's window: 10,000.
    //   3. A ride on the old bike after the tire moved (shouldn't count) and a ride
    //      on the new bike (should count): 20,000.
    [Fact]
    public async Task Tire_moved_between_bikes_keeps_its_miles()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        var bike1 = new Gear { Name = "Old Bike", Kind = GearKind.Bike };
        var bike2 = new Gear { Name = "New Bike", Kind = GearKind.Bike };
        var tire = new Component { Name = "Front Bike Tire", Kind = ComponentKind.FrontTire };

        // Two install windows for the same tire, one per bike.
        db.ComponentInstalls.AddRange(
            new ComponentInstall { Component = tire, Gear = bike1, InstalledAt = Utc(10, 10, hour: 10), RemovedAt = Utc(12, 10, hour: 10) },
            new ComponentInstall { Component = tire, Gear = bike2, InstalledAt = Utc( 12, 10, hour: 10) });

        await db.SaveChangesAsync();

        var tireDistance1 = await wear.GetComponentMetersAsync(tire.Id);

        db.Workouts.Add(NewWorkout(ActivityType.MountainRide, bike1, Utc(10, 11, hour: 10), 10));

        await db.SaveChangesAsync();

        var tireDistance2 = await wear.GetComponentMetersAsync(tire.Id);

        // Adding Workouts to the old and new bikes after the uninstall and reinstall
        db.Workouts.Add(NewWorkout(ActivityType.MountainRide, bike1, Utc(12, 11, hour: 10), 11));
        db.Workouts.Add(NewWorkout(ActivityType.MountainRide, bike2, Utc(12, 11, hour: 10), 10));

        await db.SaveChangesAsync();

        var tireDistance3 = await wear.GetComponentMetersAsync(tire.Id);

        Assert.Equal(0, tireDistance1);
        Assert.Equal(10000, tireDistance2);
        Assert.Equal(20000, tireDistance3);
    }

    [Fact]
    public async Task Part_never_installed_has_no_miles()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);

        var tire = new Component { Name = "Unused Front Tire", Kind = ComponentKind.FrontTire };
        var bike = new Gear { Name = "Bike", Kind = GearKind.Bike };
        var workout = NewWorkout( ActivityType.MountainRide, bike, Utc(10, 10, hour: 10), 10);

        db.Workouts.Add(workout);
        db.Components.Add(tire);
        db.Gear.Add(bike);

        await db.SaveChangesAsync();

        var tireDistance = await wear.GetComponentMetersAsync(tire.Id);

        Assert.Equal(0, tireDistance);
    }

    [Fact]
    public async Task Ride_one_second_before_removal_counts_for_old_part()
    {
        await using var db = await TestDb.CreateAsync();
        var wear = new WearService(db);
        var removal1 = Utc(10, 10, hour: 10).AddSeconds(-1);
        var removal2 = Utc(10, 10, hour: 10);

        var oldTire = new Component { Name = "Old Tire", Kind = ComponentKind.FrontTire };
        var newTire = new Component { Name = "New Tire", Kind = ComponentKind.FrontTire };
        var bike = new Gear { Name = "Mountain Bike", Kind = GearKind.Bike };
        var ride = NewWorkout(ActivityType.MountainRide, bike, removal1, 10);
        
        db.ComponentInstalls.AddRange(
            new ComponentInstall { Component = oldTire, Gear = bike, InstalledAt = Utc(10, 9, hour: 10), RemovedAt = removal2 },
            new ComponentInstall { Component = newTire, Gear = bike, InstalledAt = removal2 });

        db.Add(ride);

        await db.SaveChangesAsync();

        var oldTireDistance = await wear.GetComponentMetersAsync(oldTire.Id);
        var newTireDistance = await wear.GetComponentMetersAsync(newTire.Id);

        Assert.Equal(10000, oldTireDistance);
        Assert.Equal(0, newTireDistance);
    }
}