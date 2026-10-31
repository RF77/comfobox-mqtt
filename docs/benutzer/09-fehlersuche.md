---
title: Fehlersuche
description: Symptom, Ursache und Lösung für die häufigsten Probleme
order: 90
icon: pi pi-wrench
tags: [fehler, hilfe]
---

# Fehlersuche

Diese Seite ist nach **Symptom** sortiert. Suchen Sie Ihr Symptom – nicht die
Ursache.

---

## Die ComfoBox antwortet nicht

**Symptom:** Im Assistenten schlägt der Verbindungstest fehl (`no_iam`). Oder alle
Werte bleiben leer.

### 1. Baudrate prüfen

Die ComfoBox muss auf **38400 Baud** stehen und **neu gestartet** sein. Der
Werksthypoten ist 76800.

| Prüfen | Wie |
|---|---|
| Baudrate | am Bedienpanel der ComfoBox unter *Service → Expertenebene* |
| Neustart | nach der Änderung **zwingend** neu starten |

> Die Baudrate wird nur beim Start gelesen. Ohne Neustart bleibt die ComfoBox bei 76800.

### 2. Verdrahtung prüfen

| Kabel | Anschluss |
|---|---|
| A | A |
| B | B |

**A und B vertauscht** ist der zweithäufigste Fehler. Die ComfoBox antwortet dann
**gar nicht**.

### 3. Adapter prüfen

| Prüfung | Befehl |
|---|---|
| Adapter sichtbar? | `ls -l /dev/serial/by-id/` |
| Belegt? | `sudo fuser -v /dev/ttyUSB0` |
| USB-Gerät da? | `lsusb | grep -i ch34` |

### 4. Zweiter Master?

Wenn eine **direkte BACnet-Anbindung** aus Home Assistant aktiv ist, konkurriert sie
mit dem Gateway um den Bus. Das Gateway verliert dann das Rennen.

> **Es darf nur einen geben.** Gateway **oder** Direktintegration.

---

## Keine seriellen Ports in der Liste

**Symptom:** Das Dropdown im Assistenten ist leer.

| Ursache | Lösung |
|---|---|
| Adapter nicht eingesteckt | einstecken, *Erneut suchen* klicken |
| Kein Treiber | CH340-/FTDI-Treiber des Betriebssystems installieren |
| Falscher Port (Batterie, Serial-Konverter) | andere USB-Buchse |

> **Der Adapter wird manchmal erst später erkannt** – besonders nach einem Neustart
> des NAS. Das Gateway wartet von allein und verbindet sich, sobald der Port da ist.

---

## Permission denied

**Symptom:** Das Gateway startet, aber keine Werte kommen. Im Protokoll:
`Access denied` oder `UnauthorizedAccessException`.

**Lösung:** Der Benutzer im Container braucht die Gruppe `dialout`.

| Betrieb | Prüfung |
|---|---|
| Docker | `group_add: ["dialout"]` in der Compose-Datei |
| Synology | die USB-Regel des Pakets setzt die Rechte (siehe `99-comfobox-serial.rules`) |
| HA-Add-on | der Supervisor mappt den Port mit passenden Rechten |

> Auf DSM hat die Gruppe `dialout` üblicherweise die **GID 20**. Im Zweifel die
> numerische GID eintragen.

---

## Device or resource busy

**Symptom:** Der Port ist belegt.

**Ursache:** Ein anderer Prozess hält den Anschluss offen – z. B. ein
`serial-getty`, ein ModemManager oder ein zweites Gateway.

````bash
sudo fuser -v /dev/ttyUSB0
````

> **Achtung:** Läuft bereits ein altes Gateway (z. B. der Mono-Dienst auf einem
> Raspberry Pi), muss **eines** davon abgeschaltet werden. Zwei Gateway auf einem
> Bus sind genauso problematisch wie zwei Direktintegrationen.

---

## Home Assistant: Entitäten bleiben unavailable

| Ursache | Prüfung | Lösung |
|---|---|---|
| Gateway nicht erreichbar | `http://<IP>:8081/api/health` | Netzwerk, Firewall, Port |
| Falscher Port in der Integration | Add-on: `8099`, Docker/SPK: `8081` | korrigieren |
| Falscher Token | – | neuen Token erzeugen und eintragen |
| Broker-Discovery, aber kein Broker | Broker-Eintrag im Gateway | Broker eintragen oder Integration nutzen |

> **Abstand halten:** Ein `unavailable` ist **besser** als ein stehen gebliebener
> Wert. Ein alter Sollwert, der noch angezeigt wird, ist gefährlicher.

---

## Home Assistant: Werte sind da, aber Schreiben geht nicht

| Meldung | Ursache | Lösung |
|---|---|---|
| `write_limit_exceeded` | Tageslimit erreicht | Zähler prüfen; warten oder Limit erhöhen |
| `expert_mode_required` | Expertenmodus aus | Expertenmodus einschalten (mit Vorsicht) |
| `value_out_of_range` | Wert außerhalb Min/Max | gültigen Bereich aus der Entität nehmen |
| `read_only` | Wert ist nicht schreibbar | – |
| `invalid_type` | falscher Typ (Text für eine Zahl) | Zahl mit **Punkt** senden |

> **Zahlenformat:** `22.5` ist richtig, `22,5` wird abgelehnt – auch auf einem
> deutschen System.

---

## Der Neustart schlägt fehl

| Betrieb | Befehl |
|---|---|
| Docker | `docker compose restart gateway` |
| Synology | `synopkg restart ComfoBoxGateway` |
| HA-Add-on | Add-on neu starten |
| Home Assistant | `comfobox.restart` |

> Bleibt es bei `offline`, prüfen Sie, ob der Broker erreichbar ist. Fehlt der
> Broker, kann sich der Gateway nicht sauber abmelden.

---

## Werte sind veraltet

**Symptom:** In der Web-Oberfläche steht ein alter Wert; in Home Assistant ist die
Entität `unavailable`.

| Ursache | Lösung |
|---|---|
| Adapter gezogen | Adapter einstecken |
| ComfoBox ausgeschaltet | einschalten |
| Port verstellt | Assistent erneut durchlaufen |

> **Der Gateway kennzeichnet veraltete Werte** (älter als drei Abfragezyklen) und
> zeigt sie als „veraltet" an – nicht als aktuellen Wert. Das ist Absicht.

---

## Expertenmodus

Der Expertenmodus gibt zusätzliche Parameter frei, die das Regelverhalten der
Heizung beeinflussen (Kurvenparameter, Grenzwerte).

| Aspekt | Wert |
|---|---|
| Aktivieren | Konfiguration → Schreibschutz → Expertenmodus |
| Bestätigung | ja, mit Rückfrage |
| Risiko | falsche Werte können das Regelverhalten dauerhaft verändern |

> **Nur mit Fachwissen aktivieren.** Ein falsch gesetzter Parameter lässt sich
> unter Umständen nur durch einen Service-Einsatz zurücksetzen.

---

## Alles hilft nicht

| Weg | was Sie tun |
|---|---|
| **Diagnose-Seite** | Fehlerhistorie, Zustandsprüfungen, Rohdaten |
| **Protokoll (Docker)** | `docker compose logs -f gateway` |
| **Protokoll (Synology)** | `ssh synology` → `synopkg` / Container-Manager |
| **Protokoll (HA-Add-on)** | Add-on → Protokoll |
| **Test-Modus** | in der Diagnose alle Rohdaten live mitlesen |

> **Bereiten Sie das vor**, bevor Sie Support fragen: Version des Gateways, Version
> der ComfoBox-Software, Protokoll aus dem Diagnose-Bereich.
