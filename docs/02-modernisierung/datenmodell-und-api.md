# Datenmodell & API

## 1. Item-Katalog

Der Kern der Domäne. Ein Item beschreibt **genau eine** BACnet-Objekt-ID mit Typ, Grenzen,
Einheit und Schreibbarkeit. Aus einem Item werden Topic, REST-Route und HA-Discovery **abgeleitet**.

### 1.1 Definition

````csharp
// src/ComfoBox.Domain/Items/ItemDefinition.cs
namespace ComfoBox.Domain.Items;

/// <summary>Der Werte-Kind eines Items.</summary>
public enum ItemValueKind
{
    /// <summary>Eine Zahl mit Einheit und optionalen Grenzen.</summary>
    Analog,

    /// <summary>Eine Auswahl aus benannten Werten.</summary>
    Enum,

    /// <summary>
    /// Ein BACnet-Analogwert, dessen Wert nicht interpretiert wird.
    /// Der Legacy nutzte dafür den (toten) Werttyp <c>DateValue</c> – der
    /// Wert bleibt leer (Befund A20), das Item bleibt aber im Katalog.
    /// </summary>
    Date,
}

public enum RefreshPriority { None, Once, Primary, Secondary }

public sealed record ItemDefinition
{
    public required string Id { get; init; }              // z. B. "zone.set_point_normal"
    public required string Group { get; init; }            // z. B. "Zone"
    public required string Name { get; init; }             // z. B. "SetPointNormal"
    public required int BacnetObjectId { get; init; }      // z. B. 62
    public required ItemValueKind Kind { get; init; }

    public float? Min { get; init; }
    public float? Max { get; init; }
    public string? Unit { get; init; }
    public bool IsReadOnly { get; init; }
    public bool RequiresExpertMode { get; init; }
    public RefreshPriority Priority { get; init; } = RefreshPriority.Secondary;
    public string? Description { get; init; }
    public IReadOnlyDictionary<int, string> EnumValues { get; init; } = …;

    /// <summary>Der Topic-Pfad ohne BaseTopic, z. B. "Zone/SetPointNormal".</summary>
    public string TopicPath => $"{Group}/{Name}";

    /// <summary>Ist das Item grundsätzlich schreibbar?</summary>
    public bool IsWritable => !IsReadOnly;

    /// <summary>Ist das Item mit dem angegebenen Expert-Modus-Zustand schreibbar?</summary>
    public bool IsWritableWithExpertMode(bool expertModeEnabled)
        => !IsReadOnly && (!RequiresExpertMode || expertModeEnabled);
}
````

> **Die Id-Regel:** Der Topic-Pfad wird in camelCase → snake_case überführt und
> mit Punkten verbunden: `Zone/SetPointNormal` → `zone.set_point_normal`.
> Die Id ist unabhängig vom konfigurierbaren `BaseTopic` und dient als
> Schlüssel in den REST-Routen.
>
> **Schreibbarkeit:** `IsReadOnly` (fest) und `RequiresExpertMode` sind getrennt
> geführt. Der Legacy hatte beides in einem Feld (`IsReadOnly = !ExpertMode`)
> vermischt – so bleibt der effektive Zustand berechenbar.

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
            Id = "zone.set_point_normal",
            Group = "Zone",
            Name = "SetPointNormal",
            BacnetObjectId = 62,
            Kind = ItemValueKind.Analog,
            Unit = "°C",
            Min = 5f, Max = 30f,
            Priority = RefreshPriority.Primary,
            Description = "Raumsollwert normal 5.0..30.0",
        },
        // … 126 weitere
    ];
}
````

> **Der Katalog wird generiert**, nicht von Hand geschrieben:
> `node tools/generate-topics/generate-topics.mjs --catalog src/ComfoBox.Domain/Items`
> liest `src_old/ComfoBoxLib/Items/**` und erzeugt `Catalog.cs` **und**
> `ItemEnums.cs`. `--catalog-check` prüft, ob die committeten Dateien noch zum
> Legacy-Katalog passen.

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
public sealed record CachedValue(string Id, ItemValue? Value, DateTimeOffset Timestamp, bool IsStale);

internal sealed class ValueCache
{
    private readonly ConcurrentDictionary<string, CachedValue> _values = new();

    public bool TryGet(string id, out CachedValue? value);

    /// <summary>Schreibt einen Wert nach einem erfolgreichen BACnet-Read.</summary>
    public void Update(string id, ItemValue? value);

    public void MarkStale(string id);

    /// <summary>Fehlt ein Wert, wird er blockierend einmalig gelesen.</summary>
    public async Task<CachedValue> GetOrReadAsync(string id, CancellationToken ct);
}
````

> **`IsStale`:** Ein Wert gilt als veraltet, wenn der letzte Read fehlgeschlagen
> ist (dann wird er markiert) **oder** wenn er älter als drei Poll-Zyklen ist.
> Sekundäre Items werden nur alle `SecondaryPollFactor` Zyklen gelesen – die
> Schwelle ist deshalb `3 × Intervall × Faktor`.
>
> Der Wert bleibt im Cache, wenn ein Read fehlschlägt – die UI zeigt „Wert
> veraltet". So bleibt der letzte bekannte Stand sichtbar.

## 3. REST-API

Basis-URL: `/api`. Authentifizierung: **optional** (siehe
[ADR-012](../05-entscheidungen/adr-012-api-zugriffskontrolle.md)).

| Modus | Bedeutung |
|---|---|
| **LAN-Modus** (Standard, `Api:RequireAuthentication = false`) | Im reinen Heimnetz regelt die Firewall den Zugriff. Administrative Endpunkte sind offen. |
| **Authentifiziert** (`Api:RequireAuthentication = true`) | Administrative Endpunkte brauchen `Authorization: Bearer <Token>` oder `Authorization: Basic <base64(user:pass)>`. |

> **Unabhängig vom Modus:** Die `/api/setup/*`-Endpunkte sind **nach** der
> Ersteinrichtung immer geschützt - sie können die Konfiguration und damit
> auch das Administrator-Passwort überschreiben.
>
> **Und:** Wer **ein** `Authorization`-Header vorlegt und damit **nicht**
> authentifiziert wird, bekommt **immer** `401` – auch auf den offenen
> Endpunkten. Ohne Header bleibt alles wie bisher (LAN-Modus). Grund: die
> HA-Integration prüft ihren Token über `GET /api/status` – ein falscher Token
> darf dort nicht als gültig durchgehen.
> → [ADR-012](../05-entscheidungen/adr-012-api-zugriffskontrolle.md)

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
| `GET` | `/api/config/serial-ports` | Erkannte serielle Ports (Hotplug) | `SerialPort[]` |
| `POST` | `/api/config/bacnet/test` | Seriellen Port prüfen | `TestResult` |
| `PUT` | `/api/config/bacnet` | **Schnittstelle umstellen – wirkt sofort** | `204` |
| `POST` | `/api/service/restart` | **Service neu starten** | `202` |
| `POST` | `/api/service/reconnect/bacnet` | BACnet neu verbinden | `202` |
| `POST` | `/api/service/reconnect/mqtt` | MQTT neu verbinden | `202` |
| `GET` | `/api/diagnostics/errors` | Letzte Fehler (Ringpuffer) | `ErrorEntry[]` |
| `GET` | `/api/diagnostics/logs` | **Log-Einträge** (Ringpuffer, gefiltert) | `LogEntryDto[]` |
| `GET` | `/api/diagnostics/logs/stream` | **Live-Log-Tail** per SSE | `text/event-stream` |
| `DELETE` | `/api/diagnostics/logs` | Log-Puffer leeren | `204` |
| `GET` | `/api/diagnostics/writes` | Schreibzähler-Historie | `WriteStats` |
| `GET` | `/api/diagnostics/bacnet` | **Bus-Analyse** (Token-Umlauf, nur lesend) | `BacnetAnalysisDto` |
| `POST` | `/api/diagnostics/bacnet/observe` | Beobachtung starten (3 … 300 s) | `BacnetAnalysisDto` |
| `DELETE` | `/api/diagnostics/bacnet/observe` | Beobachtung beenden | `BacnetAnalysisDto` |
| `GET` | `/api/setup/state` | Ist das Setup abgeschlossen? | `SetupState` |
| `GET` | `/api/setup/ports` | Erkannte serielle Ports | `SerialPort[]` |
| `GET` | `/api/setup/ports/suggest` | Freien Web-UI-Port vorschlagen | `int` |
| `POST` | `/api/setup/bacnet/test` | BACnet-Verbindung testen | `TestResult` |
| `POST` | `/api/setup/mqtt/test` | MQTT-Verbindung testen | `TestResult` |
| `PUT` | `/api/setup/config` | Konfiguration schreiben (Assistent) | `204` |
| `POST` | `/api/setup/apply` | Konfiguration übernehmen → Neustart | `202` |

### 3.2 Beispiele

````http
GET /api/items/zone.set_point_normal
````
````json
{
  "id": "zone.set_point_normal",
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
PUT /api/items/zone.set_point_normal
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

> **Umsetzung (Phase 4):** `GET /api/items/{id}` liefert den **gecachten**
> Wert. Ein blockierender BACnet-Read in einem HTTP-Request würde den Bus
> belegen (der Poll-Zyklus wartet dann) - der Wert kommt stattdessen über den
> SSE-Stream `/api/events`, sobald der nächste Poll-Zyklus ihn gelesen hat.
> Ein Item ohne bisherigen Wert liefert `value: null`.

### 3.4 Setup-Endpunkte (Getting Started)

Diese Endpunkte speisen den [Setup-Assistenten](ui-angular-optimus.md#3-getting-started-setup-assistent).
Details zum Installationsfluss: [Synology-Paketcenter](../03-deployment/synology-paketcenter.md).

| Endpunkt | Verhalten |
|---|---|
| `GET /api/setup/state` | `{ "setupCompleted": false, "steps": ["serial","bacnet","mqtt","security"], "version": 1 }` |
| `GET /api/setup/ports` | Liest `ports.json` aus dem Datenverzeichnis (vom Supervisor erzeugt) |
| `GET /api/setup/ports/suggest` | Erster freier Port ab 8081 |
| `POST /api/setup/bacnet/test` | Öffnet den Port, sendet `WhoIs`, wartet auf `IAm` |
| `POST /api/setup/mqtt/test` | CONNECT + Publish/Receive eines Test-Topics |
| `PUT /api/setup/config` | Schreibt `config.json`; ** hasht Passwörter serverseitig** |
| `POST /api/setup/apply` | Setzt `setupCompleted = true` und löst den Neustart aus |

#### TestResult

````json
{
  "ok": false,
  "message": "Die ComfoBox antwortet nicht.",
  "hint": "no_iam",
  "durationMs": 30012
}
````

| `hint` | Bedeutung |
|---|---|
| `serial_port_not_found` | Der Port existiert nicht (mehr) |
| `permission_denied` | Keine Berechtigung (Gruppe `dialout`) |
| `busy` | Port wird von einem anderen Prozess gehalten |
| `no_iam` | Kein `IAm` innerhalb des Timeouts |
| `mqtt_unreachable` | Broker nicht erreichbar |
| `mqtt_auth_failed` | Anmeldedaten falsch |
| `mqtt_tls_failed` | TLS-Verhandlung fehlgeschlagen |

#### Schreiben der Konfiguration

````csharp
group.MapPut("/config", async (SetupConfigRequest request, ISetupService setup, CancellationToken ct) =>
{
    // Passwort kommt im Klartext an und wird hier serverseitig gehasht.
    var config = request.ToConfiguration();                       // ohne Passwort-Hash
    config.Users = request.Users
        .Select(u => new WebUser(u.Username, PasswordHasher.Hash(u.Password)))
        .ToList();

    await setup.WriteAsync(config, ct);     // → config.json (0600)
    return Results.NoContent();
});

group.MapPost("/apply", async (ISetupService setup, CancellationToken ct) =>
{
    await setup.ApplyAsync(ct);             // setupCompleted = true + Neustart anstoßen
    return Results.Accepted();
});
````

> **Wichtig:** Der Klartext-Passwort ist nur auf diesem einen Weg unterwegs. In `config.json`
> steht ausschließlich der Hash (`pbkdf2-sha256$600000$…`).
>
> **Sicherheitsregel:** Alle `/api/setup/*`-Endpunkte sind nach der Ersteinrichtung
> authentifizierungspflichtig; im Setup-Modus (noch kein Benutzer) sind sie **offen** –
> das ist vertretbar, weil der Gateway dann noch nichts kann.

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
            ctx.Response.Headers["X-Accel-Buffering"] = "no";   // nur relevant, falls doch ein Reverse-Proxy davorsteht

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

> **Kein Proxy dazwischen:** Der .NET-Host liefert UI **und** SSE direkt aus
> (→ [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md)). Es gibt daher kein
> `proxy_buffering`-Problem. Steht doch einmal ein Reverse-Proxy davor, muss dort
> `proxy_buffering off;` für `/api/events` gesetzt werden – sonst wartet der Browser auf
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

Siehe [home-assistant.md §6](home-assistant.md#6-neustart-aus-home-assistant-anforderung-6).

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

### 7.1 Konfigurationsquellen und Präzedenz

```
config.json im Datenverzeichnis   ← vom Assistenten geschrieben, VORRANG
        ↓
Umgebungsvariablen COMFOBOX__*    ← Override (CI, Tests, Debug-Läufe)
        ↓
appsettings.json (im Image)        ← Defaults
```

> **Warum die Datei Vorrang hat:** Passwörter in Env-Variablen landen sichtbar in
> `docker inspect`. In `config.json` (Modus 0600) stehen sie nur im Dateisystem.
> → [Konfiguration & Secrets](../03-deployment/konfiguration-und-secrets.md)

| Aktion | Wirkung |
|---|---|
| `PUT /api/config` | persistiert die geänderten Werte nach `config.json` (Modus 0600) |
| Teile der Konfiguration | **nur** zur Laufzeit änderbar: `ExpertMode`, `PollIntervalSeconds`, `MaxNumberOfWritesPer24h`, `UseMqttRetain` |
| `PUT /api/config/bacnet` | **wirkt sofort**: `Bacnet:Port` und `Bacnet:Baudrate` werden übernommen, die Konfiguration wird geschrieben und der BACnet-Stack baut die Verbindung **ohne Neustart** neu auf |
| **Nicht** änderbar zur Laufzeit | `Mqtt:Brokers` → erfordert einen Neustart (Button in der UI) |

> **Umsetzung (Phase 4):** `PUT /api/config` schreibt die Werte in die
> `config.json`; sie werden beim nächsten Start wirksam (die Options sind
> unveränderliche Schnappschüsse). Der Neustart wird in der Oberfläche
> angeboten.
>
> **Umsetzung (Phase 5, serielle Schnittstelle):** Der serielle Port ist die
> **einzige** Einstellung, die ohne Neustart umgestellt werden kann. Grund:
> Der Benutzer steckt den Adapter oft erst **nach** dem Start ein oder er
> wechselt die USB-Buchse. `BacnetSerialManager` schreibt die Konfiguration,
> aktualisiert die `SerialPortSettings` und fordert den Neuaufbau des
> MS/TP-Transports an - der Backoff (bis 30 s) wird dabei abgebrochen, der
> Port wird **sofort** neu geöffnet.

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
