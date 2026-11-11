using System.Data.Common;
using WeatherApp.Api.Data;
using WeatherApp.Api.Models;

namespace WeatherApp.Tests.Fakes;

// An in-memory stand-in for OracleWeatherRepository. Setting DatabaseDown = true makes
// every call fail the way a real outage would, with a DbException.
public sealed class FakeWeatherRepository : IWeatherRepository
{
    public const long LocationId = 42;

    public CachedWeather? Cached { get; set; }
    public bool DatabaseDown { get; set; }
    public IReadOnlyList<SearchHistoryItem> History { get; set; } = [];

    // Records of what the code under test did, for assertions.
    public List<long> LoggedSearches { get; } = [];
    public List<CurrentWeather> SavedWeather { get; } = [];

    public async Task<long> UpsertLocationAsync(Location location, CancellationToken ct)
    {
        await FailIfDown();
        return LocationId;
    }

    public async Task LogSearchAsync(long locationId, CancellationToken ct)
    {
        await FailIfDown();
        LoggedSearches.Add(locationId);
    }

    public async Task<CachedWeather?> GetLatestWeatherAsync(long locationId, int maxAgeMinutes, CancellationToken ct)
    {
        await FailIfDown();
        return Cached;
    }

    public async Task SaveWeatherAsync(long locationId, CurrentWeather weather, CancellationToken ct)
    {
        await FailIfDown();
        SavedWeather.Add(weather);
    }

    public async Task<IReadOnlyList<SearchHistoryItem>> GetRecentSearchesAsync(int limit, CancellationToken ct)
    {
        await FailIfDown();
        return History;
    }

    public async Task<IReadOnlyList<SearchStat>> GetSearchStatsAsync(int limit, CancellationToken ct)
    {
        await FailIfDown();
        return [];
    }

    public Task<bool> CanConnectAsync(CancellationToken ct) => Task.FromResult(!DatabaseDown);

    private Task FailIfDown() =>
        DatabaseDown ? Task.FromException(new FakeDbException()) : Task.CompletedTask;
}

// DbException is abstract, so tests need a concrete subclass to throw.
public sealed class FakeDbException() : DbException("Simulated database outage");
