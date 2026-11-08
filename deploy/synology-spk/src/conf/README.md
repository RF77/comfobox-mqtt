# `conf/privilege` und `conf/resource` – die Rechte des Pakets

> **Ins SPK wandern nur `conf/privilege` und `conf/resource` selbst.**
> `build-spk.sh` kopiert genau diese beiden Dateien in das Wurzelverzeichnis
> des SPK (nicht in das `package.tgz`) – das DSM liest sie nur dort.

## Warum es die Dateien braucht

Ab **DSM 7.0** erzwingt Synology für jedes Paket eine **niedrigere Berechtigung**
(*„Force lower privilege for package"*, siehe
[Breaking Changes in 7.0](https://help.synology.com/developer-guide/breaking_changes.html)):

> „All packages should provide `conf/privilege` with `package` in `run-as`
> explicitly. **Any privileged operation should be accomplished via resource
> worker.**"

Fehlt `conf/privilege`, gilt das Paket als **Root-Paket** und das Paketzentrum
verweigert die Installation **schon beim Hochladen**:

> *„ComfoBox Gateway kann nicht installiert werden, da es mit Root-Rechten
> ausgeführt wird. Pakete mit Root-Rechten können Befehle als Root-Benutzer
> ausführen und die Sicherheit Ihres Systems gefährden."*

## `conf/privilege` – die Identität des Pakets

```json
{
	"defaults": {
		"run-as": "package"
	},
	"username": "ComfoBoxGateway",
	"groupname": "ComfoBoxGateway"
}
```

| Feld | Bedeutung |
|---|---|
| `defaults.run-as` | **Pflicht.** `package` = der Paket-Benutzer. `root` ist in DSM 7 für Drittanbieter **verboten**. |
| `username` | Der Paket-Benutzer. Ohne diese Angabe nimmt DSM den Paketnamen. |
| `groupname` | Die Gruppe des Paket-Benutzers. Optional – das Plex-Paket auf derselben NAS setzt sie genauso. |

### ⛔ `ctrl-script` ist der Auslöser für **Fehler 319**

> *„invalid package privilege content“*

Die Datei mit `ctrl-script` und `"run-as": "root"` wurde vom DSM 7.4.1
**abgelehnt**. Belegt durch den Variantentest auf der NAS (DS923+, DSM 7.4.1):

| `ctrl-script` | Ergebnis |
|---|---|
| vorhanden (mit `run-as: root`) | **Fehler 319** – das Paket wird abgelehnt |
| nicht vorhanden | wird akzeptiert (die Installation läuft weiter) |

DSM 7 lässt Drittanbieter-Pakete **nicht** mit Root-Rechten laufen. Wer
privilegierte Aufgaben braucht, nutzt einen **Resource Worker** – siehe unten.

> **Merksatz:** Die Datei darf **nur** `defaults`, `username` und optional
> `groupname` enthalten. Alles andere wird abgelehnt.

### Das offizielle Muster

Aus dem [Synology Developer Guide, „Docker package"](https://help.synology.com/developer-guide/examples/compile_docker_package.html)
(DSM 7, amtliches Beispiel):

```json
{
    "defaults": {
        "run-as": "package"
    },
    "username": "Gitlab"
}
```

### Ground Truth: das Plex-Paket auf dieser NAS

Das auf derselben NAS **funktionierende** Plex-DSM7-Paket hat:

```json
{
    "defaults":{ "run-as": "package" },
    "username": "PlexMediaServer",
    "groupname": "PlexMediaServer"
}
```

plus eine `conf/resource`. Kein `ctrl-script`, kein `scripts/`-Ordner.

### Windows-Zeilenenden (CRLF)

Das DSM verträgt **kein** `\r` in der Datei. Die Referenzpakete entstehen mit
`jq` unter Linux und haben deshalb immer LF.

> **Windows-Falle:** mit `core.autocrlf=false` schreibt Git die CRLF
> **unverändert** in den Blob – auch der Linux-CI-Build packt sie dann so
> ins SPK. Deshalb konvertiert `build-spk.sh` **alle** Textdateien auf LF
> (`tr -d '\r'`), und `tools/check-spk.sh` prüft das.

---

## `conf/resource` – der Worker

```json
{
	"docker-project": {
		"projects": [
			{
				"name": "comfobox-gateway",
				"path": "compose",
				"build_params": {
					"build": false
				}
			}
		]
	}
}
```

Der **Docker-Project-Worker** (DSM 7.2.1+, Provider `ContainerManager>=1432`)
liest die Compose-Datei unter `target/compose/docker-compose.yaml`, legt daraus
das Projekt an und **startet bzw. stoppt es beim Start/Stopp des Pakets**.

### ⚠️ Der Worker schreibt die Datei um — und zerstört dabei Quotes

Das ist die **wichtigste Regel** für diese Datei und war die Ursache aller
fehlgeschlagenen Installationen bis 1.0.2:

Der Worker benutzt unsere Datei **nicht** unverändert. Er liest sie ein,
ergänzt zwei Einträge und schreibt sie als `docker-compose.admin.yaml` neu.
Dabei **verliert er die Anführungszeichen**:

```yaml
# unsere Datei                    # was der Worker daraus macht
group_add:                        group_add:
  - "20"                            - 20
```

Sein eigener Parser erwartet dort einen **String** und bricht ab:

```
1 error(s) decoding:
* 'group_add[0]' expected type 'string', got unconvertible type 'int', value: '20'
```

DSM meldet davon nur `fail compose up '<uuid>'` (2201) — ohne Container, ohne
Volume, ohne Logzeile. **Deshalb: keine numerischen Werte in
Anführungszeichen** in Feldern, die Strings erwarten. `tools/check-spk.sh`
lehnt `group_add` ab, `dsm-diagnose.sh` simuliert den Roundtrip.

### Was der Worker zusätzlich einträgt

```yaml
user: 286035:286035      # die UID/GID des Paket-Benutzers auf dieser NAS
cap_add:
  - NET_BIND_SERVICE
```

Der Container läuft auf dem NAS also **nicht** unter der UID aus dem Image
(`comfobox`, 1001), sondern unter der Kennung, die DSM dem Paket zuweist — und
die ist auf jeder NAS anders. Konsequenz für das Image: das Datenverzeichnis
muss für **jede** Kennung beschreibbar sein (siehe
`src/ComfoBox.Gateway/Dockerfile`, `chmod 0777`).

### ⚠️ Der Dateiname: zwei Quellen, zwei Aussagen

| Quelle | Aussage |
|---|---|
| Offizielle Entwicklerdoku *Docker Project Worker* | `path` = „directory of **compose.yaml**, relative path from target". Das Beispiel zeigt `django/compose.yaml` und `wordpress-mysql/compose.yml`. |
| `strings libdockerprojectgeneratorworker.so` auf der DS923+ | `docker-compose.yaml`, `docker-compose.yml`, `docker-compose.admin.yaml` |

**Die `strings`-Liste war abgeschnitten** (`strings | head -n 30`) – sie ist
kein Beweis dafür, dass `compose.yaml` fehlt. Damit ist der Name bis
Revision 0012 **nicht** geklärt.

Deshalb liefert `build-spk.sh` die Datei seit Revision 0013 unter **beiden**
Namen aus (byte-gleich, aus derselben Quelle erzeugt):

```
target/compose/docker-compose.yaml
target/compose/compose.yaml
```

Welchen Namen der Worker sucht, ist damit gleichgültig. `tools/check-spk.sh`
prüft, dass beide vorhanden und identisch sind.

### Drei Regeln für diese Datei

| Regel | Grund |
|---|---|
| Datei liegt unter **beiden** Namen vor | Die Quellen widersprechen sich (s. o.) – `build-spk.sh` erzeugt die Kopie. |
| `name` **klein** schreiben (`comfobox-gateway`) | Docker Compose akzeptiert als Projektnamen nur `[a-z0-9_-]`. Ein Name mit Großbuchstaben lässt das Anlegen scheitern – der Worker meldet dann nur `(fail)`. |
| `path` ist **relativ zum Zielverzeichnis** | `target/compose/docker-compose.yaml` → `"path": "compose"`. Steht dort etwas anderes, findet der Worker die Datei nicht (`tools/check-spk.sh` prüft beides gegeneinander). |
| `build_params.build = false` | Im Paket ist kein `Dockerfile` – der Build-Schritt entfällt. Er ist der einzige Schritt des Workers, der nichts tun kann; abgeschaltet kann er auch nicht scheitern. |

> **Der Projektname ist frei wählbar** – er muss nur eindeutig und stabil
> sein. Er bestimmt den Namen des Docker-Netzes (`<name>_default`) und das
> Label `com.docker.compose.project`. Der **Containername** steht separat in
> der Compose-Datei (`container_name: comfobox-gateway`) und bleibt gleich.

Der Worker arbeitet **als Root** – genau das ist der Punkt: Das Paket selbst
braucht keine Root-Rechte mehr, und der DSM akzeptiert es damit.

### Was der Worker kann – und was nicht

| Aufgabe | Lösung |
|---|---|
| Container starten/stoppen | Worker (`FROM_STARTUP_TO_HALT`) |
| Container anlegen/entfernen | Worker (`FROM_POSTINST_TO_PREUNINST`) |
| Compose-Datei | **statisch** unter `target/compose/docker-compose.yaml` |
| Docker-Socket | wird nicht gebraucht – der Worker spricht mit dem Container Manager |
| Rechte auf `var/` | `chmod 0777` in `postinst` (kein `chown` ohne Root) |

### Die Compose-Datei ist statisch

Der Worker startet das Projekt beim Start des Pakets – die Datei muss also
**vorher** existieren und darf nicht erst zur Laufzeit entstehen.

Deshalb steht der **Serial-Port nicht** in der Compose-Datei. Stattdessen:

* Der Container bindet `/dev` ein → er sieht **alle** seriellen Ports des NAS.
* Der Gateway wählt den Port zur Laufzeit aus `var/config.json` aus und
  öffnet ihn neu, wenn die Konfiguration geändert wird.

Damit muss der Container nach der Auswahl im Assistenten **nicht** neu
erstellt werden – und ein nachträglich angesteckter Adapter wird ohne
Neustart erkannt.

> **Auf einen festen Port umstellen:** in `docker-compose.yaml` die Zeile
> `- "/dev:/dev"` durch `- "/dev/ttyUSB0:/dev/ttyUSB0"` ersetzen. Dann passt
> der Port aber **nur**, wenn er schon beim Start des Pakets passt.

### Die Lifecycle-Skripte

Weil der Worker den Container startet und stoppt, sind die Skripte schlank:

| Skript | Aufgabe |
|---|---|
| `preinst` | Prüft nur die DSM-Version. **Kein Abbruch**, wenn der Container Manager fehlt – das Paketzentrum installiert ihn nach. |
| `postinst` | Legt `var/` an, ermittelt die seriellen Ports, schreibt die Startkonfiguration. |
| `preuninst` / `postuninst` | Der Worker erledigt das Aufräumen. `var/` bleibt **bewusst** erhalten (24-h-Schreibzähler!). |
| `start-stop-status` | `start`/`stop` tun nichts (der Worker arbeitet), `status` prüft den Container über `docker_inspect`. |

> **Fallstrick:** Die Skripte laufen als Paket-Benutzer **ohne** Root. Ein
> `set -u` zusammen mit einer ungesetzten Variablen (`SYNOPKG_PKGDEST` ist in
> der `preinst`-Phase nicht garantiert gesetzt) bricht das Skript mit
> „unbound variable“ ab – das war die Fehlermeldung
> *„failed to run preinst script“*. Deshalb lädt `common.sh` über den
> **relativen** Pfad und die Skripte arbeiten **ohne** `set -u`.
