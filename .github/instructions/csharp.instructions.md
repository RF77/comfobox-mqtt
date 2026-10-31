---
description: "Use when writing or changing C# code in this repository — .NET 10 gateway, BACnet stack, domain model, tests, logging, configuration, or project files. Covers the net452-to-net10 migration, code style, async rules, and the MQTT compatibility contract."
applyTo: ["**/*.cs", "**/*.csproj", "**/*.props", "**/*.sln", "**/Directory.*.props"]
---

# C#-Regeln (ComfoBox MQTT)

## 1. Ziel-Stack

| Aspekt | Wert |
|---|---|
| TFM | `net10.0` (ein einziges, kein Multi-Targeting) |
| C# | 14 (implizit über das SDK) |
| Projektformat | SDK-Projekt (`<Project Sdk="Microsoft.NET.Sdk">`) |
| Pakete | `PackageReference` + Central Package Management (`src/Directory.Packages.props`) |
| Nullable | `enable` |
| Warnungen | `TreatWarningsAsErrors=true` |

**Altbestand:** `src_old/Bacnet`, `src_old/ComfoBoxLib`, `src_old/ComfoBoxMqtt`, `src_old/ComfoBoxMqttConsole`,
`src_old/ComfoboxService`, `src_old/DemoClient` sind **net452**-Projekte und **werden nicht
mehr gepflegt**. Sie dienen **nur** als Referenz (Protokollverhalten, Item-Metadaten,
Fehlerbilder).

> **Regel:** Änderungen gehören **immer** nach `src/` (`src/ComfoBox.Bacnet`, `src/ComfoBox.Domain`,
> `src/ComfoBox.Gateway`) – **nie** „quick and dirty" in `src_old/` reparieren.
> `src_old/` wird nicht gebaut; ein Build-Fehler dort ist **erwartet** und wird **nicht** behoben.
>
> **Ausnahme:** Wenn du den Altbestand liest, um Verhalten zu übernehmen, zitiere die
> Fundstelle als `src_old/…` – damit bleibt der Bezug zum Legacy-Code erkennbar.

## 2. Layering

```
ComfoBox.Domain      → keine Abhängigkeiten (kein ILogger, kein log4net, kein NuGet)
ComfoBox.Bacnet      → System.IO.Ports + Microsoft.Extensions.Logging.Abstractions
ComfoBox.Gateway     → MQTTnet, ASP.NET Core, ComfoBox.Domain, ComfoBox.Bacnet
```

- `ComfoBox.Domain` ist **framework-frei** und enthält nur Daten + Logik
- Keine Zyklen; Abhängigkeiten zeigen immer nach innen
- Kein `ProjectReference` von `Domain` nach außen

## 3. Code-Stil

````csharp
// ✅ Records für Datenklassen
public sealed record ItemDefinition
{
    public required string Id { get; init; }
    public required int BacnetObjectId { get; init; }
}

// ✅ Pattern Matching, Primary Constructor, switch-Expression
if (value is AnalogValue a && a.Min is not null) { … }

// ✅ CancellationToken durchgereicht
public async Task<CachedValue> GetOrReadAsync(string id, CancellationToken ct)

// ❌ async void (außer Event-Handler mit try/catch)
public async void Poll() { … }

// ❌ lock (this)
lock (this) { … }

// ❌ statischer veränderlicher Zustand
public static int CurrentNumberOfWrites;

// ❌ throw ex
catch (Exception ex) { throw ex; }   // → throw;
````

| Regel | Details |
|---|---|
| Benennung | `PascalCase` Typen/Methoden, `camelCase` Locals/Parameter, `_camelCase` private Felder, `I`-Prefix für Interfaces |
| Dateien | Eine Klasse pro Datei; Dateiname = Typname |
| Zeilenlänge | 120 Zeichen |
| Kommentare | **Deutsch**, nur für das *Warum*. Kein Kommentar, der das *Was* wiederholt |
| `this.` | nicht nötig (implizit) |
| `var` | bei offensichtlichem Typ ok; bei komplexen Typen explizit |

## 4. Async & Threads

| Regel | Grund |
|---|---|
| `ConfigureAwait(false)` nicht nötig | kein `SynchronizationContext` im ASP.NET Core |
| **Kein** `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | Deadlock + Thread-Pool-Starvation |
| `SemaphoreSlim` für die Serialisierung des BACnet-Busses | der Bus kann nur eine Anfrage gleichzeitig |
| `CancellationToken` in allen langlaufenden Methoden | sauberes Herunterfahren |
| `TimeProvider` injizieren statt `DateTime.Now` | testbar (Zeit-Fenster des Schreibzählers) |
| `Channel<T>` für Ereignis-Pipelines | Health-Events, Wertänderungen |

## 5. Logging

````csharp
// ✅ Strukturierte Parameter
_logger.LogWarning("ReadProperty {Object} timed out after {Timeout} ms", item.Id, timeout);

// ❌ String-Interpolation
_logger.LogWarning($"ReadProperty {item.Id} timed out");   // → vermeiden
````

| Level | Wann |
|---|---|
| `Trace` | niemals (nicht in .NET üblich) |
| `Debug` | Frame-/Protokolldetail im BACnet-Stack |
| `Information` | Start/Stop, Connect/Disconnect, Konfigurationsänderungen |
| `Warning` | Timeouts, Degraded-Zustände, abgelehnte Schreibvorgänge |
| `Error` | Fehler mit Auswirkung (Port-Fehler, Broker-Fehler, BACnet-Busfehler) |
| `Critical` | Prozess wird beendet |

> **`System.Diagnostics.Trace` ist verboten.** Im Legacy-BACnet-Stack wurde es 26× verwendet
> und war im Release-Build unsichtbar → in `ILogger` umwandeln.

> **`Console.WriteLine` ist ebenfalls verboten** – es umgeht alle Sinks und Filter.

### 5.1 Serilog & In-Memory-Ringbuffer

| Punkt | Regel |
|---|---|
| Provider | **Serilog** (`Serilog.Extensions.Hosting`) – der einzige |
| Abstraktion | im Code immer `Microsoft.Extensions.Logging.ILogger<T>` |
| Sinks | Console (JSON), Datei (JSON, 14 Tage), **In-Memory-Ringbuffer** |
| Ringpuffer | `src/ComfoBox.Gateway/Logging/InMemoryLogSink.cs`, Default **1000** Einträge |
| Kapazität | `Logging:RingBuffer:Capacity` (`0` = aus), Obergrenze 100 000 |
| Zweck | die Web-UI zeigt die Logs an (Diagnoseseite → Reiter „Protokoll") |

**Regeln für den Sink:**

````csharp
// ✅ Immer ILogger – nie direkt die Sink-Klasse
_logger.LogInformation("Connected to broker {Broker}", broker.Host);

// ❌ Verboten: Passwörter und Tokens niemals loggen
_logger.LogInformation("Connecting with {Password}", options.Password);
````

| Verboten | Begründung |
|---|---|
| `Console.WriteLine` | umgeht Sinks und Filter |
| `System.Diagnostics.Trace` | unsichtbar im Release (Befund A16) |
| Secrets loggen | der Ringpuffer ist in der Web-UI sichtbar |
| `throw` in `Emit` | ein Log-Fehler darf den Gateway nie stoppen |
| Allokation im `Emit` | der Ringpuffer muss im Betrieb allokationsfrei sein |

> **Details:** [logging.md](../../docs/02-modernisierung/logging.md) ·
> [ADR-011](../../docs/05-entscheidungen/adr-011-logging-serilog-und-ringbuffer.md)

## 6. Konfiguration

- Bindung über `Microsoft.Extensions.Configuration` + `IOptions<T>`
- **Immer validieren**: `.Validate(...).ValidateOnStart()` → Startabbruch statt Laufzeitfehler
- Konfigurationsklassen: `sealed class` mit `init`-Properties, Default aus der Klasse
- Secrets (Passwörter) niemals loggen, nie in `appsettings.json` committen

````csharp
builder.Services.AddOptions<BacnetOptions>()
    .Bind(builder.Configuration.GetSection(BacnetOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Port), "Bacnet:Port fehlt")
    .Validate(o => o.Baudrate is 38400 or 76800, "Bacnet:Baudrate muss 38400 oder 76800 sein")
    .ValidateOnStart();
````

## 7. Fehlerbehandlung

| Situation | Verhalten |
|---|---|
| BACnet-Lesefehler | **nicht** den Gateway zerstören → Health `degraded`, Retry mit Backoff |
| BACnet-Schreibfehler | 502 an den Aufrufer, Fehler in `IHealthReporter` |
| Ungültiger Schreibwert | ablehnen (400/422), **nie** einen Default schreiben |
| MQTT-Broker weg | Backoff-Reconnect, Health `degraded`, BACnet läuft weiter |
| Unerwartete Exception im Host | Health `Critical` + kontrollierter Neustart der Komponente |

> **Legacy-Falle:** `StartPollingAsync` im alten Code tat
> `catch { _comfoBoxClient.Dispose(); throw; }` → ein einzelner Lesefehler riss alles ab.
> Das wird **nicht** übernommen.

## 8. Tests

| Testart | Wo | Regel |
|---|---|---|
| Unit | `ComfoBox.Domain.Tests` | keine I/O, keine Netzwerke, kein `Task.Delay` |
| Integration | `ComfoBox.Gateway.Tests` | Testcontainers/Mosquitto, `WebApplicationFactory` |
| Golden-File | `ComfoBox.Bacnet.Tests` | Fixtures **nicht** ändern |
| Contract | `ComfoBox.Contract.Tests` | vergleicht gegen `docs/topics.json` |

````csharp
[Fact]
public void AnalogPayload_Parses_RegardlessOfCurrentCulture()
{
    var previous = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        Assert.True(MqttPayload.TryParse("23.5", out var v));
        Assert.Equal(23.5f, v);
    }
    finally { CultureInfo.CurrentCulture = previous; }
}
````

| Coverage-Ziel | Wert |
|---|---|
| `ComfoBox.Domain` | ≥ 90 % |
| `ComfoBox.Gateway` | ≥ 80 % |
| `ComfoBox.Bacnet` | ≥ 60 % |

**Nie** `Assert.True(x.Contains(y))` → `Assert.Contains(y, x)`.
**Nie** `Assert.Equal(expected, actual)` mit vertauschten Argumenten verwechseln.

## 9. Migration der Legacy-Projekte

Reihenfolge (nicht abkürzen!):

1. **Golden-Tests gegen den alten Code schreiben** (Fixture aus `docs/Mono/MoMA Scan Results.html`)
2. `ComfoBox.Bacnet` anlegen, Codec 1:1 portieren
3. `BacnetClient` portieren
4. MSTP/Serial-Transport portieren
5. Pipe/UDP/PTP löschen
6. `Trace` → `ILogger`
7. Reconnect/Timeout härten

> Ein Golden-Test, der nach der Portierung fehlschlägt, ist **kein** Anlass zum Anpassen des
> Fixtures, sondern ein Zeichen für eine Verhaltensänderung → zurück zur Analyse.

## 10. Zentrale Dateien

| Anliegen | Datei |
|---|---|
| Item-Definitionen | `src/ComfoBox.Domain/Items/Catalog.cs` |
| Payload-Format (InvariantCulture) | `src/ComfoBox.Gateway/Mqtt/MqttPayload.cs` |
| MQTT-Verbindung | `src/ComfoBox.Gateway/Mqtt/MqttConnectionManager.cs` |
| Health | `src/ComfoBox.Gateway/Health/HealthReporter.cs` |
| Schreibzähler | `src/ComfoBox.Gateway/Protection/WriteCounter.cs` |
| Konfiguration | `src/ComfoBox.Gateway/Configuration/*.cs` |
| Golden-Fixtures | `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json` |
| Topic-Fixture | `docs/topics.json` |
