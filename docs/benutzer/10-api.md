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
    "id": "zone_setpoint_normal",
    "group": "Zone",
    "name": "SetPointNormal",
    "description": "Raumsollwert Normal",
    "valueType": "float",
    "unit": "°C",
    "isReadOnly": false,
    "min": 5,
    "max": 35,
    "value": 21.5,
    "lastUpdated": "2026-10-31T10:22:41Z",
    "isStale": false
  }
]
````

| Feld | Bedeutung |
|---|---|
| `id` | stabiler Schlüssel – für Schreibvorgänge verwenden |
| `valueType` | `float`, `enum` oder `bool` |
| `min` / `max` | gültiger Bereich (bei `null`: kein Limit) |
| `isReadOnly` | `true` = nur lesbar |
| `enumValues` | erlaubte Namen (bei `valueType = enum`) |
| `value` | aktueller Wert (`null` = noch nie gelesen) |
| `isStale` | `true` = der Wert ist älter als drei Abfragezyklen |

> **Zahlenformat:** Die API liefert und erwartet **immer** einen Punkt als
> Dezimaltrennzeichen – unabhängig von den Einstellungen des Systems.

---

## Werte schreiben

````bash
curl -X PUT -H "Authorization: Bearer MEIN-TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"value": 22.5}' \
  http://192.168.1.20:8081/api/items/zone_setpoint_normal
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
  "mqtt":   { "connected": false, "broker": "core-mosquitto" },
  "writes": { "count": 17, "limit": 200, "resetsAt": "2026-11-01T00:00:00Z" }
}
````

| Feld | Bedeutung |
|---|---|
| `bacnet.connected` | antwortet die Heizung? |
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
  http://192.168.1.20:8081/api/items/zone_setpoint_normal
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
