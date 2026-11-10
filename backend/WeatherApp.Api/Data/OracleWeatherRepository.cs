using System.Data;
using System.Data.Common;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using WeatherApp.Api.Models;
using WeatherApp.Api.Services;

namespace WeatherApp.Api.Data;

// Calls the PL/SQL package weather_pkg through ODP.NET (Oracle's ADO.NET driver).
//
// ADO.NET basics used here:
//   OracleConnection  – a connection. Opening is cheap because ODP.NET keeps a pool of
//                       real connections and reuses them.
//   OracleCommand     – one call. CommandType.StoredProcedure lets us name a procedure.
//   OracleParameter   – one argument. Values travel separately from the SQL text, so
//                       user input can never change the statement: no SQL injection.
//   DbDataReader      – reads rows one at a time from a result (here, a REF CURSOR).
//
// "await using" disposes (closes) each object when the block ends, even after an
// exception. For connections, that means the connection goes back to the pool.
public sealed class OracleWeatherRepository(IConfiguration configuration) : IWeatherRepository
{
    private readonly string _connectionString = configuration.GetConnectionString("Oracle")
        ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Oracle' is not configured.");

    public async Task<long> UpsertLocationAsync(Location location, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = Procedure(connection, "weather_pkg.upsert_location");

        // A PL/SQL FUNCTION's result comes back through a ReturnValue parameter.
        var result = command.Parameters.Add("result", OracleDbType.Int64, ParameterDirection.ReturnValue);
        command.Parameters.Add("p_name", OracleDbType.Varchar2, location.Name, ParameterDirection.Input);
        command.Parameters.Add("p_country", OracleDbType.Varchar2, location.Country, ParameterDirection.Input);
        command.Parameters.Add("p_admin1", OracleDbType.Varchar2, NullIfEmpty(location.Admin1), ParameterDirection.Input);
        command.Parameters.Add("p_latitude", OracleDbType.Decimal, location.Latitude, ParameterDirection.Input);
        command.Parameters.Add("p_longitude", OracleDbType.Decimal, location.Longitude, ParameterDirection.Input);

        await command.ExecuteNonQueryAsync(ct);

        // ODP.NET hands numbers back as OracleDecimal (Oracle NUMBER can hold more than a
        // .NET long), so convert explicitly.
        return ((OracleDecimal)result.Value).ToInt64();
    }

    public async Task LogSearchAsync(long locationId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = Procedure(connection, "weather_pkg.log_search");
        command.Parameters.Add("p_location_id", OracleDbType.Int64, locationId, ParameterDirection.Input);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<CachedWeather?> GetLatestWeatherAsync(long locationId, int maxAgeMinutes, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = Procedure(connection, "weather_pkg.get_latest_weather");
        command.Parameters.Add("p_location_id", OracleDbType.Int64, locationId, ParameterDirection.Input);
        command.Parameters.Add("p_max_age_minutes", OracleDbType.Int32, maxAgeMinutes, ParameterDirection.Input);
        command.Parameters.Add("p_result", OracleDbType.RefCursor, ParameterDirection.Output);

        // When a procedure has a REF CURSOR OUT parameter, ExecuteReader returns that cursor's rows.
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;   // never fetched before
        }

        var code = reader.GetInt32(reader.GetOrdinal("weather_code"));
        var (condition, description) = WeatherCodeMapper.Map(code);

        var weather = new CurrentWeather(
            Temperature: reader.GetDouble(reader.GetOrdinal("temperature")),
            FeelsLike: reader.GetDouble(reader.GetOrdinal("feels_like")),
            Humidity: reader.GetInt32(reader.GetOrdinal("humidity")),
            WindSpeed: reader.GetDouble(reader.GetOrdinal("wind_speed")),
            Precipitation: reader.GetDouble(reader.GetOrdinal("precipitation")),
            WeatherCode: code,
            IsDay: reader.GetInt32(reader.GetOrdinal("is_day")) == 1,
            Condition: condition,
            Description: description,
            ObservedAt: ReadUtc(reader, "observed_at"));

        return new CachedWeather(
            weather,
            FetchedAt: ReadUtc(reader, "fetched_at"),
            IsFresh: reader.GetInt32(reader.GetOrdinal("is_fresh")) == 1);
    }

    public async Task SaveWeatherAsync(long locationId, CurrentWeather weather, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = Procedure(connection, "weather_pkg.save_weather");
        command.Parameters.Add("p_location_id", OracleDbType.Int64, locationId, ParameterDirection.Input);
        command.Parameters.Add("p_temperature", OracleDbType.Decimal, weather.Temperature, ParameterDirection.Input);
        command.Parameters.Add("p_feels_like", OracleDbType.Decimal, weather.FeelsLike, ParameterDirection.Input);
        command.Parameters.Add("p_humidity", OracleDbType.Int32, weather.Humidity, ParameterDirection.Input);
        command.Parameters.Add("p_wind_speed", OracleDbType.Decimal, weather.WindSpeed, ParameterDirection.Input);
        command.Parameters.Add("p_precipitation", OracleDbType.Decimal, weather.Precipitation, ParameterDirection.Input);
        command.Parameters.Add("p_weather_code", OracleDbType.Int32, weather.WeatherCode, ParameterDirection.Input);
        command.Parameters.Add("p_is_day", OracleDbType.Int32, weather.IsDay ? 1 : 0, ParameterDirection.Input);
        command.Parameters.Add("p_observed_at", OracleDbType.TimeStamp, weather.ObservedAt.UtcDateTime, ParameterDirection.Input);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SearchHistoryItem>> GetRecentSearchesAsync(int limit, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = Procedure(connection, "weather_pkg.get_recent_searches");
        command.Parameters.Add("p_limit", OracleDbType.Int32, limit, ParameterDirection.Input);
        command.Parameters.Add("p_result", OracleDbType.RefCursor, ParameterDirection.Output);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<SearchHistoryItem>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(new SearchHistoryItem(
                LocationId: reader.GetInt64(reader.GetOrdinal("location_id")),
                Name: reader.GetString(reader.GetOrdinal("name")),
                Country: ReadString(reader, "country") ?? "",
                Admin1: ReadString(reader, "admin1"),
                Latitude: reader.GetDouble(reader.GetOrdinal("latitude")),
                Longitude: reader.GetDouble(reader.GetOrdinal("longitude")),
                LastSearchedAt: ReadUtc(reader, "last_searched_at")));
        }
        return items;
    }

    public async Task<IReadOnlyList<SearchStat>> GetSearchStatsAsync(int limit, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = Procedure(connection, "weather_pkg.get_search_stats");
        command.Parameters.Add("p_limit", OracleDbType.Int32, limit, ParameterDirection.Input);
        command.Parameters.Add("p_result", OracleDbType.RefCursor, ParameterDirection.Output);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var stats = new List<SearchStat>();
        while (await reader.ReadAsync(ct))
        {
            var avgOrdinal = reader.GetOrdinal("avg_temperature");
            stats.Add(new SearchStat(
                Name: reader.GetString(reader.GetOrdinal("name")),
                Country: ReadString(reader, "country") ?? "",
                SearchCount: reader.GetInt32(reader.GetOrdinal("search_count")),
                Rank: reader.GetInt32(reader.GetOrdinal("popularity_rank")),
                PercentOfSearches: reader.GetDouble(reader.GetOrdinal("pct_of_searches")),
                // A SQL NULL arrives as DBNull, not C# null, so check before reading.
                AverageTemperature: reader.IsDBNull(avgOrdinal) ? null : reader.GetDouble(avgOrdinal),
                LastSearchedAt: ReadUtc(reader, "last_searched_at")));
        }
        return stats;
    }

    public async Task<bool> CanConnectAsync(CancellationToken ct)
    {
        try
        {
            await using var connection = await OpenAsync(ct);
            await using var command = new OracleCommand("SELECT 1 FROM dual", connection);
            await command.ExecuteScalarAsync(ct);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    // ----- helpers -----

    private async Task<OracleConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new OracleConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;   // "throw;" re-throws the same exception and keeps its stack trace
        }
    }

    private static OracleCommand Procedure(OracleConnection connection, string name) => new(name, connection)
    {
        CommandType = CommandType.StoredProcedure,
        // IMPORTANT: by default ODP.NET matches parameters by POSITION, not name. BindByName
        // makes the names above match the PL/SQL parameter names, so order doesn't matter.
        BindByName = true
    };

    // We store UTC in plain TIMESTAMP columns, so the DateTime we read has Kind=Unspecified.
    // Label it as UTC explicitly.
    private static DateTimeOffset ReadUtc(DbDataReader reader, string column) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(column)), DateTimeKind.Utc));

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    // C# null must be sent as DBNull.Value to mean SQL NULL.
    private static object NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
