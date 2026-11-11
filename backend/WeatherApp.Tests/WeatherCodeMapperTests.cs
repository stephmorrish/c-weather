using WeatherApp.Api.Services;

namespace WeatherApp.Tests;

public class WeatherCodeMapperTests
{
    // [Theory] + [InlineData] runs the same test once per data row: one method, many cases.
    // [Fact] (used elsewhere) is a test with no parameters.
    [Theory]
    [InlineData(0, "clear", "Clear sky")]
    [InlineData(2, "partly-cloudy", "Partly cloudy")]
    [InlineData(3, "cloudy", "Overcast")]
    [InlineData(45, "fog", "Fog")]
    [InlineData(48, "fog", "Fog")]
    [InlineData(53, "drizzle", "Drizzle")]
    [InlineData(65, "rain", "Heavy rain")]
    [InlineData(81, "rain", "Rain showers")]   // inside the ">= 80 and <= 82" range arm
    [InlineData(75, "snow", "Heavy snow")]
    [InlineData(86, "snow", "Snow showers")]
    [InlineData(95, "thunderstorm", "Thunderstorm")]
    [InlineData(99, "thunderstorm", "Thunderstorm with hail")]
    public void Map_KnownCode_ReturnsConditionAndDescription(int code, string condition, string description)
    {
        var result = WeatherCodeMapper.Map(code);

        Assert.Equal((condition, description), result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(1000)]
    public void Map_UnknownCode_FallsBackToCloudy(int code)
    {
        var (condition, description) = WeatherCodeMapper.Map(code);

        Assert.Equal("cloudy", condition);
        Assert.Equal("Unknown conditions", description);
    }
}
