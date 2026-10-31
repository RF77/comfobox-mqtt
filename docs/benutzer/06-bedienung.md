---
title: Bedienung
description: Die Web-Oberfläche nutzen, Werte schreiben, Schreibschutz verstehen
order: 60
icon: pi pi-th-large
tags: [bedienung, web-ui]
---

# Bedienung

Die Web-Oberfläche erreichen Sie unter `http://<IP>:8081/` (bzw. als Panel in
Home Assistant).

---

## Aufbau

| Bereich | Inhalt |
|---|---|
| **Dashboard** | Zustand, Verbindungen, die wichtigsten Werte |
| **Geräte** | alle 127 Werte, lesen und schreiben |
| **Konfiguration** | Grundeinstellungen, Broker, Expertenmodus |
| **Diagnose** | Fehlerhistorie, Verbindungen, Schreibvorgänge |
| **Hilfe** | diese Anleitung |

---

## Dashboard

### Zustandsanzeige

Oben sehen Sie einen farbigen Balken:

| Farbe | Bedeutung | Was tun? |
|---|---|---|
| **grün** | alles in Ordnung | – |
| **gelb** | eingeschränkt | Meldung im Balken lesen |
| **rot** | nicht erreichbar | [Fehlersuche](09-fehlersuche.md) |

### Verbindungskacheln

| Kachel | Bedeutung |
|---|---|
| **BACnet** | Verbindung zur Heizung |
| **Broker** | Verbindung zum MQTT-Broker (nur wenn einer eingetragen ist) |
| **Version** | Version des Gateways |
| **Laufzeit** | wie lange es läuft |
| **Schreibvorgänge** | `17 / 200` heute |

> **Ist „Broker" rot, obwohl Sie die Web-Oberfläche nutzen?**
> Kein Problem – ohne Broker gibt es keine MQTT-Werte. Die Heizung wird trotzdem
> gelesen und bedient.

---

## Geräte (Item-Browser)

Hier sehen Sie **alle** Werte der Anlage, nach Gruppen sortiert.

| Bedienelement | Bedeutung |
|---|---|
| Suchfeld | Volltext über Name, Beschreibung und Topic |
| Filter | „nur schreibbar“, „nur Expertenmodus“, „veraltet“ |
| Spalte **Wert** | der aktuelle Wert |
| Spalte **Bereich** | kleinster und größter erlaubter Wert |
| Spalte **Schreibbar** | Ja/Nein |

### Werte schreiben

| Werttyp | Bedienung |
|---|---|
| Zahl | Zahlenfeld → **Speichern** |
| Auswahl | Dropdown mit den Namen → Auswahl **Speichern** |
| nicht schreibbar | nur Anzeige |

> **Nach dem Schreiben** aktualisiert sich der Wert aus der Heizung – das Gateway
> liest ihn zurück. Stimmt der angezeigte Wert nicht mit dem gewünschten überein,
> wurde der Schreibvorgang abgelehnt (Grund steht in der Meldung).

### Warum ist ein Wert nicht schreibbar?

| Grund | Erklärung |
|---|---|
| Gerät meldet nur | die Heizung liefert diesen Wert nur zurück |
| Expertenmodus aus | Parameter, die nur mit Spezialwissen verändert werden dürfen |

> **Achtung:** Der Expertenmodus erlaubt das Schreiben von Parametern, die das
> Regelverhalten der Heizung beeinflussen. Aktivieren Sie ihn nur, wenn Sie wissen,
> was Sie tun. Bei Rückfragen: [Expertenmodus](09-fehlersuche.md#expertenmodus).

---

## Konfiguration

| Bereich | Einstellung | Wirkung |
|---|---|---|
| Allgemein | MQTT-Basis-Topic | Präfix aller Topics |
| Allgemein | Abfrageintervall | wie oft gelesen wird (Standard 30 s) |
| Schreibschutz | Limit pro 24 h | wie viele Schreibvorgänge erlaubt sind |
| Schreibschutz | Expertenmodus | sensible Parameter freigeben |
| MQTT | Broker | Adresse, Port, Zugangsdaten |
| BACnet | Port, Baudrate | **erfordert Neustart** |
| Diagnose | Neustart, Verbindung neu aufbauen, Protokollstufe | – |

> **Felder mit dem Hinweis „Neustart erforderlich"** werden erst nach einem Neustart
> wirksam. Sie finden einen Knopf dafür direkt auf der Seite.

---

## Diagnose

| Ansicht | Inhalt |
|---|---|
| **Zustandsprüfungen** | jede Einzelprüfung mit Status und Meldung |
| **Fehlerhistorie** | die letzten 50 Fehler, filterbar |
| **Schreibvorgänge** | Zähler, Limit, Zeitraum |
| **Verbindungen** | Broker-Status, Verbindungsabbrüche, Zähler |
| **Rohdaten** | alle Nachrichten live mitlesen |

> **Bei Problemen:** Der schnellste Weg ist, in der Fehlerhistorie nach dem Zeitpunkt
> des Problems zu suchen. Dort steht der genaue Grund.

---

## Der Schreibschutz

Dieses Thema verdient eigene Erklärung, weil es die Heizung schützt.

### Das Tageslimit

| Aspekt | Wert |
|---|---|
| Standard | 200 Schreibvorgänge pro 24 Stunden |
| Zählung | jeder Schreibvorgang, auch der zu einem beliebigen Parameter |
| Speicherung | **persistent** – ein Neustart setzt den Zähler nicht zurück |
| Bei Überschreitung | der Schreibvorgang wird **abgelehnt**, nicht gekürzt |

> **Warum persistent?**
> Der Zweck ist der Schutz der Elektronik in der Heizung. Ein Neustart, der den
> Zähler zurücksetzt, würde diesen Schutz aushebeln.

### Was passiert bei einem abgelehnten Schreibvorgang?

1. Der Wert wird **nicht** geschrieben.
2. Sie bekommen eine klare Meldung.
3. Der alte Wert bleibt aktiv.

> **Es wird niemals ein Ersatzwert geschrieben** – insbesondere **nicht** die Null.
> Ein stillschweigend gesetzter Nullwert könnte die Anlage in einen undefinierten
> Zustand bringen.

### Zähler ansehen

Dashboard → Kachel **Schreibvorgänge**, oder
`ComfoBox/Special/NumberOfWritesPer24h` per MQTT.

---

## Neustart

| Wo | Wie |
|---|---|
| Dashboard | *Gateway neu starten* → Bestätigung |
| Konfiguration | *Gateway neu starten* |
| Home Assistant | `comfobox.restart` oder MQTT-Befehl |
| Kommandozeile | `docker compose restart gateway` |

> **Nach dem Neustart** sind die Werte für etwa 10 Sekunden nicht verfügbar.
> Die Heizung arbeitet in dieser Zeit mit ihrem letzten Sollwert weiter – es
> entsteht **kein** Ausfall der Heizungsregelung.

---

## Hilfe

Über **Hilfe** in der Seitenleiste. Die Anleitung funktioniert auch ohne
Internetverbindung.

| Thema | Seite |
|---|---|
| Installation | [Installation](03-installation.md) |
| Anschluss | [Anschluss](02-anschluss.md) |
| Home Assistant | [Home Assistant](05-home-assistant.md) |
| Fehler | [Fehlersuche](09-fehlersuche.md) |
