---
title: Raspberry Pi
description: Das Gateway mit einem Befehl auf einem Raspberry Pi installieren
order: 75
icon: pi pi-microchip
tags: [installation, raspberry-pi, docker]
---

# Raspberry Pi

Auf einem Raspberry Pi genügt **eine Zeile** im Terminal. Sie installiert
Docker (falls nötig), richtet den Container ein und sorgt dafür, dass er nach
jedem Neustart von selbst wieder läuft.

> **Zwei Wege – bitte zuerst prüfen, welcher passt:**
>
> | Auf dem Pi läuft … | Weg |
> |---|---|
> | **Raspberry Pi OS** (mit oder ohne Desktop) | diese Seite – der eine Befehl |
> | **Home Assistant OS** | [HA-Add-on](07-ha-addon.md) – dort gibt es kein `sudo` |
>
> „Pi V3" ist ein 32/64-Bit-Gerät. Das Gateway gibt es **nur für 64-Bit**;
> ein 32-Bit-Raspberry-Pi-OS wird vom Installer mit einem klaren Hinweis
> abgelehnt.

---

## Voraussetzungen

| Was | Anforderung |
|---|---|
| Betriebssystem | **Raspberry Pi OS 64-bit** (Lite genügt) – prüfen mit `getconf LONG_BIT` → muss `64` sein |
| Netzteil | das **offizielle** Netzteil; bei Unterspannung setzt der Pi den USB-Bus zurück |
| USB-RS485-Adapter | direkt in eine USB-Buchse des Pi – **kein Hub, keine Verlängerung** |
| Internet | nur für die Installation (das Image kommt aus der Registry) |
| SD-Karte | mindestens 4 GB frei |

> **Speicher beachten:** Der Pi 3 hat 1 GB Arbeitsspeicher, der Pi 4/5 mehr.
> Das Gateway läuft auf dem Pi 3, ist dort aber träge – die Oberfläche braucht
> beim ersten Aufruf einige Sekunden. Wer Home Assistant **und** Gateway auf
> demselben Gerät betreiben will, sollte einen Pi 4/5 nehmen.

---

## Der eine Befehl

Per SSH auf dem Pi anmelden und diesen Befehl ausführen:

````bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/pi/install.sh | sudo sh
````

Das Skript ist **wiederholbar**: ein zweiter Aufruf aktualisiert das Image und
lässt alle Daten stehen. Am Ende erscheint die Adresse der Oberfläche.

### Was das Skript tut

| Schritt | Wirkung |
|---|---|
| 1 | prüft die Architektur (bricht bei 32-Bit verständlich ab) |
| 2 | ermittelt das neueste Release als Image-Version |
| 3 | installiert Docker, falls es fehlt, und aktiviert den **Autostart** |
| 4 | zeigt vorhandene serielle Adapter an (ein fehlender ist kein Hindernis) |
| 5 | schreibt `/opt/comfobox/compose.yaml` |
| 6 | zieht das Image und startet den Container |
| 7 | wartet, bis die Oberfläche antwortet, und zeigt die Adressen |

### Optionen

````bash
# Anderer Port (Vorgabe 8099)
curl -fsSL …/install.sh | sudo sh -s -- --port 8080

# Bestimmte Version
curl -fsSL …/install.sh | sudo sh -s -- --version 1.0.4

# Image aus einer Datei (Pi ohne Internet) – Datei vorher auf den Pi kopieren
sudo sh install.sh --offline comfobox-gateway-arm64.tar.gz

# Entfernen (Daten bleiben erhalten)
curl -fsSL …/install.sh | sudo sh -s -- --uninstall
````

Das Image liegt an jedem GitHub-Release auch als Datei
`comfobox-gateway-arm64.tar.gz` bei. Das ist der Weg für einen Pi ohne
Registry-Zugriff.

---

## Nach der Installation

### 1. Assistent durchlaufen

Die Oberfläche unter `http://<IP-des-Pi>:8099` öffnen. Beim ersten Aufruf
startet der Assistent:

| Schritt | Eingabe |
|---|---|
| Serieller Port | den angebotenen Port wählen (bei einem Adapter `ttyUSB0`) |
| BACnet | **38400 Baud** – die ComfoBox muss ebenfalls darauf stehen und **neu gestartet** sein |
| MQTT | Broker-Adresse, z. B. die IP Ihres Home-Assistant-Rechners mit Port `1883` |
| Sicherheit | Benutzer und Passwort für die Oberfläche (mindestens 12 Zeichen) |

> **MQTT ist optional.** Ohne Broker laufen die Werte trotzdem in die
> Oberfläche und über die REST-Schnittstelle nach Home Assistant.

### 2. API-Token erzeugen

**Konfiguration → API-Token → Token erzeugen.** Der Token wird **genau einmal**
angezeigt – jetzt kopieren. Er ist das Passwort für die Home-Assistant-Integration.

### 3. Home Assistant verbinden

1. HACS → Integrationen → **ComfoBox** installieren (Repository
   `https://github.com/RF77/comfobox-mqtt`)
2. Home Assistant neu starten
3. **Einstellungen → Geräte & Dienste → Integration hinzufügen → ComfoBox**
4. Adresse `http://<IP-des-Pi>:8099` und den Token eintragen

Danach erscheint die ComfoBox als **ein Gerät** mit ihren Entitäten. Die
Integration fragt das Gateway über REST ab (kein MQTT nötig) und aktualisiert
alle 10 Sekunden.

> **Beides gleichzeitig ist erlaubt und sinnvoll:** MQTT für Home Assistant und
> andere Verbraucher, REST für die Integration. Beide beschreiben dieselben
> Werte – der Schreibschutz (24-h-Zähler) gilt für jeden Weg.

---

## Betrieb

| Aufgabe | Befehl |
|---|---|
| Protokoll ansehen | `cd /opt/comfobox && sudo docker compose logs -f gateway` |
| Neu starten | `cd /opt/comfobox && sudo docker compose restart` |
| Aktualisieren | denselben Ein-Zeilen-Befehl erneut ausführen |
| Zustand | `docker ps --filter name=comfobox-gateway` |
| Oberfläche | `curl -s http://localhost:8099/api/health` |

**Autostart:** Der Container hat die Neustart-Regel `unless-stopped`, und der
Docker-Dienst ist beim Booten aktiviert. Nach einem Neustart des Pi ist das
Gateway ohne Zutun wieder da. Der 24-h-Schreibzähler liegt im Volume
`comfobox-data` und überlebt jedes Update.

> **Niemals `docker compose down -v`** ausführen – das `-v` löscht das Volume
> und damit den Schreibzähler. Der EEPROM-Schutz der ComfoBox beginnt dann
> wieder bei null.

---

## Wenn etwas nicht klappt

| Symptom | Ursache | Lösung |
|---|---|---|
| `Dieses System ist 32-Bit` | 32-Bit-Raspberry-Pi-OS | 64-Bit-System installieren (`getconf LONG_BIT` → 64) |
| Kein Port im Assistenten | Adapter nicht erkannt | `ls -l /dev/ttyUSB*` prüfen; Adapter direkt anstecken, kein Hub |
| Port da, aber „Permission denied" | Gruppe fehlt | `sudo docker compose down && sudo docker compose up -d` – die Compose-Datei enthält die Gruppe `dialout` bereits |
| `no_iam` im Protokoll | ComfoBox auf 76800 Baud | auf 38400 umstellen und die Anlage **neu starten** |
| Oberfläche nach 2 Minuten nicht erreichbar | Pi 3 ist langsam, oder Port belegt | `sudo docker compose logs gateway`; `ss -ltnp \| grep 8099` |
| Container startet nach Neustart nicht | Docker-Dienst nicht aktiviert | `sudo systemctl enable --now docker` |
| Entitäten in HA „nicht verfügbar" | falsche Adresse oder Token | `curl -s http://<IP>:8099/api/health` vom HA-Rechner testen |

### Adapter wird nicht erkannt

Der Kernel von Raspberry Pi OS bringt die Treiber für CH340, FTDI, CP210x und
PL2303 mit – hier ist das kein Thema wie auf einem NAS. Bleibt der Port
trotzdem weg, hilft das Prüfskript, das auch auf dem Pi läuft:

````bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/synology-spk/tools/usb-serial-check.sh -o /tmp/usb-serial-check.sh
sudo sh /tmp/usb-serial-check.sh comfobox-gateway
````

Es ändert nichts und nennt am Ende genau eine Ursache. Weitere Fälle:
[Fehlersuche](09-fehlersuche.md).

---

## Warum Docker und nicht „einfach installieren"?

Weil es **dieselbe Software** ist, die auch als Synology-Paket und als
HA-Add-on läuft – ein Image, eine Oberfläche, ein Verhalten. Der Container
bringt alles mit (.NET, Web-Oberfläche, Hilfeseiten); auf dem Pi selbst wird
nichts installiert außer Docker. Und weil der Container als **ein** Paket
aktualisiert, gesichert und wieder entfernt werden kann.

Verwandte Seiten: [Installation im Überblick](03-installation.md) ·
[HA-Add-on](07-ha-addon.md) · [Home Assistant](05-home-assistant.md) ·
[MQTT](04-mqtt.md)
