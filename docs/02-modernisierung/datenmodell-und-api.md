# Datenmodell & API

## 1. Item-Katalog

Der Kern der Domäne. Ein Item beschreibt **genau eine** BACnet-Objekt-ID mit Typ, Grenzen,
Einheit und Schreibbarkeit. Aus einem Item werden Topic, REST-Route und HA-Discovery **abgeleitet**.

### 1.1 Definition

````csharp
// src/ComfoBox.Domain/Items/ItemDefinition.cs
namespace ComfoBox.Domain.Items;

public enum ItemValueKind { Analog, Enum, Date, Command }
public enum RefreshPriority { Once, Primary, Secondary }

public sealed record ItemDefinition
{
    public required string Id { get; init; }              // z. B. "zone.setpoint_normal"
    public required string Group { get; init; }            // z. B. "Zone"
    public required string Name { get; init; }             // z. B. "SetPointNormal"
    public required int BacnetObjectId { get; init; }      // z. B. 830
    public required ItemValueKind Kind { get; init; }
    public required ItemValueType ValueType { get; init; } // float / enum-Typ / DateTime

    public float? Min { get; init; }
    public float? Max { get; init; }
    public string? Unit { get; init; }
    public bool IsWritable { get; init; }
    public bool RequiresExpertMode { get; init; }
    public RefreshPriority Priority { get; init; } = RefreshPriority.Secondary;
    public string? Description { get; init; }
    public IReadOnlyDictionary<int, string> EnumValues { get; init; } = …;
}
````

### 1.2 Deklarativer Katalog

Der Ist-Zustand nutzt Reflection über die Klassen `Zone`, `States`, `Config`, `Tests`, `Warmwater`,
`Time` mit expression-bodied Properties (`=> new AnalogValue(830)`). Das war elegant, aber:

- Jeder Property-Zugriff erzeugt ein neues Objekt.
- Der Typ ergibt sich implizit aus der Property-Rückgabe → schwer testbar.
- `DateValue` und `CommandValue` sind tote Zweige (Befunde A20, A14b).

**Soll:** Explizite, deklarative Definition – ohne Reflection, ohne Fasterflect.

````csharp
// src/ComfoBox.Domain/Items/Catalog.cs
public static partial class Catalog
{
    public static IReadOnlyList<ItemDefinition> All { get; } =
    [
        new()
        {
            Id = "zone.setpoint_normal",
            Group = "Zone",
            Name = "SetPointNormal",
            BacnetObjectId = 830,
            Kind = ItemValueKind.Analog,
            ValueType = ItemValueKind.Analog,
            Min = 5f, Max = 30f, Unit = "°C",
            IsWritable = true,
            Priority = RefreshPriority.Primary,
            Description = "Raumsollwert Normalbetrieb",
        },
        // … 126 weitere
    ];
}
````

> **Vorteil:** Der Katalog ist eine reine Datenstruktur → direkt unit-testbar, JSON-serialisierbar
> (für die UI), und die Ableitung der Topics ist trivial zu prüfen.
>
> **Nachteil:** Mehr Tippen als die Properties. Einmalige Migration mit Skript ist möglich –
> die 127 Definitionen lassen sich aus `docs/topics.json` + `Items/**` generieren.

### 1.3 Topic-Ableitung

Der Topic-Pfad bleibt **identisch** zum Ist-Zustand:

```
{BaseTopic}/{Group}/{Name}              → State
{BaseTopic}/{Group}/{Name}/Set          → Command (nur wenn IsWritable)
{BaseTopic}/{Group}/{Name}/AsNumber     → Enum als Zahl (nur wenn Kind == Enum)
{BaseTopic}/{Group}/{Name}/AsNumber/Set → Enum als Zahl schreiben
```

**Kompatibilitätsregel:** `Group` und `Name` müssen **exakt** die bisherigen Topic-Segmente
reproduzieren. Der Contract-Test erzwingt das (siehe
[../04-migration/mqtt-kompatibilitaet.md](../04-migration/mqtt-kompatibilitaet.md)).

### 1.4 Werttypen

````csharp
// src/ComfoBox.Domain/Values/ItemValue.cs
public abstract record ItemValue
{
    public abstract object? Raw { get; }
}

public sealed record AnalogValue(float? Value, float? Min, float? Max, string? Unit) : ItemValue
{
    public override object? Raw => Value;
}

public sealed record EnumValue(int? Value, IReadOnlyDictionary<int, string> Values) : ItemValue
{
    public override object? Raw => Value;
    public string? Name => Value is not null && Values.TryGetValue(Value.Value, out var n) ? n : null;
}
````

### 1.5 Schreibschutz

| Regel | Quelle |
|---|---|
| `IsWritable == false` → kein `/Set`-Topic, kein Subscribe | Ist-Zustand |
| `RequiresExpertMode && !ExpertMode` → Item gilt als read-only | Ist-Zustand (`IsReadOnly = !ExpertMode`) |
| `Min`/`Max` verletzt → Schreibvorgang **ablehnen** | Ist-Zustand (`CheckSetValueConditions`) |
| `null` → ablehnen | Ist-Zustand |
| 24-h-Zähler überschritten → ablehnen + Health melden | Ist-Zustand, aber **persistent** (neu) |

> **Kompatibilitätsregel:** „ExpertMode=false ⇒ kein `/Set`-Topic" bleibt identisch.
> Der Ist-Zustand hat das Attribut `[ExpertMode]` nur in der Doku ausgewertet, die Schreibsperre
> aber über `IsReadOnly` beim Item gesetzt. Diese Semantik wird 1:1 übernommen.

## 2. Value-Cache

Der Cache ist die Wahrheit zwischen BACnet, MQTT, REST und UI.

````csharp
// src/ComfoBox.Gateway/ValueCache.cs
internal sealed class ValueCache(ItemCatalog catalog)
{
    private readonly ConcurrentDictionary<string, CachedValue> _values = new();

    public sealed record CachedValue(object? Value, DateTimeOffset Timestamp, bool IsStale);

    public bool TryGet(string id, out CachedValue value) => _values.TryGetValue(id, out value!);

    public void Set(string id, object? value)
        => _values[id] = new CachedValue(value, DateTimeOffset.UtcNow, IsStale: false);

    public void MarkStale(string id)
    {
        if (_values.TryGetValue(id, out var v))
            _values[id] = v with { IsStale = true };
    }

    /// <summary>Fehlt ein Wert, wird er blockierend einmalig gelesen.</summary>
    public async Task<CachedValue> GetOrReadAsync(string id, CancellationToken ct);
}
````

**`IsStale`:** Wenn ein BACnet-Read fehlschlägt, wird der letzte Wert **nicht** gelöscht, sondern
als `stale` markiert. Die UI zeigt „Wert veraltet". So bleibt der letzte bekannte Stand sichtbar.

## 3. REST-API

Basis-URL: `/api`. Authentifizierung: optional (siehe offene Frage Q2).

### 3.1 Endpunkte

| Methode | Route | Zweck | Response |
|---|---|---|---|
| `GET` | `/api/health` | Health-Zustand | `HealthReport` |
| `GET` | `/api/status` | Vollständiger Laufzeitstatus | `StatusReport` |
| `GET` | `/api/items` | Item-Katalog mit Metadaten | `ItemDto[]` |
| `GET` | `/api/items/{id}` | Ein Item inkl. aktuellem Wert | `ItemDto` |
| `PUT` | `/api/items/{id}` | Wert schreiben | `204` / `4xx` |
| `GET` | `/api/items/group/{group}` | Items einer Gruppe | `ItemDto[]` |
| `GET` | `/api/events` | SSE-Stream (Wertänderungen, Health) | `text/event-stream` |
| `GET` | `/api/config` | Aktuelle Konfiguration (ohne Secrets) | `ConfigDto` |
| `PUT` | `/api/config` | Konfiguration ändern (reload) | `204` |
| `POST` | `/api/service/restart` | **Service neu starten** | `202` |
| `POST` | `/api/service/reconnect/bacnet` | BACnet neu verbinden | `202` |
| `POST` | `/api/service/reconnect/mqtt` | MQTT neu verbinden | `202` |
| `GET` | `/api/diagnostics/errors` | Letzte Fehler (Ringpuffer) | `ErrorEntry[]` |
| `GET` | `/api/diagnostics/writes` | Schreibzähler-Historie | `WriteStats` |

### 3.2 Beispiele

````http
GET /api/items/zone.setpoint_normal
````
````json
{
  "id": "zone.setpoint_normal",
  "group": "Zone",
  "name": "SetPointNormal",
  "displayName": "Raumsollwert Normalbetrieb",
  "kind": "analog",
  "value": 21.5,
  "valueText": "21,5 °C",
  "unit": "°C",
  "min": 5,
  "max": 30,
  "writable": true,
  "requiresExpertMode": false,
  "stateTopic": "ComfoBox/Zone/SetPointNormal",
  "setTopic": "ComfoBox/Zone/SetPointNormal/Set",
  "lastUpdated": "2026-10-31T08:12:44Z",
  "stale": false
}
````

> **Hinweis:** `value` ist immer **InvariantCulture** (JSON-Zahlen). `valueText` ist die
> lokalisierte Darstellung **nur für die Anzeige**.

Schreiben:

````http
PUT /api/items/zone.setpoint_normal
Content-Type: application/json

{ "value": 21.5 }
````

Fehlerantworten:

````json
// 409 – Schreibschutz
{
  "error": "write_limit_exceeded",
  "message": "Maximale Anzahl Schreibvorgänge pro 24 h überschritten (200).",
  "writesLast24h": 200,
  "maxWritesPer24h": 200
}
````

````json
// 422 – Validierung
{
  "error": "value_out_of_range",
  "message": "Wert 40 liegt außerhalb des zulässigen Bereichs [5, 30].",
  "min": 5,
  "max": 30
}
````

````json
// 403 – Expert Mode
{ "error": "expert_mode_required", "message": "Dieser Wert erfordert den Expert-Modus." }
````

````json
// 409 – Item read-only
{ "error": "read_only", "message": "Item ist nicht schreibbar." }
````

### 3.3 Implementierung (Minimal API)

````csharp
// src/ComfoBox.Gateway/Api/ItemsEndpoints.cs
internal static class ItemsEndpoints
{
    public static void MapItems(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/items").WithTags("Items");

        group.MapGet("/", (ItemCatalog catalog) =>
            Results.Ok(catalog.All.Select(ItemDto.From)));

        group.MapGet("/{id}", async (string id, ItemCatalog catalog, IValueCache cache,
                                     CancellationToken ct) =>
        {
            var item = catalog.Find(id);
            if (item is null) return Results.NotFound();
            var value = await cache.GetOrReadAsync(id, ct);
            return Results.Ok(ItemDto.From(item, value));
        });

        group.MapPut("/{id}", async (string id, WriteValueRequest request, ItemCatalog catalog,
                                     IValueWriter writer, CancellationToken ct) =>
        {
            var item = catalog.Find(id);
            if (item is null) return Results.NotFound();

            var result = await writer.WriteAsync(item, request.Value, ct);
            return result switch
            {
                WriteOutcome.Ok          => Results.NoContent(),
                WriteOutcome.ReadOnly    => Results.Json(new { error = "read_only" }, statusCode: 409),
                WriteOutcome.ExpertMode  => Results.Json(new { error = "expert_mode_required" }, statusCode: 403),
                WriteOutcome.OutOfRange  => Results.Json(new { error = "value_out_of_range" }, statusCode: 422),
                WriteOutcome.InvalidType => Results.Json(new { error = "invalid_type" }, statusCode: 422),
                WriteOutcome.LimitExceeded => Results.Json(new { error = "write_limit_exceeded" }, statusCode: 409),
                WriteOutcome.BusError    => Results.Json(new { error = "bus_error" }, statusCode: 502),
                _                        => Results.StatusCode(502),
            };
        });
    }
}
````

## 4. SSE (Server-Sent Events) für Live-Werte

Die UI soll Werte live sehen, ohne Polling-Bremse. SSE ist gegenüber WebSockets einfacher
(one-way) und reicht hier.

````csharp
// src/ComfoBox.Gateway/Api/EventsEndpoint.cs
internal static class EventsEndpoint
{
    public static void MapEvents(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", async (HttpContext ctx, IValueCache cache,
                                          IHealthReporter health, CancellationToken ct) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";   // nginx: kein Buffering

            await using var writer = new StreamWriter(ctx.Response.Body);
            await writer.WriteAsync($"event: snapshot\ndata: {JsonSerializer.Serialize(cache.Snapshot())}\n\n");
            await writer.FlushAsync(ct);

            await foreach (var evt in cache.SubscribeAsync(ct))
            {
                await writer.WriteAsync($"event: value\ndata: {JsonSerializer.Serialize(evt)}\n\n");
                await writer.FlushAsync(ct);
            }
        });
    }
}
````

> **nginx-Wichtig:** `proxy_buffering off;` für `/api/events`, sonst wartet der Browser auf
> 4 KB und sieht keine Live-Werte.

## 5. Persistenter Schreibschutz (24 h)

**Befund A3 (P0):** Der Ist-Zustand zählt Schreibvorgänge nur im Prozessspeicher → ein Neustart
setzt den EEPROM-Schutz zurück.

### Soll

````csharp
// src/ComfoBox.Gateway/Protection/WriteCounter.cs
internal sealed class WriteCounter
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateOnly _windowStart;
    private int _count;

    public async Task<(int Count, DateOnly WindowStart)> LoadAsync(CancellationToken ct);
    public async Task<bool> TryConsumeAsync(CancellationToken ct);   // false = Limit erreicht
}
````

**Persistenzformat** (`/var/lib/comfobox/writes.json`):

````json
{
  "windowStart": "2026-10-31T00:00:00Z",
  "count": 17,
  "maxPerWindow": 200
}
````

**Regeln:**

| Regel | Umsetzung |
|---|---|
| Fenster ist **kalendarisch** (UTC-Tageswechsel), nicht relativ zum Prozessstart | `windowStart` wird beim Tageswechsel zurückgesetzt |
| Zähler wird **nach jedem** Schreibvorgang persistiert | `File.WriteAllTextAsync` (atomar via temp + rename) |
| Ein **fehlgeschlagener** Write zählt **nicht** | (behebt Befund A22) |
| Persistenzverzeichnis ist ein **Volume** | Docker-Volume, überlebt Container-Neustart |

> **Hinweis:** Der Ist-Zustand hat den Zähler nach 24 h *prozesslaufzeit* zurückgesetzt, nicht
> kalendarisch. Die Umstellung auf kalendarisch ist **strenger** und schützt den EEPROM besser.
> Ein bestehendes Setup „verbraucht" also ggf. einen Tag weniger Schreibvorgänge.

## 6. Restart

### 6.1 Per REST

````csharp
app.MapPost("/api/service/restart", (IHostApplicationLifetime lifetime, ILogger<Program> logger) =>
{
    logger.LogWarning("Neustart angefordert (REST)");
    // in-process: kontrolliertes Herunterfahren + Neustart
    lifetime.StopApplication();
    return Results.Accepted(value: new { status = "restarting" });
});
````

> **Wichtig:** Ein `docker restart` innerhalb des Containers ist nicht möglich (kein Docker-Socket
> aus Sicherheitsgründen). Der Neustart erfolgt **in-process**: Der Host beendet sich kontrolliert
> und wird vom Supervisor (Docker `restart: unless-stopped` bzw. systemd) neu gestartet.
> Die Startsequenz ist so zu bauen, dass sie idempotent ist.

### 6.2 Per MQTT

Siehe [home-assistant.md#5-restart-aus-home-assistant](home-assistant.md#5-restart-aus-home-assistant).

### 6.3 Restart-Implementierung

Der Prozess muss den eigenen Neustart sauber anstoßen können:

````csharp
// src/ComfoBox.Gateway/ServiceControl.cs
internal sealed class ServiceControl(
    IHostApplicationLifetime lifetime,
    IOptions<GatewayOptions> options,
    ILogger<ServiceControl> logger)
{
    public Task RestartAsync(CancellationToken ct)
    {
        if (!options.Value.RestartEnabled)
        {
            logger.LogWarning("Neustart ist deaktiviert (Gateway:RestartEnabled=false)");
            return Task.CompletedTask;
        }

        logger.LogInformation("Neustart wird ausgeführt …");
        // Restart-Signal für den Watchdog/Supervisor schreiben (optional)
        File.WriteAllText("/tmp/comfobox/restart.requested", DateTimeOffset.UtcNow.ToString("O"));
        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}
````

| Mechanismus | Funktioniert? |
|---|---|
| `lifetime.StopApplication()` + Docker `restart: unless-stopped` | ✅ **Standard** |
| `exit(0)` | ✅ ebenfalls – Docker startet neu |
| `Process.Start("docker", …)` | ❌ kein Docker-Socket im Container |
| `systemctl restart` | ❌ läuft nicht im Container |

## 7. Konfigurations-Endpunkt

Der Nutzer soll aus der UI heraus Expert-Mode, Poll-Intervall, Schreiblimit etc. ändern können.

| Aktion | Wirkung |
|---|---|
| `PUT /api/config` | ändert die **laufende** Konfiguration und persistiert sie nach `appsettings.override.json` |
| Teile der Konfiguration | **nur** zur Laufzeit änderbar: `ExpertMode`, `PollIntervalSeconds`, `MaxNumberOfWritesPer24h`, `UseMqttRetain` |
| **Nicht** änderbar zur Laufzeit | `Bacnet:Port`, `Bacnet:Baudrate`, `Mqtt:Brokers` → erfordern einen Neustart (Button in der UI) |

> **Sicherheitsregel:** Konfigurationsänderungen, die Schreibzugriffe ermöglichen
> (`ExpertMode = true`), erfordern eine explizite Bestätigung in der UI und werden
> ** protokolliert**.

## 8. Fehlerverhalten der API

| Situation | HTTP | MQTT |
|---|---|---|
| Item unbekannt | 404 | – |
| Item read-only | 409 | Fehler auf `Status/Errors` |
| Expert-Modus fehlt | 403 | Fehler auf `Status/Errors` |
| Wert außerhalb Min/Max | 422 | Fehler auf `Status/Errors` |
| Typ passt nicht | 422 | Fehler auf `Status/Errors` |
| 24-h-Limit erreicht | 409 | Fehler auf `Status/Errors` |
| BACnet-Bus-Fehler | 502 | Fehler auf `Status/Errors`, Health `degraded` |
| Broker nicht erreichbar | 202 (angenommen) | Fehler auf `Status/Errors`, Health `degraded` |

> **Wichtig:** Ein Schreibversuch **erzeugt** in jedem Fehlerfall einen Eintrag in
> `Status/Errors` – die UI und HA sehen denselben Fehlerkanal wie beim Ist-Zustand
> (der gar keinen hatte).
