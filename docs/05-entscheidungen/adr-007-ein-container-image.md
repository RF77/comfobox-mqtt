# ADR-007: Ein einziges Container-Image für Gateway und Web-UI

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Betrieb, Deployment, Frontend |

## Kontext

Anforderung 2 verlangt eine Web-UI, Anforderung 3 einen Container. Ursprünglich war in
[ADR-004](adr-004-containerisierung.md) vorgesehen:

| Image | Inhalt |
|---|---|
| `comfobox-gateway` | .NET 10 ASP.NET Core |
| `comfobox-web` | nginx + statisches Angular-Bundle |

Bei der Ausarbeitung der **Installation als Synology-Paket** (→ [ADR-008](adr-008-synology-spk.md))
wurden zwei Dinge relevant:

1. Das SPK soll mit **einem** Klick installierbar sein und **einen** Port öffnen.
2. Der Nutzer will **bequem** installieren – jede zusätzliche Komponente ist eine
   zusätzliche Fehlerquelle (Netzwerk zwischen Containern, Port-Konflikte, Restart-Orchestrierung).

Zusätzlich fiel auf, dass die Trennung **technisch keinen Mehrwert** hat:

| Aspekt | Auswirkung der Trennung |
|---|---|
| API-Aufruf aus der UI | Der Browser ruft nginx → nginx ruft `gateway:8080` → CORS/Proxy nötig? Nein, aber ein zusätzlicher Hop |
| SSE (Live-Werte) | nginx braucht `proxy_buffering off` – bei Fehlkonfiguration hängen die Live-Werte |
| CORS | nicht nötig (gleicher Origin), aber die Konfiguration muss es *ausschließen* |
| Startreihenfolge | `depends_on`, Healthcheck-Abhängigkeiten, Retry-Logik |
| Image-Größe | zwei Images (~230 MB + ~55 MB) statt einem (~250 MB) |
| Container-Manager-UI | zwei Container in der Liste statt einem |
| Supervisor | zwei `up -d`-Aufrufe, zwei Healthchecks |

## Entscheidung

**Gateway und Web-UI werden in EIN Docker-Image gepackt. Es gibt genau einen Container,
genau einen Port und genau einen Healthcheck.**

- Der Angular-Build wird im Docker-Build in das `wwwroot/` des .NET-Hosts kopiert
- ASP.NET Core liefert die SPA statisch aus (`UseStaticFiles` + `MapFallbackToFile`)
- Der Container läuft als `comfobox-gateway` auf Port 8080 (intern) → 8081 (extern auf dem NAS)

### Dockerfile-Struktur

````dockerfile
FROM node:22-alpine AS web-build          # 1. Angular bauen
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build   # 2. .NET bauen
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final # 3. Laufzeit
COPY --from=web-build /app/dist/ComfoBox.Web/browser ./wwwroot
````

### Statische Auslieferung aus ASP.NET Core

````csharp
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
````

> **SPA-Fallback:** `MapFallbackToFile` muss **nach** den API-Routen registriert werden,
> sonst schluckt es `/api/...`.

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **Ein Image (Gateway + UI)** ✅ | 1 Port, 1 Healthcheck, kein Proxy, kein SSE-BUFFERING-Problem, einfaches SPK | – |
| Zwei Images (Gateway + nginx) | sauberere Trennung, nginx-Caching | ein zusätzlicher Hop, ein zusätzlicher Fehlerpunkt (Proxy-Config), zwei Container in der DSM-UI – bei **einer** UI kein Mehrwert |
| Ein Image, **zwei** Prozesse (supervisord/s6) | ein Container | Zwei-Prozess-Supervision ohne init-System ist fehleranfällig (Signal-Forwarding, Restart-Logik) |
| UI in einem Go/Node-Service neben .NET | – | dritte Technologie, dritter Build – pure Overhead |
| nginx **im selben** Image, aber als einziger Einstiegspunkt | ein Port | der .NET-Prozess wäre dann intern → gleiche Komplexität wie zwei Container, aber schlechtere Fehlersuche |

## Konsequenzen

### Positiv

- **Ein** Port, **ein** Healthcheck, **ein** Container in der DSM-Oberfläche
- Kein Reverse-Proxy, kein CORS, kein `proxy_buffering`-Fehlermodus bei SSE
- Das Synology-Paket wird einfach: eine Compose-Datei, ein Service, ein `adminport`
- Kleineres Gesamtsystem (250 MB statt 285 MB, ein Image weniger zu pullen)
- Deployment auf anderen Plattformen (Raspberry Pi, Linux-Server) ohne nginx-Overhead
- **Ein** Ort für Fehleranalyse: `docker logs` zeigt Gateway **und** UI-Requests

### Negativ / Aufwand

- Das .NET-Image enthält die gebaute SPA → der Build ist nicht mehr rein .NET
  (der Angular-Build muss in CI laufen, bevor `dotnet publish` greift)
- **Kein** nginx-Caching für statische Assets → Angular-Bundle wird bei jedem
  Cold Start erneut ausgeliefert (auf einem NAS im LAN irrelevant)
- **Keine** `gzip`-Kompression durch nginx → ASP.NET Core muss selbst komprimieren
  (`AddResponseCompression` mit Brotli/Gzip – 5 Zeilen)
- Der Container ist „dick" (enthält UI + Gateway) → klarere Trennung fehlt
  (aber: die Trennung existiert im Code weiterhin, `ComfoBox.Domain` bleibt framework-frei)
- Bei einem reinen UI-Release muss das ganze Image neu gebaut werden

### Neutral

- Der Broker bleibt ein **eigener** Container (`mosquitto`) oder ein externer Dienst –
  er ist nicht Teil des Images.
- Für **manuelles** Compose-Deployment bleibt die Trennung möglich (zwei Compose-Services),
  das SPK nutzt die Ein-Image-Variante.

## Verwandte Dokumente

- [ui-angular-optimus §11](../02-modernisierung/ui-angular-optimus.md#11-build--auslieferung)
- [containerisierung](../03-deployment/containerisierung.md)
- [synology-paketcenter](../03-deployment/synology-paketcenter.md)
- [ADR-004](adr-004-containerisierung.md) · [ADR-008](adr-008-synology-spk.md)
