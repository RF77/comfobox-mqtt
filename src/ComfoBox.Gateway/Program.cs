using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serial;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Logging;
using ComfoBox.Gateway.Mqtt;
using ComfoBox.Gateway.Protection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Logging (ADR-011): Serilog ist der einzige Provider, der Code schreibt
// immer gegen ILogger<T>.
// ---------------------------------------------------------------------------
var loggingOptions = builder.Configuration.GetSection(LoggingOptions.SectionName).Get<LoggingOptions>()
    ?? new LoggingOptions();

var logBuffer = new InMemoryLogSink(
    loggingOptions.RingBuffer.Capacity,
    ParseLevel(loggingOptions.RingBuffer.MinimumLevel));

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ComfoBox.Gateway")
    .Enrich.WithProperty("Version", typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0")
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.File(
        new RenderedCompactJsonFormatter(),
        path: Path.Combine(loggingOptions.Directory, "gateway-.jsonl"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: loggingOptions.RetainedFileCount,
        shared: true)
    .WriteTo.Sink(logBuffer));

// ---------------------------------------------------------------------------
// Dienste (Konfiguration, BACnet, MQTT).
// ---------------------------------------------------------------------------
builder.Services.AddGatewayServices(
    builder.Configuration,
    builder.Environment.ContentRootPath,
    logBuffer);

// ---------------------------------------------------------------------------
// Host
// ---------------------------------------------------------------------------
Directory.CreateDirectory(
    Path.Combine(
        builder.Environment.ContentRootPath,
        builder.Configuration.GetSection(GatewayOptions.SectionName)[nameof(GatewayOptions.DataDirectory)]
            ?? new GatewayOptions().DataDirectory));

var app = builder.Build();

app.MapGet("/api/health", (IValueCache cache, IErrorCollector errors) =>
{
    var values = cache.Snapshot();

    return Results.Ok(new
    {
        status = "online",
        items = values.Length,
        stale = values.Count(v => v.IsStale),
        errors = errors.Snapshot().Length,
    });
});

app.Run();

// Löst einen Level-Text in einen Serilog-Level um.
static LogEventLevel ParseLevel(string? level)
    => Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var parsed)
        ? parsed
        : LogEventLevel.Information;

