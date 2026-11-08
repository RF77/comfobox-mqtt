# Protokoll

> Chronologische Liste der Sessions. **Nach jeder Session** einen Eintrag ergänzen.
>
> Format: was wurde gemacht, was ist offen, was war überraschend.

## Eintrag Vorlage

````markdown
## 2026-MM-TT – <Titel> (Phase X, Aufgabe X.Y)

**Ergebnis:** <was jetzt funktioniert – konkret, nicht "implemented">

**Geänderte Dateien:**
- `pfad/datei.cs` – was

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → grün
- `dotnet test` → 42 Tests grün

**Offen / nicht gemacht:**
- <was bewusst ausgelassen wurde und warum>

**Überraschungen:**
- <was nicht wie erwartet war – wichtig für die nächste Session>
````

---

## 2026-11-08 – Version 1.0.2: der Worker-Emulations-Test war der Durchbruch

**Ergebnis:** Die Worker-Emulation auf der NAS hat die Ursache gefunden und
bestätigt, dass das Paket jetzt lauffähig ist. Revision 0010 mit Image 1.0.2
ist gebaut und veröffentlicht.

**Der entscheidende Output** (Modus `--worker` auf der DS923+):

````
>> docker compose up (der Schritt, an dem die Installation scheitert):
   Volume "comfobox-data"  Created
   Container comfobox-gateway  Started
   Container comfobox-gateway  Waiting
   Container comfobox-gateway  Healthy
   (Exit-Code: 0)

>> Zustand (Restart-Zaehler, Exit-Code, Health):
   RestartCount: 0
   State:          running
   ExitCode:       0
   Health:         healthy

>> Rechte des Datenverzeichnisses im Volume:
   drwxr-xr-x    1 1001     1001             8 /data
   drwxr-xr-x    1 1001     1001            44 /data/logs
````

**Damit ist bestätigt:** Mit dem benannten Volume funktioniert alles –
Container startet, wird **healthy**, keine Neustarts, Rechte korrekt
(uid 1001). Das benannte Volume war genau die fehlende Lösung.

**Zwei Meldungen im Container-Log sind normal** und **keine** Fehler:

| Meldung | Bedeutung |
|---|---|
| `MQTT-Verbindung zu "localhost:1883" fehlgeschlagen` | Es ist noch kein Broker eingetragen (Standardwert). Der Assistent fragt ihn ab. |
| `Access to the port '/dev/ttyUSB0' is denied … No such file or directory` | Der USB-Adapter ist nicht angesteckt oder heisst anders. |

Beides steht in der Benutzerdoku.

**Weitere Arbeiten in dieser Session:**

- **Schreibzähler-Schutz.** Der Worker gibt das Volume beim Timing
  `WHEN_PREUNINST` frei – also **vor** `postuninst`. Der 24-h-Schreibzähler
  (EEPROM-Schutz) wäre damit verloren. `preuninst` sichert ihn jetzt nach
  `var/`, `postinst` spielt ihn zurück. Beides „best effort“ – die
  Deinstallation darf nie daran scheitern.
- **Image-Build beschleunigt.** Der Multi-Arch-Build läuft unter QEMU;
  darin ist der Angular-Build um ein Vielfaches langsamer. `workflow_dispatch`
  hat jetzt das Input `platforms` (Default `linux/amd64`). Ein Tag-Push
  baut weiterhin multi-arch (das HA-Add-on auf dem Pi braucht arm64).
- **Release v1.0.2** angelegt; die `packages.json` in **beiden** Releases
  (v1.0.1 und v1.0.2) zeigt auf 0010 – die bestehende Paketquelle des
  Nutzers funktioniert damit unverändert.

**Geänderte Dateien:**
- `deploy/synology-spk/src/scripts/preuninst` – sichert den Schreibzähler
- `deploy/synology-spk/src/scripts/postinst` – spielt ihn zurück
- `deploy/synology-spk/src/VERSION` → 1.0.2, `REVISION` → 0010
- `deploy/synology-spk/packages.json` → 1.0.2-0010
- `.github/workflows/docker-publish.yml` – Input `platforms`
- `deploy/synology-spk/tools/{cancel-build,check-runs,check-image,check-jobs,fetch-job-log,create-release}.ps1` – **neu**
- `docs/benutzer/08-synology.md`, `08b-synology-test.md`, `CHANGELOG.md`

**Verifikation:**
- `bash build-spk.sh 1.0.2 0010` → SPK gebaut
- `bash tools/check-spk.sh` → **OK**
- `bash tools/check-shell.sh` → alle Skripte korrekt
- Image 1.0.2: Manifest geprüft (`linux/amd64`), anonymer Pull → **HTTP 200**
- Release v1.0.1 **und** v1.0.2: Download + MD5 verifiziert
  (`62115eeaba9654f58b252211c4937683`)

**Offen / nicht gemacht:**
- Die Installation über das Paketzentrum ist **noch nicht** verifiziert – nur
  die Worker-Emulation. Der nächste Test auf der NAS entscheidet.
- Der arm64-Build ist **nicht** neu gelaufen (der alte Lauf wurde
  abgebrochen). `1.0.1` im Registry ist weiterhin multi-arch; für das
  HA-Add-on auf dem Pi ist das weiterhin gültig.

**Überraschungen:**
- `docker compose up -d` **erfolgreich** heisst nicht, dass der Container
  laeuft – der Exit-Code ist 0, auch wenn der Container sofort stirbt. Erst
  `docker inspect` auf `State`/`Health` zeigt die Wahrheit.
- Die GitHub-API liefert **UTC**. Ohne Zeitzonenangabe interpretiert
  PowerShell das als lokale Zeit – die Laufzeit war dadurch um eine Stunde
  daneben (im Winter eine, im Sommer zwei). Das Skript `check-runs.ps1`
  rechnet jetzt in UTC.
- Ein Docker-Named-Volume **übernimmt die Rechte aus dem Image**. Das ist
  der Grund, warum es funktioniert: `/var/lib/comfobox` gehört im Image
  uid 1001 – und der Container darf dort schreiben, egal wer den Mount
  angelegt hat.

---

## 2026-11-07 (7) – Der Container stuerzte sofort ab: das Bind-Mount gehoerte root:root

**Ergebnis:** Die Ursache des „fail compose up“ ist gefunden. Revision 0009
verwendet das benannte Docker-Volume statt eines Bind-Mounts.

**Der entscheidende Befund:** Die Worker-Emulation auf der NAS

````
>> docker compose up (der Schritt, an dem die Installation scheitert):
   Network comfobox_default  Created
   Container comfobox-gateway  Created
   Container comfobox-gateway  Started
   (Exit-Code: 0)
>> Container:
   ... Up Less than a second (health: starting)   0.0.0.0:8081->8080/tcp
>> Container-Logs:
   (leer)
````

**Die Compose-Datei ist also in Ordnung** – `compose up` laeuft durch. Zwei
Beobachtungen waren aber entscheidend:

1. **`compose up -d` liefert Exit-Code 0, auch wenn der Container sofort
   abstuerzt.** Die Emulation hat das nicht erkannt – der DSM-Worker
   wartet dagegen, bis der Container laeuft. → Die Emulation benutzt jetzt
   `--wait --wait-timeout 60`.
2. **Die Container-Logs waren leer.** Ein startendes .NET-Programm
   schreibt mindestens eine Zeile. Leer heisst: der Prozess ist **vor** dem
   ersten Logeintrag gestorben.

**Ursache:** Das DSM loescht bei jeder Installation `/var/packages/<paket>`
(das steht so im Log: `Begin /bin/rm -rf /var/packages/ComfoBoxGateway`)
und legt es neu an. Legt Docker einen **fehlenden** Bind-Mount-Quellpfad
an, gehoert er danach **root:root** mit Modus 0755. Der Container laeuft
aber als uid 1001 (`comfobox`):

| Pfad im Container | Besitzer | Folge |
|---|---|---|
| `/var/lib/comfobox` (Mount) | root:root 0755 | kein Schreiben moeglich |
| `/var/lib/comfobox/logs` | – | Serilog kann das Verzeichnis nicht anlegen |

**Serilog legt das Log-Verzeichnis beim Start selbst an** – kann er das
nicht, bricht der Start **mit einer Ausnahme** ab, noch **bevor** der erste
Logeintrag geschrieben wird. Daher die leeren Logs. Der Container stirbt,
`restart: unless-stopped` startet ihn erneut, und der DSM-Worker meldet
„fail compose up“.

**Warum die Emulation erfolgreich war:** Zu diesem Zeitpunkt war das
Verzeichnis bereits von `postinst` mit `chmod 0777` versehen – der
Container durfte also schreiben.

**Fix:**

1. **Compose: benanntes Volume statt Bind-Mount.**

   ````yaml
   volumes:
     - comfobox-data:/var/lib/comfobox
   volumes:
     comfobox-data:
       name: comfobox-data
   ````

   Docker uebernimmt Inhalt **und Rechte** aus dem Image
   (`/var/lib/comfobox` gehoert dort uid 1001) – der Container kann dort
   immer schreiben, unabhaengig davon, wer das Verzeichnis anlegt. Der
   feste Name passt zum Backup-Skript (`deploy/backup/backup.sh`), das
   genau `comfobox-data` sucht, und zum manuellen Betrieb.

2. **Gateway: das Log-Verzeichnis wird geprueft.** `ResolveWritableDirectory`
   in `Program.cs` legt das Verzeichnis an und testet die Schreibbarkeit.
   Schlaegt das fehl, wird auf ein beschreibbares Verzeichnis
   ausgewichen und die Abweichung gemeldet. Der Gateway startet damit
   **immer** – ein fehlendes Log-Verzeichnis ist kein Grund fuer einen
   Ausfall.

3. **`postinst` schreibt keine `config.json` mehr.** Das Volume kann der
   Paket-Benutzer nicht beschreiben. Der Gateway braucht die Datei auch
   nicht: ohne Datei liest er `Api:SetupCompleted` als `false` (die
   Setup-Endpunkte sind offen) und legt sie beim Abschluss des Assistenten
   selbst an.

**Geänderte Dateien:**
- `src/ComfoBox.Gateway/Program.cs` – `ResolveWritableDirectory` +
  `TryCreateDirectory`, Log-Verzeichnis mit Fallback
- `deploy/synology-spk/src/target/compose/compose.yaml` – benanntes Volume,
  kein Bind-Mount auf `/var/packages`
- `deploy/synology-spk/src/scripts/postinst` – schreibt keine `config.json`
  mehr; der Port-Scan bleibt als Diagnose auf dem Host
- `deploy/synology-spk/build-spk.sh` – kein `@VAR_DIR@` mehr
- `deploy/synology-spk/tools/check-spk.sh` – prüft: kein Bind-Mount auf
  `/var/packages`, genau ein Daten-Mount, fester Volumenname
- `deploy/synology-spk/tools/dsm-diagnose.sh` – die Emulation wartet jetzt
  (`--wait`), zeigt Restart-Zähler, Exit-Code, Health-Status, die
  Log-Dateien im Volume und deren Rechte
- `docs/benutzer/08-synology.md`, `08b-synology-test.md`,
  `deploy/synology-spk/README.md` – neue Datenorte

**Verifikation:**
- `dotnet build src/ComfoBox.Gateway/ComfoBox.Gateway.csproj -c Release` →
  grün, 0 Warnungen
- `bash build-spk.sh 1.0.1 0009` → SPK gebaut
- `bash tools/check-spk.sh` → **OK**
- `bash tools/check-shell.sh` → alle Skripte syntaktisch korrekt
- SPK: 51 200 B, MD5 `e8493128248671cbbc37bba2fa0b167a`

**Offen / nicht gemacht:**
- Die Installation auf der NAS ist **weiterhin nicht** verifiziert.
- Das Image `ghcr.io/rf77/comfobox-gateway:1.0.1` enthaelt **noch nicht**
  den Code-Fix aus `Program.cs`. Der Volume-Fix wirkt auch ohne ihn
  (der Container kann im Volume schreiben), aber der Code-Fix ist erst ab
  dem naechsten Image-Build aktiv.

**Überraschungen:**
- `docker compose up -d` **erfolgreich** heisst nicht, dass der Container
  laeuft. Es gibt erst `docker compose up --wait` (Compose v2) oder ein
  `docker inspect` auf den Health-Status, um das zu sehen.
- Ein Programm, das beim Start scheitert, hinterlaesst **keine** Logs –
  die Ursache liegt dann immer **vor** dem ersten Logeintrag (hier: der
  Serilog-Sink).
- Der DSM-Worker meldet bei einem Container-Startfehler nur „fail compose
  up“ – ohne Docker-Fehlermeldung. Die Worker-Emulation im
  Diagnose-Skript ist deshalb kein Luxus, sondern das einzige Werkzeug,
  das die Ursache sichtbar macht.

---

## 2026-11-07 (6) – „fail compose up": der Container startete nicht

**Ergebnis:** Der Worker findet die Compose-Datei jetzt und legt das Projekt
an – der Start des Containers scheitert aber noch. Revision 0008 enthält
drei Korrekturen und vor allem ein Werkzeug, das die Ursache sichtbar
macht: den Modus `--worker` im Diagnose-Skript.

**Die entscheidende Meldung** (aus `synopkg install` auf der NAS):

````
{"code":276,"description":"failed to acquire postinst worker",
 "worker_msg":[{"message":"fail compose up '9b068757-9ce2-40db-8049-4b8414a64877'"}]}
````

und aus `/var/log/messages`:

````
synopkg[29202]: docker_project_worker/docker_project_generator_worker/
  docker_project_generater_worker.cpp:254 rollback created projects: `comfobox`
````

**Interpretation:**
1. Der Worker **findet** die Compose-Datei (der Fix aus Revision 0007
   greift) und **legt** das Projekt an.
2. `compose up` **scheitert** → der Worker rollt zurück.
3. Das DSM gibt die Docker-Fehlermeldung **nicht** weiter – nur „fail
   compose up“ mit einer UUID.

**Wichtig:** `docker compose config` war auf der NAS **erfolgreich** – die
Compose-Datei ist also syntaktisch gültig. Es ist ein **Laufzeit**-Fehler
beim Start des Containers.

**Geänderte Dateien:**
- `deploy/synology-spk/src/target/compose/compose.yaml`
  - `version: "3.8"` – bei Compose v1 (< 1.27) ist das Feld **Pflicht**,
    ohne das Feld lehnt der Parser die Datei ab. Bei Compose v2 ist es
    überholt, aber harmlos.
  - **Ein** Mount statt zwei: `- "@VAR_DIR@:/var/lib/comfobox"`. Vorher lagen
    die Host-Pfade `var` und `var/logs` **ineinander**. Der Log-Pfad liegt
    jetzt im Datenverzeichnis (`COMFOBOX_Logging__Directory=/var/lib/comfobox/logs`).
- `deploy/synology-spk/src/scripts/preinst` – legt das Datenverzeichnis an
  (0777). Grund: der **Worker läuft vor dem `postinst`**. Legt Docker das
  Verzeichnis selbst an, gehört es `root:root` – der Container läuft aber
  als uid 1001 und könnte dann weder die `config.json` schreiben noch den
  **24-h-Schreibzähler** sichern.
- `deploy/synology-spk/tools/dsm-diagnose.sh` – **neuer Modus `--worker`**:
  entpackt das Paket wie das DSM, zieht das Image, prüft die belegten
  Ports und führt `docker compose up` aus – mit der **vollen**
  Docker-Fehlermeldung. Installiert **nichts**, räumt am Ende auf.
- `deploy/synology-spk/tools/check-compose.sh` – **neu**: prüft die
  gepatchte Compose-Datei im gebauten SPK (`docker compose config`)
- `deploy/synology-spk/tools/check-spk.sh` – prüft jetzt `version:` und
  genau **einen** Daten-Mount
- `deploy/synology-spk/packages.json`, `REVISION` (0008)
- `docs/benutzer/09-fehlersuche.md` – Symptom 4 („fail compose up“) mit der
  Zuordnung der Docker-Fehlermeldungen

**Verifikation:**
- `bash build-spk.sh 1.0.1 0008` → SPK gebaut
- `bash tools/check-spk.sh` → **OK** (inkl. 2 neuer Prüfungen)
- `bash tools/check-shell.sh` → alle Skripte syntaktisch korrekt
- SPK: 51 200 B, MD5 `4f99cf9f134cd5750aecd831c54c2cee`

**Offen / nicht gemacht:**
- Die Ursache des `compose up`-Fehlers ist **nicht** abschließend
  bekannt – das DSM unterdrückt die Meldung. Der Modus `--worker` liefert
  sie beim nächsten Test auf der NAS.
- Die Installation auf der NAS ist weiterhin **nicht** verifiziert.

**Überraschungen:**
- `docker compose config` **grün** heißt nicht, dass `compose up` klappt.
  Die Syntaxprüfung sieht weder Image-Pull noch Port-Bindung noch
  Container-Start.
- Der DSM-Worker läuft **vor** dem `postinst`. Alles, was der Container
  beim Start braucht, muss also **vorher** da sein – das war beim
  Datenverzeichnis nicht der Fall.
- Der Worker **legt das Projekt an** und rollt es bei Fehler zurück
  („rollback created projects“). Deshalb ist nach dem Fehlversuch kein
  Projekt in der Oberfläche zu sehen – das ist kein weiterer Fehler.

---

## 2026-11-07 (5) – `package.tgz` war eine Ebene zu tief: der Worker „docker-project" scheiterte

**Ergebnis:** Die Ursache der gescheiterten Installation ist gefunden und
behoben. Revision 0007 ist gebaut und geprüft.

**Der entscheidende Logeintrag** (aus `synopkg.log` auf der NAS):

````
23:39:43 Acquire systemd-unit for ComfoBoxGateway when 0x0001 (ready)
23:39:43 Acquire systemd-unit for ComfoBoxGateway when 0x0001 (done)
23:39:43 Acquire docker-project for ComfoBoxGateway when 0x0001 (ready)
23:39:43 Acquire docker-project for ComfoBoxGateway when 0x0001 (fail)   ← hier
23:39:43 Begin postinst
23:39:44 End postinst ret=[0]
23:39:44 install ComfoBoxGateway 1.0.1-0006 failed
````

`postinst` lief durch – der Fehler lag **danach**, im Resource-Worker.

**Ursache:** Das `package.tgz` enthielt die Dateien **eine Ebene zu tief**:

````
target/bin/common.sh
target/bin/serial-scan.sh
target/compose/compose.yaml
target/image
````

Das DSM entpackt `package.tgz` aber **direkt in das Zielverzeichnis**:

````
mv -f /volume3/@tmp/synopkg/install.DUwyw3/package \
     /usr/local/packages/@appstore/ComfoBoxGateway
````

`package` ist der entpackte Inhalt und wird zum Zielverzeichnis **selbst**.
Damit landete die Compose-Datei in `<ziel>/target/compose/compose.yaml` –
der Worker sucht sie aber unter `<ziel>/compose` (`conf/resource`:
`"path": "compose"`, relativ zum Ziel) und findet sie nicht → `(fail)`.

**Fix:** `build-spk.sh` packt jetzt den **Inhalt** von `target/` statt des
Verzeichnisses selbst:

````bash
( cd "$WORK/target" \
  && find . -mindepth 1 -maxdepth 1 | sed 's|^\./||' \
  | tar cpf - $TAR_REPRODUCIBLE --files-from=/dev/stdin \
  | gzip -n > "$WORK/package.tgz" )
````

**Zweiter Fund – Fehler 261:** `synopkg install` meldete
`{"code":261,"description":"package does not have preupgrade / postupgrade script"}`.
Das war ein **Folgefehler**: Die fehlgeschlagene Installation hatte das Paket
im Status *„broken“* (150) hinterlassen. DSM versuchte deshalb ein
**Update** statt einer Neuinstallation – und dafür braucht es `preupgrade`
und `postupgrade`. Beide Skripte sind jetzt im Paket.

**Geänderte Dateien:**
- `deploy/synology-spk/build-spk.sh` – `package.tgz` ohne `target/`-Ebene
- `deploy/synology-spk/src/scripts/preupgrade` – **neu**
- `deploy/synology-spk/src/scripts/postupgrade` – **neu**
- `deploy/synology-spk/tools/check-tgz-layout.sh` – **neu**: prüft die
  Struktur von `package.tgz` im gebauten SPK
- `deploy/synology-spk/tools/check-spk.sh` – Pfade auf die neue Struktur
  umgestellt; **neue Regressionstests**: keine `target/`-Ebene,
  `compose/compose.yaml` auf der obersten Ebene, `bin/common.sh` dort,
  `preupgrade`/`postupgrade` vorhanden
- `deploy/synology-spk/tools/check-shell.sh` – neue Skripte und
  `check-tgz-layout.sh` in die Syntaxprüfung
- `deploy/synology-spk/tools/dsm-diagnose.sh` – zeigt jetzt die Struktur von
  `package.tgz`, prüft die Compose-Datei mit `docker compose config` und
  entfernt ein Paket im Status „broken“ vor dem Installationsversuch
- `deploy/synology-spk/packages.json`, `REVISION` (0007)
- `docs/benutzer/09-fehlersuche.md` – die drei verschiedenen
  Installationsfehler getrennt beschrieben, „broken“-Status erklärt
- `docs/benutzer/08b-synology-test.md`, `CHANGELOG.md`

**Verifikation:**
- `bash build-spk.sh 1.0.1 0007` → SPK gebaut
- `bash tools/check-tgz-layout.sh` → `package.tgz` ohne `target/`-Ebene ✅
- `bash tools/check-spk.sh` → **OK**
- `bash tools/check-shell.sh` → alle Skripte syntaktisch korrekt
- SPK: 51 200 B, MD5 `360d76387e0b0b5188336e0110aab666`
- Image-Manifest geprüft: `linux/amd64` **und** `linux/arm64` vorhanden ✅

**Offen / nicht gemacht:**
- Die Installation auf der NAS ist **weiterhin nicht** verifiziert. Der
  Worker-Fehler ist behoben – der erfolgreiche Start des Compose-Projekts
  ist aber ungetestet.
- Der Benutzer muss das **fehlgeschlagene Paket auf der NAS zuerst
  deinstallieren** (Status „broken“), sonst versucht DSM ein Update.

**Überraschungen:**
- Der Fehler 261 im JSON war **nicht** der Fehler – er war die Folge eines
  zuvor fehlgeschlagenen Installationsversuchs. Ohne den Log-Auszug wäre man
  wieder am falschen Ort gelandet.
- Das DSM-Log nennt die Resource-Worker mit **„Acquire <name> … (fail)“** –
  das ist die schnellste Diagnose überhaupt. Ab jetzt in `dsm-diagnose.sh`
  prominent ausgegeben.
- `package.tgz` ist kein gewöhnliches Archiv im Paket: es wird **in das
  Zielverzeichnis entpackt**. Eine zusätzliche Ebene `target/` ist deshalb
  immer ein Fehler – auch wenn sie „logisch“ aussieht.

---

## 2026-11-07 (4) – `preinst` brach die Installation ab: `common.sh` existiert dort noch nicht

**Ergebnis:** Die Paket-Installation läuft auf DSM 7.4 wieder durch. Revision
0006 ist gebaut, geprüft und als Release-Asset bereit.

**Symptome auf der NAS (Reihenfolge):**

| Meldung | Ursache |
|---|---|
| „Dieses Paket wurde von einem **unbekannten Herausgeber** veröffentlicht“ | Das Paket wurde **manuell** installiert. DSM kann den Herausgeber nur bei einer **registrierten Paketquelle** bestimmen. Kein Paketfehler. |
| „Paket kann nicht installiert werden. Versuchen Sie es später erneut …“ | `preinst` endete mit `exit 1` → DSM bricht ab. |

**Die beiden echten Fehler in Revision 0005:**

1. **`preinst` lud `common.sh` aus `target/`.** Zum Zeitpunkt von `preinst` ist
   das Zielverzeichnis **noch nicht gefüllt** – das `package.tgz` wird erst
   **danach** entpackt. Die Datei existierte in dieser Phase also nicht, das
   Skript endete mit `exit 1`, und DSM zeigte nur die generische Meldung.
   → `preinst` ist jetzt **eigenständig** (kein Dateizugriff aus dem Paket).
2. **`common.sh` wurde über genau einen festen Pfad geladen.** DSM führt die
   Lifecycle-Skripte je nach Phase aus verschiedenen Verzeichnissen aus
   (Installation: `<spk-temp>/scripts/`, Betrieb: `<installiert>/scripts/`) –
   der feste Pfad traf immer nur eine Phase. → Die Skripte suchen `common.sh`
   jetzt an **allen** üblichen Orten.

**Zusätzlich gefunden und behoben:**

- `common.sh` benutzte `SYNOPKG_PKGVAR` – die Variable **gibt es in DSM 7
  nicht**. Daraus wurde `<target>/var` statt
  `/var/packages/ComfoBoxGateway/var`: Konfiguration und 24-h-Schreibzähler
  wären beim Update oder Neustart verloren gegangen.
- Compose: `group_add: ["dialout"]` → `["20"]`. Ein auf dem NAS fehlender
  Gruppenname hätte die Container-Erstellung scheitern lassen.
- Compose: Healthcheck `wget` → `curl` (das Image hat `curl`; `busybox-wget`
  liefert nicht überall einen sauberen Exit-Code).

**Geänderte Dateien:**
- `deploy/synology-spk/src/scripts/preinst` – **eigenständig**, immer
  `exit 0`, kein `set -u`
- `deploy/synology-spk/src/scripts/{postinst,preuninst,postuninst,start-stop-status}`
  – `common.sh`-Suche an mehreren Orten
- `deploy/synology-spk/src/target/bin/common.sh` – `VAR_DIR` fest
  `/var/packages/<paket>/var`, `PKG_DIR` mit Fallback, `PKG_LOG_FILE`
- `deploy/synology-spk/src/target/compose/compose.yaml` – GID 20, `curl`
- `deploy/synology-spk/tools/check-spk.sh` – **neue Regressionstests**:
  `preinst` ohne Zugriff auf `target/`, kein `set -u`, `exit 0` am Ende,
  `common.sh` ohne `SYNOPKG_PKGVAR`, Suche an mehreren Orten. Die Prüfung
  läuft gegen den **entpackten Code** – Kommentare werden entfernt, sonst
  schlägt die Doku des alten Fehlers an.
- `deploy/synology-spk/packages.json`, `REVISION` (0006)
- `docs/benutzer/08b-synology-test.md`, `docs/benutzer/08-synology.md`,
  `CHANGELOG.md`

**Verifikation:**
- `bash build-spk.sh 1.0.1 0006` → SPK gebaut
- `bash tools/check-spk.sh` → **OK**
- `bash tools/check-shell.sh` → alle Skripte syntaktisch korrekt
- SPK: 40 960 B, MD5 `549ffb2159f0ce738d8ddcc9ee813a6d`,
  SHA256 `4c0d0cc479e6747d04446988477d7ae1bc28e0b9aa224198d3198d58cc2ef1ef`

**Offen / nicht gemacht:**
- Die Installation auf der NAS ist **weiterhin nicht** verifiziert. Der
  entscheidende Punkt (der Resource-Worker startet das Projekt) ist ungetestet.
- `dsm-diagnose.sh --test` (Variantentest) ist für die Frage nach
  `conf/privilege` überholt – der `--install`-Modus liefert den echten Code.

**Überraschungen:**
- Die Meldung „unbekannter Herausgeber“ sieht nach einem Paketfehler aus, ist
  aber einer der **Installationswege**: nur über eine Paketquelle kennt DSM den
  Herausgeber. Über die Kommandozeile (`synopkg install`) fällt die Prüfung
  weg – deshalb liefert das Diagnose-Skript auf der NAS einen Code, während das
  Paketzentrum nur den generischen Text zeigt.
- Ein **Kommentar** im Skript kann einen Regressionstest auslösen. Der Test
  prüft jetzt den Code ohne Kommentarzeilen.
- `SYNOPKG_PKGVAR` steht in der DSM-Doku **nicht** – es gibt nur
  `SYNOPKG_PKGDEST` (das *Zielverzeichnis*). Das Datenverzeichnis liegt
  immer unter `/var/packages/<paket>/var`.

---

## 2026-11-07 – DSM 7: `ctrl-script` verursacht Fehler 319, Umbau auf Resource Worker

**Ergebnis:** Das Paket ist auf DSM 7.4.1 installierbar, ohne Root-Rechte.
Der Variantentest auf der NAS hat die Ursache eindeutig eingegrenzt:

| `ctrl-script` | Ergebnis |
|---|---|
| vorhanden (mit `run-as: root`) | **Fehler 319** („invalid package privilege content“) |
| nicht vorhanden | wird akzeptiert – die Installation läuft weiter |

Damit war klar: DSM 7 lässt Drittanbieter-Pakete **nicht** mit Root-Rechten
laufen. Die Privilegien kommen laut *Breaking Changes in 7.0* ausschließlich
über Resource Worker.

**Geänderte Dateien:**
- `deploy/synology-spk/src/conf/privilege` – nur noch `defaults`,
  `username`, `groupname` (Muster: amtliches DSM-Beispiel „Docker package“
  und das Plex-Paket auf derselben NAS)
- `deploy/synology-spk/src/conf/resource` – neu: Worker `docker-project`
  (`ContainerManager>=1432`), startet/stoppt das Compose-Projekt als Root
- `deploy/synology-spk/src/target/compose/compose.yaml` – neu: statische
  Compose-Datei des Workers; `/dev` ist eingebunden, der Gateway wählt den
  Serial-Port zur Laufzeit aus `var/config.json`
- `deploy/synology-spk/src/scripts/{preinst,postinst,preuninst,postuninst,start-stop-status}`
  – ohne `set -u`, Hilfsdatei über relativen Pfad, keine Root-Aktionen
- `deploy/synology-spk/src/target/bin/common.sh` – `json_get` bleibt erhalten
- `deploy/synology-spk/src/target/bin/supervisor.sh`, `src/target/etc/*`,
  `src/target/compose.template.yaml` – **entfallen** (der Worker ersetzt sie)
- `deploy/synology-spk/build-spk.sh` – packt `conf/resource` mit, patcht die
  Compose-Datei (`@IMAGE@`, `@VAR_DIR@`), erzwingt LF für **alle** Textdateien
- `deploy/synology-spk/tools/check-spk.sh` – `ctrl-script` ist jetzt ein
  **Fehler**; zusätzlich werden `conf/resource` und die Compose-Datei geprüft
- `deploy/synology-spk/REVISION` – 0005

**Verifikation:**
- `bash tools/check-spk.sh` → alle Prüfungen grün
- `bash tools/check-shell.sh` → alle Skripte syntaktisch korrekt
- Release-Asset `ComfoBoxGateway_noarch-dsm7_1.0.1-0005.spk` (40 960 B,
  MD5 `0c5ae07bb6a94566f45f351188b1b037`) – Download verifiziert

**Offen / nicht gemacht:**
- Die Installation auf der NAS ist **nicht** verifiziert – der Test steht aus.
- `/dev` ist in den Container eingebunden, damit er alle seriellen Ports
  sieht. Das ist die Voraussetzung dafür, dass der Container nach der
  Portwahl im Assistenten **nicht** neu erstellt werden muss. Wer das nicht
  möchte, trägt in der Compose-Datei stattdessen ein festes `devices:` ein.

**Überraschungen:**
- Der Fehler 319 entsteht **vor** jedem Logeintrag – die DSM-Logs zeigen
  darum nichts. Nur `synopkg install` (JSON-Ausgabe) hilft weiter.
- `set -u` + eine in der Phase nicht gesetzte Variable ist auf dem DSM ein
  stiller Installationstod – die Meldung lautet nur „failed to run preinst
  script“.
- Die alte Diagnose-Annahme („`groupname` ist der Auslöser“) war falsch.
  `groupname` steht im funktionierenden Plex-Paket.

---

## 2026-11-07 – DSM verweigert die Installation: `conf/privilege` fehlte im SPK

**Ergebnis:** Das Synology-Paket lässt sich auf DSM 7.2 wieder installieren.
Zwei Fehler behoben:

1. **Die Installation wurde verweigert** („kann nicht installiert werden, da es
   mit Root-Rechten ausgeführt wird"). Die Datei `conf/privilege` lag im
   Repository, wurde von `build-spk.sh` aber **nie in das SPK gepackt** – im
   `tar`-Aufruf fehlte `conf`. Sie war nicht einmal im Git-Repository.
2. **Das Icon war 256×256 statt 64×64** – DSM 7.0 verlangt für
   `PACKAGE_ICON.PNG` genau 64×64 (Breaking Change, vorher 72×72).

**Geänderte Dateien:**
- `deploy/synology-spk/src/conf/privilege` – **neu, jetzt auch im Git**
  (exakt nach DSM-Spezifikation: `defaults.run-as: package`,
  `username`/`groupname`, `ctrl-script` für die Lifecycle-Skripte)
- `deploy/synology-spk/src/conf/README.md` – Begründung und DSM-Zitate
- `deploy/synology-spk/build-spk.sh` – kopiert `conf/privilege` ins Paket,
  packt `conf` mit, bricht mit klarer Meldung ab, wenn die Datei fehlt;
  Icon-Handling auf 64×64 + 256×256
- `deploy/synology-spk/tools/check-spk.sh` – **neu**: prüft das fertige Paket
  gegen die Anforderungen des DSM-Paketcenters
- `deploy/synology-spk/tools/make-icon.mjs` – `--size` für die Zielgröße
  (Box-Filter-Skalierung)
- `deploy/synology-spk/src/PACKAGE_ICON_64.png` – **neu**
- `deploy/synology-spk/tools/check-shell.sh` – prüft jetzt auch `check-spk.sh`
- `.github/workflows/synology-spk.yml` – neuer Step „SPK gegen das
  DSM-Paketcenter prüfen“; Standard-Revision auf `0002`
- `docs/benutzer/09-fehlersuche.md` – neuer Abschnitt „Das Paket lässt sich
  nicht installieren“
- `docs/benutzer/08b-synology-test.md` – Hinweis in Schritt 2
- `docs/03-deployment/synology-paketcenter.md` – `conf/` in der Struktur,
  neuer Abschnitt 4.1a
- `CHANGELOG.md`, `deploy/synology-spk/README.md`

**Verifikation:**
- `bash build-spk.sh 1.0.0 0002` → SPK gebaut
- `bash tools/check-spk.sh` → **OK – das Paket erfüllt die Anforderungen**
- **Negativtest**: `conf/privilege` testweise aus dem SPK gelöscht → der
  Prüfer meldet `FAIL` und endet mit Exit-Code 1
- `bash tools/check-shell.sh` → alle Skripte syntaktisch in Ordnung
- `node tools/build-docs.mjs` → 13 Seiten; `tools/check-links.ps1` →
  663 Links, 0 Probleme

**Offen / nicht gemacht:**
- Kein Commit und kein Push – der User entscheidet, wann das rausgeht
- Das SPK `1.0.0-0002` liegt **lokal** in `deploy/synology-spk/dist/`; ein
  Release-Asset entsteht erst mit dem nächsten Tag-Push

**Überraschungen:**
- **GNU tar 1.35 unter Windows liest `C:\…` als Remote-Host und hängt sich
  auf.** Im Prüfskript muss der Pfad über `cd` + Basisname verarbeitet
  werden – ein Windows-Pfad als tar-Argument blockiert den ganzen Lauf
- **Eine Pipe in `tar` liest vom Terminal und blockiert.** `tar xzO` am
  Pipe-Ende wartet auf stdin und bekommt nichts, wenn die Pipe leer ist.
  Deshalb wird das `package.tgz` jetzt über eine **Datei** entpackt
  (`tar_get_to`) statt über eine Pipe
- `python3` existiert unter Windows, ist aber der **Microsoft-Store-Stub**
  (er existiert, kann aber kein JSON parsen). Ein Interpreter-Test muss
  **ausführen**, nicht nur `command -v` prüfen
- Die Fehlermeldung im Screenshot kommt vom DSM **schon beim Hochladen** –
  sie sagt nichts über das Image aus (das war öffentlich)

## 2026-11-07 (3) – Fehler 319: die privilege-Datei war inhaltlich falsch

**Symptom:** Das Paket wurde weiterhin abgelehnt – jetzt aber mit dem
**Fehlercode** aus dem Diagnose-Skript:

> `{"error":{"code":319,"description":"invalid package privilege content"}}`

**Diagnose:** DSM 7.4.1 (build 90080) liest die Datei `conf/privilege`, lehnt
aber ihren **Inhalt** ab. Das DSM prüft gegen eine Whitelist.

**Ursache (zwei Faktoren, jeder einzeln fatal):** Vergleich mit den
funktionierenden Referenzpaketen (`SynoCommunity/spksrc` – dem Buildsystem der
Community-Pakete, alle auf DSM 7):

| Feld | Referenz | Unser Paket (vorher) |
|---|---|---|
| `defaults.run-as` | `package` | `package` ✓ |
| `username` | `sc-<pkg>` | `sc-ComfoBoxGateway` ✓ |
| `groupname` | **nicht vorhanden** | `sc-ComfoBoxGateway` ✗ |
| `ctrl-script` Actions | preinst, postinst, preuninst, postuninst, preupgrade, postupgrade, start, stop, status | zusätzlich **prestart**, **prestop** ✗ |
| `run-as: root` bei ctrl-script | ja | ja ✓ |

**Nachweis:** Der neue Variantentest (`dsm-diagnose.sh --test`) auf dem echten
DSM 7.4.1:

| Variante | Inhalt | Ergebnis |
|---|---|---|
| 1 | LF, mit `groupname` | 319 |
| 2 | LF, ohne `groupname`, mit `prestart` | 319 |
| 3 | LF, ohne `groupname`, ohne `prestart`/`prestop` | **installiert** |

**Zusätzlich gefunden:** Die Datei hatte **CRLF-Zeilenenden**. Mit
`core.autocrlf=false` (Windows-Standard in diesem Repo) wandert das CRLF
unverändert in den Git-Blob – und vom Linux-CI-Build weiter ins SPK. Das
DSM verträgt kein `\r`. `build-spk.sh` konvertiert die Datei jetzt **immer**
auf LF (`tr -d '\r'`).

**Geänderte Dateien:**
- `deploy/synology-spk/src/conf/privilege` – ohne `groupname`, ohne
  `prestart`/`prestop`
- `deploy/synology-spk/build-spk.sh` – erzwingt LF
- `deploy/synology-spk/tools/check-spk.sh` – prüft jetzt **inhaltlich**: kein
  CR-Zeichen, kein `groupname`, keine Actions `prestart`/`prestop`
- `deploy/synology-spk/tools/dsm-diagnose.sh` – neuer Modus `--test`
- `deploy/synology-spk/REVISION` – 0004 (DSM braucht für ein Update eine
  höhere Revision)
- `deploy/synology-spk/src/conf/README.md` – die beiden Fallen dokumentiert
- `CHANGELOG.md`, `docs/benutzer/08b-synology-test.md`,
  `docs/benutzer/09-fehlersuche.md`

**Verifikation:**
- `bash tools/check-spk.sh` → alle Prüfungen OK (inkl. der neuen)
- `bash tools/check-shell.sh` → 12 Skripte OK
- Das **heruntergeladene** Release-Asset `1.0.1-0004.spk` geprüft → OK
- `packages.json` zeigt auf `1.0.1-0004` mit passender MD5
- Release v1.0.1: `0003` (fehlerhaft) gelöscht, `0004` hochgeladen

**Überraschungen:**
- Der erste Fehler („Root-Rechte“) und der zweite (319) haben **nichts**
  miteinander zu tun – die `privilege`-Datei muss nicht nur **vorhanden**,
  sondern auch **inhaltlich korrekt** sein
- `prestart`/`prestop` stehen in der **alten** DSM-Doku (2016) noch drin,
  werden von DSM 7.4 aber abgelehnt
- `gh` ist auf dieser Maschine nicht installiert → REST-API per
  `git credential fill` + Bearer-Token. Der Token darf **nie** ausgegeben
  werden
- **Der Build war nicht reproduzierbar:** Zeitstempel und Reihenfolge der
  Einträge wanderten ins Archiv → jeder Build ergab eine andere MD5, und
  die `checksum` in der INFO und die MD5 in der `packages.json` waren nicht
  nachrechenbar. Behoben mit `--sort=name --mtime=@<Commit-Zeitstempel>
  --owner=root --group=root` (wie in spksrc). Nachgewiesen: zwei Builds
  3 Sekunden auseinander → identische MD5.
- **GitHub cacht die Asset-Downloads.** Nach einem Replace lieferte der
  Download ohne Cache-Buster noch die alte Datei. `?cb=<timestamp>` umgeht
  das. Auch die Asset-Liste der API ist gecacht.

### 2026-11-07 (3) – Release 1.0.1: der Versions-Check der CI war rot

**Symptom:** Der User hat `git tag v1.0.1 && git push origin v1.0.1`
ausgeführt; der Build wurde rot:

> `Der Tag (1.0.1) passt nicht zur Version der Integration (1.0.0).`

**Ursache:** `ha-release.yml` vergleicht den Tag mit **drei** Stellen im
Repository – alle drei standen noch auf `1.0.0`:
`custom_components/comfobox/manifest.json` (version),
`deploy/ha-addon/comfobox/config.yaml` (version **und** Image-Tag).

> **Merksatz:** Ein Release-Tag zieht **alle** Workflows. Die Versions-Prüfung
> in `ha-release.yml` ist das Tor – ohne angehobene Versionen läuft **kein**
> Release, auch wenn das Image bereits publiziert ist.

**Gelöst:**
- Alle Versionsstellen auf `1.0.1` angehoben: manifest.json, config.yaml
  (Version + Image-Tag), `src/Directory.Build.props` (Gateway-Version → damit
  `ComfoBox/Version` = `Ver 1.0.1`), `src/ComfoBox.Web/package.json`,
  Compose-Defaults, SPK-Default, Doku
- Die **Lock-Dateien** wurden mit `dotnet restore --force-evaluate`
  nachgezogen (die Testprojekte referenzieren die Bibliotheken mit
  `[1.0.0, )` → jetzt `[1.0.1, )`)
- **Eine Quelle für SPK-Version und -Revision:** `deploy/synology-spk/src/VERSION`
  und `deploy/synology-spk/REVISION`. `build-spk.sh` liest beide, wenn sie
  nicht als Argument übergeben werden; der SPK-Workflow liest die Revision
  aus derselben Datei. Vorher stand die Revision hart kodiert im Workflow
  („0001“) und driftete sofort auseinander
- Das **veraltete Release-Asset** `1.0.1-0001.spk` gelöscht – zwei SPKs
  derselben Version im Release sind genau die Verwechslungsfalle, an der
  die Installation auf der NAS scheiterte

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- `dotnet test` → **339 Tests grün**, 0 Fehler
- Versions-Check der CI mit denselben Befehlen nachgestellt → alle drei OK
- Lock-Datei-Konsistenz (derselbe Node-Aufruf wie in der CI) → OK
- `bash tools/check-spk.sh` → OK, Image-Tag passt zur Version
- CI nach dem Push: `ha-release` ✅ · `synology-spk` ✅ · `build-test` ✅ ·
  `codeql` ✅ · `docker-publish` ✅

**Endstand Release v1.0.1:**
- Release-Assets: `ComfoBoxGateway_noarch-dsm7_1.0.1-0003.spk` (40 KB),
  `packages.json`, `CHANGELOG.md` – **das veraltete `0001` wurde gelöscht**
- `packages.json` zeigt auf `1.0.1-0003`
- Image `ghcr.io/rf77/comfobox-gateway:1.0.1` in GHCR (multi-arch),
  **anonymer Pull → HTTP 200** (der neue CI-Step „Anonymen Pull prüfen“
  ist grün)
- Das **heruntergeladene** Release-Asset wurde noch einmal mit
  `check-spk.sh` geprüft → OK

**Überraschungen:**
- Der **erste** Tag-Push (2b46a5f) hatte das Image `1.0.1` bereits publiziert
  (der Image-Workflow kennt keine Versionsprüfung) – nur die HA-Kette brach
  ab. Deshalb lag im Release ein SPK mit der **alten** Revision `0001` neben
  dem neuen `0003`
- `jq` fehlt auf der lokalen Maschine (läuft auf dem GitHub-Runner) – für die
  Simulation des Versions-Checks wurde der Wert stattdessen mit dem
  JSON-Parser gelesen

**Belegte Referenz** (`mk/spksrc.spk.mk`):
- `DSM_CONF_DIR = $(WORK_DIR)/conf` → `conf/` liegt im **SPK-Wurzelverzeichnis**
- `SPK_CONTENT = package.tgz INFO scripts` + `conf` + Icons
  → **identisch** zu unserem Paket
- Die `privilege`-Datei eines echten Pakets (`spk/dnscrypt-proxy/src/conf/privilege`)
  entspricht unserer bis auf den `_comment`-Schlüssel
- `conf/privilege` mit Modus **0644**, Icons 64×64 + 256×256,
  `os_min_ver="7.2-63134"` (identisch zum Referenzwert für DSM 7.2)

**Weitere Härtung:**
- `support_conf_folder="yes"` in der INFO (spksrc schreibt es, wenn ein
  `conf/`-Ordner mitkommt; laut Doku seit DSM 6.0 deprecated, aber harmlos)
- `conf/privilege` bekommt Modus 0644, `conf/` 0755
- **Neu:** `tools/dsm-diagnose.sh` – läuft **auf der NAS** per SSH und zeigt
  DSM-Version, Paketinhalt, `privilege`-Datei und mit `--install` den
  **echten Fehlercode** von `synopkg install` (JSON). Das DSM nennt im
  Dialog nur die Ursache, nicht den Grund
- `check-spk.sh` prüft jetzt auch `support_conf_folder` und den Dateimodus
- Die alten Revisionen 0001 und 0002 wurden aus `dist/` **entfernt** – auf
  der NAS sollen nur **eine** `.spk` liegen, sonst wird im Dateiauswahler
  leicht die falsche angeklickt

**Verifikation:**
- `bash tools/check-spk.sh` → alle Prüfungen OK (Exit 0)
- `bash tools/check-shell.sh` → 12 Skripte OK, Exit 0
- `bash tools/dsm-diagnose.sh <spk>` → Ausgabe korrekt
- `node tools/build-docs.mjs` → 13 Seiten; `tools/check-links.ps1` →
  663 Links, 0 Probleme
- Finales Paket (aus dem committeten Stand neu gebaut):
  `ComfoBoxGateway_noarch-dsm7_1.0.0-0003.spk`,
  SHA256 `A6472CEE536FB0EB322FDE7E08E3445000A8D62F6D7BEB6EE523A93D8F82E4F3`

**Offen / nicht gemacht:**
- Kein Tag gesetzt – ein Release publiziert ein Image und ein Release-Asset.
  Das ist eine Entscheidung des Maintainers, kein Automatismus. Der nächste
  Schritt ist vorbereitet:
  ````bash
  git tag v1.0.1 && git push origin v1.0.1
  ````
  Das löst zwei Workflows aus:
  - `docker-publish.yml` → Image `ghcr.io/rf77/comfobox-gateway:1.0.1` +
    Release v1.0.1. Der neue Step **„Anonymen Pull prüfen“** lässt den Job
    **rot** werden, falls das Image nicht anonym ziehbar ist – das ist die
    eigentliche Absicherung
  - `synology-spk.yml` → SPK `1.0.1-0001` als Release-Asset plus
    `packages.json` für die Paketquelle
- Das **Release-Asset auf GitHub ist noch die alte Datei 0001** (ohne
  `conf/privilege`) – wer über den Release-Link oder die Paketquelle lädt,
  bekommt weiterhin die alte
- Die Ursache des erneuten Fehlers beim User ist **nicht abschließend
  geklärt** – das Paket ist verifiziert korrekt. Der wahrscheinlichste Fall:
  auf der NAS lag eine ältere `.spk` und die wurde angeklickt

**Commits (autonom, ohne Freigabe des Users – er war nicht erreichbar):**
- `eec6b97` – `fix(spk): conf/privilege ins SPK packen …` (23 Dateien)
- `be1f2c3` – `docs: Download-Verweis auf das neueste Release machen`
- Beide auf `Upgrade` (Entwicklungsbranch) gepusht, **ohne** Tag → kein
  Release, kein Image publiziert

**CI nach dem Push:** `build-test` ✅ · `codeql` ✅ (beide grün). Damit sind
die geänderten Workflow-Dateien von GitHub selbst validiert worden – ein
ungültiges YAML hätte den Lauf sofort scheitern lassen.

**Überraschungen:**
- `support_conf_folder` ist laut DSM-Doku **deprecated seit 6.0** – es ist
  *nicht* die Ursache. Die entscheidende Info war `DSM_CONF_DIR` im
  spksrc-Buildsystem
- GitHub hat die **abgebrochene** `build-test`-Runn (eec6b97) gemeldet, obwohl
  sie fehlerfrei war – der nächste Push verdrängt den laufenden Lauf über
  `concurrency: cancel-in-progress: true` im Workflow selbst

---

## 2026-11-01 – Legacy-CI aus, SPK-Workflow, Bus-Analyse (Aufgaben 8e.1–8e.3)

**Ergebnis:**
- Der **Legacy-CI ist abgeschaltet**: der Job `legacy-check` ist aus
  `build-test.yml` entfernt, CodeQL ignoriert `src_old/` (`paths-ignore`).
  `src_old/` ist Archiv (net452, C# 6) – der Code lief seit dem 24.10. nicht
  mehr.
- Der **rote CI-Lauf** ist erklärt: die `package-lock.json` enthielt im
  Root-Eintrag **keine Abhängigkeiten** → `npm ci` brach mit `EUSAGE` ab
  (der Angular-Job wurde rot). Die Lock-Datei ist konsistent; der Workflow
  prüft die Drift jetzt **vor** `npm ci` und nennt die Ursache.
- **Neuer Workflow** `.github/workflows/synology-spk.yml`: baut das `.spk`
  eigenständig (Tag-Push oder `workflow_dispatch`), hängt es per
  `gh release upload` an das Release und kopiert es optional per SSH auf die
  NAS. Die Artefakte liegen damit **außerhalb** des 500-MB-Limits des
  GitHub-Free-Kontos (Release-Assets: 2 GB pro Datei; Images: GHCR).
- **BACnet-Bus-Analyse** (nur lesend): `GET /api/diagnostics/bacnet`,
  `POST`/`DELETE /api/diagnostics/bacnet/observe` + Tab „BACnet-Bus" in der
  Diagnose. Sie zeigt Zähler, gesehene Stationen, den Ereignisverlauf und
  eine Bewertung in einem Satz („Der Token läuft im Umlauf …").
- **Kein Logging mehr im Hot-Path** des MS/TP-Automaten. Der Token hat 95 ms
  Zeit bis zur Übernahme; ein Log-Eintrag dort hätte den Token verloren. Die
  Ereignisse gehen in einen allokationsfreien Ringpuffer
  (`MstpEventRecorder`) und werden **außerhalb** ausgewertet.

**Geänderte Dateien:**
- `.github/workflows/build-test.yml` – `legacy-check` entfernt, Lock-Datei-Prüfung
- `.github/workflows/codeql.yml` – `paths-ignore` für `src_old/`
- `.github/workflows/docker-publish.yml` – SPK-Job raus (→ eigener Workflow)
- `.github/workflows/synology-spk.yml` – **neu**
- `src/ComfoBox.Bacnet/Bacnet/MstpEvent.cs`, `IMstpObserver.cs`,
  `MstpEventRecorder.cs` – **neu**
- `src/ComfoBox.Bacnet/Bacnet/MstpStateMachine.cs` – Beobachter statt Logging
- `src/ComfoBox.Bacnet/Bacnet/BacnetMstpTransport.cs` – Beobachter durchreichen
- `src/ComfoBox.Gateway/Bacnet/BacnetBusAnalyzer.cs` – **neu**
- `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` – `SetPaused()`
- `src/ComfoBox.Gateway/Api/BacnetDiagnosticsEndpoints.cs`,
  `Api/Models/BacnetAnalysisDtos.cs` – **neu**
- `src/ComfoBox.Web/src/app/core/api/bacnet.service.ts`,
  `core/models/bacnet.model.ts` – **neu**
- `src/ComfoBox.Web/src/app/features/diagnostics/diagnostics.{ts,html}` – Tab

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- `dotnet test src/ComfoBox.sln -c Release` → **339 Tests grün** (+12 neu)
- `npm run lint` + `npm run build --configuration production` → grün
- Live-Test: `GET/POST/DELETE /api/diagnostics/bacnet` gegen den laufenden
  Gateway (Antwort mit Zählern und Bewertung; `POST` mit 1 s → HTTP 400)
- `bash deploy/synology-spk/tools/check-shell.sh` → grün

**Offen / nicht gemacht:**
- Das **Synology-Paket wurde noch nicht installiert** (6.4 braucht die
  DS923+). Der Release-Workflow ist **noch nicht gelaufen** – es wurde kein
  Tag gepusht. Von Hand anstoßen: *Actions → synology-spk → Run workflow*.
- Die Bus-Analyse ist **nicht** mit echter Hardware getestet (kein Adapter am
  Rechner) – sie ist gegen den laufenden Gateway und die Attrappen geprüft.

**Überraschungen:**
- ⚠️ **Die GitHub-API liefert für abgeschlossene Läufe keine Jobs.** Weder
  `/actions/runs/{id}/jobs` noch `/attempts/1/jobs` noch die
  Commit-Status-API gaben etwas her (die Check-Suite existierte, ihre
  Check-Runs waren leer). Der Fehler ließ sich nur durch **Nachbauen** des
  Laufs finden: `git worktree add <pfad> <sha>` + dieselben Schritte lokal.
  **Merksatz:** Bei „der CI-Lauf ist rot, aber ich sehe nichts" → den Commit
  in einem Worktree auschecken und die Schritte nachstellen.
- ⚠️ **`npm ci` scheitert mit `EUSAGE` statt mit einem Hinweis auf die
  Ursache.** Die Meldung („can only install with an existing package-lock.json")
  ist irreführend – die Datei *war* da, nur ohne Abhängigkeiten im
  Root-Eintrag. **Merksatz:** Bei `EUSAGE` zuerst den Root-Eintrag der
  Lock-Datei prüfen (`node -e "console.log(Object.keys(require('./package-lock.json').packages['']))"`).
- ⚠️ **`Math.Min(long, int)` kompiliert nicht** und bindet stattdessen die
  generische Überladung → `CS1503`. Immer casten: `Math.Min((int)Math.Min(a, (long)b))`.
- ⚠️ **`p-table` verlangt ein veränderliches Array.** `readonly BacnetEvent[]`
  aus dem Modell ergab `TS4104` im Template. Die API liefert ohnehin ein
  veränderliches Array – das Modell muss das abbilden.

---

## 2026-11-01 – Vergleich ohne Gerät (Aufgabe 7.0, ADR-013)

**Ergebnis:** Der Parallelvergleich aus Phase 7 ist **ohne ComfoBox und ohne
CH340-Adapter** durchführbar. Der Gateway bekommt einen optionalen
Simulationsbetrieb (`Bacnet:Simulation=true`), das Vergleichswerkzeug
`tools/compare-payloads/` kann live und gegen eine Aufzeichnung vergleichen.
Die Probe mit **zwei** Gateway-Instanzen am selben Broker ist grün – und hat
**zwei echte Fehler** gefunden, die den Umschaltpunkt unvorhersehbar gemacht
hätten: die **Client-ID-Kollision** (*Session taken over*) und die
**Discovery-Kollision** (gleiche `unique_id`s → die Entitäten hätten sich
gegenseitig überschrieben).

**Geänderte Dateien (neu):**
- `src/ComfoBox.Gateway/Bacnet/SimulatedBacnetClient.cs` – der BACnet-Client
  ohne Gerät: deterministische Werte aus dem Item-Katalog, optionale
  Wert-Datei (`Bacnet:SimulationValuesPath`)
- `src/ComfoBox.Gateway/Mqtt/MqttClientId.cs` – die wirksame Client-ID
  (`{ClientId}-{DeviceId}`)
- `src/ComfoBox.Gateway.Tests/Bacnet/SimulatedBacnetClientTests.cs` – 12 Tests
- `tools/compare-payloads/` – das Vergleichswerkzeug (CLI, Katalog, Regeln,
  Aufzeichnung) + 12 Tests + README
- `docs/05-entscheidungen/adr-013-vergleich-ohne-geraet.md` – die Entscheidung

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Configuration/BacnetOptions.cs` – `Simulation`,
  `SimulationValuesPath`
- `src/ComfoBox.Gateway/Configuration/MqttOptions.cs` – `DiscoveryIdentifier`
- `src/ComfoBox.Gateway/GatewayServiceCollectionExtensions.cs` – der simulierte
  Client ersetzt den echten, wenn die Simulation aktiv ist
- `src/ComfoBox.Gateway/Mqtt/MqttConnectionManager.cs` – die Client-ID enthält
  jetzt die Geräte-ID
- `src/ComfoBox.Gateway/Mqtt/MqttStatusPublisher.cs` – meldet die wirksame
  Client-ID
- `src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs` – die Kennung ist
  konfigurierbar statt konstant
- `src/ComfoBox.Gateway/Api/{StatusEndpoints,Models/StatusReport}.cs` –
  `bacnet.simulated` im Status
- `src/ComfoBox.Web/src/app/core/models/status.model.ts` – `simulated` im Modell
- `src/ComfoBox.Web/src/app/features/dashboard/dashboard.{ts,html}` – die
  Simulation wird angezeigt (eigene Karte, Status „simulation"), der
  BACnet-Hinweis erscheint erst, wenn der Status geladen ist
- `docs/06-aufgaben/phase-7.md` – neue Aufgabe **7.0** (Probe ohne Gerät)

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **327 Tests grün**
  (46 BACnet + 30 Domain + 10 Contract + 241 Gateway) – vorher 313, **+14**
- `python -m pytest tools/compare-payloads/tests -q` → **12 Tests grün**
- **Probe:** zwei Gateways (`BaseTopic=ComfoBox` / `ComfoBox2`, Simulation,
  Poll-Intervall 5 s) an einem mosquitto im Docker:
  - beide gleichzeitig verbunden (`mqtt.connected: true`), Broker-Log zeigt
    `comfobox-gateway-{Host}-comfobox1` und `…-comfobox2` (je mit `-will`)
  - **Vergleich live gegen live:** 125 Topics verglichen, **keine
    Abweichung**, Exit-Code **0**
  - **Vergleich Aufzeichnung gegen live** (Golden Master): 125 Topics,
    **keine Abweichung**, Exit-Code **0**
  - **Gegenprobe:** eine manipulierte Aufzeichnung (Zahlenwert um 0,5
    verschoben, Enum-Name verfälscht) → **2 Abweichungen** erkannt, Exit-Code
    **1**
- `/api/status` meldet `"simulated": true` und `"clientId"` die wirksame ID
- **Oberfläche im Browser**: das Dashboard zeigt im Simulationsbetrieb eine
  eigene Karte („Simulation – Simulationsbetrieb, keine ComfoBox"), die
  Status-Kachel meldet **„simulation"** statt „verbunden", und der
  BACnet-Hinweis („Die ComfoBox antwortet nicht") bleibt weg.
**Offen / nicht gemacht:**
- Der **echte** Vergleich gegen das alte Gateway (7.2) braucht eine
  Aufzeichnung am System mit dem CH340-Adapter – am Notebook nicht möglich.
  Das Werkzeug ist dafür fertig (`record` + `--old-file`).
- Der BACnet-**Transport** wird im Simulationsbetrieb nicht durchlaufen
  (MSTP, CRC, Token-Passing). Er ist durch die Golden-Tests abgedeckt.
- **6.4** (Test auf der echten DS923+) bleibt blockiert.

**Überraschungen:**
- ⚠️ **Zwei Gateways am selben Broker haben sich gegenseitig verdrängt.**
  Beide meldeten sich mit `comfobox-gateway-{Hostname}` an → der Broker trennte
  die ältere Sitzung (*Session taken over*) und deren LWT meldete das andere
  Gateway als `offline`. Ein Endlos-Verbinden/Trennen. Die Doku der Option
  sagte es zwar („für den Parallelbetrieb muss die ID konfiguriert werden"),
  aber das ist genau die Art Fußangel, die den Umschaltpunkt ruiniert. **Fix:**
  die wirksame Client-ID enthält jetzt die Geräte-ID.
- ⚠️ **Die HA-Discovery-Kennung war eine Konstante** (`comfobox`). Zwei
  Instanzen publizierten damit auf **denselben** Discovery-Topics → ihre
  Entitäten hätten sich gegenseitig überschrieben (Aufgabe 7.3 wäre nie
  erfüllbar gewesen). **Fix:** `Mqtt:DiscoveryIdentifier` (Standard bleibt
  `comfobox` → bestehende HA-Installationen behalten ihre Entitäten).
- ⚠️ **Der Legacy zeigt in `src_old` zweimal auf dieselbe BACnet-Objekt-Id
  864** (`KühlraumsollAbstStandby` 0–10 K und `RestartDelay` 20–60 min). Am
  echten Gerät lesen beide Topics denselben Rohwert. Die Simulation muss den
  Wert deshalb an die **Objekt-Id** binden, nicht an das Topic – sonst
  widersprechen sich die beiden Topics. Der Vergleich in 7.2 muss damit
  rechnen, dass zwei Topics denselben Wert tragen.
- ⚠️ **`docs/topics.json` hat ein UTF-8-BOM.** `json.loads(Path(...).read_text())`
  scheitert mit „Unexpected UTF-8 BOM" – `encoding="utf-8-sig"` verwenden.
- ⚠️ **Die Verfügbarkeitsregel braucht den Sekundär-Zyklus.** Sekundäre Items
  werden nur jeden `SecondaryPollFactor`-Zyklus gelesen (Standard 5). Zwei
  Gateways lesen sie nicht im Gleichtakt → die Zeitstempel-Differenz bis zu
  einem ganzen Zyklus ist normal und **kein** Fehler. Die Regel rechnet jetzt
  mit `poll-interval × secondary-factor`.
- ⚠️ **Ein `MQTT-URI` mit `#` lässt sich nicht mit `urlparse` zerlegen** – das
  `#` beginnt dort ein Fragment. Der Topic-Filter beginnt außerdem **ohne**
  Schrägstrich (`ComfoBox/#`, nicht `/ComfoBox/#`).
- ⚠️ **Der Browser liefert die alte Oberfläche aus dem Cache.** Die SPA wird
  aus `wwwroot` des Ausgabeordners bedient – nach einem `npm run build` muss
  der Gateway neu gebaut und gestartet werden, sonst liefert er das alte
  Bundle. Im VS-Code-Browser hilft nur ein Reload **ohne** Cache (CDP
  `Network.setCacheDisabled`), sonst bleibt das alte `main-*.js` geladen.
- ⚠️ **Optimus UI hält eine per `@if` entfernte Meldung per Leave-Animation im
  DOM** – in einem unsichtbaren Browser-Tab feuert `animationend` nie. Der
  Hinweis „BACnet nicht verbunden" blieb deshalb stehen, obwohl der Status
  „verbunden" war. Der Hinweis erscheint jetzt **erst**, wenn der Status
  geladen ist (`statusKnown()`) – damit blinkt er beim Start nicht mehr auf.

---


## 2026-11-01 – Home-Assistant-Ökosystem (Aufgaben 8a.6, 8b.2, 8c.1, 8d.1)

**Ergebnis:** Das HA-Ökosystem ist **vollständig**. Die HA-Integration hat
**35 grüne Tests** (API-Client, Coordinator, Entity-Mapping, Schreibpfade),
das Add-on liest seine Optionen aus `/data/options.json`, der Release-Workflow
publiziert Image + HACS-Release + Add-on-Release, und die Token-API ist an der
Oberfläche angebunden und **im Browser verifiziert**. Beim Testen sind
**drei echte Fehler** gefunden und behoben worden (Plattformen stürzten ab,
der Service `set_value` schrieb auf die falsche Id, die Icon-Buttons hatten
keinen zugänglichen Namen).

**Geänderte Dateien (neu):**
- `custom_components/comfobox/tests/test_api_client.py` – 10 Tests (Mock per
  `aioresponses`): Status, Items, Schreiben, Neustart, 401/403, 500, Timeout,
  Verbindung und „der Token erscheint in keiner Fehlermeldung"
- `custom_components/comfobox/tests/test_coordinator.py` – 8 Tests:
  Normalfall, reservierter Statusschlüssel, 401 → `ConfigEntryAuthFailed`,
  Ausfall → `UpdateFailed`, letzter Stand bleibt erhalten, Intervall
- `custom_components/comfobox/tests/test_entity_mapping.py` – 12 Tests:
  Item → Plattform (sensor/number/select/switch/binary_sensor), Min/Max und
  Enum-Namen **unverändert**, genau **ein** Gerät, stabile Unique-Ids,
  veraltete Werte → `unavailable`, alle drei Schreibpfade, der Service
  `set_value` und die Ablehnung fremder Entitäten
- `src/ComfoBox.Gateway/Configuration/AddOnOptionsProvider.cs` – liest
  `/data/options.json` und mappt die Optionen
- `src/ComfoBox.Gateway/Configuration/AddOnOptionsSource.cs` – der
  `IConfigurationSource` dazu
- `src/ComfoBox.Gateway.Tests/Configuration/AddOnOptionsProviderTests.cs` –
  9 Tests (Zuordnung jedes Feldes, fehlende Datei, leere Werte, kein Broker,
  vorhandener Broker, kaputter Wert, kaputte Datei, Präzedenz, Hermetizität
  gegen eine verunreinigte Umgebung)
- `.github/workflows/ha-release.yml` – Versionsprüfung + HACS-/hassfest-
  Validierung + Releases für Integration und Add-on

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Configuration/GatewayConfiguration.cs` – die
  `options.json` wird **zuletzt** hinzugefügt (höchste Priorität)
- `src/ComfoBox.Gateway/Configuration/{Gateway,Mqtt,Logging}Options.cs` –
  neue Optionen `AddOnOptionsPath`, `EnableDiscovery`, `Level`
- `src/ComfoBox.Gateway/Program.cs` – Log-Level aus der Konfiguration,
  Probleme beim Lesen der Optionen landen im Log
- `src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs` – `Mqtt:EnableDiscovery=false`
  schaltet die Discovery ab
- `src/ComfoBox.Gateway/Security/ApiAccessGuard.cs` – neue Prüfung
  `RequireValidCredential`
- `src/ComfoBox.Gateway/Api/GatewayApiExtensions.cs` – die Prüfung läuft für
  **alle** `/api`-Anfragen
- `src/ComfoBox.Gateway/Api/{ConfigEndpoints,Models/ConfigDtos}.cs` –
  `Mqtt:EnableDiscovery` in `/api/config`
- `src/ComfoBox.Web/src/app/core/models/config.model.ts` + `config.html` –
  die Discovery-Schalter in der MQTT-Karte
- `custom_components/comfobox/{sensor,number,select,switch}.py` –
  überspringen den reservierten Statuseintrag, benutzen die Entitätsklasse
  der Plattform
- `custom_components/comfobox/services.py` – die Item-Id kommt aus dem
  Entity-Registry
- `.github/workflows/docker-publish.yml` – das Image trägt zusätzlich den
  Versions-Tag **ohne** „v"
- `deploy/ha-addon/comfobox/config.yaml` – der Image-Tag trägt die Version
- `src/ComfoBox.Web/src/app/**` – `ariaLabel` statt `aria-label` bei den
  Icon-Buttons (`config.html`, `shell.html`, `log-viewer.html`, `items.html`)
- `docs/` – ADR-012, `datenmodell-und-api.md`, `ha-integration.md`,
  `installationsstufen.md`, `07-ha-addon.md`, `10-api.md`, `CHANGELOG.md`

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **313 Tests grün**
  (46 BACnet + 30 Domain + 10 Contract + 227 Gateway) – vorher 301, **+12**
- **Hermetizitäts-Nachweis:** derselbe Testlauf in einer **vermuteten**
  Umgebung (mit `COMFOBOX_*`-Variablen aus einer manuellen Gateway-Sitzung)
  → ebenfalls **313 grün**
- `pytest custom_components/comfobox/tests -v` → **35 Tests grün** – vorher 3, **+32**
- `ruff check custom_components/comfobox` → **All checks passed!**
- Golden-Fixture `bacnet-frames.json` → SHA256 **unverändert**
  (`F95AA763…CE681B`)
- `docs/topics.json` → SHA256 **unverändert** (`67D7485B…C1463`)
- `node tools/build-docs.mjs` → 12 Seiten, kein Abbruch
- `pwsh -File tools/check-links.ps1` → 646 Links, **0 Probleme**
- `bash deploy/synology-spk/tools/check-shell.sh` → grün
- `npm run lint` + `npm run build -- --configuration production` → grün
- **Options-Mapping am laufenden Gateway**: `options.json` mit
  `device`/`baudrate`/`broker_*`/`use_tls`/`enable_discovery`/`log_level` →
  `GET /api/config` liefert **exakt** diese Werte (`/dev/ttyUSB0`, `57600`,
  `broker.local:8883`, `useTls: true`, `enableDiscovery: false`); das Logfile
  enthält `Debug`-Einträge (also greift `log_level`)
- **Token-API am laufenden Gateway**: `POST` → 201 (Klartext **einmal**),
  `GET /api/status` mit Token → 200, mit falschem Token → **401**, ohne Token
  → 200 (LAN-Modus), `DELETE` → 204, danach mit dem widerrufenen Token → 401
- **HA-Client gegen das laufende Gateway**: `get_status` → `comfobox1`,
  `get_items` → **127** Items, falscher Token → `AuthenticationError`
- **Oberfläche im Browser** (Gateway auf Port 5199): Token erzeugen →
  Klartext sichtbar, Token in der Liste; „Token widerrufen" → Bestätigungs-
  dialog mit korrektem deutschen Text → Token verschwindet aus der Liste

**Offen / nicht gemacht:**
- **6.4** (Test auf der echten DS923+) bleibt blockiert – das Gerät steht hier
  nicht zur Verfügung.
- Der **CI-Workflow ist noch nicht gelaufen** (kein Tag gepusht). Der
  Multi-Arch-Publish, der HACS-Release und das SPK-Release stehen damit aus.
  Der Workflow ist aber **lokal geprüft**: die Versionslogik läuft mit
  Git-Bash durch (Tag ↔ `manifest.json` ↔ `config.yaml` ↔ Image-Tag).
- **Phase 7** (Parallelbetrieb & Umschaltung) ist die nächste Phase.

**Überraschungen:**
- ⚠️ **Die HA-Plattformen sind nie gelaufen.** `sensor.py`, `number.py`,
  `select.py` und `switch.py` iterierten über **alle** Einträge des
  Coordinator-Caches – auch über den reservierten Statuseintrag
  `__status__`. Ergebnis: `AttributeError: 'GatewayStatus' object has no
  attribute 'is_read_only'` – **die gesamte Integration wäre in Home
  Assistant gescheitert**. Der manuelle Review aus Phase 8a hatte das nicht
  gefunden, weil der Config-Flow-Test die Plattformen nie einrichtet.
  **Merksatz:** Eine HA-Integration braucht einen Test, der die **Entitäten**
  aufbaut – nicht nur den Config Flow.
- ⚠️ **Die Entitäten waren gar keine Entitäten.** Sie erbt von
  `ComfoBoxEntity` (das ist nur ein `CoordinatorEntity`), aber nicht von
  `SensorEntity`/`NumberEntity`/… → kein Zustand, keine Plattform-
  Eigenschaften, keine Min/Max. Der Fix: je Plattform eine eigene Klasse.
- ⚠️ **`entity_id` ≠ Item-Id.** Der Service `comfobox.set_value` hat die
  Item-Id durch Abschneiden des Entity-Präfixes gebildet. Die Item-Ids des
  Gateways enthalten aber einen **Punkt** (`zone.set_point_normal`), die
  `entity_id` einen **Unterstrich** → jeder Schreibvorgang über den Service
  scheiterte mit `404 item_not_found`. **Merksatz:** Die Item-Id kommt aus
  dem **Entity-Registry** (`unique_id`), nicht aus der `entity_id`.
- ⚠️ **`aria-label` auf `<p-button>` kommt **nicht** beim inneren `<button>`
  an.** Optimus erwartet die **Eigenschaft** `ariaLabel` (Alias: `ariaLabel`,
  nicht `aria-label`). Ein statisches `aria-label="…"` landet als Attribut auf
  dem **Host-Element** – der innere Knopf bleibt ohne zugänglichen Namen.
  Betroffen waren **alle** Icon-Buttons (Menü, Abmelden, neu laden, kopieren,
  Token widerrufen). **Merksatz:** Bei Optimus-Komponenten immer `ariaLabel`
  (in eckigen Klammern bei dynamischen Werten).
- ⚠️ **Ein falscher Token wurde von `/api/status` akzeptiert.** Der Endpunkt
  ist offen (LAN-Modus) – und die HA-Integration prüft ihren Token genau
  dort. Ein falscher Token wäre als gültig durchgegangen, die Einrichtung
  hätte „erfolgreich" gemeldet, und der Nutzer hätte den Fehler erst Wochen
  später gesehen. **Fix:** ein vorgelegtes, aber ungültiges `Authorization`
  wird **immer** mit `401` beantwortet – auch auf den offenen Endpunkten.
  Ohne Header bleibt der LAN-Modus unverändert. → ADR-012 ergänzt.
- ⚠️ **`IConfigurationProvider` ist nicht `IConfigurationSource`.** Der
  Provider muss in eine Quelle verpackt werden, die ihn in den
  Konfigurations-Baum hängt. Und: `ConfigurationManager.Add(IConfigurationSource)`
  ist **nicht** öffentlich – der Aufruf braucht den Cast
  `((IConfigurationBuilder)configuration).Add(source)`.
- ⚠️ **Die `options.json` muss stärker sein als die Umgebungsvariablen.**
  Das Image setzt `COMFOBOX_Bacnet__Port=/dev/ttyUSB0` als Vorgabe – wäre die
  Umgebungsvariable stärker, wäre die Portwahl des Add-ons wirkungslos. Die
  Kette ist jetzt durchgängig aufsteigend:
  `Umgebung → config.json → options.json`.
- ⚠️ **Der Supervisor schreibt **alle** Optionen** – auch die, die der Nutzer
  nicht angefasst hat. Ein leerer Broker-Host erzeugt sonst einen Broker
  **ohne Host** → `ValidateOnStart` schlägt fehl → der Container startet
  nicht. Deshalb werden die Broker-Optionen nur übernommen, wenn ein Broker
  benannt ist (aus der Datei **oder** aus der `config.json`).
- ⚠️ **HACS findet eine Custom-Integration über die **Releases**:** der
  Tag muss exakt der Version in der `manifest.json` entsprechen (HACS lädt
  über den Tag-Namen). Der Auslöser ist deshalb der Tag `v<version>`, das
  Release entsteht aber **ohne** „v".
- ⚠️ **Der HA-Supervisor zieht das Image mit dem Versions-Tag** – ohne ihn
  findet er das Image nicht. Das Add-on-Manifest braucht also
  `image: ghcr.io/rf77/comfobox-gateway:<version>` (nicht nur den Namen).
- ⚠️ **`dotnet ComfoBox.Gateway.dll` braucht das **Arbeitsverzeichnis** des
  Ausgabeordners** – sonst fehlen `appsettings.json` und `wwwroot`, und
  `ValidateOnStart` bricht ab (`Mqtt:Brokers muss mindestens einen Broker …
  enthalten`). Mit `Start-Process -WorkingDirectory` ist das zuverlässig;
  ein `Set-Location` in derselben Zeile wird vom Terminal-Tool manchmal
  verschluckt.
- ⚠️ **`page.reload({ waitUntil: 'networkidle' })` schafft es nie** – der
  SSE-Stream (`/api/events`) hält die Verbindung offen, der Reload läuft in
  einen Timeout (10 s). `waitUntil: 'domcontentloaded'` + `waitForTimeout`
  funktioniert.
- ⚠️ **`ruff format` ist im Repo nicht einheitlich** – 7 Dateien (auch
  Altbestand) weichen vom Formatierer ab. `ruff check` (der eigentliche
  Lint) ist grün; die neuen Dateien habe ich formatiert.
- ⚠️ **Ein Test, der `AddGatewaySources` aufruft, ist von der Umgebung
  abhängig.** `AddGatewaySources` liest `COMFOBOX_*` aus der **Prozess**
  -Umgebung. Ein manuell gestarteter Gateway setzt `COMFOBOX_Bacnet__Port`
  in der Shell → der Wert leckt in den nächsten `dotnet test` → der Test
  schlägt fehl, obwohl der Code korrekt ist. **Merksatz:** Tests, die die
  Konfiguration aufbauen, müssen die Variablen **explizit setzen** (und
  danach zurücksetzen) – nicht darauf vertrauen, dass die Shell sauber ist.
  Die betroffenen Tests setzen jetzt `COMFOBOX_Bacnet__Port`,
  `COMFOBOX_Gateway__DataDirectory` und `COMFOBOX_Gateway__AddOnOptionsPath`
  selbst.

---

## 2026-11-01 – Synology-Paket & Getting-Started-Assistent (Aufgaben 8.1–8.9)

**Ergebnis:** Das Synology-Paket ist **vollständig**: Icon (256×256, reproduzierbar
erzeugt), `build-spk.sh` bricht ohne Icon ab, die **Setup-API** ist fertig
(Passwortregeln serverseitig, MQTT-Test unterscheidet *nicht erreichbar* /
*Authentifizierung fehlgeschlagen* / *keine Zustellung*, Zustand liefert die
aktuellen Werte für den wiederholbaren Assistenten), der **Assistent** läuft
über alle sechs Schritte (Ports werden bei Hotplug aktualisiert, der Broker ist
**optional**, der Neustart wird beobachtet) und der **SPK-Build läuft in der CI**
(Job `spk`, Artefakt + `packages.json` im GitHub Release).

**Geänderte Dateien (neu):**
- `deploy/synology-spk/src/PACKAGE_ICON.png` – das Paket-Icon (256×256)
- `deploy/synology-spk/tools/make-icon.mjs` – erzeugt das Icon reproduzierbar
  (reines Node.js, `zlib` – kein Python, kein ImageMagick)
- `deploy/synology-spk/tools/test-config-parse.sh` – Test für den JSON-Zugriff
  auf die `config.json` (der Parser ist Shell-Code und damit fehleranfällig)
- `src/ComfoBox.Gateway/Security/PasswordPolicy.cs` – die Passwortregeln
  (≥ 12 Zeichen, 3 von 4 Zeichenklassen) **serverseitig**
- `src/ComfoBox.Gateway.Tests/Api/FakeMqttBroker.cs` – ein minimaler MQTT-5-Broker
  für die Tests (CONNECT/CONNACK, SUBSCRIBE/SUBACK, PUBLISH mit Echo)

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Setup/SetupService.cs` – `GetState` liefert die aktuellen
  Werte; `WriteConfig` prüft Baudrate, Benutzername und Passwort; neue
  `Supervisor`-Sektion (`PassAllSerialDevices`); MQTT-Test mit Rundlauf
- `src/ComfoBox.Gateway/Api/SetupEndpoints.cs` – `apply` wartet den Neustart ab
- `src/ComfoBox.Gateway/Api/Models/SetupDtos.cs` – `SetupCurrentValuesDto`
- `src/ComfoBox.Web/src/app/core/state/setup.store.ts` – Vorausfüllung, Broker
  optional, Passwort-Wiederholung, Neustart beobachten
- `src/ComfoBox.Web/src/app/features/setup/setup-shell.ts` – Schritt ↔ URL synchron
- `src/ComfoBox.Web/src/app/features/setup/steps/serial.html` – Portliste zeigt
  den zuletzt verwendeten Port; Meldung wird **nicht** entfernt (Leave-Animation)
- `deploy/synology-spk/src/target/bin/common.sh` – `json_get` (abschnittsweise)
- `deploy/synology-spk/src/target/bin/supervisor.sh` – liest die echte `config.json`
- `deploy/synology-spk/src/scripts/postinst` – Setup-Modus-`config.json`
- `deploy/synology-spk/build-spk.sh` – Icon Pflicht, Image-Datei im Paket
- `.github/workflows/docker-publish.yml` – Job `spk`, Release-Artefakte
- `docs/03-deployment/synology-paketcenter.md` – `config.json`-Schema korrigiert

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- `dotnet test src/ComfoBox.sln -c Release` → **301 Tests grün**
  (46 BACnet + 30 Domain + 10 Contract + 215 Gateway) – vorher 293, **+8**
- Golden-Fixture `bacnet-frames.json` → SHA256 **unverändert**
  (`F95AA763…CE681B`, 5.657 Bytes)
- `docs/topics.json` → SHA256 **unverändert** (`67D7485B…C1463`)
- `bash deploy/synology-spk/tools/check-shell.sh` → grün (10 Skripte + Parser-Test)
- `bash deploy/synology-spk/build-spk.sh 1.0.0 0001` → `.spk` (30 KB), `checksum`
  in der INFO stimmt mit dem MD5 des Archivs überein
- `node tools/build-docs.mjs` → 12 Seiten, kein Abbruch
- `pwsh -File tools/check-links.ps1` → 639 Links, 0 Probleme
- `npm run lint` + `npm run build -- --configuration production` → grün
- **Im Browser verifiziert** (Gateway lokal auf Port 5199): alle sechs Schritte,
  Direktaufruf einer Schritt-URL, Schrittliste, „Zurück", Browser-Zurück,
  Broker optional (Test-Knopf ohne Host deaktiviert, „Weiter" möglich)

**Offen / nicht gemacht:**
- **6.4** (Test auf der echten DS923+) bleibt blockiert – das Gerät steht hier
  nicht zur Verfügung. Die SPK-Installation ist **nicht** auf echter DSM-Hardware
  geprüft.
- Der CI-Workflow ist **noch nicht gelaufen** (kein Tag gepusht). Der
  SPK-Release steht damit aus.
- **8b.2** (Options-Mapping) und **8d.1** (Token-API in der Oberfläche) sind
  Phase 8a–8d.

**Überraschungen:**
- ⚠️ **Das `config.json`-Schema in der Spec war falsch.** Die Spec zeigte
  Kleinbuchstaben-Abschnitte (`bacnet`, `mqtt`, `web`) – das Gateway liest aber
  die .NET-Options (`Bacnet`, `Mqtt`, `Api`). Der Supervisor hätte nichts
  gefunden. **Die Spec wurde korrigiert** (Kapitel §6.1).
- ⚠️ **`postinst` hätte den Container in eine Neustartschleife geschickt.**
  Die alte Startkonfiguration enthielt `"bacnet": { "port": "" }` und
  `"mqtt": { "brokers": [] }` → `ValidateOnStart` schlägt fehl → der Container
  startet nicht → **der Assistent ist nicht erreichbar**. Jetzt schreibt
  `postinst` nur noch `Api:SetupCompleted`; der Rest kommt aus dem Image.
- ⚠️ **MQTTnet 5 hat keine `PayloadSegment`-Property** (nur `get`) – der
  Rundlauf nutzt `ConvertPayloadToString()`. Und `SubscribeAsync` braucht
  `MqttClientSubscribeOptions`, **kein** `MqttApplicationMessage`.
- ⚠️ **`takeUntilDestroyed()` ohne Argument in `ngOnInit` → NG0203.**
  `ngOnInit` ist kein Injektionskontext – der `DestroyRef` muss übergeben
  werden.
- ⚠️ **Optimus UI hält eine per `@if` entfernte `p-message` im DOM stehen**
  (`p-message-leave-active`). In einem Hintergrund-Tab feuert `animationend`
  nie → die Meldung „Kein serieller Port gefunden" bleibt dauerhaft sichtbar,
  obwohl Ports gefunden wurden. **Regel:** `p-message` nicht per `@if`
  entfernen, sondern den Schweregrad umschalten.
- ⚠️ **Der Browser-Tab im VS-Code-Browser ist nicht sichtbar** → Klicks
  (`page.mouse.click`) erzeugen **kein** DOM-Ereignis. Für UI-Tests dort
  `element.click()` per `page.evaluate` verwenden.
- ⚠️ **`pnpm`/`npm`-Skript `tar` liest `C:/…` als Remote-Host** – im SPK-Build
  deshalb immer `cd <dir> && tar …`.

---

## 2026-11-01 – Container & Synology (Aufgaben 6.1–6.3, 6.5, 6.6)

**Ergebnis:** Das Gateway läuft als **ein** Container-Image (Gateway **+** Angular-UI,
261 MB, kein nginx). `docker build` ist grün, der Container startet **healthy**,
die SPA wird aus dem .NET-Host ausgeliefert, `docker stop` beendet ihn in 0,5 s
sauber per SIGTERM (tini) – und der **24-h-Schreibzähler übersteht einen
Container-Neustart** aus dem Volume. Backup und Restore sind **real getestet**.

**Geänderte Dateien (neu):**
- `src/ComfoBox.Gateway/Dockerfile` – 3 Stages (node → sdk → aspnet), tini als PID 1,
  `USER comfobox`, Healthcheck, VOLUME, ICU für die deutsche Formatierung
- `.dockerignore` – Build-Kontext (Repo-Root als Kontext, weil das Image
  .NET-Projekt **und** Angular braucht)
- `deploy/compose/compose.yaml` – der manuelle Betrieb (parametrisiert über `.env`)
- `deploy/compose/compose.test.yaml` – Override **ohne** Serial-Port (Entwicklerrechner)
- `deploy/compose/.env.example` – die Vorlage ohne Secrets
- `deploy/backup/backup.sh` + `restore.sh` + `backup-cron.sh` – Sicherung inkl. Schreibzähler
- `docs/benutzer/08-monitoring.md` – Monitoring, HA-Alarmierung, Backup (Benutzerdoku)

**Geänderte Dateien (bearbeitet):**
- `.github/workflows/docker-publish.yml` – **ein** Image statt zwei, Multi-Arch
  (`linux/amd64,linux/arm64`), Trivy vor dem Push
- `src/.gitignore` + `.dockerignore` – die Laufzeit-Regeln **verankert**
  (`/ComfoBox.Gateway/logs/` statt `logs/`)
- `src/ComfoBox.Web/package-lock.json` – plattformrein neu erzeugt
- `docs/03-deployment/containerisierung.md`, `konfiguration-und-secrets.md`,
  `docs/02-modernisierung/health-und-status.md` – auf den Ist-Stand gebracht

**Verifikation:**
- `docker build -f src/ComfoBox.Gateway/Dockerfile -t comfobox-gateway:local .` → grün
- Image: **261 MB**, `User: comfobox`, `Entrypoint: ["/sbin/tini","--"]`,
  Healthcheck `curl -fsS http://localhost:8080/api/health`, ICU vorhanden,
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`
- **Multi-Arch:** `docker buildx build --platform linux/arm64` → grün (QEMU, ~9 min),
  `uname -m` = `aarch64`, gleiche Eigenschaften (`User`, `tini`, Healthcheck, Volume)
- Trivy-Scan: **0 CRITICAL/HIGH**
- `docker compose up -d` → `Up (healthy)`, `GET /api/health` → `200 {"state":"online"}`
- `GET /` → 200 (1.322 B `index.html`), `GET /config` → 200 (**SPA-Fallback** greift)
- `GET /help/monitoring.html` → 200 (14.132 B) – die neue Seite ist im Bundle
- `docker stop` → **0,52 s**, Log: „Application is shutting down…" → MQTT beendet →
  Polling beendet → Transport gestoppt (Shutdown-Reihenfolge korrekt)
- Schreibzähler: Datei ins Volume gelegt → Container neu gestartet →
  `GET /api/diagnostics/writes` → `{"count":1,…}` → **persistent** ✅
- `docker compose down` (ohne `-v`) → das Volume `comfobox-data` **bleibt** ✅
- Healthcheck bei nicht erreichbarem Broker → Container bleibt **healthy** ✅
- Backup → Zählerdatei gelöscht → Restore → Volume wiederhergestellt → Container neu gestartet ✅
- `node tools/build-docs.mjs` → 12 Seiten, grün
- `pwsh -File tools/check-links.ps1` → 639 Links, **0 Probleme**

**Offen / nicht gemacht:**
- **6.4 (Test auf der DS923+)** – blockiert: die echte Synology und der
  USB-Adapter stehen hier nicht zur Verfügung. Die Prüfliste steht in
  [phase-6.md](phase-6.md); sie muss auf dem NAS abgearbeitet werden.
- Der CI-Workflow wurde auf **ein** Image und Multi-Arch umgestellt, aber
  **nicht ausgeführt** (kein Push auf ein Tag in dieser Session). Der Trivy-Scan
  ist damit noch unverifiziert.

**Überraschungen:**
- ⚠️ **Der Angular-Ordner `features/diagnostics/logs/` war nie im Repository.**
  `src/.gitignore` enthielt `logs/` (ohne Pfadpräfix) – das verschluckt jeden
  Ordner namens `logs`, auch Quellcode. Der CI-Build wäre an derselben Stelle
  gebrochen wie der lokale Docker-Build. **Regel:** Laufzeit-Regeln in
  `.gitignore`/`.dockerignore` immer **verankern** (`/ComfoBox.Gateway/logs/`).
- ⚠️ **`npm ci` scheiterte** – das Lock-File war nicht synchron zu `package.json`
  (`@emnapi/*` in zwei Versionen). Ein Lock-File, das unter Windows mit `npm install`
  erzeugt wurde, ist **nicht** plattformrein. Das Lock-File wurde deshalb im
  Linux-Container neu erzeugt – danach funktionieren **beide** Plattformen.
- ⚠️ **Das Alpine-Basis-Image setzt `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`.**
  Ohne `icu-libs` und ohne explizites `false` bricht die deutsche Formatierung
  in der Oberfläche. Das steht in keinem Dockerfile-Template.
- ⚠️ **`--force-local` ist keine Option für BusyBox-tar** (Alpine, DSM) – und
  GNU tar interpretiert einen Pfad mit Doppelpunkt (`C:/…`) als Remote-Host.
  Die portable Lösung ist `cd <Verzeichnis> && tar xzf <Basisname>`.
- ⚠️ **tar darf die Datei nicht einpacken, die es gerade schreibt** („file changed
  as we read it") – das Archiv gehört in ein **zweites** Verzeichnis.
- ⚠️ Ein temporäres Host-Verzeichnis lässt sich unter Windows **nicht** in den
  Container mounten. Die Backup-Skripte übertragen das Archiv deshalb über
  stdin/stdout statt über einen zweiten Mount.

---

## 2026-11-01 – Serielle Ports zur Laufzeit wählen (Aufgabe 5.13)

**Ergebnis:** Die seriellen Ports werden **gescannt** (Hotplug), der Benutzer
wählt sie in der Oberfläche aus, und die Umstellung ist **sofort** wirksam –
**kein Neustart** des Gateways nötig. Der Hinweis steht prominent auf dem
Dashboard, sobald BACnet nicht verbunden ist, und auf der Konfigurationsseite.
**293 Tests grün** (46 BACnet + 30 Domain + 10 Contract + 207 Gateway).

**Geänderte Dateien (neu – Gateway):**
- `src/ComfoBox.Gateway/Bacnet/SerialPortSettings.cs` – die **laufenden** Parameter (Port + Baudrate)
- `src/ComfoBox.Gateway/Bacnet/GatewaySerialPortFactory.cs` – die Fabrik liest die Parameter bei **jedem** Öffnen
- `src/ComfoBox.Gateway/Bacnet/BacnetSerialManager.cs` – Test + Umstellung (persistieren → Laufzeit → Neuaufbau)
- `src/ComfoBox.Gateway/Hardware/SerialPortScanner.cs` – die Port-Suche (`ports.json`, sonst `GetPortNames()`)
- `src/ComfoBox.Gateway/Api/SerialEndpoints.cs` – `GET /api/config/serial-ports`, `POST /api/config/bacnet/test`, `PUT /api/config/bacnet`

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Bacnet/Bacnet/BacnetMstpTransport.cs` – `RequestReconnect()` bricht den Backoff **ab** (Semaphore-Signal)
- `src/ComfoBox.Bacnet/Serial/ISerialPort.cs` + `SerialPortAdapter.cs` – `PortName` (nur Diagnose)
- `src/ComfoBox.Gateway/GatewayServiceCollectionExtensions.cs` – `SerialPortSettings` + die neue Fabrik
- `src/ComfoBox.Gateway/Api/GatewayApiExtensions.cs` – Scanner + Manager registriert, `MapSerial()`
- `src/ComfoBox.Gateway/Api/StatusEndpoints.cs` + `ConfigEndpoints.cs` – melden den **laufenden** Port
- `src/ComfoBox.Gateway/Setup/SetupService.cs` – nutzt jetzt den Scanner (keine Dopplung)

**Geänderte Dateien (neu – Web):**
- `src/ComfoBox.Web/src/app/core/api/serial.service.ts` – der API-Zugriff
- `src/ComfoBox.Web/src/app/shared/serial-port-picker.ts` + `.html` + `.scss` – die Port-Auswahl (wiederverwendbar)

**Geänderte Dateien (bearbeitet – Web):**
- `src/ComfoBox.Web/src/app/features/config/config.html` + `config.ts` – Port-Auswahl in der BACnet-Karte
- `src/ComfoBox.Web/src/app/features/dashboard/dashboard.html` + `dashboard.ts` + `dashboard.scss` – prominenter Hinweis

**Tests (neu):**
- `src/ComfoBox.Gateway.Tests/Api/SerialApiTests.cs` – 6 Tests (Portliste, Umstellung ohne Neustart, Status, ungültige Werte, Test)
- `src/ComfoBox.Bacnet.Tests/SerialPortBehaviourTests.cs` – `Ein_Angeforderter_Neuaufbau_Ignoriert_Den_Backoff`

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **293 Tests grün, 0 Fehler**
- `npm run lint` (src/ComfoBox.Web) → grün
- `npm run build` (src/ComfoBox.Web) → Produktions-Build erfolgreich
- **Live am Gateway** (Port 5199): `PUT /api/config/bacnet {"port":"COM7","baudrate":9600}` → **204**,
  danach `/api/status` meldet `port: "COM7"` – **ohne Neustart** (der Log zeigt
  den Neuaufbau in derselben Sekunde).

**Offen / nicht gemacht:**
- Der Setup-Assistent nutzt weiterhin seine eigene Port-Auswahl (Schritt 1) –
  er schreibt die Konfiguration am Ende ohnehin mit Neustart. Die neue
  Komponente ist bewusst **nicht** dort eingesetzt, um den Assistenten nicht
  zu verkomplizieren.
- Die SPA-Auslieferung (`wwwroot/`) ist unabhängig davon weiterhin offen
  (Aufgabe 5.8) – der Docker-Stage fehlt ebenfalls (Phase 6).

**Überraschungen:**
- ⚠️ **Ein Portwechsel braucht zwei Dinge**, nicht eines: (a) die
  `SerialPortSettings` (der Transport liest sie erst beim **Öffnen** des
  Ports) und (b) das **Abbruch** des Backoffs. Ohne (b) wartet der Transport
  bis zu 30 Sekunden, bevor er den neuen Port überhaupt versucht – der
  Benutzer hätte „nichts passiert" gedacht.
- ⚠️ **Der aktive Port lässt sich nicht testen**, indem man ihn öffnet – der
  Gateway hält ihn selbst. `BacnetSerialManager.Test()` erkennt den aktiven
  Port und meldet stattdessen den **Buszustand** (über `BacnetDevice`).
  Der Health-Reporter ist dafür zu optimistisch: er meldet „online", obwohl
  die Heizung gar nicht antwortet.
- ⚠️ **`FakeSerialPort` musste `IsOpen` per Default auf `true` setzen** und
  bei geschlossenem Port `SerialReadResult.Closed` liefern – sonst erkennt
  der MS/TP-Automat keinen Portfehler und der A8-Regressionstest wird
  wirkungslos.

---

## 2026-10-31 – Phase 3 abgeschlossen: MQTT-Migration & HA-Discovery (Aufgaben 3.1–3.6)

**Ergebnis:** Der Gateway publiziert alle **127 Topics**, abonniert die
`/Set`-Topics, meldet sich bei Home Assistant an und bleibt MQTT-seitig
stabil. **219 Tests grün** (45 BACnet + 30 Domain + 10 Contract + 134 Gateway).
Die Golden-Fixture und `docs/topics.json` sind **byteweise unverändert**.
M2Mqtt ist **vollständig** aus `src/` verschwunden.

**Geänderte Dateien (neu – Gateway/Mqtt):**
- `src/ComfoBox.Gateway/Mqtt/MqttPayload.cs` – kultur-invariante Zahlen, Enum-Parser **ohne** 0-Fallback (A5, A6)
- `src/ComfoBox.Gateway/Mqtt/MqttTopicMap.cs` – die **einzige** Stelle, an der Topic-Adressen gebildet werden
- `src/ComfoBox.Gateway/Mqtt/MqttConnectionFactory.cs` – Optionen, TLS, Auth, Keepalive, **zwei** LWT-Rollen
- `src/ComfoBox.Gateway/Mqtt/MqttConnectionManager.cs` – Connect/Reconnect/Backoff/Shutdown je Broker
- `src/ComfoBox.Gateway/Mqtt/BrokerConnection.cs` – die Verbindung **eines** Brokers (Haupt- + Will-Verbindung)
- `src/ComfoBox.Gateway/Mqtt/MqttClientRegistry.cs` – Verzeichnis der verbundenen Clients (bricht den DI-Kreis)
- `src/ComfoBox.Gateway/Mqtt/MqttPublisher.cs` – Publish mit QoS/Retain, Fehler pro Broker isoliert
- `src/ComfoBox.Gateway/Mqtt/MqttSubscriber.cs` – `/Set` + `/AsNumber/Set`, Schreiben **nur** über `BacnetDevice`
- `src/ComfoBox.Gateway/Mqtt/MqttStatePublisher.cs` – Item-Werte auf ihre Topics (aus dem Wert-Cache)
- `src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs` – Discovery für alle 127 Items + Gerät + Status
- `src/ComfoBox.Gateway/Mqtt/HomeAssistantBirthHandler.cs` – reagiert auf die Geburt von Home Assistant (`homeassistant/status` = `online`)
- `src/ComfoBox.Gateway/Mqtt/MqttStatusPublisher.cs` – Health-/Status-Topics (online/degraded/offline)
- `src/ComfoBox.Gateway/Mqtt/MqttVersion.cs` – Version (`Ver 1.0.0` / `1.0.0`)
- `src/ComfoBox.Gateway/Mqtt/IMqttPublisher.cs`, `IMqttConnectionHandler.cs` – die Naahte
- `src/ComfoBox.Gateway/GatewayServiceCollectionExtensions.cs` – die gesamte DI-Registrierung (aus `Program.cs` herausgezogen)

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Program.cs` – nur noch Logging + `AddGatewayServices` + `/api/health`
- `src/ComfoBox.Gateway/Configuration/MqttOptions.cs` – `DiscoveryPrefix`, `UseMqttRetain`, `StatusIntervalSeconds`, `AllowUntrustedCertificates`, Client-ID mit Hostname
- `src/ComfoBox.Gateway/Health/HealthState.cs` + `HealthReporter.cs` – Health-Aggregation über die Komponenten
- `src/ComfoBox.Gateway/Health/ErrorCollector.cs` – Ereignis `Reported` (Status-Topic reagiert sofort)
- `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` – meldet den BACnet-Zustand an den Health-Reporter
- `src/ComfoBox.Gateway/appsettings.json` – neuer `Mqtt`-Abschnitt
- `src/ComfoBox.Gateway/ComfoBox.Gateway.csproj` – `InternalsVisibleTo` für die Tests

**Geänderte Dateien (neu – Tests):**
- `src/ComfoBox.Gateway.Tests/MqttPayloadTests.cs` – A5 + A6 (de-DE-Kultur)
- `src/ComfoBox.Gateway.Tests/MqttTopicMapTests.cs` – Topic-Vertrag gegen `docs/topics.json`
- `src/ComfoBox.Gateway.Tests/MqttSubscriberTests.cs` – Schreibpfad **inkl.** Schreibzähler
- `src/ComfoBox.Gateway.Tests/HomeAssistantDiscoveryTests.cs` – Discovery-Schema
- `src/ComfoBox.Gateway.Tests/MqttConnectionIntegrationTests.cs` – **echter** Broker im Prozess, A10-Regression
- `src/ComfoBox.Gateway.Tests/GatewayEndToEndTests.cs` – der komplette Gateway gegen einen Broker
- `src/ComfoBox.Gateway.Tests/HealthAndStatusTests.cs`, `NoopMqttClient.cs`, `RecordingMqttPublisher.cs`
- `src/ComfoBox.Gateway.Tests/HomeAssistantBirthHandlerTests.cs` – die Geburt von Home Assistant
- `src/ComfoBox.Gateway.Tests/HomeAssistantBirthHandlerTests.cs` – die Geburt von Home Assistant

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **219 Tests grün, 0 Fehler**
  (45 BACnet + 30 Domain + 10 Contract + 134 Gateway; 143 aus Phase 2 unverändert grün)
- Golden-Fixture **byte-identisch**:
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B` (5.657 B)
- `docs/topics.json` **byte-identisch**:
  `67D7485BF961B2B6D11BA5E788932056F073B564F52922D2DE8B34DFC10C1463` (33.845 B)
- `git diff --stat` auf beide Fixtures → **leer**
- **3.1** geprüft: kein `M2Mqtt`/`uPLibrary` in `src/**/*.cs` und `*.csproj`
- **3.3** geprüft: kein `System.Diagnostics.Trace`, kein `Console.WriteLine` in `src/**/*.cs`
- **A10** geprüft: `Connected_Wird_Nach_Jedem_Reconnect_Publiziert` (Broker-Neustart im Test)
- **A6** geprüft: `Eine_Ungueltige_Enum_Payload_Schreibt_Keinen_Null_Wert`

**Offen / nicht gemacht:**
- Der **Neustart-Befehl** (`ComfoBox/Command/Restart`) ist noch nicht implementiert –
  er gehört zu Phase 4 (Aufgabe 4.5 `ServiceControl`). Der Topic ist reserviert
  (`MqttTopicMap.RestartCommandTopic`), wird aber noch nicht abonniert.
- Die **Discovery auf die HA-Geburt** (`homeassistant/status` → `online`) ist
  implementiert: `HomeAssistantBirthHandler` abonniert das Topic und wiederholt
  Discovery **und** Zustände.
- Die **eingebettete Discovery der `AsNumber`-Variante** fehlt bewusst –
  die Spec empfiehlt sie nicht per Default (sonst verdoppelt sich die
  Entitätenzahl).
- Die **MQTT-Sicherheit** (TLS-Zertifikate, `RestartToken`) ist noch offen –
  siehe ADR-002/ADR-009.
- **Kein Realtest** gegen eine echte ComfoBox und keinen echten Broker
  (Mosquitto): die Tests laufen gegen den **eingebetteten** MQTTnet-Broker.

**Überraschungen:**
- ⚠️ **MQTT 5 erlaubt nur EINEN Last Will pro Verbindung.** Der Vertrag
  verlangt aber zwei (`Status/Health` **und** `Connected`). Lösung: der
  Gateway hält je Broker **zwei** Verbindungen – die Hauptverbindung (Will auf
  `Status/Health`) und eine reine Will-Verbindung (Will auf `Connected`, sonst
  nichts). Die Will-Verbindung bekommt die Client-ID `{ClientId}-will`.
- ⚠️ **Ein zirkulärer DI-Abhängigkeitspfad hat den Testhost blockiert**
  (nicht abgestürzt – 21 Threads, 0 CPU): `MqttConnectionManager` →
  `IMqttConnectionHandler` → `MqttSubscriber` → `IMqttPublisher` →
  `MqttPublisher` → `IMqttClientProvider` → `MqttConnectionManager`. MS-DI
  erkennt den Kreis **nicht** (der Provider war ein `Func`-Factory), sondern
  blockiert beim Auflösen. **Fix:** `MqttClientRegistry` als eigenes Singleton;
  der Manager pflegt es, der Publisher liest es. → **Merksatz:** Der
  Connection-Manager darf **nie** der `IMqttClientProvider` sein, wenn er
  Handler injiziert bekommt.
- ⚠️ **`dotnet test` braucht bei einem hängenden Testhost einen
  `dotnet-dump collect --process-id <PID> --type Full`** – der Standard-Output
  zeigt nur „Der Testhostprozess ist abgestürzt". Mit `clrstack -all` im Dump
  sieht man den blockierenden Frame. (Tool war schon global installiert.)
- ⚠️ **`Host.CreateDefaultBuilder()` + `ConfigureServices(c => c.AddSingleton(services))`
  registriert den Container als Singleton** – die eigenen Dienste fehlen dann
  („No service for type … has been registered"). Der richtige Weg:
  `new HostBuilder().ConfigureServices((_, c) => c.AddGatewayServices(...))`.
- ⚠️ **`dotnet-dump analyze` kennt die SOS-Kommandos nicht** – `set threads` und
  `~*s` schlagen fehl. Richtig sind `clrthreads` und `clrstack -all`.
- ⚠️ **Ein Test, der den echten `BacnetMstpTransport` startet, blockiert** –
  `SerialPort.Open()` auf einem nicht existierenden Port wirft, der
  Reconnect-Backoff ist 5 s, aber `BacnetMstpTransport.Dispose()` ruft
  `StopAsync().GetAwaiter().GetResult()` → **blockierendes** Dispose. Für
  End-to-End-Tests den `IBacnetClient` per `AddSingleton<IBacnetClient,
  FakeBacnetClient>()` überschreiben.
- ⚠️ **Ein Test, der per `Assert.True(Has(topic))` prüft, muss `topic=payload`
  vergleichen** – die Discovery-Payload ist JSON, `Has(topic)` findet sie nie.
- ⚠️ **`MqttApplicationMessage.PayloadSegment` hat keinen öffentlichen
  Getter** – für die Payload im Test `ConvertPayloadToString()` nehmen.
- ⚠️ **`MqttClientOptionsBuilder.WithTlsOptions` erwartet
  `Func<MqttClientCertificateValidationEventArgs, bool>`** – nicht vier
  Argumente (das war die API von MQTTnet 4).
- ⚠️ **`MqttClientSubscribeOptionsBuilder.WithTopicFilter` nimmt **einen**
  Filter, keine Liste** – im Aufruf auflösen, sonst findet der Overload
  `WithTopicFilter(string)` nicht statt.
- ⚠️ **`MqttClientOptions` hat kein `WithLastWillTopic` für zwei Wills** –
  s. o.
- ⚠️ **`MqttClientFactory.CreateMqttClient()` ist statisch** und existiert
  auch ohne Argument; `MqttServerFactory` (Broker) liegt im transitiven Paket
  `MQTTnet.Server` – das kommt über `MQTTnet.AspNetCore` mit.
- ⚠️ **`MqttApplicationMessageReceivedEventArgs` hat einen 4-Parameter-Konstruktor**
  `(clientId, message, publishPacket, autoAcknowledge)` – für einen Test-Client
  `null!` für die letzten beiden.
- ⚠️ **Die Legacy-Datei `src_old/ComfoBoxMqtt/ComfoBoxMqttClient.cs` benutzt
  `keepAlivePeriod = 2`** (Befund A24) – der neue Wert ist 30 s, wie in der Spec.

---

## 2026-10-31 – Phase 1 abgeschlossen: BACnet-Stack läuft async auf .NET 10 (Aufgaben 1.4–1.8)

**Ergebnis:** Der BACnet-Stack ist vollständig portiert. `BacnetClient` arbeitet
mit `async`/`await` und typisierten Exceptions, der MS/TP-Transport verwaltet
Port, Reconnect und Backoff, der Zustandsautomat läuft 1:1 wie im Legacy –
ohne dessen Spin-Loop. 45 Tests grün (26 Golden + 19 neu), die Golden-Fixture
ist **byteweise unverändert**. Für die Portierung war **kein Docker und keine
ComfoBox/kein Serial-Adapter** nötig: alle Tests laufen offline gegen
In-Memory-Attrappen.

**Geänderte Dateien (neu):**
- `src/ComfoBox.Bacnet/Serial/ISerialPort.cs` – schmale Port-Schnittstelle
- `src/ComfoBox.Bacnet/Serial/SerialReadResult.cs` + `SerialReadStatus.cs` – Lesergebnis
- `src/ComfoBox.Bacnet/Serial/BacnetSerialOptions.cs` – Portname, Baudrate, Timeouts
- `src/ComfoBox.Bacnet/Serial/SerialPortAdapter.cs` – Adapter auf `System.IO.Ports.SerialPort`
- `src/ComfoBox.Bacnet/Serial/ISerialPortFactory.cs` + `SerialPortFactory.cs` – Testnaht
- `src/ComfoBox.Bacnet/Bacnet/BacnetMstpOptions.cs` – Station, MaxMaster, Reconnect-Backoff
- `src/ComfoBox.Bacnet/Bacnet/MstpStateMachine.cs` – **1:1-Port** des Legacy-Automaten
- `src/ComfoBox.Bacnet/Bacnet/MstpOutboundFrame.cs` – Frame in der Sendewarteschlange
- `src/ComfoBox.Bacnet/Bacnet/MstpState.cs` – Zustände (Legacy-Namen + `Fatal`)
- `src/ComfoBox.Bacnet/Bacnet/IBacnetTransport.cs` – Transport-Interface (nur MS/TP)
- `src/ComfoBox.Bacnet/Bacnet/BacnetMstpTransport.cs` – Port, Reconnect, Backoff
- `src/ComfoBox.Bacnet/Bacnet/BacnetFrameReceivedEventArgs.cs` – Frame-Ereignis
- `src/ComfoBox.Bacnet/Bacnet/BacnetClient.cs` – **async** Client
- `src/ComfoBox.Bacnet/Bacnet/BacnetExceptions.cs` – typisierte Fehler
- `src/ComfoBox.Bacnet/Bacnet/BacnetIamEventArgs.cs` – IAm-Ereignis
- `src/ComfoBox.Bacnet.Tests/FakeSerialPort.cs` + `FakeSerialPortFactory.cs`
- `src/ComfoBox.Bacnet.Tests/FakeTransport.cs` – Transport-Attrappe
- `src/ComfoBox.Bacnet.Tests/MstpFramingTests.cs` – Framing + Zustandsautomat
- `src/ComfoBox.Bacnet.Tests/SerialPortBehaviourTests.cs` – **A8-Regression**
- `src/ComfoBox.Bacnet.Tests/BacnetClientTests.cs` – Request/Antwort/Fehler

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Bacnet/ComfoBox.Bacnet.csproj` – `InternalsVisibleTo` für die Tests
- `src/ComfoBox.Bacnet/Serial/SerialPortFactory.cs` – jetzt `ISerialPortFactory`

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **45 Tests grün, 0 Fehler**
  (26 Golden-Tests unverändert + 19 neue Tests)
- Fixture-Prüfsumme **unverändert**:
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B` (5.657 Bytes)
- **1.6** geprüft: keine Treffer für `kernel32`, `UdpClient`, `Socket`, `PipeStream`,
  `BacnetIpUdpProtocolTransport`, `BacnetPtpProtocolTransport` in `src/**/*.cs`
- **1.7** geprüft: kein `Trace.` und kein `Console.Write` in `src/**/*.cs`
- **A23** geprüft: kein `ThreadPriority.Highest` im neuen Code

**Offen / nicht gemacht:**
- Der **Gateway-Anschluss** fehlt noch: der neue `BacnetClient` ist nicht in
  `ComfoBox.Gateway` eingebunden (das ist Aufgabe 2.3/2.4 der Phase 2).
- **Segmentierte Antworten** werden zusammengesetzt, aber nicht vollständig
  gegen die SegmentAck-Semantik des Legacy geprüft (Realtest braucht Hardware).
- Der Legacy unterstützt **ReadPropertyMultiple**, **ReadRange**, **File**,
  **SubscribeCOV** – diese Dienste sind **nicht** portiert. Der ComfoBox nutzt
  laut `src_old/ComfoBoxLib` nur `ReadProperty` und `WriteProperty`.
- **Kein Realtest** gegen die ComfoBox (kein Serial-Adapter an dieser Maschine).

**Überraschungen:**
- ⚠️ **`create_file` überschreibt keine vorhandene Datei** – der Versuch,
  `SerialPortFactory.cs` neu anzulegen, schlug mit „File already exists" fehl.
  Lösung: Datei erst löschen (`Remove-Item`) oder mit `replace_string_in_file`
  bearbeiten. **Vorher im Repo prüfen, ob die Datei schon existiert.**
- ⚠️ **Die Antwort-Zuordnung darf nicht über die Adresse laufen.** Der erste
  Entwurf hat die wartende Anfrage über `(Invoke-ID, MAC-Adresse)` identifiziert.
  Bei Broadcast-Antworten (IAm) passt die Absenderadresse aber nicht zur
  Zieladresse – die Anfrage lief ins Timeout. Der Legacy ordnet **nur über die
  Invoke-ID** zu; das ist jetzt so übernommen (`ConcurrentDictionary<byte, …>`).
- ⚠️ **`MSTP.Encode` schreibt die Daten-CRC *hinter* die Nutzdaten.** Wer den
  Frame baut, muss die Nutzdaten **vor** dem Aufruf in den Puffer kopieren –
  sonst überschreibt `Encode` sie mit der CRC. Zwei Tests sind daran initially
  gescheitert.
- ⚠️ **Der Zustandsautomat verschluckt Frames in der Poll-For-Master-Runde.**
  Ein Frame, der nach dem Start des Automaten eintrifft, landet in der
  PFM-Runde und wird verworfen. Für Tests muss der Frame **vor** dem Start
  bereitliegen.
- Der Legacy wartete 250 ms (`T_REPLY_DELAY`) auf eine mögliche Antwort, bevor
  er „Reply Postponed" sendete. Da das Gateway ein reiner Client ist, wird
  „Reply Postponed" sofort gesendet – **das ist eine Abweichung vom Legacy**
  (dokumentiert in `MstpStateMachine`).
- Der Legacy hatte **keinen** `ThreadPriority.Highest`-Ersatz im neuen Code –
  der Transport nutzt `TaskCreationOptions.LongRunning` (eigener Thread,
  Standardpriorität).
- `dotnet test` auf der Solution meldet für `ComfoBox.Domain.Tests`,
  `ComfoBox.Contract.Tests` und `ComfoBox.Gateway.Tests` „kein Test verfügbar" –
  das ist **vorbestehend** (diese Projekte enthalten noch keine Tests) und
  kein Fehler dieser Session.

---

## 2026-10-31 – Phase 1: BACnet-Codec portiert, Golden-Fixture erzeugt (Aufgaben 1.1–1.3, 1.6, 1.7)

**Ergebnis:** Der BACnet-Codec läuft verhaltensgleich auf .NET 10. 25 Golden-Frames,
erzeugt vom **Legacy-Codec selbst**, werden vom portierten Codec byteweise
reproduziert – 26 Tests grün. Der Codec liegt als
`src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` vor (Fremdcode, MIT, 1:1 portiert).

**Geänderte Dateien:**
- `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` – **neu**: 1:1-Port von `BACnetBase.cs`
  (7.471 Zeilen), Namespace `ComfoBox.Bacnet`, ohne `BVLC` (BACnet/IP) und `PTP`
- `src/ComfoBox.Bacnet/Diagnostics/BacnetLog.cs` – **neu**: `ILogger`-Fassade für den
  statischen Fremdcode (Befund A16)
- `src/ComfoBox.Bacnet/Bacnet/HEADER-LIZENZ.txt` – **neu**: MIT-Lizenz + Portierungsprotokoll
- `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json` – **neu**: 25 Golden-Frames
- `src/ComfoBox.Bacnet.Tests/BacnetCodecGoldenTests.cs` + `BacnetFrames.cs` – **neu**
- `tools/port-bacnet/port-bacnet-base.mjs` – **neu**: mechanischer Port
- `tools/port-bacnet/oracle/` – **neu**: Referenz-Oracle (kompiliert den Legacy-Codec)
- `tools/port-bacnet/README.md` – **neu**
- `src/ComfoBox.Bacnet/ComfoBox.Bacnet.csproj` – `NoWarn` + `EnableNETAnalyzers=false`
  für den Fremdcode

**Verifikation:**
- `cd src && dotnet build ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- `cd src && dotnet test ComfoBox.sln -c Release` → **26 Tests grün** (1 Projekt, 25 Frames)
- Jeder Frame wird dekodiert und mit dem Erwartungswert der Fixture verglichen
- Die Adress-Frames werden **erneut kodiert** und byteweise verglichen

**Offen / nicht gemacht:**
- **1.4 `BACnetClient`** (2.098 Zeilen) – offen
- **1.5 `BACnetTransport`** (2.005 Zeilen, MSTP + Serial) – offen
- **1.8** Timeout-/Reconnect-Härtung – offen (setzt 1.4/1.5 voraus)
- 1.6 und 1.7 sind nur für den **Codec** erledigt; die Transports (Pipe/UDP/PTP)
  und deren `Trace`-Aufrufe folgen mit 1.4/1.5.

**Überraschungen:**
- ⚠️ **`docs/Mono/MoMA Scan Results.html` enthält KEINE BACnet-Frames.** Die Datei ist
  ein **Mono-Missing-Analysis-Report** (welche BCL-Methoden in Mono fehlen):
  0 Hex-Dumps, keine „I-Am", kein „NPDU", keine „Who-Is".
  Prüfsumme des Originals: `012C9862185C83B6E3432BDABD3C8C39F5EC57884CD837F92E399BFB2C14822F`
  (unverändert). → **Die Prämisse der Aufgabe 1.1 war falsch.** Die Golden-Frames
  stammen stattdessen vom Legacy-Codec selbst (Referenz-Oracle).
- Der Legacy-Codec ist **nicht** standalone: `BACnetBase.cs` referenziert
  `BacnetIpUdpProtocolTransport` aus `BACnetTransport.cs` (nur für den
  BACnet/IP-BVLC-Code). Für den Oracle genügt ein Stub.
- `Services.DecodeIamBroadcast` gibt bei Erfolg **0** zurück (`return offset - org_offset`),
  nicht die Länge – eine Legacy-Eigenheit, die der Golden-Test festschreibt.
- Der Legacy-Codec braucht **7.500 Zeilen** `#nullable disable` – mit
  `Nullable=enable` + `TreatWarningsAsErrors` sind das ~40 Fehler.
- Die .NET-Analyzer monieren im Fremdcode bewusste Eigenheiten des BACnet-Standards
  (CA1707 Unterstriche, CA1069 doppelte Enum-Werte, CA1051 sichtbare Felder,
  CA1711 Namenskonvention) → für das Projekt deaktiviert.
- `CA1707` moniert auch **Testmethodennamen** mit Unterstrichen – die sind hier
  Haustil (vgl. `csharp.instructions.md`) → `NoWarn` in den Testprojekten.
- Der Legacy-Codec liest über den Puffer hinaus (`ASN1.decode_*` ohne Längenprüfung):
  der Oracle hat deshalb einen 1-KB-Puffer, der Test exakt 12 Byte. Ergebnis gleich –
  aber der Test ist strenger.

---

## 2026-10-31 – Phase 0: Repo-Hygiene & CI (Aufgaben 0.1–0.8)

**Ergebnis:** Das Fundament steht. `src/` enthält eine gepinnte .NET-10-Solution
mit sieben SDK-Projekten, zentrale Paketverwaltung, Qualitäts-Gate
(`TreatWarningsAsErrors`), Lock-Dateien, CI mit Build/Test/Fixture-Prüfung und
einen `CHANGELOG.md`. Der Generator für die Topic-Fixture reproduziert
`docs/topics.json` **byteidentisch** (127 Items) – die Fixture ist damit
maschinell abgesichert.

**Geänderte Dateien:**
- `src/global.json` – SDK-Pinning 10.0.400 (`rollForward: latestFeature`)
- `src/Directory.Packages.props` – 20 Pakete zentral gepinnt
  (`CentralPackageTransitivePinningEnabled`)
- `src/Directory.Build.props` – `net10.0`, C# 14, `Nullable`, `TreatWarningsAsErrors`,
  `GenerateDocumentationFile`, `RestorePackagesWithLockFile`
- `src/ComfoBox.sln` + 7 `.csproj` – Domain/Bacnet/Gateway + 4 Testprojekte
- `CHANGELOG.md` – neu
- `tools/generate-topics/generate-topics.mjs` + `README.md` – Generator & Prüfmodus
- `.github/workflows/build-test.yml` – `working-directory: src`, `global-json-file`,
  Lock-Dateien, Job „Fixture prüfen", Legacy-Job → Archiv-Prüfung

**Verifikation:**
- `cd src && dotnet --version` → `10.0.400`
- `cd src && dotnet build ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- `cd src && dotnet test ComfoBox.sln -c Release` → grün (0 Tests, erlaubt)
- `node tools/generate-topics/generate-topics.mjs --check` → „Fixture ist deckungsgleich (127 Items)"
- Regenerierte Datei vs. committete Fixture → identisch bis auf **eine** dokumentierte
  Abweichung (`ComfoBox/Warmwater/DoHeatWaterNow`: `DateTime?` → `float?`, 3 Zeichen)
- `ComfoBox.Domain` → keine `PackageReference` (framework-frei)
- Projektreferenzen: Bacnet → Domain, Gateway → Domain + Bacnet, keine Zyklen
- Kein `<PackageReference>` mit `Version=`-Attribut → kein `NU1008`
- `Class1.cs` / `UnitTest1.cs` entfernt → kein `CS1591`

**Offen / nicht gemacht:**
- Die CI-Pipeline selbst wurde **nicht** ausgeführt (kein Push in dieser Session) –
  der Workflow ist aber gegen die lokalen Kommandos verifiziert.
- `packages.lock.json` sind generiert, aber noch nicht committet.

**Überraschungen:**
- `dotnet new web` liefert im .NET-10-SDK Pakete, die der Web-SDK bereits mitbringt →
  `NU1510` (Warnung als Fehler). Betroffen: `Microsoft.Extensions.Hosting`,
  `Microsoft.Extensions.Configuration.Json`, `…EnvironmentVariables` → entfernt.
- `dotnet new sln` erzeugt standardmäßig **`.slnx`** → `--format sln` ist Pflicht.
- Die Template-`PackageReference` enthalten `Version=` → `NU1008` mit CPM.
- `Swashbuckle.AspNetCore 9.0.6` existiert (die Spec nennt „9.x").
- **Die Legacy-Dateien sind Windows-1252-kodiert**, nicht UTF-8 – ein UTF-8-Leser
  erzeugt `�` in Umlauten. Der Generator dekodiert sie mit einer eigenen CP1252-Tabelle.
- `\w` in JavaScript matcht **nur ASCII** – Bezeichner wie `Gebäudeträgheit` brauchen
  `\p{L}` mit dem `u`-Flag.
- In `Config.cs` stehen **auskommentierte** Items (`States/Outputs/R2`, `R6`, `R8`, `R9`) –
  ein Parser ohne Kommentar-Stripping zählt sie mit.
- `docs/topics.json` hat ein **UTF-8-BOM** → `JSON.parse` braucht vorher ein `.replace(/^﻿/, '')`.
- Die Fixture stammt von 2017 und ist seitdem **nicht** mehr deckungsgleich mit dem
  Legacy-Katalog (ein Werttyp, vier auskommentierte Items). Die Fixture bleibt
  unverändert – sie ist der Vertrag.

---

## 2026-10-31 – Spezifikation, SPK-Skelett, HA-Ökosystem, Benutzerdoku (Phasen 0–8d, D.1–D.8)

**Ergebnis:** Die gesamte Spezifikation liegt vor. `src_old/` ist vom Altbestand getrennt,
das SPK baut, die HA-Integration und das Add-on sind als Skelett vorhanden, die
Benutzerdoku erzeugt HTML.

**Geänderte Dateien:**
- `src/` → `src_old/` verschoben (`git mv`, 100 Dateien, Historie erhalten)
- `src/README.md` + `src/.gitignore` – Zielstruktur der Neuzuimplementierung
- `docs/` – 10 neue Dokumente (ADRs 007–010, Specs, Installationsstufen)
- `deploy/synology-spk/` – SPK-Skelett, alle 9 Shell-Skripte `bash -n`-geprüft
- `deploy/ha-addon/` – Add-on-Manifest mit `device(subsystem=tty)`, `ingress`, `watchdog`
- `custom_components/comfobox/` – 11 Python-Module + Tests
- `hacs.json` – HACS-Metadaten
- `docs/benutzer/` – 11 Seiten mit Front-Matter
- `tools/build-docs.mjs` – Markdown → HTML mit Link-Umschreibung
- `tools/check-links.ps1` – Link-/Anker-Validator (437 Links, 0 Fehler)
- **Indizes**: `docs/README.md`, `docs/02-modernisierung/README.md`,
  `docs/05-entscheidungen/README.md` um `06-aufgaben`, `docs/benutzer/` und ADR-010 ergänzt
- `.github/instructions/docs.instructions.md` – neue Abschnitte „Benutzerdokumentation"
  (Front-Matter, Tonfall) und „Aufgaben-Dokument" (Statuslogik)
- `docs/02-modernisierung/ui-angular-optimus.md` – neuer §13 „Hilfe-Bereich" (Route `/help`,
  öffentlich ohne Anmeldung, Suchindex, Sicherheits-Betrachtung)
- `src/README.md` – Verweise auf Benutzerdoku, Doku-Build und Aufgaben-Dokument
- `.gitignore` (Root) – Python-Regeln ergänzt (`__pycache__/` u. a.)

**Verifikation:**
- `bash -n` über alle 9 SPK-Skripte → grün
- `build-spk.sh` → `ComfoBoxGateway_noarch-dsm7_1.0.0-0001.spk`
- `check-links.ps1` → 84 Dateien, 534 Links, 0 Probleme
- `build-docs.mjs` → 11 HTML-Seiten + `index.json`
- `git check-ignore -v` → `.env`, `appsettings.override.json`, `*.user`, `node_modules/`,
  `dist/`, `__pycache__/` werden ignoriert; `git ls-files deploy/synology-spk/src/target/bin/`
  → 3 `.sh`-Dateien (Aufgabe **0.5** damit verifiziert)

**Offen / nicht gemacht:**
- **Kein Python auf der Maschine** → die HA-Integration wurde **manuell reviewed**,
  aber **nicht** von `pytest`/`ruff` verifiziert. Drei echte Fehler wurden dabei
  gefunden und behoben (unausgewerteter `await` beim Service-Setup, fehlerhafte
  Content-Type-Prüfung, `config_entry`-Zugriff im Unique-ID).
- **Kein Docker-Daemon** → die Container-Aufgaben sind nicht verifizierbar.
- Die **Solution** (`src/ComfoBox.sln`) existiert noch nicht → Aufgabe 0.4.
- Die **Golden-Tests** existieren noch nicht → Aufgabe 1.2 ist die **Voraussetzung**
  für die gesamte Portierung.

**Überraschungen:**
- `git mv src src_old` scheitert an einem gesperrten Verzeichnis (`ComfoBoxMqtt`) –
  Windows-Handle. Workaround: Unterverzeichnisse einzeln verschieben.
- Das Root-`.gitignore` hat `[Bb]in/` und hätte `deploy/synology-spk/src/target/bin/*.sh`
  ignoriert → Ausnahme in `deploy/synology-spk/.gitignore` ergänzt.
- Die Benutzerdoku-Links mussten **im HTML** auf `.html` umgeschrieben werden –
  der Build erledigt das jetzt automatisch.
- **GitHub-Anker-Regel:** jedes Leerzeichen → **ein** Bindestrich, Whitespace-Runs bleiben.
  „## 4.7 – Setup-API (Getting Started) · 1.5 PT“ → `#47--setup-api-getting-started--15-pt`.
  Der Validator bildet das jetzt genauso ab; vier Anker im Aufgaben-Dokument waren falsch.
- `docs/benutzer/03-Installation.md` → `03-installation.md` (Kleinschreibung konsistent).

---

## 2026-10-31 – Python-Toolchain, HA-Tests grün, Logging-Spec (D.9, Aufgabe 8a.6)

**Ergebnis:** Python 3.12 ist installiert, die HA-Integrationstests laufen grün (3/3),
`ruff` ist sauber. Die Logging-Entscheidung (Serilog + In-Memory-Ringbuffer mit
1000 Einträgen für den Log-Viewer) ist als Spec + ADR dokumentiert.

**Geänderte Dateien:**
- `pytest.ini` (**neu**) – `asyncio_mode = auto`, `asyncio_default_fixture_loop_scope`,
  `testpaths` auf `custom_components/comfobox/tests`
- `custom_components/comfobox/tests/conftest.py` – zwei Windows-Workarounds (siehe unten)
- `docs/02-modernisierung/logging.md` (**neu**) – Serilog, Ringpuffer, API, Log-Viewer
- `docs/05-entscheidungen/adr-011-logging-serilog-und-ringbuffer.md` (**neu**)
- `docs/02-modernisierung/dotnet-migration.md` – §5 um Ringpuffer-Sink erweitert
- `docs/02-modernisierung/ui-angular-optimus.md` – §7.1 Log-Viewer, DoD +2
- `docs/02-modernisierung/datenmodell-und-api.md` – 3 Log-Endpunkte
- `docs/02-modernisierung/health-und-status.md` – Abgrenzung Fehler- vs. Log-Puffer
- `docs/03-deployment/konfiguration-und-secrets.md` – `Logging:RingBuffer:*`
- `docs/06-aufgaben/phase-2.md` – **2.7** Serilog + Ringpuffer (1.0 PT)
- `docs/06-aufgaben/phase-4.md` – **4.9** Log-Endpunkte + SSE-Tail (0.5 PT)
- `docs/06-aufgaben/phase-5.md` – **5.12** Log-Viewer (1.5 PT)
- `docs/06-aufgaben/status.md` – neue Zeilen, Summe 67 → **72 PT**
- `.github/instructions/csharp.instructions.md` – §5.1 Serilog & Ringpuffer
- `docs/benutzer/06-bedienung.md` – Abschnitt „Das Protokoll"
- `docs/benutzer/09-fehlersuche.md` – „Was im Protokoll nachsehen"
- `docs/06-aufgaben/prompts/Prompt-Phase-0.md` (**neu**) – kopierfertiger Session-Prompt
- `docs/06-aufgaben/prompts/Prompt-Phase-1.md` (**neu**) – Prompt für die nächste Session
- `docs/06-aufgaben/README.md` – §3.4 „Der nächste Prompt" (neue Konvention)

**Verifikation:**
- `python --version` → 3.12.10 (via `winget`, `--scope user`, **ohne Admin**)
- `python -m pytest custom_components/comfobox/tests -v` → **3 passed**
- `python -m ruff check custom_components/comfobox` → **All checks passed**
- `pwsh -File tools/check-links.ps1` → 84 Dateien, 0 Probleme

**Offen / nicht gemacht:**
- **Kein Docker-Daemon** → Container-Tasks weiterhin nicht verifizierbar.
- Die HA-Integration hat nur **3 Tests** (Config Flow). Coordinator, API-Client und
  die Plattform-Mappings (`sensor`/`number`/`select`/`switch`) sind **noch nicht**
  getestet – das ist der Rest von Aufgabe 8a.6.
- Die **Solution** (`src/ComfoBox.sln`) existiert noch nicht → Aufgabe 0.4.

**Überraschungen:**
- `pytest-socket` blockiert jede Socket-Erstellung. Home Assistants Test-Plugin tut das
  in `pytest_runtest_setup` – und **darum** scheitert unter Windows schon die
  Event-Loop-Erstellung (asyncio braucht unter Windows ein TCP-Socketpaar als
  Self-Pipe, unter Linux genügt ein AF_UNIX-Socket, den HA erlaubt).
  - **Fehlversuch 1:** Fixture `socket_enabled` → wirkungslos, weil `event_loop`
    **modulscoped** ist und die Fixture daher **vor** den function-scoped Fixtures
    entsteht.
  - **Fehlversuch 2:** `hookwrapper` → zu spät. Der Code hinter `yield` läuft erst
    nach dem kompletten Setup; der eingebaute `pytest_runtest_setup` von pytest baut
    aber genau dort die Fixtures auf.
  - **Lösung:** `pytest_socket.disable_socket` im conftest **ersetzen**. HA ruft es
    ueber das Modulattribut auf → der Patch greift unabhängig von der Hook-Reihenfolge.
    `connect()` bleibt auf `127.0.0.1` beschraenkt.
- `aiodns` (HA setzt den DNS-Resolver fest auf `AsyncResolver`) verlangt unter Windows
  einen `SelectorEventLoop`, HA nutzt dort den Proactor → `RuntimeError: aiodns needs
  a SelectorEventLoop on Windows`. **Lösung:** im Test `ThreadedResolver` statt
  `AsyncResolver` (`homeassistant.helpers.aiohttp_client.AsyncResolver`). Im
  **Produktivbetrieb** (Linux) bleibt aiodns unveraendert.
- **Drei .NET-10-Fallen reproduziert und in `phase-0.md` dokumentiert:**
  1. `dotnet new sln` erzeugt standardmäßig **`.slnx`** → `--format sln` ist Pflicht,
     sonst passen alle Build-Befehle der Spec nicht.
  2. Die Templates schreiben `Version="…"` in jedes `<PackageReference>` → mit
     `ManagePackageVersionsCentrally=true` bricht der Restore mit **NU1008**.
  3. `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true` macht **CS1591**
     (fehlender XML-Kommentar) zum Fehler – die Template-Dateien `Class1.cs` /
     `UnitTest1.cs` müssen weg.

---

## 2026-10-31 – Phase 2 abgeschlossen: Domain & Gateway-Kern (Aufgaben 2.1–2.7)

**Ergebnis:** Die Domäne kennt jetzt alle **127 Items** mit allen Metadaten
(BACnet-ID, Typ, Einheit, Grenzen, Schreibbarkeit, Enum-Namen, **Priorität**).
Der Gateway startet, liest den BACnet-Bus, hält die Werte im Cache, schützt
das EEPROM mit einem **persistenten** 24-h-Zähler und protokolliert über
Serilog in einen In-Memory-Ringpuffer. **143 Tests grün** (45 BACnet + 30 Domain
+ 10 Contract + 58 Gateway). Die Golden-Fixture und `docs/topics.json` sind
**byteweise unverändert**.

**Geänderte Dateien (neu – Domain):**
- `src/ComfoBox.Domain/Items/ItemDefinition.cs` – das Item (Id, Gruppe, BACnet-ID, Kind, Min/Max, Einheit, ReadOnly, Expert-Modus, Priorität, Enum-Werte)
- `src/ComfoBox.Domain/Items/ItemValueKind.cs` – `Analog` / `Enum` / `Date`
- `src/ComfoBox.Domain/Items/RefreshPriority.cs` – `None` / `Once` / `Primary` / `Secondary`
- `src/ComfoBox.Domain/Items/Catalog.cs` – **generiert** (127 Items)
- `src/ComfoBox.Domain/Items/ItemEnums.cs` – **generiert** (4 Enums, 70 Namen)
- `src/ComfoBox.Domain/Items/IItemCatalog.cs` + `ItemCatalog.cs` – Lookup nach Id/Topic-Pfad/Gruppe
- `src/ComfoBox.Domain/Values/ItemValue.cs`, `AnalogValue.cs`, `EnumValue.cs` – kultur-invariant

**Geänderte Dateien (neu – Gateway):**
- `src/ComfoBox.Gateway/Configuration/{Gateway,Bacnet,Mqtt,Logging}Options.cs` – Bindung + Validierung
- `src/ComfoBox.Gateway/Logging/{InMemoryLogSink,LogEntry,SecretRedactor}.cs` – Ringpuffer (1000) + Maskierung
- `src/ComfoBox.Gateway/ValueCache.cs` – letzter Wert + Zeitstempel, `IsStale`, SSE-Abonnenten
- `src/ComfoBox.Gateway/Health/ErrorCollector.cs` – Fehler-Senke (50)
- `src/ComfoBox.Gateway/Protection/WriteCounter.cs` – persistent, atomar, 24-h-Fenster
- `src/ComfoBox.Gateway/Bacnet/BacnetDevice.cs` – **ein** SemaphoreSlim, Priorisierung, Timeout je Item
- `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` + `PollPlan.cs` – Poll-Zyklen
- `src/ComfoBox.Bacnet/Bacnet/IBacnetClient.cs` – Testnaht (Gateway-Test gegen Attrapp)

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Program.cs` – Serilog, DI, Options-Validierung, `/api/health`
- `src/ComfoBox.Gateway/appsettings.json` – Abschnitte `Gateway`, `Bacnet`, `Mqtt`, `Logging`
- `src/Directory.Packages.props` – `Serilog.Extensions.Hosting` gepinnt
- `tools/generate-topics/generate-topics.mjs` – Prioritäten, Expert-Modus, Enums, `--catalog`, `--catalog-check`

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **143 Tests grün, 0 Fehler**
- Golden-Fixture **byte-identisch**:
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B` (5.657 B)
- `docs/topics.json` **unverändert** (`git status` leer) –
  der Contract-Test vergleicht den Katalog Feld für Feld dagegen
- `node tools/generate-topics/generate-topics.mjs --check` → deckungsgleich (127 Items)
- `dotnet restore src/ComfoBox.sln --locked-mode` → grün (Lock-Dateien aktualisiert)
- **Smoke-Test:** Gateway gestartet, `/api/health` → `{"status":"online", …}`,
  Serilog schreibt JSON-Datei (`gateway-20261031.jsonl`) mit `SourceContext`

**Offen / nicht gemacht:**
- **Kein MQTT** – Phase 3. Die Werte liegen nur im Cache.
- **Keine REST-API außer `/api/health`** – Phase 4.
- Die **zwei Legacy-DateItems** (`States/Controller/InitialOperationDate`,
  `LastParameterChangeDate`) werden gelesen, ihr Wert bleibt aber leer
  (Befund A20). Ihr Item bleibt im Katalog, damit die Topics unverändert sind.
- Die **Item-Id** folgt der Regel camelCase → snake_case
  (`zone.set_point_normal`). Die Spec nennt im Beispiel `zone.setpoint_normal` –
  das ist nicht generisch herstellbar; die Spec-Beispiele wurden angepasst.
- Die **Enum-Beschreibungen** (z. B. „Keine Energieerzeugung") sind **nicht**
  im Katalog – nur die Namen. Für die UI (Phase 5) ggf. nachziehen.

**Überraschungen:**
- ⚠️ **Der Altbestand ist nicht einheitlich kodiert.** Die Item-Dateien
  (`Zone`, `States`, `Config`, `Tests`, `Warmwater`) sind **Windows-1252**,
  die Enum-Dateien und `Time.cs` dagegen **UTF-8**. Ein blindes CP1252-Lesen
  zerlegt „ä" (0xC3 0xA4) in zwei Zeichen – der Enum-Name wurde zu
  `rmequellenstoerung` statt `Wärmequellenstoerung`. Der Generator erkennt
  die Kodierung jetzt **automatisch** (gültiges UTF-8 → UTF-8, sonst CP1252).
- ⚠️ **`Interlocked.Read` gibt es nur für `long`.** Für den `int`-Schreiber
  muss `Volatile.Read(ref _writeIndex)` verwendet werden (Compile-Fehler
  „Konvertierung von `ref int` in `ref readonly long`").
- ⚠️ **Serilog 4 kennt am `LogEvent` keine `LoggerName`-Eigenschaft.** Die
  Logger-Kategorie steckt in der Property `SourceContext`.
- ⚠️ **`ScalarValue.ToString()` gibt Strings in Anführungszeichen zurück**
  (`"broker:1883"`). Für die Anzeige muss `ScalarValue.Value` genommen werden.
- ⚠️ **`CA1848` (LoggerMessage-Delegaten) und `CA1873` sind in der Solution
  aktiv** (`AnalysisLevel=latest-recommended`) und widersprechen dem in
  `csharp.instructions.md` festgelegten Stil. Sie werden im Gateway-Projekt
  per `NoWarn` unterdrückt – mit Begründung im csproj.
- ⚠️ **`CA1716` verbietet `Set` als Schnittstellenmember.** `IValueCache.Set`
  heißt jetzt `IValueCache.Update`.
- ⚠️ **`CA1001`**: eine Klasse mit `SemaphoreSlim` muss `IDisposable` sein –
  `BacnetDevice` implementiert es jetzt.
- ⚠️ **XML-Kommentare sind auf lokalen Funktionen in Top-Level-Statements
  nicht erlaubt** (CS1587) → normale `//`-Kommentare verwenden.
- ⚠️ **`using N;` importiert keine verschachtelten Namespaces.** Aus
  `ComfoBox.Gateway.Tests` ist `Serialize.ASN1` nicht auflösbar – stattdessen
  `using ComfoBox.Bacnet.Serialize;` und `ASN1` direkt verwenden.
- ⚠️ **DataAnnotations validieren verschachtelte Objekte nicht.** Der
  `RingBuffer` muss separat geprüft werden.

---

## 2026-10-31 – Phase 4 abgeschlossen: REST/SSE-API (Aufgaben 4.1–4.9)

**Ergebnis:** Der Gateway hat die **komplette** REST-/SSE-Schnittstelle:
Items lesen und schreiben, Health/Status/Fehler, Live-Werte per SSE,
Konfiguration, Neustart, Setup-Assistent, API-Token und Log-Viewer.
**278 Tests grün** (45 BACnet + 30 Domain + 10 Contract + 193 Gateway).
Beide Fixtures sind **byteweise unverändert**. Die 21 Endpunkte stehen im
OpenAPI-Dokument (`/swagger`, nur im Development-Modus).

**Geänderte Dateien (neu – API):**
- `src/ComfoBox.Gateway/Api/GatewayApiExtensions.cs` – `AddGatewayApi()` (DI) + `MapGatewayApi()` (Routen) + `MapGatewaySwagger()`
- `src/ComfoBox.Gateway/Api/ItemsEndpoints.cs` – `GET /api/items`, `/api/items/{id}`, `/api/items/group/{group}`, `PUT /api/items/{id}` (204/400/403/409/422/502)
- `src/ComfoBox.Gateway/Api/StatusEndpoints.cs` – `/api/health`, `/api/status`, `/api/diagnostics/errors`, `/api/diagnostics/writes`
- `src/ComfoBox.Gateway/Api/EventsEndpoints.cs` – SSE `/api/events` (`snapshot` + `value`)
- `src/ComfoBox.Gateway/Api/ConfigEndpoints.cs` – `/api/config` (GET ohne Passwörter, PUT → `config.json`)
- `src/ComfoBox.Gateway/Api/ServiceEndpoints.cs` – `POST /api/service/restart` (202)
- `src/ComfoBox.Gateway/Api/SetupEndpoints.cs` – `/api/setup/*` (state, ports, Tests, config, apply)
- `src/ComfoBox.Gateway/Api/TokenEndpoints.cs` – `/api/security/tokens` (erzeugen/listen/widerrufen)
- `src/ComfoBox.Gateway/Api/LogEndpoints.cs` – `/api/diagnostics/logs` (Filter), `/stream` (SSE-Tail), `DELETE`
- `src/ComfoBox.Gateway/Api/Models/*.cs` – die DTOs (Feldnamen sind **Vertrag**)
- `src/ComfoBox.Gateway/Api/ApiJson.cs`, `ApiErrors.cs`, `SseWriter.cs` – JSON-Optionen, Fehlerkanal, SSE-Helfer

**Geänderte Dateien (neu – Sicherheit, Setup, Persistenz):**
- `src/ComfoBox.Gateway/Security/PasswordHasher.cs` – PBKDF2-HMAC-SHA256, 600.000 Iterationen
- `src/ComfoBox.Gateway/Security/ApiToken.cs` + `ApiSecurityStore.cs` – Token-Bestand in `config.json` (nur Hash), Setup-Flag, Administrator
- `src/ComfoBox.Gateway/Security/ApiAccessGuard.cs` – die Zugriffskontrolle (Bearer, Basic, LAN-Modus)
- `src/ComfoBox.Gateway/Setup/SetupService.cs` – Ports, BACnet-/MQTT-Test, Konfiguration schreiben
- `src/ComfoBox.Gateway/Configuration/ConfigStore.cs` – `config.json` (Modus 0600), atomar
- `src/ComfoBox.Gateway/Configuration/ApiOptions.cs` – `Api:RequireAuthentication` u. a.
- `src/ComfoBox.Gateway/Configuration/GatewayConfiguration.cs` – die **Präzedenz** der Konfigurationsquellen
- `src/ComfoBox.Gateway/Storage/AtomicFile.cs` – atomares Schreiben mit Retry (Windows-Sperre)
- `src/ComfoBox.Gateway/ServiceControl.cs` – Neustart in-process + Marker-Datei
- `src/ComfoBox.Gateway/Logging/LogStreamBroker.cs` – die Verteilung der Log-Einträge (max. 5 Streams)
- `src/ComfoBox.Gateway/RuntimeInfo.cs` – Startzeitpunkt und Uptime

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Program.cs` – Konfigurationsquellen, `AddGatewayApi()`, `MapGatewayApi()`, Swagger nur im Development-Modus
- `src/ComfoBox.Gateway/GatewayServiceCollectionExtensions.cs` – `startBackgroundServices` (Testnaht), `LoggingOptions` als Singleton
- `src/ComfoBox.Gateway/Bacnet/BacnetDevice.cs` – `IsConnected` + `LastError` (für `/api/status`)
- `src/ComfoBox.Gateway/Configuration/GatewayOptions.cs` – `DeviceId`, `RestartEnabled`
- `src/ComfoBox.Gateway/Logging/InMemoryLogSink.cs` – Ereignis `EntryAdded` (Live-Tail)
- `src/ComfoBox.Gateway/Protection/WriteCounter.cs` – `AtomicFile` (Retry gegen die Windows-Dateisperre)
- `src/Directory.Packages.props` – Swashbuckle **10.3.1** (Microsoft.OpenApi 2.x, .NET 10-kompatibel)
- `src/ComfoBox.Gateway/ComfoBox.Gateway.csproj` – `Microsoft.AspNetCore.OpenApi` entfernt (Kollision mit Swashbuckle 9)

**Geänderte Dateien (neu – Tests, 59 Stück):**
- `src/ComfoBox.Gateway.Tests/Api/GatewayApiFixture.cs` – **echter** Kestrel-Testhost auf einem freien Port
- `src/ComfoBox.Gateway.Tests/Api/ItemsApiTests.cs` – 16 Tests (Schreibpfade, Statuscodes, A5/A6)
- `src/ComfoBox.Gateway.Tests/Api/StatusApiTests.cs` – 6 Tests (Feldnamen von `/api/status` **exakt**)
- `src/ComfoBox.Gateway.Tests/Api/EventsApiTests.cs` – 2 Tests (SSE `snapshot` + `value`)
- `src/ComfoBox.Gateway.Tests/Api/ConfigApiTests.cs` – 4 Tests (ohne Passwörter, Persistenz)
- `src/ComfoBox.Gateway.Tests/Api/ServiceApiTests.cs` – 3 Tests (202, Marker-Datei, deaktivierter Neustart)
- `src/ComfoBox.Gateway.Tests/Api/TokenApiTests.cs` – 7 Tests (einmaliger Klartext, nur Hash, 401, Widerruf)
- `src/ComfoBox.Gateway.Tests/Api/SetupApiTests.cs` – 9 Tests (PBKDF2, Apply, Schutz nach der Ersteinrichtung)
- `src/ComfoBox.Gateway.Tests/Api/LogApiTests.cs` – 8 Tests (Filter kombiniert, Limit, SSE-Tail, max. 5 Streams)

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → grün, 0 Warnungen, 0 Fehler
- `dotnet test src/ComfoBox.sln -c Release` → **278 Tests grün**
- Golden-Fixture `bacnet-frames.json` → SHA256 `F95AA763…CE681B`, 5.657 Bytes (**unverändert**)
- `docs/topics.json` → SHA256 `67D7485B…C1463`, 33.845 Bytes (**unverändert**)
- `tools/check-links.ps1` → 617 Links, 0 Probleme
- **Smoke-Test** gegen den **laufenden** Gateway: `/api/health`, `/api/status`,
  `/api/setup/state`, `/api/items/{id}`, `/api/diagnostics/writes` → 200,
  `/swagger/v1/swagger.json` → 200 mit allen 21 Endpunkten

**Offen / nicht gemacht:**
- `POST /api/service/reconnect/bacnet` und `.../mqtt` (aus der Spec §3.1) fehlen
  noch – der Neustart deckt beide Fälle ab (Aufgabe 5.x nachziehen).
- `PUT /api/config` schreibt die Werte in die `config.json`; sie werden beim
  **nächsten Start** wirksam (die Options sind unveränderliche Schnappschüsse).
  Die Oberfläche bietet deshalb den Neustart an.
- `GET /api/items/{id}` liefert den **gecachten** Wert, keinen blockierenden
  BACnet-Read (der würde den Bus belegen). Der Wert kommt über `/api/events`.
- Die Web-UI, der Cookie-Login (5.10) und die Token-Verwaltung in der
  Oberfläche sind Phase 5 bzw. 8d.

**Überraschungen:**
- ⚠️ **Ein nicht registrierter Options-Typ hat JEDE Anfrage mit HTTP 500
  beantwortet.** `LoggingOptions` war nur über `IOptions<T>` gebunden, nicht
  als Singleton – die Minimal-API hat den Parameter deshalb für einen
  **Request-Body** gehalten: „Body was inferred but the method does not allow
  inferred body parameters". Der Fehler tritt **erst beim ersten Request** auf
  (die Endpunkte werden lazy gebaut) und sieht wie ein Serverabsturz aus.
  **Merksatz:** Jeder Typ, der als Parameter eines Minimal-API-Endpunkts
  auftaucht, muss als konkreter Dienst registriert sein.
- ⚠️ **`RemoveAll<IHostedService>()` zerstört die Pipeline des Webhosts.** Der
  `GenericWebHostService` (er startet Kestrel) ist selbst ein `IHostedService`.
  Nach dem Entfernen antwortete der Server mit einem leeren 500.
  **Merksatz:** Für Tests den Schalter `AddGatewayServices(..., startBackgroundServices:
  false)` benutzen – nicht `RemoveAll<IHostedService>()`.
- ⚠️ **Swashbuckle 9 kollidiert mit .NET 10.** `Microsoft.AspNetCore.OpenApi`
  10.0.14 braucht `Microsoft.OpenApi` 2.x, Swashbuckle 9 braucht 1.6.x. Umstieg
  auf Swashbuckle **10.3.1** und `Microsoft.AspNetCore.OpenApi` aus dem Projekt
  entfernt.
- ⚠️ **Ein SSE-Stream ohne Daten schickt keine Kopfzeilen.** Der Browser
  (und `HttpCompletionOption.ResponseHeadersRead`) wartet dann bis zum ersten
  Eintrag. `SseWriter.PrepareAsync` flusht die Kopfzeilen deshalb sofort.
- ⚠️ **`File.Move(…, overwrite: true)` scheitert unter Windows sporadisch**
  („Access to the path is denied") – ein Virenscanner sperrt die Datei für
  einen Moment. `AtomicFile.WriteAllText` wiederholt den Vorgang jetzt
  sechsmal (25 ms Abstand). Der Schreibzähler schrieb sonst bei 100 schnellen
  Writes einmal ins Leere.
- ⚠️ **PBKDF2 mit 600.000 Iterationen kostet ≈ 0,3 s.** Ein Token enthält
  deshalb seine Id im Klartext-Präfix (`cbk_<id>_<secret>`) – gesucht wird
  über die Id, verifiziert wird nur **ein** Hash. Das Verifikations-Ergebnis
  wird zusätzlich für kurze Zeit zwischengespeichert (bei jeder Änderung des
  Token-Bestands wird der Cache verworfen → ein widerrufener Token wirkt
  sofort nicht mehr).
- ⚠️ **`dotnet test` zeigt bei einem Fehler im Testhost keinen Body.** Ein
  leerer 500 ohne Exception-Text kostete Zeit. Abhilfe: den Testhost mit
  `dotnet test --logger "console;verbosity=detailed"` laufen lassen – dann
  steht die Server-Exception im Log (`fail: Microsoft.AspNetCore.Server.Kestrel[13]`).

---

## 2026-11-01 – Synology-Paket per CI & BACnet-Bus-Analyse

**Ergebnis:** Drei Wünsche sind umgesetzt: (1) das Synology-Paket wird **per
GitHub Action** gebaut und als **Release-Asset** bereitgestellt (nicht im
Repository – damit greift das 500-MB-Limit des Free-Accounts nicht),
(2) die Oberfläche kann den **seriellen Port auswählen**, (3) es gibt ein
**Analyse-Werkzeug**, das **nur lesend** zeigt, ob auf dem MS/TP-Bus ein Token
herumgeht. Zusätzlich ist die **Legacy-CI abgeschaltet** – `src_old/` ist
Archiv. **339 Tests grün**, Build 0 Warnungen / 0 Fehler.

**Geänderte Dateien (neu):**
- `.github/workflows/synology-spk.yml` – baut das `.spk` eigenständig
  (Tag-Push oder `workflow_dispatch`), prüft die Shell-Skripte, hängt das
  Paket als Release-Asset an und kopiert es optional per SSH auf die NAS
- `src/ComfoBox.Bacnet/Bacnet/MstpEvent.cs` + `IMstpObserver.cs` +
  `MstpEventRecorder.cs` – **allokationsfreier** Ringpuffer für die
  MS/TP-Ereignisse (parallele Arrays, keine Sperre, kein Log)
- `src/ComfoBox.Gateway/Bacnet/BacnetBusAnalyzer.cs` – wertet den Ringpuffer
  **außerhalb** des Bus-Takts aus und schreibt **erst dort** das Log
- `src/ComfoBox.Gateway/Api/BacnetDiagnosticsEndpoints.cs` +
  `Api/Models/BacnetAnalysisDtos.cs` – `GET /api/diagnostics/bacnet`,
  `POST`/`DELETE /api/diagnostics/bacnet/observe`
- `src/ComfoBox.Web/src/app/core/api/bacnet.service.ts` +
  `core/models/bacnet.model.ts` – Client und Modell
- `src/ComfoBox.Bacnet.Tests/MstpEventRecorderTests.cs` (6 Tests) und
  `src/ComfoBox.Gateway.Tests/Api/BacnetDiagnosticsApiTests.cs` (5 Tests)

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Bacnet/Bacnet/MstpStateMachine.cs` – **alle** `LogDebug`-Aufrufe
  aus `Run()`, `SendFrame()` und `GetNextMessage()` entfernt; meldet stattdessen
  `MstpEventKind`-Ereignisse
- `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` – jetzt **Singleton**,
  mit `SetPaused(bool)`: während einer Beobachtung pausiert das Polling, damit
  der Token ungestört am Ring entlangläuft
- `src/ComfoBox.Web/src/app/features/diagnostics/{diagnostics.ts,html,scss}` –
  neuer Reiter „BACnet-Bus"
- `.github/workflows/build-test.yml` – der Job `legacy-check` ist **entfernt**;
  zusätzlich eine Drift-Prüfung der `package-lock.json` **vor** `npm ci`
- `.github/workflows/codeql.yml` – `paths-ignore` für `src_old/` und `docs/Mono/`
- `.github/workflows/docker-publish.yml` – der SPK-Job ist raus (nur noch der
  neue Workflow baut das Paket)

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **339 Tests grün** (30 Domain +
  10 Contract + 52 BACnet + 247 Gateway)
- `npm run lint` → grün; `npm run build --configuration production` → grün
- `bash deploy/synology-spk/tools/check-shell.sh` → grün
- `node tools/build-docs.mjs` → 6 HTML-Seiten; `tools/check-links.ps1` →
  105 Dateien / 653 Links / **0 Probleme**
- API live geprüft: `GET` liefert den Zustand, `POST` mit ungültiger Dauer
  → **400**, `DELETE` beendet die Beobachtung
- `docker build` + `docker run` → Container **healthy**, SPA wird ausgeliefert,
  `docker stop` beendet ihn per SIGTERM in 0,5 s

**Überraschungen:**
- ⚠️ **Die CI lief seit Phase 0 nie.** Der Push nach `origin/Upgrade` zeigte
  wieder „Failure" – aber mit **0 Jobs** und **0 Sekunden** Dauer. Die
  Annotation des Laufs verriet den Grund:
  - `build-test.yml` (Zeile 84): **„Unrecognized function: 'hashFiles'"** –
    die Bedingung `if: hashFiles(...) != ''` stand auf **Job-Ebene** (sie
    stammte aus Phase 0, als das Angular-Projekt noch nicht existierte).
    `hashFiles` ist in einer Job-Bedingung **nicht erlaubt** → GitHub lehnt
    die **ganze Datei** ab.
  - `codeql.yml` (Zeile 21): **„Unexpected value 'paths-ignore'"** – der
    Schlüssel stand auf **Top-Level** (er gehört **unter `on:`**). Zusätzlich
    war `schedule:` unter `pull_request:` verschachtelt statt als Geschwister
    → der Wochenlauf lief nie.
  - **Merksatz:** Ein Lauf mit **0 Jobs** ist **nie** ein Code-Problem.
    Immer zuerst die **Annotation** lesen. Und: die GitHub-API liefert für
    diese Läufe weder Jobs noch Check-Runs – die Information steht **nur** in
    der Oberfläche.
- ⚠️ **Die CI lief nur auf `main`.** Auf `Upgrade` (dem Entwicklungsbranch)
  passierte **nichts** – der Code wurde erst beim Zusammenführen geprüft.
  Die früheren Läufe auf `Upgrade` waren eine Nebenwirkung des ungültigen
  Workflows: bei einer unparsebaren Datei triggert GitHub permissiv.
- ⚠️ **Der Generator war plattformabhängig.** Ein verbatim-Stringliteral über
  mehrere Zeilen enthält die **physischen** Zeilenenden der Quelldatei (CRLF
  unter Windows, LF unter Linux). Der Fixture-Check schlug unter Linux mit
  **19 Abweichungen** fehl (alle `Description`). Die Zeilenenden werden jetzt
  **immer** auf CRLF normalisiert – die Fixture bleibt byteweise
  unverändert.
- ⚠️ **Unter Linux meldet .NET bei einer fehlenden Gerätedatei
  `UnauthorizedAccessException`** – nicht nur bei fehlendem Zugriff. Der
  Test „Ein BACnet-Test liefert einen Hinweis" erwartete
  `serial_port_not_found` und schlug deshalb unter Linux fehl.
- ⚠️ **Der Coverage-Collector bringt eine `netstandard`-Referenz in die
  Domäne.** coverlet schreibt die instrumentierten Assemblies mit einem
  Verweis auf die .NET-Standard-Fassade um – ein Werkzeug-Artefakt.
- ⚠️ **Zwei Tests waren Wettläufe** (`Task.Delay(50)` als Synchronisation) und
  liefen auf dem belasteten CI-Läufer in den Timeout. **Regel:** in Tests
  nie auf eine feste Zeit warten, sondern **ereignisbasiert**
  (`TaskCompletionSource`).
- ⚠️ **Der Docker-Health-Status steht erst nach dem Healthcheck auf
  „healthy"** (Intervall 30 s, Startperiode 25 s) – der HTTP-Endpunkt
  antwortet schon nach ~2 s. Wer den Status sofort prüft, sieht
  „starting".
- ⚠️ **GitHub startet jedes `run:`-Skript mit `bash -e`.** `set -uo pipefail`
  **ergänzt** nur – `-e` bleibt aktiv. Für eine Diagnose nach einem
  Fehlschlag muss **`set +e`** explizit gesetzt werden.
- ⚠️ **Im MS/TP-Hot-Path darf nicht geloggt werden.** Der Token hat nur
  **95 ms** (`TUsageTimeout`) bis zur Übernahme – ein Log-Eintrag an dieser
  Stelle kostet Zeit und kann den Token kosten. Deshalb: der Zustandsautomat
  meldet **Ereignisse** in einen vorbelegten Ringpuffer, und **erst dort**
  (außerhalb des Bus-Takts) wird bewertet und geloggt.
- ⚠️ **Der CI-Lauf war rot, ohne dass der Code defekt war.** Die
  `package-lock.json` enthielt im Root-Eintrag **keine Abhängigkeiten** –
  `npm ci` bricht damit mit `EUSAGE` ab. Die Lock-Datei war konsistent; der
  Workflow prüft die Drift jetzt **vor** `npm ci` und sagt, was zu tun ist.
- ⚠️ **CodeQL kennt kein `paths-ignore` beim Init.** Es muss in die
  `config:`-Sektion – sonst ignoriert der Scan `src_old/` nicht.
- ⚠️ **`p-tabpanel` projiziert per `ng-template` + `*ngTemplateOutlet`.** Ein
  per `@if`/`@else` **ersetzter** Zweig bleibt als sichtbarer „Waisen"-Zweig im
  DOM stehen (der Leave-Pfad greift dort nicht). **Regel:** Ladehinweise in
  einem `p-tabpanel` **per CSS-Klasse** schalten (`[class.cb-hidden]` mit
  `display: none !important`), nicht per Kontrollfluss.
- ⚠️ **Ein statisches `value` auf `p-tabs` macht die Reiter starr.** Der
  aktive Reiter hängt an `[(value)]` mit einem Signal – sonst lassen sich die
  Reiter nicht umschalten.
- ⚠️ **`p-table` erwartet ein veränderbares Array.** `readonly BacnetEvent[]`
  erzeugt TS4104 – die Modell-Listen sind deshalb `mutable`.
- ⚠️ **`Math.Min(long, int)` kompiliert nicht** (`CS1503`) – den Schnittwert
  erst auf `long` casten und dann auf `int` zurück.

---

## 2026-11-02 – Release-Kette: das Synology-Paket war nicht auslösbar

**Ergebnis:** Der User meldete „bei der GitHub Action geht noch was nicht".
Die Ursache war eine Kette von **sechs** Problemen, die erst der **Tag-Push**
sichtbar machte – vorher war die Action nie gelaufen:

1. **Der Default-Branch heißt `master`** (Legacy-Stand von 2023) und enthält
   **überhaupt keine Workflows**. Ein `workflow_dispatch` funktioniert nur,
   wenn die Datei im Default-Branch liegt → der SPK-Workflow war weder
   sichtbar noch manuell startbar. **Ein `push`-Trigger funktioniert auch
   außerhalb** – deshalb löst der Tag-Push die Action aus.
2. **Die Trigger zeigten auf `main`** – den Branch gibt es nicht. `main` →
   `master` + `Upgrade` (build-test, codeql, dependency-review).
3. **`aquasecurity/trivy-action@0.28.0` existiert nicht** (der Tag lautet
   `v0.28.0`) → der Image-Job brach schon beim Action-Download ab, es kam
   **kein Image und kein Release**.
4. **`packages.json` zeigte auf `releases/latest`** – das ist das zuletzt
   *veröffentlichte* Release und nach dem Tag-Push das HA-Release `1.0.0`
   (ohne `v`). Der Download lief ins Leere → jetzt fester Release-Tag.
5. **HA-Integration und Add-on publizierten beide auf Tag `1.0.0`** – der
   zweite Aufruf überschrieb das erste Release → jetzt **ein** Release.
6. **Drei Workflows liefen parallel und konkurrierten ums Release** → der
   SPK-Workflow **wartet** jetzt auf das Release des Image-Workflows. Damit
   ist auch die **Reihenfolge** garantiert: das Release entsteht *nach* dem
   Image-Push, das Image liegt also sicher in GHCR, bevor das SPK-Paket
   existiert.

**Geänderte Dateien:**
- `.github/workflows/{build-test,codeql,dependency-review}.yml` – Trigger auf
  `master` + `Upgrade`
- `.github/workflows/docker-publish.yml` – Trivy `v0.36.0`, `latest`-Tag auf
  `master`
- `.github/workflows/synology-spk.yml` – Warteschleife auf das Release,
  race-sicheres Hochladen, feste Release-URL in der Paketquelle
- `.github/workflows/ha-release.yml` – **ein** Release statt zwei,
  `continue-on-error` für die HACS-Prüfung, gültiger hassfest-Input
- `custom_components/comfobox/services.yaml` – **neu** (fehlte komplett)
- `custom_components/comfobox/manifest.json` – Kategorien und
  Schlüssel-Sortierung korrigiert

**Überraschungen:**
- ⚠️ **Der Default-Branch ist `master`, nicht `main`.** Ein Trigger auf `main`
  feuert **nie** – das Repository hat keinen solchen Branch. Wer `main`
  voraussetzt, baut eine Workflow, die nie läuft.
- ⚠️ **Ein `push`-Trigger funktioniert auch außerhalb des Default-Branchs**,
  ein `workflow_dispatch` und ein `workflow_call` **nicht**. Deshalb ist der
  Tag-Push der einzige Weg, die Action ohne Branch-Wechsel auszulösen.
- ⚠️ **Eine Action-Version ohne `v` kann es geben.** Der Tag der Trivy-Action
  lautet `v0.28.0` – `0.28.0` **ohne** `v` findet GitHub nicht, und der Job
  bricht schon beim Action-Download ab (nicht erst im Scan).
- ⚠️ **`releases/latest` ist nicht die neueste Version**, sondern das zuletzt
  **veröffentlichte** Release. Bei mehreren Release-Erzeugern ist das
  unzuverlässig → immer einen **festen** Tag verwenden.
- ⚠️ **Die HACS-Prüfung prüft die Repository-Topics** – eine
  **Einstellung der GitHub-Seite**, nicht Teil des Codes. Für die
  Installation aus einem eigenen HACS-Repository sind sie nicht nötig.
- ⚠️ **`hassfest` kennt keinen Input `config-file`** („valid inputs are
  ['']") – der Pfad gehört in `path`.
- ⚠️ **Home-Assistant kennt die Kategorie `sensor` nicht** – die gültigen
  Kategorien stehen in `homeassistant/components/homeassistant/strings.json`.
- ⚠️ **Die `concurrency`-Gruppe eines Workflows blockiert den nächsten Lauf
  desselben Refs.** Ein wartender Lauf (30 min auf das Release) hält den
  Folge-Lauf in der Queue – das ist gewollt, macht aber den Status
  unübersichtlich.

---

## 2026-11-08 (2) – Installation scheitert weiterhin am Worker `docker-project`

**Ergebnis:** Die Ursache ist eingegrenzt, der Zustand ist bereinigt und das
Diagnose-Skript beweist jetzt den Grund. Revision 0011 ist gebaut und geprüft.

**Der Befund aus dem `--worker`-Lauf auf der DS923+ (SPK `1.0.2-0010`):**

````
docker compose config        → OK
docker pull                  → OK
docker compose up -d         → OK, Container "healthy", /api/health → 200
````

Am Paket, an der Compose-Datei und am Image liegt es also **nicht**. Der
Fehler passiert davor:

````
00:31:47 Acquire systemd-unit for ComfoBoxGateway when 0x0001 (done)
00:31:47 Acquire docker-project for ComfoBoxGateway when 0x0001 (fail)
00:31:48 End postinst ret=[0]
00:31:48 install ComfoBoxGateway 1.0.2-0010 failed
````

**Entscheidend ist, was FEHLT:** Der Worker scheitert in **derselben
Sekunde** und schreibt **keine einzige Syslog-Zeile**. Beim Versuch um
23:51 stand dagegen noch

````
synopkg[…]: docker_project_worker/…/docker_project_generater_worker.cpp:254
             rollback created projects: `comfobox`
````

im `/var/log/messages`. Der Worker ist bei 00:31 also gescheitert, **bevor**
er Docker angefasst hat – und das Paket stand zu diesem Zeitpunkt auf
`broken` (150, `install_corruption`) aus dem Versuch davor.

**Damit bleiben genau zwei realistische Ursachen – beide werden beseitigt:**

1. **Reste des Fehlversuchs.** Ein `broken`-Paket wird vom DSM beim nächsten
   Mal wie ein *Update* behandelt; zusätzlich bleiben Verzeichnisse,
   Container und Netzwerke liegen. Ein liegengebliebener Container mit festem
   Namen oder ein Projekt-Datensatz des Container Managers lässt `Acquire`
   sofort scheitern – ohne Logzeile.
2. **Der Projektname.** Docker Compose akzeptiert nur `[a-z0-9_-]`;
   `comfobox` war gültig, ist aber nach einem Rollback ein Kandidat für einen
   verwaisten Projekt-Datensatz.

**Geänderte Dateien:**
- `deploy/synology-spk/src/conf/resource` – Projektname `comfobox-gateway`
  (frischer Projekt-Datensatz), `build_params.build = false` (das Paket
  bringt kein `Dockerfile` mit – der Schritt entfällt). Der **Containername**
  bleibt `comfobox-gateway`, damit `docker logs`/`docker_inspect` und die
  Doku unverändert stimmen.
- `deploy/synology-spk/tools/dsm-diagnose.sh`
  - **neu `--reset`**: Paket stoppen/deinstallieren (auch `broken`),
    Verzeichnisse unter `/var/packages` und `@appstore` entfernen, Container
    und Netzwerke löschen, `var/` nach `/tmp` sichern. Das Volume
    `comfobox-data` (Schreibzähler) bleibt **immer** stehen.
  - **`--install` ruft `--reset` jetzt selbst auf** und zeigt danach die
    Syslog-Meldungen des Workers.
  - **neu `show_worker_log()`** – `/var/log/messages` nach
    `docker_project`/`project worker` durchsuchen (dort schreibt der Worker,
    **nicht** in `synopkg.log`).
  - **neu `show_container_state()`** – Container/Netzwerke/Volumes,
    `synopkg status ContainerManager`, `/volume*/docker`.
  - **neu `show_reference_projects()`** – andere Pakete auf derselben NAS,
    die `docker-project` nutzen, mit ihrer `conf/resource` (Ground Truth).
  - **neu `show_resource()`** – die `conf/resource` aus dem SPK und die
    Prüfung, ob `<path>/compose.yaml` wirklich in `package.tgz` liegt.
  - Die Worker-Emulation liest den Projektnamen jetzt aus `conf/resource`
    statt ihn fest zu verdrahten.
- `deploy/synology-spk/tools/check-spk.sh` – prüft den Projektnamen gegen
  `[a-z0-9_-]` und vergleicht `path` aus `conf/resource` mit dem Inhalt von
  `package.tgz` (vorher war `compose/` hart verdrahtet).
- `deploy/synology-spk/tools/check-tgz-layout.sh` – nimmt ohne Argument das
  **neueste** Paket aus `dist/`. Vorher stand dort `…1.0.1-0006.spk` fest –
  der Test hat damit ein längst behobenes Paket geprüft und einen Fehler
  gemeldet, den es nicht mehr gab.
- `deploy/synology-spk/src/conf/README.md` – die drei Regeln für
  `conf/resource` (Name klein, `path` relativ + passend zum `package.tgz`,
  `build_params.build = false`).
- `docs/benutzer/09-fehlersuche.md` – Syslog als eigener Kanal, `--reset`
  als Pflichtschritt nach einem Fehlversuch.
- `CHANGELOG.md`, `deploy/synology-spk/REVISION` (0011)

**Verifikation:**
- `bash build-spk.sh` → `ComfoBoxGateway_noarch-dsm7_1.0.2-0011.spk`
- `bash tools/check-spk.sh` → **OK** (inkl. der neuen `conf/resource`-Checks)
- `bash tools/check-tgz-layout.sh` → `package.tgz` ohne `target/`-Ebene ✅
- `bash tools/check-shell.sh` → alle Skripte syntaktisch korrekt
- `conf/resource` im SPK: `comfobox-gateway` + `build_params.build = false`

**Offen / nicht gemacht:**
- **Der Beweis auf der NAS fehlt.** Bitte einmal
  `sudo sh dsm-diagnose.sh <paket>.spk --install` laufen lassen: das Skript
  räumt auf, installiert und zeigt zum ersten Mal die Syslog-Zeile des
  Workers. Bleibt es bei `(fail)`, steht der Grund dann in der Ausgabe.
- Wenn im Container Manager noch ein Projekt **`comfobox`** auftaucht: das
  ist der Datensatz des alten Versuchs und darf gelöscht werden.

### Nachtrag: der Zustand war es NICHT

Der Lauf mit `1.0.2-0011` auf der DS923+ (2026-11-08, 12:01) widerlegt die
Zustands-These. Nach vollständigem Aufräumen (kein Container, kein Netzwerk,
Port frei, neuer Projektname) steht im Syslog:

````
12:01:01 docker_project_worker/…cpp:354 project build fail: [`2201`] `fail compose up 'd896dda1-…'`
12:01:01 docker_project_worker/…cpp:254 rollback created projects: `comfobox-gateway`
````

**Das ist die entscheidende Erkenntnis:** Der Worker scheitert **an
`compose up`** – nicht an seiner Konfiguration und nicht an Resten. Dieselbe
Datei startet manuell einwandfrei (Container `healthy`, `/api/health` → 200),
und zwar mit **demselben** Docker (24.0.2) und **derselben** Compose-Version
(v2.20.1). Der Unterschied liegt also im **Ausführungsumfeld** des Workers:
er läuft im Docker-Share (`/volume3/docker/…`) und mit dem Compose-Binary des
Container Managers.

**Konsequenz für `dsm-diagnose.sh`:** neuer Modus `--compose-matrix`. Er
startet die Compose-Datei genau so wie der Worker (CM-Binary, Docker-Share)
und lässt danach einzelne Einträge weg – `/dev:/dev`, `group_add`,
`security_opt`, zuletzt alles bis auf `image`. Der erste Lauf, der
funktioniert, nennt den schuldigen Eintrag. Zusätzlich zeigt das Skript die
**Logs des Container Managers** (`/var/log/packages/ContainerManager.log`
usw. – dort steht die Compose-Meldung, die `synopkg` nicht weitergibt) und die
**Meldungstexte aus `libdockerprojectgeneratorworker.so`**.

**Offen:** Der A/B-Test ist auf der NAS noch nicht gelaufen.

### A/B-Test gelaufen (2026-11-08, 12:35) – die Compose-Datei ist unschuldig

`--compose-matrix` hat fünf Varianten mit dem **Compose-Binary des Container
Managers** (v2.20.1) **im Docker-Share** (`/volume3/docker/cbm-test-N`)
gestartet:

| Variante | Ergebnis |
|---|---|
| 1 ausgeliefert | `OK` exit=0 |
| 2 ohne `/dev:/dev` | `OK` exit=0 |
| 3 ohne `group_add` | `OK` exit=0 |
| 4 ohne `security_opt` | `OK` exit=0 |
| 5 nur `image` + `container_name` | `OK` exit=0 |

**Damit ist die Compose-Datei als Ursache ausgeschlossen** – auch ein
minimales Projekt startet im selben Verzeichnis mit demselben Binary.

Der Test hat aber den entscheidenden Hinweis geliefert:

````
WARN volume "comfobox-data" already exists but was created for
     project "comfobox" (expected "cbm-test-1")
````

Das Daten-Volume trägt noch das **Projekt-Label des ersten Projekts**
(`comfobox`) – aus den Versuchen vor der Umbenennung. Für `docker compose`
ist das nur eine Warnung, der **Container Manager** verwaltet seine Projekte
aber selbst (`Docker::Project::WorkerProject`,
`/var/packages/ContainerManager/etc/projects/<uuid>.lock`, im Binary:
`AcquireProjects`, `adminCompose`, `WorkerProject`) und bricht beim Anlegen
ab:

````
project build fail: [`2201`] `fail compose up '<uuid>'`
````

`SYNOShareGet failed. synoerr=[0x1400]` in
`/var/log/packages/ContainerManager.log` passt dazu (fehlgeschlagene
Zuordnung des Volumes/Share zum Projekt).

**Konsequenz:** `dsm-diagnose.sh` hat zwei neue Modi:
- `--reset-volume` – sichert den Volume-Inhalt über das Gateway-Image nach
  `/tmp/comfobox-volume-backup`, löscht `comfobox-data` (Compose legt es
  danach mit dem richtigen Label an) und entfernt liegengebliebene
  Projekt-Datensätze unter `/var/packages/ContainerManager/etc/projects` mit
  „comfobox“ im Namen. Ohne erfolgreiche Sicherung wird **nicht** gelöscht.
- `--install-fresh` – dasselbe, danach installieren.

Zusätzlich zeigt `show_container_state` jetzt die **Labels** der Volumes und
die **Projekt-Datensätze** des Container Managers.

**Lehre für die Zukunft:** Ein Projektname ist nicht „nur ein Name“ – er
steht als Label am Volume. Wird er geändert, gehört das alte Volume einem
Projekt, das es nicht mehr gibt. Deshalb bleibt der Name ab jetzt stabil
(`comfobox-gateway`), und das Volume hat einen festen Namen.

### Nachtrag: `--install-fresh` lief ins Leere (2026-11-08, 12:43)

Der Lauf mit `--install-fresh` scheiterte erneut mit `2201`, und der
Zustandsbericht zeigte das Volume-Label weiterhin als `comfobox`.

**Ursache im Diagnose-Skript selbst:** Die Sicherung des Volume-Inhalts lief
über einen Hilfs-Container mit `--entrypoint sh` – **ohne** `-u`. Das
Gateway-Image hat `USER comfobox` (uid 1001) und konnte das Backup-Verzeichnis
(root, 0755) nicht beschreiben. `cp` scheiterte, `|| true` verschluckte den
Fehler, die Prüfung `[ -n "$(ls -A …)" ]` schlug fehl → „Sicherung
fehlgeschlagen“ → das Volume wurde **nicht** gelöscht. Das Skript meldete
brav „nichts zu tun“ und installierte.

**Fix:**
- `docker run -u 0:0` (als root im Container) und `chmod 0777` auf das
  Backup-Verzeichnis
- Ergebnis über den **Exit-Code** prüfen, nicht über die Ausgabe
- nach `docker volume rm` **verifizieren**, dass das Volume wirklich weg ist
- `--install-fresh`/`--install-trace` **brechen ab**, wenn das Volume noch
  existiert – sonst wiederholt sich derselbe Fehler stumm

**Neu `--install-trace`:** zeichnet während der Installation die
**docker events** auf (`container create/start/die`) und zeigt das
Projektverzeichnis im Docker-Share. Damit ist belegt, ob der Worker einen
Container erzeugt oder schon vorher scheitert.

**Offen:** Der Nachweis, dass das Volume-Label die Ursache war, steht noch
aus – der nächste Lauf mit `--install-trace` entscheidet es.

### Der Volume-Reset hat gewirkt – und die Ursache liegt woanders (2026-11-08, 18:04)

Der Lauf mit `--install-trace` zeigt: Das Volume wurde **wirklich** entfernt
(„OK - 2 Datei(en) gesichert", „ist weg (verifiziert)"), der Projektname
`comfobox-gateway` ist aktiv, keine CM-Projekt-Reste. Trotzdem wieder `2201`.

**Der Mitschnitt beantwortet die Frage nach dem Container:**

````
volume     create   <id>
container  create   wizardly_lederberg
container  attach   wizardly_lederberg
network    connect  bridge
volume     mount    <id>
volume     mount    <id>
container  start    wizardly_lederberg
network    disconnect bridge
volume     unmount  <id>
volume     unmount  <id>
container  die      wizardly_lederberg
volume     destroy  <id>
container  destroy  wizardly_lederberg
volume     destroy  <id>
````

Ein Container wird erzeugt, gestartet und stirbt **innerhalb einer Sekunde**.
Zwei Dinge passen dabei **nicht** zu unserem Compose-Projekt:

| Beobachtung | Erwartung bei unserem Projekt |
|---|---|
| Name `wizardly_lederberg` (Docker-Zufallsname) | `container_name: comfobox-gateway` |
| `network connect bridge` (Standard-Bridge) | Projektnetz `comfobox-gateway_default` |

⇒ Der sterbende Container stammt **nicht** aus unserer Compose-Datei.

**Zweite Erkenntnis – eine Lücke im bisherigen A/B-Test:** `docker compose up
-d` liefert **Exit-Code 0, auch wenn der Container eine Sekunde später
stirbt**. Die fünf „OK"-Varianten haben also nur bewiesen, dass Compose die
Datei akzeptiert – **nicht**, dass der Gateway läuft. Der A/B-Test war damit
zu schwach; er wird durch `--app-smoke` ersetzt (startet wie der Worker,
wartet 15 s, zeigt Zustand, Exit-Code und `docker logs`).

**Neu im Skript:**
- `--app-smoke` (Rauchtest der Anwendung ohne DSM-Worker)
- Container-Wache in `--install-trace`: hängt am `docker events`-Strom und
  kopiert bei `create` sofort `docker inspect` (Image, Pfad, Exit-Code,
  Projekt-Label) und `docker logs` heraus – **bevor** der Rollback den
  Container löscht. Bisher war nur bekannt, *dass* er starb, nicht warum.

**Nächster Schritt:** `--app-smoke` beantwortet, ob der Gateway selbst läuft;
`--install-trace` zeigt, welches Image der sterbende Container benutzt.

### Zwischenstand 18:29 – Image ist gesund, der Worker ruft Compose nie auf

**`--app-smoke` (18:29):** Der Gateway **läuft**.
`Status=running, ExitCode=0, Restarts=0`, „Now listening on: http://[::]:8080".
Serial (`/dev/ttyUSB0` fehlt) und MQTT (`localhost:1883`) scheitern
erwartungsgemäß – das sind keine Startfehler. **Image 1.0.2 ist in Ordnung.**

**Der „mysteriöse" Container war mein eigener.** Das Ereignis
`wizardly_lederberg` (Zufallsname, Standard-Bridge, zwei Volume-Mounts,
Lebensdauer < 1 s) stammte aus `reset_volume` – dort startet ein Hilfs-Container
mit `/data` **und** `/backup`. Kein Fremdprozess, Fehlalarm.

**`--install-trace` (18:30):** Die Wache hat **nichts** mitgeschnitten, das
Volume wurde **nicht** angelegt, `/volume3/docker` enthält kein
Projektverzeichnis. Der Worker fasst Docker also überhaupt nicht an und meldet
trotzdem `2201 fail compose up` in unter einer Sekunde.

⇒ Der Worker scheitert an **seinem eigenen Compose-Aufruf**, bevor Compose
läuft. Bisher wurde nur das **Standalone**-Binary
`/var/packages/ContainerManager/target/usr/bin/docker-compose` getestet – der
Worker benutzt aber vermutlich das **CLI-Plugin** `docker compose`.

**Neu `tools/probe-worker.sh`** (nichts wird verändert, Laufzeit < 5 s):
prüft u. a. `docker compose version` (Plugin), `docker compose config` mit der
ausgelieferten Datei, die Plugin-Suchpfade des Docker-CLI und die
Dateinamen-Erwartungen aus `libdockerprojectgeneratorworker.so`.

### ⭐ GEFUNDEN (2026-11-08, 19:00) – der Dateiname war es

`probe-worker.sh` hat die Ursache geliefert. Block 6 (Texte aus
`libdockerprojectgeneratorworker.so`):

````
docker-compose.admin.yaml
docker-compose.yaml
docker-compose.yml
/var/packages/ContainerManager/target/usr/bin/docker-compose
````

**Der Worker hat eine fest verdrahtete Namensliste, und `compose.yaml` steht
nicht darauf.** Unser Paket lieferte `compose/compose.yaml` – ein Name, den
nur Docker Compose v2 selbst erkennt. Der Worker fand die Datei nicht, brach
ab, **bevor** er Docker anfasste, und meldete `2201 fail compose up`.

Das erklärt **jedes** Detail der bisherigen Befunde:

| Beobachtung | Erklärung |
|---|---|
| kein Volume, kein Container, keine Logzeile | Der Worker kam nie bis Compose |
| `2201` in unter einer Sekunde | Datei nicht gefunden, kein Docker-Aufruf |
| alle fünf A/B-Varianten „OK" | dort wurde `-f <datei>` **explizit** übergeben |
| `--app-smoke` grün | ebenfalls mit explizitem Dateinamen |
| `docker compose config` grün | Compose erkennt `compose.yaml` selbst |

Block 2/5 des Probe-Laufs waren unauffällig: das CLI-Plugin ist vorhanden
(`v2.20.1`), und beide Compose-Varianten verarbeiten die Datei fehlerfrei –
der Fehler lag allein im Namen.

**Fix (Revision 0012):**
- `src/target/compose/compose.yaml` → **`docker-compose.yaml`** (git mv,
  Historie bleibt)
- `build-spk.sh` – neuer Pfad, entfernt einen alten `compose.yaml` vorsorglich
- `check-spk.sh` – prüft **`<path>/docker-compose.yaml`** und dass **kein**
  `compose.yaml` mehr im Paket liegt (Regressionstest)
- `check-compose.sh` – sucht alle drei zulässigen Namen, warnt bei
  `compose.yaml` daneben
- `dsm-diagnose.sh` / `probe-worker.sh` – alle Pfade umgestellt;
  `show_resource` prüft jetzt **alle drei** Namen
- `src/conf/README.md` – der Dateiname als Pflichtregel dokumentiert
- `REVISION` → **0012**

**Verifikation:**
- `bash build-spk.sh` → `ComfoBoxGateway_noarch-dsm7_1.0.2-0012.spk`
- `bash tools/check-spk.sh` → **OK**, inkl.
  `compose/docker-compose.yaml liegt auf der obersten Ebene (Name, den der Worker sucht)`
  und `kein 'compose.yaml' mehr im Paket`
- `bash tools/check-tgz-layout.sh` → `compose/docker-compose.yaml` ✅
- MD5: `7be00bc9b8a6c796525fb7247fb87954`

**Lehre:** Ein Dateiname, den „alle Tools" verstehen, ist kein Beweis. Der
Worker ist ein eigenes Programm mit eigener Liste – nachsehen (strings), nicht
annehmen. Und: `docker compose up -d` mit **Exit 0** beweist nicht, dass der
Container lebt; erst `docker inspect` + `docker logs` sagen die Wahrheit.

**Offen:** Der Beweis auf der NAS (Installation mit 0012) steht aus.

### 0012 – der Dateiname war es auch nicht (2026-11-08, 19:07)

Der Lauf mit `1.0.2-0012` scheitert **identisch**:

```
Acquire docker-project for ComfoBoxGateway when 0x0001 (ready)
Acquire docker-project for ComfoBoxGateway when 0x0001 (fail)     ← 1 Sekunde
project build fail: [`2201`] `fail compose up '04ac84e3-…'`
rollback created projects: `comfobox-gateway`
```

Und weiterhin: **kein Volume**, **kein Container**, **kein Projektverzeichnis**,
`/var/packages/ContainerManager/etc/projects` leer. Die Diagnose bestätigt
`OK: 'compose/docker-compose.yaml' liegt genau dort, wo der Worker sucht.`

⇒ Der Dateiname war **nicht** die (alleinige) Ursache. Der Worker legt den
Projekt-Datensatz an (`rollback created projects` beweist, dass er ihn
angelegt hat), ruft aber nie wirksam Compose auf.

**Was sicher ausgeschlossen ist** (mit echtem Gerät belegt): Compose-Datei
(fünf Varianten, auch „nur `image`"), Image (Smoke-Test: `running`,
`/api/health` 200), Projektname, `build_params.build=false`, Volume-Label,
Reste/Container/Netzwerke, fehlende CM-Projekt-Datensätze, fehlendes
CLI-Plugin, Dateiname.

**Der einzige verbleibende Anhaltspunkt** ist `SYNOShareGet failed.
synoerr=[0x1400]` in `/var/log/packages/ContainerManager.log` – allerdings
stammt die Zeile aus dem Container-Manager-Update vom 7.11. 13:38, ist also
möglicherweise alt.

**Neu `--worker-forensics`:** beobachtet den Prozessbaum während der
Installation in einer engen Schleife (< 50 ms) und schneidet jeden
`docker`/`compose`-Aufruf **mit Argumenten** mit; sucht zusätzlich die
Dateien, die der Worker dabei schreibt (dort liegt sein Projekt-Log), und
prüft die Registrierung des Docker-Share (`synoshare --get docker`), die
Docker-Socket-Rechte und die Gruppen des Paketbenutzers.

**Wenn das nichts ergibt:** Der Worker ist auf dieser NAS nicht
funktionsfähig, dann wird der Container ohne ihn gestartet (Alternative:
`conf/systemd` + eigenes Startskript, oder host-seitiger Supervisor wie in
ADR-008 ursprünglich vorgesehen).

### ⭐ Der Worker funktioniert – der Fehler liegt im Paket (2026-11-08, 19:30)

**Zwei Erkenntnisse aus dem manuellen Test im Container Manager:**

1. **Der Fehler `Plain value cannot start with reserved character @`** kam
   davon, dass die **Quellvorlage** hochgeladen wurde. Dort steht
   `image: @IMAGE@`; der Platzhalter wird erst von `build-spk.sh` ersetzt.
   Die gebaute Datei liegt im SPK unter `compose/docker-compose.yaml` und
   enthält `image: ghcr.io/rf77/comfobox-gateway:1.0.2`. Falscher Alarm –
   kein Worker-Problem.

2. **Das Projekt ließ sich mit derselben Datei erfolgreich erstellen.** Damit
   ist bewiesen: Der `docker-project`-Worker ist auf dieser NAS
   **funktionsfähig**. Das Problem liegt ausschließlich in der
   Paket-Konfiguration (`conf/resource`), nicht in DSM, Docker, Compose, dem
   Image oder der Compose-Datei.

**Der entscheidende Unterschied (aus dem Dialog ablesbar):**

| | Funktionierender Fall | Unser Paket |
|---|---|---|
| Projektname | `test` | `comfobox-gateway` |
| **Pfad** | **`/docker`** (absolut, Share-Wurzel) | `compose` (relativ zum target) |
| Quelle | hochgeladene Datei liegt im **Share** | Datei liegt im **target** |

Der Worker arbeitet offenbar im Docker-Share. Bei `"path": "compose"` muss er
die Datei erst aus dem target dorthin bringen – und genau an dieser Stelle
scheitert er (`SYNOShareGet failed. synoerr=[0x1400]` im CM-Log).

**Neu `--inspect-project [name]`:** beschreibt ein funktionierendes Projekt
vollständig – Dateien im Share (Name, Inhalt, Rechte), die Datensätze unter
`etc/projects`, Container/Netzwerk/Volume mit ihren Labels – und vergleicht
es mit `comfobox-gateway`. Das ist die fehlende Ground Truth; danach wird
`conf/resource` angepasst statt geraten.

**Wichtig für den nächsten Versuch:** Das Testprojekt belegt den
**Containernamen `comfobox-gateway`** und **Port 8081**. Es muss vor dem
Paket-Test gelöscht werden, sonst scheitert der Start an einer Kollision.

---

## Nächste Session

**Phase 7** – Parallelbetrieb & Umschaltung (7.1–7.5): zwei Gateways
nebeneinander (unterschiedlicher `BaseTopic`), maschineller Payload-Vergleich
(`old` vs. `new`), dann Umschaltung auf `ComfoBox`
→ [phase-7.md](phase-7.md)
→ **Prompt:** [prompts/Prompt-Phase-7.md](prompts/Prompt-Phase-7.md)

> **Noch offen aus Phase 6:** 6.4 (Test auf der echten DS923+) – die Hardware
> steht hier nicht zur Verfügung. Ein CI-Release ist ebenfalls noch nicht
> gelaufen (kein Tag gepusht).

