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
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Konfiguration (Phase 4): die Umgebungsvariablen COMFOBOX_* und die
// config.json im Datenverzeichnis. Die Datei hat Vorrang vor den Variablen.
// Die Optionen des HA-Add-ons (/data/options.json) haben wiederum Vorrang vor
// der config.json - sie werden zuletzt hinzugefügt.
// ---------------------------------------------------------------------------
var configurationWarnings = new List<string>();

GatewayConfiguration.AddGatewaySources(
    builder.Configuration,
    builder.Environment.ContentRootPath,
    configurationWarnings.Add);

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
    .MinimumLevel.Is(ParseLevel(loggingOptions.Level))
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
// Kompression (Brotli/Gzip): es gibt keinen nginx-Container, der komprimiert
// (→ ADR-007).
// ---------------------------------------------------------------------------
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// ---------------------------------------------------------------------------
// Host
// ---------------------------------------------------------------------------
Directory.CreateDirectory(
    GatewayConfiguration.ResolveDataDirectory(builder.Configuration, builder.Environment.ContentRootPath));

var app = builder.Build();

// Probleme beim Lesen der Konfiguration gehören in den Log - sie stehen
// sonst nur im Bildschirm des Nutzers.
foreach (var warning in configurationWarnings)
{
    app.Logger.LogWarning("{Konfiguration}", warning);
}

// Die Swagger-Oberfläche ist **nur** im Development-Modus erreichbar.
if (app.Environment.IsDevelopment())
{
    app.MapGatewaySwagger();
}

// Alle REST-/SSE-Endpunkte (/api/items, /api/status, /api/events, …).
app.MapGatewayApi();

// ---------------------------------------------------------------------------
// Die Web-Oberfläche (Angular) wird aus dem selben Host ausgeliefert - es
// gibt keinen nginx-Container (→ ADR-007).
// ---------------------------------------------------------------------------
app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

// Der SPA-Fallback steht **nach** den API-Routen: sonst würde er /api/…
// schlucken und index.html liefern.
app.MapFallbackToFile("index.html");

app.Run();

// Löst einen Level-Text in einen Serilog-Level um.
static LogEventLevel ParseLevel(string? level)
    => Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var parsed)
        ? parsed
        : LogEventLevel.Information;

