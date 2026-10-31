# .NET-Migration

## 1. Ziel

Alle Projekte werden von **.NET Framework 4.5.2 / `packages.config`** auf **.NET 10 / SDK-Projektformat
mit `PackageReference`** umgestellt. Danach ist `dotnet build` ohne Visual Studio, ohne Mono und
ohne Windows möglich.

## 2. Paket-Mapping

| Alt (`packages.config`) | Neu (`PackageReference`) | Anmerkung |
|---|---|---|
| log4net 2.0.5 | `Microsoft.Extensions.Logging.*` + `Serilog.Extensions.Hosting` | Serilog-Provider für strukturierte JSON-Logs |
| M2Mqtt 4.3.0.0 | `MQTTnet` 5.2.0.1603 (+ `MQTTnet.AspNetCore`) | siehe [mqtt-library-migration](mqtt-library-migration.md) |
| Fasterflect 2.1.3 | — (entfällt) | deklarativer Item-Katalog, siehe [datenmodell-und-api](datenmodell-und-api.md) |
| Newtonsoft.Json 8.0.3 | — (entfällt) | `System.Text.Json` |
| Nito.AsyncEx 3.0.1 | — (entfällt) | `Microsoft.Extensions.Hosting` |
| — | `System.IO.Ports` 10.0.x | **neu** – `System.IO.Ports` ist nicht in der BCL |
| — | `Microsoft.Extensions.Hosting` 10.0.x | neu |
| — | `Microsoft.Extensions.Configuration.*` 10.0.x | neu |
| — | `Swashbuckle.AspNetCore` 9.x | neu, OpenAPI |

### Central Package Management

````xml
<!-- src/Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MQTTnet" Version="5.2.0.1603" />
    <PackageVersion Include="System.IO.Ports" Version="10.0.0" />
    <PackageVersion Include="Serilog.Extensions.Hosting" Version="9.0.0" />
    <!-- … -->
  </ItemGroup>
</Project>
````

### SDK-Projektformat

````xml
<!-- src/ComfoBox.Domain/ComfoBox.Domain.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
````

`net10.0` als **einziges** TFM: alle Konsumenten (Gateway, Tests, Docker) laufen auf Linux.
`EnableWindowsTargeting` ist **nicht** nötig, weil der WPF-`DemoClient` entfällt
(siehe [ui-angular-optimus](ui-angular-optimus.md#ablösung-des-democlients)).

## 3. Projekttransformation

| Alt | Neu | Transformation |
|---|---|---|
| `src/Bacnet` | `src/ComfoBox.Bacnet` | nur MSTP/Serial behalten, `System.IO.Ports` |
| `src/ComfoBoxLib` | `src/ComfoBox.Domain` (Katalog/Werte) **+** `ComfoBox.Gateway/Bacnet` (I/O) | Trennung: Fachmodell vs. I/O |
| `src/ComfoBoxMqtt` | `src/ComfoBox.Gateway/Mqtt` | Rewrite auf MQTTnet |
| `src/ComfoBoxMqttConsole` | `src/ComfoBox.Gateway` | → ASP.NET Core Host |
| `src/ComfoboxService` | — | entfällt (Docker bzw. `WindowsService`-Host) |
| `src/DemoClient` (WPF) | — | entfällt → wird durch die Web-UI ersetzt |
| `src/ServiceInstaller` (WiX) | — | entfällt → `dotnet publish` + Container |
| `src/XmlConfigMerge` (Binär) | — | entfällt |

## 4. Settings → `Microsoft.Extensions.Configuration`

`ApplicationSettingsBase` existiert in .NET Core/.NET 5+ nicht. Ersatz:

````csharp
// src/ComfoBox.Gateway/Configuration/GatewayOptions.cs
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public string BaseTopic { get; set; } = "ComfoBox";       // Teil des MQTT-Vertrags
    public int PollIntervalSeconds { get; set; } = 60;
    public bool UseMqttRetain { get; set; } = true;
    public bool ExpertMode { get; set; }
    public int MaxNumberOfWritesPer24h { get; set; } = 200;
}

public sealed class BacnetOptions
{
    public const string SectionName = "Bacnet";
    public string Port { get; set; } = "/dev/ttyUSB0";
    public int Baudrate { get; set; } = 38400;
    public short ClientId { get; set; } = 3;      // MSTP This-Station
    public short MasterId { get; set; } = 1;      // ELESTA Device-ID
    public int ResponseTimeoutMs { get; set; } = 1000;
    public int Retries { get; set; } = 3;
}

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";
    public List<MqttBrokerOptions> Brokers { get; set; } = [];
    public string DiscoveryPrefix { get; set; } = "homeassistant";
    public bool EnableDiscovery { get; set; } = true;
    public bool EmbedBroker { get; set; }
    public int EmbeddedBrokerPort { get; set; } = 1883;
}

public sealed class MqttBrokerOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; }
    public bool AllowUntrustedCertificates { get; set; }   // nur für selbstsignierte Certs im LAN
}
````

### Bindung & Validierung beim Start

````csharp
// src/ComfoBox.Gateway/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables("COMFOBOX_")      // COMFOBOX_Mqtt__Brokers__0__Host=…
    .AddCommandLine(args);

builder.Services.AddOptions<GatewayOptions>()
    .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseTopic), "Gateway:BaseTopic darf nicht leer sein")
    .Validate(o => o.MaxNumberOfWritesPer24h >= 0, "Gateway:MaxNumberOfWritesPer24h muss >= 0 sein")
    .ValidateOnStart();

builder.Services.AddOptions<BacnetOptions>()
    .Bind(builder.Configuration.GetSection(BacnetOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Port), "Bacnet:Port fehlt (z. B. /dev/ttyUSB0)")
    .Validate(o => o.Baudrate is 38400 or 76800, "Bacnet:Baudrate muss 38400 oder 76800 sein")
    .ValidateOnStart();

builder.Services.AddOptions<MqttOptions>()
    .Bind(builder.Configuration.GetSection(MqttOptions.SectionName))
    .Validate(o => o.Brokers.Count > 0, "Mqtt:Brokers ist leer")
    .ValidateOnStart();

builder.Services.AddHostedService<GatewayHostedService>();
builder.Services.AddSingleton<IBacnetDevice, BacnetDevice>();
builder.Services.AddSingleton<IValueCache, ValueCache>();
builder.Services.AddSingleton<IMqttPublisher, MqttPublisher>();
builder.Services.AddSingleton<IHealthReporter, HealthReporter>();
builder.Services.AddSingleton<ItemCatalog>();
````

> **Befund B12/F12 behoben:** `ValidateOnStart()` lässt den Prozess mit einer klaren Meldung
> abbrechen, statt zur Laufzeit still zu scheitern.

### Beispiel `appsettings.json`

````json
{
  "Gateway": {
    "BaseTopic": "ComfoBox",
    "PollIntervalSeconds": 60,
    "UseMqttRetain": true,
    "ExpertMode": false,
    "MaxNumberOfWritesPer24h": 200
  },
  "Bacnet": {
    "Port": "/dev/ttyUSB0",
    "Baudrate": 38400,
    "ClientId": 3,
    "MasterId": 1
  },
  "Mqtt": {
    "EnableDiscovery": true,
    "DiscoveryPrefix": "homeassistant",
    "Brokers": [
      { "Host": "192.168.1.10", "Port": 1883, "Username": "comfobox", "Password": "…" }
    ]
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Information", "Override": { "Microsoft.AspNetCore": "Warning" } }
  }
}
````

## 5. Logging

log4net wird durch `Microsoft.Extensions.Logging` (Abstraktion) + Serilog (Provider) ersetzt.

````csharp
// src/ComfoBox.Gateway/Program.cs
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.File(new RenderedCompactJsonFormatter(),
                  path: "/var/log/comfobox/gateway-.jsonl",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 14));
````

**Wichtig:** Der BACnet-Stack muss von `System.Diagnostics.Trace` auf `ILogger` umgestellt werden
(Befund A16 – 26 Fundstellen). Portierungsregel:

````csharp
// alt
Trace.WriteLine($"Connection Error in {nameof(Idle)}");

// neu
_logger.LogWarning("BACnet MSTP connection error in {Method}", nameof(Idle));
````

> `System.Diagnostics.Trace` kann **nicht** global umgeleitet werden – der Code muss umgestellt werden.

## 6. Host & Lebenszyklus

`AsyncContext.Run` + `while(true)` + `Thread.Sleep` entfallen. Der Host übernimmt:

````csharp
// src/ComfoBox.Gateway/GatewayHostedService.cs
internal sealed class GatewayHostedService(
    IBacnetDevice device,
    IMqttPublisher mqtt,
    ItemCatalog catalog,
    IHealthReporter health,
    IOptions<GatewayOptions> options,
    ILogger<GatewayHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await device.ConnectAsync(stoppingToken);          // WhoIs → IAm, mit Retry
        await mqtt.ConnectAsync(stoppingToken);             // + LWT + Discovery
        await mqtt.PublishStatusAsync(catalog, stoppingToken);

        await Task.WhenAll(
            PollLoopAsync(stoppingToken),
            mqtt.ListenAsync(stoppingToken));
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        var schedule = catalog.Items.ToLookup(i => i.RefreshPriority);
        while (!ct.IsCancellationRequested)
        {
            foreach (var item in schedule[RefreshPriority.Primary])
                await ReadAndPublishAsync(item, ct);

            await Task.Delay(TimeSpan.FromSeconds(_options.Value.PollIntervalSeconds), ct);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Sauberes Herunterfahren: erst MQTT "offline", dann BACnet schließen
        await mqtt.PublishOfflineAsync(CancellationToken.None);
        await base.StopAsync(cancellationToken);
    }
}
````

**Behebt:** A9 (Reconnect ohne Cancellation), A11 (Reconnect beim Stoppen), A12 (NRE beim Stop),
A1 (`async void`), A7 (Fehler killt alles).

### Windows-Dienst (optional)

Falls der Windows-Service weiterhin gebraucht wird:

````csharp
builder.Host.UseWindowsService(options => options.ServiceName = "ComfoBoxGateway");
````

Das Paket `Microsoft.Extensions.Hosting.WindowsServices` ist **nur** unter Windows verfügbar –
deshalb ist es ein optionales, per `#if WINDOWS` bzw. separates Projekt abgesichertes Paket.
Der Hauptpfad ist Docker (siehe [ADR-004](../05-entscheidungen/adr-004-containerisierung.md)).

## 7. C#-Sprachversion

Der Ist-Zustand ist implizit **C# 6**. Mit .NET 10 steht C# 14 zur Verfügung. Bewusste
Neuerungen, die den Code lesbarer machen:

| Muster | Vorher | Nachher |
|---|---|---|
| Nullprüfung | `if (x != null && x.Y != null)` | `if (x?.Y is not null)` |
| Pattern Matching | – | `if (value is AnalogValue a && a.Min is not null)` |
| Records | – | `public sealed record ItemMetadata(string Id, string Topic, …);` |
| `switch` | `switch (x) { case 1: … }` | `switch (x) { case 1 => …, _ => … }` |
| Interpolierte Strings | ✓ | ✓ |
| `required`/`init` | – | `public required string Topic { get; init; }` |
| Primäre Konstruktoren | – | in den Beispielen verwendet |

**Verboten bleibt:** `async void` (außer bei echten Event-Handlern mit `try/catch`),
`lock(this)`, statische mutable Zustände, `throw ex`.

## 8. `Nullable` & Analyser

````xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningAsError>CS8600;CS8602;CS8603;CS8618</WarningAsError>
  </PropertyGroup>
</Project>
````

`CA1848` (Logging mit `LoggerMessage` Delegates) wird **nicht** erzwungen – der Overhead rechtfertigt
die Umstellung nicht bei jedem Log-Aufruf.

## 9. Zentrale Package-Versionen pro Projekt

| Projekt | Pakete |
|---|---|
| `ComfoBox.Domain` | keine (framework-frei) |
| `ComfoBox.Bacnet` | `System.IO.Ports`, `Microsoft.Extensions.Logging.Abstractions` |
| `ComfoBox.Gateway` | `MQTTnet`, `MQTTnet.AspNetCore`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.*`, `Serilog.Extensions.Hosting`, `Swashbuckle.AspNetCore` |
| `ComfoBox.Web` | keine C#-Pakete (Angular) |

`ComfoBox.Domain` bleibt **frei von allen Abhängigkeiten** – dadurch ist es ohne Umstände testbar
und später z. B. für einen AOT-Build oder eine WASM-Variante nutzbar.

## 10. Migrations-Checkliste (Projekt `ComfoBox.Domain`)

1. `dotnet new classlib -n ComfoBox.Domain -f net10.0` in `src/`
2. `Values/*` und `Items/*` aus `ComfoBoxLib` übernehmen
3. `Fasterflect`-abhängige Enumeration durch deklarativen Katalog ersetzen
4. `log4net`-Referenzen entfernen, `ILogger` injizieren
5. `IsReadOnly`-Semantik (ExpertMode) unverändert übernehmen
6. `DateValue` entweder fertig implementieren **oder** aus dem Katalog entfernen (Befund A20) –
   *Entscheidung: aus dem Katalog entfernen, es gibt keine nutzbaren Date-Items*
7. `dotnet build` grün, Tests grün
