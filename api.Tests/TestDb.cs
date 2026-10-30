using Microsoft.EntityFrameworkCore;

namespace GearTracker.Api.Tests;

public static class TestDb
{
    public static async Task<AppDbContext> CreateAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? $"Host=localhost;Database=geartracker_test;Username={Environment.UserName}";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection)
                .Options;
            
            var db = new AppDbContext(options);
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            return db;
    }
}