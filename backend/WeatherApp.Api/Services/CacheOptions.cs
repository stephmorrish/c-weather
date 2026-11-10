namespace WeatherApp.Api.Services;

public sealed class CacheOptions
{
    public const string SectionName = "WeatherCache";

    // Open-Meteo updates current conditions every 15 minutes, so caching for 10 minutes
    // saves calls without serving noticeably old data.
    public int MaxAgeMinutes { get; set; } = 10;
}
