namespace WeatherApp.Api.Models;

public record SearchStat(
    string Name,
    string Country,
    int SearchCount,
    int Rank,
    double PercentOfSearches,
    double? AverageTemperature,   // null when we never fetched weather for this place
    DateTimeOffset LastSearchedAt);
