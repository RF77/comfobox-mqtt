---
title: Anschluss
description: Adapter, Verdrahtung, Baudrate und die Erkennung des seriellen Ports
order: 20
icon: pi pi-plug
tags: [hardware, anschluss]
---

# Anschluss

Die ComfoBox wird über einen **USB-RS485-Adapter** angesprochen. Die ComfoBox
selbst hat keinen USB- oder Netzwerkanschluss für die Kommunikation.

---

## Adapter

### Unterstützte Chips

| Chip | Kennung | Bekannt aus |
|---|---|---|
| **CH340 / CH341** | `1a86:7523` | der ursprüngliche Adapter dieses Projekts |
| **FTDI FT232** | `0403:6001` | sehr verbreitet |
| **Prolific PL2303** | `067b:2303` | günstige No-Name-Adapter |

> **Achtung bei No-Name-Adaptern:** Viele Adapter mit CH340-Chip liefern
> instabile 5-V-Signale. Wenn die Verbindung sporadisch abbricht, testen Sie
> einen anderen Adapter oder einen mit eigener Spannungsversorgung.

### Qualitätsmerkmale

| Merkmal | Empfehlung |
|---|---|
| Anschluss | **Automatische Umschaltung** (A/B) – das erspart Fehlverdrahtung |
| Abschirmung | ja, gegen Störungen durch die Heizungssteuerung |
| Länge | unter 3 m, sonst Qualitätsverlust |
| galvanische Trennung | ideal, verhindert Erdschleifen |

---

## Verdrahtung

```
   USB-RS485-Adapter                ComfoBox ELESTA
  ┌─────────────────┐              ┌──────────────────┐
  │  USB            │              │                  │
  │   │             │              │                  │
  │  [CH340]        │   A  ────────┤  A               │
  │   │             │   B  ────────┤  B               │
  │  [GND]      ───┼─── GND ──────┤  GND (optional)  │
  │   │             │              │                  │
  └─────────────────┘              └──────────────────┘
        │
       USB
```

> **GND wird nur angeschlossen, wenn der Adapter es anbietet.** Bei den meisten
> CH340-Adaptern ist der GND-Pin nicht belastbar relevant; er schützt aber vor
> Potentialdifferenzen zwischen Adapter und ComfoBox.

> **A und B dürfen nicht vertauscht werden.** Die ComfoBox antwortet dann
> überhaupt nicht – auch nicht mit einem Timeout-Fehler, sondern komplett stumm.

### Polarität bei Zweideutigkeit

Sind A und B unklar (z. B. bei Adaptern ohne Beschriftung):

1. A/B **vertauschen**
2. ComfoBox neu starten
3. Funktioniert es? → diePolarität war falsch, so belassen

Der Anschluss ist in den meisten Fällen **verworfen** (A an A), aber nicht immer.

---

## Baudrate

| Einstellung | Wert |
|---|---|
| Baudrate | **38400** |
| Parität | keine |
| Datenbits | 8 |
| Stoppbits | 1 |

Das ist die Werkseinstellung des ELESTA-Controllers **nach** der Umstellung.
Vor der Umstellung spricht er mit 76800.

> **Die Baudrate lässt sich nur am Bedienpanel ändern**, nicht über MQTT oder die
> Web-Oberfläche. Das Gateway kann sie nicht selbst herausfinden – es merkt sich
> nur, ob es eine Antwort bekommt.

---

## Adapter erkennen

### In der Web-Oberfläche

Öffnen Sie **Hilfe → Erste Schritte → Adapter erkennen** oder starten Sie den
Assistenten erneut. Dort sehen Sie alle erkannten Anschlüsse:

| Angezeigt | Bedeutung |
|---|---|
| `/dev/ttyUSB0` | Adapter, flüchtiger Name – kann sich beim Neustart ändern |
| `/dev/serial/by-id/usb-…` | **stabiler Pfad** – dieser ist besser |
| `/dev/ttyAMA0` | eingebaute serielle Schnittstelle des Systems |

> **Immer den `by-id`-Pfad wählen**, wenn einer angeboten wird. Er enthält die
> Seriennummer des Adapters und überlebt Neustarts sowie das Umstecken an einen
> anderen USB-Port.

### Auf der Kommandozeile (Linux)

````bash
# alle seriellen Geräte
ls -l /dev/serial/by-id/ 2>/dev/null
ls -l /dev/ttyUSB* /dev/ttyACM* 2>/dev/null

# USB-Informationen
lsusb | grep -i -E 'ch34|prolific|ftdi'

# belegte Ports (falls "device busy")
sudo fuser -v /dev/ttyUSB0
````

### Wenn der Port nicht auftaucht

| Ursache | Lösung |
|---|---|
| Adapter nicht angesteckt | einstecken, Seite neu laden |
| kein Treiber | CH340/FTDI-Treiber des NAS/HA-Betriebssystems installieren |
| Port ohne Berechtigung | Benutzer zur Gruppe `dialout` hinzufügen |
| Adapter wird nach dem Reboot später erkannt | das Gateway wartet mit Wiederholung – das ist normal |

### Auf einem Synology-NAS: das Prüfskript

Auf einem NAS lässt sich von der Web-Oberfläche aus nicht erkennen, **warum**
kein Port erscheint – Hardware, Kernel-Treiber, Container und Anwendung sehen
von außen gleich aus. Dafür gibt es ein Skript, das nichts verändert und am
Ende genau eine Ursache nennt:

````bash
cd /volume1/homes/<benutzer>
sudo sh usb-serial-check.sh            # Containername ist optional
````

Es prüft der Reihe nach: Ist der Adapter am USB-Bus sichtbar? Hat der Kernel
den passenden Treiber geladen? Gibt es `/dev/ttyUSB*`? Sieht der Container das
Gerät? Darf er es öffnen? Und was meldet das Gateway selbst?

| Befund des Skripts | Bedeutung |
|---|---|
| „Der Adapter ist nicht angeschlossen …" | Hardware, Kabel oder USB-Buchse – nicht der Gateway |
| „Der Adapter STECKT … keine Gerätedatei" | dem NAS fehlt der Treiber (`modprobe ch341`) |
| „NAS hat das Gerät, der Container sieht es nicht" | Durchreichung des Pakets |
| „Container sieht das Gerät, die Anwendung meldet keinen Port" | Fehler im Gateway – Ausgabe an den Entwickler geben |
| „NAS, Container und Anwendung sehen denselben Port" | Erkennung in Ordnung – dann liegt es an der Oberfläche |

---

## Reichweite und Störungen

| Störquelle | Wirkung | Abhilfe |
|---|---|---|
| Heizungssteuerung (Motoren, Relais) | sporadischeTimeouts | geschirmtes Kabel, Abstand zu Leitungen |
| Netzteile ohne Erdung | Masseschleife | galvanische Trennung |
| lange Leitung | zufällige Fehler | kürzeres Kabel oder Adapter mit Repeater |
| parallel geschaltete Verbraucher | falsche Antworten | **nie** den Bus parallel abgreifen |

> **Wichtig:** Auf demselben RS485-Bus darf nur **ein** Gerät senden. Ein
> zweiter Master (z. B. eine direkte BACnet-Anbindung aus Home Assistant) führt
> zu widersprüchlichen Antworten. Nutzen Sie entweder das Gateway **oder** eine
> Direktintegration – nie beides.

---

## Nächster Schritt

| Sie möchten … | Lesen Sie |
|---|---|
| das Gateway installieren | [Erste Schritte](01-erste-schritte.md) |
| den Anschluss prüfen, weil nichts kommt | [Fehlersuche](09-fehlersuche.md) |
