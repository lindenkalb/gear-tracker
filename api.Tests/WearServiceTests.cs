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
}