namespace WeatherApp.Api.Models;

// What GET /api/weather returns. FromCache and IsStale are filled in properly
// in Phase 2, once results are cached in Oracle.
public record WeatherResponse(
    Location Location,
    CurrentWeather Current,
    DateTimeOffset FetchedAt,
    bool FromCache,
    bool IsStale);
