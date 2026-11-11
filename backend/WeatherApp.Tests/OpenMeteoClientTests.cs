using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherApp.Api.Services;

namespace WeatherApp.Tests;

// Tests the real OpenMeteoClient, but swaps the network for a StubHandler. HttpClient
// passes every request through an HttpMessageHandler, so replacing that handler lets the
// test decide exactly what "Open-Meteo" responds with, and see what was requested.
public class OpenMeteoClientTests
{
    private const string GeocodingJson = """
        {
          "results": [
            { "id": 6167865, "name": "Toronto", "latitude": 43.70643, "longitude": -79.39864,
              "country": "Canada", "admin1": "Ontario" },
            { "id": 5174095, "name": "Toronto", "latitude": 40.46423, "longitude": -80.60091,
              "country": "United States", "admin1": "Ohio" }
          ],
          "generationtime_ms": 0.6
        }
        """;

    private const string ForecastJson = """
        {
          "latitude": 43.7, "longitude": -79.4,
          "current": {
            "time": "2026-11-10T06:30", "interval": 900,
            "temperature_2m": 5.3, "relative_humidity_2m": 84, "apparent_temperature": 2.2,
            "is_day": 0, "precipitation": 0.4, "weather_code": 61, "wind_speed_10m": 10.3
          }
        }
        """;

    [Fact]
    public async Task SearchLocations_ParsesResults()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, GeocodingJson);

        var locations = await client.SearchLocationsAsync("Toronto", 5, CancellationToken.None);

        Assert.Equal(2, locations.Count);
        var first = locations[0];
        Assert.Equal("Toronto", first.Name);
        Assert.Equal("Canada", first.Country);
        Assert.Equal("Ontario", first.Admin1);
        Assert.Equal(43.70643, first.Latitude);
    }

    [Fact]
    public async Task SearchLocations_NoMatches_ReturnsEmptyList()
    {
        // When nothing matches, Open-Meteo leaves "results" out of the JSON entirely.
        var (client, _) = CreateClient(HttpStatusCode.OK, """{ "generationtime_ms": 0.3 }""");

        var locations = await client.SearchLocationsAsync("zzqqxx", 5, CancellationToken.None);

        Assert.Empty(locations);
    }

    [Fact]
    public async Task SearchLocations_EscapesTheQuery()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, GeocodingJson);

        await client.SearchLocationsAsync("São Paulo", 5, CancellationToken.None);

        Assert.Contains("name=S%C3%A3o%20Paulo", handler.LastRequestUri);
    }

    [Fact]
    public async Task GetCurrentWeather_MapsFieldsAndWeatherCode()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, ForecastJson);

        var weather = await client.GetCurrentWeatherAsync(43.7, -79.4, CancellationToken.None);

        Assert.Equal(5.3, weather.Temperature);
        Assert.Equal(2.2, weather.FeelsLike);
        Assert.Equal(84, weather.Humidity);
        Assert.Equal(0.4, weather.Precipitation);
        Assert.False(weather.IsDay);
        Assert.Equal("rain", weather.Condition);           // code 61 → light rain
        Assert.Equal("Light rain", weather.Description);
        // Open-Meteo's time has no offset; the client must treat it as UTC.
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 6, 30, 0, TimeSpan.Zero), weather.ObservedAt);
    }

    [Fact]
    public async Task GetCurrentWeather_UsesDotDecimals_EvenInFrenchCulture()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, ForecastJson);
        var original = CultureInfo.CurrentCulture;
        try
        {
            // In French, 43.65 is written "43,65", and a comma would break the URL.
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            await client.GetCurrentWeatherAsync(43.65, -79.38, CancellationToken.None);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;   // always restore shared state
        }

        Assert.Contains("latitude=43.65", handler.LastRequestUri);
        Assert.Contains("longitude=-79.38", handler.LastRequestUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetCurrentWeather_ErrorStatus_ThrowsWeatherApiException(HttpStatusCode status)
    {
        var (client, _) = CreateClient(status, """{ "error": true }""");

        // Assert.ThrowsAsync passes only if the code throws exactly this exception type.
        await Assert.ThrowsAsync<WeatherApiException>(
            () => client.GetCurrentWeatherAsync(43.7, -79.4, CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentWeather_InvalidJson_ThrowsWeatherApiException()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "<html>not json</html>");

        var ex = await Assert.ThrowsAsync<WeatherApiException>(
            () => client.GetCurrentWeatherAsync(43.7, -79.4, CancellationToken.None));
        Assert.Contains("could not understand", ex.Message);
    }

    [Fact]
    public async Task GetCurrentWeather_MissingCurrentBlock_ThrowsWeatherApiException()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, """{ "latitude": 43.7 }""");

        await Assert.ThrowsAsync<WeatherApiException>(
            () => client.GetCurrentWeatherAsync(43.7, -79.4, CancellationToken.None));
    }

    // ----- helpers -----

    private static (OpenMeteoClient Client, StubHandler Handler) CreateClient(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var client = new OpenMeteoClient(
            new HttpClient(handler),
            Options.Create(new OpenMeteoOptions()),
            NullLogger<OpenMeteoClient>.Instance);   // a logger that discards everything
        return (client, handler);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string LastRequestUri { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // AbsoluteUri keeps the escaping (%20 etc.) exactly as it was sent.
            LastRequestUri = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
