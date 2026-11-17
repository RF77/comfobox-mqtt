---
title: API
description: HTTP-Schnittstelle für eigene Automatisierungen
order: 100
icon: pi pi-code
tags: [api, schnittstelle, automation]
---

# API

Das Gateway bietet eine HTTP-Schnittstelle an. Damit lassen sich eigene
Automatisierungen bauen – ohne MQTT und ohne die Web-Oberfläche.

> **Für die meisten Fälle brauchen Sie das nicht.** Die
> [HACS-Integration](05-home-assistant.md) nutzt genau diese Schnittstelle.

---

## Zugang

| Betriebsweg | Adresse |
|---|---|
| Docker / Synology | `http://<IP>:8081` |
| HA-Add-on | `http://<HA-IP>:8099` |

### Authentifizierung

| Methode | Verwendung |
|---|---|
| **API-Token** | für Maschinen und Automatisierungen (**empfohlen**) |
| Benutzername/Passwort | für das Web-Interface im Browser |

Für den API-Token:

````bash
curl -H "Authorization: Bearer MEIN-TOKEN" http://192.168.1.20:8081/api/status
````

> **Token erzeugen:** Web-Oberfläche → *Konfiguration → API-Token*. Der Token wird
> **einmal** angezeigt. Er wird nur als Hash gespeichert – niemand kann ihn
> wiederherstellen, nur widerrufen.

> **Ein falscher Token wird sofort erkannt:** Liegt ein `Authorization`-Header
> vor, der nicht gültig ist, antwortet der Gateway **immer** mit `401` – auch
> auf den ansonsten offenen Endpunkten. Ohne Header bleibt alles erreichbar
> (LAN-Modus). So erkennt die Home-Assistant-Integration einen falschen Token
> **beim Einrichten** und nicht erst Wochen später.

---

## Endpunkte

| Methode | Pfad | Zweck |
|---|---|---|
| `GET` | `/api/status` | Geräteinfo, Verbindungen, Schreibzähler |
| `GET` | `/api/items` | alle Werte mit Metadaten |
| `GET` | `/api/items/{id}` | ein einzelner Wert |
| `PUT` | `/api/items/{id}` | Wert schreiben |
| `GET` | `/api/health` | Zustandsprüfung |
| `GET` | `/api/config` | Konfiguration lesen |
| `PUT` | `/api/config` | Konfiguration ändern |
| `GET` | `/api/diagnostics/errors` | letzte Fehler |
| `GET` | `/api/diagnostics/bacnet` | Bus-Analyse: läuft ein MS/TP-Token? (nur lesend) |
| `POST` | `/api/diagnostics/bacnet/observe` | Bus-Analyse starten (3 … 300 s) |
| `DELETE` | `/api/diagnostics/bacnet/observe` | Bus-Analyse beenden |
| `GET` | `/api/events` | Live-Werte (SSE) |
| `POST` | `/api/service/restart` | Gateway neu starten |

---

## Werte lesen

````bash
curl -H "Authorization: Bearer MEIN-TOKEN" \
  http://192.168.1.20:8081/api/items
````

Antwort (gekürzt):

````json
[
  {
    "id": "zone.set_point_normal",
    "group": "Zone",
    "name": "SetPointNormal",
    "displayName": "Raumsollwert normal",
    "description": "Raumsollwert Normalbetrieb der Zone",
    "kind": "analog",
    "value": 21.5,
    "valueText": "21,5 °C",
    "unit": "°C",
    "min": 5,
    "max": 30,
    "writable": true,
    "requiresExpertMode": false,
    "stateTopic": "ComfoBox/Zone/SetPointNormal",
    "setTopic": "ComfoBox/Zone/SetPointNormal/Set",
    "lastUpdated": "2026-11-15T10:22:41+00:00",
    "stale": false,
    "enumValues": {}
  }
]
````

| Feld | Bedeutung |
|---|---|
| `id` | stabiler Schlüssel – für Schreibvorgänge verwenden |
| `displayName` | Anzeigename für die Oberfläche |
| `description` | die vollständige Beschreibung aus dem Katalog (Tooltip) |
| `kind` | `analog`, `enum` oder `date` |
| `min` / `max` | gültiger Bereich (bei `null`: kein Limit) |
| `writable` | `true` = schreibbar (Experten-Modus berücksichtigt) |
| `requiresExpertMode` | `true` = erfordert den Experten-Modus |
| `enumValues` | erlaubte Namen als `{ "Zahl": "Name" }` (bei `kind = enum`) |
| `value` | aktueller Wert (`null` = noch nie gelesen) – **maschinenlesbar** |
| `valueText` | Anzeigetext (nur für Menschen – nicht auswerten) |
| `stale` | `true` = der Wert ist älter als drei Abfragezyklen |
| `stateTopic` / `setTopic` | die MQTT-Adressen des Werts (auch bei abgeschaltetem MQTT) |

> **Zahlenformat:** Die API liefert und erwartet **immer** einen Punkt als
> Dezimaltrennzeichen – unabhängig von den Einstellungen des Systems.

---

## Werte schreiben

````bash
curl -X PUT -H "Authorization: Bearer MEIN-TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"value": 22.5}' \
  http://192.168.1.20:8081/api/items/zone.set_point_normal
````

| Antwort | Bedeutung |
|---|---|
| `204` | geschrieben |
| `400` | ungültige Anfrage |
| `401` | Token fehlt oder ist ungültig |
| `403` | Expertenmodus nötig |
| `409` | Tageslimit erreicht oder Wert nicht schreibbar |
| `422` | Wert außerhalb des Bereichs / falscher Typ |
| `502` | die Heizung hat nicht geantwortet |

> **Ein abgelehnter Schreibvorgang schreibt nichts.** Es wird **niemals** ein
> Ersatzwert gesendet.

---

## Live-Werte (SSE)

Für Automatisierungen, die nicht pollen wollen:

````bash
curl -N -H "Authorization: Bearer MEIN-TOKEN" \
  http://192.168.1.20:8081/api/events
````

Beim Verbinden kommt ein **Snapshot** aller Werte, danach bei jeder Änderung ein
einzelnes Ereignis:

````text
event: snapshot
data: [{"id":"zone_setpoint_normal","value":21.5}, …]

event: value
data: {"id":"zone_setpoint_normal","value":22.5,"timestamp":"2026-10-31T10:23:02Z"}
````

| Ereignis | Inhalt |
|---|---|
| `snapshot` | alle aktuellen Werte (einmalig beim Verbinden) |
| `value` | ein geänderter Wert |

> **Bei Abbruch:** Die Verbindung muss neu aufgebaut werden. Automatisierungen
> sollten das mit einer Wartezeit tun, damit kein Ansturm entsteht.

---

## Zustand

````bash
curl -H "Authorization: Bearer MEIN-TOKEN" http://192.168.1.20:8081/api/status
````

````json
{
  "deviceId": "comfobox1",
  "version": "1.0.0",
  "uptimeSeconds": 4523,
  "bacnet": { "connected": true, "port": "/dev/ttyUSB0", "lastError": null },
  "mqtt":   { "enabled": false, "connected": false, "broker": "" },
  "writes": { "count": 17, "limit": 200, "resetsAt": "2026-11-01T00:00:00Z" }
}
````

| Feld | Bedeutung |
|---|---|
| `bacnet.connected` | antwortet die Heizung? |
| `mqtt.enabled` | ist MQTT überhaupt eingeschaltet? |
| `mqtt.connected` | ist der Broker erreichbar? |
| `writes.count` | Schreibvorgänge heute |
| `writes.limit` | Tageslimit |
| `writes.resetsAt` | wann der Zähler zurückgesetzt wird |

---

## Beispiel: Sollwert per Cronjob

````bash
#!/bin/bash
# Setzt den Raumsollwert auf 21.5 °C
curl -s -X PUT \
  -H "Authorization: Bearer MEIN-TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"value": 21.5}' \
  http://192.168.1.20:8081/api/items/zone.set_point_normal
````

> **Beachten Sie das Tageslimit.** Ein Cronjob, der stündlich schreibt, erreicht
> das Limit (200) nach gut acht Tagen. Lesen ist unbegrenzt, Schreiben nicht.

---

## Sicherheit

| Empfehlung | Begründung |
|---|---|
| **Token statt Passwort** | maschinen-tauglich, widerrufbar, kein Sitzungsmanagement |
| Token nur im Gateway-Netz | nicht ins Internet exponieren |
| Für Zugriff von außen | Reverse Proxy mit TLS **und** Authentifizierung |
| Tokens regelmäßig prüfen | widerrufene Tokens wirken sofort |

---

## KI-Assistenten (Home-Assistant-MCP)

Die ComfoBox-Werte lassen sich auch einem KI-Assistenten zugänglich machen.
Es gibt zwei Wege:

| Weg | Was die KI sieht | Voraussetzung |
|---|---|---|
| **Home-Assistant-MCP** | die HA-Entitäten und Dienste | HA-MCP-Server ist eingerichtet |
| **REST-API** | den vollständigen Item-Katalog | API-Token mit passendem Umfang |

### Über Home-Assistant-MCP

Die KI sieht die Entitäten der **HACS-Integration**: Sensoren, Number-, Select-
und Schalter-Entitäten des Geräts **ComfoBox ELESTA**. Die Entitäts-IDs leiten
sich aus Geräte- und Wertnamen ab, z. B.
`number.comfobox_elesta_raumsollwert_normal`. Die **genauen** IDs und die
erlaubten Werte stehen in Home Assistant unter *Einstellungen → Geräte &
Dienste → ComfoBox ELESTA*.

| Was die KI tun soll | Wie |
|---|---|
| Werte lesen | Entitätszustand lesen (Sensor, Number, Select) |
| Zahlenwert schreiben | Dienst `comfobox.set_value` mit `entity_id` und `value` |
| Auswahlwert schreiben | Dienst `comfobox.set_value` mit dem **exakten** Enum-Namen |
| Neu lesen / Neustart | `comfobox.read_all` bzw. `comfobox.restart` |

> **Die KI darf keine Grenzen erfinden.** Min/Max und die erlaubten Enum-Namen
> kommen aus dem Gateway. Ein Wert außerhalb des Bereichs wird abgelehnt – es
> wird **nichts** geschrieben.

### Über die REST-API

Für KI-Werkzeuge, die direkt HTTP sprechen, ist die API der präzisere Weg:
`GET /api/items` liefert alle Werte **mit** `min`, `max`, `enumValues` und
`writable`. Ein Überblick über Entitäten, Felder und Sicherheitsregeln steht in
der Hilfeseite [KI-Assistenten](11-ki.md).

> **Empfehlung:** Für lesende KI-Aufgaben einen Token mit dem Umfang
> **Nur lesen** verwenden. Für schreibende Aufgaben einen eigenen Token mit
> **Lesen und regeln** – er lässt sich jederzeit widerrufen.
