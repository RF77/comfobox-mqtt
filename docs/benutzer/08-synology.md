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

---

## Installation

### Variante A – aus dem Paketzentrum (empfohlen)

1. Kopieren Sie die Datei `ComfoBoxGateway_noarch-dsm7_<version>.spk` auf das NAS
   (z. B. per Samba-Freigabe oder `scp`)
2. **Paketcenter → Installieren → Manuelle Installation**
3. Datei auswählen → **Installieren**
4. **Fertig** – das Browserfenster öffnet sich automatisch

### Variante B – eigene Paketquelle (für mehrere Installationen)

Sie können die Paketquelle im DSM eintragen, dann erscheint das Paket in der
normalen Liste:

**Paketcenter → Einstellungen → Paketquelle → Hinzufügen**

| Feld | Wert |
|---|---|
| Name | `ComfoBox` |
| URL | `https://<Release-URL>/packages.json` |

Danach: **Paketcenter → Community → ComfoBox Gateway → Installieren**

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
| `/var/packages/ComfoBoxGateway/var/` | **Konfiguration, Schreibzähler, Protokolle** |
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
| SSH | `/var/packages/ComfoBoxGateway/var/logs/` |
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
