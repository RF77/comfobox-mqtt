# Installation als Synology-Paket (SPK) mit Getting Started

> Anforderung 3 (Erweiterung): *„Ist es möglich, dass ich das als Third Party App direkt in der
> DS 923+ sehe und einfach installieren klicken kann? Ich möchte dann, dass eine Art
> ‚Getting Started' losgeht."*

## 1. Kurzantwort

**Ja.** Das Ergebnis ist:

```
Paketcenter  →  „Installieren" (Klick)
                   ↓
   ComfoBox Gateway wird installiert, Container starten
                   ↓
   Browser öffnet automatisch die Web-UI  →  Getting-Started-Assistent
                   ↓
   [1] Serielle Ports wählen  →  [2] BACnet testen  →  [3] MQTT-Broker
   →  [4] Benutzer & Passwort  →  [5] Fertig
                   ↓
   Gateway läuft, Home Assistant entdeckt die Entitäten automatisch
```

Der Benutzer muss **kein** `.env` schreiben, **kein** `compose.yaml` verstehen und **keine**
Pfade nachschlagen. Das ist der Zielzustand.

### Wie das technisch funktioniert

| Mechanismus | DSM-Feld | Wirkung |
|---|---|---|
| **SPK-Paket** | `package="ComfoBoxGateway"` | erscheint im Paketcenter, installierbar per Klick |
| **Admin-Link** | `adminport="8081"`, `adminurl="/"` | DSM öffnet beim Start automatisch `http://<NAS>:8081/` |
| **Container Manager** | `install_dep_packages="ContainerManager>=1432"` | Container Manager wird als Abhängigkeit mit installiert/gestartet |
| **Lifecycle** | `scripts/start-stop-status` | Paket-Start/Stopp steuert die Container; Boot-Autostatik inklusive |
| **Konfiguration** | `config.json` im persistenten Verzeichnis | vom Assistenten geschrieben, vom Supervisor gelesen |

> **Wichtig zur Erwartungshaltung:** Ein selbst gebautes SPK ist ein **Drittanbieter-Paket**.
> Es erscheint **nicht** automatisch im offiziellen Synology-Store. Es gibt drei Wege
> (siehe [§9](#9-verteilung-des-pakets)), alle leadern zum selben Ergebnis.

---

## 2. Entscheidungsübersicht

| Frage | Entscheidung | Begründung |
|---|---|---|
| Wie wird verteilt? | **SPK-Paket** (`.spk`) | erscheint im Paketcenter, Start/Stopp, Autostart |
| Wie viele Container? | **1** (Gateway + UI in einem Image) | 1 Port, 1 Image, kein nginx-Proxy, kein CORS (→ [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md)) |
| Wo läuft der Assistent? | In der **Web-UI** des Gateways | volle Kontrolle, kein DSM-ExtJS-Framework nötig, updatebar ohne SPK-Neubau |
| Wer rendert die Compose-Datei? | Ein **Supervisor** auf dem NAS | erzeugt `devices:`-Einträge dynamisch → Serial-Port-Wechsel ohne Neuinstallation |
| Wie werden Secrets behandelt? | **Datei** `config.json` (0600), nicht Env | Passwörter erscheinen nicht in `docker inspect` |
| Serial-Port durchreichen? | **alle vorhandenen** `ttyUSB*` | Connection-Test im Assistenten möglich, Hotplug tolerant |

---

## 3. Was der Benutzer sieht

### 3.1 Installation

```
Paketcenter
  └── Installieren
        └── (Suche oder manuelle Installation der comfobox-gateway_1.0.0.spk)
              └── "Paket wird installiert …"
                    └── Container Manager wird eingerichtet (Abhängigkeit)
                          └── Container werden gestartet
                                └── Browser öffnet http://<NAS>:8081/
```

Nach der Installation zeigt das Paketcenter den Eintrag **„ComfoBox Gateway"** mit:

| Element | Funktion |
|---|---|
| **Starten / Stoppen** | startet bzw. stoppt die Container |
| **Konfiguration** (Hauptlink) | öffnet die Web-UI (d. h. den Assistenten) |
| **Deinstallieren** | entfernt Container + Paket, **Daten bleiben** (Wiederherstellung möglich) |
| **Protokoll** | zeigt das Supervisor-Log |

### 3.2 Getting Started (Assistent)

Der Assistent ist Teil der Angular-App und wird gezeigt, solange die Konfiguration unvollständig ist.

| Schritt | Inhalt | Validierung |
|---|---|---|
| **0 – Willkommen** | Erklärung, Zielbild, „Los geht's" | – |
| **1 – Serielle Schnittstelle** | Liste aller erkannten Ports (Dropdown mit Beschreibungen), Auswahl, Checkbox „Alle Ports durchreichen" | Port muss auswählbar sein; Port-Scan wird live wiederholt |
| **2 – BACnet** | Baudrate (38400/76800), Geräte-ID, Hinweis „ComfoBox muss auf 38400 stehen und neu gestartet sein" | **Verbindungstest** (WhoIs → IAm) |
| **3 – MQTT-Broker** | Host, Port, Benutzer, Passwort, TLS, Discovery an/aus, Option „eigenen Broker mitbringen" | **Verbindungstest** (MQTT CONNECT) |
| **4 – Web-UI-Zugang** | Benutzername, Passwort, Wiederholung, Port-Vorschlag | Passwortregeln; Port-Konfliktprüfung |
| **5 – Fertig** | Zusammenfassung, Neustart auslösen, Fortschritt, danach Verweis auf Home Assistant | Health-Check nach Neustart |

**Verhalten bei Abbruch:** Der Assistent ist jederzeit unter `/setup` erreichbar und wiederholbar.
Ein unvollständiger Zustand blockiert **nicht** den Betrieb – der Gateway startet im
**Setup-Modus**: API und UI laufen, BACnet und MQTT warten auf Konfiguration.

---

## 4. Architektur des SPK

### 4.1 Dateistruktur

````
comfobox-gateway_1.0.0.spk            ← tar-Archiv
├── INFO                                ← Paketmetadaten
├── conf/
│   └── privilege                       ← Rechte des Pakets (Pflicht ab DSM 7.0)
├── PACKAGE_ICON.PNG                   ← 64×64 (Pflicht ab DSM 7.0)
├── PACKAGE_ICON_256.PNG               ← 256×256 (große Ansicht)
├── scripts/
│   ├── preinst                         ← Prüfungen (Container Manager da? Architektur?)
│   ├── postinst                        ← Verzeichnisse, udev-Regel, Supervisor installieren
│   ├── preuninst                       ← Container stoppen
│   ├── postuninst                      ← Container + Projekt entfernen
│   └── start-stop-status               ← Supervisor + Container starten/stoppen
└── package.tgz
    └── target/
        ├── bin/
        │   ├── supervisor.sh            ← Renderer + Watchdog
        │   └── serial-scan.sh           ← Serial-Port-Enumeration
        ├── etc/
        │   ├── comfobox-gateway.service ← systemd-Unit
        │   └── 99-comfobox-serial.rules ← udev-Regel
        └── compose.template.yaml        ← Compose-Vorlage
````

> ⚠️ **`conf/privilege` ist Pflicht ab DSM 7.0.** Fehlt die Datei, gilt das
> Paket als **Root-Paket** und das Paketzentrum verweigert die Installation
> **schon beim Hochladen**:
>
> *„… kann nicht installiert werden, da es mit Root-Rechten ausgeführt
> wird. Pakete mit Root-Rechten können Befehle als Root-Benutzer ausführen
> und die Sicherheit Ihres Systems gefährden."*
>
> Die Datei muss im **SPK-Wurzelverzeichnis** liegen (nicht im `package.tgz`).
> Details und Begründung: [`deploy/synology-spk/src/conf/README.md`](../../deploy/synology-spk/src/conf/README.md).

Das konkrete Skelett liegt in [`deploy/synology-spk/`](../../deploy/synology-spk/).

### 4.1a Prüfung des fertigen Pakets

Der Build prüft das Ergebnis gegen die Anforderungen des DSM-Paketcenters:

````bash
cd deploy/synology-spk
bash build-spk.sh
bash tools/check-spk.sh
````

Geprüft werden: `conf/privilege` vorhanden **und** an der richtigen Stelle,
`defaults.run-as = package`, Icon-Größen (64×64 / 256×256), die Pflichtfelder
der `INFO`, `os_min_ver ≥ 7.0-40000` und das Image-Tag. Der Prüfer läuft auch
in der CI – ohne die `conf/privilege` wird der Build rot, **bevor** das Paket
das NAS erreicht.

### 4.2 INFO – relevante Felder

````
package="ComfoBoxGateway"
version="1.0.0-0001"
displayname="ComfoBox Gateway"
displayname_ger="ComfoBox Gateway"
description="MQTT-Gateway für die Zehnder ComfoBox Series 5 (RS485/BACnet MSTP) mit Web-Oberfläche."
description_ger="MQTT-Gateway für die Zehnder ComfoBox Series 5 (RS485/BACnet MSTP) mit Web-Oberfläche."
maintainer="RF77"
maintainer_url="https://github.com/RF77/comfobox-mqtt"
distributor="RF77"
distributor_url="https://github.com/RF77/comfobox-mqtt"
support_url="https://github.com/RF77/comfobox-mqtt/issues"
helpurl="https://github.com/RF77/comfobox-mqtt/tree/main/docs"

arch="noarch"
os_min_ver="7.2-63134"

# Container Manager als Abhängigkeit (wird bei Bedarf mitinstalliert & gestartet)
install_dep_packages="ContainerManager>=1432"
start_dep_services="ContainerManager"

# Admin-Link → DSM öffnet beim Start automatisch die Web-UI
adminprotocol="http"
adminport="8081"
adminurl="/"
checkport="yes"

# Lifecycle
precheckstartstop="yes"
ctl_stop="yes"
ctl_uninstall="yes"

# Paketcenter-Verhalten
silent_install="no"
silent_upgrade="yes"
silent_uninstall="yes"
thirdparty="yes"
install_type="system"
extractsize="1024"
checksum="…"
````

| Feld | Wirkung |
|---|---|
| `install_dep_packages` | Container Manager wird **vor** unserem Paket installiert; ohne ihn startet nichts |
| `adminport` + `adminurl` | DSM öffnet `http://<NAS>:8081/` → **Getting Started** |
| `checkport="yes"` | DSM prüft vor der Installation, ob 8081 frei ist |
| `precheckstartstop="yes"` | erlaubt `prestart`/`prestop` im `start-stop-status` |
| `arch="noarch"` | das Paket enthält keine Binärdateien → läuft auf jeder CPU-Architektur |
| `os_min_ver="7.2-63134"` | ab DSM 7.2.1 (dort ist der Docker-Project-Worker verfügbar) |

> **Hinweis zu `start_dep_services`:** Das Feld erwartete laut DSM-Doku *Dienste* (z. B. `ssh-shell`).
> Der Container Manager wird primär über `install_dep_packages` abgesichert; die zusätzliche
> Angabe ist defensiv und muss auf dem Ziel-NAS verifiziert werden.

### 4.3 Lifecycle-Skripte

| Skript | Aufgabe |
|---|---|
| `preinst` | Prüfungen: DSM-Version ≥ 7.2.1, Container Manager vorhanden, Port 8081 frei. **Keine** Seiteneffekte (laut DSM-Vorgabe) |
| `postinst` | Verzeichnisse anlegen, udev-Regel installieren, systemd-Unit aktivieren, Supervisor starten, erste `compose.yaml` rendern |
| `start-stop-status start` | Supervisor starten (PID-Datei), Container starten |
| `start-stop-status stop` | Supervisor stoppen, Container stoppen (LWT → `offline` in HA) |
| `start-stop-status status` | `0` wenn Supervisor **und** Container laufen, `3` sonst |
| `preuninst` | Container stoppen |
| `postuninst` | Container + Projekt + Images entfernen, Supervisor deaktivieren, **Datenverzeichnis behalten** |

**Exit-Codes für `status`** (DSM-Vorgabe):

| Code | Bedeutung |
|---|---|
| `0` | läuft |
| `1` | tot, PID-Datei vorhanden |
| `2` | tot, Lock-Datei vorhanden |
| `3` | läuft nicht |
| `4` | Status unbekannt |
| `150` | Paket defekt → neu installieren |

---

## 5. Der Supervisor

Der Supervisor ist das Herzstück: Er ist der einzige Ort, der Compose-Datei rendert und Container
erstellt. Dadurch ist der Serial-Port **zur Laufzeit** änderbar.

### 5.1 Aufgaben

````
┌──────────────────────────────────────────────────────────────┐
│  supervisor.sh (läuft als Root auf dem NAS)                   │
│                                                              │
│  alle 3 s:                                                   │
│    1. ports.json aktualisieren (Serial-Port-Scan)           │
│    2. Hash von config.json + ports.json vergleichen         │
│    3. bei Änderung: compose.yaml rendern                    │
│    4. bei Änderung: docker compose up -d                     │
└──────────────────────────────────────────────────────────────┘
        ▲                        │
        │ schreibt                │ liest
        ▼                        ▼
   ports.json              config.json  (vom Assistenten geschrieben)
        │                        │
        └────────► compose.yaml ◄┘
                    │
                    ▼
              docker compose up -d
                    │
        ┌───────────┴───────────┐
        ▼                       ▼
  comfobox-gateway        (kein Broker-Container
  (Gateway + UI)           mehr, siehe unten)
````

### 5.2 Warum kein Docker-Socket im Container

| Variante | Bewertung |
|---|---|
| Docker-Socket in den Container mounten | ❌ Der Container könnte das NAS verwalten (Deinstallation des NAS per API!) |
| Supervisor auf dem Host | ✅ Der Container bekommt nur das, was er braucht: sein Volume und sein Serial-Device |

### 5.3 Render-Vorgang

Die Compose-Vorlage enthält Platzhalter, die der Supervisor per `sed` ersetzt:

````yaml
services:
  gateway:
    image: ghcr.io/RF77/comfobox-gateway:1.0.1
    container_name: comfobox-gateway
    restart: unless-stopped
    ports:
      - "${WEB_PORT}:8080"
    environment:
      - COMFOBOX_CONFIG_FILE=/var/lib/comfobox/config.json
      - COMFOBOX_SETUP_MODE=1
    devices:
      ${SERIAL_DEVICES}
    group_add:
      - "${DIALOUT_GID}"
    volumes:
      - ${DATA_DIR}:/var/lib/comfobox
      - ${LOG_DIR}:/var/log/comfobox
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://localhost:8080/api/health"]
      interval: 30s
      timeout: 5s
      start_period: 25s
      retries: 3
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "5"
````

Beispiel für die gerenderte `devices:`-Sektion:

````yaml
    devices:
      - "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0:/dev/ttyUSB0"
      - "/dev/ttyUSB1:/dev/ttyUSB1"
````

### 5.4 Serial-Port-Scan

````sh
#!/bin/sh
# serial-scan.sh – schreibt ports.json
# Aufruf: serial-scan.sh <ziel-pfad>

TARGET="$1"

emit_port() {
    dev="$1"; id_path="$2"; vendor="$3"; product="$4"
    [ -e "$dev" ] || return 0
    label="$vendor $product"
    if [ -n "$id_path" ] && [ -e "$id_path" ]; then
        printf '    { "device": "%s", "stableId": "%s", "description": "%s" },\n' \
               "$dev" "$id_path" "$label" >> "$TARGET"
    else
        printf '    { "device": "%s", "stableId": null, "description": "%s" },\n' \
               "$dev" "$label" >> "$TARGET"
    fi
}

{
    printf '{\n  "generatedAt": "%s",\n  "ports": [\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    for byid in /dev/serial/by-id/*; do
        [ -e "$byid" ] || continue
        target="$(readlink -f "$byid")"
        case "$target" in
            /dev/ttyUSB*|/dev/ttyACM*)
                vid="$(cat /sys/class/tty/$(basename "$target")/device/../idVendor 2>/dev/null)"
                pid="$(cat /sys/class/tty/$(basename "$target")/device/../idProduct 2>/dev/null)"
                emit_port "$target" "$byid" "USB $vid" "$pid"
                ;;
        esac
    done
    printf '  ]\n}\n'
} > "$TARGET"
````

> **Warum `by-id`:** `/dev/ttyUSB0` kann sich nach einem Reboot oder erneuten Anstecken ändern.
> `/dev/serial/by-id/…` ist an die Seriennummer gebunden und **stabil**. Der Assistent zeigt
> beide an, speichert aber die stabile Variante.

### 5.5 Verhalten bei Konfigurationsänderung

```
Assistent schreibt config.json
        ↓
Supervisor erkennt Hash-Änderung (≤ 3 s)
        ↓
compose.yaml neu rendern (Serial-Port jetzt korrekt durchgereicht)
        ↓
docker compose up -d
        ↓
Container wird ersetzt (create + start)
        ↓
Gateway startet, liest config.json, verbindet BACnet + MQTT
        ↓
Health = online → HA-Entitäten werden verfügbar
```

**Ausfallzeit:** ca. 5–10 Sekunden (Container-Neustart). Der LWT wird ausgelöst → HA zeigt
kurzzeitig `unavailable`. Das ist akzeptabel und wird im Assistenten angekündigt.

---

## 6. Konfiguration und Secrets

### 6.1 `config.json` (persistent, chmod 0600)

Liegt in `SYNOPKG_PKGVAR` und wird in den Container gemountt.

> **Das Schema ist das der .NET-Konfiguration** – die Abschnittsnamen sind
> großgeschrieben und entsprechen den Options-Klassen des Gateways
> (`Bacnet`, `Mqtt`, `Api`, `Gateway`). Das ist keine Geschmacksfrage: das
> Gateway bindet die Datei über `Microsoft.Extensions.Configuration`, und
> **die Schlüssel sind case-insensitiv**. Ein Abschnitt `bacnet` würde also
> stillschweigend als `Bacnet` gelesen – und ein leerer `Port` oder eine
> leere `Brokers`-Liste würde den Start des Gateways verhindern.

````json
{
  "SchemaVersion": 1,
  "Api": {
    "SetupCompleted": true,
    "AdminUsername": "admin",
    "AdminPasswordHash": "pbkdf2-sha256$600000$<salt-b64>$<hash-b64>",
    "RequireAuthentication": false
  },
  "Bacnet": {
    "Port": "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0",
    "Baudrate": 38400,
    "DeviceId": 1,
    "ThisStation": 3
  },
  "Mqtt": {
    "KeepAliveSeconds": 30,
    "DiscoveryPrefix": "homeassistant",
    "UseMqttRetain": true,
    "Brokers": [
      {
        "Host": "192.168.1.10",
        "Port": 1883,
        "Username": "comfobox",
        "Password": "…",
        "UseTls": false
      }
    ]
  },
  "Gateway": {
    "BaseTopic": "ComfoBox",
    "PollIntervalSeconds": 60,
    "ExpertMode": false,
    "MaxWritesPer24h": 200,
    "RestartEnabled": true
  },
  "Supervisor": {
    "PassAllSerialDevices": true,
    "WebPort": 8081,
    "Image": "ghcr.io/rf77/comfobox-gateway:1.0.1"
  }
}
````

| Abschnitt | Wer schreibt ihn | Wer liest ihn |
|---|---|---|
| `Api` | der Assistent (Administrator, `SetupCompleted`) | das Gateway |
| `Bacnet` | der Assistent (Port, Baudrate) | das Gateway **und** der Supervisor |
| `Mqtt` | der Assistent (Broker) | das Gateway |
| `Gateway` | der Assistent (Expertenmodus, Limit) | das Gateway |
| `Supervisor` | der Assistent (`PassAllSerialDevices`) | **nur** der Supervisor des NAS |

> **Warum ein eigener Abschnitt `Supervisor`?** Er enthält die Angaben, die
> nur der Supervisor auf dem NAS braucht – insbesondere
> `PassAllSerialDevices`: der `devices:`-Block der Compose-Datei hängt davon
> ab, ob *alle* seriellen Ports durchgereicht werden (Voreinstellung, weil der
> Verbindungstest im Assistenten sonst nicht möglich ist) oder nur der
> gewählte.

> **Wichtig:** Die Datei darf **keine** leeren Werte für `Bacnet:Port` und
> **keine** leere `Mqtt:Brokers`-Liste enthalten. Das Gateway validiert beide
> beim Start (`ValidateOnStart`) und würde sonst in einer Neustartschleife
> hängen bleiben – der Assistent wäre dann nicht erreichbar. Deshalb legt
> `postinst` nur `Api:SetupCompleted` an; alles andere kommt aus dem Image
> (`appsettings.json` + Umgebungsvariablen), bis der Assistent schreibt.


### 6.2 Warum als Datei statt als Env-Variablen

| Aspekt | Env-Variablen | Datei im Volume |
|---|---|---|
| Sichtbarkeit | `docker inspect` zeigt **alle** Werte inkl. Passwort | Passwort steht nur in einer 0600-Datei |
| Persistenz | nur mit `env_file` | Teil des Volumes, überlebt Container-Neustart |
| Änderung zur Laufzeit | Neustart nötig | Supervisor erkennt die Datei und startet neu |
| Backup | im `docker compose`/`.env` | zusammen mit den Daten im selben Verzeichnis |

> Das Gateway liest **primär die Datei**; Env-Variablen bleiben als **Override** unterstützt
> (für CI, Tests, manuelle Debug-Läufe). Die Datei hat Vorrang.

### 6.3 Passwort-Hash

| Algorithmus | Parameter |
|---|---|
| PBKDF2-HMAC-SHA256 | 600.000 Iterationen, 16-Byte-Salt (OWASP-Empfehlung) |
| Format | `pbkdf2-sha256$<iterations>$<salt-base64>$<hash-base64>` |

Umsetzung in .NET 10 ohne Zusatzpaket:

````csharp
// src/ComfoBox.Gateway/Security/PasswordHasher.cs
internal static class PasswordHasher
{
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string encoded)
    {
        var parts = encoded.Split('$');                       // pbkdf2-sha256, iter, salt, hash
        if (parts.Length != 4) return false;
        var iterations = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
````

> `FixedTimeEquals` statt `SequenceEqual` → timing-sicher.

### 6.4 Web-UI-Authentifizierung

| Aspekt | Entscheidung |
|---|---|
| Mechanismus | **Cookie-Authentifizierung** (`AddAuthentication().AddCookie()`) |
| Warum kein JWT | Die UI ist ein Browser im selben Netz; Cookie ist einfacher und sicherer (HttpOnly, SameSite) |
| Sitzung | 8 h, gleitend |
| CSRF | `SameSite=Strict` + Anti-Forgery-Token bei schreibenden Requests |
| API-Key | optional zusätzlich für Skript-Zugriff (`X-Api-Key`) |

---

## 7. API für den Assistenten

Zusätzlich zu den regulären Endpunkten (siehe
[Datenmodell & API](../02-modernisierung/datenmodell-und-api.md)):

| Methode | Route | Zweck |
|---|---|---|
| `GET` | `/api/setup/state` | `setupCompleted`? welche Schritte sind offen? |
| `GET` | `/api/setup/ports` | erkannte serielle Ports (aus `ports.json`) |
| `POST` | `/api/setup/bacnet/test` | BACnet-Verbindung testen (WhoIs → IAm) |
| `POST` | `/api/setup/mqtt/test` | MQTT-Verbindung testen (CONNECT + Publish/Receive) |
| `GET` | `/api/setup/ports/suggest` | freier Web-UI-Port vorschlagen |
| `PUT` | `/api/setup/config` | Konfiguration schreiben (Assistent) |
| `POST` | `/api/setup/apply` | Konfiguration übernehmen → Supervisor erkennt Änderung → Container-Neustart |

### 7.1 Beispiel

````http
GET /api/setup/ports
````

Die Antwort ist ein **Array** – der Endpoint liefert die Liste direkt, ohne
den Zeitstempel der `ports.json` (der steckt in der Datei selbst):

````json
[
  {
    "port": "/dev/ttyUSB0",
    "description": "Prolific CH340 /dev/ttyUSB0 (1a86:7523)",
    "stablePath": "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0"
  }
]
````

> Der `stablePath` ist der reboot-stabile Pfad aus der `ports.json` des
> Supervisors. Er wird gespeichert – `/dev/ttyUSB0` kann sich nach einem
> Reboot ändern.

### 7.2 Test-Endpunkte

````http
POST /api/setup/bacnet/test
Content-Type: application/json

{ "port": "/dev/ttyUSB0", "baudrate": 38400, "masterId": 1 }
````

````json
{
  "ok": true,
  "message": "ELESTA (Device-ID 1) hat auf WhoIs geantwortet.",
  "durationMs": 412
}
````

Bei Fehler:

````json
{
  "ok": false,
  "message": "Kein IAm innerhalb von 30 s. Prüfen Sie: Baudrate (ComfoBox muss auf 38400 stehen und neu gestartet sein), A/B-Verdrahtung, Adapter belegt.",
  "hint": "serial_port_not_found"
}
````

> Der Test läuft **im Gateway-Prozess** → er sieht nur die durchgereichten Devices. Deshalb
> werden standardmäßig **alle** vorhandenen seriellen Ports durchgereicht (siehe §2).

### 7.3 `PUT /api/setup/config` + `POST /api/setup/apply`

````http
PUT /api/setup/config
Content-Type: application/json

{ "port": "/dev/serial/by-id/…",
  "baudrate": 38400,
  "brokerHost": "10.10.1.20", "brokerPort": 1883,
  "brokerUsername": "comfobox", "brokerPassword": "…", "useTls": false,
  "adminUsername": "admin", "adminPassword": "…",
  "passAllSerialDevices": true }
````

→ **204 No Content**. Der Assistent schreibt die Datei; das Passwort wird dabei **serverseitig
gehasht** (der Client schickt das Klartext, das Gateway ersetzt es durch den Hash).

> **Der Anfrage-Body ist flach**, die Datei ist geschachtelt: das Gateway
> übersetzt die flache Anfrage in die Abschnitte der `config.json`
> (`Bacnet:Port`, `Mqtt:Brokers:0:Host`, `Api:AdminPasswordHash`,
> `Supervisor:PassAllSerialDevices`).
>
> **Regeln:** Das Administrator-Passwort muss mindestens 12 Zeichen lang sein
> und 3 von 4 Zeichenklassen enthalten (Groß-, Kleinbuchstaben, Ziffern,
> Symbole). Verstößt es dagegen, antwortet der Endpunkt mit **400** – die
> Oberfläche prüft dieselben Regeln vorher, damit der Benutzer es sofort
> sieht.
>
> **Der Broker ist optional:** ohne `brokerHost` bleibt der MQTT-Abschnitt
> unverändert, der Gateway läuft dann ohne Broker (die Werte sind nur über
> die Web-Oberfläche und die HA-Integration erreichbar).

`POST /api/setup/apply` → **202 Accepted**. Der Gateway schreibt die Datei, der Supervisor
startet den Container neu. Die UI beobachtet `GET /api/health` und wechselt nach
`online` in die normale Ansicht.

> **Wichtig:** Der Assistent darf nur laufen, wenn entweder noch keine Konfiguration existiert
> **oder** der Benutzer sich als Administrator authentifiziert hat (`/setup` ist nach der
> Ersteinrichtung passwortgeschützt).

---

## 8. Betrieb im Detail

### 8.1 Verzeichnisse auf dem NAS

| Pfad | Inhalt | Persistent? |
|---|---|---|
| `/var/packages/ComfoBoxGateway/target/` | Programm (Supervisor, Compose-Template, Unit) | nein (bei Update ersetzt) |
| `/var/packages/ComfoBoxGateway/var/` | `config.json`, `ports.json`, `compose.yaml`, `logs/`, `writes.json` | **ja** (überlebt Update & Deinstallation) |
| `/var/log/packages/ComfoBoxGateway.log` | DSM-Protokoll des Pakets | – |
| `/etc/udev/rules.d/99-comfobox-serial.rules` | udev-Regel (stabile Symlinks, `dialout`-Rechte) | – |

> Auf einer DS923+ mit einem Volume liegt `var` unter `/volume1/@appstore/ComfoBoxGateway/var`
> bzw. `/volume1/docker/...`. Der genaue Pfad hängt vom gewählten Volume ab – der SPK nutzt
> `${SYNOPKG_PKGVAR}` und ist damit volumenunabhängig.

### 8.2 udev-Regel

````
# /etc/udev/rules.d/99-comfobox-serial.rules
SUBSYSTEM=="tty", ATTRS{idVendor}=="1a86", ATTRS{idProduct}=="7523", \
  SYMLINK+="comfobox", GROUP="dialout", MODE="0660"
SUBSYSTEM=="tty", ATTRS{idVendor}=="1a86", ATTRS{idProduct}=="7523", \
  SYMLINK+="comfobox-by-id", GROUP="dialout", MODE="0660"
````

Zweck: stabiler Pfad `/dev/comfobox` **und** korrekte Gruppenrechte, auch wenn der Adapter
spät angesteckt wird.

### 8.3 systemd-Unit

````ini
# /etc/systemd/system/comfobox-gateway.service
[Unit]
Description=ComfoBox Gateway Supervisor
After=docker.service network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/var/packages/ComfoBoxGateway/target/bin/supervisor.sh run
ExecStop=/var/packages/ComfoBoxGateway/target/bin/supervisor.sh stop
Restart=always
RestartSec=5
TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
````

> **Warum systemd und nicht nur das SPK-`start-stop-status`?** Beides wird genutzt:
> `synopkg start` (Paketcenter) → `start-stop-status start` → Supervisor. Der systemd-Unit
> sorgt zusätzlich dafür, dass der Supervisor nach einem NAS-Neustart läuft, auch wenn das
> Paket-Startverhalten sich unterscheidet. Redundanz ist hier billig, Ausfall nicht.

### 8.4 Firewall

DSM blockiert eingehende Verbindungen standardmäßig. Der SPK kann den Port über die
DSM-Firewall-API freigeben (optional, mit Rückfrage im Assistenten):

| Variante | Vorgehen |
|---|---|
| **Standard** | Der Assistent zeigt einen Hinweis: „Port 8081 im DSM-Firewall freigeben: Firewall → Verbindungen → Hinzufügen" |
| **Automatisch** | `postinst` ruft `synofirewall` auf (Berechtigungen im DSM prüfen!) |

> **Empfehlung:** Standardmäßig **keine** automatische Firewall-Änderung. Der Nutzer
> entscheidet selbst – das ist eine Sicherheitsentscheidung, die nicht automatisiert werden
> sollte. Der Assistent bietet den passenden Link an.

### 8.5 Reverse Proxy (optional)

Wer die UI nicht direkt auf Port 8081, sondern über `https://nas.domain/compfobox` erreichbar
machen will, nutzt **DSM → Anmeldeportal → Reverse Proxy** (ab DSM 7.2 mit Container Manager
verfügbar). Der Assistent bietet diesen Weg als Link an.

---

## 9. Verteilung des Pakets

| Weg | Aufwand | Updates | Sichtbarkeit |
|---|---|---|---|
| **A. Manuelle Installation** | Datei `.spk` aus dem Release auf den NAS legen → Paketcenter → Installieren → Manuelle Installation | manuell | erscheint unter „Installierte Pakete" |
| **B. Eigene Paketquelle** | JSON-Index auf einem eigenen Webserver/GitHub-Release → Paketcenter → Einstellungen → Paketquelle → hinzufügen | **automatisch** | erscheint in der Liste, Update-Hinweis |
| **C. Synology-Partner** | formale Qualifizierung bei Synology | automatisch | offizieller Store, „Drittanbieter"-Kennzeichnung |

**Empfehlung:** **B** – eine eigene Paketquelle über GitHub Releases. Der Nutzer trägt einmalig
die URL ein und bekommt danach Update-Hinweise im Paketcenter.

**Ablauf Variante B:**

1. Release `v1.0.0` im GitHub-Repo mit `comfobox-gateway_1.0.0-0001.spk` und `packages.json`
2. Der Nutzer trägt in DSM ein:
   `Paketcenter → Einstellungen → Paketquelle → Hinzufügen → Name: ComfoBox → URL: https://github.com/RF77/comfobox-mqtt/releases/latest/download/packages.json`
3. Das Paket erscheint unter „Installieren" und lässt sich mit einem Klick installieren
4. Bei jedem neuen Release erscheint ein Update-Hinweis

> **Wichtig:** Variante A funktioniert **immer** und ohne Synology-Zusammenarbeit. Sie ist der
> Fallback, falls die Paketquelle nicht funktioniert.

---

## 10. Alternative: manuelles Compose-Deployment

Der SPK ist die bequemste Variante, aber **nicht** die einzige. Für Nutzer, die Container
komplett selbst verwalten wollen (z. B. auf einem anderen NAS oder einem Raspberry Pi), bleibt
der dokumentierte Weg aus
[containerisierung.md](containerisierung.md) und [synology-ds923.md](synology-ds923.md).

| Merkmal | SPK | manuelles Compose |
|---|---|---|
| Aufwand | Klick im Paketcenter | YAML + `.env` schreiben |
| Getting Started | ✓ Assistent | ✗ manuell konfigurieren |
| Port-Wechsel zur Laufzeit | ✓ | Container neu erstellen |
| Serial-Ports | automatisch erkannt | manuell eintragen |
| Update | ✓ über Paketcenter | `docker compose pull && up -d` |
| Für Power-User | umständlich | **bevorzugt** |

> **Beide Wege werden unterstützt.** Die Komponenten sind identisch: das SPK ist „nur" ein
> komfortabler Wrapper um genau diese Compose-Datei.

---

## 11. Update & Deinstallation

### 11.1 Update

````
DSM-Ablauf:
  start-stop-status stop      → Container gestoppt, Supervisor gestoppt
  postinst                    → neue Dateien, compose.yaml neu gerendert
  start-stop-status start     → Container mit neuer Version gestartet

Erhalten:
  • config.json  (inkl. Passwörter, Benutzer)
  • writes.json  (24-h-Schreibzähler!)
  • logs/
  • das persistente Volume
````

> **Achtung:** Der 24-h-Schreibzähler **muss** das Update überleben – sonst beginnt das
> EEPROM-Schutzfenster neu. Der Schreibzähler liegt deshalb in `SYNOPKG_PKGVAR`, **nicht** im
> Docker-Volume. (Siehe auch [Konfiguration & Secrets](konfiguration-und-secrets.md#6-persistente-daten).)

### 11.2 Deinstallation

| Element | Verhalten |
|---|---|
| Container | werden gestoppt und **entfernt** |
| Images | bleiben (Cache) |
| `config.json`, `writes.json` | **bleiben** → erneute Installation übernimmt die Konfiguration |
| Supervisor, systemd-Unit, udev-Regel | werden entfernt |
| Backup-Hinweis | der Assistent bzw. das Deinstallationsprotokoll weist auf das Datenverzeichnis hin |

> **Warum die Daten behalten?** Ein Uninstall ist im DSM oft eine versehentliche Aktion.
> Die Daten zu behalten macht die Installation idempotent und den Rollback trivial.

---

## 12. Aufwand

| Aufgabe | Aufwand |
|---|---|
| SPK-Skelett (INFO, Skripte, Verzeichnisse, udev, systemd) | 2 PT |
| Supervisor (Renderer, Watchdog, Serial-Scan) | 1,5 PT |
| Compose-Template + Renderer-Anbindung | 0,5 PT |
| Gateway: Setup-Modus, `config.json`-Lesen, Datei-vor-Env | 1 PT |
| Gateway: Setup-Endpunkte (`/api/setup/*`) | 1,5 PT |
| Gateway: Passwort-Hashing + Cookie-Auth + Benutzerverwaltung | 2 PT |
| UI: Assistent (6 Schritte, Validierung, Tests) | 3 PT |
| Paketquelle + Release-Pipeline für das SPK | 0,5 PT |
| Test auf der DS923+ (Installation, Update, Deinstallation, Reboot) | 1 PT |
| **Summe** | **≈ 13 PT** |

> **Zusätzlich** zu den 41 PT der [Roadmap](../04-migration/roadmap.md) – die SPK-Installation
> ersetzt aber einen Teil des manuellen Deployments (Phase 6, Task 6.3–6.5) und ist für den
> Endnutzer **deutlich** bequemer.

---

## 13. Checkliste

### SPK
- [ ] `INFO` vollständig, `arch="noarch"`, `os_min_ver="7.2-63134"`
- [ ] `install_dep_packages="ContainerManager>=1432"` → Installation ohne Container Manager unmöglich
- [ ] `adminport`/`adminurl` gesetzt → DSM öffnet die UI automatisch
- [ ] `preinst` prüft Container Manager + freien Port
- [ ] `postinst` legt Verzeichnisse an, installiert udev-Regel und Unit
- [ ] `start-stop-status` implementiert `start`/`stop`/`status` mit korrekten Exit-Codes
- [ ] `status` gibt `0` nur zurück, wenn Supervisor **und** Container laufen
- [ ] Deinstallation entfernt Container, behält Daten
- [ ] Update erzeugt keine doppelten Container (Projektname stabil)

### Assistent
- [ ] Alle 6 Schritte mit Vor/Zurück, Zustand erhalten
- [ ] Portliste zeigt Beschreibung und „stabilen Pfad"
- [ ] BACnet-Test mit sprechender Fehlermeldung
- [ ] MQTT-Test mit sprechender Fehlermeldung
- [ ] Passwortregeln, Bestätigung, „Stärke"-Anzeige
- [ ] Fortschritt beim Neustart, Health-Check, Fehlerbehandlung
- [ ] Assistent ist nach der Ersteinrichtung passwortgeschützt
- [ ] Assistent ist wiederholbar (Konfigurationsänderung)

### Betrieb
- [ ] NAS-Neustart → Container läuft wieder automatisch
- [ ] Adapter spät angesteckt → Port erscheint in der Liste, Gateway verbindet sich
- [ ] Adapter gezogen → Health `degraded`, kein 100 % CPU
- [ ] `config.json` ist 0600, Passwort nicht im Klartext
- [ ] `docker inspect` zeigt **keine** Passwörter
- [ ] Firewall-Hinweis im Assistenten
- [ ] Backup enthält `config.json` und `writes.json`

---

## 14. Verwandte Dokumente

- [containerisierung.md](containerisierung.md) – Dockerfile, Compose, Healthcheck
- [synology-ds923.md](synology-ds923.md) – Serial-Port auf dem NAS, Fehlersuche
- [konfiguration-und-secrets.md](konfiguration-und-secrets.md) – Konfigurationsschema, Backups
- [ci-cd.md](ci-cd.md) – Release-Pipeline (auch für das SPK)
- [ui-angular-optimus.md](../02-modernisierung/ui-angular-optimus.md) – Assistent-Oberfläche
- [datenmodell-und-api.md](../02-modernisierung/datenmodell-und-api.md) – Setup-Endpunkte
- [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md) – ein Container statt zwei
- [ADR-008](../05-entscheidungen/adr-008-synology-spk.md) – Installation als SPK
