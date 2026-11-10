namespace WeatherApp.Api.Services;

// The "Options pattern": settings live in appsettings.json under "OpenMeteo" and
// ASP.NET Core copies them into this class. Code asks for IOptions<OpenMeteoOptions>
// instead of reading config strings by hand.
public sealed class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    public string GeocodingUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1/search";
    public string ForecastUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";
}
