---
description: "Use when writing Dockerfiles, docker-compose files, Synology SPK packages, or deployment configuration for this repository — building images, running on Synology DS923+, serial device passthrough, healthchecks, volumes, or secrets handling."
applyTo: ["**/Dockerfile", "**/docker-compose*.y*ml", "**/compose*.y*ml", "**/.dockerignore", "deploy/synology-spk/**", "**/INFO", "**/scripts/**"]
---

# Docker & Deployment – Regeln

## 1. Zielplattform

**Synology DS923+** (AMD Ryzen R1600, x86_64, 4 GB RAM, DSM 7.2.1+, Container Manager / Docker 24+)
**und** Home Assistant (HA OS / Supervised → Raspberry Pi 4/5, HA Green, VMs).

⇒ **Multi-Arch**: `linux/amd64` **und** `linux/arm64`.
Das HA-Add-on auf einem Raspberry Pi 5 braucht `arm64` – ein reines `amd64`-Image
liefert dort einen `exec format error`.
(→ [ADR-009](../../docs/05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md#e2--ha-add-on-f2))

| Plattform | Architektur |
|---|---|
| Synology DS923+ | `linux/amd64` |
| HA Green, Intel-NUC, VMs | `linux/amd64` |
| Raspberry Pi 4/5 | `linux/arm64` |

> **Primärer Installationsweg:** das Synology-Paket (SPK) in `deploy/synology-spk/`
> → [ADR-008](../../docs/05-entscheidungen/adr-008-synology-spk.md).
> Zweiter Weg: das HA-Add-on in `deploy/ha-addon/`.
> Manuelles Compose bleibt als Alternative für andere Hosts (Pi, Linux-Server).

## 2. Images

| Image | Base | Inhalt | Zielgröße |
|---|---|---|---|
| `comfobox-gateway` | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` | .NET-Gateway **+ Angular-UI** (ein Image!) | ~250 MB |
| `mosquitto` (optional) | `eclipse-mosquitto:2` | Broker | ~25 MB |

> **Ein Image für Gateway und UI** → [ADR-007](../../docs/05-entscheidungen/adr-007-ein-container-image.md).
> Es gibt **keinen** `comfobox-web`-Container und **keine** nginx-Konfiguration.

**Regeln:**

- **Multi-Stage-Build**: `node` (Angular) → `dotnet/sdk` (.NET) → `dotnet/aspnet` (Laufzeit)
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

### 2.1 Statische Auslieferung aus dem .NET-Host

Die SPA liegt unter `wwwroot/` und wird von ASP.NET Core ausgeliefert:

````csharp
app.UseDefaultFiles();          // → index.html
app.UseStaticFiles();           // → Assets
app.MapFallbackToFile("index.html");  // SPA-Routing (NACH den API-Routen!)
````

| Punkt | Regel |
|---|---|
| Reihenfolge | `MapFallbackToFile` **nach** allen `MapGet`/`MapPut` → sonst schluckt es `/api/...` |
| Kompression | `AddResponseCompression()` mit Brotli/Gzip – der nginx-Pfad existiert nicht mehr |
| Assets cachen | `UseStaticFiles` + `Cache-Control` per Middleware (Angular nutzt gehashte Namen) |

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

> **Im SPK-Betrieb ist der `devices:`-Block dynamisch:** Der Supervisor rendert ihn aus der
> Portwahl im Getting-Started-Assistenten. Die Compose-Datei im Repo ist ein Template mit
> Platzhaltern (`${SERIAL_DEVICES}`), **keine** direkt lauffähige Datei.

## 4. Compose

| Aspekt | Regel |
|---|---|
| `image:` (nicht `build:`) auf dem NAS | Images kommen aus der Registry |
| Projektname | `comfobox` |
| **Ein** Service `gateway` | Gateway + UI in einem Container |
| Gateway-Port | `8081:8080` (8081 ist der Port, den das SPK im DSM öffnet) |
| `env_file: .env` | Secrets getrennt vom Image |
| `restart: unless-stopped` | |
| Volumes | `comfobox-data:/var/lib/comfobox` (Schreibzähler!), `./logs:/var/log/comfobox` |

> **Kritisch:** Der persistierte 24-h-Schreibzähler liegt im Volume `comfobox-data`.
> `docker compose down -v` löscht ihn → das EEPROM-Schreiblimit beginnt neu. Deshalb:
> `down` **ohne** `-v` verwenden und das Volume im Backup einschließen.

## 5. Konfiguration & Secrets

| Punkt | Regel |
|---|---|
| Konfiguration | `config.json` im Datenverzeichnis (Modus 0600) + Env `COMFOBOX__*` als **Override** |
| Präzedenz | **`config.json` schlägt Env-Variablen** – Passwörter sollen nicht in `docker inspect` landen |
| Passwörter | **niemals** im Image, **niemals** im Git → `config.json` (0600) bzw. `.env` mit `chmod 600` |
| `.env` in `.gitignore` | `.env.example` mit Platzhaltern committen |
| API-Key | optional; UI nicht ins Internet exponieren |

> **Details:** [Konfiguration & Secrets](../../docs/03-deployment/konfiguration-und-secrets.md),
> [API §7.1](../../docs/02-modernisierung/datenmodell-und-api.md#71-konfigurationsquellen-und-präzedenz)

## 6. Synology-Paket (SPK)

Regeln für `deploy/synology-spk/`:

### 6.1 `INFO`

| Feld | Wert | Begründung |
|---|---|---|
| `package` | `ComfoBoxGateway` | keine Punkte, camelcase |
| `arch` | `noarch` | Image ist `linux/amd64`; SPK ist plattformneutral |
| `os_min_ver` | `7.2-63134` | DSM-Version, ab der Container Manager + SPK-Funktionen verfügbar sind |
| `install_dep_packages` | `ContainerManager>=1432` | erzwingt Container Manager als Abhängigkeit |
| `start_dep_services` | `ContainerManager` | Container Manager muss laufen |
| `adminprotocol` / `adminport` / `adminurl` | `http` / `8081` / `/` | **öffnet den Browser nach der Installation** |
| `checkport` | `yes` | Portkonflikt wird vor der Installation geprüft |
| `precheckstartstop` | `yes` | Vorprüfung beim Start |
| `silent_install` | `no` | der Nutzer **soll** sehen, was passiert |
| `silent_upgrade` / `silent_uninstall` | `yes` | Routine |
| `thirdparty` | `yes` | erscheint als Drittanbieter-Paket |
| `ctl_stop` / `ctl_uninstall` | `yes` | „Stoppen"/„Deinstallieren" im DSM |
| `install_type` | `system` | nicht als „App installieren" |

> `checksum` wird beim Build **automatisch** in die INFO geschrieben (MD5 über `package.tgz`).

### 6.2 Lifecycle-Skripte

`scripts/` enthält: `preinst`, `postinst`, `preuninst`, `postuninst`, `start-stop-status`.

| Skript | Aufgabe | Seiteneffekte |
|---|---|---|
| `preinst` | Voraussetzungen prüfen (DSM-Version, Container Manager, freier Port) | **keine** |
| `postinst` | Verzeichnisse anlegen, udev-Regel + systemd-Unit installieren, erste Port-Erkennung, Default-`config.json` schreiben, Compose rendern, Container starten | ja |
| `preuninst` | Container stoppen | ja |
| `postuninst` | Container entfernen, systemd-Unit + udev-Regel entfernen | ja, **aber `var/` bleibt** (Schreibzähler!) |
| `start-stop-status` | `start` / `stop` / `status` / `prestart` / `prestop` / `log` | ja |

**Exit-Codes** (DSM-Konvention):

| Code | Bedeutung |
|---|---|
| `0` | Erfolg |
| `1` | Unbekannter Fehler |
| `2` | Falsche Parameter / Aufruf |
| `3` | Paket ist nicht „gestartet" (Status-Abfrage) |
| `4` | Abhängigkeit nicht erfüllt (Container Manager fehlt) |
| `150` | „Sollzustand ist Stop" (Status-Abfrage bei gestopptem Paket) |

> `status` muss `0` liefern, wenn der Container läuft, und `150`, wenn er gestoppt ist –
> nur so zeigt das DSM-Paketcenter den richtigen Status.

### 6.3 Supervisor

| Regel | Begründung |
|---|---|
| Der Supervisor läuft **auf dem Host**, nicht im Container | der `devices:`-Block hängt vom gewählten Port ab |
| **Kein** Docker-Socket im Container | Sicherheit; der Container braucht ihn nicht |
| Compose wird aus `compose.template.yaml` **gerendert** | Platzhalter `${WEB_PORT} ${SERIAL_DEVICES} ${DIALOUT_GID} …` |
| Watch-Loop prüft `config.json` + `ports.json` (MD5) | Änderung → `compose up -d` (Container wird ersetzt) |
| `passAllSerialDevices` | Standard **an** → der Verbindungstest im Assistenten funktioniert vor der endgültigen Portwahl |
| Shell-Skripte mit `bash -n` prüfen | `deploy/synology-spk/tools/check-shell.sh` |

## 7. Build in CI

````yaml
- uses: docker/build-push-action@v6
  with:
    context: .
    file: ./src/ComfoBox.Gateway/Dockerfile
    platforms: linux/amd64,linux/arm64
    push: true
    cache-from: type=gha,scope=gateway
    cache-to: type=gha,mode=max,scope=gateway
````

- **Ein** Build-Kontext (`.`) → der Dockerfile braucht das Angular-Ergebnis
- **Multi-Arch** `linux/amd64,linux/arm64` → Pflicht für das HA-Add-on
- `cache-from`/`cache-to` mit `type=gha` → GitHub Actions Cache
- Trivy-Scan **vor** dem Push (Fail on CRITICAL/HIGH)
- Anschließend: SPK bauen (`deploy/synology-spk/build-spk.sh`) → GitHub Release

## 7a. Home-Assistant-Add-on

Manifest in `deploy/ha-addon/comfobox/config.yaml`.

| Schlüssel | Wert | Begründung |
|---|---|---|
| `image` | `ghcr.io/rf77/comfobox-gateway` | Multi-Arch-Image; der Supervisor zieht den Tag `= version` |
| `version` | muss dem Image-Tag entsprechen | sonst findet der Supervisor das Image nicht |
| `init` | `false` | das Image bringt tini mit – S6/S6-overlay würde sonst nicht starten |
| `arch` | `aarch64`, `amd64` | Pi 5 und HA Green |
| `device` (Option) | `device(subsystem=tty)` | **Dropdown** der seriellen Ports mit stabilen `by-id`-Pfaden |
| `ports` | `8080/tcp: 8099` | Adresse für die HA-Integration (Ingress ist dafür ungeeignet) |
| `ingress` | `true` | Web-UI als Panel in HA |
| `ingress_port` | `8080` | Container-Port der UI |
| `watchdog` | `http://[HOST]:[PORT:8080]/api/health` | Neustart bei Hänger |
| `services` | `mqtt:want` | interner Mosquitto optional |
| `map` | `app_config`, `data` | Konfiguration + persistenter Schreibzähler |

> **Wichtig:** Der Supervisor schreibt die Optionen nach `/data/options.json`.
> Der Gateway liest diese Datei optional → `AddOnOptionsProvider.cs`.
> **Kein** `uart: true` – der Nutzer wählt genau **einen** Port (schmalste Rechte).

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
| SPK-Status „gestoppt" | `status` liefert nicht 0/150 | `synopkg status ComfoBoxGateway` prüfen |
| Container startet nicht | `devices:` zeigt auf falschen Pfad | `compose.yaml` im `var`-Verzeichnis prüfen |

## 9. Checkliste vor jedem Deployment

- [ ] `docker build` erfolgreich
- [ ] Trivy-Scan ohne CRITICAL/HIGH
- [ ] Image-Größe im Rahmen
- [ ] `devices:` zeigt auf `by-id`-Pfad
- [ ] `group_add` gesetzt
- [ ] `config.json` vorhanden, Modus 0600, nicht im Git
- [ ] Volume `comfobox-data` **nicht** gelöscht
- [ ] `compose.yaml` zeigt auf den richtigen Tag (nicht `latest` in Produktion)
- [ ] Backup von `comfobox-data` und `config.json` vorhanden
- [ ] *(nur SPK)* `bash -n` über alle Skripte in `deploy/synology-spk/` grün
- [ ] *(nur SPK)* `INFO` enthält `checksum` und `adminport`
- [ ] *(nur SPK)* `postuninst` lässt das `var`-Verzeichnis stehen
