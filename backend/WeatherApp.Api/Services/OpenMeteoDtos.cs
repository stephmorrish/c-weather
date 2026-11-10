using System.Text.Json.Serialization;

namespace WeatherApp.Api.Services;

// DTOs ("data transfer objects") mirror the exact JSON Open-Meteo sends us.
// They are "internal" so only this project can see them: the rest of the app
// works with our own clean Models, never with the third-party JSON shape.
//
// [JsonPropertyName] maps snake_case JSON names onto PascalCase C# properties.

internal sealed record GeocodingResponse(
    [property: JsonPropertyName("results")] List<GeocodingResult>? Results);

internal sealed record GeocodingResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("admin1")] string? Admin1,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude);

internal sealed record ForecastResponse(
    [property: JsonPropertyName("current")] ForecastCurrent? Current);

internal sealed record ForecastCurrent(
    [property: JsonPropertyName("time")] string Time,   // e.g. "2026-11-10T01:15" (UTC, no offset)
    [property: JsonPropertyName("temperature_2m")] double Temperature,
    [property: JsonPropertyName("apparent_temperature")] double FeelsLike,
    [property: JsonPropertyName("relative_humidity_2m")] int Humidity,
    [property: JsonPropertyName("wind_speed_10m")] double WindSpeed,
    [property: JsonPropertyName("precipitation")] double Precipitation,
    [property: JsonPropertyName("weather_code")] int WeatherCode,
    [property: JsonPropertyName("is_day")] int IsDay);   // 1 = day, 0 = night
