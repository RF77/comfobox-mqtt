using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serial;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway;
using ComfoBox.Gateway.Api;
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
// Konfiguration (Phase 4): die Umgebungsvariablen COMFOBOX_* und die
// config.json im Datenverzeichnis. Die Datei hat Vorrang vor den Variablen.
// ---------------------------------------------------------------------------
GatewayConfiguration.AddGatewaySources(builder.Configuration, builder.Environment.ContentRootPath);

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
// Dienste (Konfiguration, BACnet, MQTT) und die REST/SSE-Schnittstelle.
// ---------------------------------------------------------------------------
builder.Services.AddGatewayServices(
    builder.Configuration,
    builder.Environment.ContentRootPath,
    logBuffer);

builder.Services.AddGatewayApi(
    builder.Configuration,
    builder.Environment.ContentRootPath);

// ---------------------------------------------------------------------------
// Host
// ---------------------------------------------------------------------------
Directory.CreateDirectory(
    GatewayConfiguration.ResolveDataDirectory(builder.Configuration, builder.Environment.ContentRootPath));

var app = builder.Build();

// Die Swagger-Oberfläche ist **nur** im Development-Modus erreichbar.
if (app.Environment.IsDevelopment())
{
    app.MapGatewaySwagger();
}

// Alle REST-/SSE-Endpunkte (/api/items, /api/status, /api/events, …).
app.MapGatewayApi();

app.Run();

// Löst einen Level-Text in einen Serilog-Level um.
static LogEventLevel ParseLevel(string? level)
    => Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var parsed)
        ? parsed
        : LogEventLevel.Information;

