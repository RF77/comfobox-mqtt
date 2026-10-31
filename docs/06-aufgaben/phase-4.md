# Phase 4 – REST/SSE-API (4 PT)

**Ziel:** Die Schnittstelle zwischen Gateway und Web-UI – **und** die Basis für die
HA-Integration ohne MQTT.
**Braucht:** Phase 2, Phase 3
**Spec:** [datenmodell-und-api](../02-modernisierung/datenmodell-und-api.md)

> **Diese API ist ein Produktmerkmal**, nicht nur ein UI-Hilfsmittel. Die
> [HACS-Integration](../02-modernisierung/ha-integration.md) spricht genau diese
> Endpunkte an. Änderungen am DTO sind **breaking changes** für HA-Nutzer.

---

## 4.1 – Items-Endpunkte · 1.0 PT

| | |
|---|---|
| **Endpunkte** | `GET /api/items`, `GET /api/items/{id}`, `PUT /api/items/{id}` |

| Methode | Pfad | Antwort |
|---|---|---|
| `GET` | `/api/items` | `ItemDto[]` |
| `GET` | `/api/items/{id}` | `ItemDto` |
| `PUT` | `/api/items/{id}` | `204` / Fehlercode |

**Statuscodes (verbindlich):**

| Code | Bedeutung |
|---|---|
| `204` | geschrieben |
| `400` | ungültige Anfrage |
| `403` | Expertenmodus nötig |
| `409` | Tageslimit überschritten / nicht schreibbar |
| `422` | Wert außerhalb des Bereichs / falscher Typ |
| `502` | die Heizung hat nicht geantwortet |

**Akzeptanzkriterien:**
- [ ] Min/Max/Enum-Werte kommen **unverändert** aus dem Katalog
- [ ] Ein ungültiger Wert wird **abgelehnt** (kein Default, kein `0`)
- [ ] Der Schreibzähler wird bei jedem erfolgreichen Schreibvorgang erhöht
- [ ] Fehler landen in `Status/Errors`

---

## 4.2 – Health, Status, Diagnostics · 0.5 PT

| | |
|---|---|
| **Endpunkte** | `GET /api/health`, `GET /api/status`, `GET /api/diagnostics/errors` |

`/api/status` liefert (verbindlich für die HA-Integration):

````json
{
  "deviceId": "comfobox1",
  "version": "1.0.0",
  "uptimeSeconds": 4523,
  "bacnet": { "connected": true, "port": "/dev/ttyUSB0", "lastError": null },
  "mqtt":   { "connected": false, "broker": "core-mosquitto" },
  "writes": { "count": 17, "limit": 200, "resetsAt": "2026-11-01T00:00:00Z" }
}
````

**Akzeptanzkriterien:**
- [ ] Die Feldnamen sind **exakt** wie oben (die HA-Integration liest sie)
- [ ] `/api/health` antwortet auch bei `degraded` mit HTTP 200
- [ ] Die Fehlerhistorie ist ein Ringpuffer (letzte 50)

---

## 4.3 – SSE-Stream · 1.0 PT

| | |
|---|---|
| **Endpunkt** | `GET /api/events` |

````csharp
ctx.Response.Headers.ContentType = "text/event-stream";
ctx.Response.Headers.CacheControl = "no-cache";
ctx.Response.Headers["X-Accel-Buffering"] = "no";

await writer.WriteAsync($"event: snapshot\ndata: {JsonSerializer.Serialize(cache.Snapshot())}\n\n");
await writer.FlushAsync(ct);

await foreach (var evt in cache.SubscribeAsync(ct))
{
    await writer.WriteAsync($"event: value\ndata: {JsonSerializer.Serialize(evt)}\n\n");
    await writer.FlushAsync(ct);
}
````

| Ereignis | Inhalt |
|---|---|
| `snapshot` | alle aktuellen Werte, einmalig beim Verbinden |
| `value` | ein geänderter Wert |

**Akzeptanzkriterien:**
- [ ] `snapshot` + `value`-Events
- [ ] Abbruch des Clients beendet den Stream sauber (kein Leck)
- [ ] Kein `proxy_buffering`-Problem (es gibt keinen Proxy → [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md))

---

## 4.4 – Konfigurations-Endpunkt · 0.5 PT

| | |
|---|---|
| **Endpunkte** | `GET /api/config`, `PUT /api/config` |

**Präzedenz der Konfigurationsquellen:**

```
config.json im Datenverzeichnis   ← vom Assistenten geschrieben, VORRANG
        ↓
Umgebungsvariablen COMFOBOX__*    ← Override
        ↓
appsettings.json (im Image)        ← Defaults
```

> **Warum die Datei Vorrang hat:** Passwörter in Env-Variablen landen sichtbar in
> `docker inspect`.

**Akzeptanzkriterien:**
- [ ] `GET` liefert die Konfiguration **ohne** Passwörter (nur Hashes)
- [ ] `PUT` schreibt nach `config.json` (Modus 0600)
- [ ] Die Präzedenz ist implementiert **und** getestet

---

## 4.5 – Restart & ServiceControl · 0.5 PT

| | |
|---|---|
| **Endpunkt** | `POST /api/service/restart` |

> **Der Neustart läuft in-process** (`IHostApplicationLifetime.StopApplication()`),
> **nicht** per `docker restart`. Der Container wird vom Supervisor neu gestartet.
> Das ist sicherer (kein Docker-Socket im Container) und funktioniert auch ohne Docker.

| Aspekt | Wert |
|---|---|
| Erlaubt | nur für Administratoren |
| Antwort | `202 Accepted` |
| Verbindung | wird sauber getrennt (der Browser zeigt „wird neu gestartet …“) |

**Akzeptanzkriterien:**
- [ ] `POST /api/service/restart` → `202`
- [ ] Beim Herunterfahren wird `offline` publiziert, **dann** getrennt
- [ ] Kein Docker-Socket nötig

---

## 4.6 – OpenAPI/Swagger · 0.5 PT

| | |
|---|---|
| **Endpunkt** | `/swagger` (nur im Development-Modus) |

**Akzeptanzkriterien:**
- [ ] `/swagger` zeigt alle Endpunkte mit Schemata
- [ ] Die DTOs sind als Schema dokumentiert
- [ ] Fehlercodes sind beschrieben

---

## 4.7 – Setup-API (Getting Started) · 1.5 PT

| | |
|---|---|
| **Endpunkte** | siehe [datenmodell §3.4](../02-modernisierung/datenmodell-und-api.md#34-setup-endpunkte-getting-started) |
| **Bezug** | Aufgabe 8.7 / Phase 8 |

> Diese Aufgabe ist **hier** aufgeführt, weil sie die API betrifft – umgesetzt wird
> sie zusammen mit dem Assistenten (Phase 8.8), da beide ineinandergreifen.

| Endpunkt | Zweck |
|---|---|
| `GET /api/setup/state` | `setupCompleted`? |
| `GET /api/setup/ports` | erkannte serielle Ports |
| `GET /api/setup/ports/suggest` | freien Web-Port vorschlagen |
| `POST /api/setup/bacnet/test` | BACnet-Verbindung testen |
| `POST /api/setup/mqtt/test` | MQTT-Verbindung testen |
| `PUT /api/setup/config` | Konfiguration schreiben |
| `POST /api/setup/apply` | übernehmen → Neustart |

**Akzeptanzkriterien:**
- [ ] `PUT /api/setup/config` hasht das Passwort **serverseitig** (PBKDF2, 600.000 Iterationen)
- [ ] In `config.json` steht **kein** Klartext-Passwort; Datei-Modus 0600
- [ ] `POST /api/setup/apply` setzt `setupCompleted=true` und stößt den Neustart aus
- [ ] Nach der Ersteinrichtung sind alle `/api/setup/*`-Endpunkte authentifizierungspflichtig
- [ ] Fehler liefern einen kontextbezogenen `hint` (`no_iam`, `busy`, `permission_denied`, …)

---

## 4.8 – Token-API · 1.0 PT

| | |
|---|---|
| **Endpunkte** | `POST /api/security/tokens`, `DELETE /api/security/tokens/{id}` |
| **Bezug** | Aufgabe 8d.1 / Phase 8a–d |

| Aspekt | Wert |
|---|---|
| Erzeugen | nur für Administratoren |
| Anzeige | der Token wird **einmal** im Klartext angezeigt |
| Speicherung | nur der **Hash** (`pbkdf2-sha256$600000$…`) in `config.json` |
| Verwendung | `Authorization: Bearer <token>` |
| Widerruf | `DELETE` → der Token wirkt sofort nicht mehr |

**Akzeptanzkriterien:**
- [ ] Der Token ist **einmal** sichtbar, danach nur der Hash
- [ ] PBKDF2-HMAC-SHA256 mit 600.000 Iterationen
- [ ] Ein ungültiger Token → `401`
- [ ] Ein widerrufener Token → `401`
- [ ] Der Token erscheint **nirgends** im Log

---

## Abnahme der Phase 4

- [ ] Alle Endpunkte vorhanden und getestet
- [ ] Validierung mit korrekten Statuscodes
- [ ] SSE liefert `snapshot` + `value`
- [ ] Fehler landen in `Status/Errors`
- [ ] API-Tests grün

→ Weiter mit [Phase 5](phase-5.md)
