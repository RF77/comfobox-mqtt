# Containerisierung

> **Ein Image für Gateway und Web-UI** → [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md).
> Die Verteilung als Synology-Paket steht in [synology-paketcenter.md](synology-paketcenter.md).

## 1. Ein Image statt zwei

**Entscheidung:** ein einziges Image, ein Container, ein Port.

| Image | Inhalt | Größe |
|---|---|---|
| `comfobox-gateway` | .NET 10 ASP.NET Core **+** gebautes Angular-Bundle in `wwwroot/` | ~250 MB |
| `mosquitto` *(optional)* | MQTT-Broker | ~25 MB |

| Entscheidung | Begründung |
|---|---|
| **Ein** Image | ein Port, ein Healthcheck, kein Reverse-Proxy, kein CORS, kein SSE-Buffering-Problem → [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md) |
| ASP.NET Core liefert die SPA aus | `UseDefaultFiles()` + `UseStaticFiles()` + `MapFallbackToFile("index.html")` |
| Kein nginx-Container | ein zusätzlicher Proxy ist ein zusätzlicher Fehlerpunkt ohne Mehrwert |
| **Kein** Zwei-Prozess-Container | Supervision von zwei Prozessen (Signal-Forwarding, Restart-Semantik, Log-Mixing) ist fehleranfällig |

## 2. Dockerfile – Gateway + UI

> Die Datei liegt in [`src/ComfoBox.Gateway/Dockerfile`](../../src/ComfoBox.Gateway/Dockerfile).
> Der Build-Kontext ist das **Repository-Wurzelverzeichnis** (das Image braucht
> sowohl das .NET-Projekt als auch die Angular-Oberfläche):
>
> ````bash
> docker build -f src/ComfoBox.Gateway/Dockerfile -t comfobox-gateway:local .
> ````

````dockerfile
# syntax=docker/dockerfile:1

# ─────────────────────────────────────────────────────────────────────────────
# Stage 1: Angular-Build
# ─────────────────────────────────────────────────────────────────────────────
FROM node:22-alpine AS web-build
WORKDIR /app

# package*.json zuerst → Layer-Cache
COPY src/ComfoBox.Web/package.json src/ComfoBox.Web/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY src/ComfoBox.Web/ ./
RUN npm run build

# ─────────────────────────────────────────────────────────────────────────────
# Stage 2: .NET-Build
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# NuGet-Restore zuerst → Layer-Cache
COPY src/global.json src/Directory.Build.props src/Directory.Packages.props ./
COPY src/ComfoBox.Domain/ComfoBox.Domain.csproj src/ComfoBox.Domain/
COPY src/ComfoBox.Domain/packages.lock.json src/ComfoBox.Domain/
COPY src/ComfoBox.Bacnet/ComfoBox.Bacnet.csproj src/ComfoBox.Bacnet/
COPY src/ComfoBox.Bacnet/packages.lock.json src/ComfoBox.Bacnet/
COPY src/ComfoBox.Gateway/ComfoBox.Gateway.csproj src/ComfoBox.Gateway/
COPY src/ComfoBox.Gateway/packages.lock.json src/ComfoBox.Gateway/
RUN dotnet restore src/ComfoBox.Gateway/ComfoBox.Gateway.csproj

COPY src/ src/

# Die gebaute SPA an die Stelle, die das Gateway-csproj erwartet
COPY --from=web-build /app/dist/ComfoBox.Web/browser \
     src/ComfoBox.Web/dist/ComfoBox.Web/browser

RUN dotnet publish src/ComfoBox.Gateway/ComfoBox.Gateway.csproj \
        --configuration Release \
        --output /app/publish \
        --no-restore \
        /p:UseAppHost=false \
        /p:InvariantGlobalization=false

# ─────────────────────────────────────────────────────────────────────────────
# Stage 3: Runtime
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

# tini: PID 1 ohne Zombie-Reaping und ohne Signal-Weiterleitung.
# curl:  nur für den Healthcheck.
# icu-*: das Alpine-Basis-Image setzt DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
#         (alpine bringt kein ICU mit) – die Oberfläche formatiert deutsch.
RUN apk add --no-cache tini curl icu-libs icu-data-full \
 && addgroup -S comfobox -g 1001 \
 && adduser -S comfobox -u 1001 -G comfobox

WORKDIR /app
COPY --from=build /app/publish ./

RUN mkdir -p /var/lib/comfobox /var/log/comfobox \
 && chown -R comfobox:comfobox /app /var/lib/comfobox /var/log/comfobox

USER comfobox

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=1 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    COMFOBOX_Gateway__DataDirectory=/var/lib/comfobox \
    COMFOBOX_Logging__Directory=/var/log/comfobox

EXPOSE 8080
VOLUME ["/var/lib/comfobox"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=25s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["/sbin/tini", "--"]
CMD ["dotnet", "ComfoBox.Gateway.dll"]
````

### Entscheidungen im Dockerfile

| Entscheidung | Begründung |
|---|---|
| `alpine` statt `debian` | kleineres Image (~260 MB statt ~450 MB), läuft auf der DS923+ (x86_64) |
| 3 Stages (node → sdk → aspnet) | im Runtime-Image ist **kein** Node und **kein** SDK |
| `tini` als PID 1 | ohne PID-1 leidet der .NET-Prozess kein `SIGTERM` → `docker stop` dauert 10 s und killt hart → **LWT wird nicht ausgelöst**, HA bleibt im alten Zustand |
| `USER comfobox` | kein Root im Container |
| `InvariantGlobalization=false` + `icu-libs` | `de-DE`-Formatierung für die UI (`valueText`) – Payload bleibt invariant. **Achtung:** das Alpine-Basis-Image setzt `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` – ohne ICU und ohne explizites `false` bricht die Formatierung. |
| `HEALTHCHECK` mit `curl` | `curl` ist 1 MB – das Wegen ist den schlanken Runtime-Container wert |
| `/p:UseAppHost=false` | kleineres Publish-Artefakt |
| `VOLUME /var/lib/comfobox` | persistenter Schreibzähler (Befund A3) + `config.json` |
| `.dockerignore` im Repo-Root | hält den Build-Kontext klein und verhindert, dass lokale Artefakte (`bin/`, `obj/`, `data/`, `logs/`) ins Image gelangen |

> **Fallstrick:** Die Pfade in `.dockerignore` müssen **verankert** sein
> (`/src/ComfoBox.Gateway/logs/`). Ein unbestimmtes `**/logs` entfernt auch den
> Angular-Quellordner `src/ComfoBox.Web/src/app/features/diagnostics/logs/` aus dem
> Build-Kontext – der Angular-Build bricht dann mit „Unknown reference" ab.
> Dasselbe gilt für `.gitignore` in `src/`.

> **Zu `tini`:** Das ist kein Luxus. Ohne PID-1 leitet Docker `SIGTERM` an PID 1, aber der
> .NET-Prozess ist PID 1 → er bekommt es, aber ohne Zombie-Reaping und ohne korrektes
> Shutdown-Signal an die `IHostedService`s. `tini` löst das sauber.

## 3. SPA-Auslieferung aus dem .NET-Host

````csharp
// src/ComfoBox.Gateway/Program.cs
app.UseDefaultFiles();      // → wwwroot/index.html
app.UseStaticFiles();       // → wwwroot/… (Assets)
app.UseResponseCompression();

// API-Routen zuerst …
app.MapGet("/api/health", …);
app.MapPut("/api/items/{id}", …);

// … dann der SPA-Fallback (MUSS ans Ende!)
app.MapFallbackToFile("index.html");
````

| Punkt | Regel |
|---|---|
| Reihenfolge | `MapFallbackToFile` **nach** allen API-Routen → sonst schluckt es `/api/…` |
| Kompression | `AddResponseCompression()` (Brotli/Gzip) – es gibt kein nginx mehr |
| Asset-Caching | `UseStaticFiles` + `Cache-Control: public, max-age=31536000, immutable` (Angular nutzt gehashte Namen) |
| SSE | braucht **keine** Sonderbehandlung – der Browser spricht direkt mit dem .NET-Host |

> **Vorteil:** kein `proxy_buffering off`, kein `proxy_read_timeout 24h`, kein
> `try_files $uri $uri/ /index.html` – die drei Stellschrauben, an denen SSE-Deployments
> üblicherweise scheitern, entfallen komplett.

## 4. compose.yaml (manueller Betrieb)

> Die Datei liegt in [`deploy/compose/compose.yaml`](../../deploy/compose/compose.yaml).
> Sie ist über eine `.env` (Vorlage: `.env.example`) parametrisiert – Image-Tag,
> Port, Serial-Port, GID und Log-Verzeichnis.

````yaml
name: comfobox

services:
  gateway:
    image: ${COMFOBOX_IMAGE:-ghcr.io/rf77/comfobox-gateway:1.0.0}
    container_name: comfobox-gateway
    restart: unless-stopped
    ports:
      - "${COMFOBOX_WEB_PORT:-8081}:8080"          # Web-UI + API
    environment:
      COMFOBOX_Gateway__DataDirectory: /var/lib/comfobox
      COMFOBOX_Logging__Directory: /var/log/comfobox
      COMFOBOX_Bacnet__Port: "${COMFOBOX_SERIAL_PORT:-/dev/ttyUSB0}"
      COMFOBOX_Bacnet__Baudrate: "${COMFOBOX_BACNET_BAUDRATE:-38400}"
      ASPNETCORE_URLS: http://+:8080
      DOTNET_gcServer: "1"
    devices:
      - "${COMFOBOX_SERIAL_DEVICE:-/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0}:/dev/ttyUSB0"
    group_add:
      - "${COMFOBOX_DIALOUT_GID:-20}"
    volumes:
      - comfobox-data:/var/lib/comfobox
      - "${COMFOBOX_LOG_DIR:-./logs}:/var/log/comfobox"
    security_opt:
      - "no-new-privileges:true"
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8080/api/health"]
      interval: 30s
      timeout: 5s
      start_period: 25s
      retries: 3

  # optional – eigener Broker, wenn HA nicht auf dem NAS läuft
  mosquitto:
    image: eclipse-mosquitto:2
    container_name: comfobox-mosquitto
    restart: unless-stopped
    ports:
      - "1883:1883"
    volumes:
      - ./mosquitto/config:/mosquitto/config:ro
      - mosquitto-data:/mosquitto/data
      - mosquitto-log:/mosquitto/log

volumes:
  comfobox-data:
  mosquitto-data:
  mosquitto-log:
````

### Wichtige Compose-Details

| Detail | Begründung |
|---|---|
| `devices:` mit `/dev/serial/by-id/…` | stabiler Pfad über Reboots hinweg |
| `group_add: ["20"]` (numerische GID) | Zugriff auf das Device-Node, falls nicht root. `dialout` ist auf DSM die GID 20 – die **numerische** Form ist portabler als der Name. |
| `restart: unless-stopped` | Neustart nach Crash/Neustart des NAS; **kein** Neustart bei `unhealthy` |
| `8081:8080` | der einzige exponierte Port (UI **und** API) |
| Volume `comfobox-data` | persistenter Schreibzähler (24-h-Limit) + `config.json` |
| `security_opt: no-new-privileges` | Privilege-Escalation im Container verhindern |
| **kein** `docker.sock` | der Container braucht keine Container-Orchestrierung |

> **`docker compose down` niemals mit `-v`!** Das Volume `comfobox-data` enthält den
> Schreibzähler – mit `-v` beginnt das 24-h-Fenster neu und der EEPROM-Schutz
> wäre umgangen.

### 4.0 Testen ohne Serial-Port

Auf einem Entwickler-Rechner gibt es kein `/dev/ttyUSB0` – mit dem `devices:`-Block
startet der Container dort gar nicht. Dafür gibt es einen Override in
[`deploy/compose/compose.test.yaml`](../../deploy/compose/compose.test.yaml):

````bash
docker compose -f compose.yaml -f compose.test.yaml up -d
````

Er entfernt den `devices:`-Block (`!override`, braucht Compose ≥ 2.24) und meldet
dem Gateway stattdessen den nicht vorhandenen Port `COM_TEST`. Der Gateway läuft
dann normal: REST-API, Web-Oberfläche, MQTT – nur die BACnet-Abfrage liefert
(erwartungsgemäß) keine Werte.

### 4.1 Template-Variante für den SPK-Betrieb

Im Synology-Paket wird die Compose-Datei **zur Laufzeit gerendert**, weil der gewählte
Serial-Port erst nach der Installation feststeht:

````yaml
services:
  gateway:
    image: ${IMAGE}
    container_name: comfobox-gateway
    restart: unless-stopped
    ports:
      - "${WEB_PORT}:8080"
    environment:
      COMFOBOX_CONFIG_FILE: /var/lib/comfobox/config.json
    devices:
      ${SERIAL_DEVICES}
    group_add:
      - "${DIALOUT_GID}"
    volumes:
      - ${DATA_DIR}:/var/lib/comfobox
      - ${LOG_DIR}:/var/log/comfobox
    security_opt:
      - "no-new-privileges:true"
````

> Der Supervisor (`deploy/synology-spk/src/target/bin/supervisor.sh`) ersetzt die Platzhalter
> per `sed` und startet den Container neu, wenn sich `config.json` oder `ports.json` ändern.
> → [synology-paketcenter §5](synology-paketcenter.md#5-der-supervisor)

## 5. Image-Größe

| Image | Erwartete Größe | Gemessen |
|---|---|---|
| `comfobox-gateway` (inkl. UI) | ~250 MB (alpine + .NET 10 ASP.NET Runtime + ~3 MB Angular-Bundle) | **261 MB** |
| `comfobox-mosquitto` | ~25 MB | – |

> Die 261 MB enthalten **auch die ICU-Datenbank** (`icu-data-full`, ca. 30 MB) für die
> deutsche Formatierung. Ohne sie spart man 30 MB, verliert aber `de-DE`.

**Optimierung bei Bedarf:**

| Maßnahme | Ersparnis | Nachteil |
|---|---|---|
| `dotnet publish -p:PublishTrimmed=true` | ~40 MB | **Achtung:** Reflection (`System.Text.Json`) → Probleme. **Nicht empfohlen**, solange Reflection im Einsatz ist. |
| `-p:PublishSingleFile=true` | ~30 MB | Startzeit höher, Debugging schwerer |
| `chiseled`-Runtime-Images (`:10.0-alpine-chiseled`) | ~60 MB | kein Shell im Container – **nicht** mit `curl` für den Healthcheck |
| `icu-data-full` → `icu-data` | ~15 MB | nur die häufigsten Locales (de ist dabei) |

> Auf einer DS923+ (4 GB RAM, AMD Ryzen R1600) ist ~275 MB Gesamtgröße irrelevant. **Priorität hat
> Robustheit, nicht Größe.** → Kein Trimming.

## 6. Healthcheck-Semantik

Der Dockerfile-Healthcheck prüft **nur**, ob der HTTP-Server antwortet – **nicht**, ob der
Zustand `online` ist:

````dockerfile
HEALTHCHECK CMD curl -fsS http://localhost:8080/api/health || exit 1
````

> **Warum nicht auf `"state":"online"` prüfen:** Ein `degraded`-Zustand (USB-Adapter gezogen)
> ist kein Container-Problem. Ein Neustart würde es nicht beheben, aber die Heizungssteuerung
> würde für Sekunden ausfallen. **Der Healthcheck darf nur „hängt der Prozess" feststellen.**
> Die fachliche Zustandsbewertung macht der Health-Check-Endpoint (siehe
> [health-und-status](../02-modernisierung/health-und-status.md)).

## 7. Logs

| Container | Log-Ziel | Format |
|---|---|---|
| `gateway` | stdout (JSON) + Datei `/var/log/comfobox/gateway-*.jsonl` | JSON (strukturiert) |
| `mosquitto` | stdout + `/mosquitto/log` | Text |

`docker compose logs -f gateway` zeigt die strukturierten Logs inkl. aller Health-Events
**und** der HTTP-Zugriffe der Web-UI (gleicher Prozess).

## 8. Aufräumen & Updates

````bash
# Alte Images entfernen
docker image prune -a -f

# Container neu starten (nach Update)
docker compose up -d --force-recreate

# Vollständiger Reset (ACHTUNG: löscht den Schreibzähler)
docker compose down -v
````

> **Im SPK-Betrieb** übernimmt der Supervisor das Neuerstellen:
> `synopkg restart ComfoBoxGateway` bzw. ein Update über das DSM-Paketcenter.

## 9. Sicherheits-Härtung

| Maßnahme | Umsetzung |
|---|---|
| Kein Root | `USER comfobox` |
| Nur nötige Rechte | `group_add: dialout`, kein `privileged` |
| Minimales Base-Image | alpine |
| Nur ein exponierter Port | `8081` (UI + API) |
| **Kein** Docker-Socket | der Container braucht keine Container-Orchestrierung |
| Secrets nicht im Image | `config.json` (0600) im Volume bzw. `.env` mit `chmod 600` |
| Abhängigkeits-Scan | Trivy in CI (siehe [ci-cd](ci-cd.md)) |
| `read_only` Root-FS | optional (`read_only: true` + `tmpfs`), braucht Anpassungen für Logs |
| `no-new-privileges` | `security_opt: ["no-new-privileges:true"]` |
| Passwort-Hashing | PBKDF2-HMAC-SHA256, 600.000 Iterationen (nie Klartext in der Config) |

## 10. Verwandte Dokumente

- [synology-paketcenter.md](synology-paketcenter.md) – Installation als SPK, Supervisor
- [synology-ds923.md](synology-ds923.md) – Container Manager, udev, Betrieb auf der DS923+
- [konfiguration-und-secrets.md](konfiguration-und-secrets.md) – Config-Dateien, Env-Override, Backups
- [ci-cd.md](ci-cd.md) – Image-Build, Trivy, Release
- [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md) – warum ein Image
