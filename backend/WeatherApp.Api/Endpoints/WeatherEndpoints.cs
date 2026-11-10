using WeatherApp.Api.Data;
using WeatherApp.Api.Models;
using WeatherApp.Api.Services;

namespace WeatherApp.Api.Endpoints;

// An "extension method" (note the "this" on the first parameter) lets Program.cs call
// app.MapWeatherEndpoints() as if it were built in. It keeps routes out of Program.cs.
public static class WeatherEndpoints
{
    private const int MaxQueryLength = 100;
    private const int DefaultListLimit = 10;

    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder app)
    {
        // A route group: every route below starts with /api.
        var api = app.MapGroup("/api");

        // Minimal API endpoints. ASP.NET Core fills the lambda's parameters for us:
        //   - simple types (string? q) come from the query string  (?q=London)
        //   - registered services (WeatherService) come from dependency injection
        //   - CancellationToken fires if the browser gives up on the request
        api.MapGet("/locations/search", async (string? q, WeatherService weather, CancellationToken ct) =>
        {
            var query = q?.Trim() ?? "";
            if (query.Length < 2 || query.Length > MaxQueryLength)
            {
                // 400 Bad Request with ProblemDetails listing which field is wrong.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["q"] = [$"Enter between 2 and {MaxQueryLength} characters."]
                });
            }

            return Results.Ok(await weather.SearchLocationsAsync(query, ct));
        });

        api.MapGet("/weather", async (
            double? lat, double? lon, string? name, string? country, string? admin1, bool? logSearch,
            WeatherService weather, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (lat is null or < -90 or > 90) errors["lat"] = ["Latitude must be between -90 and 90."];
            if (lon is null or < -180 or > 180) errors["lon"] = ["Longitude must be between -180 and 180."];
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            // ".Value" is safe here: the checks above guarantee lat/lon are not null.
            var location = new Location(
                Name: string.IsNullOrWhiteSpace(name) ? $"{lat:0.##}, {lon:0.##}" : name.Trim(),
                Country: country?.Trim() ?? "",
                Admin1: admin1?.Trim(),
                Latitude: lat!.Value,
                Longitude: lon!.Value);

            return Results.Ok(await weather.GetWeatherAsync(location, logSearch ?? false, ct));
        });

        // These two talk to the repository directly. There's no business logic to add,
        // and limit validation happens in PL/SQL (an out-of-range limit raises ORA-20001,
        // which becomes a 400).
        api.MapGet("/history", async (int? limit, IWeatherRepository repository, CancellationToken ct) =>
            Results.Ok(await repository.GetRecentSearchesAsync(limit ?? DefaultListLimit, ct)));

        api.MapGet("/stats", async (int? limit, IWeatherRepository repository, CancellationToken ct) =>
            Results.Ok(await repository.GetSearchStatsAsync(limit ?? DefaultListLimit, ct)));

        api.MapGet("/health", async (IWeatherRepository repository, CancellationToken ct) =>
        {
            var databaseOk = await repository.CanConnectAsync(ct);
            // The API itself is healthy even if the database isn't, because weather still works.
            return Results.Ok(new { status = "ok", database = databaseOk ? "ok" : "unavailable" });
        });

        return app;
    }
}
