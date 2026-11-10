namespace WeatherApp.Api.Models;

// All values are metric: °C, km/h, mm.
public record CurrentWeather(
    double Temperature,
    double FeelsLike,
    int Humidity,
    double WindSpeed,
    double Precipitation,
    int WeatherCode,          // WMO weather code from Open-Meteo
    bool IsDay,
    string Condition,         // Simple category the UI animates: "clear", "rain", "snow", ...
    string Description,       // Human-readable text, e.g. "Light drizzle"
    DateTimeOffset ObservedAt);
