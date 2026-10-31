---
title: MQTT
description: Broker einrichten, Topics verstehen und Werte senden
order: 40
icon: pi pi-send
tags: [mqtt, broker]
---

# MQTT

Mit MQTT bekommen Sie die Werte in beliebige Systeme – Home Assistant, Grafana,
Node-RED, ioBroker. Für die reine Web-Oberfläche brauchen Sie **keinen** Broker.

---

## Broker einrichten

### Eigenen Broker mitbringen

Im Assistenten unter Schritt 3 gibt es die Option **Eigenen Broker mitbringen**.
Damit startet das Gateway einen eigenen MQTT-Broker im selben Docker-Netz.

| Feld | Wert |
|---|---|
| Host | `mosquitto` (der interne Name) |
| Port | `1883` |
| Benutzer/Passwort | frei wählbar |

> **Ab wann?** Der Broker startet zusammen mit dem Gateway. Nach *Übernehmen und starten*
> ist er unter `mosquitto:1883` erreichbar.

### Externen Broker verwenden

| Feld | Bedeutung |
|---|---|
| **Host** | Adresse des Brokers, z. B. `192.168.1.10` oder `homeassistant` |
| **Port** | `1883` (unverschlüsselt) oder `8883` (TLS) |
| **Benutzer/Passwort** | falls der Broker eine Anmeldung verlangt |
| **TLS** | Schalter für verschlüsselte Verbindung |

### Ist mein Broker erreichbar?

Der Assistent prüft das für Sie: nach dem Eintragen auf **Verbindung testen** klicken.
Das Ergebnis sagt klar, was fehlt:

| Meldung | Bedeutung |
|---|---|
| „Broker erreichbar, Anmeldung erfolgreich" | alles in Ordnung |
| „Broker nicht erreichbar" | Adresse, Port oder Firewall prüfen |
| „Anmeldung fehlgeschlagen" | Benutzername oder Passwort falsch |
| „TLS-Verhandlung fehlgeschlagen" | Port passt nicht zur TLS-Einstellung |

---

## Topics

Alle Werte werden unter einem gemeinsamen Präfix veröffentlicht – standardmäßig
`ComfoBox`. Das Präfix ist in der Konfiguration änderbar.

````
ComfoBox/
├── Connected                     1 oder 0
├── Version                       Ver 1.0.0
├── Special/NumberOfWritesPer24h  42
├── Status/…                      Zustand, Fehler, Uptime
├── Command/Restart               Neustart auslösen
└── {Gruppe}/{Name}
    ├── {Name}                    Wert (mit "behalten")
    ├── {Name}/Set                Wert schreiben
    ├── {Name}/AsNumber           Enum als Zahl
    └── {Name}/AsNumber/Set       Enum als Zahl schreiben
````

| Gruppe | Inhalt |
|---|---|
| `Zone` | Zonen der Lüftungsanlage (Betriebsart, Sollwerte, Zeiten) |
| `States` | Zustände (Betriebsstunden, Störungen, Typen) |
| `Warmwater` | Warmwasser (Sollwert, Sofortaufheizung) |
| `Config` | Konfigurationswerte (nur mit Expertenmodus) |
| `Tests` | Testwerte (nur mit Expertenmodus) |

> **Die vollständige Liste aller 127 Topics** steht in der
> [Topic-Referenz](https://github.com/RF77/comfobox-mqtt/blob/main/docs/topics.md).

### Werte lesen

Ein einfaches Abo:

````bash
mosquitto_sub -h 192.168.1.10 -t 'ComfoBox/Zone/SetPointNormal' -v
````

Ausgabe:

````
21.5
````

### Werte schreiben

````bash
mosquitto_pub -h 192.168.1.10 -t 'ComfoBox/Zone/SetPointNormal/Set' -m '22.5'
````

> **Zahlenformat:** Der Wert muss einen **Punkt** als Dezimaltrennzeichen verwenden –
> auch auf einem deutschen System. `22.5` ist richtig, `22,5` wird abgelehnt.
>
> **Grund:** Die Werte werden nach einem festen Format geschrieben, unabhängig von
> den Einstellungen des Betriebssystems. So funktioniert eine Automatisierung
> auf jedem Rechner gleich.

### Aufzählungswerte (Enums)

Manche Werte sind nicht Zahlen, sondern Namen:

````bash
# lesen
mosquitto_sub -h 192.168.1.10 -t 'ComfoBox/Zone/OperationMode' -v
# → Normal

# schreiben
mosquitto_pub -h 192.168.1.10 -t 'ComfoBox/Zone/OperationMode/Set' -m 'Reduced'
````

| Aspekt | Regel |
|---|---|
| Namen | **exakt** wie angezeigt, inkl. Groß-/Kleinschreibung |
| Zahlen | zusätzlich über `…/AsNumber` verfügbar |
| Ungültiger Name | wird **abgelehnt** – es wird **kein** Ersatzwert geschrieben |

> **Warum wird kein Ersatzwert geschrieben?**
> Ein stillschweigend gesetzter Nullwert könnte die Lüftungsanlage in einen
> undefinierten Zustand bringen. Besser: eine klare Fehlermeldung.

---

## Zustand und Fehler

| Topic | Inhalt |
|---|---|
| `ComfoBox/Connected` | `1` = verbunden, `0` = getrennt |
| `ComfoBox/Status/Health` | `online`, `degraded` oder `offline` |
| `ComfoBox/Status/Errors` | letzter Fehler (als JSON) |
| `ComfoBox/Special/NumberOfWritesPer24h` | Anzahl der Schreibvorgänge heute |

> **Zustandstände:**
>
> | Zustand | Bedeutung |
> |---|---|
> | `online` | alles funktioniert |
> | `degraded` | die Heizung ist erreichbar, aber etwas stimmt nicht (z. B. kein Broker) |
> | `offline` | das Gateway läuft nicht oder der Anschluss ist weg |
>
> `offline` wird auch gesendet, wenn das Gateway unerwartet abstürzt – dafür sorgt
> ein sogenannter „Letzter Wille" im Broker.

### Schreibvorgänge zählen

Jeder Schreibvorgang auf die Heizung zählt gegen ein Tageslimit (Standard: 200).
Der Zähler wird **persistent** gespeichert – ein Neustart setzt ihn **nicht** zurück.

> **Warum?** Jeder Schreibvorgang verändert einen Speicher in der Heizung. Zu viele
> Schreibvorgänge beschädigen ihn irgendwann. Das Limit schützt die Elektronik.

---

## Neustart aus Home Assistant

Mit einem Klick:

````yaml
# als MQTT-Befehl
service: mqtt.publish
data:
  topic: "ComfoBox/Command/Restart"
  payload: ""
````

Oder Sie legen in Home Assistant eine Schaltfläche an, die genau diesen Befehl sendet.
