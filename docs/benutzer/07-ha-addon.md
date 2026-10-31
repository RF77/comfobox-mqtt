---
title: Home-Assistant-Add-on
description: Installation und Betrieb als Add-on in Home Assistant
order: 70
icon: pi pi-box
tags: [home-assistant, add-on]
---

# Home-Assistant-Add-on

Das Add-on ist die bequemste Variante für Home Assistant auf **HA OS** oder
**Supervised**: eine Installation, ein Port, die Oberfläche direkt in Home Assistant.

> **Voraussetzung:** Home Assistant auf **HA OS** oder **Supervised**. Auf einem
> Home-Assistant-Container (z. B. auf einem NAS) gibt es keine Add-ons – dort
> nutzen Sie [Docker oder Synology](03-installation.md).

---

## Installation

1. **Einstellungen → Add-ons → Add-on-Store**
2. Menü (⋮) oben rechts → **Repositories**
3. Diese Adresse eintragen:
   `https://github.com/RF77/comfobox-mqtt/tree/main/deploy/ha-addon`
   → **Hinzufügen**
4. **ComfoBox Gateway** suchen → **Installieren**
5. **Starten**

> **Der erste Start dauert etwas länger** – das Image wird geladen.

---

## Konfiguration

Im Tab **Konfiguration** des Add-ons:

| Feld | Bedeutung | Standard |
|---|---|---|
| **Serieller Port** | Dropdown mit allen seriellen Anschlüssen | – |
| **Baudrate** | Kommunikationsgeschwindigkeit | 38400 |
| **MQTT-Basis-Topic** | Präfix aller Topics | `ComfoBox` |
| **Broker-Host** | Adresse des Brokers (leer = ohne MQTT) | – |
| **Broker-Port** | Port des Brokers | 1883 |
| **Broker-Benutzername** | optional | – |
| **Broker-Passwort** | optional | – |
| **TLS verwenden** | verschlüsselte Broker-Verbindung | aus |
| **Home-Assistant-Discovery** | Entitäten automatisch anmelden | an |
| **Log-Level** | Detailgrad der Protokolle | Information |

### Der serielle Port

Das Dropdown zeigt alle **seriellen** Anschlüsse des Systems – bevorzugt mit
stabilen Pfaden wie `/dev/serial/by-id/…`. Diese Pfade überleben Neustarts.

| Problem | Lösung |
|---|---|
| Der Adapter fehlt im Dropdown | USB-Adapter einstecken, Add-on neu starten |
| Falscher Port gewählt | Konfiguration ändern → Add-on neu starten |
| „Permission denied“ | siehe [Anschluss](02-anschluss.md#adapter-erkennen) |

> **Warum kein „alle Ports durchreichen"?**
> Weil das unnötig weitreichend wäre. Sie wählen genau **einen** Anschluss – das
> ist die schmalste Rechtevergabe, die funktioniert.

### Der Broker

| Variante | Einstellung |
|---|---|
| **Home-Assistants eigener Mosquitto** | `Broker-Host: core-mosquitto`, Port `1883` |
| **Externer Broker** | `Broker-Host: 192.168.1.10` + Zugangsdaten |
| **Kein Broker** | `Broker-Host` leer lassen |

> **Ohne Broker** läuft das Gateway ohne MQTT. Die Werte sind dann nur über die
> [HACS-Integration](05-home-assistant.md) erreichbar. Das ist ein legitimer
> Betriebszustand.

---

## Die Oberfläche

Das Add-on bringt die Web-Oberfläche **direkt in Home Assistant** – als Panel in der
linken Leiste.

| Aspekt | Wert |
|---|---|
| Menüeintrag | **ComfoBox** (mit Thermometer-Symbol) |
| Zugriff | ohne zusätzlichen Port, ohne zweiten Browser-Tab |
| Rechte | nur für Administratoren sichtbar |

> **Wichtig:** Das Panel ist **nur** die Oberfläche. Die HACS-Integration nutzt
> bewusst **nicht** diesen Weg, sondern den Port 8099 – eine Panel-Adresse enthält
> ein rotierendes Token und ist für Automatisierungen ungeeignet.

---

## Erreichbarkeit für die Integration

| Zweck | Adresse |
|---|---|
| Web-Oberfläche | Panel in Home Assistant |
| Integration / Automatisierungen | `http://<HA-IP>:8099` |

Der Port **8099** wird auf dem Host freigegeben. Über ihn läuft die REST-Schnittstelle.

> **In der Firewall?** Wenn die Integration keine Verbindung bekommt, prüfen Sie, ob
> Port 8099 zwischen den Geräten freigegeben ist.

---

## Überwachung

Der Home-Assistant-Supervisor überwacht das Add-on. Antwortet das Gateway nicht
mehr, startet der Supervisor es neu.

| Prüfung | Wirkung |
|---|---|
| `/api/health` antwortet nicht | Neustart des Add-ons |
| Container beendet sich | Neustart des Add-ons |

> **Der Healthcheck prüft nur, ob der Prozess lebt** – nicht, ob die Heizung
> antwortet. Ein gezogener USB-Adapter ist **kein** Grund für einen Neustart:
> Die Heizung läuft in diesem Fall mit ihrem letzten Sollwert weiter, und ein
> Neustart würde nichts bringen.

---

## Backups

Das Add-on wird **im laufenden Betrieb** gesichert. Enthalten sind:

- `config.json` – Ihre Einstellungen inkl. Passwort-Hashes
- `writes.json` – der Schreibzähler (**wichtig!**)
- Protokolldateien

> **Der Schreibzähler muss in jedem Backup sein.** Fehlt er, beginnt der
> Schreibschutz für die Heizung von vorn.

---

## Aktualisieren

**Einstellungen → Add-ons → ComfoBox Gateway → Aktualisieren**

> **Vor dem Update** prüfen, ob die Änderungen in Frage kommen – im Tab
> **Changelog** des Add-ons steht, was sich geändert hat.

---

## Problembehebung

| Symptom | Ursache | Lösung |
|---|---|---|
| Add-on startet nicht | Port 8099 belegt | Port im DSM/Host freigeben |
| `no_iam` im Protokoll | ComfoBox auf 76800 Baud | auf 38400 umstellen + neu starten |
| Keine Werte in HA | Broker fehlt | Broker eintragen oder HACS-Integration nutzen |
| Entitäten `unavailable` | Gateway nicht erreichbar | Port 8099 in der Firewall freigeben |
| Neustart-Schleife | Port belegt | Protokoll im Add-on ansehen |
