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

Es gibt dafür **drei verschiedene Ursachen** – die Meldung im Paketzentrum
ist immer dieselbe.

### Symptom 1: „…da es mit Root-Rechten ausgeführt wird“

> *„ComfoBox Gateway kann nicht installiert werden, da es mit Root-Rechten
> ausgeführt wird. Pakete mit Root-Rechten können Befehle als Root-Benutzer
> ausführen und die Sicherheit Ihres Systems gefährden."*

**Ursache:** Dem Paket fehlt die Datei `conf/privilege`. Ab DSM 7.0 verlangt
Synology für jedes Paket eine ausdrückliche Rechteangabe – ohne diese Datei
gilt das Paket als Root-Paket und das Paketzentrum verweigert die
Installation.

**Lösung:** Sie brauchen eine neuere Version des Pakets. Ab **1.0.0-0002** ist
die Datei enthalten.

### Symptom 2: „…von einem unbekannten Herausgeber veröffentlicht“

**Ursache:** Sie haben das Paket **manuell** installiert (Datei auf die NAS
kopiert). Das DSM kann den Herausgeber nur bei einer **registrierten
Paketquelle** bestimmen.

**Lösung:** Über die Paketquelle installieren –
*Paketcenter → Einstellungen → Paketquellen → Hinzufügen*:

| Feld | Wert |
|---|---|
| Name | `ComfoBox` |
| Speicherort (URL) | <https://github.com/RF77/comfobox-mqtt/releases/download/v1.0.1/packages.json> |

Danach erscheint das Paket unter **Community** und wird von dort installiert.
**Sie müssen dafür keine Datei auf die NAS kopieren.**

### Symptom 3: „Paket kann nicht installiert werden. Versuchen Sie es später erneut…“

**Ursache:** Ein Skript des Pakets oder der DSM-Worker ist gescheitert. Die
Meldung ist **generisch** – sie nennt den Grund nicht. Im Log des DSM steht
dagegen genau, welcher Schritt geklappt hat:

````bash
grep -E "Acquire|failed" /var/log/synopkg.log | tail -10
````

| Zeile | Bedeutung |
|---|---|
| `Acquire docker-project … (fail)` | Der Worker hat das Compose-Projekt nicht angelegt. Meist stimmt die Struktur von `package.tgz` nicht – oder es liegen Reste eines früheren Versuchs herum. |
| `Acquire docker-project … (done)` | Der Worker war in Ordnung – der Fehler liegt in einem Skript. |
| `End postinst ret=[0]` | Die Skripte liefen durch. |

**Den Grund nennt nur das Syslog**, nicht `synopkg.log`. Der Worker schreibt
nach `/var/log/messages`:

````bash
sudo grep -i -E "docker_project|comfobox" /var/log/messages | tail -20
````

Steht dort **gar nichts**, ist der Worker gescheitert, *bevor* er mit Docker
gesprochen hat – dann liegt es am Zustand des Pakets (siehe nächster
Abschnitt) oder an `conf/resource`.

**Lösung:** Holen Sie den echten Fehlercode mit dem Diagnose-Skript (unten).

### Wichtig: „broken“ nach einem Fehlversuch

Schlägt eine Installation fehl, hinterlässt DSM 7.4 das Paket im Status
**„broken“ (150)**. Beim nächsten Versuch versucht DSM dann ein **Update**
statt einer Neuinstallation – und scheitert an *„package does not have
preupgrade / postupgrade script“* (Fehler **261**), obwohl das Paket
in Ordnung ist. Zusätzlich bleiben Verzeichnisse, Container und Netzwerke
des Fehlversuchs liegen – auch die lassen den Worker sofort scheitern.

**Vor dem nächsten Installationsversuch deshalb aufräumen** – ein Befehl
erledigt alles (Paket deinstallieren, Verzeichnisse entfernen, Container und
Netzwerke löschen):

````bash
sudo sh /tmp/dsm-diagnose.sh /tmp/<paket>.spk --reset
````

Das Docker-Volume `comfobox-data` bleibt dabei **erhalten** – dort liegt der
24-h-Schreibzähler (EEPROM-Schutz).

> **Meistens ist es die falsche Datei:** Auf der NAS liegen oft mehrere
> `.spk` nebeneinander, und im Dateiauswahler wird die **alte** angeklickt.
> Prüfen Sie das so:
>
> ````bash
> tar tf /volume1/pfad/zur/datei.spk | grep conf
> # erwartet: conf/ und conf/privilege und conf/resource
> ````

### Den echten Fehlercode holen

Das Paket wird mit allem Nötigen ausgeliefert. Das Diagnose-Skript liegt
**im Release** direkt neben dem `.spk` (`dsm-diagnose.sh`):

````bash
# 1. SSH aktivieren: Control Panel → Terminal & SNMP → SSH-Dienst aktivieren
# 2. Verbinden
ssh admin@<IP-der-NAS>

# 3. Skript und Paket nach /tmp legen (von Ihrem Rechner)
scp dsm-diagnose.sh admin@<IP-der-NAS>:/tmp/
scp ComfoBoxGateway_noarch-dsm7_<version>.spk admin@<IP-der-NAS>:/tmp/

# 4. Diagnose ausführen - räumt zuerst auf und installiert dann
sudo sh /tmp/dsm-diagnose.sh /tmp/ComfoBoxGateway_noarch-dsm7_<version>.spk --install
````

Das Skript zeigt unter anderem:

- die **Struktur von `package.tgz`** (muss auf der obersten Ebene liegen)
- **`conf/resource`** und ob der dort genannte `path` zum Paketinhalt passt
- die **Compose-Datei** und prüft sie mit `docker compose config`
- die **Reste** früherer Versuche (Broken-Status, Container, Netzwerke) und
  räumt sie weg
- den **Fehlercode** aus der JSON-Ausgabe von `synopkg install`
- die **Meldungen des Resource-Workers** aus `/var/log/messages`
- ob ein **anderes Paket auf der NAS** denselben Worker nutzt (Vergleich)

Die vollständige Anleitung steht in
[Synology testen](08b-synology-test.md).

### Symptom 4: „failed to acquire postinst worker“ / „fail compose up“

**Ursache:** Der Container konnte nicht gestartet werden. Das DSM meldet nur
*„fail compose up“* – die eigentliche Docker-Fehlermeldung bleibt
verborgen.

**Das DSM-Log verrät, wie weit es kam:**

````bash
grep -E "Acquire|failed" /var/log/synopkg.log | tail -10
````

| Zeile | Bedeutung |
|---|---|
| `Acquire docker-project … (fail)` | Der Worker hat das Projekt **nicht** angelegt – die Compose-Datei wurde nicht gefunden. |
| `Acquire docker-project … (done)` | Der Worker war in Ordnung – der Fehler liegt im Container-Start. |

**Lösung:** Der Modus `--worker` macht exakt das, was der DSM-Worker macht,
und zeigt die **volle** Docker-Fehlermeldung:

````bash
sudo sh /tmp/dsm-diagnose.sh /tmp/ComfoBoxGateway_noarch-dsm7_<version>.spk --worker
````

Das Skript entpackt das Paket wie das DSM, zieht das Image, prüft die
belegten Ports und führt `docker compose up` aus – mit der vollständigen
Ausgabe. Das Paket wird dabei **nicht** installiert; am Ende wird das Projekt
wieder entfernt.

| Ursache | Kennzeichen in der Ausgabe | Lösung |
|---|---|---|
| Image nicht ziehbar | `unauthorized` / `manifest unknown` | Image-Status prüfen (Schritt 0 in der Testanleitung) |
| Port belegt | `port is already allocated` | Anderen Port verwenden oder den Beleger stoppen |
| Verzeichnis fehlt | `no such file or directory` | Paket deinstallieren und neu installieren |
| Image passt nicht zur Architektur | `no matching manifest` | Image für `linux/amd64` neu bauen |

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
| Container startet nicht (Pull-Fehler) | Das Image `ghcr.io/rf77/comfobox-gateway` ist **privat** – ohne öffentliches Paket kann der Supervisor es nicht ziehen | Image auf „public" stellen (Package → Settings → Change visibility). Prüfen mit `docker pull ghcr.io/rf77/comfobox-gateway:1.0.1` – **Achtung:** ein `401` beim Aufruf von `ghcr.io/v2/…` im Browser ist immer normal und sagt nichts über die Sichtbarkeit aus |
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
