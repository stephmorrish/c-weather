using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using WeatherApp.Api.Models;

namespace WeatherApp.Api.Services;

// Code depends on this interface rather than the concrete class, so tests (Phase 5)
// can swap in a fake that never touches the network.
public interface IOpenMeteoClient
{
    Task<IReadOnlyList<Location>> SearchLocationsAsync(string query, int count, CancellationToken ct);
    Task<CurrentWeather> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken ct);
}

// A "typed HttpClient". Program.cs registers it with AddHttpClient, which hands us
// a correctly managed HttpClient (no socket exhaustion) with retry/timeout policies attached.
//
// The parameters after the class name form a "primary constructor": dependency
// injection supplies them, and they are available throughout the class.
public sealed class OpenMeteoClient(
    HttpClient http,
    IOptions<OpenMeteoOptions> options,
    ILogger<OpenMeteoClient> logger) : IOpenMeteoClient
{
    private const string CurrentFields =
        "temperature_2m,relative_humidity_2m,apparent_temperature,is_day,precipitation,weather_code,wind_speed_10m";

    private readonly OpenMeteoOptions _options = options.Value;

    // "async Task<T>" means: this method does I/O and returns a T later.
    // "await" frees the thread while waiting for the network instead of blocking it.
    public async Task<IReadOnlyList<Location>> SearchLocationsAsync(string query, int count, CancellationToken ct)
    {
        // Uri.EscapeDataString makes user input safe to put in a URL ("São Paulo" → "S%C3%A3o%20Paulo").
        var url = $"{_options.GeocodingUrl}?name={Uri.EscapeDataString(query)}&count={count}&language=en&format=json";

        var response = await GetJsonAsync<GeocodingResponse>(url, ct);

        // When nothing matches, Open-Meteo leaves "results" out entirely, so Results is null.
        // "?." stops at null, and "?? []" substitutes an empty list.
        return response.Results?
            .Select(r => new Location(r.Name, r.Country ?? "", r.Admin1, r.Latitude, r.Longitude))
            .ToList() ?? [];
    }

    public async Task<CurrentWeather> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken ct)
    {
        // InvariantCulture matters. On a PC set to French, 43.65.ToString() gives "43,65",
        // which would break the URL. Invariant always uses a dot.
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{_options.ForecastUrl}?latitude={latitude}&longitude={longitude}&current={CurrentFields}&wind_speed_unit=kmh&timezone=GMT");

        var response = await GetJsonAsync<ForecastResponse>(url, ct);
        var c = response.Current
            ?? throw new WeatherApiException("Weather service response did not include current conditions.");

        // Tuple deconstruction: pull both named values out of the mapper's result at once.
        var (condition, description) = WeatherCodeMapper.Map(c.WeatherCode);

        // Open-Meteo's time has no offset; we asked for GMT, so treat it as UTC.
        var observedAt = DateTimeOffset.Parse(c.Time + "Z", CultureInfo.InvariantCulture);

        return new CurrentWeather(
            c.Temperature, c.FeelsLike, c.Humidity, c.WindSpeed, c.Precipitation,
            c.WeatherCode, c.IsDay == 1, condition, description, observedAt);
    }

    // One place that turns every way the HTTP call can fail into a WeatherApiException.
    private async Task<T> GetJsonAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            // GetFromJsonAsync throws HttpRequestException on non-2xx status codes.
            var result = await http.GetFromJsonAsync<T>(url, ct);
            return result ?? throw new WeatherApiException("Weather service returned an empty response.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Open-Meteo request failed ({Status}) for {Url}", ex.StatusCode, url);
            throw new WeatherApiException("The weather service is unavailable right now.", ex);
        }
        catch (ExecutionRejectedException ex)
        {
            // Polly (the resilience library) throws this when a timeout fires or the
            // circuit breaker is open after repeated failures.
            logger.LogWarning(ex, "Open-Meteo request rejected by resilience policy for {Url}", url);
            throw new WeatherApiException("The weather service is not responding. Please try again shortly.", ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Open-Meteo returned JSON we could not read for {Url}", url);
            throw new WeatherApiException("The weather service sent data we could not understand.", ex);
        }
    }
}
