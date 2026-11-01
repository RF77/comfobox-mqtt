# Konfiguration & Secrets

## 1. Konfigurationsquellen und Präzedenz

```
appsettings.json            (im Image, Defaults)
        ↓
appsettings.{Environment}.json  (optional, z. B. Production)
        ↓
Umgebungsvariablen COMFOBOX_*   (überschreiben)
        ↓
config.json                  (vom Assistenten geschrieben – höchste Priorität)
```

> **Warum hat die Datei Vorrang?** Passwörter in Umgebungsvariablen landen
> sichtbar in `docker inspect`. Die ausdrückliche Wahl des Benutzers im
> Assistenten darf außerdem nicht von einer Umgebungsvariable überschrieben
> werden. Umgesetzt in `GatewayConfiguration.AddGatewaySources()`
> (`src/ComfoBox.Gateway/Configuration/GatewayConfiguration.cs`).
>
> **Umgebungsvariablen:** `Microsoft.Extensions.Configuration` übersetzt `__`
> (Doppel-Unterstrich) in Hierarchie. Mit dem Präfix `COMFOBOX_` wird
> `COMFOBOX_Gateway__BaseTopic` zu `Gateway:BaseTopic`.

## 2. Environment-Variable-Schema

`Microsoft.Extensions.Configuration` übersetzt `__` (Doppel-Unterstrich) in Hierarchie:

| Variable | Abschnitt | Schlüssel | Beispiel |
|---|---|---|---|
| `COMFOBOX_Gateway__BaseTopic` | Gateway | BaseTopic | `ComfoBox` |
| `COMFOBOX_Gateway__ExpertMode` | Gateway | ExpertMode | `true` |
| `COMFOBOX_Gateway__PollIntervalSeconds` | Gateway | PollIntervalSeconds | `30` |
| `COMFOBOX_Gateway__MaxNumberOfWritesPer24h` | Gateway | MaxNumberOfWritesPer24h | `200` |
| `COMFOBOX_Gateway__RestartEnabled` | Gateway | RestartEnabled | `true` |
| `COMFOBOX_Bacnet__Port` | Bacnet | Port | `/dev/ttyUSB0` |
| `COMFOBOX_Bacnet__Baudrate` | Bacnet | Baudrate | `38400` |
| `COMFOBOX_Bacnet__MasterId` | Bacnet | MasterId | `1` |
| `COMFOBOX_Mqtt__Brokers__0__Host` | Mqtt | Brokers[0].Host | `192.168.1.10` |
| `COMFOBOX_Mqtt__Brokers__0__Port` | Mqtt | Brokers[0].Port | `1883` |
| `COMFOBOX_Mqtt__Brokers__0__Username` | Mqtt | Brokers[0].Username | `comfobox` |
| `COMFOBOX_Mqtt__Brokers__0__Password` | Mqtt | Brokers[0].Password | `••••` |
| `COMFOBOX_Mqtt__Brokers__0__UseTls` | Mqtt | Brokers[0].UseTls | `false` |
| `COMFOBOX_Mqtt__EnableDiscovery` | Mqtt | EnableDiscovery | `true` |
| `COMFOBOX_Mqtt__DiscoveryPrefix` | Mqtt | DiscoveryPrefix | `homeassistant` |
| `COMFOBOX_Mqtt__EmbedBroker` | Mqtt | EmbedBroker | `false` |
| `COMFOBOX_Logging__Level` | Logging | Level | `Information` |
| `COMFOBOX_Logging__Directory` | Logging | Directory | `/var/log/comfobox` |
| `COMFOBOX_Logging__RingBuffer__Capacity` | Logging | RingBuffer.Capacity | `1000` |
| `COMFOBOX_Logging__RingBuffer__MinimumLevel` | Logging | RingBuffer.MinimumLevel | `Information` |
| `COMFOBOX_Gateway__DeviceId` | Gateway | DeviceId | `comfobox1` |
| `COMFOBOX_Gateway__RestartEnabled` | Gateway | RestartEnabled | `true` |
| `COMFOBOX_Api__RequireAuthentication` | Api | RequireAuthentication | `false` |
| `COMFOBOX_Api__SuggestedUiPort` | Api | SuggestedUiPort | `8081` |
| `COMFOBOX_Api__BacnetTestTimeoutSeconds` | Api | BacnetTestTimeoutSeconds | `10` |
| `COMFOBOX_Api__MqttTestTimeoutSeconds` | Api | MqttTestTimeoutSeconds | `10` |

## 3. Vollständige `appsettings.json` (Referenz)

````json
{
  "Gateway": {
    "BaseTopic": "ComfoBox",
    "DeviceId": "comfobox1",
    "PollIntervalSeconds": 60,
    "UseMqttRetain": true,
    "ExpertMode": false,
    "MaxNumberOfWritesPer24h": 200,
    "RestartEnabled": true,
    "DataDirectory": "/var/lib/comfobox"
  },
  "Bacnet": {
    "Port": "/dev/ttyUSB0",
    "Baudrate": 38400,
    "ClientId": 3,
    "MasterId": 1,
    "ResponseTimeoutMs": 1000,
    "Retries": 3,
    "ItemTimeoutMs": 3000,
    "ReconnectDelaySeconds": 5
  },
  "Mqtt": {
    "EnableDiscovery": true,
    "DiscoveryPrefix": "homeassistant",
    "EmbedBroker": false,
    "EmbeddedBrokerPort": 1883,
    "PublishQoS": 0,
    "SubscribeQoS": 1,
    "Brokers": [
      {
        "Host": "localhost",
        "Port": 1883,
        "Username": "",
        "Password": "",
        "UseTls": false,
        "AllowUntrustedCertificates": false
      }
    ]
  },
  "Api": {
    "RequireAuthentication": false,
    "ApiKey": ""
  },
  "Logging": {
    "Level": "Information",
    "Directory": "/var/log/comfobox",
    "RingBuffer": {
      "Capacity": 1000,
      "MinimumLevel": "Information"
    }
  },
  "Api": {
    "RequireAuthentication": false,
    "SuggestedUiPort": 8081,
    "BacnetTestTimeoutSeconds": 10,
    "MqttTestTimeoutSeconds": 10
  }
}
````

> **Der Ringpuffer** hält die letzten 1000 Log-Einträge im Speicher, damit die Web-UI
> sie anzeigen kann. `Capacity: 0` schaltet ihn ab.
> → [logging.md §8](../02-modernisierung/logging.md#8-konfiguration)

## 4. Secrets

| Secret | Quelle | Bemerkung |
|---|---|---|
| MQTT-Broker-Passwort | `.env` auf dem NAS / `docker secret` | **nie** ins Image, **nie** ins Git |
| API-Key (falls Auth aktiv) | `.env` | |
| TLS-Zertifikat (falls Broker TLS nutzt) | Volume-Mount | PEM-Datei, `chmod 600` |

### `.env` auf der DS923+

````bash
cd /volume1/docker/comfobox
cat > .env <<'EOF'
MQTT_PASSWORD=8f3c1a9e7b2d4f6a0c5e8b1d3f7a9c2e
API_KEY=…
EOF
chmod 600 .env
````

In `compose.yaml`:

````yaml
services:
  gateway:
    env_file:
      - .env
    environment:
      COMFOBOX_Mqtt__Brokers__0__Password: "${MQTT_PASSWORD}"
      COMFOBOX_Api__ApiKey: "${API_KEY}"
````

> `.env` muss in `.gitignore` stehen. Für die Entwicklungs-Umgebung existiert
> `.env.example` mit Platzhaltern.

## 5. Authentifizierung der Web-UI

| Modus | Konfiguration | Einsatz |
|---|---|---|
| **Aus** (Standard) | `Api:RequireAuthentication = false` | Nur im LAN, NAS-Firewall regelt den Zugriff |
| **API-Key** | `Api:RequireAuthentication = true`, `Api:ApiKey` gesetzt | `Authorization: Bearer <key>` |
| **Reverse-Proxy** | nginx mit Basic-Auth | empfohlen, wenn das NAS im Internet erreichbar ist |

> **Empfehlung:** Die UI **nicht** ins Internet exponieren. Wenn nötig: VPN (WireGuard/Tailscale)
> oder nginx mit Auth vor nginx auf der DS923+.

## 6. Persistente Daten

| Pfad im Container | Zweck | Volume |
|---|---|---|
| `/var/lib/comfobox` | Schreibzähler (24-h-Fenster) | `comfobox-data` |
| `/var/lib/comfobox/config.json` | Konfiguration des Assistenten, Admin-Hash, API-Token (Modus 0600) | `comfobox-data` |
| `/var/log/comfobox` | JSON-Log-Dateien | `./logs` (Bind-Mount) |

> **Wichtig:** Die `config.json` muss im **gleichen** Volume liegen wie
> `/var/lib/comfobox`, sonst ist die UI-Konfiguration nach einem Container-Neustart weg.
> Der Schreibzähler (`write-counter.json`) liegt dort ebenfalls.

## 7. Backup & Restore

Die Skripte liegen in [`deploy/backup/`](../../deploy/backup/):

| Skript | Aufgabe |
|---|---|
| `backup.sh` | sichert das Volume `comfobox-data` (inkl. **Schreibzähler**) + die Logs |
| `restore.sh` | stellt ein Archiv wieder her (stoppt den Container, schreibt das Volume zurück, startet neu) |
| `backup-cron.sh` | Wrapper für den täglichen Cronjob (mit Protokoll und optionaler E-Mail) |

````bash
# Backup (täglich per Cronjob oder DSM-Aufgabenplaner)
cd /volume1/docker/comfobox
./backup/backup.sh ./backup

# Restore
./backup/restore.sh ./backup/comfobox-backup-2026-11-01-030000.tar.gz
curl -s http://localhost:8081/api/health
````

| Was | Warum |
|---|---|
| `comfobox-data/` (Volume) | **Schreibzähler** – sonst beginnt das 24-h-Fenster neu; dazu die `config.json` (Passwort-Hash, API-Token) |
| `logs/` | die JSON-Protokolle für die Fehlersuche |
| `.env` | MQTT-Passwort, API-Key |
| `mosquitto/config/` | Broker-Konfiguration (ACLs, Passwortdatei) |

> **Die Wiederherstellung ist getestet** – siehe
> [Monitoring & Alarmierung §7](../benutzer/08-monitoring.md#7-wiederherstellung-testen).

## 8. Konfigurationsänderungen zur Laufzeit

| Wert | Zur Laufzeit änderbar? | Mechanismus |
|---|---|---|
| `Gateway:ExpertMode` | ✅ | `PUT /api/config` |
| `Gateway:PollIntervalSeconds` | ✅ | `PUT /api/config` |
| `Gateway:MaxNumberOfWritesPer24h` | ✅ | `PUT /api/config` |
| `Gateway:UseMqttRetain` | ✅ | `PUT /api/config` |
| `Bacnet:Port` | ❌ | Neustart nötig |
| `Bacnet:Baudrate` | ❌ | Neustart nötig |
| `Mqtt:Brokers` | ❌ | Neustart nötig |
| `Mqtt:EnableDiscovery` | ✅ | `PUT /api/config` → Discovery wird neu gesendet |
| `Gateway:RestartEnabled` | ❌ | Neustart nötig |

> **Sicherheitsregel:** Alle über die API vorgenommenen Änderungen werden **protokolliert**
> (mit Quelle: `api` / `mqtt`). Aktivieren von `ExpertMode` erfordert zusätzlich eine
> Bestätigung.
