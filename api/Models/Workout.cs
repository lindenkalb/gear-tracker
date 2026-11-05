namespace GearTracker.Api.Models;

/// <summary>
/// The kind of activity a workout was. Stored in the database as text, like "Run",
/// because of the enum rule in AppDbContext.ConfigureConventions.
/// </summary>
public enum ActivityType
{
    Run,
    Hike,
    Ski,
    MountainRide,
    RoadRide,
    Basketball,
    Soccer,
    Baseball,
    Climbing
}

/// <summary>
/// Where a workout came from. Together with SourceKey, it identifies a workout,
/// so the same one is never imported twice.
/// </summary>
public enum WorkoutSource
{
    // Imported from the Health app's "Export All Health Data" zip file.
    HealthExport,
    // Synced from the iPhone app through HealthKit (Phase 5).
    HealthKit
}

/// <summary>
/// One run, hike, ride or other activity. EF Core turns this class into the
/// Workouts table: each property becomes a column, and each Workout object is a row.
/// </summary>
public class Workout
{
    // The primary key. Because it's named Id, EF Core makes it the key automatically,
    // and Postgres numbers new rows for you (1, 2, 3...).
    public int Id { get; set; }

    public ActivityType Type { get; set; }

    // Always stored in UTC.
    // The Postgres driver only accepts UTC times for these columns, so use
    // DateTime.UtcNow (never DateTime.Now) when creating workouts.
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // Moving time from Apple Health. It will leave out pauses
    // so it could be shorter than EndTime - StartTime.
    public TimeSpan Duration { get; set; }

    // Every distance is stored in meters and only converted to miles for display,
    // so the math never mixes units.
    public double DistanceMeters { get; set; }

    // Null when a workout has no route, like a treadmill run.
    // The ? makes these nullable: "no data" is different from 0 meters of climbing.
    public double? ElevationGainMeters { get; set; }
    public double? ElevationLossMeters { get; set; }

    // The gear you use. Empty until gear is assigned
    // Workouts and gear are many-to-many: a ride can use a bike and cycling shoes,
    // and one pair of shoes is used on many runs. EF Core stores the links in a hidden
    // join table called GearWorkout. Gear.Workouts is the other side of this list.
    public List<Gear> GearUsed { get; set; } = [];

    public WorkoutSource Source { get; set; }

    // Identifies the workout within it's own source to prevent duplicates
    // A unique index in AppDbContext refuses any second workout with the same
    // Source and SourceKey. Starts as "" so it's never null.
    public string SourceKey { get; set; } = "";

    // Set when you delete a workout in Health. Keeping the row instead of
    // removing it later lets a later re-import recognize it and not add it back.
    // This is called a "soft delete": the wear calculations skip any workout
    // where DeletedAt has a value.
    public DateTime? DeletedAt { get; set; }
}