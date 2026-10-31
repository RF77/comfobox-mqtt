# ADR-005: Schnittstelle Gateway ↔ UI – REST + SSE

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | API, UI, Integration |

## Kontext

Die Web-UI ([ADR-003](adr-003-frontend.md)) muss:

1. den **Item-Katalog** mit aktuellen Werten anzeigen (127 Items),
2. Werte **schreiben** können (Analog + Enum),
3. **Live-Werte** ohne Polling-Last anzeigen,
4. **Health- und Fehlerstatus** anzeigen (Anforderung 5),
5. **Konfiguration** ändern können,
6. den **Service neu starten** können (Anforderung 6),
7. mit Fehlern des Gateways umgehen (Broker weg, BACnet weg).

Der Ist-Zustand bietet **nur** MQTT. Die UI könnte direkt über MQTT sprechen (mqtt.js im Browser),
kann dann aber:

- den Serial-Port-Zustand nicht abfragen (nur die publizierten Topics),
- keinen Neustart auslösen (der Restart-Befehl ist ein MQTT-Topic – das ginge),
- die Fehlerhistorie nicht auslesen,
- die Konfiguration nicht ändern,
- nicht unterscheiden zwischen „Wert ist alt" und „Gateway ist tot".

## Entscheidung

**Das Gateway stellt eine REST-API unter `/api` bereit (Lesen/Schreiben/Konfiguration/Aktionen)
und streamt Live-Werte per Server-Sent Events (SSE) unter `/api/events`.**

### Endpunkt-Gruppen

| Gruppe | Endpunkte | Methode |
|---|---|---|
| Items | `/api/items`, `/api/items/{id}`, `PUT /api/items/{id}` | GET/PUT |
| Health | `/api/health`, `/api/status` | GET |
| Diagnose | `/api/diagnostics/errors`, `/api/diagnostics/writes` | GET |
| Config | `/api/config` | GET/PUT |
| Aktionen | `/api/service/restart`, `/api/service/reconnect/{bacnet,mqtt}` | POST |
| Stream | `/api/events` | GET (SSE) |

### Warum REST + SSE und nicht gRPC / GraphQL / WebSockets

| Kriterium | REST + SSE | gRPC-Web | GraphQL | WebSockets |
|---|---|---|---|---|
| Browser-Support | nativ | braucht grpc-web-Proxy | braucht Client-Lib | braucht Client-Lib |
| Bidirektional | nein (reicht nicht) | ja | ja | ja |
| Streaming | SSE (ausreichend) | Streaming | Subscription | ja |
| Debugging | `curl` | schwierig | mittel | schwierig |
| OpenAPI | eingebaut | nein | GraphQL-Schema | nein |
| Aufwand | gering | hoch | mittel | mittel |

Die Anforderung ist **eine Richtung** (Server → UI) plus sporadische Aktionen (UI → Server).
Genau dafür ist SSE gebaut. WebSockets wären Over-Engineering, GraphQL für 8 Endpunkte
unverhältnismäßig.

### Fehlerformat

Einheitliches Fehlerformat für alle Endpunkte:

````json
{
  "error": "value_out_of_range",
  "message": "Wert 40 liegt außerhalb des zulässigen Bereichs [5, 30].",
  "min": 5,
  "max": 30
}
````

### Live-Werte

SSE sendet einen `snapshot` (alle aktuellen Werte) beim Verbindungsaufbau und danach
`value`-Events bei Änderung. nginx muss dafür `proxy_buffering off;` setzen.

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **REST + SSE** ✅ | Standard, debugbar, dokumentierbar, ausreichend | – |
| **Nur MQTT (mqtt.js im Browser)** | kein Backend-Duplikat, WebSocket-Transport | kann Konfiguration/Neustart/Fehlerhistorie nicht abbilden; UI-Auth schwieriger; Status ist nur der publizierte Snapshot |
| **REST + WebSocket** | bidirektional | SSE reicht; WS braucht eigene Reconnect-/Heartbeat-Logik in der UI |
| **GraphQL** | ein Schema, ein Endpunkt | Over-Engineering bei 8 Endpunkten; schwieriger zu testen und zu dokumentieren |
| **gRPC / gRPC-Web** | typisiert, Streaming | zusätzlicher Proxy nötig, schlechtere Browser-Debugbarkeit, kein Vorteil bei dieser Größe |
| **SignalR** | ausgereift, bidirektional | reines .NET-Ökosystem; die UI ist aber ein Browser → SignalR funktioniert, ist aber unnötiger Overhead gegenüber SSE |
| **Polling (REST alle 2 s)** | simpel | 127 Items × 30/min = unnötige Last auf dem NAS und auf dem Serial-Bus |

## Konsequenzen

### Positiv

- Eine **klare, versionierbare** Schnittstelle (`/api`), die unabhängig von MQTT existiert
- Testbar mit `WebApplicationFactory` ohne echten Broker oder echte Hardware
- `curl`/`Postman`/`Swagger UI` → Support ohne UI-Entwickler
- Die UI kann **Fehlerzustände** sauber abbilden (drei Ebenen: HTTP-Status, `Status/Errors`, SSE)
- **Authentifizierung** ist an einer Stelle implementierbar (API-Key / Proxy)
- OpenAPI ist per Generator verfügbar → die UI kann **automatisch** einen API-Client erzeugen
- Die MQTT-Schnittstelle bleibt unabhängig → HA-Anbindung wird nicht von der UI beeinflusst

### Negativ / Aufwand

- **Doppelte Wahrheit:** Werten fließen über MQTT *und* über die API. → Lösung: Beide lesen
  aus demselben `ValueCache`; die API ist ein **Projektion** desselben Zustands, keine zweite Quelle.
- Zwei Protokolle zu pflegen (REST + MQTT)
- SSE reconnectet nicht automatisch → die UI muss einen Reconnect mit Backoff implementieren
  (Browser `EventSource` reconnectet **nicht** zuverlässig bei Server-Absturz → eigener
  `EventSource`-Neuaufbau nötig)
- Authentifizierung muss implementiert werden (offene Frage Q2)

### Neutral

- Der Neustart wird **in-process** ausgelöst (`IHostApplicationLifetime.StopApplication()`),
  nicht per `docker restart` → der Container wird vom Supervisor neu gestartet.
  Das ist sicherer (kein Docker-Socket im Container) und funktioniert auch ohne Docker.
- Die API hört standardmäßig auf `0.0.0.0:8080` innerhalb des Containers; nach außen wird sie
  nur über nginx veröffentlicht (`127.0.0.1:8080`).

## Verwandte Dokumente

- [datenmodell-und-api](../02-modernisierung/datenmodell-und-api.md)
- [health-und-status](../02-modernisierung/health-und-status.md)
- [ui-angular-optimus](../02-modernisierung/ui-angular-optimus.md)
- [ADR-003](adr-003-frontend.md)
