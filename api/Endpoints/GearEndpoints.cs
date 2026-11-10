using GearTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GearTracker.Api.Endpoints;

// What a client sends to add or edit gear.
public record SaveGearRequest(string Name, GearKind Kind, ActivityType? DefaultFor, double? ReplaceAtMeters);

// What the API sends back about a piece of gear.
public record GearResponse(int Id, string Name, GearKind Kind, ActivityType? DefaultFor, double? ReplaceAtMeters, DateOnly? RetiredOn);

public static class GearEndpoints
{
    // An extension method on WebApplication, so Program.cs can call app.MapGearEndpoints().
    public static void MapGearEndpoints(this WebApplication app)
    {
        var gear = app.MapGroup("/gear");

        // GET /gear: every piece of gear, sorted by name
        gear.MapGet("/", async (AppDbContext db) =>
            await db.Gear
                .OrderBy(g => g.Name)
                .Select(g => ToResponse(g))
                .ToListAsync());

        // GET /gear/{id}: one piece of gear, or 404 if it doesn't exist
        gear.MapGet("/{id:int}", async (int id, AppDbContext db) =>
            await db.Gear.FindAsync(id) is Gear g
                ? Results.Ok(ToResponse(g))
                : Results.NotFound());

        // POST /gear: add new gear
        gear.MapPost("/", async (SaveGearRequest request, AppDbContext db) =>
        {
            if (Validate(request) is string error)
                return Results.BadRequest(error);

            var g = new Gear
            {
                Name = request.Name.Trim(),
                Kind = request.Kind,
                DefaultFor = request.DefaultFor,
                ReplaceAtMeters = request.ReplaceAtMeters,
            };
            db.Gear.Add(g);
            await db.SaveChangesAsync();

            // 201 Created, with the new gear's address and data
            return Results.Created($"/gear/{g.Id}", ToResponse(g));
        });

        // PUT /gear/{id}: edit gear
        gear.MapPut("/{id:int}", async (int id, SaveGearRequest request, AppDbContext db) =>
        {
            var g = await db.Gear.FindAsync(id);
            if (g is null)
                return Results.NotFound();
            if (Validate(request) is string error)
                return Results.BadRequest(error);

            g.Name = request.Name.Trim();
            g.Kind = request.Kind;
            g.DefaultFor = request.DefaultFor;
            g.ReplaceAtMeters = request.ReplaceAtMeters;
            await db.SaveChangesAsync();   // EF Core noticed the changes and writes an UPDATE

            return Results.Ok(ToResponse(g));
        });

        // POST /gear/{id}/retire: mark gear as retired (its history stays)
        gear.MapPost("/{id:int}/retire", async (int id, AppDbContext db) =>
        {
            var g = await db.Gear.FindAsync(id);
            if (g is null)
                return Results.NotFound();

            g.RetiredOn ??= DateOnly.FromDateTime(DateTime.UtcNow);   // only set it if it isn't already
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(g));
        });
    }

    // Returns an error message, or null if the request is fine.
    private static string? Validate(SaveGearRequest r) =>
        string.IsNullOrWhiteSpace(r.Name) ? "Name is required."
        : r.ReplaceAtMeters <= 0 ? "ReplaceAtMeters must be more than 0."
        : null;

    // Copies an entity into a response record.
    private static GearResponse ToResponse(Gear g) =>
        new(g.Id, g.Name, g.Kind, g.DefaultFor, g.ReplaceAtMeters, g.RetiredOn);
}