# Health & Status

> Anforderung 5: *„Falls Verbindungsprobleme oder sonst Probleme, soll HA davon mit bekommen.
> Auch unser UI unter 2) soll das anzeigen."*

## 1. Zustandsmodell

```
                  ┌──────────────────────────────────────────┐
                  │            HealthState                   │
                  ├──────────────────────────────────────────┤
  Fehlerfrei  →  │ online                                   │
                  │   BACnet verbunden ✓                     │
                  │   MQTT verbunden ✓                      │
                  │   Polling läuft ✓                        │
                  ├──────────────────────────────────────────┤
  Eingeschränkt →│ degraded                                 │
                  │   mindestens eine Komponente defekt      │
                  │   (BACnet weg / Broker weg / Timeout)    │
                  ├──────────────────────────────────────────┤
  Gateway weg  → │ offline                                  │
                  │   Prozess beendet oder Broker getrennt   │
                  │   (LWT)                                  │
                  └──────────────────────────────────────────┘
```

| Zustand | MQTT-Topic | HA-Verfügbarkeit | UI |
|---|---|---|---|
| `online` | `ComfoBox/Status/Health` = `online` | verfügbar | grün |
| `degraded` | `…` = `degraded` | verfügbar | gelb + Hinweis |
| `offline` | `…` = `offline` (LWT) | **nicht verfügbar** | rot / nicht erreichbar |

## 2. Health-Checks

````csharp
// src/ComfoBox.Gateway/Health/HealthCheck.cs
public enum HealthStatus { Healthy, Degraded, Unhealthy }

public sealed record HealthCheckResult(
    string Name,
    HealthStatus Status,
    string? Message = null,
    DateTimeOffset? LastSuccess = null);

public interface IHealthCheck
{
    string Name { get; }
    Task<HealthCheckResult> CheckAsync(CancellationToken ct);
}
````

| Check | Healthy, wenn | Degraded, wenn | Unhealthy, wenn |
|---|---|---|---|
| `bacnet-connection` | Letzter `IAm` < 60 s alt | Alter `IAm`, Port offen | Port zu / kein `IAm` |
| `bacnet-read` | Letzter erfolgreicher Read < 3 × Poll-Intervall | Zeitüberschreitung × 3 | keine Reads seit 10 × Intervall |
| `mqtt-connection` | `client.IsConnected` | – | nicht verbunden > 60 s |
| `mqtt-discovery` | Discovery zuletzt vollständig publiziert | – | fehlgeschlagen |
| `write-limit` | `count < max` | `count ≥ 90 % max` | `count ≥ max` |
| `poll-loop` | Letzter Poll-Zyklus < 2 × Intervall | – | Loop steht |

> **Unhealthy vs. Degraded:** `Unhealthy` führt zu `offline` (LWT-Payload), `Degraded` zu
> `degraded`. Der Ist-Zustand kannte nur `Connected` 1/0.

## 3. Status-Topics

| Topic | Payload | Retain | QoS | Zweck |
|---|---|---|---|---|
| `ComfoBox/Status/Health` | `online` \| `degraded` \| `offline` | ja | 1 | **Availability** (HA) |
| `ComfoBox/Status/Version` | `1.0.0` | ja | 0 | Version |
| `ComfoBox/Status/Uptime` | `12345` (Sekunden) | ja | 0 | Laufzeit seit Start |
| `ComfoBox/Status/StartedAt` | `2026-10-31T08:00:00Z` | ja | 0 | Startzeitpunkt |
| `ComfoBox/Status/Writes` | `17` | ja | 0 | Schreibvorgänge im aktuellen 24-h-Fenster |
| `ComfoBox/Status/WritesMax` | `200` | ja | 0 | Limit |
| `ComfoBox/Status/Bacnet` | JSON | ja | 0 | Details BACnet |
| `ComfoBox/Status/Mqtt` | JSON | ja | 0 | Details MQTT |
| `ComfoBox/Status/LastError` | Text | ja | 0 | letzter Fehler (Kurzform) |
| `ComfoBox/Status/Errors` | JSON-Array | nein | 0 | Fehlerhistorie (letzte 50) |
| `ComfoBox/Status/Restart` | `restarted` | nein | 0 | Signal nach Neustart |
| `ComfoBox/Connected` | `1` \| `0` | ja | 1 | **Kompatibilität** (Ist-Zustand) |
| `ComfoBox/Version` | `Ver 1.0.0` | nein | 0 | **Kompatibilität** (Ist-Zustand) |

> **Kompatibilitätsregel:** `ComfoBox/Connected` und `ComfoBox/Version` bleiben **exakt** wie im
> Ist-Zustand (Werte `1`/`0` bzw. `Ver <x>`). Neue, aussagekräftigere Topics kommen **dazu**.

### JSON-Payload `Status/Bacnet`

````json
{
  "state": "connected",
  "port": "/dev/ttyUSB0",
  "baudrate": 38400,
  "masterId": 1,
  "deviceName": "ELESTA",
  "lastIAm": "2026-10-31T08:12:40Z",
  "lastRead": "2026-10-31T08:12:44Z",
  "readCount": 1842,
  "errorCount": 3,
  "consecutiveErrors": 0
}
````

### JSON-Payload `Status/Mqtt`

````json
{
  "state": "connected",
  "brokers": [
    { "host": "192.168.1.10", "port": 1883, "connected": true, "reconnects": 2, "lastError": null }
  ],
  "published": 18420,
  "received": 37,
  "discoveryEnabled": true,
  "lastDiscovery": "2026-10-31T08:00:03Z"
}
````

### JSON-Payload `Status/Errors`

````json
[
  {
    "timestamp": "2026-10-31T08:11:02Z",
    "severity": "warning",
    "source": "Bacnet",
    "code": "timeout",
    "message": "ReadProperty (Zone/SetPointNormal) timed out after 1000 ms",
    "detail": "…"
  }
]
````

## 4. Fehler-Sammlung

````csharp
// src/ComfoBox.Gateway/Health/HealthReporter.cs
internal sealed class HealthReporter(
    IEnumerable<IHealthCheck> checks,
    Channel<HealthEvent> channel,
    ILogger<HealthReporter> logger) : IHealthReporter
{
    private readonly Channel<HealthEvent> _events = Channel.CreateBounded<HealthEvent>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Report(HealthEvent evt)
    {
        if (!_events.Writer.TryWrite(evt))
            logger.LogWarning("Fehlerkanal voll – Ereignis verworfen: {Code}", evt.Code);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var evt in _events.Reader.ReadAllAsync(ct))
        {
            _errors.Add(evt);                       // Ringpuffer (letzte 50)
            _lastError = evt;
            await _statusPublisher.PublishAsync(evt, ct);
        }
    }
}
````

**Regeln:**

| Regel | Umsetzung |
|---|---|
| Ringpuffer | letzte **50** Fehler, älteste werden verworfen |
| Deduplizierung | gleicher `code` + gleiche `source` innerhalb von 60 s → **ein** Eintrag, Zähler erhöht |
| Severity | `info` / `warning` / `error` |
| Log-Kopplung | jedes Ereignis wird **auch** geloggt (Serilog) |
| Kein Log-Flood | Health-Checks laufen alle 30 s, nicht im Poll-Takt |

> **Abgrenzung zum Log-Ringpuffer:** Dieser Puffer sammelt **nur Fehler** (letzte 50) und
> steuert den Health-Status. Er ist **nicht** derselbe Puffer wie der Log-Ringpuffer
> (letzte 1000 Log-Einträge, alle Level) für den Log-Viewer in der Web-UI.
> → [logging.md §5](logging.md#5-abgrenzung-fehler-ringpuffer-vs-log-ringpuffer),
> [ADR-011](../05-entscheidungen/adr-011-logging-serilog-und-ringbuffer.md)

## 5. Fehlerklassifikation

| Code | Severity | Auslöser | Effekt auf Health |
|---|---|---|---|
| `bacnet_port_open_failed` | error | Port nicht öffnbar | degraded |
| `bacnet_no_iam` | warning | kein `IAm` innerhalb Timeout | degraded |
| `bacnet_read_timeout` | warning | Read > Timeout | degraded (3× in Folge → unhealthy) |
| `bacnet_write_failed` | error | Write abgelehnt | degraded |
| `mqtt_connect_failed` | error | Broker nicht erreichbar | degraded → offline nach 60 s |
| `mqtt_publish_failed` | warning | Publish nicht möglich | degraded |
| `write_limit_exceeded` | warning | 24-h-Limit erreicht | degraded |
| `value_out_of_range` | info | Schreibwert ungültig | healthy (Benutzerfehler) |
| `expert_mode_required` | info | Schreibwert braucht Expert-Modus | healthy |
| `invalid_value` | info | Parsing fehlgeschlagen | healthy |
| `config_invalid` | error | Konfiguration unvollständig | unhealthy (Startabbruch) |

> **Unterscheidung wichtig:** Benutzerfehler (`value_out_of_range`) dürfen den Health-Status
> **nicht** verschlechtern – der Gateway läuft ja. Geräte-/Verbindungsfehler schon.

## 6. API-Endpunkte

| Route | Inhalt |
|---|---|
| `GET /api/health` | Gesamtzustand + alle Checks |
| `GET /api/status` | Vollständiger Laufzeitstatus (Version, Uptime, Zähler, Broker) |
| `GET /api/diagnostics/errors` | Fehlerhistorie (letzte 50) |
| `GET /api/diagnostics/logs` | **Log-Einträge** (letzte 1000, filterbar) – siehe [logging.md §6](logging.md#6-api) |
| `GET /api/diagnostics/logs/stream` | **Live-Log-Tail** per SSE |
| `GET /api/diagnostics/writes` | Schreibzähler-Historie (24 h) |
| `GET /api/diagnostics/bacnet` | **Bus-Analyse**: Token-Umlauf auf dem MS/TP-Bus |
| `POST /api/diagnostics/bacnet/observe` | Beobachtung starten (3 … 300 s) |
| `DELETE /api/diagnostics/bacnet/observe` | Beobachtung beenden, Polling läuft weiter |

### 6.1 Bus-Analyse (nur lesend)

Die Bus-Analyse zeigt, ob auf dem MS/TP-Bus überhaupt ein Token herumgeht.
Sie ist **rein lesend**: sie stellt keine Anfragen an die ComfoBox und
schreibt keine Werte.

> **Timing:** der MS/TP-Token hat nur wenige Millisekunden Zeit (95 ms bis
> zur Übernahme). Der Gateway protokolliert deshalb **nichts** im laufenden
> Betrieb – die Zustände werden in einen allokationsfreien Ringpuffer
> geschrieben und **außerhalb** des Bus-Takts ausgewertet. Ein Log-Eintrag
> im Hot-Path würde den Token verlieren und den Bus zum Erliegen bringen.

Beispiel:

````json
{
  "state": "degraded",
  "checks": [
    { "name": "bacnet-connection", "status": "Unhealthy", "message": "Port /dev/ttyUSB0 nicht öffnbar", "lastSuccess": null },
    { "name": "mqtt-connection", "status": "Healthy", "lastSuccess": "2026-10-31T08:12:50Z" },
    { "name": "write-limit", "status": "Healthy", "message": "17/200 Schreibvorgänge" }
  ],
  "version": "1.0.0",
  "uptimeSeconds": 4523
}
````

## 7. Anzeige in der UI

Siehe [ui-angular-optimus.md §4](ui-angular-optimus.md#4-dashboard-statusübersicht) – dort ist die
Health-Anzeige (Header-Banner + Statusseite) beschrieben.

## 8. Alarmierung

| Kanal | Trigger | Umsetzung |
|---|---|---|
| HA-Automation | `sensor.comfobox_health` ≠ `online` | MQTT-Trigger in HA |
| HA-Benachrichtigung | Health-Wechsel | `mobile_app`-Aktion |
| Log | jedes Ereignis | Serilog, JSON |
| Docker-Healthcheck | `Health = unhealthy` | `HEALTHCHECK` im Dockerfile (siehe [containerisierung](../03-deployment/containerisierung.md)) |

### Docker-Healthcheck

````dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=25s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1
````

> **Der Healthcheck prüft nur die Erreichbarkeit, nicht den Zustand.** Der Endpoint
> antwortet immer mit HTTP 200 – auch im Zustand `degraded`. Ein `degraded`-Zustand
> (Broker nicht erreichbar, Heizung antwortet nicht) darf den Container **nicht** neu
> starten: Ein Neustart unterbricht die Heizungssteuerung für Sekunden und behebt das
> Problem nicht.
>
> Die fachliche Zustandsbewertung macht der Health-Endpunkt selbst
> (`GET /api/health` → `state`), nicht der Container-Runtime. → [Monitoring & Alarmierung](../benutzer/08-monitoring.md)

## 9. Verhalten bei Ausfällen (Soll)

| Ausfall | Erwartetes Verhalten |
|---|---|
| USB-Adapter gezogen | Health → `degraded`; Fehler im Log; Port wird geschlossen; Retry mit Backoff; **keine** 100-%-CPU (Befund A8) |
| Broker nicht erreichbar | Health → `degraded` → nach 60 s `offline` (LWT); Retry mit Backoff; BACnet-Polling läuft **weiter**; Werte werden im Cache gehalten und beim Reconnect publiziert |
| Broker kommt wieder | `Connected=1` **und** `Status/Health=online` werden publiziert (behebt A10); Discovery + Retained-Werte erneut senden |
| ComfoBox antwortet nicht | Read-Timeout → Warnung; nach 3× in Folge `degraded`; kein Gateway-Neustart (behebt A7) |
| Prozess wird gekillt | LWT → `offline` → HA-Entitäten werden `unavailable` |
| Speicher voller / 24-h-Limit | Schreibvorgänge werden abgelehnt; Health → `degraded` mit `write_limit_exceeded` |
