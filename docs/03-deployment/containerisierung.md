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
RUN npm run build -- --configuration production

# ─────────────────────────────────────────────────────────────────────────────
# Stage 2: .NET-Build
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# NuGet-Restore zuerst → Layer-Cache
COPY src/Directory.Packages.props src/
COPY src/*.props src/
RUN dotnet restore src/ComfoBox.Gateway/ComfoBox.Gateway.csproj

COPY src/ src/
RUN dotnet publish src/ComfoBox.Gateway/ComfoBox.Gateway.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false \
        /p:InvariantGlobalization=false

# ─────────────────────────────────────────────────────────────────────────────
# Stage 3: Runtime
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

# Serielle Ports: der Paket-Namespace reicht auf Linux nicht aus,
# das Gerät muss zusätzlich per --device durchgereicht werden.
# tini sorgt für korrekte Signal-Weiterleitung (SIGTERM → Prozessende).
RUN apk add --no-cache tini curl \
 && addgroup -S comfobox -g 1001 \
 && adduser -S comfobox -u 1001 -G comfobox

WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=web-build /app/dist/ComfoBox.Web/browser ./wwwroot

# Verzeichnisse für persistierte Daten und Logs
RUN mkdir -p /var/lib/comfobox /var/log/comfobox \
 && chown -R comfobox:comfobox /app /var/lib/comfobox /var/log/comfobox

USER comfobox

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=1 \
    COMFOBOX_CONFIG_FILE=/var/lib/comfobox/config.json

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
| `alpine` statt `debian` | kleineres Image (~250 MB statt ~450 MB), läuft auf der DS923+ (x86_64) |
| 3 Stages (node → sdk → aspnet) | im Runtime-Image ist **kein** Node und **kein** SDK |
| `tini` als PID 1 | ohne PID-1 leidet der .NET-Prozess kein `SIGTERM` → `docker stop` dauert 10 s und killt hart → **LWT wird nicht ausgelöst**, HA bleibt im alten Zustand |
| `USER comfobox` | kein Root im Container |
| `InvariantGlobalization=false` | `de-DE`-Formatierung für die UI (`valueText`) – Payload bleibt invariant |
| `HEALTHCHECK` mit `curl` | `curl` ist 1 MB – das Wegen ist den schlanken Runtime-Container wert |
| `/p:UseAppHost=false` | kleineres Publish-Artefakt |
| `VOLUME /var/lib/comfobox` | persistenter Schreibzähler (Befund A3) + `config.json` |
| `COMFOBOX_CONFIG_FILE` | Zeiger auf die Konfigurationsdatei (siehe [konfiguration-und-secrets](konfiguration-und-secrets.md)) |

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

````yaml
name: comfobox

services:
  gateway:
    image: ghcr.io/<user>/comfobox-gateway:1.0.0
    container_name: comfobox-gateway
    restart: unless-stopped
    ports:
      - "8081:8080"          # Web-UI + API
    environment:
      COMFOBOX_CONFIG_FILE: "/var/lib/comfobox/config.json"
    devices:
      - "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0:/dev/ttyUSB0"
    group_add:
      - "dialout"
    volumes:
      - comfobox-data:/var/lib/comfobox
      - ./logs:/var/log/comfobox
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
| `group_add: ["dialout"]` | Zugriff auf das Device-Node, falls nicht root |
| `restart: unless-stopped` | Neustart nach Crash/Neustart des NAS; **kein** Neustart bei `unhealthy` |
| `8081:8080` | der einzige exponierte Port (UI **und** API) |
| Volume `comfobox-data` | persistenter Schreibzähler (24-h-Limit) + `config.json` |
| `security_opt: no-new-privileges` | Privilege-Escalation im Container verhindern |
| **kein** `docker.sock` | der Container braucht keine Container-Orchestrierung |

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

| Image | Erwartete Größe |
|---|---|
| `comfobox-gateway` (inkl. UI) | ~250 MB (alpine + .NET 10 ASP.NET Runtime + ~2 MB Angular-Bundle) |
| `comfobox-mosquitto` | ~25 MB |

**Optimierung bei Bedarf:**

| Maßnahme | Ersparnis | Nachteil |
|---|---|---|
| `dotnet publish -p:PublishTrimmed=true` | ~40 MB | **Achtung:** Reflection (`System.Text.Json`) → Probleme. **Nicht empfohlen**, solange Reflection im Einsatz ist. |
| `-p:PublishSingleFile=true` | ~30 MB | Startzeit höher, Debugging schwerer |
| `chiseled`-Runtime-Images (`:10.0-alpine-chiseled`) | ~60 MB | kein Shell im Container |

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
