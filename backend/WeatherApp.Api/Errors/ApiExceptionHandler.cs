using Microsoft.AspNetCore.Diagnostics;
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
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not WeatherApiException)
        {
            return false;
        }

        logger.LogWarning("Returning 503: {Message}", exception.Message);

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Weather service unavailable",
                Detail = exception.Message
            }
        });
    }
}
