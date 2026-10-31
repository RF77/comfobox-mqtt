# 03 – Deployment

## 1. Zielbild

| Aspekt | Ziel |
|---|---|
| Verpackung | Docker-Image (OCI) aus GitHub Container Registry |
| Zielplattform | **Synology DS923+** (DSM 7.2, Container Manager) |
| Compose | `compose.yaml` mit 2 Services (`gateway`, `web`) + optional `mosquitto` |
| Serial-Port | `/dev/ttyUSB0` per `devices:` in den Container durchgereicht |
| Konfiguration | Datei (`appsettings.json`) + Umgebungsvariablen |
| Persistenz | Docker-Volume für Schreibzähler und Logs |
| Neustart | `restart: unless-stopped` + Healthcheck |
| Update | `docker compose pull && docker compose up -d` |

## 2. Dokumente

| Dokument | Inhalt |
|---|---|
| [containerisierung.md](containerisierung.md) | Dockerfile, Multi-Stage-Build, Compose, Image-Größe, Healthcheck |
| [synology-ds923.md](synology-ds923.md) | Container Manager, **Serial-Port in Docker**, udev, Betrieb |
| [konfiguration-und-secrets.md](konfiguration-und-secrets.md) | Env-Variablen, Bind-Mounts, Credentials, Backups |
| [ci-cd.md](ci-cd.md) | GitHub Actions: Build, Test, Image, Release |

## 3. Antwort auf die Kernfrage

> *„Docker Images können schon auch auf den Serial Port zugreifen?"*

**Ja.** Ein Container bekommt Zugriff auf einen seriellen Port, indem das
**Device-Node** (`/dev/ttyUSB0`) per `--device` bzw. `devices:` in den Container durchgereicht wird.
Der Kernel öffnet das Gerät dann im **Host**-Namespace – der Container sieht es als normales
Datei-Device.

Auf der **Synology DS923+** (DSM 7.2, Container Manager / Docker 24+):

| Weg | Machbar | Hinweise |
|---|---|---|
| `devices:` in `compose.yaml` | ✅ **empfohlen** | Volume-Name des USB-Geräts ermitteln, `/dev/serial/by-id/...` bevorzugen |
| `docker run --device=/dev/ttyUSB0 …` | ✅ | für Tests |
| „Pfadzuweisung" im DSM-UI | ✅ | Container Manager → Projekt → Erweiterung → Geräte |
| `--privileged` | ⚠️ vermeiden | Zu weitgehende Rechte |
| `privileged: true` + Port-Scan | ❌ | nicht nötig und unsicher |

**Voraussetzungen auf dem NAS:**

1. Der USB-RS485-Adapter muss **am Host** sichtbar sein (`/dev/ttyUSB0`).
2. Der Container-Prozess braucht Les-/Schreibrechte → `group_add: dialout` (bzw. `root`, siehe unten).
3. Der Adapter darf **nicht gleichzeitig** von einem Host-Prozess (z. B. `serial-getty`,
   `ModemManager`) beansprucht werden.
4. **Stabiler Pfad:** `/dev/serial/by-id/usb-…` statt `/dev/ttyUSB0` – sonst ändert sich der Name
   nach einem Reboot oder wenn der Adapter neu angesteckt wird.

> **Wichtiger Hinweis zur DS923+:** Die DS923+ hat keinen nativen RS485-Port – der USB-RS485-Adapter
> ist zwingend erforderlich (im Ist-Zustand ein CH340). Der Container braucht also den
> USB-Durchschliff des NAS; DSM 7.2 unterstützt das mit den üblichen
> USB-Serial-Treibern (CH340 ist im DSM-Paket enthalten).

Details und Schritt-für-Schritt-Anleitung: [synology-ds923.md](synology-ds923.md).

## 4. Container-Übersicht

| Container | Image | Ports | Volumes |
|---|---|---|---|
| `comfobox-gateway` | `ghcr.io/<user>/comfobox-gateway:<tag>` | `8080` → `8080` | `/var/lib/comfobox` (Schreibzähler), `/dev/ttyUSB0` |
| `comfobox-web` | `ghcr.io/<user>/comfobox-web:<tag>` | `8081` → `80` | – |
| `mosquitto` *(optional)* | `eclipse-mosquitto:2` | `1883` → `1883` | `./mosquitto/config`, `./mosquitto/data` |

> Der Broker läuft **optional** mit. Standard ist ein externer Broker (HA-Add-on oder ein
> Mosquitto auf einem anderen Host). Ein eigener Broker auf der DS923+ ist sinnvoll, wenn HA
> nicht auf dem NAS läuft.

## 5. Release- und Update-Prozess

````bash
# Auf der DS923+ (SSH)
cd /volume1/docker/comfobox
docker compose pull
docker compose up -d
docker compose logs -f gateway
````

| Schritt | Was passiert |
|---|---|
| 1 | `docker compose pull` holt neue Images (gleicher Tag oder `:latest`) |
| 2 | `docker compose up -d` erstellt die Container neu (bei Image-Änderung) |
| 3 | alter Container wird gestoppt → **LWT** → `ComfoBox/Status/Health = offline` in HA |
| 4 | neuer Container startet, verbindet sich, publiziert Discovery + `online` |
| 5 | HA-Entitäten sind nach ~5 s wieder `available` |

> Der Healthcheck (`HEALTHCHECK` im Dockerfile) verhindert, dass ein Container mit kaputtem
> Start als „läuft" gilt. Docker markiert ihn dann als `unhealthy` (er wird **nicht** automatisch
> neu gestartet – das ist gut, sonst Endlos-Neustarts).
