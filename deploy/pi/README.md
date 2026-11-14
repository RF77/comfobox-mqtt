# ComfoBox Gateway – Installation auf einem Raspberry Pi

Ein Befehl, danach läuft es – inklusive Autostart nach einem Neustart.

````bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/pi/install.sh | sudo sh
````

Anleitung für Anwender: [`docs/benutzer/07b-raspberry-pi.md`](../../docs/benutzer/07b-raspberry-pi.md)

## Dateien

| Datei | Zweck |
|---|---|
| `install.sh` | Das gesamte Installationsprogramm. Erzeugt `/opt/comfobox/compose.yaml` und startet den Container. |
| `serial-port-check.sh` | Diagnose am Pi: findet den Adapter, zeigt die Hub-Kette, prüft Treiber, Rechte und Öffnen und misst die Stabilität. |

## Was das Skript bewusst *nicht* tut

- **Es fragt nichts ab.** Port, Baudrate, Broker und Passwort kommen aus dem
  Getting-Started-Assistenten in der Web-Oberfläche. Ein Dialog im Terminal
  wäre eine zweite Stelle, an der man dasselbe falsch einstellen kann.
- **Es bearbeitet keine `config.json`.** Die Datei liegt im Docker-Volume und
  gehört dem Gateway. Das Skript kennt sie nicht.
- **Es boundet keinen festen Gerätepfad.** Der Container bekommt `/dev`
  durchgereicht und sucht den Port zur Laufzeit – ein später angesteckter oder
  umgesteckter Adapter braucht keine Neuinstallation.

## Entwurfsentscheidungen (und warum)

| Entscheidung | Grund |
|---|---|
| `/dev:/dev` + `device_cgroup_rules` statt `devices:` | Mit `devices:` schlägt `compose up` fehl, wenn der Adapter beim Booten **nicht** steckt. Der Mount ist unabhängig davon, und die Cgroup-Regeln (`c 188:*`, `c 166:*`) erlauben das Öffnen. |
| zusätzlich `group_add: ["20"]` | `/dev/ttyUSB0` gehört `root:dialout` mit Modus `0660`. Der Container läuft als uid 1001 – ohne Gruppe käme `EACCES` beim Öffnen. GID 20 ist auf Debian und Raspberry Pi OS `dialout`. |
| `DOTNET_gcServer=0` | Der Server-GC legt mehrere Heaps an. Auf einem Pi 3 mit 1 GB Speicher ist das die falsche Wahl. |
| `start_period: 60s` | .NET startet auf einem Pi 3 deutlich langsamer als auf einem Server – mit 25 s würde der Healthcheck zu früh greifen. |
| `restart: unless-stopped` + `systemctl enable docker` | Zusammen ergibt das den Autostart. `unless-stopped` (statt `always`) heißt: wer den Container absichtlich stoppt, behält das nach einem Neustart. |
| Version über die GitHub-API, Fallback im Skript | Ein Pi ohne Internet soll trotzdem eine Chance haben. Der eingebettete Wert ist der Stand beim Schreiben – **bei jedem Release mitziehen**. |

## Seriellen Port prüfen

`serial-port-check.sh` ist das Pi-Gegenstück zu
`deploy/synology-spk/tools/usb-serial-check.sh`. Es schreibt **nichts** auf den
Bus und ändert keine Konfiguration.

````bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/pi/serial-port-check.sh -o /tmp/serial-port-check.sh

sudo sh /tmp/serial-port-check.sh                       # vollständiger Lauf
sudo sh /tmp/serial-port-check.sh --free-port --read 5  # Container kurz stoppen, Busdaten mitlesen
sh /tmp/serial-port-check.sh --list                     # nur auflisten (ohne root)
````

Es zeigt dieselben Beweise wie die DS923-Untersuchung: VID/PID des Chips,
die **Hub-Kette** (dort waren es zwei Hubs), den gebundenen Treiber, Rechte und
Group, das Öffnen mit 38400 8N1, N Öffnen/Schließen-Runden, optional Busdaten
und die dmesg-Differenz während des Laufs. Am Ende steht genau eine Ursache
mit dem nächsten Schritt.

> **Der eingebaute Hub des Pi zählt nicht als Zwischenhub.** Ein Pi 3 hat einen
> fest eingebauten LAN9514/LAN7515 (`0424:9514`), an dem Ethernet und die
> USB-Buchsen hängen. Der Adapter erscheint deshalb als `1-1.2` – also „1
> Zwischenhub“, obwohl er direkt an der Buchse steckt. Das Skript erkennt diese
> Kennungen und meldet nur **fremde** Hubs als Fehlerquelle (`davon N extern`).

> Der Gateway hält den Port, solange er läuft. Dann meldet der Test `EBUSY` –
das ist **kein** Fehler. Nur `--free-port` schafft einen freien Port und startet
den Container am Ende wieder (auch bei Abbruch).

## Prüfen

Auf einem Linux-Rechner (nicht nötig auf dem Pi):

````bash
sh -n deploy/pi/install.sh                      # Syntax
sh -n deploy/pi/serial-port-check.sh            # Syntax
sh deploy/pi/install.sh --help                  # Hilfe
sh deploy/pi/serial-port-check.sh --list        # Trockenlauf ohne root
dashboard=…                                     # Trockenlauf mit gefaktem docker siehe unten
````

Ein vollständiger Trockenlauf ohne Docker: `docker`, `curl` und `systemctl`
durch Attrappen ersetzen (`PATH`-Präfix) und im Skript `TARGET_DIR` auf ein
temporäres Verzeichnis umbiegen. Die erzeugte `compose.yaml` lässt sich
anschließend mit `docker compose config` oder `yaml.safe_load` prüfen.

## Fallstricke (verifiziert)

1. **`curl | sudo sh` hat kein `$0`.** Ein `sed` auf die eigene Datei (etwa für
   den Hilfetext) läuft ins Leere, weil `$0` die Shell selbst ist. Der
   Hilfetext steht deshalb als Literal im Skript.
2. **`get.docker.com` installiert kein Compose-Plugin**, wenn Docker schon
   vorhanden ist. Das Skript prüft `docker compose version` und installiert
   `docker-compose-plugin` nach.
3. **32-Bit-Raspberry-Pi-OS ist `armv7l`.** .NET 10 unterstützt `armhf` nicht
   mehr – das Image gibt es nur als `aarch64`. Der Abbruch muss **vor** dem
   Docker-Download kommen, sonst hat der Nutzer minutenlang etwas installiert,
   das er nicht braucht.
4. **Der Docker-Socket ist nach `systemctl start docker` nicht sofort da.**
   Deshalb die Warteschleife vor dem ersten `docker info`.

## Release-Asset

Der Workflow `docker-publish.yml` hängt an jedes Release zusätzlich
`comfobox-gateway-arm64.tar.gz` (Ergebnis von `docker save`). Damit lässt sich
das Image ohne Registry auf einen Pi bringen:

````bash
sudo sh install.sh --offline comfobox-gateway-arm64.tar.gz
````
