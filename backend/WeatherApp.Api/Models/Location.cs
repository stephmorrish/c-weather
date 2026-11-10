namespace WeatherApp.Api.Models;

// A "record" is a class whose main job is to hold data. The compiler generates the
// constructor, properties, equality (two records with the same values are Equal)
// and a readable ToString() for us. Perfect for API request/response shapes.
//
// "string?" means the value may be null. The project has <Nullable>enable</Nullable>,
// so the compiler warns us whenever we might use a null without checking first.
public record Location(
    string Name,
    string Country,
    string? Admin1,      // State / province / region, e.g. "Ontario"
    double Latitude,
    double Longitude);
