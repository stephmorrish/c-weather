using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WeatherApp.Api.Data;
using WeatherApp.Api.Services;
using WeatherApp.Tests.Fakes;

namespace WeatherApp.Tests;

// Integration tests: they start the whole API in memory (routing, validation, the
// exception handler, JSON settings) and send real HTTP requests to it. Only the two
// outside dependencies, Open-Meteo and Oracle, are swapped for fakes.
//
// IClassFixture shares one WebApplicationFactory between the tests in this class,
// because starting the app is slower than creating a new object.
public class ApiEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private (HttpClient Http, FakeOpenMeteoClient Client, FakeWeatherRepository Repository) CreateApp()
    {
        var client = new FakeOpenMeteoClient();
        var repository = new FakeWeatherRepository();

        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                // A later registration wins, so these replace the real implementations.
                services.AddSingleton<IOpenMeteoClient>(client);
                services.AddSingleton<IWeatherRepository>(repository);
            }));

        return (app.CreateClient(), client, repository);
    }

    [Fact]
    public async Task Weather_ValidRequest_ReturnsCamelCaseJson()
    {
        var (http, _, _) = CreateApp();

        var response = await http.GetAsync("/api/weather?lat=43.7&lon=-79.4&name=Toronto&country=Canada");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Toronto", json.GetProperty("location").GetProperty("name").GetString());
        Assert.Equal("clear", json.GetProperty("current").GetProperty("condition").GetString());
        Assert.False(json.GetProperty("fromCache").GetBoolean());
    }

    [Theory]
    [InlineData("/api/weather?lat=200&lon=0", "lat")]
    [InlineData("/api/weather?lat=0&lon=-181", "lon")]
    [InlineData("/api/weather", "lat")]
    [InlineData("/api/locations/search?q=a", "q")]
    public async Task InvalidInput_Returns400WithFieldError(string url, string field)
    {
        var (http, _, _) = CreateApp();

        var response = await http.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"expected an error for '{field}'");
    }

    [Fact]
    public async Task Weather_ApiDownAndNothingCached_Returns503ProblemDetails()
    {
        var (http, client, _) = CreateApp();
        client.WeatherError = new WeatherApiException("The weather service is unavailable right now.");

        var response = await http.GetAsync("/api/weather?lat=43.7&lon=-79.4");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Weather service unavailable", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task History_DatabaseDown_Returns503()
    {
        var (http, _, repository) = CreateApp();
        repository.DatabaseDown = true;

        var response = await http.GetAsync("/api/history");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Database unavailable", problem.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(false, "ok")]
    [InlineData(true, "unavailable")]
    public async Task Health_ReportsDatabaseStatus(bool databaseDown, string expected)
    {
        var (http, _, repository) = CreateApp();
        repository.DatabaseDown = databaseDown;

        var json = await http.GetFromJsonAsync<JsonElement>("/api/health");

        Assert.Equal("ok", json.GetProperty("status").GetString());
        Assert.Equal(expected, json.GetProperty("database").GetString());
    }
}
