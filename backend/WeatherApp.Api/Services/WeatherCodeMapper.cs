namespace WeatherApp.Api.Services;

// Open-Meteo reports conditions as WMO weather codes (0 = clear sky, 61 = slight rain, ...).
// See the "WMO Weather interpretation codes" table at https://open-meteo.com/en/docs
public static class WeatherCodeMapper
{
    // A switch *expression* returns a value. "or" and relational patterns (>= / <=)
    // let several codes share one arm. "_" is the default arm.
    // The return type is a tuple with named parts: (string Condition, string Description).
    public static (string Condition, string Description) Map(int code) => code switch
    {
        0 => ("clear", "Clear sky"),
        1 => ("clear", "Mainly clear"),
        2 => ("partly-cloudy", "Partly cloudy"),
        3 => ("cloudy", "Overcast"),
        45 or 48 => ("fog", "Fog"),
        51 => ("drizzle", "Light drizzle"),
        53 => ("drizzle", "Drizzle"),
        55 => ("drizzle", "Heavy drizzle"),
        56 or 57 => ("drizzle", "Freezing drizzle"),
        61 => ("rain", "Light rain"),
        63 => ("rain", "Rain"),
        65 => ("rain", "Heavy rain"),
        66 or 67 => ("rain", "Freezing rain"),
        71 => ("snow", "Light snow"),
        73 => ("snow", "Snow"),
        75 => ("snow", "Heavy snow"),
        77 => ("snow", "Snow grains"),
        >= 80 and <= 82 => ("rain", "Rain showers"),
        85 or 86 => ("snow", "Snow showers"),
        95 => ("thunderstorm", "Thunderstorm"),
        96 or 99 => ("thunderstorm", "Thunderstorm with hail"),
        _ => ("cloudy", "Unknown conditions")
    };
}
