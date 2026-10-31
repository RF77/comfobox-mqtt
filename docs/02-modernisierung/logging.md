# Logging – Serilog & In-Memory-Ringbuffer

> Ziel: **strukturierte Logs** im Backend und **Live-Ansicht der Logs in der Web-UI**,
> ohne dass der Benutzer auf dem NAS (Synology) oder im Container-Log herumklicken muss.
>
> Entscheidung: [ADR-011](../05-entscheidungen/adr-011-logging-serilog-und-ringbuffer.md).
> Abhängigkeiten: [dotnet-migration.md §5](dotnet-migration.md#5-logging),
> [health-und-status.md](health-und-status.md), [ui-angular-optimus.md §7](ui-angular-optimus.md#7-diagnoseseite).

---

## 1. Warum Serilog?

Im Altbestand schrieb **log4net 2.0.5** in `app.config`-definierte Dateien. Das war:

- an .NET Framework 4.5.2 und Windows-Pfade gebunden,
- **im Release-Build praktisch unsichtbar** (Befund A16 – der BACnet-Stack nutzte `Trace`),
- nicht strukturiert – aus einem Logfile-Text ließen sich Fehler nicht maschinell auswerten.

| | Altbestand | Ziel |
|---|---|---|
| Abstraktion | `log4net.ILog` | `Microsoft.Extensions.Logging.ILogger<T>` |
| Provider | log4net | **Serilog** (`Serilog.Extensions.Hosting`) |
| Format | Text-Dateien | JSON (strukturiert) + Klartext für Konsole |
| Ablage | Datei im Arbeitsverzeichnis | Datei (rolling) + **Konsole** + **In-Memory-Ringbuffer** |
| Auswertung | manuell mit einem Editor | **Web-UI (Diagnoseseite)** |

> **Warum Serilog und nicht nur `Microsoft.Extensions.Logging`?**
> `Microsoft.Extensions.Logging` ist die *Abstraktion* – jeder Code schreibt gegen `ILogger`.
> Serilog ist der *Provider* und bringt mit, was wir brauchen:
> strukturierte Properties, `RenderedCompactJsonFormatter`, Rolling-File-Sinks und
> **eigene Sinks** – und genau die brauchen wir für den Ringbuffer.

---

## 2. Grundsätze

| Regel | Begründung |
|---|---|
| Im Code **immer** `ILogger<T>` | Austauschbarkeit des Providers, Testbarkeit |
| **Kein** `System.Diagnostics.Trace` | im Release unsichtbar (Befund A16) |
| **Kein** `Console.WriteLine` | umgeht alle Sinks und Filter |
| Strukturierte Parameter, keine Interpolation | `LogWarning("Read {Object} timed out", id)` |
| Serilog ist der **einzige** Provider | keine doppelte Ausgabe in die Konsole |
| Secrets werden **nie** geloggt | Passwörter, Tokens, Broker-Credentials |
| Jeder Fehler landet in **drei** Kanälen | Log, `ComfoBox/Status/Errors`, UI |

Die letzte Regel ist die Brücke zum Altbestand: Der Fehler-Ringpuffer aus
[health-und-status.md §4](health-und-status.md#4-fehler-sammlung) (letzte **50** Fehler) und
der Log-Ringpuffer (letzte **1000** Einträge) sind **zwei verschiedene** Dinge – siehe §5.

---

## 3. Serilog-Konfiguration

### 3.1 Setup im Host

````csharp
// src/ComfoBox.Gateway/Program.cs
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ComfoBox.Gateway")
    .Enrich.WithProperty("Version", typeof(Program).Assembly.GetName().Version?.ToString())
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.File(
        new RenderedCompactJsonFormatter(),
        path: "/var/log/comfobox/gateway-.jsonl",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true));
````

### 3.2 Sinks

| Sink | Zweck | Standard |
|---|---|---|
| **Console** | `docker logs`, Supervisor-Log | JSON (`RenderedCompactJsonFormatter`) |
| **File** | 14 Tage Historie auf dem Volume | JSON, täglich rotierend |
| **In-Memory-Ringbuffer** | **Log-Viewer in der Web-UI** | 1000 Einträge, siehe §4 |

> **Warum JSON auf der Konsole?** Der Docker-Log landet im `docker logs` und in der
> Synology-Logrotation. JSON ist maschinell filterbar (`jq`) und behält die Properties.
> Für Menschen genügt das **JSON-Log im UI** (§6) – dort wird die Meldung gerendert.

### 3.3 Dateirechte

Der Container läuft als **unprivilegierter** Benutzer. Das Log-Verzeichnis muss dem
Gateway-Benutzer gehören:

````yaml
# compose.yaml
volumes:
  - ./logs:/var/log/comfobox
environment:
  - COMFOBOX_Logging__Directory=/var/log/comfobox
````

> **Fallstrick:** Ein schreibgeschützter Mount (`ro`) lässt den Container beim Start
> mit einer Exception abbrechen. Der Mount muss **beschreibbar** sein.

---

## 4. Der In-Memory-Ringbuffer

### 4.1 Zweck

Der Benutzer soll in der Web-UI sehen, **was das Gateway gerade tut** – ohne SSH, ohne
`docker logs`, ohne DSM-Logbrowser. Dazu hält das Gateway die letzten Log-Einträge im
Speicher.

### 4.2 Anforderungen

| # | Anforderung | Umsetzung |
|---|---|---|
| 1 | Fester Speicherbedarf | Ringpuffer mit fester Kapazität, kein Wachstum |
| 2 | Konfigurierbar | `Logging:RingBuffer:Capacity`, Standard **1000** |
| 3 | Thread-sicher | `lock`-freie Queue + Snapshot per Volatile-Read |
| 4 | Kein I/O im Log-Pfad | `Emit` schreibt nur in eine Struktur |
| 5 | Filterbar | Level, Quelle (Logger-Kategorie), Volltext |
| 6 | Ohne Secrets | Redaktion vor dem Speichern |
| 7 | Abschaltbar | `Capacity = 0` → Puffer aus, kein Overhead |

### 4.3 Kapazität und Speicherbedarf

| `Capacity` | Ø. Eintragsgröße | Speicherbedarf | Sinn |
|---|---|---|---|
| 0 | – | 0 | Puffer aus (Default in sehr restriktiven Umgebungen) |
| 100 | ~400 B | ~40 KB | nur Warnungen und Fehler |
| **1000** | ~400 B | **~400 KB** | **Standard** – deckt einen Betriebstag ab |
| 5000 | ~400 B | ~2 MB | Diagnose bei häufigen Fehlern |

> **Standard 1000** bei ~400 Byte pro Eintrag ≈ **400 KB** – vernachlässigbar neben dem
> BACnet-Stack und dem MQTT-Client. Der Puffer ist **flüchtig**: nach einem Neustart
> ist er leer. Das ist gewollt – er ersetzt **keine** Dateilog-Historie, sondern macht
> die *aktuelle* Sitzung sichtbar.

### 4.4 Die Serilog-Sink

Eine eigene Sink ist der saubere Weg: Serilog ruft `Emit` für **jedes** Log-Ereignis auf,
unabhängig davon, ob eine Datei-Sink konfiguriert ist.

````csharp
// src/ComfoBox.Gateway/Logging/InMemoryLogSink.cs
namespace ComfoBox.Gateway.Logging;

/// <summary>
/// Serilog-Sink, die die letzten Log-Eintraege im Speicher haelt (Ringpuffer).
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink, IDisposable
{
    private readonly LogEntry[] _buffer;
    private readonly LogEventLevel _minimumLevel;
    private int _writeIndex;   // atomar: naechster Schreibplatz
    private long _count;       // atomar: Anzahl geschriebener Eintraege

    public InMemoryLogSink(int capacity, LogEventLevel minimumLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _buffer = new LogEntry[capacity];
        _minimumLevel = minimumLevel;
    }

    public void Emit(LogEvent logEvent)
    {
        if (_buffer.Length == 0 || logEvent.Level < _minimumLevel)
            return;

        var entry = LogEntry.From(logEvent);

        // Slot reservieren; bei Ueberlauf ueberschreibt der neueste Eintrag
        // den aeltesten (Ringpuffer-Verhalten).
        var index = (int)(Interlocked.Increment(ref _writeIndex) - 1);
        if (index >= 0)
            Volatile.Write(ref _buffer[index % _buffer.Length], entry);

        Interlocked.Increment(ref _count);
    }

    /// <summary>Liefert die Eintraege, neueste zuerst.</summary>
    public IReadOnlyList<LogEntry> Snapshot(int limit)
    {
        var total = Interlocked.Read(ref _count);
        if (total == 0) return [];

        var size = (int)Math.Min(total, _buffer.Length);
        var take = Math.Min(limit, size);
        var result = new LogEntry[take];

        var newest = (int)(Interlocked.Read(ref _writeIndex) - 1);
        for (var i = 0; i < take; i++)
        {
            var index = ((newest - i) % _buffer.Length + _buffer.Length) % _buffer.Length;
            var entry = Volatile.Read(ref _buffer[index]);
            if (entry is not null) result[i] = entry;
        }

        return result;
    }

    public void Dispose() { /* nichts freizugeben */ }
}
````

> **Warum ein Array und keine `Queue`?** Ein `Queue<T>` wächst intern und kopiert beim
> Verwerfen des ältesten Elements. Ein festes Array mit modulo-Index hat **konstanten**
> Speicherbedarf und **keine** Allokation im Betrieb – das ist genau das, was ein
> Ringpuffer leisten soll.

### 4.5 Der Eintrag

````csharp
// src/ComfoBox.Gateway/Logging/LogEntry.cs
namespace ComfoBox.Gateway.Logging;

/// <summary>Ein Log-Eintrag im Ringpuffer.</summary>
public sealed record LogEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }          // "Information", "Warning", …
    public required string Source { get; init; }         // Logger-Kategorie, z. B. "ComfoBox.Bacnet.BacnetClient"
    public required string Message { get; init; }        // gerenderte Meldung
    public string? Exception { get; init; }              // Stacktrace, wenn Exception
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
````

| Feld | Herkunft | Beispiel |
|---|---|---|
| `Timestamp` | `logEvent.Timestamp` | `2026-10-31T14:23:07.123+01:00` |
| `Level` | `logEvent.Level` | `Warning` |
| `Source` | `logEvent.LoggerName` | `ComfoBox.Bacnet.BacnetClient` |
| `Message` | `logEvent.RenderMessage()` | `ReadProperty Zone.SetPointNormal timed out` |
| `Exception` | `logEvent.Exception?.ToString()` | `System.TimeoutException: …` |
| `Properties` | `logEvent.Properties` | `{"Object": "Zone.SetPointNormal", "TimeoutMs": "3000"}` |

### 4.6 Registrierung

````csharp
// src/ComfoBox.Gateway/Program.cs
var loggingOptions = builder.Configuration
    .GetSection("Logging")
    .Get<LoggingOptions>() ?? new LoggingOptions();

var logBuffer = new InMemoryLogSink(
    capacity: loggingOptions.RingBuffer.Capacity,
    minimumLevel: loggingOptions.RingBuffer.MinimumLevel);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    // … Console + File wie in §3.1 …
    .WriteTo.Sink(logBuffer));

builder.Services.AddSingleton(logBuffer);
````

> **Ein Ringpuffer pro Prozess.** Er wird als **Singleton** registriert und sowohl vom
> API-Endpunkt (§6) als auch vom SSE-Stream (§6.3) gelesen. Es gibt bewusst **keine**
> zweite Instanz und keinen Zugriff über `HttpContext` – der Puffer ist ein
> Anwendungsobjekt, kein Request-Scoped-Objekt.

### 4.7 Redaktion von Secrets

Der Puffer ist über die Web-UI einsehbar. Deshalb werden Werte, die wie Secrets
aussehen, **vor** dem Speichern maskiert:

| Muster | Ersetzung |
|---|---|
| Properties `Password`, `Token`, `Secret`, `ApiKey`, `ConnectionString` | `••••` |
| Werte in URLs (`https://user:pass@host`) | Passwort → `••••` |
| MQTT-Broker-Passwort | wird nie geloggt (Regel §2) |

Die Redaktion passiert im Serilog-`Enrich`/Destructure-Teil **vor** der Ringbuffer-Sink,
damit sie auch für die Datei-Sink gilt.

---

## 5. Abgrenzung: Fehler-Ringpuffer vs. Log-Ringpuffer

Zwei ähnliche Dinge – bewusst getrennt:

| | Fehler-Ringpuffer | Log-Ringpuffer |
|---|---|---|
| Ort | `HealthReporter` ([health-und-status.md §4](health-und-status.md#4-fehler-sammlung)) | `InMemoryLogSink` (dieses Dokument) |
| Inhalt | **nur** Fehler mit Code und Quelle | **alle** Log-Einträge (auch `Information`) |
| Kapazität | 50 | 1000 (konfigurierbar) |
| Zweck | Health-Status, Alarmierung | Diagnose, „was passiert gerade?" |
| Verbraucher | `/api/diagnostics/errors`, `ComfoBox/Status/Errors` | `/api/diagnostics/logs`, Log-Viewer |
| Persistenz | nein | nein |

> **Regel:** Jeder Eintrag im Fehler-Ringpuffer erzeugt **zusätzlich** einen
> Log-Eintrag – aber **nicht** umgekehrt. Sonst wäre der Fehlerpuffer redundant.

---

## 6. API

### 6.1 Endpunkte

| Methode | Route | Zweck | Response |
|---|---|---|---|
| `GET` | `/api/diagnostics/logs` | Log-Einträge (gefiltert) | `LogEntryDto[]` |
| `GET` | `/api/diagnostics/logs/stream` | **Live-Tail** per SSE | `text/event-stream` |
| `DELETE` | `/api/diagnostics/logs` | Puffer leeren | `204` |

### 6.2 `GET /api/diagnostics/logs`

| Parameter | Typ | Standard | Bedeutung |
|---|---|---|---|
| `level` | `string` | – | Minimaler Level (`Trace`…`Critical`), case-insensitiv |
| `source` | `string` | – | Teilstring auf die Logger-Kategorie |
| `search` | `string` | – | Volltext in Meldung / Exception / Properties |
| `since` | `datetime` | – | nur Einträge ab diesem Zeitpunkt |
| `limit` | `int` | `200` | max. Anzahl (Obergrenze 1000) |
| `order` | `string` | `desc` | `desc` (neueste zuerst) oder `asc` |

Beispiel:

````http
GET /api/diagnostics/logs?level=Warning&source=Bacnet&limit=50
````

````json
[
  {
    "timestamp": "2026-10-31T14:23:07.123+01:00",
    "level": "Warning",
    "source": "ComfoBox.Bacnet.BacnetClient",
    "message": "ReadProperty Zone.SetPointNormal timed out after 3000 ms",
    "exception": null,
    "properties": { "Object": "Zone.SetPointNormal", "TimeoutMs": "3000" }
  }
]
````

> **Reihenfolge:** neueste zuerst. Der Log-Viewer zeigt die neuesten Einträge oben –
> genau wie ein Tail.

### 6.3 `GET /api/diagnostics/logs/stream` (SSE)

Live-Tail für die Web-UI. Nutzt denselben SSE-Mechanismus wie
[`/api/events`](datenmodell-und-api.md#4-sse-server-sent-events-für-live-werte):

````text
event: log
data: {"timestamp":"2026-10-31T14:23:07.123+01:00","level":"Warning","source":"…","message":"…"}

event: log
data: {"timestamp":"2026-10-31T14:23:09.400+01:00","level":"Error","source":"…","message":"…"}
````

| Eigenschaft | Wert |
|---|---|
| Intervall | nur bei neuen Einträgen (event-getrieben, kein Polling) |
| Puffer beim Verbinden | die letzten 50 Einträge als Initial-Snapshot |
| Abbruch | `ConnectionAborted` → Subskription wird entfernt |
| Backpressure | langsame Clients verlieren Einträge (bounded channel, `DropOldest`) |

> **Warum SSE und nicht WebSocket?** Das Protokoll ist bereits im Einsatz
> ([`/api/events`](datenmodell-und-api.md#4-sse-server-sent-events-für-live-werte)),
> läuft über denselben Port und braucht keine zusätzliche Infrastruktur.

### 6.4 `DELETE /api/diagnostics/logs`

Leert den Puffer. Wird im UI als „Protokoll leeren" angeboten. Antwort: `204 No Content`.

---

## 7. Web-UI: der Log-Viewer

Ort: **Diagnoseseite** ([ui-angular-optimus.md §7](ui-angular-optimus.md#7-diagnoseseite)),
Reiter „Protokoll".

| Element | Verhalten |
|---|---|
| **Live-Ansicht** | Toggle „Live" – per SSE, sonst historische Liste |
| **Level-Filter** | Mehrfachauswahl (Debug, Information, Warning, Error) |
| **Quellen-Filter** | Dropdown mit den vorhandenen Logger-Kategorien |
| **Textsuche** | Volltext über Meldung, Exception und Properties |
| **Zeitbereich** | „letzte 5 min / 15 min / Stunde / alles" |
| **Auto-Scroll** | folgt dem Ende, wenn „Live" aktiv (abschaltbar) |
| **Export** | gefilterte Einträge als `.log`-Datei herunterladen |
| **Leeren** | Button „Protokoll leeren" → `DELETE /api/diagnostics/logs` |
| **Kopieren** | einzelner Eintrag per Klick kopieren (inkl. Exception) |

### 7.1 Farbcodierung

| Level | Farbe |
|---|---|
| `Debug` | grau |
| `Information` | Standard |
| `Warning` | gelb/orange |
| `Error` | rot |
| `Critical` | dunkelrot, fett |

### 7.2 Beispielhafte Darstellung

````
14:23:07.123  W  ComfoBox.Bacnet.BacnetClient   ReadProperty Zone.SetPointNormal timed out after 3000 ms
14:23:07.124      └─ Object=Zone.SetPointNormal  TimeoutMs=3000
14:23:09.400  E  ComfoBox.Mqtt.MqttPublisher    Publish failed: Connection refused
14:23:09.401      └─ Exception: System.Net.Sockets.SocketException (111): Connection refused
````

> **Wichtig:** Die UI zeigt **die gerenderte Meldung**, nicht das rohe JSON. Properties
> werden eingerückt darunter angezeigt – das entspricht dem gewohnten Serilog-Format.

---

## 8. Konfiguration

### 8.1 `appsettings.json`

````json
{
  "Logging": {
    "Level": "Information",
    "Directory": "/var/log/comfobox",
    "RingBuffer": {
      "Capacity": 1000,
      "MinimumLevel": "Information"
    }
  }
}
````

| Schlüssel | Typ | Standard | Bedeutung |
|---|---|---|---|
| `Logging:Level` | `string` | `Information` | globaler Minimal-Level |
| `Logging:Directory` | `string` | `/var/log/comfobox` | Verzeichnis der Datei-Sink |
| `Logging:RingBuffer:Capacity` | `int` | `1000` | Anzahl Einträge im Ringpuffer, `0` = aus |
| `Logging:RingBuffer:MinimumLevel` | `string` | `Information` | Level, ab dem in den Puffer geschrieben wird |

### 8.2 Environment-Variablen

| Variable | Beispiel |
|---|---|
| `COMFOBOX_Logging__Level` | `Information` |
| `COMFOBOX_Logging__Directory` | `/var/log/comfobox` |
| `COMFOBOX_Logging__RingBuffer__Capacity` | `1000` |
| `COMFOBOX_Logging__RingBuffer__MinimumLevel` | `Information` |

### 8.3 Validierung

````csharp
builder.Services.AddOptions<LoggingOptions>()
    .Bind(builder.Configuration.GetSection(LoggingOptions.SectionName))
    .Validate(o => o.RingBuffer.Capacity >= 0, "Logging:RingBuffer:Capacity darf nicht negativ sein")
    .Validate(o => o.RingBuffer.Capacity <= 100_000, "Logging:RingBuffer:Capacity ist zu gross")
    .ValidateOnStart();
````

> **Obergrenze 100 000:** bei ~400 Byte pro Eintrag wären das 40 MB – auf einem
> Raspberry Pi zu viel. Der Default 1000 (~400 KB) ist der Normalfall.

### 8.4 Laufzeit ändern

Die Kapazität ist **nur** über einen Neustart änderbar (der Ringpuffer wird beim Start
allokiert). Der Log-Viewer bietet deshalb **keinen** Regler dafür an – nur den Filter.

> **Warum nicht dynamisch?** Eine Änderung der Kapazität würde eine Neuzuweisierung
> des Puffers erfordern – unter Last ein Speicher-Pit-Stop. Der Nutzer bekommt den
> Default (1000), und wer mehr braucht, konfiguriert es einmalig.

---

## 9. Fehlerverhalten

| Situation | Verhalten |
|---|---|
| Ringpuffer voll | ältester Eintrag wird überschrieben, **kein** Fehler, **kein** Log-Flood |
| `Capacity = 0` | Sink ist inaktiv, `Emit` kehrt sofort zurück |
| Log-Pfad wirft | Serilog fängt Fehler im Sink **intern** ab – der Gateway läuft weiter |
| SSE-Client ist langsam | bounded channel mit `DropOldest` – keine Blockade des Log-Pfads |
| Zu viele gleichzeitige SSE-Clients | höchstens 5 gleichzeitige Log-Streams |

> **Grundregel:** Ein Fehler im Logging darf den Gateway **nie** stoppen. Serilog
> kapselt Sink-Fehler, zusätzlich wird der Sink selbst defensiv implementiert
> (kein `throw` in `Emit`).

---

## 10. Definition of Done

- [ ] `Serilog.Extensions.Hosting` in `Directory.Packages.props`, Version gepinnt
- [ ] `UseSerilog` in `Program.cs` mit Console-, File- und Ringbuffer-Sink
- [ ] `ILogger<T>` wird **überall** verwendet – kein `Trace`, kein `Console.WriteLine`
- [ ] Der BACnet-Stack ist vollständig auf `ILogger` umgestellt (26 Fundstellen, Befund A16)
- [ ] `InMemoryLogSink` ist thread-sicher und allokiert im Betrieb nicht
- [ ] `Logging:RingBuffer:Capacity` ist konfigurierbar, Standard 1000, `0` schaltet ab
- [ ] Secrets werden vor dem Speichern maskiert
- [ ] `GET /api/diagnostics/logs` liefert gefilterte Einträge (Level, Quelle, Text, Zeit, Limit)
- [ ] `GET /api/diagnostics/logs/stream` liefert Live-Einträge per SSE
- [ ] `DELETE /api/diagnostics/logs` leert den Puffer
- [ ] Der Log-Viewer in der Diagnoseseite zeigt Live- und historische Einträge mit Filtern
- [ ] Export der gefilterten Einträge als Datei funktioniert
- [ ] Unit-Test: Ringpuffer überschreibt den ältesten Eintrag, ohne Fehler
- [ ] Unit-Test: `Capacity = 0` deaktiviert den Puffer
- [ ] Unit-Test: Filter (Level, Quelle, Text) liefert die erwartete Teilmenge
- [ ] Integrationstest: `GET /api/diagnostics/logs` liefert `200` mit `LogEntryDto[]`

---

## 11. Verwandte Dokumente

- [ADR-011](../05-entscheidungen/adr-011-logging-serilog-und-ringbuffer.md) – die Entscheidung
- [dotnet-migration.md §5](dotnet-migration.md#5-logging) – Serilog-Basis-Setup
- [health-und-status.md §4](health-und-status.md#4-fehler-sammlung) – Fehler-Ringpuffer (50)
- [ui-angular-optimus.md §7](ui-angular-optimus.md#7-diagnoseseite) – Diagnoseseite mit Log-Viewer
- [datenmodell-und-api.md §3](datenmodell-und-api.md#3-rest-api) – REST-Endpunkte
- [konfiguration-und-secrets.md](../03-deployment/konfiguration-und-secrets.md) – Konfigurationsschema
- [befunde.md](../01-ist-zustand/befunde.md) – Befund A16 (`Trace` statt Logging)
