using WeatherApp.Api.Data;
using WeatherApp.Api.Endpoints;
using WeatherApp.Api.Errors;
using WeatherApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Register services (dependency injection)
//    We describe *what* the app needs; the framework creates the objects and
//    passes them to whoever asks for them in a constructor or endpoint.
//    Lifetimes:  Singleton = one for the whole app
//                Scoped    = one per HTTP request
//                Transient = a new one every time it's asked for
// ---------------------------------------------------------------------------
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.Configure<OpenMeteoOptions>(
    builder.Configuration.GetSection(OpenMeteoOptions.SectionName));
builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));

// AddHttpClient registers OpenMeteoClient as the IOpenMeteoClient implementation and
// gives it a pooled HttpClient. AddStandardResilienceHandler wraps every request with
// retries (for brief network blips), per-attempt and total timeouts, and a circuit breaker.
builder.Services
    .AddHttpClient<IOpenMeteoClient, OpenMeteoClient>()
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    });

builder.Services.AddSingleton(TimeProvider.System);
// Singleton is fine: the repository holds only a connection string, and ODP.NET
// manages the connection pool itself.
builder.Services.AddSingleton<IWeatherRepository, OracleWeatherRepository>();
builder.Services.AddScoped<WeatherService>();

// CORS lets a page on another origin (the React dev server) call this API directly.
// In development Vite proxies /api, so this is a fallback for direct calls.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------------------------------------------------------------------------
// 2. Configure the request pipeline (middleware runs in this order per request)
// ---------------------------------------------------------------------------
app.UseExceptionHandler();   // turn uncaught exceptions into ProblemDetails responses
app.UseStatusCodePages();    // give bare 404s etc. a ProblemDetails body too
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();        // API description at /openapi/v1.json
}

app.MapWeatherEndpoints();

app.Run();
