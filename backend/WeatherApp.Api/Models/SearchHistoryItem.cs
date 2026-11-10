namespace WeatherApp.Api.Models;

public record SearchHistoryItem(
    long LocationId,
    string Name,
    string Country,
    string? Admin1,
    double Latitude,
    double Longitude,
    DateTimeOffset LastSearchedAt);
