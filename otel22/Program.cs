using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var resource = ResourceBuilder
    .CreateDefault()
    .AddService(typeof(Program).Assembly.FullName);

builder.Services
    // tracing
    .AddOpenTelemetryTracing(c => c
        .SetResourceBuilder(resource)
        .AddAspNetCoreInstrumentation()
        .AddSource(Traces.Name)
        .AddConsoleExporter()
        .AddOtlpExporter()
        )
    // metrics
    .AddOpenTelemetryMetrics(c => c
        .SetResourceBuilder(resource)
        .AddAspNetCoreInstrumentation()
        .AddMeter(Meters.Name)
        .AddConsoleExporter()
        .AddOtlpExporter()
        )
    // logging
    .AddLogging(configure => configure
        .ClearProviders()
        .AddOpenTelemetry(c => c
            .SetResourceBuilder(resource)
            .AddConsoleExporter()
            .AddOtlpExporter()
            )
        );


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", async (ILogger<Program> logger) =>
{
    using var activity = Traces.ActivitySource.StartActivity();

    Meters.EndpointHit?.Add(1);
    logger.LogInformation("This is a log for endpoint {endpoint}", "weatherforecast");
    var forecast = Enumerable.Range(1, 5).Select(index =>
       new WeatherForecast
       (
           DateTime.Now.AddDays(index),
           Random.Shared.Next(-20, 55),
           summaries[Random.Shared.Next(summaries.Length)]
       ))
        .ToArray();

    using (var scopedActivity = Traces.ActivitySource.StartActivity("my scoped activity"))
    {
        await Task.Delay(100);
        scopedActivity?.AddEvent(new("That was intense"));
    }

    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

class Meters
{
    public static string Name = typeof(Meters).Assembly.FullName!;
    static Meter _meter = new(Name);
    public static Counter<int> EndpointHit { get; } = _meter.CreateCounter<int>("endpoint-hit");
}

class Traces
{
    public static string Name = typeof(Traces).Assembly.FullName!;
    public static ActivitySource ActivitySource { get; } = new(Name);
}