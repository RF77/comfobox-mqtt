# 03 – Deployment

## 1. Zielbild

| Aspekt | Ziel |
|---|---|
| Verpackung | Docker-Image (OCI) aus GitHub Container Registry + **Synology-Paket (SPK)** |
| Zielplattform | **Synology DS923+** (DSM 7.2.1+, Container Manager) |
| Installation | *„Installieren"* im DSM-Paketcenter → Browser öffnet sich → **Getting-Started-Assistent** |
| Container | **ein** Container `gateway` (ASP.NET Core **inkl.** Angular-UI) → [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md) |
| Compose | `compose.yaml` mit **1** Service (`gateway`) + optional `mosquitto` |
| Serial-Port | zur Laufzeit gerendert (`devices:`) aus der Portwahl im Assistenten → [ADR-008](../05-entscheidungen/adr-008-synology-spk.md) |
| Konfiguration | `config.json` (Modus 0600) + Umgebungsvariablen als Override |
| Persistenz | Verzeichnis `/var/lib/comfobox` (Schreibzähler, Konfiguration) |
| Neustart | `restart: unless-stopped` + Healthcheck |
| Update | `docker compose pull && docker compose up -d` **oder** SPK-Upgrade im DSM |

## 2. Dokumente

| Dokument | Inhalt |
|---|---|
| [synology-paketcenter.md](synology-paketcenter.md) | **SPK-Paket, Installation per Klick, Getting-Started-Assistent** |
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

| Container | Image | Ports | Volumes / Devices | optional |
|---|---|---|---|---|
| `gateway` | `ghcr.io/<user>/comfobox-gateway:<tag>` | `8081` → `8080` | `/var/lib/comfobox`, `/dev/ttyUSB*` (gerendert) | nein |
| `mosquitto` | `eclipse-mosquitto:2` | `1883` → `1883` | `./mosquitto/config`, `./mosquitto/data` | ja |

> **Ein Container** enthält Gateway **und** Web-UI (→ [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md)).
> Es gibt keinen zweiten nginx-Container – ASP.NET Core liefert die SPA statisch aus.
>
> Der Broker läuft **optional** mit. Standard ist ein externer Broker (HA-Add-on oder ein
> Mosquitto auf einem anderen Host). Ein eigener Broker auf der DS923+ ist sinnvoll, wenn HA
> nicht auf dem NAS läuft.

### 4.1 Zwei Betriebsarten

| Betriebsart | Einstieg | Für wen |
|---|---|---|
| **SPK** (empfohlen) | DSM-Paketcenter → *Installieren* → Assistent | Normalfall |
| **Manuelles Compose** | `docker compose up -d` + Hand-Konfiguration | Container-Enthusiasten, andere Hosts (Pi, Linux-Server) |

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
