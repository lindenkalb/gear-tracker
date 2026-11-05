using Microsoft.EntityFrameworkCore;

namespace GearTracker.Api.Tests;

/// <summary>
/// Gives each test a fresh, empty database, so tests never affect each other and never
/// touch your real geartracker database. The class is static, so you call its method on
/// the class itself (TestDb.CreateAsync()) without creating a TestDb object.
/// </summary>
public static class TestDb
{
    /// <summary>
    /// Wipes the test database, rebuilds every table, and returns an AppDbContext
    /// connected to it.
    /// </summary>
    public static async Task<AppDbContext> CreateAsync()
    {
        // Use the TEST_DB_CONNECTION environment variable if it's set (GitHub Actions will
        // set one in Phase 4). Otherwise, ?? falls back to the geartracker_test database on
        // this computer, signing in as the current user.
        var connection = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? $"Host=localhost;Database=geartracker_test;Username={Environment.UserName}";

            // Build the options by hand: the same job AddDbContext does in Program.cs.
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection)
                .Options;

            var db = new AppDbContext(options);
            // Delete the test database, including the last test's data, if it exists...
            await db.Database.EnsureDeletedAsync();
            // ...then create it again, with every table built straight from your classes.
            // This also creates it the very first time, so you never need to run createdb.
            await db.Database.EnsureCreatedAsync();
            return db;
    }
}