// Program.cs is where the API starts. It runs top to bottom in two stages:
//   1. Setup: register the services the app uses (the builder.Services lines).
//   2. Run: after builder.Build(), map web addresses to code, then start the server.

using GearTracker.Api;
using GearTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

// Creates the builder, which loads settings from appsettings.json, user secrets
// (on your Mac), and environment variables (on Azure later).
var builder = WebApplication.CreateBuilder(args);

// Register the database. The connection string is read from settings under
// ConnectionStrings:Default (your user secrets, locally). ASP.NET creates a fresh
// AppDbContext for each request and hands it to any endpoint or service that asks.
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register WearService, so endpoints can ask for one as a parameter.
// "Scoped" means a new WearService for each request, the same as AppDbContext.
builder.Services.AddScoped<GearTracker.Api.Services.WearService>();

// Build the app. Setup is finished after this line; everything below says what it does.
var app = builder.Build();

// GET /gear/{id}/wear: total miles on one piece of gear.
// ASP.NET fills each lambda parameter for you: "int id" matches {id} in the address
// by name, and WearService comes from the services registered above.
// Dividing by 1609.344 (meters in one mile) converts the answer to miles.
app.MapGet("/gear/{id}/wear", async (int id, GearTracker.Api.Services.WearService wear) =>
    Results.Ok(await wear.GetGearMetersAsync(id) / 1609.344));

// GET /components/{id}/wear: total miles on one part, across every install window.
app.MapGet("/components/{id}/wear", async (int id, GearTracker.Api.Services.WearService wear) =>
    Results.Ok(await wear.GetComponentMetersAsync(id) / 1609.344));

// GET /health/db: a quick check that the API can reach the database.
// Returns 200 "database connected", or a 500 error if it can't connect.
app.MapGet("/health/db", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok("database connected")
        : Results.Problem("can't reach the database"));

// Configure the HTTP request pipeline.
// In development only, publish a machine-readable description of every endpoint
// at /openapi/v1.json.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Send anyone who uses a plain http:// address to the https:// one instead.
app.UseHttpsRedirection();

// Everything from here down to app.Run() is the template's sample endpoint
// (GET /weatherforecast). It's safe to delete once you have real endpoints.
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// Start the server. It keeps running and answering requests until you stop it (Ctrl+C).
app.Run();

// The shape of one forecast in the sample endpoint above. A record is a short way to
// write a class that only holds data. Delete it along with the sample endpoint.
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
