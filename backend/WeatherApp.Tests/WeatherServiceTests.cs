using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WeatherApp.Api.Data;
using WeatherApp.Api.Services;
using WeatherApp.Tests.Fakes;

namespace WeatherApp.Tests;

// Unit tests for the caching / fallback rules in WeatherService. Each test follows
// Arrange → Act → Assert: set up the fakes, call one method, check the outcome.
//
// xUnit creates a NEW instance of this class for every test, so the fields below start
// fresh each time and tests can't affect each other.
public class WeatherServiceTests
{
    private readonly FakeOpenMeteoClient _client = new();
    private readonly FakeWeatherRepository _repository = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);   // a clock the test controls
    private readonly WeatherService _service;

    public WeatherServiceTests()
    {
        _service = new WeatherService(
            _client,
            _repository,
            Options.Create(new CacheOptions { MaxAgeMinutes = 10 }),
            _time,
            NullLogger<WeatherService>.Instance);
    }

    [Fact]
    public async Task FreshCache_IsReturnedWithoutCallingTheApi()
    {
        var cachedAt = TestData.Now.AddMinutes(-3);
        _repository.Cached = new CachedWeather(TestData.Weather(temperature: 4.0), cachedAt, IsFresh: true);

        var result = await _service.GetWeatherAsync(TestData.Toronto, logSearch: false, CancellationToken.None);

        Assert.True(result.FromCache);
        Assert.False(result.IsStale);
        Assert.Equal(4.0, result.Current.Temperature);
        Assert.Equal(cachedAt, result.FetchedAt);
        Assert.Equal(0, _client.WeatherCalls);
    }

    [Fact]
    public async Task NoCache_CallsTheApiAndSavesTheReading()
    {
        _client.Weather = TestData.Weather(temperature: 7.5);

        var result = await _service.GetWeatherAsync(TestData.Toronto, logSearch: false, CancellationToken.None);

        Assert.False(result.FromCache);
        Assert.Equal(7.5, result.Current.Temperature);
        Assert.Equal(TestData.Now, result.FetchedAt);              // came from the fake clock
        Assert.Equal(1, _client.WeatherCalls);
        Assert.Equal(7.5, Assert.Single(_repository.SavedWeather).Temperature);
    }

    [Fact]
    public async Task StaleCache_IsRefreshedFromTheApi()
    {
        _repository.Cached = new CachedWeather(TestData.Weather(temperature: 1.0), TestData.Now.AddHours(-1), IsFresh: false);
        _client.Weather = TestData.Weather(temperature: 9.0);

        var result = await _service.GetWeatherAsync(TestData.Toronto, logSearch: false, CancellationToken.None);

        Assert.False(result.FromCache);
        Assert.Equal(9.0, result.Current.Temperature);
    }

    [Fact]
    public async Task ApiDown_WithOldReading_ServesItMarkedStale()
    {
        var oldFetch = TestData.Now.AddHours(-2);
        _repository.Cached = new CachedWeather(TestData.Weather(temperature: 1.0), oldFetch, IsFresh: false);
        _client.WeatherError = new WeatherApiException("down");

        var result = await _service.GetWeatherAsync(TestData.Toronto, logSearch: false, CancellationToken.None);

        Assert.True(result.FromCache);
        Assert.True(result.IsStale);
        Assert.Equal(1.0, result.Current.Temperature);
        Assert.Equal(oldFetch, result.FetchedAt);
    }

    [Fact]
    public async Task ApiDown_WithNothingCached_Throws()
    {
        _client.WeatherError = new WeatherApiException("down");

        await Assert.ThrowsAsync<WeatherApiException>(
            () => _service.GetWeatherAsync(TestData.Toronto, logSearch: false, CancellationToken.None));
    }

    [Fact]
    public async Task DatabaseDown_StillReturnsWeatherFromTheApi()
    {
        _repository.DatabaseDown = true;

        var result = await _service.GetWeatherAsync(TestData.Toronto, logSearch: true, CancellationToken.None);

        Assert.False(result.FromCache);
        Assert.Equal(1, _client.WeatherCalls);
        Assert.Empty(_repository.SavedWeather);
    }

    [Fact]
    public async Task DatabaseAndApiBothDown_Throws()
    {
        _repository.DatabaseDown = true;
        _client.WeatherError = new WeatherApiException("down");

        await Assert.ThrowsAsync<WeatherApiException>(
            () => _service.GetWeatherAsync(TestData.Toronto, logSearch: false, CancellationToken.None));
    }

    // Two rows of data for one test: logSearch true logs one search, false logs none.
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task LogSearch_ControlsSearchHistory(bool logSearch, int expectedLogs)
    {
        await _service.GetWeatherAsync(TestData.Toronto, logSearch, CancellationToken.None);

        Assert.Equal(expectedLogs, _repository.LoggedSearches.Count);
    }

    [Fact]
    public async Task SearchLocations_TrimsTheQuery()
    {
        _client.Locations = [TestData.Toronto];

        var results = await _service.SearchLocationsAsync("  Toronto  ", CancellationToken.None);

        Assert.Equal(TestData.Toronto, Assert.Single(results));   // records compare by value
    }
}
