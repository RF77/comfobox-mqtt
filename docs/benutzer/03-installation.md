---
title: Installation
description: Die vier Möglichkeiten, das Gateway zu installieren
order: 30
icon: pi pi-download
tags: [installation]
---

# Installation

Es gibt **vier** Wege. Alle verwenden dieselbe Software – Sie wählen nur den
Weg, der zu Ihrer Umgebung passt.

| Weg | Passend für | Aufwand |
|---|---|---|
| [Synology-Paket](08-synology.md) | DS923+ und andere DSM-Nas | 1 Klick |
| [Home-Assistant-Add-on](07-ha-addon.md) | Home Assistant auf HA OS / Supervised | 2 Klicks |
| [Docker-Container](03-installation.md#docker-container) | jeder Rechner mit Docker | eine Datei |
| [Nur Home Assistant](#nur-home-assistant) | Sie nutzen nur HA, kein Docker | 1 Klick |

---

## Docker-Container

### Voraussetzungen

- Docker auf dem Zielsystem (NAS, Raspberry Pi, Linux-Server)
- der USB-RS485-Adapter an diesem System

### Installation

Erstellen Sie eine Datei `compose.yaml`:

````yaml
services:
  gateway:
    image: ghcr.io/rf77/comfobox-gateway:1.0.1
    container_name: comfobox-gateway
    restart: unless-stopped
    ports:
      - "8081:8080"
    environment:
      COMFOBOX_CONFIG_FILE: /var/lib/comfobox/config.json
    devices:
      - "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0:/dev/ttyUSB0"
    group_add:
      - "dialout"
    volumes:
      - comfobox-data:/var/lib/comfobox
    security_opt:
      - "no-new-privileges:true"

volumes:
  comfobox-data:
````

Dann starten:

````bash
docker compose up -d
docker compose logs -f gateway
````

> **Den Pfad anpassen:** Ersetzen Sie den `by-id`-Pfad durch den Ihres
> Adapters. Die richtige Datei finden Sie mit `ls -l /dev/serial/by-id/`.

### Zugang

Die Oberfläche erreichen Sie unter `http://<IP>:8081/`.

### Wichtige Befehle

| Befehl | Wirkung |
|---|---|
| `docker compose logs -f gateway` | Protokoll live verfolgen |
| `docker compose restart gateway` | neu starten |
| `docker compose pull && docker compose up -d` | auf eine neue Version aktualisieren |
| `docker compose down` | stoppen (**ohne** `-v`!) |

> **Wichtig:** `docker compose down -v` löscht auch das Datenvolumen – und damit
> den Schreibzähler. Der Schreibschutz für die Elektronik der ComfoBox beginnt
> dann erneut. Verwenden Sie immer nur `down`.

---

## Nur Home Assistant

Wenn Sie **kein Docker** und **kein NAS** einsetzen wollen, können Sie die
ComfoBox auch **direkt aus Home Assistant** ansprechen. In diesem Fall braucht
Sie **keinen** MQTT-Broker.

### Voraussetzung

Home Assistant auf **HA OS** oder **Supervised** – dort stehen die Add-ons zur
Verfügung.

### Schritt 1 – Add-on installieren

1. **Einstellungen → Add-ons → Add-on-Store**
2. Menü oben rechts → **Repositories**
3. Diese Adresse eintragen:
   `https://github.com/RF77/comfobox-mqtt/tree/main/deploy/ha-addon`
   → **Hinzufügen**
4. **ComfoBox Gateway** suchen → **Installieren** → **Starten**

Details: [Home-Assistant-Add-on](07-ha-addon.md)

### Schritt 2 – Seriellen Port wählen

In der Add-on-Konfiguration gibt es das Feld **Serieller Port** – ein Dropdown
mit allen Anschlüssen des Systems. Wählen Sie den Adapter.

### Schritt 3 – Integration einrichten

1. **Einstellungen → Geräte & Dienste → Integration hinzufügen → ComfoBox**
2. Adresse eintragen: `http://<HA-IP>:8099`
3. API-Token eintragen (zu finden in der Web-Oberfläche des Add-ons unter
   *Konfiguration → API-Token*)
4. **Hinzufügen**

Fertig – das Gerät **ComfoBox ELESTA** erscheint mit allen Werten.

Details: [Home Assistant](05-home-assistant.md)

---

## Aktualisierung

| Weg | Vorgehen |
|---|---|
| Docker | `docker compose pull && docker compose up -d` |
| Synology | Paketzentrum → **ComfoBox Gateway** → **Aktualisieren** |
| HA-Add-on | Add-on-Store → **Aktualisieren** |
| HA-Integration | HACS → **ComfoBox** → **Aktualisieren** |

> **Immer eine konkrete Version verwenden**, nicht `latest`. So können Sie im
> Problemfall auf die vorherige Version zurück.

---

## Deinstallation

| Weg | Vorgehen | Daten |
|---|---|---|
| Docker | `docker compose down` | Volume bleibt erhalten |
| Synology | Paketzentrum → **Deinstallieren** | Konfiguration und Schreibzähler bleiben erhalten |
| HA-Add-on | Add-on → **Deinstallieren** | Daten bleiben erhalten |

> **Der Schreibzähler bleibt bei allen Varianten erhalten.** Das ist Absicht –
> er schützt die Elektronik der ComfoBox vor zu häufigen Schreibvorgängen.
