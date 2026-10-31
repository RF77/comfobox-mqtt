---
description: "Use when writing Dockerfiles, docker-compose files, or deployment configuration for this repository — building images, running on Synology DS923+, serial device passthrough, healthchecks, volumes, or secrets handling."
applyTo: ["**/Dockerfile", "**/docker-compose*.y*ml", "**/compose*.y*ml", "**/.dockerignore", "**/nginx.conf"]
---

# Docker & Deployment – Regeln

## 1. Zielplattform

**Synology DS923+** (AMD Ryzen R1600, x86_64, 4 GB RAM, DSM 7.2, Container Manager / Docker 24+).

⇒ Nur **`linux/amd64`**. Kein arm64 (die DS923+ braucht es nicht; der Build-Aufwand wäre
unnötig doppelt).

## 2. Images

| Image | Base | Inhalt | Zielgröße |
|---|---|---|---|
| `comfobox-gateway` | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` | .NET-Gateway | ~230 MB |
| `comfobox-web` | `nginx:1.27-alpine` | statisches Angular-Bundle | ~55 MB |
| `mosquitto` (optional) | `eclipse-mosquitto:2` | Broker | ~25 MB |

**Regeln:**

- **Multi-Stage-Build**: SDK-Stage zum Bauen, Runtime-Stage als Ziel
- **`tini` als PID 1** → korrektes `SIGTERM` → `docker stop` beendet den Prozess sauber
  (sonst wird der LWT nicht ausgelöst und HA bleibt im alten Zustand)
- **Kein Root**: `USER comfobox`
- **Kein** `privileged: true`, **kein** Docker-Socket
- **Kein** Trimming/AOT (Reflection in `System.Text.Json`) → Robustheit vor Größe
- `HEALTHCHECK` prüft nur die **Erreichbarkeit** des HTTP-Servers, **nicht** `state == online`
  (ein `degraded`-Zustand ist kein Container-Problem und darf keinen Neustart auslösen)

````dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=25s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1
````

## 3. Serielle Schnittstelle

**Ja – Container können auf den Serial Port zugreifen.** Das Device-Node des Hosts wird
per `devices:` durchgereicht.

````yaml
services:
  gateway:
    devices:
      - "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0:/dev/ttyUSB0"
    group_add:
      - "dialout"
````

| Regel | Begründung |
|---|---|
| `/dev/serial/by-id/…` statt `/dev/ttyUSB0` | Pfad ist reboot-stabil (`ttyUSB0` kann sich ändern) |
| `group_add: dialout` | Zugriff auf das Device-Node ohne Root |
| **Nie** `privileged` | unnötige Rechte |
| `restart: unless-stopped` | NAS-Neustart → Container startet mit |
| Container muss den Port **spät enumerierten** Geräten tolerieren | Retry mit Backoff, nicht crashen |

> **Hinweis:** `dialout` hat auf DSM üblicherweise **GID 20**. Im Zweifel die numerische GID
> verwenden.

## 4. Compose

| Aspekt | Regel |
|---|---|
| `image:` (nicht `build:`) auf dem NAS | Images kommen aus der Registry |
| Projektname | `comfobox` |
| Gateway-Port | `127.0.0.1:8080:8080` → nur nginx ist der öffentliche Eintrittspunkt |
| UI-Port | `8081:80` |
| `env_file: .env` | Secrets getrennt vom Image |
| `restart: unless-stopped` | |
| Volumes | `comfobox-data:/var/lib/comfobox` (Schreibzähler!), `./logs:/var/log/comfobox` |

> **Kritisch:** Der persistierte 24-h-Schreibzähler liegt im Volume `comfobox-data`.
> `docker compose down -v` löscht ihn → das EEPROM-Schreiblimit beginnt neu. Deshalb:
> `down` **ohne** `-v` verwenden und das Volume im Backup einschließen.

## 5. Konfiguration & Secrets

| Punkt | Regel |
|---|---|
| Konfiguration | `appsettings.json` (Bind-Mount) + Env `COMFOBOX__*` |
| Passwörter | **niemals** im Image, **niemals** im Git → `.env` mit `chmod 600` |
| `.env` in `.gitignore` | `.env.example` mit Platzhaltern committen |
| `appsettings.override.json` | im **selben** Volume wie `/var/lib/comfobox` |
| API-Key | optional; UI nicht ins Internet exponieren |

````bash
# .env.example
MQTT_PASSWORD=changeme
API_KEY=changeme
````

## 6. nginx

| Regel | Begründung |
|---|---|
| `location /api/events` **ohne** `proxy_buffering` | sonst kommen SSE-Live-Werte erst beim Verbindungsende |
| `proxy_read_timeout 24h` | SSE-Verbindung soll nicht timeouten |
| `try_files $uri $uri/ /index.html` | SPA-Routing |
| `expires 1y` für `.js/.css/…` | Angular nutzt gehashte Dateinamen |
| `gzip off` für `/api/events` | SSE-Kompression puffert |

## 7. Build in CI

````yaml
- uses: docker/build-push-action@v6
  with:
    context: ./src/ComfoBox.Gateway
    file: ./src/ComfoBox.Gateway/Dockerfile
    platforms: linux/amd64
    push: true
    cache-from: type=gha,scope=gateway
    cache-to: type=gha,mode=max,scope=gateway
````

- **Getrennte Kontexte** pro Image (`context: ./src/ComfoBox.Gateway`) → kleiner Build-Kontext,
  schnellerer Cache
- `cache-from`/`cache-to` mit `type=gha` → GitHub Actions Cache
- Trivy-Scan **vor** dem Push (Fail on CRITICAL/HIGH)

## 8. Betrieb & Diagnose

````bash
docker compose ps
docker compose logs -f gateway
docker compose exec gateway wget -qO- http://localhost:8080/api/health
docker inspect --format '{{json .State.Health}}' comfobox-gateway
````

| Symptom | Ursache | Behebung |
|---|---|---|
| `port_not_open` | Gerät fehlt | `ls /dev/ttyUSB*` prüfen |
| `Access denied` | fehlende Gruppe | `group_add: dialout` |
| `Device or resource busy` | Host-Dienst belegt Port | `fuser -v /dev/ttyUSB0` |
| `no_iam` | falsche Baudrate | ComfoBox auf 38400 prüfen |
| `unhealthy` | `/api/health` antwortet nicht | Logs prüfen |
| HA-Entitäten `unavailable` | Broker nicht erreichbar | `COMFOBOX_Mqtt__Brokers__0__Host` prüfen |

## 9. Checkliste vor jedem Deployment

- [ ] `docker build` erfolgreich
- [ ] Trivy-Scan ohne CRITICAL/HIGH
- [ ] Image-Größe im Rahmen
- [ ] `devices:` zeigt auf `by-id`-Pfad
- [ ] `group_add` gesetzt
- [ ] `.env` vorhanden, `chmod 600`, nicht im Git
- [ ] Volume `comfobox-data` **nicht** gelöscht
- [ ] `compose.yaml` zeigt auf den richtigen Tag (nicht `latest` in Produktion)
- [ ] Backup von `comfobox-data` und `.env` vorhanden
