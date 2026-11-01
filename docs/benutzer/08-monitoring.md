---
title: Monitoring und Alarmierung
description: Health überwachen, Fehler erkennen und den Gateway alarmieren
order: 85
icon: bell
tags: [betrieb, monitoring, home-assistant]
---

# Monitoring und Alarmierung

> Diese Seite beschreibt, wie Sie den Zustand des Gateways überwachen und
> sich bei Störungen benachrichtigen lassen.

## 1. Die drei Überwachungspunkte

Das Gateway meldet seinen Zustand auf drei Wegen. Sie sollten **mindestens**
einen davon überwachen.

| Kanal | Abfrage | Was Sie sehen |
|---|---|---|
| Health-Endpunkt | `GET /api/health` | `online`, `degraded` oder `offline` mit einer Meldung je Komponente |
| Status-Endpunkt | `GET /api/status` | Laufzeit, BACnet-Verbindung, MQTT-Broker, Schreibzähler |
| MQTT-Topic | `ComfoBox/Status/Health` | Dasselbe wie der Health-Endpunkt, zusätzlich für Home Assistant |

### Zustände

| Zustand | Bedeutung | Handlungsbedarf |
|---|---|---|
| `online` | Alle Komponenten arbeiten | keiner |
| `degraded` | Eine Komponente arbeitet nicht (z. B. kein Broker, Heizung antwortet nicht) | **ja** – aber **kein** Container-Neustart |
| `offline` | Das Gateway selbst läuft nicht mehr | **ja** – Container prüfen |

> **Wichtig:** Ein `degraded`-Zustand ist **kein** Container-Problem. Ein Neustart würde
> die Heizungssteuerung für Sekunden unterbrechen und das Problem nicht beheben.
> Der Docker-Healthcheck prüft deshalb **nur**, ob der HTTP-Server antwortet.

## 2. Abfrage per Kommandozeile

```bash
# Zustand (mit den Einzelprüfungen)
curl -s http://NAS-IP:8081/api/health | jq .

# Kurzform
curl -s http://NAS-IP:8081/api/health | jq -r .state

# Vollständiger Status
curl -s http://NAS-IP:8081/api/status | jq .

# nur der Schreibzähler
curl -s http://NAS-IP:8081/api/diagnostics/writes | jq .
```

Beispielausgabe von `/api/health`:

```json
{
  "state": "online",
  "message": "Alle Komponenten sind in Ordnung.",
  "checks": [
    {
      "name": "Bacnet",
      "state": "online",
      "message": "Der BACnet-Bus ist erreichbar.",
      "changedAt": "2026-11-01T08:33:43.3671945+00:00"
    }
  ],
  "version": "1.0.0",
  "uptimeSeconds": 3600,
  "startedAt": "2026-11-01T08:33:47.2776305+00:00"
}
```

## 3. Überwachung im Docker

```bash
# Zustand des Containers
docker compose ps

# Health-Status im Detail
docker inspect --format '{{json .State.Health}}' comfobox-gateway

# Log seit dem letzten Start
docker compose logs -f gateway
```

| Anzeige | Bedeutung |
|---|---|
| `Up … (healthy)` | Der HTTP-Server antwortet. |
| `Up … (unhealthy)` | Der HTTP-Server antwortet **nicht** mehr → Neustart prüfen. |
| `Restarting` | Der Container stürzt beim Start ab → `docker compose logs` zeigt die Ursache. |

> Ein `unhealthy` führt **nicht** zu einem automatischen Neustart. Das ist Absicht:
> Ein Neustart würde bei einem nicht erreichbaren USB-Adapter nichts lösen.

## 4. Alarmierung in Home Assistant

Das Gateway veröffentlicht seinen Zustand **retained** auf
`ComfoBox/Status/Health`. Home Assistant macht daraus den Sensor
`sensor.comfobox_health` (bei der HACS-Integration) oder einen eigenen
MQTT-Sensor.

### 4.1 Einfache Benachrichtigung

```yaml
# configuration.yaml
automation:
  - alias: "ComfoBox: Gateway gestört"
    triggers:
      - trigger: state
        entity_id: sensor.comfobox_health
        to: degraded
    actions:
      - action: notify.mobile_app_iphone
        data:
          title: "ComfoBox"
          message: >-
            Das Gateway meldet den Zustand „degraded“.
            {{ state_attr('sensor.comfobox_health', 'message') }}
```

### 4.2 Alarmierung mit Wiederholung und Eskalation

```yaml
automation:
  - alias: "ComfoBox: Gateway gestört (mit Eskalation)"
    triggers:
      - trigger: state
        entity_id: sensor.comfobox_health
        to: degraded
        for: "00:05:00"
    actions:
      - action: notify.mobile_app_iphone
        data:
          title: "ComfoBox: gestört"
          message: >-
            Der Gateway meldet seit 5 Minuten „degraded“.
            Bitte die Diagnose in der Web-UI prüfen.

      - action: notify.persistent_notification
        data:
          title: "ComfoBox"
          message: "Der Gateway ist gestört – siehe Web-UI → Diagnose."

    # Nach 30 Minuten zusätzlich eine Meldung an eine weitere Instanz
      - if:
          - condition: template
            value_template: "{{ states('sensor.comfobox_health') == 'degraded' }}"
        then:
          - action: notify.email
            data:
              title: "ComfoBox: weiterhin gestört"
              message: "Seit 30 Minuten in Zustand degraded."
```

| Baustein | Zweck |
|---|---|
| `for: "00:05:00"` | Erst alarmieren, wenn der Zustand **anhält** – ein kurzer Ausschlag bleibt still. |
| `state_attr(…, 'message')` | Die Meldung des Gateways mitnehmen (z. B. „Mqtt: Der Broker ist nicht erreichbar"). |
| `notify.persistent_notification` | Eine Benachrichtigung in HA selbst, auch ohne Push-App. |

### 4.3 Alarm bei vollständigem Ausfall

Wenn das Gateway **ausfällt**, wird das Topic per **Last Will** auf `offline`
gesetzt – auch ohne dass HA etwas tun muss. Die Entitäten werden dadurch
`unavailable`.

```yaml
  - alias: "ComfoBox: Gateway offline"
    triggers:
      - trigger: state
        entity_id: sensor.comfobox_health
        to: offline
    actions:
      - action: notify.mobile_app_iphone
        data:
          title: "ComfoBox: offline"
          message: >-
            Das Gateway ist nicht mehr erreichbar.
            Container-Status prüfen: docker compose ps
```

### 4.4 Sensor selbst anlegen (ohne HACS-Integration)

```yaml
mqtt:
  sensor:
    - name: "ComfoBox Health"
      state_topic: "ComfoBox/Status/Health"
      availability_topic: "ComfoBox/Status/Health"
      payload_available: "online"
      payload_not_available: "offline"
      device_class: enum
      options: ["online", "degraded", "offline"]
```

## 5. Alarmierung ohne Home Assistant

Wer kein Home Assistant betreibt, kann den MQTT-Topic direkt überwachen:

```bash
# einmalig
mosquitto_sub -h BROKER -t 'ComfoBox/Status/Health' -v

# in einer Schleife, nur bei Zustandswechsel
watch -n 30 "mosquitto_sub -h BROKER -C 1 -t 'ComfoBox/Status/Health' -v"
```

## 6. Backup

| Aspekt | Lösung |
|---|---|
| **Was** | das Volume `comfobox-data` (enthält `config.json` **und** den Schreibzähler) + die Logdateien |
| **Wie** | `deploy/backup/backup.sh` (siehe unten) |
| **Wann** | täglich per Cronjob oder DSM-Aufgabenplaner |
| **Aufbewahrung** | die letzten 14 Sicherungen |
| **Wiederherstellung** | `deploy/backup/restore.sh` |

> **Der Schreibzähler gehört ins Backup.** Er schützt das EEPROM der Heizung
> vor zu vielen Schreibvorgängen. Ohne ihn würde ein Wiederherstellen das
> 24-Stunden-Fenster neu beginnen lassen.

### 6.1 Backup einrichten (manueller Betrieb)

```bash
cd /volume1/docker/comfobox
cp -r /pfad/zu/comfobox-mqtt/deploy/backup ./backup
chmod +x ./backup/*.sh

# einmalig testen
./backup/backup.sh ./backup

# täglich um 03:00 (DSM: Aufgabenplaner → Geplant: täglich)
./backup/backup-cron.sh
```

### 6.2 Wiederherstellung

```bash
# 1. Container stoppen (das Skript macht das selbst)
# 2. Restore ausführen
./backup/restore.sh ./backup/comfobox-backup-2026-11-01-030000.tar.gz

# 3. Prüfen
curl -s http://localhost:8081/api/health
```

| Was passiert | Warum |
|---|---|
| Der Container wird gestoppt | Sonst schreibt er während der Wiederherstellung in das Volume. |
| Das Volume wird **vollständig** geleert und ersetzt | Keine Altlasten aus dem vorherigen Zustand. |
| Der Schreibzähler wird mit zurückgeschrieben | Das 24-h-Fenster bleibt unverändert. |
| Der Container wird neu gestartet | Er lädt die wiederhergestellte Konfiguration. |

## 7. Wiederherstellung testen

Ein Backup, das nie getestet wurde, ist kein Backup. Der Test dauert zwei
Minuten:

```bash
# 1. Aktuellen Zustand merken
curl -s http://localhost:8081/api/diagnostics/writes

# 2. Backup erstellen
./backup/backup.sh ./backup

# 3. Zählerdatei absichtlich löschen (Gegenprobe)
docker run --rm -v comfobox-data:/data alpine rm -f /data/write-counter.json

# 4. Restore
./backup/restore.sh ./backup/comfobox-backup-<Zeitstempel>.tar.gz

# 5. Kontrolle: der Zähler muss wieder da sein
curl -s http://localhost:8081/api/diagnostics/writes
```

## 8. Fehlerbilder

| Symptom | Ursache | Behebung |
|---|---|---|
| `degraded`, Meldung „Mqtt: …“ | Der Broker ist nicht erreichbar | Broker-Adresse und Port prüfen, Netzwerk testen |
| `degraded`, Meldung „Bacnet: …“ | Die Heizung antwortet nicht | Adapter prüfen, Baudrate (38400), Kabel |
| `offline` | Der Container läuft nicht | `docker compose ps`, `docker compose logs` |
| `unhealthy` | Der HTTP-Server antwortet nicht | Neustart: `docker compose restart gateway` |
| Schreibvorgänge werden abgelehnt | Das 24-h-Limit ist erreicht | Werte prüfen; Limit ggf. bewusst erhöhen |
| Die Entitäten werden `unavailable` | `ComfoBox/Status/Health` ist nicht `online` | Broker-Verbindung und Gateway-Status prüfen |

## 9. Weiterführende Seiten

- [Fehlersuche](09-fehlersuche.md) – die häufigsten Symptome und ihre Ursache
- [Synology](08-synology.md) – Betrieb auf dem NAS
- [API](10-api.md) – alle Endpunkte im Detail
- [Home Assistant](05-home-assistant.md) – Einbindung und Entitäten
