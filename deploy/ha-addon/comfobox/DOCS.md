# ComfoBox Gateway – Add-on-Dokumentation

## Installation

1. In Home Assistant: **Einstellungen → Add-ons → Add-on-Store**
2. Menü (⋮) oben rechts → **Repositories**
3. Repository-URL eintragen:
   `https://github.com/RF77/comfobox-mqtt/tree/main/deploy/ha-addon`
   → **Hinzufügen**
4. **ComfoBox Gateway** suchen → **Installieren**
5. **Starten**

> Voraussetzung: Home Assistant auf **HA OS** oder **Supervised**.

## Ersteinrichtung

| Schritt | Wo | Was |
|---|---|---|
| 1 | Add-on-Seite → **Konfiguration** | **Serieller Port** aus dem Dropdown wählen |
| 2 | Add-on-Seite → **Konfiguration** | **Broker** eintragen (optional – ohne Broker läuft das Gateway ohne MQTT) |
| 3 | **Web UI** (Panel in HA) | Getting-Started-Assistent durchlaufen: BACnet-Test, Benutzer + Passwort |
| 4 | **Web UI** → Konfiguration | API-Token für die HA-Integration erzeugen |

> **Wichtig:** Die ComfoBox muss auf **38400 Baud** umgestellt und **neu gestartet** sein.
> Steht sie noch auf dem Werks-Default 76800, antwortet sie nicht.

## Der serielle Port

Der Supervisor zeigt im Dropdown alle **seriellen** Geräte des Hosts an – bevorzugt mit
stabilen `/dev/serial/by-id/…`-Pfaden. Diese Pfade überleben Neustarts und das Umstecken
des Adapters.

| Problem | Lösung |
|---|---|
| Adapter wird nicht angezeigt | USB-Adapter an einen anderen Port stecken; Seite neu laden |
| Falscher Port gewählt | Konfiguration ändern → Add-on neu starten |
| „Permission denied" | Group-Rechte des Adapters prüfen (CH340 → `dialout`) |

## Broker

| Variante | Einstellung |
|---|---|
| **HA-eigener Mosquitto** | `broker_host: core-mosquitto`, `broker_port: 1883` |
| **Externer Broker** | `broker_host: <IP>`, `broker_port: 1883`, Username/Passwort |
| **Kein Broker** | `broker_host: ""` → das Gateway läuft ohne MQTT; die Werte sind nur über die HA-Integration erreichbar |

## Verbindung aus Home Assistant

Nach der Installation:

1. Web-UI öffnen → **Konfiguration** → **API-Token** → Token erzeugen (wird **einmal** angezeigt)
2. In HA: **Einstellungen → Geräte & Dienste → Integration hinzufügen → ComfoBox**
3. `http://<HA-IP>:8099` als Adresse und den Token als Passwort eintragen

> Das Add-on stellt den Port **8099** bereit. Der Ingress-Pfad (Panel in HA) ist **nicht**
> für die Integration geeignet – er enthält ein rotierendes Token.

## Ports

| Port | Zweck |
|---|---|
| `8099` (Host) | REST/SSE – Adresse für die ComfoBox-HA-Integration |
| Panel in HA | Web-UI über Ingress (kein zusätzlicher Port) |

## Backups

Das Add-on wird **hot** gesichert. Enthalten sind:

- `/data` → `config.json`, `writes.json` (**Schreibzähler!**), Logs
- `/config` → benutzereigene Konfigurationsdateien

> **Wichtig:** Der 24-h-Schreibzähler muss in jedem Backup enthalten sein. Ohne ihn
> beginnt der EEPROM-Schutz der ComfoBox neu.

## Problembehebung

| Symptom | Ursache | Lösung |
|---|---|---|
| Add-on startet nicht | Port belegt | `ports:` anpassen → Neustart |
| `no_iam` im Log | ComfoBox auf 76800 Baud | auf 38400 umstellen + neu starten |
| Kein Broker | `broker_host` leer | Broker eintragen oder bewusst ohne Broker betreiben |
| Entitäten `unavailable` | Gateway nicht erreichbar | Port 8099 in der Firewall freigeben |
| Watchdog-Neustarts | `/api/health` antwortet nicht | Logs im Add-on ansehen |

## Quellen

- [Dokumentation](../../../docs/02-modernisierung/ha-integration.md)
- [Installationsstufen](../../../docs/03-deployment/installationsstufen.md)
- [ADR-009](../../../docs/05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md)
