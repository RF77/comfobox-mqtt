---
title: Home Assistant
description: Automatische Entitäten und die HACS-Integration ohne MQTT
order: 50
icon: pi pi-home
tags: [home-assistant, entitäten]
---

# Home Assistant

Es gibt **zwei** Wege, die ComfoBox in Home Assistant einzubinden. Sie können
**beide** nutzen – sie greifen auf dieselben Daten zu.

| Weg | Broker nötig? | Aufwand | Vorteil |
|---|---|---|---|
| **MQTT-Discovery** | **ja** | gering | alle Werte automatisch, ohne Zusatzinstallation |
| **HACS-Integration** | **nein** | mittel | funktioniert **ohne** Broker, ein Gerät mit allen Werten |

> **Sie brauchen nur eins.** Die MQTT-Variante ist einfacher, wenn Sie ohnehin
> einen Broker betreiben (die meisten Home-Assistant-Installationen haben einen).
> Die HACS-Integration ist die richtige Wahl, wenn Sie keinen Broker einsetzen wollen.

---

## Variante 1 – Automatische Entitäten über MQTT

Das Gateway meldet seine Werte selbst bei Home Assistant an. Sie müssen **nichts**
konfigurieren.

### Einrichtung

| Schritt | Was |
|---|---|
| 1 | In den Assistenten den Broker eintragen und testen |
| 2 | **Discovery** einschalten |
| 3 | In Home Assistant: **Einstellungen → Geräte & Dienste → Integration hinzufügen → MQTT** |
| 4 | denselben Broker eintragen wie im Gateway |

> **Der Discovery-Prefix** ist standardmäßig `homeassistant`. Er muss in Home Assistant
> und im Gateway **übereinstimmen**.

### Was erscheint?

Ein Gerät **ComfoBox** mit allen 127 Werten als Entitäten.

| Werttyp | Entität in HA |
|---|---|
| Zahl, nicht schreibbar | Sensor |
| Zahl, schreibbar | Number |
| Auswahl, schreibbar | Select |
| Auswahl, nicht schreibbar | Sensor mit Options-Liste |

### Verfügbarkeit

Ist das Gateway nicht erreichbar, werden die Entitäten **unavailable** – nicht
„falsch". Das ist wichtig: Ein `unavailable` ist eindeutig, ein stehen gebliebener
Wert wäre gefährlich.

---

## Variante 2 – HACS-Integration (ohne Broker)

Die Integration spricht **direkt** mit dem Gateway über dessen HTTP-Schnittstelle.
Ein MQTT-Broker wird **nicht** benötigt.

### Installation

1. **HACS** öffnen
2. Menü → **Integrationen → Repositories** (oben rechts das Drei-Punkte-Menü)
3. Diese Adresse eintragen:
   `https://github.com/RF77/comfobox-mqtt`
   → **Hinzufügen**
4. **ComfoBox** suchen → **Herunterladen**
5. **Home Assistant neu starten**

### Einrichtung

1. **Einstellungen → Geräte & Dienste → Integration hinzufügen → ComfoBox**
2. **Adresse des Gateways** eintragen:
   - im Add-on-Betrieb: `http://<HA-IP>:8099`
   - bei Docker/Synology: `http://<Gateway-IP>:8081`
3. **API-Token** eintragen

> **Woher bekomme ich den Token?**
> In der Web-Oberfläche des Gateways unter **Konfiguration → API-Token** →
> **Neuen Token erzeugen**. Der Token wird **einmal** angezeigt – danach ist er
> nicht mehr abrufbar. Bewahren Sie ihn sicher auf.

4. **Hinzufügen** klicken

Fertig. Das Gerät **ComfoBox ELESTA** erscheint mit allen Werten.

---

## Werte in der Integration

| Kategorie | Anmerkung |
|---|---|
| nicht schreibbare Zahlen | Sensoren |
| schreibbare Zahlen | Number-Entitäten mit **genau** dem gültigen Bereich |
| schreibbare Auswahlwerte | Select-Entitäten mit den Originalnamen |
| schreibbare 0/1-Werte | Schalter |
| Sonderwerte | BACnet-Verbindung, Broker-Verbindung, Schreibzähler, Version |

> **Der gültige Bereich kommt direkt aus dem Gateway** – die Integration erfindet
> keine Grenzen. So kann kein Wert außerhalb des erlaubten Bereichs gesetzt werden.

### Schreibvorgänge

Auch über die Integration greift der Schreibschutz:

| Regel | Wirkung |
|---|---|
| Tageslimit (Standard 200) | wird überschritten → der Schreibvorgang wird **abgelehnt** |
| Werte außerhalb Min/Max | werden **abgelehnt** |
| Ungültiger Auswahlname | wird **abgelehnt** |

### Wenn das Gateway ausfällt

Die Entitäten werden **unavailable**. Das ist beabsichtigt: Ein stehen gebliebener
Sollwert wäre gefährlicher als ein sichtbarer Ausfall.

---

## Dienste (Aktionen)

| Dienst | Wirkung |
|---|---|
| `comfobox.set_value` | Wert schreiben |
| `comfobox.restart` | Gateway neu starten |
| `comfobox.read_all` | alle Werte sofort neu lesen |

Beispiel:

````yaml
service: comfobox.set_value
target:
  entity_id: number.comfobox_zone_setpoint_normal
data:
  value: 21.5
````

---

## Neustart aus Home Assistant

| Weg | Wie |
|---|---|
| **MQTT** | Schalter-Entität, die `ComfoBox/Command/Restart` sendet |
| **HACS-Integration** | `comfobox.restart` |
| **Web-Oberfläche** | Dashboard → *Gateway neu starten* |

> **Achtung:** Nach dem Neustart sind die Werte für etwa 10 Sekunden nicht
> verfügbar. Die Heizung arbeitet in dieser Zeit mit ihrem letzten Sollwert weiter.

---

## Zwei Wege gleichzeitig?

Ja – das ist unproblematisch. Beide greifen **nur lesend** auf dieselben Daten zu
(die MQTT-Variante publiziert nur). Es entsteht kein Konflikt.

> **Anders wäre es nur, wenn das Gateway selbst noch auf den Bus schreibt** – das
> ist genau der Fall, in dem der Schreibzähler zählt. Beide Wege zusammen erzeugen
> also **keine** doppelten Schreibvorgänge.

---

## Nächster Schritt

| Sie möchten … | Lesen Sie |
|---|---|
| das Gateway als Add-on installieren | [HA-Add-on](07-ha-addon.md) |
| die Web-Oberfläche nutzen | [Bedienung](06-bedienung.md) |
| eigene Automatisierungen bauen | [API](10-api.md) |
