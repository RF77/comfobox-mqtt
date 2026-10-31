using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serial;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Logging;
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
// Konfiguration: binden, validieren, bei Start durchsetzen.
// ---------------------------------------------------------------------------
builder.Services.AddOptions<GatewayOptions>()
    .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(o => o.BaseTopic == "ComfoBox" || !o.BaseTopic.Contains('/'), "Gateway:BaseTopic darf kein '/' enthalten.")
    .ValidateOnStart();

builder.Services.AddOptions<BacnetOptions>()
    .Bind(builder.Configuration.GetSection(BacnetOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        o => BacnetOptions.IsAllowedBaudrate(o.Baudrate),
        $"Bacnet:Baudrate muss einer der folgenden Werte sein: {string.Join(", ", BacnetOptions.AllowedBaudrates)}.")
    .ValidateOnStart();

builder.Services.AddOptions<MqttOptions>()
    .Bind(builder.Configuration.GetSection(MqttOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        o => MqttOptions.HasValidBroker(o),
        "Mqtt:Brokers muss mindestens einen Broker mit Host und Port enthalten.")
    .ValidateOnStart();

builder.Services.AddOptions<LoggingOptions>()
    .Bind(builder.Configuration.GetSection(LoggingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------------------------------------------------------------------------
// Dienste
// ---------------------------------------------------------------------------
var dataDirectory = ResolveDataDirectory(builder.Environment.ContentRootPath, builder.Configuration);

builder.Services.AddSingleton<IItemCatalog>(_ => new ItemCatalog());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(logBuffer);
builder.Services.AddSingleton<IErrorCollector>(sp => new ErrorCollector(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<IValueCache>(sp =>
{
    var options = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;

    // Ein Wert gilt erst als veraltet, wenn er drei Poll-Zyklen alt ist -
    // sekundäre Items werden nur alle SecondaryPollFactor Zyklen gelesen.
    var staleAfter = TimeSpan.FromSeconds(
        options.PollIntervalSeconds * options.SecondaryPollFactor * 3);

    return new ValueCache(sp.GetRequiredService<TimeProvider>(), staleAfter);
});
builder.Services.AddSingleton<IWriteCounter>(sp =>
{
    var options = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;
    var path = Path.Combine(dataDirectory, WriteCounter.FileName);

    return new WriteCounter(path, options.MaxWritesPer24h, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddSingleton<IBacnetClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<BacnetOptions>>().Value;

    var serial = new BacnetSerialOptions
    {
        PortName = options.Port,
        BaudRate = options.Baudrate,
        ReadTimeoutMs = options.ItemTimeoutMs,
        WriteTimeoutMs = options.ItemTimeoutMs,
    };

    var transport = new BacnetMstpTransport(
        new SerialPortFactory(serial),
        new BacnetMstpOptions
        {
            ThisStation = (short)options.ThisStation,
            MaxMaster = (byte)options.MaxMaster,
            Serial = serial,
        },
        sp.GetRequiredService<ILogger<BacnetMstpTransport>>());

    var client = new BacnetClient(transport, sp.GetRequiredService<ILogger<BacnetClient>>())
    {
        Timeout = options.ItemTimeoutMs,
        Retries = options.Retries,
    };

    return client;
});
builder.Services.AddSingleton<BacnetDevice>();
builder.Services.AddHostedService<BacnetPollingService>();

// ---------------------------------------------------------------------------
// Host
// ---------------------------------------------------------------------------
Directory.CreateDirectory(dataDirectory);

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

// Ermittelt das Datenverzeichnis (Schreibzähler, später die Setup-Konfiguration).
static string ResolveDataDirectory(string contentRootPath, IConfiguration configuration)
{
    var configured = configuration.GetSection(GatewayOptions.SectionName)[nameof(GatewayOptions.DataDirectory)]
        ?? new GatewayOptions().DataDirectory;

    return Path.IsPathRooted(configured)
        ? configured
        : Path.Combine(contentRootPath, configured);
}

