# Synology DS923+

> Anforderung 3: *„Alles als Docker Image, damit ich es auf meiner DS 923+ deployen kann.
> Docker Images können schon auch auf den Serial Port zugreifen?"*

## 1. Kurzantwort

**Ja, Container können auf den Serial Port zugreifen.** Das Device-Node des Hosts
(`/dev/ttyUSB0`) wird per `--device` bzw. `devices:` in den Container durchgereicht. Der Kernel
öffnet das Gerät im Host-Namespace, der Container sieht ein normales Zeichengerät.

Auf der DS923+ (DSM 7.2) geschieht das über den **Container Manager** (Docker 24+).

## 2. Voraussetzungen auf der DS923+

| Voraussetzung | Prüfung | Hinweis |
|---|---|---|
| Container Manager installiert | DSM → Paketzentrum | ab DSM 7.2 enthalten |
| USB-RS485-Adapter (CH340) | — | im Ist-Zustand erprobt; DS923+ hat **keinen** RS485-Port |
| Serial-Treiber vorhanden | `ls /dev/ttyUSB*` per SSH | CH340 ist im DSM-Paket enthalten |
| Docker-Bridge-Netz | `docker network ls` | Standard `bridge` genügt |
| SSH-Zugang | DSM → Terminal & SNMP | für die Einrichtung empfohlen |

> **Hinweis zur Hardware:** Die DS923+ nutzt einen AMD Ryzen R1600 (x86_64, 4 Kerne, 4 GB RAM).
> Ein `linux/amd64`-Image läuft nativ – **kein** `linux/arm64` nötig (anders als bei den
> DS+/DS- mit Realtek). Memory: ~150 MB für den Gateway-Container – kein Problem.

## 3. Schritt-für-Schritt: Serial-Port einrichten

### 3.1 Adapter identifizieren

Per SSH auf der DS923+:

````bash
# Alle seriellen Devices anzeigen
ls -l /dev/ttyUSB* /dev/ttyACM* 2>/dev/null

# Stabilen Pfad ermitteln (by-id ist reboot-stabil!)
ls -l /dev/serial/by-id/
# → /dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0 -> ../../ttyUSB0
# (bei CH340: usb-1a86_USB_Serial-if00-port0)
````

> **Warum `by-id`:** `/dev/ttyUSB0` kann sich nach einem Reboot oder wenn der Adapter
> neu angesteckt wird zu `/dev/ttyUSB1` ändern. `/dev/serial/by-id/…` ist an die
> Seriennummer/Interfaces des Geräts gebunden und **bleibt stabil**.

### 3.2 Rechte & udev-Regel

````bash
# Wer besitzt das Device?
ls -l /dev/ttyUSB0
# crw-rw---- 1 root dialout 188, 0

# Gruppen-ID von dialout ermitteln (für group_add im Compose)
getent group dialout
# dialout:x:20:root,fux
````

Damit der Container-Prozess zugreifen kann, braucht er die Gruppe `dialout` (GID 20 auf DSM):

````yaml
group_add:
  - "20"        # dialout
  # oder: - "dialout"   (Compose unterstützt Gruppennamen)
````

Optional: udev-Regel, damit die Rechte nach jedem Reboot stimmen und der Pfad stabil bleibt:

````
# /etc/udev/rules.d/99-comfobox-serial.rules  (auf dem NAS via SSH)
SUBSYSTEM=="tty", ATTRS{idVendor}=="1a86", ATTRS{idProduct}=="7523", \
  SYMLINK+="comfobox", GROUP="dialout", MODE="0660"
````

````bash
udevadm control --reload-rules && udevadm trigger
ls -l /dev/comfobox
````

> `/dev/comfobox` ist dann noch stabiler als `by-id`, weil die Regel zusätzlich nach
> Vendor/Product filtert.

### 3.3 Störer entfernen

Manche Dienste auf dem NAS beanspruchen USB-Serial-Geräte:

````bash
# Prüfen, ob etwas den Port belegt
sudo fuser -v /dev/ttyUSB0
sudo lsof /dev/ttyUSB0

# Kandidaten (je nach DSM-Version vorhanden):
sudo systemctl stop serial-getty@.service 2>/dev/null
sudo systemctl stop ModemManager.service 2>/dev/null
````

> Wenn ein Host-Prozess den Port exklusiv hält, schlägt `port.Open()` im Container mit
> `Access denied` oder `Device or resource busy` fehl. Der Gateway meldet das dann über
> `Status/Bacnet` → die UI zeigt den Fehler.

## 4. Deployment auf der DS923+

### 4.1 Verzeichnis anlegen

````bash
mkdir -p /volume1/docker/comfobox/mosquitto/config
cd /volume1/docker/comfobox
````

### 4.2 compose.yaml anlegen

Siehe [containerisierung.md §4](containerisierung.md#4-composeyaml-manueller-betrieb). Wichtig auf der DS923+:

| Anpassung | Grund |
|---|---|
| `image:` statt `build:` | Das Image wird aus der Registry gezogen, nicht lokal gebaut |
| `/volume1/docker/comfobox/…` als Pfade | DSM-Bind-Mounts (die Pfade der Compose-Datei sind relativ zum Projektordner) |
| `restart: unless-stopped` | NAS-Neustart → Container startet automatisch |
| `group_add: ["dialout"]` | Rechte auf `/dev/ttyUSB0` |

### 4.3 `.env` für Secrets

````bash
cd /volume1/docker/comfobox
cat > .env <<'EOF'
MQTT_PASSWORD=hier-ein-langes-zufallspasswort
EOF
chmod 600 .env
````

### 4.4 Starten

````bash
docker compose pull
docker compose up -d
docker compose ps
docker compose logs -f gateway
````

Erwartete Ausgabe:

````
comfobox-gateway   Up  (healthy)   0.0.0.0:8081->8080/tcp
comfobox-mosquitto Up              0.0.0.0:1883->1883/tcp
````

### 4.5 Zugriff

| Dienst | URL |
|---|---|
| Web-UI **und** API | `http://<NAS-IP>:8081` (ein Port für beides) |
| API (rein) | `http://<NAS-IP>:8081/api/health` |
| MQTT-Broker | `<NAS-IP>:1883` |

## 5. Container Manager (DSM-GUI) statt SSH

Alternativ ohne SSH:

1. **Container Manager** → **Projekt** → **Erstellen**
2. Name: `comfobox`; Pfad: `/volume1/docker/comfobox`
3. `compose.yaml` aus dieser Dokumentation einfügen
4. **Projekt → Erweiterung → Geräte**: `/dev/serial/by-id/usb-…` → `/dev/ttyUSB0` hinzufügen
   *(alternativ über `devices:` in der compose-Datei)*
5. **Projekt → Erweiterung → Quelle**: `env_file: .env`
6. **Erstellen** → **Starten**

> Der DSM-GUI-Pfad ist einsteigerfreundlicher, hat aber den Nachteil, dass die
> `compose.yaml` nicht versioniert wird. **Empfehlung:** compose.yaml im Git-Repo halten und
> per SSH/CI deployen.

## 6. Betrieb auf der DS923+

### 6.1 Autostart

`restart: unless-stopped` sorgt dafür, dass der Container nach einem NAS-Neustart wieder läuft.

> **Achtung:** Nach einem NAS-Neustart ist der USB-Adapter evtl. noch nicht enumeriert, wenn der
> Container startet. Der Gateway muss das abfangen: Port-Open **retryen**, nicht crashen.
> → Wird in [bacnet-portierung.md §5](../02-modernisierung/bacnet-portierung.md#5-der-serial-port-crash-sicherheit)
> umgesetzt (Retry mit Backoff, Health = `degraded`).

### 6.2 Logrotate / Log-Ausgabe

Serilog schreibt zusätzlich nach `/var/log/comfobox/gateway-*.jsonl` (Mount `./logs`).
Die Rotation ist in Serilog konfiguriert (`RollingInterval.Day`, 14 Dateien).

### 6.3 Updates

````bash
cd /volume1/docker/comfobox
docker compose pull
docker compose up -d
docker image prune -f
````

### 6.4 Backup

| Zu sichern | Pfad | Wichtig? |
|---|---|---|
| `appsettings.json` | Projektordner | **ja** |
| `.env` | Projektordner | **ja** (MQTT-Passwort) |
| `mosquitto/config/mosquitto.conf` | Projektordner | **ja** |
| `mosquitto/data/` (Volume) | Docker-Volume | ja (retained messages) |
| `comfobox-data/` (Volume) | Docker-Volume | **ja** (Schreibzähler!) |
| `logs/` | Projektordner | nein |

> **Wichtig:** Der Schreibzähler liegt im Volume `comfobox-data`. Wird er beim Update gelöscht,
> beginnt das 24-h-Fenster neu → mehr EEPROM-Schreibvorgänge als geplant.
> `docker compose down` (ohne `-v`) lässt Volumes intakt.

## 7. Performance auf der DS923+

| Aspekt | Erwartung |
|---|---|
| CPU | < 5 % im Normalbetrieb (Polling alle 60 s) |
| RAM | ~120–180 MB (Gateway inkl. UI), ~20 MB (mosquitto) |
| Polling-Laufzeit | ~2–5 s für 127 Items (MSTP, 38400 Baud) |
| IO | vernachlässigbar |

> **Hinweis:** Bei 38400 Baud und `MaxInfoFrames = 1` ist die serielle Strecke der Engpass.
> Jedes Item braucht mindestens 2 Frames (Request + ACK + Response). 127 Items ≈ 380 Frames ×
> ~30 Bit-Zeit ≈ 4 s. Das ist der Grund für die Priorisierung
> ([ui-angular-optimus/../uebersicht-und-zielbild](../02-modernisierung/uebersicht-und-zielbild.md#51-lesen-poll)).

## 8. Alternative zum USB-Adapter: TCP-Serial-Gateway

Falls der USB-Adapter am NAS **nicht** zuverlässig funktioniert, kann ein **serielles
TCP-Gateway** (z. B. USR-TCP232, ELI825, oder ein ESP32 mit `SerialToMqtt`/`s2a` firmware)
zwischen ComfoBox und NAS geschaltet werden:

````
ComfoBox ──RS485──► TCP-Serial-Gateway (LAN) ──TCP:5432──► NAS/Container
````

Dann konfiguriert der Gateway den **TCP-Transport** statt SerialPort:
`Bacnet:Transport: "tcp"`, `Bacnet:TcpHost: "192.168.1.50"`, `Bacnet:TcpPort: 5432`.

> **Optionale Erweiterung der Spec:** Der BACnet-Transport wird als **Interface** abstrahiert
> (`IBacnetTransport` mit `SerialBacnetTransport` und `TcpBacnetTransport`), damit beide
> Varianten ohne Code-Änderung unterstützt werden. **Umfang:** ~1 Tag zusätzlich.
> → Empfehlung: **im ersten Release nur Serial**, TCP-Transport als Folge-PR.

## 9. Fehlersuche auf der DS923+

| Symptom | Ursache | Behebung |
|---|---|---|
| `Status/Bacnet.state = port_not_open` | Gerät nicht vorhanden | `ls /dev/ttyUSB*` prüfen, Adapter neu anstecken |
| `Access denied` beim Port-Open | fehlende Gruppe | `group_add: ["dialout"]` |
| `Device or resource busy` | Host-Prozess belegt den Port | `fuser -v /dev/ttyUSB0`, seriellen Dienst stoppen |
| `no_iam` Timeout | falsche Baudrate | ComfoBox auf 38400 prüfen, Baudrate anpassen |
| `status = degraded` dauerhaft | ComfoBox antwortet nicht | Verkabelung (A/B vertauscht?), Terminierung |
| Container startet nicht | `device` Pfad falsch | `by-id`-Pfad prüfen, `docker inspect` |
| `unhealthy` | `/api/health` antwortet nicht | `docker compose logs gateway` |
| HA-Entitäten `unavailable` | Broker nicht erreichbar | `COMFOBOX_Mqtt__Brokers__0__Host` prüfen |
