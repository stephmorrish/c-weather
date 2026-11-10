using WeatherApp.Api.Models;

namespace WeatherApp.Api.Services;

// The "business logic" layer. Endpoints call this; this calls the API client and
// (from Phase 2) the database. Keeping endpoints thin makes the logic easy to test.
//
// TimeProvider is .NET's injectable clock. Tests can pass a fake clock to control "now".
public sealed class WeatherService(IOpenMeteoClient client, TimeProvider time)
{
    private const int MaxSearchResults = 5;

    public Task<IReadOnlyList<Location>> SearchLocationsAsync(string query, CancellationToken ct) =>
        client.SearchLocationsAsync(query.Trim(), MaxSearchResults, ct);

    public async Task<WeatherResponse> GetWeatherAsync(Location location, CancellationToken ct)
    {
        var current = await client.GetCurrentWeatherAsync(location.Latitude, location.Longitude, ct);

        // Named arguments (FromCache: false) make boolean parameters readable at the call site.
        return new WeatherResponse(location, current, time.GetUtcNow(), FromCache: false, IsStale: false);
    }
}
