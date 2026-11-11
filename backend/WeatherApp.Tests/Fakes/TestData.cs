using WeatherApp.Api.Models;

namespace WeatherApp.Tests.Fakes;

// Shared sample data, so every test doesn't have to build its own.
public static class TestData
{
    public static readonly Location Toronto = new("Toronto", "Canada", "Ontario", 43.7064, -79.3986);

    public static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    // Optional parameters let a test change only the value it cares about.
    public static CurrentWeather Weather(double temperature = 5.3, string condition = "clear") => new(
        Temperature: temperature,
        FeelsLike: temperature - 3,
        Humidity: 84,
        WindSpeed: 10.3,
        Precipitation: 0,
        WeatherCode: 0,
        IsDay: false,
        Condition: condition,
        Description: "Clear sky",
        ObservedAt: Now.AddMinutes(-15));
}
