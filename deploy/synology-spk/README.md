# ComfoBox Gateway – Synology-Paket (SPK)

Dieses Verzeichnis enthält das **Synology-Paket (SPK)** für den ComfoBox-MQTT-Gateway.
Es ist die bequeme Installationsvariante für die Synology DS923+ (und alle anderen
DSM-NAS mit x86_64/ARM64 ab DSM 7.2.1).

Die vollständige Spezifikation steht in
[`docs/03-deployment/synology-paketcenter.md`](../../docs/03-deployment/synology-paketcenter.md).

---

## Was der Benutzer bekommt

```
Paketcenter → Installieren → Klick
  → Container Manager wird als Abhängigkeit eingerichtet
  → Container startet
  → Browser öffnet automatisch http://<NAS-IP>:8081/
  → Getting-Started-Assistent:
      1. Serielle Ports wählen
      2. BACnet testen (Baudrate, Geräte-ID)
      3. MQTT-Broker eintragen und testen
      4. Benutzer & Passwort für die Web-UI
      5. Fertig → Gateway läuft, Home Assistant entdeckt die Entitäten
```

## Bauen

```bash
./build-spk.sh              # → dist/ComfoBoxGateway_noarch-dsm7_1.0.0-0001.spk
./build-spk.sh 1.1.0 0002  # → dist/ComfoBoxGateway_noarch-dsm7_1.1.0-0002.spk
```

Voraussetzung: nur `tar`, `gzip`, `md5sum` – **kein** Docker, **kein** Synology-Toolkit.

### Prüfen

```bash
bash tools/check-shell.sh        # Syntax aller Skripte + Test des JSON-Parsers
node tools/make-icon.mjs         # Icon neu erzeugen (nur nach Designänderungen)
```

> Das Icon wird **versioniert** (`src/PACKAGE_ICON.png`). `make-icon.mjs` ist
> nur der Reproduzierbarkeitsweg – ohne Icon bricht `build-spk.sh` ab.

## Installieren

| Weg | Schritte |
|---|---|
| **Manuell** | `.spk` auf das NAS kopieren → Paketcenter → Installieren → Manuelle Installation → Datei wählen |
| **Paketquelle** | `packages.json` aus dem GitHub-Release als Paketquelle in DSM eintragen → Paket erscheint in der Liste |

## Inhalt dieses Verzeichnisses

```
build-spk.sh                       Build-Skript
packages.json                      Paketquelle für das DSM-Paketcenter
src/
├── INFO                            Paketmetadaten (adminport → Web-UI, Dependency → Container Manager)
├── PACKAGE_ICON.png                Paket-Icon (256×256) – erzeugt mit tools/make-icon.mjs
├── scripts/
│   ├── preinst                     Prüfungen vor der Installation
│   ├── postinst                    Verzeichnisse, udev-Regel, systemd-Unit, erster Start
│   ├── preuninst                   Container stoppen
│   ├── postuninst                  Container entfernen, Daten behalten
│   └── start-stop-status           Lifecycle: start / stop / status / prestart / prestop
├── target/
│   ├── bin/
│   │   ├── supervisor.sh            Rendert compose.yaml, startet Container, überwacht config.json
│   │   ├── serial-scan.sh           Enumeriert serielle Ports → ports.json
│   │   └── common.sh                Gemeinsame Pfade, Docker-Erkennung, JSON-Zugriff
│   ├── etc/
│   │   ├── comfobox-gateway.service systemd-Unit für den Supervisor
│   │   └── 99-comfobox-serial.rules udev-Regel (stabile Symlinks, dialout-Rechte)
│   ├── compose.template.yaml        Compose-Vorlage mit ${…}-Platzhaltern
│   └── image                        Image-Referenz des Pakets (schreibt build-spk.sh)
└── tools/
    ├── check-shell.sh               Syntaxprüfung aller Skripte + Test des JSON-Parsers
    ├── make-icon.mjs                Erzeugt das Paket-Icon (reproduzierbar)
    └── test-config-parse.sh         Test für den JSON-Zugriff auf die config.json
```

## Das Schema der `config.json`

Die `config.json` wird vom **Gateway** geschrieben (der Assistent schreibt sie
über die Setup-API) und folgt dem Schema der .NET-Konfiguration:

````json
{
  "Bacnet":     { "Port": "/dev/ttyUSB0", "Baudrate": 38400 },
  "Mqtt":       { "Brokers": [ { "Host": "…", "Port": 1883 } ] },
  "Api":        { "SetupCompleted": true, "AdminPasswordHash": "pbkdf2-…" },
  "Supervisor": { "PassAllSerialDevices": true }
}
````

> **Wichtig:** Der Supervisor liest daraus den seriellen Port und die Angabe
> „alle Ports durchreichen". Er liest **abschnittsweise** (`json_get` in
> `common.sh`), damit `Port` nicht mit `BrokerPort` verwechselt wird.
> Der Test `tools/test-config-parse.sh` sichert dieses Verhalten ab.

> **Ebenso wichtig:** Die Datei darf **keine** leeren Werte für
> `Bacnet:Port` oder `Mqtt:Brokers` enthalten – das Gateway prüft beide beim
> Start und würde sonst in einer Neustartschleife hängen bleiben. Deshalb
> schreibt `postinst` nur `Api:SetupCompleted`; alles andere kommt aus dem
> Image (`appsettings.json` + Umgebungsvariablen).

## Architektur in einem Satz

> Der **Supervisor** auf dem NAS ist die einzige Instanz, die die Compose-Datei rendert –
> deshalb kann der Serial-Port zur Laufzeit gewechselt werden, ohne das Paket neu zu
> installieren. Der Container selbst bekommt **keinen** Docker-Socket und läuft unprivileged.

## Wichtige Pfade auf dem NAS

| Pfad | Inhalt |
|---|---|
| `/var/packages/ComfoBoxGateway/target/` | Programm (wird bei Update ersetzt) |
| `/var/packages/ComfoBoxGateway/var/` | Protokolle und Status — auf der NAS lesbar |
| Docker-Volume `comfobox-data` | `config.json`, `ports.json`, `writes.json` (24-h-Schreibzähler) — **persistent** |
| `/etc/udev/rules.d/99-comfobox-serial.rules` | udev-Regel |
| `/etc/systemd/system/comfobox-gateway.service` | systemd-Unit |

## Rechte des Pakets (`conf/privilege`)

Ab DSM 7.0 verlangt Synology für jedes Paket eine ausdrückliche
Rechteangabe. Fehlt `conf/privilege`, gilt das Paket als **Root-Paket** und
das Paketzentrum verweigert die Installation schon beim Hochladen.

Die Datei liegt in [`src/conf/`](src/conf/) und wird von `build-spk.sh` ins
SPK gepackt. Details: [`src/conf/README.md`](src/conf/README.md).

## Prüfen

````bash
# Bauen
bash build-spk.sh

# Gegen die Anforderungen des DSM-Paketcenters prüfen
bash tools/check-spk.sh
````

## Diagnose auf der NAS

Wenn das Paketzentrum das Paket ablehnt, nennt der Dialog nur die Ursache
(„mit Root-Rechten ausgeführt"), nicht den Grund. Das Skript
`tools/dsm-diagnose.sh` läuft **auf der NAS** (per SSH) und liefert den
echten Fehlercode:

````bash
sudo sh dsm-diagnose.sh /volume1/pfad/ComfoBoxGateway_noarch-dsm7_1.0.0-0003.spk --install
````

### Der Adapter wird nicht erkannt

Zeigt der Assistent „Kein serieller Port gefunden", sehen fünf verschiedene
Ursachen von außen gleich aus (Hardware, Kernel-Treiber, Mount, Device-Cgroup,
Anwendung). `tools/usb-serial-check.sh` grenzt sie ein – es **ändert nichts**
und nennt am Ende genau eine Ursache:

````bash
sudo sh usb-serial-check.sh            # Containername optional, Vorgabe comfobox-gateway
````

Geprüft wird der Reihe nach: USB-Geräte am Bus (`/sys/bus/usb/devices`,
bekannte Chips `1a86:7523`, `0403:*`, `10c4:*`, `067b:2303`), vorhandene und
geladene Kernelmodule (`ch341`, `ftdi_sio`, `cp210x`, `pl2303`, `usbserial`),
die Gerätedateien auf der NAS, die Sicht des Containers, das Öffnen des Geräts
im Container, die Device-Cgroup sowie `GET /api/config/serial-ports`.

#### Der DSM-Kernel bringt keinen USB-Seriell-Treiber mit

Das ist der häufigste Befund auf einer DS923+ und er hat eine unangenehme
Eigenschaft: **er sieht wie ein kaputter Adapter aus.**

Der Adapter erscheint im `sysfs` (z. B. `1a86:7523  USB2.0-Serial`), aber an
seinem Interface steht **kein Treiber** – und es entsteht kein
`/dev/ttyUSB0`. Ursache: im DSM-Kernel fehlen *alle* USB-Seriell-Module,
einschließlich `usbserial`, des gemeinsamen Unterbaus:

| Modul | Zweck |
|---|---|
| `usbserial` | Unterbau für **alle** USB-Seriell-Chips |
| `ch341` | CH340/CH341 (`1a86:7523`) |
| `ftdi_sio` | FTDI (`0403:*`) |
| `cp210x` | Silicon Labs (`10c4:*`) |
| `pl2303` | Prolific (`067b:2303`) |

`modprobe ch341` scheitert deshalb mit `module not found`. **Ein anderer
Adapter hilft nicht** – auch ein FTDI-Adapter braucht `ftdi_sio`, und das
fehlt genauso. Es gibt drei Wege:

| Weg | Aufwand | Haltbarkeit |
|---|---|---|
| **A** Treibermodule für den eigenen Kernel nachinstallieren ([`robertklep/dsm7-usb-serial-drivers`](https://github.com/robertklep/dsm7-usb-serial-drivers)) | mittel | muss nach **jedem** DSM-Update wiederholt und nach jedem Boot erneut geladen werden (Aufgabe im DSM-Aufgabenplaner: „bei Systemstart") |
| **B** Gateway auf einem Raspberry Pi (HA-Add-on) oder Mini-PC betreiben | gering | dauerhaft – dort ist der Treiber im Kernel |
| **C** Anderer USB-Port / kürzeres Kabel | gering | behebt **nur** Enumeration-Fehler, nicht den fehlenden Treiber |

Weg A ist nicht von Synology unterstützt und kann durch ein DSM-Update
jederzeit wieder verschwinden – das Skript nennt am Ende den passenden Weg für
den konkreten Kernel.

#### Die Device-Cgroup ist der zweite Blocker

Der Mount `/dev:/dev` macht die Gerätedatei im Container nur **sichtbar**.
Ob `open()` gelingt, entscheidet die Device-Cgroup, und Docker erlaubt dort
standardmäßig kein USB-Seriell. Deshalb steht in der Compose-Datei:

````yaml
device_cgroup_rules:
  - "c 188:* rwm"   # ttyUSB*
  - "c 166:* rwm"   # ttyACM*
````

Ohne diese Regeln erscheint `/dev/ttyUSB0` mit Modus `0666` im Container und
lässt sich trotzdem nicht öffnen (`OPEN-FEHLER` in Abschnitt 7 des Skripts).
`group_add` ist dafür **kein** Ersatz – siehe den Kommentar in der
Compose-Datei.

## Sicherheit

- Container läuft **unprivileged** (`USER comfobox`), kein `privileged`
- **Kein** Docker-Socket im Container
- Passwörter liegen ausschließlich in `config.json` (Modus 0600), **nicht** in Env-Variablen
  → erscheinen nicht in `docker inspect`
- Der Assistent öffnet **keine** Firewall-Regeln automatisch

## Bekannte Grenzen

| Grenze | Umgang |
|---|---|
| Erfordert DSM ≥ 7.2.1 | `os_min_ver="7.2-63134"` in der INFO |
| Container Manager muss installiert sein | `install_dep_packages` → DSM installiert ihn bei Bedarf |
| Die Web-UI spricht **HTTP** ohne TLS | im LAN unkritisch; für Zugriff von außen Reverse Proxy nutzen |
| Deinstallation löscht die Container, **nicht** die Daten | Wiederinstallation übernimmt die Konfiguration |

## ⚠️ Fallstrick: `bin/` im Root-`.gitignore`

Das Root-`.gitignore` enthält die Regel `[Bb]in/` und ignoriert damit **jeden** Ordner
namens `bin` — auch `src/target/bin/`. Damit würden die drei Supervisor-Skripte
**stillschweigend nicht versioniert** und das SPK wäre unvollständig.

**Gegenmaßnahme:** `deploy/synology-spk/.gitignore` enthält die Ausnahmen

````
!src/target/bin/
!src/target/bin/**
````

> Git wertet die tiefere `.gitignore` zuerst aus — die Ausnahme schlägt also die
> Root-Regel. Prüfen mit:
> ```bash
> git status --untracked-files=all -- deploy | grep 'target/bin'
> ```
> Fehlt eine der drei `.sh`-Dateien in der Ausgabe, ist die Ausnahme wieder verlorengegangen.
