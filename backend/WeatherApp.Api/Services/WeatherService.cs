using System.Data.Common;
using Microsoft.Extensions.Options;
using WeatherApp.Api.Data;
using WeatherApp.Api.Models;

namespace WeatherApp.Api.Services;

// The "business logic" layer. Endpoints call this; this coordinates the API client and
// the database. Keeping endpoints thin makes the logic easy to test.
//
// The database is treated as an optional extra. If Oracle is down, weather still loads,
// just without caching or history. DbException is the base class of OracleException,
// so this class never needs to know which database is behind the repository.
public sealed class WeatherService(
    IOpenMeteoClient client,
    IWeatherRepository repository,
    IOptions<CacheOptions> cacheOptions,
    TimeProvider time,
    ILogger<WeatherService> logger)
{
    private const int MaxSearchResults = 5;
    private readonly int _maxAgeMinutes = cacheOptions.Value.MaxAgeMinutes;

    public Task<IReadOnlyList<Location>> SearchLocationsAsync(string query, CancellationToken ct) =>
        client.SearchLocationsAsync(query.Trim(), MaxSearchResults, ct);

    // logSearch is true when the user picked this location, and false for refreshes, so
    // auto-refresh doesn't flood the search history.
    public async Task<WeatherResponse> GetWeatherAsync(Location location, bool logSearch, CancellationToken ct)
    {
        // 1. Record the location and look for a cached reading.
        long? locationId = null;
        CachedWeather? cached = null;
        try
        {
            locationId = await repository.UpsertLocationAsync(location, ct);
            if (logSearch)
            {
                await repository.LogSearchAsync(locationId.Value, ct);
            }
            cached = await repository.GetLatestWeatherAsync(locationId.Value, _maxAgeMinutes, ct);
        }
        catch (DbException ex)
        {
            logger.LogWarning(ex, "Database unavailable; fetching weather without the cache.");
        }

        // 2. Fresh cache hit: no need to call Open-Meteo at all.
        //    "is { IsFresh: true }" is a property pattern: not null AND IsFresh is true.
        if (cached is { IsFresh: true })
        {
            return new WeatherResponse(location, cached.Weather, cached.FetchedAt, FromCache: true, IsStale: false);
        }

        // 3. Ask Open-Meteo. If it fails but we have an older reading, serve that, marked stale.
        //    "when (...)" is an exception filter: this catch only runs if the condition is true;
        //    otherwise the exception keeps going up and becomes a 503.
        CurrentWeather current;
        try
        {
            current = await client.GetCurrentWeatherAsync(location.Latitude, location.Longitude, ct);
        }
        catch (WeatherApiException ex) when (cached is not null)
        {
            logger.LogWarning(ex, "Open-Meteo failed; serving stale reading from {FetchedAt}.", cached.FetchedAt);
            return new WeatherResponse(location, cached.Weather, cached.FetchedAt, FromCache: true, IsStale: true);
        }

        // 4. Save the new reading for next time.
        if (locationId is long id)
        {
            try
            {
                await repository.SaveWeatherAsync(id, current, ct);
            }
            catch (DbException ex)
            {
                logger.LogWarning(ex, "Could not cache weather for location {LocationId}.", id);
            }
        }

        return new WeatherResponse(location, current, time.GetUtcNow(), FromCache: false, IsStale: false);
    }
}
