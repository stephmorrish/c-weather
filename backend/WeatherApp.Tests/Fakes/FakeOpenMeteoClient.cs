using WeatherApp.Api.Models;
using WeatherApp.Api.Services;

namespace WeatherApp.Tests.Fakes;

// A hand-written "fake": a simple stand-in for the real client that never touches the
// network. Tests set its properties to choose what happens, then check what was called.
// (Libraries such as Moq or NSubstitute can generate these, but writing one shows that
// there's no magic: it's just another class implementing the interface.)
public sealed class FakeOpenMeteoClient : IOpenMeteoClient
{
    public CurrentWeather Weather { get; set; } = TestData.Weather();
    public IReadOnlyList<Location> Locations { get; set; } = [];

    // When set, GetCurrentWeatherAsync fails with this exception.
    public Exception? WeatherError { get; set; }

    public int WeatherCalls { get; private set; }

    public Task<IReadOnlyList<Location>> SearchLocationsAsync(string query, int count, CancellationToken ct) =>
        Task.FromResult(Locations);

    public Task<CurrentWeather> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken ct)
    {
        WeatherCalls++;
        return WeatherError is null
            ? Task.FromResult(Weather)
            : Task.FromException<CurrentWeather>(WeatherError);
    }
}
