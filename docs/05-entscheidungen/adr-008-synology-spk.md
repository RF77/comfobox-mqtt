# ADR-008: Installation als Synology-Paket (SPK) mit Getting-Started-Assistenten

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Betrieb, Deployment, Installation, Frontend |

## Kontext

Der Nutzer betreibt eine **Synology DS923+** und möchte die Anwendung so bequem wie
möglich installieren:

> *„Ist es möglich, dass ich das als Third Party App direkt in der DS 923+ sehe und einfach
> installieren klicken kann? Ich möchte dann, dass eine Art ‚Getting Started' losgeht.
> Dort soll mal die verfügbaren Serial Ports sehen und auswählen können, dann noch User und
> Passwort für die Config-Seite später usw. Es soll möglichst bequem sein. Ob ich 1 oder 2
> Container habe, spielt weniger eine Rolle, so lange es einfach und bequem zum installieren ist."*

Kernproblem: **Der serielle Port ist erst nach der Installation bekannt.** Er hängt davon ab,
welcher USB-RS485-Adapter an welchem USB-Port steckt. Das ist ein Chicken-and-Egg-Problem:

- Die Installation darf **nicht** erfragen, wo der Port ist (das wäre unbequem)
- Der Container braucht aber `devices:` mit dem richtigen Pfad
- Der Adapter kann jederzeit an einem anderen Port stecken

## Entscheidung

Wir liefern ein **Synology-Paket (SPK)** aus, das:

1. **im DSM-Paketcenter als Third-Party-App** erscheint → *„Installieren"* klicken
2. **keine** Parameter beim Installieren abfragt (kein WIZARD) → Installation in unter einer Minute
3. nach der Installation **automatisch den Browser öffnet** (`adminport` + `adminurl` + `adminprotocol`)
4. einen **Getting-Started-Assistenten** startet, der den Nutzer durch sechs Schritte führt
5. seine Konfiguration **serverseitig** schreibt (kein Neustart durch den Nutzer nötig,
   außer bei Serial-Port- oder Broker-Änderung)

### Bausteine

| Baustein | Entscheidung | Begründung |
|---|---|---|
| Paketformat | **SPK** (`package.tgz` + `INFO` + `scripts/`) | Offizielles DSM-Format für Third-Party-Pakete |
| Container-Orchestrierung | **eigener Supervisor** (`supervisor.sh`) auf dem Host | Der `devices:`-Block hängt vom gewählten Port ab → muss zur Laufzeit gerendert werden |
| Image | **ein** Image für Gateway + UI | → [ADR-007](adr-007-ein-container-image.md) |
| Container-Manager-Abhängigkeit | `install_dep_packages="ContainerManager>=1432"` | DSM 7.2.1+ |
| Serial-Port-Erkennung | udev-Regel + `/dev/serial/by-id/`-Symlink + `serial-scan.sh` | stabiler Pfad über Neustarts hinweg |
| Konfiguration | `config.json` im `var`-Verzeichnis, Modus 0600 | Passwörter nicht in Env-Variablen |
| Benutzer-Passwort | **PBKDF2-HMAC-SHA256**, 600.000 Iterationen, serverseitig | → [ADR-005](adr-005-api-stil.md) |
| Assistent | **6 Schritte** in der Angular-UI | → [ui-angular-optimus §3](../02-modernisierung/ui-angular-optimus.md#3-getting-started-setup-assistent) |
| Port-Änderung | Supervisor rendert `compose.yaml` neu und ersetzt den Container | ≈10 s Unterbrechung, wird angekündigt |

### Die sechs Schritte des Assistenten

| # | Schritt | Inhalt |
|---|---|---|
| 0 | Willkommen | Erklärung, Voraussetzung „ComfoBox auf 38400 Baud" |
| 1 | Schnittstelle | Liste der seriellen Ports, Auswahl, „alle durchreichen" |
| 2 | BACnet | Baudrate, Geräte-ID, **Verbindungstest** |
| 3 | MQTT | Broker, Port, User/Pass, TLS, Discovery, **Verbindungstest** |
| 4 | Web-Zugang | Benutzername, Passwort (≥ 12 Zeichen), Web-Port-Vorschlag |
| 5 | Fertig | Zusammenfassung, Übernehmen, Neustart beobachten |

### Die Supervisor-Architektur

```
DSM-Paketcenter          Container Manager              Gateway
      │                        │                           │
      │  start-stop-status     │                           │
      ├───────────────────────►│                           │
      │                        │                           │
      │                   supervisor.sh (Host)               │
      │                    ├─► serial-scan.sh → ports.json │
      │                    ├─► render  → compose.yaml     │
      │                    └─► compose up -d ─────────────►│
      │                                                 BACnet (RS485)
      │                                                 MQTT
```

**Warum ein eigener Supervisor statt Docker Project Worker?**

Der DSM **Docker Project Worker** (7.2.1+) ist die offizielle Empfehlung für Compose-Projekte
aus der DSM-Oberfläche. Er hätte aber:

- **kein** `install_dep_packages`-Mechanismus für die Installation des Pakets selbst
- **keine** garantierte Reihenfolge (SPK → Container-Start) mit unserem Startskript
- **keine** Möglichkeit, den `devices:`-Block nach der Port-Wahl neu zu rendern, ohne
  das Projekt im DSM-UI zu bearbeiten (das der Nutzer tun müsste → **unbequem**)
- ein zusätzliches Konzept (YAML-Stack), das der Nutzer verstehen muss

Der eigene Supervisor ist ~200 Zeilen Shell, rendert `compose.yaml` aus einer Vorlage und
reagiert automatisch auf Änderungen an `config.json`/`ports.json`. Der Nutzer muss **nichts**
in der DSM-Container-Oberfläche tun.

### Kein Docker-Socket im Container

Der Container bekommt **keinen** Zugriff auf `/var/run/docker.sock`. Grund:

- Das ist eine **Sicherheitslücke**: ein kompromittierter Container könnte den Host
  containerisieren (privilege escalation)
- Die Anwendung braucht ihn nicht – sie spricht mit BACnet und MQTT, nicht mit Docker
- Der Supervisor auf dem Host erledigt alle Container-Operationen

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **SPK + eigener Supervisor + Assistent** ✅ | 1 Klick, kein WIZARD, Browser öffnet automatisch, Portwahl im Assistenten, alles automatisch | – |
| SPK + **WIZARD** (Installations-Assistent im DSM) | native DSM-Optik | Der Nutzer müsste die Parameter **vor** der Installation eingeben – genau das, was er nicht wollte; außerdem ist der WIZARD auf eine Seite begrenzt und kann die Port-Liste nicht dynamisch anzeigen |
| **Docker Project Worker** (DSM-UI-Projekt) | offiziell, YAML-basiert | kein `install_dep_packages`, keine automatische Port-Übernahme, Extra-Konzept für den Nutzer |
| **Manuelles Compose** (Nutzer kopiert `docker-compose.yml`) | maximale Flexibilität, kein Paket | nicht „bequem", kein GUI, Nutzer muss Pfade/Berechtigungen selbst herausfinden |
| **Nur `docker run`-Anleitung** | simpel | nicht „bequem", falscher Zielzustand |
| **Zwei Container** (Gateway + UI) | Trennung | → [ADR-007](adr-007-ein-container-image.md): ein Image reicht, ein Container ist einfacher zu verwalten |
| **Konfiguration über Env-Variablen** | 12-factor | Passwörter landen in `docker inspect`; kein bequemer Assistent |
| **Windows-Service / Mono** (Status quo) | läuft heute | nur auf Windows bzw. Mono auf dem Pi – nicht auf der DS923+ |
| **Paket als „Application" im DSM** (proprietäres Paketformat) | mehr DSM-Integration |proprietäres Format, nicht verfügbar für Drittanbieter |

## Konsequenzen

### Positiv

- **Ein Klick** im Paketzcenter → Browser öffnet sich → Assistent startet
- Keine Parameter bei der Installation, kein WIZARD, kein Docker- und kein YAML-Wissen nötig
- Der Assistent ist **wiederholbar** (Serial-Port oder Broker später wechseln)
- Der Nutzer sieht **erklärt**, was zu tun ist (38400 Baud!) – Fehler werden kontextbezogen
  gemeldet statt als „HTTP 500"
- Passwörter liegen nur in `config.json` (0600), nicht in Env-Variablen
- Ein einziges Artefakt (`*.spk`) für die Verteilung – auch abseits der eigenen NAS

### Negativ / Aufwand

- **≈13 PT** zusätzlicher Aufwand (SPK-Skelett, Supervisor, Assistent, Setup-API)
- Das SPK muss bei **jedem** DSM-Update weitergepflegt werden (API-Kompatibilität)
- Signierte Pakete sind für Drittanbieter ein Problem: Synology signiert nur eigene Pakete;
  Community-Pakete erscheinen unter „Installieren → Manuelle Installation → Herkunft: Andere"
  mit einer Warnung
- Der eigene Supervisor ist Wartungscode (Shell) – er muss robust sein, sonst startet
  der Container nach einem DSM-Update nicht mehr
- Kein Docker-Socket im Container heißt: kein `docker exec` von Hand in den Container
  (Debugging muss über Logs + `/api/diagnostics` laufen)

### Neutral

- Der Container läuft mit `group_add: dialout` → braucht den Serial-Port
- Der Container wird als **eigener** Container in der DSM-Container-UI angezeigt –
  das ist nützlich (Logs, Neustart, Stoppen) und kein Nachteil
- Ein **eigener** MQTT-Broker (Mosquitto) bleibt optional und ist ein **zweiter** Container,
  wenn der Nutzer keinen Broker im Docker-Netzwerk hat

## Verwandte Dokumente

- [synology-paketcenter](../03-deployment/synology-paketcenter.md) – die vollständige Spezifikation
- [ui-angular-optimus §3](../02-modernisierung/ui-angular-optimus.md#3-getting-started-setup-assistent)
- [datenmodell-und-api §3.4](../02-modernisierung/datenmodell-und-api.md#34-setup-endpunkte-getting-started)
- [ADR-004](adr-004-containerisierung.md) · [ADR-007](adr-007-ein-container-image.md)
- `deploy/synology-spk/` – die Umsetzung
