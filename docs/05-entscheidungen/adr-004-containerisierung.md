# ADR-004: Betrieb als Container-Image (Synology DS923+)

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Betrieb, Deployment, Hardware |

> **Nachtrag (2026-10-31):** Die Entscheidung „Container-Image" bleibt unverändert.
> **Zwei Details wurden später präzisiert:**
> - Die *Zwei-Image-Aufteilung* ist überholt → **ein** Image für Gateway + UI
>   ([ADR-007](adr-007-ein-container-image.md)).
> - Die *Installation* erfolgt zusätzlich als **Synology-Paket (SPK)** mit
>   Getting-Started-Assistenten ([ADR-008](adr-008-synology-spk.md)).
> - Der Serielle Port wird **zur Laufzeit gerendert** (Portwahl im Assistenten), nicht
>   statisch in der Compose-Datei.

## Kontext

Anforderung 3: *„Alles als Docker Image, damit ich es auf meiner DS 923+ deployen kann.
Docker Images können schon auch auf den Serial Port zugreifen?"*

Der Ist-Zustand kennt nur:

| Variante | Problem |
|---|---|
| Mono auf Raspberry Pi | manuelle Installation, kein Autostart, kein systemd, kein Update-Mechanismus |
| Windows-Service + WiX-MSI | Build-Broken (fehlende `HexMaster.ServiceHelper.dll`), Windows-only |
| `Copy*.cmd` auf einen Netzwerk-Share | fest verdrahtete Pfade, kein Konfig-Deployment, kein Rollback |

Zielhardware: **Synology DS923+** (AMD Ryzen R1600, x86_64, 4 GB RAM, DSM 7.2 mit
Container Manager/Docker 24+).

**Antwort auf die Kernfrage:** Ja – Container können auf den Serial Port zugreifen. Das
Device-Node (`/dev/ttyUSB0`) wird per `devices:` in den Container durchgereicht; der Kernel
öffnet es im Host-Namespace.

## Entscheidung

**Der gesamte Dienst wird als OCI-Container-Image ausgeliefert und auf der DS923+ via
Docker Compose betrieben.**

### Image-Aufteilung

> ⚠️ **Überholt durch [ADR-007](adr-007-ein-container-image.md)** – es gibt jetzt **ein** Image
> für Gateway **und** UI. Die folgende Tabelle beschreibt den ursprünglichen Stand.

| Image | Inhalt | Port |
|---|---|---|
| `comfobox-gateway` | .NET 10 ASP.NET Core (multi-stage, `alpine`, `tini`) | `127.0.0.1:8080` |
| `comfobox-web` | nginx + statisches Angular-Bundle | `8081:80` |
| `mosquitto` *(optional)* | MQTT-Broker | `1883` |

→ ursprünglich **zwei getrennte Images** statt eines mit zwei Prozessen. Heute: **ein** Image
(Begründung in [ADR-007](adr-007-ein-container-image.md) und
[containerisierung §1](../03-deployment/containerisierung.md#1-ein-image-statt-zwei)).

### Serial-Port

````yaml
services:
  gateway:
    devices:
      - "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0:/dev/ttyUSB0"
    group_add:
      - "dialout"
````

- `by-id`-Pfad ist reboot-stabil (`ttyUSB0` kann sich ändern)
- `group_add` gibt Zugriff auf das Device-Node ohne Root im Container
- **kein** `privileged: true`, **kein** Docker-Socket

### Konfiguration & Persistenz

| Aspekt | Umsetzung |
|---|---|
| Konfiguration | `appsettings.json` (Bind-Mount) + Env-Variablen (`COMFOBOX__*`) |
| Secrets | `.env` mit `chmod 600` auf dem NAS |
| Persistenz | Docker-Volume `comfobox-data` für den 24-h-Schreibzähler |
| Logs | stdout (JSON) + `/var/log/comfobox` (Bind-Mount) |
| Neustart | `restart: unless-stopped` + `HEALTHCHECK` |
| Updates | `docker compose pull && docker compose up -d` |

### Image-Größe

alpine statt debian (~230 MB statt ~450 MB). **Kein** Trimming/AOT, weil
`System.Text.Json` Reflection nutzt – Robustheit ist wichtiger als 40 MB.

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **Docker + Compose auf der DS923+** ✅ | Anforderung, Passt zur Hardware, reproduzierbar | – |
| **systemd-Unit auf dem NAS** (ohne Docker) | kein Build nötig | Build-Artefakt (`.NET`) auf dem NAS zu betreiben ist aufwendiger; kein isoliertes Deployment; .NET-Runtime-Installation auf DSM ist unüblich |
| **Container Manager GUI-Projekt** (ohne `compose.yaml`) | einsteigerfreundlich | `compose.yaml` wird nicht versioniert → Deployments nicht reproduzierbar |
| **Ein Image mit Gateway + nginx** | ein Deployment-Objekt | Zwei-Prozess-Supervision ohne init-System; Signal-Forwarding und Restart-Semantik sind fehleranfällig. **Heute obsolet:** es gibt weder nginx noch zwei Prozesse, sondern ein Image mit **einem** Prozess (→ [ADR-007](adr-007-ein-container-image.md)) |
| **Deployment auf dem Raspberry Pi** | Bekanntes Terrain | Anforderung nennt die DS923+; ein Container läuft auf beiden – die DS923+ ist aber die Zielhardware |
| **Bare-Metal .NET auf Windows** (Status quo +) | kein Docker nötig | Windows-Server ist nicht die Zielhardware; kein Linux |
| **Bau des Images auf dem NAS selbst** | kein Registry nötig | Sehr langsame Builds auf dem NAS; ARM/x86-Architektur-Problem bei arm64; CI auf GitHub Actions ist schneller |

## Konsequenzen

### Positiv

- **Reproduzierbares Deployment:** ein `docker compose pull && docker compose up -d`
- **Rollback** auf die vorige Version per Tag
- **Isolation:** der Container sieht nur den seriellen Port und die gemappten Verzeichnisse
- **Kein Root im Container**, `read_only` optional
- Image ist plattformunabhängig baubar → auf einem anderen NAS/PC identisch lauffähig
- `HEALTHCHECK` macht den Zustand in der Container-UI sichtbar
- CI baut, testet, scannt und publiziert automatisch

### Negativ / Aufwand

- **Serial-Port-Durchreichung ist die kritischste Stelle.** Fehlerquellen: udev-Regel,
  `dialout`-GID auf DSM, Belegung durch Host-Dienste, späte USB-Enumeration nach NAS-Reboot
  → Mitigation: eigener Prototyp in Phase 0/1, `by-id`-Pfad, Retry mit Backoff, klarer
  Health-Zustand bei Portfehlern
- Zusätzliche Technologie-Schicht (Docker) im Projekt
- Zwei Images → zwei Release-Artefakte *(heute: ein Image → ein Artefakt, siehe [ADR-007](adr-007-ein-container-image.md))*
- `mosquitto` im Compose ist **optional**; Standard ist ein externer Broker → die
  `compose.yaml` muss je nach Installation angepasst werden (per `.env` steuerbar)
- Debugging ist schwieriger (Logs über `docker compose logs` statt direkt auf der Konsole)
- **Rollback-Pflicht:** Container löschen → Volume kann den Schreibzähler verlieren
  → Backup-Routinen dokumentiert

### Neutral

- Die DS923+ ist x86_64 → nur `linux/amd64` wird gebaut (kein arm64)
- Der Broker läuft **nicht** zwingend im selben Compose-Projekt

## Verwandte Dokumente

- [containerisierung](../03-deployment/containerisierung.md)
- [synology-ds923](../03-deployment/synology-ds923.md)
- [konfiguration-und-secrets](../03-deployment/konfiguration-und-secrets.md)
- [ci-cd](../03-deployment/ci-cd.md)
- [ADR-001](adr-001-dotnet-version.md)
