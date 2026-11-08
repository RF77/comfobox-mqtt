---
title: Hilfe
description: Anleitungen für Installation, Betrieb und Fehlersuche des ComfoBox Gateways
order: 0
icon: pi pi-book
---

# ComfoBox Gateway – Hilfe

Diese Anleitung begleitet Sie durch die Installation und den Betrieb des
**ComfoBox Gateways** für die Zehnder ComfoBox Series 5 (ELESTA).

## Inhaltsverzeichnis

| Seite | Inhalt |
|---|---|
| [Erste Schritte](01-erste-schritte.md) | Installation, Anschluss und die ersten gelesenen Werte |
| [Anschluss](02-anschluss.md) | Adapter, Verdrahtung, Baudrate, Fehlersuche am Kabel |
| [Installation](03-installation.md) | Die fünf Installationsmöglichkeiten im Vergleich |
| [MQTT](04-mqtt.md) | Broker einrichten, Topics, Werte schreiben |
| [Home Assistant](05-home-assistant.md) | Automatische Entitäten und die HACS-Integration |
| [Bedienung](06-bedienung.md) | Web-Oberfläche, Werte schreiben, Schreibschutz, **Protokoll ansehen** |
| [HA-Add-on](07-ha-addon.md) | Installation als Add-on in Home Assistant |
| [Raspberry Pi](07b-raspberry-pi.md) | **Ein Befehl im Terminal – inklusive Autostart** |
| [Synology](08-synology.md) | Installation als Paket auf der DS923+ |
| [Synology testen](08b-synology-test.md) | **Schritt für Schritt: Paket installieren, Image freigeben, ersten Test machen** |
| [Monitoring](08-monitoring.md) | Health überwachen, Alarmierung, **Backup und Wiederherstellung** |
| [Fehlersuche](09-fehlersuche.md) | Symptom, Ursache und Lösung |
| [API](10-api.md) | Schnittstelle für eigene Automatisierungen |

## Was Sie zuerst brauchen

| Komponente | Anforderung |
|---|---|
| Zehnder ComfoBox Series 5 (ELESTA) | mit installierter Wärmepumpe oder Lüftungsgerät |
| USB-RS485-Adapter | die ComfoBox hat **keinen** eigenen Anschluss |
| Home Assistant | optional – nur wenn Sie die Werte dort nutzen wollen |
| MQTT-Broker | optional – nur für die MQTT-Anbindung |

> **Noch nicht eingerichtet?** Beginnen Sie mit
> [Erste Schritte](01-erste-schritte.md) – dort steht Schritt für Schritt, was zu tun ist.

## Kurzfassung

1. **Anschließen:** Adapter an A/B der ComfoBox, USB in den Computer oder das NAS
2. **Baudrate:** ComfoBox auf **38400** umstellen und **neu starten**
3. **Installieren:** entweder als Raspberry-Pi-Container (ein Befehl), als
   Synology-Paket, als Home-Assistant-Add-on, als Docker-Container oder – wenn
   Sie nur Home Assistant nutzen – direkt als HACS-Integration
4. **Einrichten:** den Assistenten durchlaufen, Benutzer und Passwort setzen
5. **Fertig:** die Werte erscheinen in der Web-Oberfläche und – falls gewünscht –
   in Home Assistant

## Hilfe anzeigen

Diese Seiten sind in der Web-Oberfläche unter **Hilfe** eingebaut und
funktionieren auch ohne Internetverbindung.
