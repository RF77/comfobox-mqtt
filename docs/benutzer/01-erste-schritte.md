---
title: Erste Schritte
description: Installation, Anschluss und die ersten gelesenen Werte
order: 10
icon: pi pi-rocket
tags: [installation, anleitung]
---

# Erste Schritte

Diese Anleitung führt Sie von der Lieferung bis zu den ersten gelesenen Werten.
Sie dauert etwa 15 Minuten.

---

## 1. ComfoBox auf 38400 Baud umstellen

> **Das ist der häufigste Grund, warum nichts funktioniert.**
> Die ComfoBox spricht nach dem Werksthypoten **76800 Baud**. Das Gateway
> erwartet **38400 Baud**.

### Wie Sie die Baudrate ändern

1. Öffnen Sie am Bedienpanel der ComfoBox das Menü **Service**
2. Wählen Sie **Installationsassistent** bzw. **Expertenebene**
3. Suchen Sie den Punkt **Baudrate** (bei manchen Geräten unter *Kommunikation*)
4. Stellen Sie den Wert auf **38400** ein
5. **Speichern**
6. **Starten Sie die ComfoBox neu** – die Änderung wird erst danach wirksam

> **Warum der Neustart?** Die Baudrate wird nur beim Start des Kommunikations-
> prozesses gelesen. Ohne Neustart bleibt die ComfoBox bei 76800.

Prüfen Sie, ob die ComfoBox antwortet, mit dem Tool **„Kommunikation prüfen"**,
sofern Ihr Gerät eines anbietet.

---

## 2. USB-RS485-Adapter anschließen

| Kabel am Adapter | Anschluss an der ComfoBox |
|---|---|
| **A** (oder D+/RS485-) | **A** |
| **B** (oder D-/RS485-) | **B** |
| **GND** | **GND** (falls vorhanden) |

> **A und B nicht vertauschen.** Bei vertauschten Kabeln antwortet die ComfoBox
> gar nicht – das ist der zweithäufigste Fehler.

Stecken Sie den Adapter in einen USB-Port des Computers, des NAS oder des
Raspberry Pi.

---

## 3. Software installieren

Wählen Sie eine der vier Möglichkeiten:

| Ihre Situation | Empfehlung |
|---|---|
| Synology-NAS (DS923+) | [Paket aus dem Paketzentrum](08-synology.md) |
| Home Assistant auf HA OS / Supervised | [Add-on](07-ha-addon.md) |
| Eigenes Linux-System mit Docker | [Docker-Container](03-installation.md#docker-container) |
| Nur Home Assistant, ohne Docker | [HACS-Integration](05-home-assistant.md) |

> **Wichtig:** Auf **keinem** System dürfen das Gateway und eine direkte
> BACnet-Verbindung gleichzeitig auf denselben Anschluss gelegt werden.
> Der Bus kann nur einen Sender auf einmal vertragen.

---

## 4. Ersteinrichtung

Beim ersten Start fragt das Gateway nach seinen Einstellungen. Der Assistent
begleitet Sie durch sechs Schritte:

| Schritt | Was Sie tun |
|---|---|
| 1 | **Seriellen Port wählen** – die Liste zeigt alle erkannten Anschlüsse |
| 2 | **BACnet prüfen** – Baudrate 38400, Geräte-ID 1, dann *Verbindung testen* |
| 3 | **MQTT** – Broker eintragen (optional) und *Verbindung testen* |
| 4 | **Web-Zugang** – Benutzername und Passwort setzen (mindestens 12 Zeichen) |
| 5 | **Fertig** – *Übernehmen und starten* |

> **Keine seriellen Ports in der Liste?**
> Der Adapter ist nicht erkannt. Stecken Sie ihn ab und stecken Sie ihn neu ein
> und klicken Sie auf *Erneut suchen*. Details: [Anschluss](02-anschluss.md).

> **„Die ComfoBox antwortet nicht"?**
> Prüfen Sie die Baudrate (Schritt 1) und die Verdrahtung (Schritt 2).
> Details: [Fehlersuche](09-fehlersuche.md#die-comfobox-antwortet-nicht).

---

## 5. Erste Werte sehen

Nach dem Neustart öffnet sich die Web-Oberfläche. Sie sehen:

| Kachel | Bedeutung |
|---|---|
| **Health-Banner** | grün = alles in Ordnung, gelb = eingeschränkt, rot = nicht erreichbar |
| **BACnet** | verbunden oder getrennt |
| **MQTT** | verbunden oder getrennt (nur wenn ein Broker eingetragen ist) |
| **Betriebsart** | die aktuell gewählte Betriebsart der Lüftungsanlage |
| **Raumsollwert** | der aktuell gesetzte Sollwert in °C |

Unter **Geräte** sehen Sie den vollständigen Katalog aller 127 Mess- und
Stellwerte. Links neben jedem Wert steht, ob er **schreibbar** ist.

---

## Zugang zur Web-Oberfläche

| Installationsart | Adresse |
|---|---|
| Synology-Paket | `http://<NAS-IP>:8081/` |
| Home-Assistant-Add-on | **Hilfe**-Panel in Home Assistant |
| Docker-Container | `http://<Rechner-IP>:8081/` |

Die Oberfläche ist passwortgeschützt. Das Passwort haben Sie in Schritt 4 des
Assistenten vergeben.

> **Passwort vergessen?**
> Im Docker-Betrieb: die Datei `config.json` im Datenverzeichnis bearbeiten und
> den Container neu starten. Im Synology-Betrieb: das Paket deinstallieren und
> neu installieren – die Konfiguration bleibt dabei erhalten.

---

## Nächster Schritt

| Sie möchten … | Lesen Sie |
|---|---|
| die Installation im Detail | [Installation](03-installation.md) |
| Werte an Home Assistant übergeben | [Home Assistant](05-home-assistant.md) |
| Werte über MQTT nutzen | [MQTT](04-mqtt.md) |
| etwas nicht funktioniert | [Fehlersuche](09-fehlersuche.md) |
