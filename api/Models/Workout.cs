namespace GearTracker.Api.Models;

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

public enum WorkoutSource
{
    HealthExport,
    HealthKit
}

public class Workout
{
    public int Id { get; set; }

    public ActivityType Type { get; set; }

    // Always stored in UTC.
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // Moving time from Apple Health. It will leave out pauses
    // so it could be shorter than EndTime - StartTime.
    public TimeSpan Duration { get; set; }

    public double DistanceMeters { get; set; }

    // Null when a workout has no route, like a treadmill run.
    public double? ElevationGainMeters { get; set; }
    public double? ElevationLossMeters { get; set; }

    // The gear you use. Empty until gear is assigned
    public List<Gear> GearUsed { get; set; } = [];

    public WorkoutSource Source { get; set; }

    // Identifies the workout within it's own source to prevent duplicates
    public string SourceKey { get; set; } = "";

    // Set when you delete a workout in Health. Keeping the row instead of
    // removing it later lets a later re-import recognize it and not add it back.
    public DateTime? DeletedAt { get; set; }
}