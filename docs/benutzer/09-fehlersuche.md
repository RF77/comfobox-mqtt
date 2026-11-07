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

## Das Paket lässt sich nicht installieren

**Symptom:** Beim Hochladen der `.spk` ins Paketzentrum erscheint:

> *„ComfoBox Gateway kann nicht installiert werden, da es mit Root-Rechten
> ausgeführt wird. Pakete mit Root-Rechten können Befehle als Root-Benutzer
> ausführen und die Sicherheit Ihres Systems gefährden."*

**Ursache:** Dem Paket fehlt die Datei `conf/privilege`. Ab DSM 7.0 verlangt
Synology für jedes Paket eine ausdrückliche Rechteangabe – ohne diese Datei
gilt das Paket als Root-Paket und das Paketzentrum verweigert die
Installation.

> **Meistens ist es die falsche Datei:** Auf der NAS liegen oft mehrere
> `.spk` nebeneinander, und im Dateiauswahler wird die **alte** angeklickt.
> Prüfen Sie das so:
>
> ````bash
> tar tf /volume1/pfad/zur/datei.spk | grep conf
> # erwartet: conf/ und conf/privilege
> ````

**Lösung:** Sie brauchen eine neuere Version des Pakets. Prüfen Sie die
verwendete Version auf der [Download-Seite](https://github.com/RF77/comfobox-mqtt/releases);
ab **1.0.0-0002** ist die Datei enthalten.

> **Eigene Handarbeit ist nicht nötig** – das Paket wird mit der Datei
> ausgeliefert. Sollte die Meldung weiterhin kommen, holen Sie den echten
> Fehlercode:
>
> ````bash
> sudo sh dsm-diagnose.sh /volume1/pfad/zur/datei.spk --install
> ````
>
> Das Skript liegt im Repository unter
> `deploy/synology-spk/tools/dsm-diagnose.sh`. Es liefert die JSON-Ausgabe
> von `synopkg install` – dort steht der Fehlercode.

---

## Keine seriellen Ports in der Liste

**Symptom:** Die Port-Auswahl (Konfiguration → BACnet oder Übersicht) ist leer.

| Ursache | Lösung |
|---|---|
| Adapter nicht eingesteckt | einstecken, *Neu suchen* klicken |
| Kein Treiber | CH340-/FTDI-Treiber des Betriebssystems installieren |
| Falscher Port (Batterie, Serial-Konverter) | andere USB-Buchse |

> **Der Adapter wird manchmal erst später erkannt** – besonders nach einem Neustart
> des NAS. Das Gateway wartet von allein und verbindet sich, sobald der Port da ist.
> Sie können aber auch selbst suchen: *Neu suchen* in der Port-Auswahl.

---

## Die ComfoBox antwortet nicht

**Symptom:** Die Übersicht meldet „BACnet nicht verbunden".

| Ursache | Lösung |
|---|---|
| Falscher Port | Port-Auswahl: richtigen Port wählen, *Übernehmen* (wirkt sofort) |
| Falsche Baudrate | Baudrate 38400 einstellen (Werks-Default 76800) |
| ComfoBox steht noch auf 76800 | am Bedienpanel umstellen und neu starten |
| Adapter abgezogen | einstecken, *Neu suchen* klicken |

> **Ein Portwechsel ist sofort wirksam** – ein Neustart des Gateways ist nicht
> nötig.

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
| Container startet nicht (Pull-Fehler) | Das Image `ghcr.io/rf77/comfobox-gateway` ist **privat** – ohne öffentliches Paket kann der Supervisor es nicht ziehen | Image auf „public" stellen (Package → Settings → Change visibility). Prüfen mit `docker pull ghcr.io/rf77/comfobox-gateway:1.0.0` – **Achtung:** ein `401` beim Aufruf von `ghcr.io/v2/…` im Browser ist immer normal und sagt nichts über die Sichtbarkeit aus |
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
| **Diagnose → Protokoll** | die letzten 1000 Log-Einträge, live, filterbar – **der schnellste Weg** |
| **Diagnose-Seite** | Fehlerhistorie, Zustandsprüfungen, Rohdaten |
| **Protokoll (Docker)** | `docker compose logs -f gateway` |
| **Protokoll (Synology)** | `ssh synology` → `synopkg` / Container-Manager |
| **Protokoll (HA-Add-on)** | Add-on → Protokoll |
| **Test-Modus** | in der Diagnose alle Rohdaten live mitlesen |

> **Bereiten Sie das vor**, bevor Sie Support fragen: Version des Gateways, Version
> der ComfoBox-Software, Protokoll aus dem Diagnose-Bereich.

### Was im Protokoll nachsehen

| Symptom im Protokoll | Bedeutung | nächster Schritt |
|---|---|---|
| `no_iam` | die ComfoBox antwortet nicht | [Baudrate prüfen](#1-baudrate-prüfen) |
| `Permission denied` | der Container darf den Port nicht öffnen | [Permission denied](#permission-denied) |
| `Device or resource busy` | ein zweiter Master belegt den Bus | [Ein zweiter Master?](#4-zweiter-master) |
| `Publish failed` | der MQTT-Broker ist nicht erreichbar | [MQTT einrichten](04-mqtt.md) |
| `timed out` | einzelne Werte kommen nicht | Ggf. Baudrate oder Verkabelung |
| `write_limit_exceeded` | das Tageslimit ist erreicht | [Schreibschutz](06-bedienung.md#der-schreibschutz) |

> **Tipp:** Setzen Sie den Filter auf **Level: Warnung oder höher**, um die relevanten
> Zeilen ohne Rauschen zu sehen. Mit **Live** laufen neue Zeilen automatisch ein – Sie
> müssen die Seite nicht neu ladenen.
