---
title: Synology
description: Installation als Paket auf der Synology DS923+
order: 80
icon: pi pi-server
tags: [synology, nas]
---

# Synology DS923+

Das Gateway lässt sich als **Paket** auf der DS923+ installieren. Sie klicken
*Installieren*, der Browser öffnet sich, und ein Assistent führt Sie durch die
Einrichtung.

> **Voraussetzung:** DSM 7.2.1 oder neuer mit **Container Manager**.
> Fehlt der Container Manager, installiert DSM ihn automatisch mit.

> **Sie probieren das Paket gerade zum ersten Mal aus?** Dann nehmen Sie die
> [Schritt-für-Schritt-Anleitung](08b-synology-test.md) – sie beschreibt auch
> den Schritt, der **auf GitHub** erledigt werden muss, bevor der Container
> starten kann (das Image muss öffentlich sein).

---

## Installation

### Variante A – Paketquelle (empfohlen)

Sie tragen die Paketquelle im DSM ein, dann erscheint das Paket in der
normalen Liste – und Sie müssen **keine Datei auf die NAS kopieren**:

**Paketcenter → Einstellungen → Paketquellen → Hinzufügen**

| Feld | Wert |
|---|---|
| Name | `ComfoBox` |
| Speicherort (URL) | <https://github.com/RF77/comfobox-mqtt/releases/download/v1.0.1/packages.json> |

Danach: **Paketcenter → Community → ComfoBox Gateway → Installieren**

> **Die URL ist versionsgebunden.** Für eine neuere Version steht die
> passende `packages.json` im jeweiligen Release. Die Datei nennt die
> **exakte** Download-Adresse des Pakets – sie ist deshalb auf einen festen
> Release-Tag verlinkt und nicht auf „das neueste Release".
>
> **Warum dieser Weg?** Nur bei einer registrierten Paketquelle kennt das DSM
> den Herausgeber. Die manuelle Installation einer lokalen Datei bricht auf
> DSM 7.4 sonst mit *„Dieses Paket wurde von einem unbekannten Herausgeber
> veröffentlicht"* ab.

### Variante B – Datei von Hand

1. Laden Sie die Datei `ComfoBoxGateway_noarch-dsm7_1.0.1-0006.spk` aus dem
   GitHub-Release herunter:
   <https://github.com/RF77/comfobox-mqtt/releases/download/v1.0.1/ComfoBoxGateway_noarch-dsm7_1.0.1-0006.spk>
   (Alternativ über die [Releases](https://github.com/RF77/comfobox-mqtt/releases))
2. Kopieren Sie die Datei auf das NAS (z. B. per Samba-Freigabe oder `scp`)
3. **Paketcenter → Installieren → Manuelle Installation**
4. Datei auswählen → **Installieren**
5. **Fertig** – das Browserfenster öffnet sich automatisch

> **Auf der NAS darf nur eine `.spk` liegen.** Das Paketzentrum installiert die
> Datei, die Sie im Dateiauswahler anklicken – liegen dort mehrere Versionen
> nebeneinander, gewinnt die falsche. Alte `.spk` vorher löschen.

---

## Was bei der Installation passiert

| Schritt | Was das Paket tut |
|---|---|
| Voraussetzungen prüfen | DSM-Version, Container Manager, freier Port 8081 |
| Verzeichnisse anlegen | Konfiguration und Protokolle |
| USB-Regel installieren | stabiler Pfad für den Adapter, passende Rechte |
| Ersten Port suchen | alle seriellen Anschlüsse ermitteln |
| Konfiguration anlegen | mit „Einrichtung noch nicht abgeschlossen" |
| Container starten | Compose-Datei erzeugen und starten |

> **Es werden keine Parameter abgefragt.** Das ist Absicht: die Installation soll
> **ein Klick** sein. Alles Weitere erledigt der Assistent danach.

---

## Der Assistent

Nach der Installation öffnet sich automatisch `http://<NAS-IP>:8081/` und der
Einrichtungsassistent startet:

| Schritt | Inhalt |
|---|---|
| 1 | **Seriellen Port wählen** – alle erkannten Anschlüsse |
| 2 | **BACnet prüfen** – Baudrate 38400, Geräte-ID 1, Verbindungstest |
| 3 | **MQTT** – Broker eintragen und testen (optional) |
| 4 | **Web-Zugang** – Benutzername und Passwort |
| 5 | **Fertig** – übernehmen, Neustart wird beobachtet |

> **Der Assistent ist wiederholbar.** Sie können ihn jederzeit über
> *Konfiguration → Erneut einrichten* erneut durchlaufen – etwa wenn Sie den
> Adapter umgesteckt haben.

Details: [Erste Schritte](01-erste-schritte.md)

---

## Wichtige Pfade auf dem NAS

| Pfad | Inhalt |
|---|---|
| `/var/packages/ComfoBoxGateway/target/` | Programm – wird beim Update ersetzt |
| `/var/packages/ComfoBoxGateway/var/` | **Protokolle und Status** (auf der NAS lesbar) |
| Docker-Volume `comfobox-data` | **Konfiguration, Schreibzähler, ports.json** (im Container) |

> **Warum ein Docker-Volume?** Das DSM löscht bei jeder Installation
> `/var/packages/<paket>` und legt es neu an. Ein Bind-Mount auf dieses
> Verzeichnis gehört danach `root:root` – der Container (uid 1001) kann
> dort weder die Konfiguration schreiben noch den **24-h-Schreibzähler**
> (EEPROM-Schutz) sichern. Ein benanntes Volume übernimmt Rechte aus dem
> Image – der Container kann dort immer schreiben.

> **Achtung beim Deinstallieren:** Der DSM-Worker gibt das Projekt und
> damit möglicherweise auch das Volume wieder frei. Der 24-h-Schreibzähler
> geht dabei verloren, und das Schreiblimit beginnt erneut. **Vor dem
> Deinstallieren sichern** (siehe [Backup](../03-deployment/synology-paketcenter.md)).
| `/etc/udev/rules.d/99-comfobox-serial.rules` | USB-Regel |
| `/etc/systemd/system/comfobox-gateway.service` | Dienst für die Überwachung |

> **Bei einem Update wird `target/` ersetzt, `var/` bleibt.** Ihre Einstellungen
> und der Schreibzähler bleiben erhalten.

---

## Der Container

| Aspekt | Wert |
|---|---|
| Name | `comfobox-gateway` |
| Port | 8081 (Host) → 8080 (Container) |
| Image | `ghcr.io/rf77/comfobox-gateway:<version>` |
| Rechte | keine Administratorrechte, keine Sonderrechte |
| Serieller Port | genau der gewählte Anschluss |

> **Es läuft nur ein Prozess.** Die Oberfläche wird direkt vom .NET-Host ausgeliefert –
> kein zusätzlicher Webserver.

---

## Serielle Ports wechseln

Wenn Sie den Adapter in eine andere USB-Buchse stecken:

1. In der Web-Oberfläche: **Konfiguration → BACnet**
2. Auf **Neu suchen** klicken
3. Den neuen Port wählen und **Übernehmen** – der Gateway benutzt ihn **sofort**

> **Sie müssen das Paket nicht neu installieren** und nichts auf der Kommandozeile
> machen.

Falls das Paket den Anschluss fest an den Container durchreicht (Option
„Alle seriellen Ports durchreichen" **nicht** aktiv), muss der Container
zusätzlich mit dem neuen Anschluss neu erstellt werden:

1. In der Web-Oberfläche: **Konfiguration → Erneut einrichten**
2. Schritt 1: neuen Port wählen
3. **Übernehmen**

Das dauert etwa 10 Sekunden.

---

## Dienste

| Befehl | Wirkung |
|---|---|
| `synopkg start ComfoBoxGateway` | starten |
| `synopkg stop ComfoBoxGateway` | stoppen |
| `synopkg status ComfoBoxGateway` | Status abfragen |
| `synopkg restart ComfoBoxGateway` | neu starten |

> **Über SSH** erreichbar. Die Befehle laufen auch, wenn der Container gerade
> kaputt ist.

---

## Protokolle

| Weg | Datei |
|---|---|
| Container-Manager → `comfobox-gateway` → Protokoll | live |
| SSH | `docker logs comfobox-gateway` |

> **Nach der Installation zu erwarten:** Im Protokoll stehen zwei Meldungen,
> die **normal** sind und **keinen** Fehler darstellen:
>
> | Meldung | Bedeutung |
> |---|---|
> | `MQTT-Verbindung zu "localhost:1883" fehlgeschlagen` | Es ist noch kein Broker eingetragen. Der Assistent fragt ihn nach der Installation ab. |
> | `Access to the port '/dev/ttyUSB0' is denied … No such file or directory` | Der USB-Adapter ist nicht angesteckt oder hat einen anderen Namen. Der Assistent listet alle erkannten Ports auf. |
| Web-Oberfläche | **Diagnose** |

> **Bei Supportfragen** ist das Protokoll aus der Web-Oberfläche (Diagnose) der
> einfachste Weg – es ist ohne SSH erreichbar.

---

## Aktualisieren

**Paketcenter → ComfoBox Gateway → Aktualisieren**

Der Container wird dabei automatisch neu erstellt. Konfiguration und Schreibzähler
bleiben erhalten.

---

## Deinstallieren

**Paketcenter → ComfoBox Gateway → Deinstallieren**

| Was | Verhalten |
|---|---|
| Container | wird entfernt |
| Dienst, USB-Regel | werden entfernt |
| **Konfiguration und Schreibzähler** | **bleiben erhalten** |

> **Warum bleiben die Daten?** Der Schreibzähler schützt die Elektronik der Heizung.
> Ein Deinstallieren darf ihn nicht zurücksetzen. Bei einer Neuinstallation wird
> die vorhandene Konfiguration wieder übernommen.

---

## Problembehebung

| Symptom | Ursache | Lösung |
|---|---|---|
| Installation schlägt fehl | Container Manager fehlt | DSM installiert ihn automatisch – ggf. von Hand |
| „Port belegt" | 8081 ist belegt | Port in den Paketeinstellungen ändern |
| Paket startet nicht | Container Manager läuft nicht | Container Manager starten |
| Browser öffnet sich nicht | Popup-Blocker | `http://<NAS-IP>:8081/` manuell aufrufen |
| Werte fehlen | falscher Serial-Port | Assistent erneut durchlaufen |
| Alles `unavailable` | USB-Adapter gezogen | Adapter einstecken, ComfoBox neu starten |
