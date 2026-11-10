using WeatherApp.Api.Models;

namespace WeatherApp.Api.Data;

// The newest reading stored for a location. IsFresh is decided by the database
// (fetched within the cache's max age).
public record CachedWeather(CurrentWeather Weather, DateTimeOffset FetchedAt, bool IsFresh);
