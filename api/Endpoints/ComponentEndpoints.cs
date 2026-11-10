using GearTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GearTracker.Api.Endpoints;

// What a client sends to add or edit a part.
public record SaveComponentRequest(string Name, ComponentKind Kind, double? ReplaceAtMeters);

// What the API sends back about a part.
public record ComponentResponse(int Id, string Name, ComponentKind Kind, double? ReplaceAtMeters, DateOnly? RetiredOn);

// What a client sends to install a part. Leave InstalledAt out to mean "now".
public record InstallRequest(int GearId, DateTimeOffset? InstalledAt);

// What a client sends to remove a part. Leave RemovedAt out (or send no body) to mean "now".
public record RemoveRequest(DateTimeOffset? RemovedAt);

// What the API sends back about one install window.
public record InstallResponse(int Id, int ComponentId, int GearId, DateTime InstalledAt, DateTime? RemovedAt);

public static class ComponentEndpoints
{
    // An extension method on WebApplication, so Program.cs can call app.MapComponentEndpoints().
    public static void MapComponentEndpoints(this WebApplication app)
    {
        var components = app.MapGroup("/components");

        // GET /components: every part, sorted by name
        components.MapGet("/", async (AppDbContext db) =>
            await db.Components
                .OrderBy(c => c.Name)
                .Select(c => ToResponse(c))
                .ToListAsync());

        // GET /components/{id}: one part, or 404 if it doesn't exist
        components.MapGet("/{id:int}", async (int id, AppDbContext db) =>
            await db.Components.FindAsync(id) is Component c
                ? Results.Ok(ToResponse(c))
                : Results.NotFound());

        // GET /components/{id}/installs: every install window this part has had, oldest first
        components.MapGet("/{id:int}/installs", async (int id, AppDbContext db) =>
            await db.ComponentInstalls
                .Where(i => i.ComponentId == id)
                .OrderBy(i => i.InstalledAt)
                .Select(i => ToResponse(i))
                .ToListAsync());

        // POST /components: add a new part
        components.MapPost("/", async (SaveComponentRequest request, AppDbContext db) =>
        {
            if (Validate(request) is string error)
                return Results.BadRequest(error);

            var c = new Component
            {
                Name = request.Name.Trim(),
                Kind = request.Kind,
                ReplaceAtMeters = request.ReplaceAtMeters,
            };
            db.Components.Add(c);
            await db.SaveChangesAsync();

            // 201 Created, with the new part's address and data
            return Results.Created($"/components/{c.Id}", ToResponse(c));
        });

        // PUT /components/{id}: edit a part
        components.MapPut("/{id:int}", async (int id, SaveComponentRequest request, AppDbContext db) =>
        {
            var c = await db.Components.FindAsync(id);
            if (c is null)
                return Results.NotFound();
            if (Validate(request) is string error)
                return Results.BadRequest(error);

            c.Name = request.Name.Trim();
            c.Kind = request.Kind;
            c.ReplaceAtMeters = request.ReplaceAtMeters;
            await db.SaveChangesAsync();   // EF Core noticed the changes and writes an UPDATE

            return Results.Ok(ToResponse(c));
        });

        // POST /components/{id}/install: put a part on a piece of gear
        components.MapPost("/{id:int}/install", async (int id, InstallRequest request, AppDbContext db) =>
        {
            // 1. The part must exist.
            var c = await db.Components.FindAsync(id);
            if (c is null)
                return Results.NotFound();

            // 2. The gear must exist.
            var g = await db.Gear.FindAsync(request.GearId);
            if (g is null)
                return Results.BadRequest("No gear with that ID.");

            // 3. Neither one can be retired.
            if (c.RetiredOn is not null)
                return Results.BadRequest("That part is retired.");
            if (g.RetiredOn is not null)
                return Results.BadRequest("That gear is retired.");

            // 4. The part can't already be on something.
            bool alreadyInstalled = await db.ComponentInstalls
                .AnyAsync(i => i.ComponentId == id && i.RemovedAt == null);
            if (alreadyInstalled)
                return Results.Conflict("Remove it from its current gear first.");

            // 5. Use the time the client sent, converted to UTC, or now if it sent none.
            var installedAt = request.InstalledAt?.UtcDateTime ?? DateTime.UtcNow;

            // 6. No overlapping windows: the new one can't start before an earlier one ended,
            //    or rides during that stretch would count twice.
            bool overlaps = await db.ComponentInstalls
                .AnyAsync(i => i.ComponentId == id && i.RemovedAt > installedAt);
            if (overlaps)
                return Results.BadRequest("That time overlaps an earlier install of this part.");

            // 7. Save the new install window. RemovedAt stays null: it's on until removed.
            var install = new ComponentInstall { Component = c, Gear = g, InstalledAt = installedAt };
            db.ComponentInstalls.Add(install);
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(install));
        });

        // POST /components/{id}/remove: take a part off whatever it's on
        components.MapPost("/{id:int}/remove", async (int id, RemoveRequest? request, AppDbContext db) =>
        {
            // 1. The part must exist.
            if (await db.Components.FindAsync(id) is null)
                return Results.NotFound();

            // 2. Find its open install window.
            var install = await db.ComponentInstalls
                .FirstOrDefaultAsync(i => i.ComponentId == id && i.RemovedAt == null);
            if (install is null)
                return Results.Conflict("That part isn't installed on anything.");

            // 3. Use the time the client sent, converted to UTC, or now if it sent none.
            var removedAt = request?.RemovedAt?.UtcDateTime ?? DateTime.UtcNow;

            // 4. A part can't come off before (or at the same moment) it went on.
            if (removedAt <= install.InstalledAt)
                return Results.BadRequest("The removal time must be after the install time.");

            // 5. Close the window. The row stays, so the part keeps its history.
            install.RemovedAt = removedAt;
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(install));
        });

        // POST /components/{id}/retire: the part is thrown away (its history stays)
        components.MapPost("/{id:int}/retire", async (int id, AppDbContext db) =>
        {
            var c = await db.Components.FindAsync(id);
            if (c is null)
                return Results.NotFound();

            // A thrown-away part can't still be on a bike, so close any open install now.
            var open = await db.ComponentInstalls
                .FirstOrDefaultAsync(i => i.ComponentId == id && i.RemovedAt == null);
            if (open is not null)
                open.RemovedAt = DateTime.UtcNow;

            c.RetiredOn ??= DateOnly.FromDateTime(DateTime.UtcNow);   // only set it if it isn't already
            await db.SaveChangesAsync();   // saves both changes together

            return Results.Ok(ToResponse(c));
        });
    }

    // Returns an error message, or null if the request is fine.
    private static string? Validate(SaveComponentRequest r) =>
        string.IsNullOrWhiteSpace(r.Name) ? "Name is required."
        : r.ReplaceAtMeters <= 0 ? "ReplaceAtMeters must be more than 0."
        : null;

    // Copies a part into a response record.
    private static ComponentResponse ToResponse(Component c) =>
        new(c.Id, c.Name, c.Kind, c.ReplaceAtMeters, c.RetiredOn);

    // Copies an install window into a response record. (Same name as the method above,
    // different parameter type: that's overloading.)
    private static InstallResponse ToResponse(ComponentInstall i) =>
        new(i.Id, i.ComponentId, i.GearId, i.InstalledAt, i.RemovedAt);
}