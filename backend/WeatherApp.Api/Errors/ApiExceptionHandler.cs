using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Oracle.ManagedDataAccess.Client;
using WeatherApp.Api.Services;

namespace WeatherApp.Api.Errors;

// A global exception handler. app.UseExceptionHandler() calls it for any exception an
// endpoint doesn't catch. Returning true means "handled"; false lets the next handler
// (the default 500 response) take over.
//
// Responses use the standard "ProblemDetails" JSON shape (RFC 9457):
//   { "type": "...", "title": "...", "status": 503, "detail": "..." }
// so the React app can read every error the same way.
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    // Must match weather_pkg.c_invalid_input (-20001). ODP.NET reports it as a positive number.
    private const int OracleInvalidInput = 20001;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        // A switch expression over the exception's type. Order matters: OracleException is
        // also a DbException, so the more specific pattern has to come first.
        (int Status, string Title, string Detail)? problem = exception switch
        {
            WeatherApiException => (StatusCodes.Status503ServiceUnavailable,
                "Weather service unavailable", exception.Message),

            // Our own RAISE_APPLICATION_ERROR: the message is safe and useful to show.
            OracleException { Number: OracleInvalidInput } oracle => (StatusCodes.Status400BadRequest,
                "Invalid request", CleanOracleMessage(oracle.Message)),

            // Any other database error: don't leak internal details to the browser.
            DbException => (StatusCodes.Status503ServiceUnavailable,
                "Database unavailable", "Search history is unavailable right now. Weather still works."),

            _ => null
        };

        if (problem is null)
        {
            return false;
        }

        var (status, title, detail) = problem.Value;
        logger.LogWarning(exception, "Returning {Status}: {Title}", status, title);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = detail }
        });
    }

    // "ORA-20001: Limit must be between 1 and 50.\nORA-06512: at ..." → "Limit must be between 1 and 50."
    private static string CleanOracleMessage(string message)
    {
        var firstLine = message.Split('\n')[0];
        var colon = firstLine.IndexOf(": ", StringComparison.Ordinal);
        return colon >= 0 ? firstLine[(colon + 2)..].Trim() : firstLine.Trim();
    }
}
