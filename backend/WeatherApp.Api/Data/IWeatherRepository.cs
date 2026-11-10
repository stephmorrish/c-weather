using WeatherApp.Api.Models;

namespace WeatherApp.Api.Data;

// The "repository" pattern: one interface for everything the app stores, so the rest
// of the code never sees SQL. Each method maps to one weather_pkg procedure.
public interface IWeatherRepository
{
    Task<long> UpsertLocationAsync(Location location, CancellationToken ct);
    Task LogSearchAsync(long locationId, CancellationToken ct);
    Task<CachedWeather?> GetLatestWeatherAsync(long locationId, int maxAgeMinutes, CancellationToken ct);
    Task SaveWeatherAsync(long locationId, CurrentWeather weather, CancellationToken ct);
    Task<IReadOnlyList<SearchHistoryItem>> GetRecentSearchesAsync(int limit, CancellationToken ct);
    Task<IReadOnlyList<SearchStat>> GetSearchStatsAsync(int limit, CancellationToken ct);
    Task<bool> CanConnectAsync(CancellationToken ct);
}
