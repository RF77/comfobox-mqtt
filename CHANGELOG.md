# Changelog

Alle wesentlichen Änderungen an diesem Projekt.

Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung an [Semantic Versioning](https://semver.org/lang/de/).

> **Status:** Diese Version ist **nicht freigegeben**. Es gab bis heute keinen
> einzigen erfolgreichen Zugriff auf eine echte ComfoBox – siehe
> [README.md](README.md).

## [Unreleased]

### Added

- **Die Web-Oberfläche lässt sich durch Benutzer und Passwort schützen.**
  Einrichtung im Assistenten, spätere Verwaltung unter
  *Konfiguration → Sicherheit*: Benutzername, Passwortwechsel (verlangt das
  aktuelle Passwort – ein gestohlenes Sitzungs-Cookie genügt nicht) und der
  Schalter *Anmeldung erforderlich*. Passwörter liegen als
  PBKDF2-SHA256-Hash (600 000 Iterationen, zufälliges Salt) in der
  `config.json`; ein Passwortwechsel verwirft alle Sitzungen. Ohne
  Administrator lässt sich die Anmeldepflicht nicht einschalten
  (`409 admin_missing`) – sonst wäre es eine Aussperrung.
- **API-Token mit Umfang und Gültigkeitsdauer.** Die Erzeugung fragt drei
  Dinge ab:
  | Feld | Werte |
  |---|---|
  | Name | frei, z. B. `home-assistant` |
  | Umfang | `read` (nur lesen), `control` (regeln), `admin` (verwalten) |
  | Gültig für | unbegrenzt, 30, 90 oder 365 Tage |

  Der Umfang ist die **äußere** Grenze: ein `read`-Token erhält beim
  Schreiben `403 read_only_token` (nicht `401` – sonst liefe der Client in
  eine Anmeldeschleife). Ein abgelaufenes Token wirkt sofort nicht mehr,
  bleibt aber in der Liste und ist dort als *abgelaufen* markiert. Der
  Klartext ist genau einmal sichtbar.
- **Die Oberfläche ist in Deutsch, Englisch und Französisch verfügbar.**
  Typisierte Wörterbücher (`src/app/core/i18n/`); fehlt in einer Sprache ein
  Schlüssel, bricht der TypeScript-Build ab. Die Sprachwahl steht in der
  Anmeldung und in der Navigation, wird im `localStorage` gemerkt und
  entscheidet beim ersten Besuch die Browsersprache. Zahlen und Datumsangaben
  folgen derselben Sprache (`de-DE`, `en-GB`, `fr-FR`).
- **Englische Dokumentation unter [`docs/en/`](docs/en/README.md)** für alle
  drei Betriebsarten: Raspberry Pi (Ein-Befehl-Installation), Synology DSM 7,
  Docker Compose – dazu Home Assistant, Sicherheit/Token, REST-API,
  Fehlersuche und Architektur.
- **`README.md` und Hilfe auf Englisch** neu geschrieben, mit Verweisen auf
  die Dokumentation, die Betriebsarten und den **alten Quellcode**
  ([`src_old/`](src_old/)) – inklusive der Klarstellung, dass diese Version
  **nicht freigegeben** ist, weil noch nie eine echte ComfoBox geantwortet
  hat.

### Changed

- Die Konfigurationsseite ist vollständig übersetzt und enthält jetzt die
  Bereiche *Sicherheit* und *API-Token*; die frühere Checkbox
  „Administrative Rechte" ist durch die Umfangsauswahl ersetzt.
- Der Home-Assistant-Config-Flow nennt den passenden Umfang: `read` genügt für
  Sensoren, `control` ist zum Schreiben nötig.
- Der Diagnose-Tab *BACnet-Bus* übersetzt die Ereignisarten (Token
  übernommen/benutzt/weitergegeben …).

### Fixed

- Ein ungenutzter `KeyValuePipe`-Import in der Diagnose-Komponente erzeugte
  bei jedem Build eine Warnung (`NG8113`).
- Die Sprachwahl war als `readonly`-Array typisiert; `p-select` akzeptiert das
  nicht und der Angular-Build brach mit `TS4104` ab.

### Notes

- **Die Meldungen der REST-API bleiben deutsch.** Der stabile Fehlercode
  (`error`) ist sprachneutral und für Programme gedacht; nur die Oberfläche
  ist übersetzt.
- Die Hilfe-Seiten in der Anwendung (`/help`) bleiben deutsch – sie sind die
  deutsche Benutzerdokumentation (`docs/benutzer/`).

## [1.0.4] – 2026-11-08

### Fixed

- **Der serielle Port wäre im Container nie zu öffnen gewesen.** Der Mount
  `/dev:/dev` macht die Gerätedatei nur **sichtbar**; ob `open()` gelingt,
  entscheidet die Device-Cgroup des Containers – und dort erlaubt Docker
  standardmäßig kein USB-Seriell. `/dev/ttyUSB0` wäre mit Modus `0666`
  erschienen und hätte trotzdem `EPERM` geliefert. Die Compose-Datei setzt
  jetzt:
  ```yaml
  device_cgroup_rules:
    - "c 188:* rwm"   # ttyUSB*
    - "c 166:* rwm"   # ttyACM*
  ```
  Die Werte überleben den Roundtrip des DSM-Workers (er entfernt die
  Anführungszeichen, aus `"c 188:* rwm"` wird weiterhin ein String – anders
  als bei `"20"`, siehe 1.0.3).
- **Das Image meldete die falsche Version.** `ghcr.io/…:1.0.3` antwortete auf
  `/api/status` und `ComfoBox/Version` mit `1.0.1`: die Version kam aus
  `src/Directory.Build.props`, das Image wurde aber über den Release-Tag
  gebaut. Der Wert geht jetzt als `--build-arg VERSION` in
  `dotnet publish /p:Version` und ins OCI-Label
  (`org.opencontainers.image.version`). `Directory.Build.props` ist auf
  `1.0.4` nachgezogen.
- Das HA-Add-on verwies mit `image:` noch auf `:1.0.1`, bei `version: 1.0.3`.
  Jetzt einheitlich.

### Changed

- **`tools/usb-serial-check.sh` korrigiert** – die erste Ausgabe führte an
  zwei Stellen in die Irre:
  - Abschnitt 2 las den Treiber über *alle* Unterverzeichnisse des
    USB-Geräts. Dadurch stand bei jedem Gerät `power -> Treiber: driver`
    (der Name des Symlinks selbst), und ein **nicht** gebundenes Interface
    meldete „Treiber: driver" statt „KEIN Treiber gebunden". Jetzt werden nur
    die Interface-Verzeichnisse (`*:*.*`) geprüft.
  - Der Befund empfahl bei fehlender Gerätedatei pauschal
    `modprobe ch341` und einen FTDI-Adapter. Auf einer DS923+ fehlt aber
    **jedes** USB-Seriell-Modul, auch `ftdi_sio` – der Ratschlag war dort
    falsch. Der Befund unterscheidet jetzt „Modul vorhanden, nicht geladen"
    von „Kernel hat gar keinen Treiber" und nennt für den zweiten Fall die
    realen Wege.
  - Neu: Erkennung fehlerhafter Enumerationsversuche (`error -32`/`-71`,
    „unable to enumerate", „attempt power cycle"), Anzahl der Zwischen-Hubs
    vor dem Adapter, Prüfung von `modules.builtin` und der Nachweis, ob die
    Device-Cgroup `c 188:` erlaubt.

### Added

- **Installation auf einem Raspberry Pi in einem Befehl.**
  ```bash
  curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/pi/install.sh | sudo sh
  ```
  `deploy/pi/install.sh` prüft die Architektur (32-Bit wird mit Begründung
  abgelehnt – das Image gibt es nur als `aarch64`), installiert Docker samt
  Compose-Plugin, aktiviert den Docker-Dienst für den **Autostart**, schreibt
  `/opt/comfobox/compose.yaml` und startet den Container. Wiederholbar: ein
  erneuter Aufruf aktualisiert das Image, die Daten bleiben.
  Der Container bekommt `/dev` durchgereicht (Adapter darf später gesteckt
  werden) plus `device_cgroup_rules` für `ttyUSB`/`ttyACM` und
  `group_add: ["20"]` für `dialout`. `DOTNET_gcServer=0`, weil der Server-GC
  auf einem Pi 3 mit 1 GB Speicher die falsche Wahl ist.
- **Das Image liegt an jedem Release als `comfobox-gateway-arm64.tar.gz`**
  (`docker save`). Damit lässt sich ein Pi ohne Registry-Zugriff versorgen:
  `sudo sh install.sh --offline comfobox-gateway-arm64.tar.gz`.
- **Neue Benutzerseite [`docs/benutzer/07b-raspberry-pi.md`](docs/benutzer/07b-raspberry-pi.md)** –
  Voraussetzungen, der Befehl, Assistent, HACS-Integration, Betrieb, Fehlersuche.

## [1.0.3] – 2026-11-08

### Fixed
- **Die Ursache für `fail compose up` (2201) ist gefunden: eine einzige
  Zeile in der Compose-Datei.** Der DSM-Worker `docker-project` benutzt
  unsere Datei **nicht** unverändert. Er liest sie ein, ergänzt `user:` und
  `cap_add:` und schreibt sie als `docker-compose.admin.yaml` neu — dabei
  **gehen die Anführungszeichen verloren**:
  ```yaml
  group_add:
    - "20"        # unsere Datei
  ```
  wird zu
  ```yaml
  group_add:
    - 20          # die Datei des Workers
  ```
  Sein eigener Parser erwartet an dieser Stelle einen String und bricht ab:
  ```
  1 error(s) decoding:
  * 'group_add[0]' expected type 'string', got unconvertible type 'int', value: '20'
  ```
  Genau das ist der Grund, warum der Fehler nur beim Paket auftrat: Beim
  manuellen Anlegen im Container Manager wird unsere Datei **mit**
  Anführungszeichen benutzt — dort lief alles.
  → `group_add` ist entfernt; der Zugriff auf `/dev/ttyUSB*` läuft über den
  Mount `/dev`. `tools/check-spk.sh` lehnt `group_add` jetzt ab, und
  `dsm-diagnose.sh` simuliert den Roundtrip des Workers (entfernt die
  Anführungszeichen und lässt Compose die Datei erneut prüfen).
- **Der Container läuft auf dem NAS unter der Paket-Kennung, nicht als
  `uid 1001`.** Der Worker schreibt `user: <paket-uid>` in die erzeugte
  Datei. Das Datenverzeichnis im Image gehört aber `comfobox` (1001) und hat
  `0755` — der Prozess könnte weder `config.json` schreiben noch das Log
  anlegen und würde sofort sterben. `/var/lib/comfobox` und
  `/var/log/comfobox` sind deshalb jetzt `0777`.

### Added
- **`tools/usb-serial-check.sh` – findet den USB-RS485-Adapter oder den Grund,
  warum er fehlt.** Zeigt der Assistent „Kein serieller Port gefunden", sehen
  fünf Ursachen von außen gleich aus: Hardware, fehlender Kernel-Treiber,
  fehlende Durchreichung in den Container, Device-Cgroup (sichtbar, aber nicht
  öffenbar) oder ein Fehler in der Anwendung. Das Skript ändert **nichts** und
  prüft der Reihe nach: USB-Geräte am Bus samt bekannter Seriell-Chips
  (`1a86:7523`, `0403:*`, `10c4:*`, `067b:2303`), vorhandene und geladene
  Kernelmodule (`ch341`, `ftdi_sio`, `cp210x`, `pl2303`, `usbserial`), die
  Gerätedateien auf der NAS, die Sicht des Containers, einen echten
  Öffnen/Schließen-Test im Container und `GET /api/config/serial-ports`.
  Am Ende steht genau ein Befund mit dem nächsten Schritt.
- `dsm-diagnose.sh --start`: startet das Paket und prüft Containerzustand,
  Health, Container-Kennung, **Schreibzugriff auf die seriellen Geräte**,
  `/api/health` und das Log. Nötig, weil `synopkg install` das Paket
  absichtlich gestoppt zurücklässt — der Container steht danach auf `Created`.
- `dsm-diagnose.sh --pkguser-probe`: prüft, unter welcher Kennung der Worker
  arbeitet, ob der Paket-Benutzer an den Docker-Daemon kommt und in den
  Docker-Share schreiben darf, zeigt die Rechte der seriellen Geräte und
  startet die echte Worker-Datei als `root` und als Paket-Benutzer — mit der
  vollständigen Fehlermeldung, die DSM sonst verschluckt.
- `dsm-diagnose.sh` zeigt direkt nach einem Fehlschlag den Inhalt der vom
  Worker erzeugten `docker-compose.admin.yaml` und sichert sie nach
  `/tmp/cb-compose-after-install`.

### Changed
- Das Paket liefert die Compose-Datei unter **beiden** Namen aus
  (`compose.yaml` und `docker-compose.yaml`, byte-gleich). Die offizielle
  Entwicklerdoku nennt `compose.yaml`, die Namensliste im Worker-Binary
  `docker-compose.yaml` — welchen Namen der Worker sucht, ist damit
  gleichgültig.

## [1.0.2] – 2026-11-08

### Fixed
- **Der Dateiname ist jetzt doppelt abgesichert (Revision 0013).** Die
  Diagnose vom 08.11. hat gezeigt, dass die Begründung aus 0012 auf einer
  **unvollständigen** Grundlage stand: die `strings`-Ausgabe aus
  `libdockerprojectgeneratorworker.so` war mit `head -n 30` **abgeschnitten**.
  Damit war „`compose.yaml` kennt der Worker nicht“ **kein Beweis**.
  Die offizielle Entwicklerdoku *Docker Project Worker* sagt dagegen
  ausdrücklich: `path` ist das *„directory of **compose.yaml**, relative path
  from target“* – und ihr Beispiel zeigt `django/compose.yaml`.
  → `build-spk.sh` legt die Datei jetzt unter **beiden** Namen ab
  (`compose/docker-compose.yaml` **und** `compose/compose.yaml`,
  byte-gleich), `tools/check-spk.sh` prüft beide und vergleicht sie mit `cmp`.
  Ausserdem wurde bestätigt: `compose.yaml` war in Revision 0011 **nie** unter
  sauberen Bedingungen getestet – damals trug das Daten-Volume noch das
  Projekt-Label `comfobox`, was für sich schon `2201` auslöst.
- **Kollisionsprüfung vor jeder Installation** (`precheck_conflicts` in
  `tools/dsm-diagnose.sh`): Container mit gleichem Namen, Daten-Volume mit
  fremdem Projekt-Label, belegter Port 8081 und gleichnamiger
  Container-Manager-Datensatz. Der Lauf vom 08.11. 19:50 hat gezeigt, dass
  das Aufräumen wirklich greift (`frei - nichts im Weg`) – der Fehler lag
  also nicht an Resten.
- **Das Diagnose-Skript sichert jetzt die Datei, die der Worker selbst
  erzeugt.** In `<target>/compose/` lag nach einem Fehlschlag ein
  `docker-compose.admin.yaml` mit **953 Byte**, während unsere Datei
  5364 Byte hat – der Worker arbeitet also mit einer eigenen Kurzfassung.
  `show_worker_compose_leftovers` gibt sie direkt nach dem Fehlschlag aus und
  kopiert sie nach `/tmp/cb-compose-after-install`, bevor der nächste Lauf
  das Paketverzeichnis löscht.
- **Die Container-Wache liess `docker events` weiterlaufen.** `kill` traf nur
  das Subshell; der Prozess schrieb danach in ein gelöschtes Verzeichnis
  (`containers.log: No such file or directory`, vier Mal im Lauf vom 08.11.).
  Jetzt wird er ausdrücklich beendet (`pkill -f 'docker events --filter'`) und
  die Schleife bricht ab, wenn das Verzeichnis verschwindet.
- **Die Installation scheiterte an einem Dateinamen.** Der DSM-Worker
  `docker-project` sucht die Compose-Datei mit einer **fest verdrahteten
  Namensliste** – nachgewiesen mit `strings` aus
  `libdockerprojectgeneratorworker.so`:
  ```
  docker-compose.yaml
  docker-compose.yml
  docker-compose.admin.yaml
  ```
  Unser Paket lieferte **`compose/compose.yaml`**. Diesen Namen kennt nur
  Docker Compose selbst, nicht der Worker. Er fand die Datei nicht, brach ab,
  **bevor** er Docker anfasste (kein Volume, kein Container, keine Logzeile)
  und meldete nur:
  ```
  project build fail: [`2201`] `fail compose up '<uuid>'`
  ```
  → Die Datei heisst jetzt **`compose/docker-compose.yaml`**. `build-spk.sh`
  entfernt einen alten `compose.yaml` vorsorglich, `tools/check-spk.sh` und
  `tools/check-compose.sh` prüfen den Namen, die Diagnose sucht alle drei
  zulässigen Varianten.
- **Was vorher ausgeschlossen wurde** (alles mit echtem Gerät geprüft): die
  Compose-Datei selbst (fünf Varianten bis „nur `image`" liefen durch), das
  Image (Smoke-Test: `Status=running`, `/api/health` → 200), der Projektname,
  `build_params.build = false`, verwaiste Volumes/Container/Netzwerke und
  fehlende CM-Projekt-Datensätze. Auch das CLI-Plugin `docker compose` ist
  vorhanden und verarbeitet unsere Datei fehlerfrei.
- **Die Volume-Sicherung im Diagnose-Skript lief ins Leere.** Der
  Hilfs-Container lief mit der Standardkennung des Images – das Gateway
  läuft als `uid 1001` und konnte das Backup-Verzeichnis (root, 0755) nicht
  beschreiben. `cp` scheiterte, `|| true` verschluckte den Fehler, und weil
  die Sicherung „fehlschlug“, wurde das Volume **nicht** gelöscht. Ergebnis:
  `--install-fresh` lief ohne Wirkung durch. Jetzt läuft der Container mit
  `-u 0:0`, das Verzeichnis wird auf `0777` gesetzt, das Ergebnis wird
  geprüft (nicht die Ausgabe) und nach dem Löschen wird **verifiziert**,
  dass das Volume weg ist – sonst bricht das Skript ab.
- **Neue Diagnose-Werkzeuge** (`deploy/synology-spk/tools/dsm-diagnose.sh`):
  `--app-smoke` (startet das Projekt wie der Worker und zeigt `docker logs` –
  `compose up -d` allein beweist **nicht**, dass der Container lebt),
  `--reset-volume`, `--install-fresh`, `--install-trace` (mit
  Container-Wache: kopiert `docker inspect` und `docker logs` im Moment von
  `create`, bevor der Rollback löscht) und `--compose-matrix`.
  Neu ausserdem `tools/probe-worker.sh`: zeigt in fünf Sekunden, welchen
  Compose-Aufruf und welche Dateinamen der Worker benutzt.
- **Das Diagnose-Skript zeigt jetzt den Grund.** Neu: `conf/resource` aus
  dem Paket samt Prüfung, ob der dort genannte `path` zum Inhalt von
  `package.tgz` passt; die **Syslog-Meldungen des Resource-Workers** aus
  `/var/log/messages` (dort schreibt er – nicht in `synopkg.log`); der
  Docker-Zustand (Container, Netzwerke, Volumes, Container Manager) und ein
  Vergleich mit anderen Paketen auf derselben NAS, die denselben Worker
  nutzen.
- **`tools/check-spk.sh` prüft `conf/resource` jetzt gegen das Paket:** der
  Projektname muss für Docker Compose gültig sein (klein geschrieben), und
  `<path>/compose.yaml` muss genau dort in `package.tgz` liegen, wo der
  Worker sucht.
- **Der 24-h-Schreibzähler (EEPROM-Schutz) geht beim Deinstallieren nicht
  mehr verloren.** Der DSM-Worker gibt das Projekt und damit das Volume
  `comfobox-data` frei, bevor `postuninst` läuft. Das `preuninst` sichert
  den Zähler deshalb jetzt nach `var/`, und das `postinst` spielt ihn beim
  nächsten Installieren wieder ins Volume zurück. Beide Schritte sind
  „best effort“ – ein Fehler darf die Installation **nie** verhindern.
- **Der Image-Build braucht für die Synology nicht mehr ewig.** Der
  Multi-Arch-Build (`linux/amd64,linux/arm64`) läuft unter QEMU – darin ist
  der Angular-Build um ein Vielfaches langsamer. `workflow_dispatch` hat jetzt
  das Input `platforms`; für die DS923+ genügt `linux/amd64` (rund
  10 Minuten statt über 30). Bei einem Tag-Push wird weiterhin multi-arch
  gebaut – das HA-Add-on auf dem Raspberry Pi braucht `arm64`.

## [1.0.1] – 2026-11-07

### Fixed
- **Der Container stuerzte sofort wieder ab** („fail compose up“). Ursache:
  Das DSM loescht bei jeder Installation `/var/packages/<paket>` und legt es
  neu an. Legt Docker einen **fehlenden** Bind-Mount-Quellpfad an, gehoert er
  danach **root:root** (Modus 0755). Der Container laeuft aber als uid 1001
  und kann dort weder die `config.json` schreiben noch den
  **24-h-Schreibzaehler** sichern – Serilog bricht den Start mit einer
  Ausnahme ab, **bevor** der erste Logeintrag geschrieben wird. Der
  Container startete, stuerzte sofort wieder ab, und der DSM-Worker meldete
  nur „fail compose up“.
  → Die Compose-Datei verwendet jetzt das **benannte Docker-Volume
  `comfobox-data`** (fester Name, wie beim manuellen Betrieb und im
  Backup-Skript). Docker uebernimmt daraus Inhalt und Rechte des Images –
  der Container kann dort immer schreiben.
- **Der Gateway startet nicht mehr am Log-Verzeichnis.** Serilog legt das
  Verzeichnis beim Start selbst an – kann er das nicht, bricht der Start
  mit einer Ausnahme ab, ohne eine einzige Zeile zu protokollieren. Der
  Gateway prueft das Verzeichnis jetzt, weicht bei Bedarf auf ein
  beschreibbares aus und meldet die Abweichung im Log.
- **Der Container liess sich nicht starten** („failed to acquire postinst
  worker“ / „fail compose up“). Drei Korrekturen an der Compose-Datei und
  am `preinst`:
  - Das Feld `version` fehlte – bei Docker Compose v1 (unter 1.27) ist es
    **Pflicht**, ohne das Feld lehnt der Parser die Datei ab.
  - Die persistenten Daten lagen über **zwei** Mounts, deren Host-Pfade
    ineinander lagen (`var` und `var/logs`). Jetzt gibt es **einen** Mount;
    das Log-Verzeichnis liegt im Datenverzeichnis
    (`COMFOBOX_Logging__Directory=/var/lib/comfobox/logs`).
  - Das Datenverzeichnis wird jetzt im **`preinst`** angelegt, nicht erst im
    `postinst`. Grund: der DSM-Worker läuft **vor** dem `postinst`. Legt
    Docker ein fehlendes Verzeichnis an, gehört es `root:root` – der
    Container läuft aber als uid 1001 und könnte dann weder die
    `config.json` schreiben noch den **24-h-Schreibzähler** sichern.
- **Das Diagnose-Skript kann den DSM-Worker jetzt nachstellen.** Der neue
  Modus `--worker` macht exakt das, was der Worker tut (`docker compose up`)
  und zeigt die **volle** Docker-Fehlermeldung. Das DSM meldet dazu nur
  „fail compose up“ – ohne die Ursache. Der Modus installiert das Paket
  **nicht** und räumt das Projekt danach wieder auf.
- **Der DSM-Worker „docker-project“ ist gescheitert** – die eigentliche
  Ursache der gescheiterten Installation:
  ````
  Acquire docker-project for ComfoBoxGateway when 0x0001 (fail)
  ````
  Das `package.tgz` enthielt die Dateien eine Ebene zu tief
  (`target/compose/compose.yaml`). Das DSM entpackt `package.tgz` aber
  **direkt in das Zielverzeichnis** – im DSM-Log:
  ````
  mv -f …/install.XXXX/package /usr/local/packages/@appstore/ComfoBoxGateway
  ````
  Die Compose-Datei landete damit in `<ziel>/target/compose/…`, während der
  Worker sie unter `<ziel>/compose` sucht (`conf/resource`: `"path": "compose"`,
  relativ zum Ziel). Ergebnis: **Worker `(fail)`, Installation abgebrochen.**
  → `package.tgz` enthält die Dateien jetzt direkt auf der obersten Ebene
  (`bin/`, `compose/`, `image`).
- **Fehler 261 („package does not have preupgrade / postupgrade script“).**
  DSM 7.4 lässt ein fehlgeschlagenes Paket im Status **„broken“** (150)
  zurück und versucht beim nächsten Mal ein Update statt einer Neuinstallation
  – das scheitert ohne die beiden Skripte. → `preupgrade` und `postupgrade`
  sind jetzt im Paket.
- **Die Installation brach trotz aller vorherigen Korrekturen weiterhin ab**
  („Paket kann nicht installiert werden“). Ursache: `preinst` lud die
  Hilfsdatei `common.sh` aus `target/` – aber das `package.tgz` wird erst
  **nach** `preinst` entpackt. In dieser Phase existiert die Datei also noch
  gar nicht, das Skript endete mit `exit 1`, und das Paketzentrum zeigte nur
  die generische Meldung. `preinst` ist jetzt **eigenständig** und endet
  immer mit `0`.
- **`common.sh` wurde über genau einen festen Pfad geladen.** Das DSM führt
  die Lifecycle-Skripte je nach Phase aus verschiedenen Verzeichnissen aus
  (Installation: `<spk-temp>/scripts/`, Betrieb: `<installiert>/scripts/`).
  Die Skripte suchen `common.sh` jetzt an allen üblichen Orten.
- **Das Datenverzeichnis lag auf der Systempartition.** `common.sh` benutzte
  `SYNOPKG_PKGVAR` – diese Variable gibt es in DSM 7 nicht. Daraus wurde
  `<target>/var` statt `/var/packages/ComfoBoxGateway/var`: Konfiguration und
  24-h-Schreibzähler wären beim Update oder Neustart verloren gegangen.
- **Compose-Datei: Gruppenname `dialout` → numerische GID `20`.** Ein auf dem
  NAS fehlender oder anders benannter Gruppen hätte die Container-Erstellung
  und damit die Installation scheitern lassen.
- **Compose-Datei: Healthcheck von `wget` auf `curl` umgestellt.** Das Image
  enthält `curl`; `busybox-wget` liefert unter manchen Versionen keinen
  sauberen Exit-Code.
- **`tools/check-spk.sh` prüft die Lifecycle-Skripte jetzt mit.** Neu:
  `preinst` darf keine Datei aus `target/` voraussetzen, kein Skript darf
  `set -u` setzen, `common.sh` darf `SYNOPKG_PKGVAR` nicht verwenden und
  muss an mehreren Orten gesucht werden.
- **Das Synology-Paket installierte sich auf DSM 7.4 nicht.** Zwei Fehler
  hintereinander:
  1. Die Datei `conf/privilege` lag im Repository, wurde von `build-spk.sh`
     aber **nie in das SPK gepackt** (im `tar`-Aufruf fehlte `conf`) – und war
     nicht einmal im Git-Repository. Ohne sie gilt das Paket als Root-Paket.
  2. Die Datei war **inhaltlich falsch**: DSM 7.4 lehnt sie mit **Fehler 319**
     („invalid package privilege content“) ab. Ursache ist der Block
     `ctrl-script` mit `"run-as": "root"`: DSM 7 lässt Drittanbieter-Pakete
     **nicht** mit Root-Rechten laufen. Nachgewiesen mit dem Variantentest auf
     einem echten DSM 7.4.1 – mit `ctrl-script` immer Fehler 319, ohne
     `ctrl-script` wird die Datei akzeptiert.
- **Das Paket braucht keine Root-Rechte mehr.** Statt der Lifecycle-Skripte
  im Root-Kontext arbeitet jetzt der DSM-Worker **„docker-project“**
  (`conf/resource`). Er legt das Compose-Projekt an und startet bzw. stoppt
  es beim Start/Stopp des Pakets – als Root. `conf/privilege` enthält
  nur noch `defaults`, `username` und `groupname` (Muster des amtlichen
  DSM-Beispiels und des auf derselben NAS laufenden Plex-Pakets).
- **Der Container wird nicht mehr neu erstellt, wenn der Serial-Port
  wechselt.** Die Compose-Datei bindet `/dev` ein; der Gateway wählt den
  Port zur Laufzeit aus der Konfiguration aus.
- **Die Installation brach mit „failed to run preinst script“ ab.** Ursache
  war `set -u` zusammen mit der in dieser Phase nicht gesetzten Variablen
  `SYNOPKG_PKGDEST` („unbound variable“). Die Skripte laden ihre
  Hilfsdateien jetzt über den relativen Pfad und arbeiten ohne `set -u`.
- **Ein fehlender Container Manager brach die Installation ab.** Er wird
  jetzt nur noch gemeldet – das Paketzentrum installiert die Abhängigkeit
  bei Bedarf selbst.
- **`build-spk.sh` erzwingt jetzt LF-Zeilenenden** für `conf/privilege`. Mit
  `core.autocrlf=false` wandert das Windows-CRLF unverändert in den Blob und
  von dort ins SPK.
- **`tools/check-spk.sh` prüft die privilege-Datei jetzt inhaltlich**: kein
  CR-Zeichen, kein `groupname`, keine Actions `prestart`/`prestop`.
- **Der Build ist jetzt reproduzierbar.** Derselbe Quellstand ergibt
  dieselbe MD5 – vorher wanderten Zeitstempel und Reihenfolge in das Archiv,
  sodass die `checksum` in der `INFO` und die MD5 in der `packages.json`
  nicht nachrechenbar waren. `tar` läuft jetzt mit `--sort=name`,
  `--mtime=@<Commit-Zeitstempel>`, `--owner=root --group=root`.
- **Das Paket-Icon war 256×256 statt 64×64.** DSM 7.0 verlangt für
  `PACKAGE_ICON.PNG` genau 64×64 (Breaking Change, vorher 72×72). Die große
  Variante liegt jetzt als `PACKAGE_ICON_256.PNG` daneben.
- **Die Sichtbarkeits-Prüfung des Container-Images war falsch.** Der
  Manifest-Abruf `https://ghcr.io/v2/…/manifests/<tag>` liefert **immer**
  `401` – auch bei öffentlichen Images, weil die Registry zwingend einen
  Bearer-Token verlangt und ein Browser den nicht selbst anfordern kann. Die
  Anleitung deutete das `401` aber als „privat" und schickte den Nutzer auf
  eine sinnlose Fährte. Neu: Prüfung über die GitHub-Paket-Seite, über
  `docker pull` oder über einen anonymen Pull-Token.

### Added
- **CI: der Image-Workflow prüft jetzt den Anonymous-Pull.** Nach dem Push
  fragt er das Manifest mit einem **anonymen** Token ab – genau das, was die
  Synology und der HA-Supervisor tun. Bisher meldete er nur die
  API-Sichtbarkeit, die nichts über den Anonymous-Pull aussagt. Der Job wird
  jetzt rot, wenn das Image nicht anonym ziehbar ist.
- **`deploy/synology-spk/tools/check-spk.sh`** – prüft das fertige Paket
  gegen die Anforderungen des DSM-Paketcenters (`conf/privilege` vorhanden
  **und** an der richtigen Stelle, `run-as: package`, Icon-Größen,
  INFO-Pflichtfelder, `os_min_ver ≥ 7.0-40000`, Image-Tag). Läuft in der CI.
- **`deploy/synology-spk/tools/dsm-diagnose.sh`** – läuft auf der NAS (per
  SSH) und liefert DSM-Version, Paketinhalt, `privilege`-Datei und den
  **echten Fehlercode** von `synopkg install` (JSON). Das DSM nennt im Dialog
  nur die Ursache, nicht den Grund. Mit `--test` probiert das Skript die
  möglichen Varianten der `privilege`-Datei **durch** und installiert die
  erste, die funktioniert.

### Changed
- Die **SPK-Revision** steht jetzt in `deploy/synology-spk/REVISION`, die
  SPK-Version in `deploy/synology-spk/src/VERSION` – der lokale Build und die
  CI bauen damit garantiert dasselbe Paket. Eine hart kodierte Revision im
  Workflow driftete sofort auseinander.
- Die **Version** des Gateways steht in `src/Directory.Build.props` und
  stimmt jetzt mit dem Release-Tag überein (`ComfoBox/Version` trägt
  `Ver 1.0.1`).
## [Unreleased]

### Added
- **Release `v1.0.0`** – das Synology-Paket (.spk) und die Paketquelle
  (`packages.json`) liegen als Release-Asset im Repository, das
  Container-Image (linux/amd64 + linux/arm64) in GHCR
- Phase 0: `src/global.json` (SDK-Pinning auf 10.0.400)
- Phase 0: `src/Directory.Packages.props` (Central Package Management)
- Phase 0: `src/Directory.Build.props` (Nullable, `TreatWarningsAsErrors`, C# 14, Lock-Dateien)
- Phase 0: `src/ComfoBox.sln` mit sieben SDK-Projekten
  (`ComfoBox.Domain`, `ComfoBox.Bacnet`, `ComfoBox.Gateway` + vier Testprojekte)
- Phase 0: CI-Workflow `.github/workflows/build-test.yml` (Build, Test, Coverage, NuGet-Vulnerability-Scan)
- Phase 0: Generator `tools/generate-topics/generate-topics.mjs` – reproduziert
  `docs/topics.json` byteidentisch (127 Items) und prüft sie in CI
- Phase 1: `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` – 1:1-Port des Legacy-Codecs
  (Fremdcode, MIT; Herkunft und Änderungen in `Bacnet/HEADER-LIZENZ.txt`)
- Phase 1: Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  mit 25 Frames, erzeugt vom Legacy-Codec über `tools/port-bacnet/oracle/`
- Phase 1: `ComfoBox.Bacnet.Diagnostics.BacnetLog` – `ILogger`-Fassade für den
  statischen Fremdcode (ersetzt `System.Diagnostics.Trace`, Befund A16)
- Phase 1: `tools/port-bacnet/port-bacnet-base.mjs` – mechanischer, idempotenter Port
- Phase 1: `ComfoBox.Bacnet.BacnetClient` – **async** BACnet-Client
  (`ReadPropertyAsync`, `WritePropertyAsync`, `WhoIsAsync`, `IamReceived`-Ereignis)
- Phase 1: typisierte BACnet-Fehler (`BacnetTimeoutException`, `BacnetDeviceErrorException`,
  `BacnetDeviceAbortException`, `BacnetDecodeException`) statt `Exception("…")`
- Phase 1: `ComfoBox.Bacnet.BacnetMstpTransport` – MS/TP-Transport mit Port-Verwaltung,
  Reconnect und exponentiellem Backoff (5 s → 30 s)
- Phase 1: `ComfoBox.Bacnet.Serial` – schmale Port-Schnittstelle (`ISerialPort`,
  `ISerialPortFactory`, `SerialPortAdapter`) als Testnaht
- Phase 1: `MstpStateMachine` – 1:1-Port des Legacy-MS/TP-Automaten
- Phase 1: 19 neue Tests (MS/TP-Framing, Serial-Verhalten inkl. A8-Regression,
  Client-Request/Antwort/Fehler) → 45 Tests grün
- Phase 2: `ComfoBox.Domain` – der **Item-Katalog mit 127 Items**
  (`ItemDefinition`, `ItemValueKind`, `RefreshPriority`, `ItemCatalog`),
  deklarativ und ohne Reflection
- Phase 2: `ComfoBox.Domain.Items.Catalog` + `ItemEnums` werden aus dem
  Legacy-Quelltext **generiert** (`tools/generate-topics/generate-topics.mjs
  --catalog`) – inklusive BACnet-ID, Einheit, Grenzen, Schreibbarkeit,
  Expert-Modus, **Priorität** und der exakten Enum-Namen
- Phase 2: `ComfoBox.Domain.Values` – `ItemValue`, `AnalogValue`, `EnumValue`
  (kultur-invariant, `InvariantCulture`)
- Phase 2: `ComfoBox.Gateway` – Host mit DI, Options-Bindung und
  **Fail-Fast-Validierung** (`ValidateOnStart`)
- Phase 2: `ComfoBox.Gateway.Bacnet.BacnetDevice` – **ein** `SemaphoreSlim` für
  alle Buszugriffe, Priorisierung (`Once`/`Primary`/`Secondary`), Timeout je
  Item, Fehlerbehandlung ohne Abbruch des Zyklus
- Phase 2: `ComfoBox.Gateway.Bacnet.PollPlan` – welcher Zyklus welche Items liest
- Phase 2: `ComfoBox.Gateway.ValueCache` – letzter Wert + Zeitstempel,
  `IsStale` (3 Poll-Zyklen), SSE-Abonnenten
- Phase 2: `ComfoBox.Gateway.Protection.WriteCounter` – **persistenter**
  24-h-Schreibzähler (JSON-Datei, atomar geschrieben)
- Phase 2: `ComfoBox.Gateway.Health.ErrorCollector` – Fehler-Senke (letzte 50)
- Phase 2: `ComfoBox.Gateway.Logging.InMemoryLogSink` – Serilog-Sink mit
  In-Memory-Ringpuffer (1000 Einträge) für den Log-Viewer der Web-UI
- Phase 2: `ComfoBox.Gateway.Logging.SecretRedactor` – Passwörter und Tokens
  werden **vor** dem Speichern maskiert
- Phase 2: `ComfoBox.Bacnet.IBacnetClient` – Testnaht, damit der Gateway ohne
  Hardware getestet werden kann
- Phase 2: 98 neue Tests (30 Domain, 10 Contract, 58 Gateway) → **143 Tests grün**
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttConnectionFactory` – MQTTnet-5-Optionen
  (TLS, Auth, Keepalive 30 s, Protokoll v5, **zwei** Last Wills)
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttConnectionManager` + `BrokerConnection` –
  Connect/Reconnect mit exponentiellem Backoff, orderly Shutdown je Broker
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttClientRegistry` – Verzeichnis der
  verbundenen Clients (bricht den zirkulären DI-Pfad)
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttPublisher` – Publish mit wählbarem
  QoS/Retain; ein defekter Broker blockiert die anderen nicht
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttSubscriber` – `/Set`- und
  `/AsNumber/Set`-Topics; das Schreiben läuft **ausschließlich** über
  `BacnetDevice` (der 24-h-Zähler kann nicht umgangen werden)
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttPayload` – kultur-invariante Zahlen und
  ein Enum-Parser **ohne** 0-Fallback
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttTopicMap` – die einzige Stelle, an der
  Topic-Adressen gebildet werden
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttStatePublisher` – die Item-Werte auf
  ihren Topics (aus dem Wert-Cache, inkl. `AsNumber` bei Enums)
- Phase 3: `ComfoBox.Gateway.Mqtt.HomeAssistantDiscovery` – Discovery für alle
  127 Items, das Gerät und die Status-Entitäten (retained, QoS 1)
- Phase 3: `ComfoBox.Gateway.Mqtt.HomeAssistantBirthHandler` – reagiert auf die
  „Geburt" von Home Assistant (`homeassistant/status` = `online`) und wiederholt
  Discovery und Zustände
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttStatusPublisher` – `Status/Health`,
  `Status/Uptime`, `Status/Writes`, `Status/Bacnet`, `Status/Mqtt`,
  `Status/LastError`, `Status/Errors`
- Phase 3: `ComfoBox.Gateway.Health.HealthReporter` – Health-Aggregation über
  die Komponenten (Mqtt, Bacnet) mit `online`/`degraded`/`offline`
- Phase 3: `ComfoBox.Gateway.GatewayServiceCollectionExtensions` – die gesamte
  DI-Registrierung (aus `Program.cs` herausgezogen, damit sie testbar ist)
- Phase 3: 76 neue Tests (Payload, Topic-Vertrag, Subscriber, Discovery,
  Health, MQTT-Integration gegen einen eingebetteten Broker, End-to-End) →
  **219 Tests grün**
- Phase 4: `ComfoBox.Gateway.Api.GatewayApiExtensions` – `AddGatewayApi()` (DI)
  und `MapGatewayApi()` (Routen) – die REST-Schnittstelle liegt **nicht** in
  `Program.cs`, damit die Tests sie isoliert aufbauen können
- Phase 4: Item-Endpunkte – `GET /api/items`, `GET /api/items/{id}`,
  `GET /api/items/group/{group}`, `PUT /api/items/{id}` mit den Statuscodes
  204/400/403/409/422/502; **jeder** Schreibvorgang läuft über
  `BacnetDevice.WriteAsync` (der 24-h-Zähler ist nicht umgehbar)
- Phase 4: `GET /api/health`, `GET /api/status`, `GET /api/diagnostics/errors`,
  `GET /api/diagnostics/writes` – die Feldnamen von `/api/status` sind
  **Vertrag** (die Home-Assistant-Integration liest sie)
- Phase 4: SSE-Stream `GET /api/events` mit den Ereignissen `snapshot` und
  `value`
- Phase 4: `GET`/`PUT /api/config` – `GET` liefert die Konfiguration **ohne**
  Passwörter (nur `passwordSet`), `PUT` schreibt nach `config.json`
  (Modus 0600)
- Phase 4: `POST /api/service/restart` → 202; der Neustart läuft **in-process**
  (`IHostApplicationLifetime.StopApplication()`) – kein Docker-Socket nötig
- Phase 4: OpenAPI/Swagger unter `/swagger` (nur im Development-Modus) –
  Swashbuckle **10.3.1**
- Phase 4: Setup-API `/api/setup/*` – Zustand, serielle Ports (aus
  `ports.json`), freier Web-Port, BACnet-/MQTT-Test mit maschinenlesbarem
  `hint`, Konfiguration schreiben (Passwort **serverseitig** gehasht),
  `apply` → Neustart
- Phase 4: Token-API `/api/security/tokens` – der Klartext ist **einmal**
  sichtbar, gespeichert wird nur der PBKDF2-Hash (`pbkdf2-sha256$600000$…`)
- Phase 4: Log-Endpunkte `/api/diagnostics/logs` (Filter level/source/search/
  since/limit/order, Obergrenze 1000), `/stream` (SSE-Tail mit Initial-
  Snapshot, max. 5 gleichzeitige Streams) und `DELETE`
- Phase 4: `ComfoBox.Gateway.Security.ApiAccessGuard` – die Zugriffskontrolle
  (Bearer-Token, Basic-Auth des Administrators, LAN-Modus) – siehe
  [ADR-012](docs/05-entscheidungen/adr-012-api-zugriffskontrolle.md)
- Phase 4: `ComfoBox.Gateway.Storage.AtomicFile` – atomisches Schreiben mit
  Retry (Windows sperrt die Datei unter Umständen für einen Moment)
- Phase 4: `Gateway:DeviceId` (Standard `comfobox1`) und
  `Gateway:RestartEnabled` als neue Einstellungen; `Api:RequireAuthentication`,
  `Api:SuggestedUiPort`, `Api:BacnetTestTimeoutSeconds`,
  `Api:MqttTestTimeoutSeconds` als neuer `Api`-Abschnitt
- Phase 4: 59 neue Tests (Items, Status, SSE, Config, Neustart, Token, Setup,
  Log) gegen einen **echten** Kestrel-Testhost → **278 Tests grün**
- Phase 5 (Aufgabe 5.13): **Serielle Ports zur Laufzeit wählen** – die Ports
  werden **gescannt** (Hotplug: `ports.json` des Supervisors, sonst
  `SerialPort.GetPortNames()`), der Benutzer wählt sie in der Oberfläche aus,
  und die Umstellung ist **sofort** wirksam – **kein Neustart** nötig.
  Neuer Endpunkt-Satz: `GET /api/config/serial-ports`,
  `POST /api/config/bacnet/test`, `PUT /api/config/bacnet`
- Phase 5 (Aufgabe 5.13): `ComfoBox.Gateway.Bacnet.SerialPortSettings` – die
  **laufenden** Parameter der Schnittstelle; die Port-Fabrik liest sie bei
  jedem Öffnen, deshalb genügt ein Neuaufbau der Verbindung
- Phase 5 (Aufgabe 5.13): `ComfoBox.Gateway.Bacnet.BacnetSerialManager` –
  prüft einen Port und stellt die Schnittstelle um (Konfiguration schreiben →
  Laufzeit übernehmen → BACnet-Stack neu aufbauen)
- Phase 5 (Aufgabe 5.13): `ComfoBox.Gateway.Hardware.SerialPortScanner` – die
  Port-Suche; der Setup-Assistent benutzt jetzt dieselbe Quelle
- Phase 5 (Aufgabe 5.13): `app-serial-port-picker` in der Weboberfläche –
  eingesetzt auf der Konfigurationsseite und **prominent auf dem Dashboard**,
  sobald BACnet nicht verbunden ist
- Phase 5 (Aufgabe 5.13): 7 neue Tests (Port-Umstellung ohne Neustart,
  Status, Validierung, Sofort-Reconnect) → **293 Tests grün**
- Phase 6 (Aufgabe 6.1): `src/ComfoBox.Gateway/Dockerfile` – **ein** Image für
  Gateway **und** Weboberfläche (ADR-007), 3 Stages (node → sdk → aspnet),
  `tini` als PID 1, `USER comfobox`, ICU für die deutsche Formatierung,
  Healthcheck (nur Erreichbarkeit) und `VOLUME /var/lib/comfobox`
- Phase 6 (Aufgabe 6.1): `.dockerignore` – der Build-Kontext ist das
  Repository-Wurzelverzeichnis (das Image braucht .NET **und** Angular)
- Phase 6 (Aufgabe 6.2): `deploy/compose/compose.yaml` – der manuelle Betrieb,
  parametrisiert über `.env` (Vorlage `.env.example`), mit `devices:`,
  `group_add`, `security_opt` und **ohne** Docker-Socket
- Phase 6 (Aufgabe 6.2): `deploy/compose/compose.test.yaml` – Override
  **ohne** Serial-Port (Entwicklerrechner ohne `/dev/ttyUSB0`)
- Phase 6 (Aufgabe 6.3): CI-Workflow `docker-publish.yml` auf **ein** Image
  und **Multi-Arch** (`linux/amd64,linux/arm64`) umgestellt; Trivy-Scan vor
  dem Push
- Phase 6 (Aufgabe 6.3): CI-Job „container" in `build-test.yml` – baut das
  Image und prüft **echt**: kein Root, tini als PID 1, die Angular-UI im
  Image, Health-Endpunkt, SPA-Auslieferung und das saubere Herunterfahren
- Phase 6 (Aufgabe 6.5): `deploy/backup/backup.sh`, `restore.sh` und
  `backup-cron.sh` – die Sicherung enthält das Volume `comfobox-data` mit dem
  **24-h-Schreibzähler**; die Wiederherstellung ist getestet
- Phase 6 (Aufgabe 6.5): Benutzerdoku `docs/benutzer/08-monitoring.md` –
  Health überwachen, HA-Alarmierung für `degraded`/`offline`, Backup und
  Wiederherstellung

- Phase 8 (Aufgabe 8.1): **Paket-Icon** `deploy/synology-spk/src/PACKAGE_ICON.png`
  (256×256) – reproduzierbar erzeugt mit
  `node deploy/synology-spk/tools/make-icon.mjs` (nur Node.js, kein Python)
- Phase 8 (Aufgabe 8.7): `GET /api/setup/state` liefert zusätzlich die aktuell
  gespeicherten Werte (`current`) – damit ist der Assistent **wiederholbar**,
  ohne dass der Benutzer alles neu eingeben muss
- Phase 8 (Aufgabe 8.7): das Passwort des Administrators wird **serverseitig**
  geprüft (`PasswordPolicy`: mindestens 12 Zeichen, 3 von 4 Zeichenklassen);
  ein Verstoß ergibt **400**, nicht einen stillen Durchlass
- Phase 8 (Aufgabe 8.7): der MQTT-Test unterscheidet jetzt drei Fälle –
  `mqtt_unreachable`, `mqtt_auth_failed` (der Broker lehnt die Anmeldung ab)
  und `mqtt_publish_failed` (der Broker stellt keine Nachrichten zu); er prüft
  zusätzlich mit einem Publish/Subscribe-Rundlauf
- Phase 8 (Aufgabe 8.7): die Konfiguration enthält einen neuen Abschnitt
  `Supervisor` mit `PassAllSerialDevices` – der Supervisor des NAS rendert
  daraus den `devices`-Block der Compose-Datei
- Phase 8 (Aufgabe 8.8): der Assistent lädt die gespeicherten Werte vor
  (Port, Baudrate, Broker, Benutzername), aktualisiert die Portliste alle 5 s
  (Hotplug), lässt den **Broker optional** und beobachtet den Neustart
  (`GET /api/health`), bis der Gateway wieder antwortet
- Phase 8 (Aufgabe 8.8): die Portliste zeigt den zuletzt verwendeten Port
  an, auch wenn das Betriebssystem ihn nicht mehr kennt
- Phase 8 (Aufgabe 8.9): CI-Job `spk` in `docker-publish.yml` – baut das
  Synology-Paket, erzeugt die `packages.json` (Paketquelle für das
  DSM-Paketcenter) und legt beides im GitHub Release ab
- Phase 8: `deploy/synology-spk/tools/test-config-parse.sh` – Test für den
  JSON-Zugriff des Supervisors auf die `config.json`
- Phase 8a (Aufgabe 8a.6): **35 Tests** der HA-Integration (`pytest`) –
  API-Client (Mock), Coordinator (Normalfall, Timeout, 401) und das
  Entity-Mapping (Item → Plattform, Min/Max, Enum-Namen, Unique-Ids, Gerät)
- Phase 8b (Aufgabe 8b.2): `AddOnOptionsProvider` – liest
  `/data/options.json` des HA-Supervisors und mappt die Optionen auf die
  Gateway-Konfiguration (`Bacnet:Port`, `Bacnet:Baudrate`,
  `Gateway:BaseTopic`, `Mqtt:Brokers:0:*`, `Mqtt:EnableDiscovery`,
  `Logging:Level`)
- Phase 8b (Aufgabe 8b.2): neue Optionen `Gateway:AddOnOptionsPath` (Pfad
  der `options.json`), `Mqtt:EnableDiscovery` und `Logging:Level`
- Phase 8c (Aufgabe 8c.1): Workflow `.github/workflows/ha-release.yml` –
  prüft die Versionen (Tag ↔ `manifest.json` ↔ `config.yaml` ↔ Image-Tag),
  validiert mit `hacs/action` und `hassfest` und erzeugt die Releases für
  die Integration (HACS) und das Add-on
- Phase 8d (Aufgabe 8d.1): `GET /api/config` meldet jetzt auch
  `Mqtt:EnableDiscovery`; die Oberfläche zeigt den Schalter in der
  MQTT-Karte
- **BACnet-Bus-Analyse** (nur lesend): `GET /api/diagnostics/bacnet`,
  `POST`/`DELETE /api/diagnostics/bacnet/observe`. Sie zeigt, ob auf dem
  MS/TP-Bus ein Token herumgeht – Zähler, gesehenen Stationen, den
  Ereignisverlauf und eine Bewertung in einem Satz
- `ComfoBox.Bacnet.IMstpObserver` + `MstpEventRecorder` – ein
  **allokationsfreier** Ringpuffer für die MS/TP-Ereignisse. Der
  Zustandsautomat meldet Token-Übernahme, Weitergabe, PFM-Runden und Frames
  **ohne** Log und **ohne** Sperre
- `ComfoBox.Gateway.Bacnet.BacnetBusAnalyzer` – wertet den Ringpuffer
  **außerhalb** des Bus-Takts aus (eigener Thread, 1 s-Takt) und schreibt
  dort erst das Log
- Neue Oberflächen-Seite „BACnet-Bus" in der Diagnose: Beobachtung
  starten/beenden, Zähler und Ereignistabelle
- `BacnetPollingService.SetPaused()` – während einer Beobachtung pausiert
  das Polling, damit der Token ungestört am Ring entlangläuft
- CI-Workflow `.github/workflows/synology-spk.yml` – baut das Synology-Paket
  **eigenständig** (Tag-Push oder `workflow_dispatch`), hängt es als
  Release-Asset an und kopiert es optional per SSH auf die NAS

### Changed
- **Der Legacy-Code wird von der CI nicht mehr angefasst:** der Job
  `legacy-check` ist entfernt, CodeQL ignoriert `src_old/` (`paths-ignore`).
  Grund: `src_old/` ist Archiv (net452, C# 6) – der Code lief seit dem
  24.10. nicht mehr und ist nur noch Referenz
- CI: das Synology-Paket wird nicht mehr im `docker-publish.yml` gebaut, sondern
  im eigenen Workflow `synology-spk.yml` – die Release-Notizen bleiben dadurch
  an einer Stelle
- **Kein Logging mehr im Hot-Path des MS/TP-Automaten.** Der Token hat nur
  95 ms Zeit bis zur Übernahme; ein Log-Eintrag an dieser Stelle hätte den
  Token verloren und den Bus blockiert. Die Zustände werden nun gesammelt
  und außerhalb ausgewertet
- Die Artefakte liegen **außerhalb** des 500-MB-Limits des GitHub-Free-
  Kontos: Release-Assets (2 GB pro Datei) und GHCR als Registry-Speicher
- CI: `dotnet` läuft jetzt in `src/` – dadurch greift `global.json` (SDK ist gepinnt)
- Phase 8b (Aufgabe 8b.2): die `options.json` des HA-Add-ons hat jetzt
  **Vorrang vor den Umgebungsvariablen** (und vor der `config.json`).
  Grund: das Image setzt `COMFOBOX_Bacnet__Port=/dev/ttyUSB0` als Vorgabe –
  wäre die Umgebungsvariable stärker, wäre die Portwahl des Add-ons
  wirkungslos
- Phase 8d (Aufgabe 8d.1): **ein ungültiges `Authorization`-Header wird
  immer mit `401` beantwortet** – auch auf den offenen Endpunkten. Ohne
  Header bleibt der LAN-Modus unverändert. Grund: die HA-Integration prüft
  ihren Token über `GET /api/status`; ein falscher Token darf dort nicht als
  gültig durchgehen (→ ADR-012)
- Phase 8d (Aufgabe 8d.1): das Image des HA-Add-ons trägt jetzt den
  **Versions-Tag** (`ghcr.io/rf77/comfobox-gateway:1.0.0`) – ohne ihn findet
  der Supervisor das Image nicht
- Phase 8c (Aufgabe 8c.1): das Image wird zusätzlich mit dem Versions-Tag
  **ohne** „v" publiziert – das ist der Tag, den der HA-Supervisor zieht
- Phase 8: **die `config.json` des Synology-Pakets folgt dem Schema der
  .NET-Konfiguration** (`Bacnet`, `Mqtt`, `Api`, `Gateway`, `Supervisor`).
  Die Spec zeigte vorher die Kleinbuchstaben-Abschnitte – damit hätte der
  Supervisor nichts gefunden (siehe Protokoll 2026-11-01)
- Phase 8: `postinst` schreibt nur noch `Api:SetupCompleted` in die
  Startkonfiguration. Die alte Datei enthielt einen leeren `Port` und eine
  leere Broker-Liste → `ValidateOnStart` schlug fehl → der Container startete
  nicht → der Assistent war nicht erreichbar
- Phase 8: das Image des Pakets steht in `target/image` (schreibt
  `build-spk.sh`) – das SPK kann nie ein falsches Image ziehen
- Phase 8: `build-spk.sh` bricht **ohne** Icon ab (das Paketcenter braucht
  `PACKAGE_ICON.PNG`)
- CI: der Job „Legacy-Projekte (net452) kompilieren weiterhin" wurde durch eine
  Archiv-Prüfung ersetzt – `src_old/` wird **nicht** gebaut (Projektregel §5)
- Phase 1: der BACnet-Codec enthält kein BACnet/IP (`BVLC`) und kein PTP mehr –
  der Stack läuft ausschließlich über MS/TP
- Phase 1: der neue BACnet-Code nutzt durchgängig `ILogger` statt `Trace`/`Console`
- Phase 3: **M2Mqtt ist vollständig entfernt** – der MQTT-Client ist MQTTnet 5
  (`5.2.0.1603`), async, mit TLS und Authentifizierung (ADR-002)
- Phase 3: QoS wurde von „Subscribe 2 / Publish 0" auf „Subscribe 1 /
  Publish 0|1" geändert – kompatibel, weil QoS vom Empfänger bestimmt wird
- Phase 3: die Client-ID ist jetzt `comfobox-gateway-{Hostname}` (stabil)
  statt `ComfoBox{DateTime.Ticks}` (jede Sitzung war eine neue)
- Phase 3: `Mqtt:ClientId`, `Mqtt:DiscoveryPrefix`, `Mqtt:UseMqttRetain` und
  `Mqtt:StatusIntervalSeconds` sind konfigurierbar
- Phase 5 (Aufgabe 5.13): **`Bacnet:Port` und `Bacnet:Baudrate` sind jetzt ohne
  Neustart änderbar** – als einzige Einstellung. Vorher galten sie als
  „nur Konfigurationsdatei"
- Phase 5 (Aufgabe 5.13): `GET /api/status` und `GET /api/config` melden den
  **laufenden** Port (nicht mehr den aus der Konfigurationsdatei)
- Phase 7 (Aufgabe 7.0): **Simulationsbetrieb** – `Bacnet:Simulation=true`
  ersetzt den BACnet-Client durch einen, der ohne Gerät antwortet
  (deterministische Werte aus dem Item-Katalog, optional über
  `Bacnet:SimulationValuesPath`). `/api/status` meldet `"simulated": true`
  (ADR-013)
- Phase 7 (Aufgabe 7.0): **`tools/compare-payloads/`** – der
  Payload-Vergleich aus Aufgabe 7.2: live (`--old mqtt://…`) oder gegen eine
  Aufzeichnung (`--old-file`), mit `record` zum Aufzeichnen. Zahlen mit
  Toleranz 0.01, Enums **exakt**, Exit-Code ≠ 0 bei Abweichungen
- Phase 7 (Aufgabe 7.0): `Mqtt:DiscoveryIdentifier` – die Kennung der
  Home-Assistant-Discovery ist konfigurierbar (Standard unverändert
  `comfobox`). Für den Parallelbetrieb müssen zwei Gateways verschiedene
  Kennungen haben
- Phase 7 (Aufgabe 7.0): die **wirksame MQTT-Client-ID** enthält jetzt die
  Geräte-ID (`{ClientId}-{DeviceId}`)

### Fixed
- **Das Synology-Paket liess sich nicht installieren.** Das DSM verweigerte den
  Upload mit *„kann nicht installiert werden, da es mit Root-Rechten
  ausgefuehrt wird"*. Ursache: Die Datei `conf/privilege` lag im Repository,
  wurde von `build-spk.sh` aber **nie in das SPK gepackt** (im `tar`-Aufruf
  fehlte `conf`). Ohne diese Datei gilt das Paket ab DSM 7.0 als Root-Paket.
  - `build-spk.sh` kopiert `conf/privilege` jetzt ins Paket und **bricht ab**,
    wenn die Datei fehlt
  - `conf/privilege` entspricht jetzt exakt der DSM-Spezifikation
    (`defaults.run-as: package`, `ctrl-script` fuer die Lifecycle-Skripte)
  - **Neu:** `tools/check-spk.sh` prueft das fertige Paket gegen die
    Anforderungen des DSM-Paketcenters (`conf/privilege` vorhanden **und**
    an der richtigen Stelle, Icon-Groessen, INFO-Pflichtfelder,
    `os_min_ver >= 7.0-40000`, Image-Tag) – der Check laeuft auch in der CI
  - Die INFO enthaelt jetzt `support_conf_folder="yes"` (das DSM weiss damit,
    dass es das `conf/`-Verzeichnis lesen soll) und `conf/privilege` bekommt
    den Modus 0644 – beides wie bei funktionierenden Referenzpaketen
- **Neu:** `tools/dsm-diagnose.sh` – laeuft auf der NAS (per SSH) und zeigt
  DSM-Version, Paketinhalt, `privilege`-Datei und den **echten Fehlercode**
  von `synopkg install`. Das DSM nennt im Dialog nur die Ursache, nicht den
  Grund – dieses Skript liefert die Fehlermeldung mit Code
- **Das Paket-Icon war 256×256 statt 64×64.** DSM 7.0 verlangt fuer
  `PACKAGE_ICON.PNG` genau 64×64 (Breaking Change, vorher 72×72). Die grosse
  Variante liegt jetzt als `PACKAGE_ICON_256.PNG` daneben
- **Die Sichtbarkeits-Prüfung des Container-Images war falsch und hat in eine
  endlose Fehlersuche geführt.** Der Manifest-Abruf
  `https://ghcr.io/v2/rf77/comfobox-gateway/manifests/1.0.0` liefert **immer**
  `401` – auch bei öffentlichen Images, weil die Registry zwingend einen
  Bearer-Token verlangt und ein Browser den nicht selbst anfordern kann. Die
  Anleitung deutete das `401` aber als „privat". Neu: Prüfung über die
  GitHub-Paket-Seite, über `docker pull` oder über einen anonymen Pull-Token
- **CI: der Image-Workflow prüft jetzt den Anonymous-Pull.** Nach dem Push
  fragt er das Manifest mit einem **anonymen** Token ab – genau das, was die
  Synology und der HA-Supervisor tun. Bisher meldete er nur die
  API-Sichtbarkeit, die nichts über den Anonymous-Pull aussagt
- **Die GitHub Action fuer das Synology-Paket war nicht benutzbar.** Fuenf
  Ursachen, alle gefunden ueber die Annotationen der Release-Laeufe:
  - Der **Default-Branch heisst `master`** (Legacy-Stand ohne Workflows).
    Ein `workflow_dispatch` ist nur moeglich, wenn der Workflow im
    Default-Branch liegt - der SPK-Workflow war deshalb weder sichtbar noch
    manuell startbar. Ueber den **Tag-Push** laeuft er auch ohne
    Branch-Wechsel
  - Die Trigger zeigten auf **`main`** - den Branch gibt es nicht.
    `main` → `master` + `Upgrade` (build-test, codeql, dependency-review)
  - **`aquasecurity/trivy-action@0.28.0` gibt es nicht** (der Tag lautet
    `v0.28.0`) - der Image-Job brach schon beim Action-Download ab, es kam
    kein Image und kein Release
  - **`packages.json` zeigte auf `releases/latest`** - das ist das zuletzt
    *veroeffentlichte* Release und nach dem Tag-Push das HA-Release `1.0.0`
    (ohne `v`), nicht `v1.0.0`. Der Download liefe ins Leere. Die URL zeigt
    jetzt auf den festen Release-Tag
  - **HA-Integration und Add-on publizierten beide auf Tag `1.0.0`** - der
    zweite Aufruf ueberschrieb das erste Release. Es gibt jetzt **ein**
    Release mit beiden Assets
- **Drei Workflows liefen beim Tag-Push parallel und konkurrierten ums
  Release.** Der SPK-Workflow legt es nicht mehr blind an, sondern **wartet**
  auf das Release des Image-Workflows. Das loest zugleich das
  Reihenfolge-Problem: das Release entsteht **nach** dem Image-Push - das
  Image liegt also sicher in GHCR, bevor das SPK-Paket existiert
- **Die HA-Integration hatte keine `services.yaml`** (HACS: „Registers
  services but has no services.yaml") und ein **unsortiertes Manifest** mit
  der ungueltigen Kategorie `sensor`. Beides behoben
- **Die CI lief seit Phase 0 nie – der Lauf war „rot", ohne dass ein Job
  gestartet wurde.** GitHub hatte `build-test.yml` und `codeql.yml` schon bei
  der **YAML-Validierung** abgelehnt (0 Jobs, 0 Sekunden):
  - `build-test.yml`: `if: hashFiles(...)` auf **Job-Ebene** – `hashFiles` ist
    in einer Job-Bedingung nicht erlaubt („Unrecognized function")
  - `codeql.yml`: `paths-ignore` auf **Top-Level** („Unexpected value") und
    `schedule:` fälschlich unter `pull_request:` verschachtelt
  - Ein Lauf mit **0 Jobs** ist **nie** ein Code-Problem – immer zuerst die
    Annotation des Laufs lesen
- **Die CI lief nur auf `main`** – auf dem Entwicklungsbranch `Upgrade` lief
  gar nichts. `Upgrade` steht jetzt in der Branch-Liste, CodeQL zusätzlich
  mit `workflow_dispatch`
- **Der Generator war plattformabhängig:** ein verbatim-Stringliteral über
  mehrere Zeilen enthält die *physischen* Zeilenenden der Quelldatei (CRLF
  unter Windows, LF unter Linux). Der Fixture-Check schlug deshalb unter
  Linux fehl (19 Abweichungen). Die Zeilenenden werden jetzt **immer** auf
  CRLF normalisiert – die Fixture bleibt byteweise unverändert
- **Ein nicht vorhandener Serial-Port wurde unter Linux als
  „permission_denied" gemeldet.** .NET wirft unter Unix eine
  `UnauthorizedAccessException`, wenn die **Gerätedatei fehlt** – nicht nur,
  wenn sie dem Benutzer nicht gehört. Die Zuordnung prüft jetzt, ob es die
  Datei überhaupt gibt
- **Der Coverage-Collector brachte eine `netstandard`-Referenz in die
  Domäne.** coverlet schreibt die instrumentierten Assemblies mit einem
  Verweis auf die .NET-Standard-Fassade um – ein Werkzeug-Artefakt, keine
  Abhängigkeit der Domäne
- **Zwei Tests waren Wettläufe** und liefen auf dem CI-Läufer in den Timeout
  (`ValueCacheTests` nach 10 s, `BacnetClientTests.IAm_Loest_Das_Ereignis_Aus`).
  Beide warten jetzt **ereignisbasiert** statt auf eine feste Zeit
- **Der Container-Check prüfte den Docker-Health-Status zu früh:** der
  Endpunkt antwortet nach ~2 s, der Status steht erst nach dem periodischen
  Healthcheck (Intervall 30 s) auf „healthy". Der Schritt wartet jetzt aktiv
- **Der Test-Schritt meldete nur „Exit code 1".** Er nennt jetzt den
  fehlgeschlagenen Test als Annotation, in der Job-Zusammenfassung und im
  hochgeladenen Log
- **Die Reiter der Diagnose-Seite ließen sich nicht umschalten.** `p-tabs`
  hatte ein statisches `value="checks"` – der Klick auf einen anderen Reiter
  änderte nichts. Die Auswahl hängt jetzt an einem Signal (`[(value)]`)
- **Ein per `@if` ersetzter Zweig blieb als sichtbarer „Waisen"-Zweig im DOM.**
  `p-tabpanel` projiziert seinen Inhalt per `ng-template` + `*ngTemplateOutlet`;
  ein ersetzter `@if`/`@else`-Zweig wird **nicht** aus dem DOM entfernt. Der
  Ladehinweis „Die Bus-Analyse wird geladen …" blieb deshalb dauerhaft
  sichtbar, obwohl die Analyse längst fertig war. Der Hinweis wird jetzt
  **per CSS-Klasse** geschaltet (`display: none`), nicht per Kontrollfluss
- **Der CI-Lauf war rot, ohne dass der Code defekt war:** die
  `package-lock.json` des Angular-Projekts enthielt im Root-Eintrag **keine
  Abhängigkeiten** – `npm ci` bricht in diesem Fall mit `EUSAGE` ab und der
  Job „Build (Angular)" wurde rot. Die Lock-Datei ist konsistent; der
  Workflow meldet eine Drift jetzt mit einem klaren Hinweis
- Phase 7 (Aufgabe 7.0): **zwei Gateways am selben Broker haben sich gegenseitig
  verdrängt** – beide meldeten sich mit `comfobox-gateway-{Hostname}` an, der
  Broker trennte die ältere Sitzung (*Session taken over*) und deren Last-Will
  meldete das andere Gateway als `offline`. Die wirksame Client-ID enthält jetzt
  die Geräte-ID
- Phase 7 (Aufgabe 7.0): die Kennung der Home-Assistant-Discovery war eine
  Konstante (`comfobox`) → zwei Instanzen publizierten auf denselben
  Discovery-Topics und hätten sich die Entitäten gegenseitig überschrieben.
  Die Kennung ist jetzt konfigurierbar (`Mqtt:DiscoveryIdentifier`)
- Phase 8a (Aufgabe 8a.6): die Plattformen `sensor`, `number`, `select` und
  `switch` brachen mit `AttributeError: 'GatewayStatus' object has no
  attribute 'is_read_only'` ab – sie iterierten über **alle** Einträge des
  Coordinator-Caches, auch über den reservierten Status-Eintrag
  `__status__`. Sie überspringen ihn jetzt (und benutzen die Entitätsklasse
  der Plattform)
- Phase 8a (Aufgabe 8a.6): die Entitäten der Plattformen waren **keine**
  `SensorEntity`/`NumberEntity`/`SelectEntity`/`SwitchEntity` – sie hatten
  damit weder einen Zustand noch die Plattform-Eigenschaften
- Phase 8a (Aufgabe 8a.6): der Service `comfobox.set_value` ermittelte die
  Item-Id durch Abschneiden des Entity-Präfixes. Die Item-Ids des Gateways
  enthalten aber einen **Punkt** (`zone.set_point_normal`), die `entity_id`
  einen Unterstrich → jeder Schreibvorgang über den Service scheiterte mit
  `404 item_not_found`. Der Service liest jetzt die Item-Id aus dem
  Entity-Registry (über die `unique_id`)
- Phase 8d (Aufgabe 8d.1): die Icon-Buttons der Oberfläche (Token widerrufen,
  Navigation, Abmelden, neu laden, Eintrag kopieren) hatten **keinen
  zugänglichen Namen**: `aria-label` als Attribut auf `<p-button>` erreicht
  den inneren `<button>` nicht. Optimus erwartet die Eigenschaft `ariaLabel`
- Phase 8: der Assistent zeigte nach einem Klick auf die Schrittliste oder den
  Browser-Zurück den **falschen Schritt** (Fortschritt und „Weiter" liefen auseinander);
  jetzt wird der Zustand bei jeder Navigation mit der URL synchronisiert
- Phase 8: ein **direkter Aufruf** von `/setup/serial` (z. B. als Lesezeichen)
  startete immer bei Schritt 1 – die erste Navigation ist vor `ngOnInit`
  abgeschlossen und kommt als Ereignis nie an
- Phase 8: die Meldung „Kein serieller Port gefunden" blieb stehen, obwohl
  Ports gefunden wurden – Optimus UI hält eine per `@if` entfernte `p-message`
  per Leave-Animation im DOM (in einem Hintergrund-Tab feuert `animationend` nie)
- Phase 8: `POST /api/setup/apply` stieß den Neustart an, ohne ihn
  abzuwarten – der Endpunkt wartet jetzt, bis der Host beendet ist
- Phase 6: **`src/.gitignore` verschluckte den Angular-Quellordner
  `src/ComfoBox.Web/src/app/features/diagnostics/logs/`** – die Regel `logs/`
  (ohne Pfadpräfix) traf auch jeden Ordner namens `logs` im Quellcode. Der
  LogViewer war dadurch **nicht im Repository** und der CI-Build wäre an
  derselben Stelle gebrochen wie der lokale Docker-Build. Die Laufzeit-Regeln
  sind jetzt verankert (`/ComfoBox.Gateway/logs/`). Dasselbe gilt für
  `.dockerignore`
- Phase 6: **`package-lock.json` war nicht synchron zu `package.json`**
  (`@emnapi/*` in zwei Versionen) – `npm ci` schlug deshalb fehl. Das
  Lock-File wurde plattformrein (unter Linux) neu erzeugt; danach
  funktionieren **beide** Plattformen
- Phase 6: **das Alpine-Basis-Image setzt
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`** – ohne `icu-libs` und ohne
  explizites `false` bricht die deutsche Formatierung in der Oberfläche
- Phase 5 (Aufgabe 5.13): ein angeforderter Neuaufbau der seriellen
  Schnittstelle wartet nicht mehr den Backoff (bis 30 s) ab – der Port wird
  **sofort** neu geöffnet
- Phase 4: **Dark-Mode der Weboberfläche** – der Seitenhintergrund blieb weiß,
  weil `--p-surface-50` ein Palette-Token ist und im Dark-Mode nicht
  umgeschaltet wird. Er kommt jetzt über `--cb-page-background` aus
  `light-dark()` und ist im Dark-Mode einen Tick heller als die Karten
- Phase 4: **Dark-Mode der Weboberfläche** – die Hilfeseiten, die Auswahlfelder
  und der Log-Viewer benutzten hellen Palette-Tokens (weißer Text auf hellem
  Grund). Sie nutzen jetzt semantische Tokens (`--p-content-hover-background`,
  `--p-content-border-color`, `--p-form-field-background`)
- Phase 3: **Befund A10** – nach **jedem** Reconnect wird `ComfoBox/Connected`
  mit `1` publiziert. Der Legacy tat das nur beim ersten Connect, danach sah
  Home Assistant dauerhaft „offline"
- Phase 3: **Befund A5** – alle Zahlen-Payloads sind kultur-invariant
  (`21.5`, niemals `21,5`). Auf deutschen Systemen wurden Schreibvorgänge
  dadurch verworfen oder als `235` interpretiert
- Phase 3: **Befund A6** – eine ungültige Enum-Payload wird abgelehnt und
  gemeldet. Der Legacy schrieb in diesem Fall den Wert `0`
- Phase 3: **Befund A9** – die Reconnect-Logik ist eine ereignisbasierte
  Schleife mit Backoff und `CancellationToken` statt der Rekursion des Legacy
- Phase 3: **Befund A11** – beim Shutdown wird erst `offline` publiziert, dann
  getrennt; der Last Will wird nicht fälschlich ausgelöst
- Phase 3: **Befund A24** – das Keep-Alive ist 30 s statt 2 s (der Legacy hat
  damit den Broker laufend getötet)
- Phase 3: **Befund A25** – TLS, Benutzername und Passwort sind pro Broker
  konfigurierbar; ein defekter Broker blockiert die anderen nicht
- Phase 1: **Befund A8** – ein Serial-Portfehler löste im Legacy einen
  Tight-Spin-Loop mit 100 % CPU aus. Der neue Zustandsautomat endet bei einem
  Portfehler, der Transport verbindet sich mit Backoff neu (5 s → 30 s)
- Phase 1: **Befund A19** – die Sende-Warteschlange des Legacy war eine
  ungeschützte `LinkedList`, auf die Aufrufer und Zustandsautomat gleichzeitig
  zugriffen. Jetzt `ConcurrentQueue`
- Phase 1: **Befund A23** – der MS/TP-Thread läuft mit `ThreadPriority.Highest`.
  Der neue Transport nutzt `TaskCreationOptions.LongRunning` (Standardpriorität)
- Phase 1: die Invoke-ID Vergabe im Client war nicht threadsicher (`m_invoke_id++`)
  → jetzt `Interlocked`
- Phase 2: **Befund A15** – die Prioritäts-Attribute (`[Once]`, `[Primary]`,
  `[Secondary]`) wurden im Legacy gelesen und **verworfen**; alle Items bekamen
  `RefreshPriority.None`. Der neue Katalog wertet sie aus, der Poll-Plan liest
  primäre Items in jedem und sekundäre Items nur alle 5 Zyklen
- Phase 2: **Befund A22** – der Legacy zählte `CurrentNumberOfWrites++` auch bei
  **fehlgeschlagenen** Schreibvorgängen. Der neue Zähler reserviert vor dem
  Schreiben und gibt die Reservierung bei einem Fehler wieder frei
- Phase 2: die Item-Id folgt der Regel camelCase → snake_case
  (`zone.set_point_normal`) – die Beispiele in der Spec wurden angepasst

## [0.1.0] – 2026-10-31

### Added
- Vollständige Spezifikation (`docs/02-modernisierung/`)
- 11 Entscheidungsprotokolle (ADR-001 … ADR-011)
- Aufgaben-Dokument `docs/06-aufgaben/` mit Phasen 0–8d
- Benutzerdokumentation (`docs/benutzer/`) mit Build-Skript
- Home-Assistant-Integration (HACS) und HA-Add-on-Manifest
- Synology-SPK-Paket mit Lifecycle-Skripten und host-seitigem Supervisor

### Changed
- Legacy-Bestand von `src/` nach `src_old/` verschoben (Archiv, wird nicht gebaut)

[Unreleased]: https://github.com/example/comfobox-mqtt/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/example/comfobox-mqtt/releases/tag/v0.1.0
