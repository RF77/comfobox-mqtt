# Containerisierung

## 1. Zwei Images statt einem

**Entscheidung:** zwei getrennte Images.

| Image | Inhalt | Warum getrennt |
|---|---|---|
| `comfobox-gateway` | .NET 10 ASP.NET Core | enthält native Abhängigkeiten (`System.IO.Ports`), keine Node-Tools |
| `comfobox-web` | nginx + statisches Angular-Bundle | unabhängige Update-Zyklen, kein Node im Runtime-Image |

> **Warum nicht ein Image mit beiden:** Zwei-Prozess-Supervision in einem Container ist
> fehleranfällig (Signal-Forwarding, Restart-Semantik, Log-Mixing). nginx leitet `/api` intern
> an `gateway:8080` weiter – der Nutzer sieht nur einen Port.

## 2. Dockerfile – Gateway

````dockerfile
# syntax=docker/dockerfile:1
# ─────────────────────────────────────────────────────────────────────────────
# Build-Stufe
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
# Runtime-Stufe
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

# Verzeichnisse für persistierte Daten und Logs
RUN mkdir -p /var/lib/comfobox /var/log/comfobox \
 && chown -R comfobox:comfobox /app /var/lib/comfobox /var/log/comfobox

USER comfobox

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=1 \
    COMFOBOX_Bacnet__Port=/dev/ttyUSB0

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
| `alpine` statt `debian` | kleineres Image (~230 MB statt ~450 MB), läuft auf der DS923+ (x86_64) |
| `tini` als PID 1 | ohne PID-1 bekommt der .NET-Prozess kein `SIGTERM` → `docker stop` dauert 10 s und killt hart → **LWT wird nicht ausgelöst**, HA bleibt im alten Zustand |
| `USER comfobox` | kein Root im Container |
| `InvariantGlobalization=false` | `de-DE`-Formatierung für die UI (`valueText`) – Payload bleibt invariant |
| `HEALTHCHECK` mit `curl` | `curl` ist 1 MB – das Wegen ist den schlanken Runtime-Container wert |
| `/p:UseAppHost=false` | kleineres Publish-Artefakt |
| `VOLUME /var/lib/comfobox` | persistenter Schreibzähler (Befund A3) |

> **Zu `tini`:** Das ist kein Luxus. Ohne PID-1 leitet Docker `SIGTERM` an PID 1, aber der
> .NET-Prozess ist PID 1 → er bekommt es, aber ohne Zombie-Reaping und ohne korrektes
> Shutdown-Signal an die `IHostedService`s. `tini` löst das sauber.

## 3. Dockerfile – Web

````dockerfile
# syntax=docker/dockerfile:1
FROM node:22-alpine AS build
WORKDIR /app

COPY src/ComfoBox.Web/package.json src/ComfoBox.Web/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY src/ComfoBox.Web/ ./
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS final
COPY --from=build /app/dist/ComfoBox.Web/browser /usr/share/nginx/html
COPY src/ComfoBox.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
````

## 4. nginx.conf

````nginx
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # Basispfad für die SPA
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Statische Assets cachen (Angular hashed filenames)
    location ~* \.(?:js|css|woff2?|svg|png|jpg|jpeg|gif|ico)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # API → Gateway
    location /api/ {
        proxy_pass http://gateway:8080;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SSE: KEIN Buffering, sonst kommen Live-Werte erst beim Verbindungsende
    location = /api/events {
        proxy_pass http://gateway:8080;
        proxy_http_version 1.1;
        proxy_set_header Connection '';
        proxy_buffering off;
        proxy_cache off;
        gzip off;
        chunked_transfer_encoding off;
        proxy_read_timeout 24h;
        proxy_send_timeout 24h;
    }
}
````

## 5. compose.yaml

````yaml
name: comfobox

services:
  gateway:
    image: ghcr.io/<user>/comfobox-gateway:1.0.0
    container_name: comfobox-gateway
    restart: unless-stopped
    depends_on:
      - mosquitto
    ports:
      - "127.0.0.1:8080:8080"     # nur lokal → Zugriff über nginx
    environment:
      COMFOBOX_Bacnet__Port: "/dev/ttyUSB0"
      COMFOBOX_Bacnet__Baudrate: "38400"
      COMFOBOX_Gateway__BaseTopic: "ComfoBox"
      COMFOBOX_Mqtt__Brokers__0__Host: "mosquitto"
      COMFOBOX_Mqtt__Brokers__0__Port: "1883"
      COMFOBOX_Mqtt__Brokers__0__Username: "comfobox"
      COMFOBOX_Mqtt__Brokers__0__Password: "${MQTT_PASSWORD}"
      COMFOBOX_Mqtt__EnableDiscovery: "true"
      COMFOBOX_Logging__Level: "Information"
    devices:
      - "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0:/dev/ttyUSB0"
    group_add:
      - "dialout"
    volumes:
      - comfobox-data:/var/lib/comfobox
      - ./logs:/var/log/comfobox
    devices_note: "siehe devices: weiter oben"

  web:
    image: ghcr.io/<user>/comfobox-web:1.0.0
    container_name: comfobox-web
    restart: unless-stopped
    depends_on:
      - gateway
    ports:
      - "8081:80"

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
| `127.0.0.1:8080:8080` für das Gateway | das Gateway muss **nicht** öffentlich erreichbar sein – nginx ist der einzige Eintrittspunkt |
| Volume `comfobox-data` | persistenter Schreibzähler (24-h-Limit) |
| `COMFOBOX_*` Env-Variablen | überschreiben die JSON-Config (siehe [konfiguration-und-secrets](konfiguration-und-secrets.md)) |

## 6. Image-Größe

| Image | Erwartete Größe |
|---|---|
| `comfobox-gateway` | ~230 MB (alpine + .NET 10 ASP.NET Runtime) |
| `comfobox-web` | ~55 MB (nginx + statische Dateien) |
| `comfobox-mosquitto` | ~25 MB |

**Optimierung bei Bedarf:**

| Maßnahme | Ersparnis | Nachteil |
|---|---|---|
| `dotnet publish -p:PublishTrimmed=true` | ~40 MB | **Achtung:** Reflection (Fasterflect ist weg, aber `System.Text.Json` nutzt Reflection) → Probleme. **Nicht empfohlen**, solange Reflection im Einsatz ist. |
| `-p:PublishSingleFile=true` | ~30 MB | Startzeit höher, Debugging schwerer |
| `chiseled`-Runtime-Images (`:10.0-alpine-chiseled`) | ~60 MB | kein Shell im Container |

> Auf einer DS923+ (4 GB RAM, AMD Ryzen R1600) ist ~300 MB Gesamtgröße irrelevant. **Priorität hat
> Robustheit, nicht Größe.** → Kein Trimming.

## 7. Healthcheck-Semantik

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

## 8. Logs

| Container | Log-Ziel | Format |
|---|---|---|
| `gateway` | stdout (JSON) + Datei `/var/log/comfobox/gateway-*.jsonl` | JSON (strukturiert) |
| `web` | stdout (nginx) | Text |
| `mosquitto` | stdout + `/mosquitto/log` | Text |

`docker compose logs -f gateway` zeigt die strukturierten Logs inkl. aller Health-Events.

## 9. Aufräumen & Updates

````bash
# Alte Images entfernen
docker image prune -a -f

# Container neu starten (nach Update)
docker compose up -d --force-recreate

# Vollständiger Reset (ACHTUNG: löscht den Schreibzähler)
docker compose down -v
````

## 10. Sicherheits-Härtung

| Maßnahme | Umsetzung |
|---|---|
| Kein Root | `USER comfobox` |
| Nur nötige Rechte | `group_add: dialout`, kein `privileged` |
| Minimales Base-Image | alpine |
| Nur ein exponierter Port | `8081` (nginx). Das Gateway lauscht nur auf `127.0.0.1` |
| Secrets nicht im Image | Env-Variablen / `.env` mit `chmod 600` |
| Abhängigkeits-Scan | Trivy in CI (siehe [ci-cd](ci-cd.md)) |
| `read_only` Root-FS | optional (`read_only: true` + `tmpfs`), braucht Anpassungen für Logs |
| `no-new-privileges` | `security_opt: ["no-new-privileges:true"]` |
