namespace WeatherApp.Api.Services;

// Our own exception type for "the external weather API let us down".
// ApiExceptionHandler turns it into a 503 response, so callers never see
// raw HttpClient errors.
public sealed class WeatherApiException(string message, Exception? inner = null)
    : Exception(message, inner);
