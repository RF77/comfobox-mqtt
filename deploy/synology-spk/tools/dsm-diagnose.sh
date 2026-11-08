#!/bin/sh
# ---------------------------------------------------------------------------
# dsm-diagnose.sh – prüft ein Synology-Paket **auf der NAS** und liefert den
# echten Fehlercode des DSM.
#
# Warum es das gibt:
#   Die Fehlermeldung im Paketzentrum („kann nicht installiert werden, da es
#   mit Root-Rechten ausgeführt wird") sagt nur, dass DSM eine Bedingung
#   nicht erfüllt sieht – nicht welche. Der Weg über die Kommandozeile
#   `synopkg install` liefert dagegen ein **JSON mit Fehlercode und Text**.
#
# Aufruf (auf der NAS, per SSH):
#   sudo sh dsm-diagnose.sh /pfad/zum/paket.spk [--reset|--reset-volume|--install-fresh|--install-trace|--worker-forensics|--install|--worker|--compose-matrix|--app-smoke|--test|--log]
#
#   --reset    **Raeumt alle Reste fehlgeschlagener Versuche weg**: Paket
#              stoppen und deinstallieren (auch im Status "broken"), die
#              Verzeichnisse unter /var/packages und @appstore entfernen,
#              liegengebliebene Container und Netzwerke loeschen. Das
#              Docker-Volume "comfobox-data" bleibt erhalten (dort liegt der
#              24-h-Schreibzaehler). Danach ist die NAS wieder in dem
#              Zustand, den eine Erstinstallation erwartet.
#   --reset-volume
#              Wie --reset, entfernt **zusaetzlich das Daten-Volume**
#              'comfobox-data'. Noetig, wenn es noch das Projekt-Label eines
#              frueheren Projekts traegt - der Container Manager bricht dann
#              mit "fail compose up" (2201) ab. Der Inhalt wird vorher nach
#              /tmp/comfobox-volume-backup gesichert; ohne erfolgreiche
#              Sicherung wird nicht geloescht.
#   --install-fresh
#              --reset-volume und danach installieren. Das ist der
#              vollstaendige Neuanfang. Bricht ab, wenn das Volume nicht
#              wirklich verschwunden ist (sonst wiederholt sich der Fehler).
#   --install-trace
#              Wie --install-fresh, zeichnet aber zusaetzlich die
#              **docker events** waehrend der Installation auf und zeigt das
#              Projektverzeichnis im Docker-Share. Damit ist belegt, ob der
#              Worker einen Container erzeugt (`create`/`start`/`die`) oder
#              schon vorher scheitert.
#   --app-smoke
#              **Rauchtest ohne DSM-Worker.** Startet das Compose-Projekt wie
#              der Worker (CM-Binary, Docker-Share), wartet 15 s und zeigt
#              Zustand, Exit-Code und `docker logs`. Damit ist belegt, ob der
#              Gateway selbst laeuft - `compose up -d` allein beweist das
#              nicht (es liefert auch bei einem sofort sterbenden Container 0).
#   --worker-forensics
#              **Beobachtet den Worker waehrend der Installation.** Schneidet
#              in einer engen Schleife (< 50 ms) jeden docker/compose-Prozess
#              mit Argumenten mit, sucht die Dateien, die der Worker dabei
#              schreibt (dort liegt sein Projekt-Log) und prueft die
#              Registrierung des Docker-Share (`synoshare`). Das ist die
#              letzte fehlende Information, wenn `fail compose up` ohne
#              Container, ohne Volume und ohne Logzeile auftritt.
#   --inspect-project [name]
#              **Ground Truth.** Beschreibt ein FUNKTIONIERENDES Projekt im
#              Container Manager vollstaendig (Dateien im Docker-Share,
#              Datensaetze unter etc/projects, Container/Netzwerk/Volume mit
#              ihren Labels) und vergleicht es mit 'comfobox-gateway'.
#              Aendert nichts. Der Projektname ist optional.
#   --pkguser-probe
#              **Unter welcher Kennung arbeitet der Worker?** Der Worker legt
#              eine eigene `docker-compose.admin.yaml` mit `user: <paket-uid>`
#              an - er arbeitet also als Paket-Benutzer. Diese Probe prueft,
#              ob dieser Benutzer an den Docker-Daemon kommt und in den
#              Docker-Share schreiben darf, und startet die echte
#              Worker-Datei als root und als Paket-Benutzer (mit voller
#              Fehlermeldung). Aendert nichts an der Installation.
#   --install  **Empfohlen.** Macht zuerst --reset, installiert dann das
#              Paket und zeigt anschliessend, was der Resource-Worker
#              "docker-project" ins Syslog geschrieben hat. Genau dort steht
#              der Grund fuer "Acquire docker-project … (fail)".
#   --worker   **Emuliert den DSM-Worker "docker-project"** und zeigt die
#              volle Fehlermeldung von `docker compose up`. Das Paket wird
#              dabei NICHT installiert - nur die Compose-Datei wird
#              ausprobiert und danach wieder aufgeraeumt.
#   --compose-matrix
#              **A/B-Test der Compose-Datei.** Startet sie mit dem
#              Compose-Binary des Container Managers im Docker-Share - also
#              so, wie es der Worker tut - und laesst danach einzelne
#              Eintraege weg ('/dev:/dev', 'group_add', 'security_opt',
#              alles bis auf 'image'). Der erste Lauf, der funktioniert,
#              nennt den schuldigen Eintrag. Nuetzlich, wenn der Worker
#              `fail compose up` (2201) meldet, die Datei manuell aber
#              startet. Installiert nichts, laesst das Volume unangetastet.
#   --test     Probiert mehrere Varianten der privilege-Datei durch und
#              installiert die erste, die das DSM akzeptiert. Ein Fehler in
#              prepare_install aendert nichts an der NAS.
#   --log      Zeigt nur die Log-Auszuege des DSM (nichts wird installiert)
#
# Voraussetzung: nur BusyBox-Standardwerkzeuge (sh, tar, md5sum, sha256sum).
# Ohne --reset/--reset-volume/--install-fresh/--install/--test/--worker/
# --compose-matrix wird **nichts** veraendert.
#
# **Vor jedem Installationsversuch** laeuft eine Kollisionspruefung
# (`precheck_conflicts`): ein fremder Container mit demselben Namen, ein
# Volume 'comfobox-data' mit dem Projekt-Label eines anderen Projekts, ein
# belegter Port 8081 oder ein gleichnamiger Projekt-Datensatz des Container
# Managers lassen `compose up` **sofort** scheitern (2201) - ohne Container,
# ohne Volume, ohne weitere Logzeile. Genau das war der Fehler nach dem
# GUI-Test. Das Skript bricht in diesem Fall ab und nennt das Objekt.
# ---------------------------------------------------------------------------
set -u

SPK="${1:-}"
MODE="${2:-}"
# Manche Modi nehmen ein drittes Argument (z. B. den Projektnamen bei
# --inspect-project). Bei den anderen wird es ignoriert.
ARG3="${3:-}"
if [ -z "$SPK" ]; then
    echo "Aufruf: sh dsm-diagnose.sh <pfad/zum/paket.spk> [--install|--worker|--test|--log]" >&2
    exit 1
fi

if [ ! -f "$SPK" ]; then
    echo "FEHLER: Datei nicht gefunden: $SPK" >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# Docker finden
#
# **Wichtig:** Das Skript laeuft mit `sudo`. sudo setzt auf dem DSM einen
# **minimalen PATH** - der Container Manager ist dort nicht enthalten. Ohne
# diese Ergänzung findet das Skript kein `docker` und bricht mit
# "docker ist nicht im PATH" ab, obwohl Docker installiert ist.
#
# Gesucht wird an den ueblichen Stellen:
#   /var/packages/ContainerManager/target/usr/bin   (Container Manager)
#   /var/packages/Docker/target/usr/bin            (aelteres Docker-Paket)
#   /usr/local/bin                                 (System-Installation)
# ---------------------------------------------------------------------------
DOCKER_BIN=""
for _candidate in \
    /var/packages/ContainerManager/target/usr/bin/docker \
    /var/packages/Docker/target/usr/bin/docker \
    /usr/local/bin/docker \
    /usr/bin/docker
do
    if [ -x "$_candidate" ]; then
        DOCKER_BIN="$_candidate"
        break
    fi
done

if [ -n "$DOCKER_BIN" ]; then
    PATH="$(dirname "$DOCKER_BIN"):$PATH"
    export PATH
fi

# ---------------------------------------------------------------------------
# Hilfsfunktionen
# ---------------------------------------------------------------------------

# Die DSM-Version ausgeben (auf DSM gibt es /etc/VERSION).
show_dsm_version() {
    if [ -f /etc/VERSION ]; then
        cat /etc/VERSION
        echo ""
    fi
    echo "Kernel: $(uname -r 2>/dev/null)"
}

# Die Log-Auszüge des DSM. Hier steht der **genaue** Grund, warum das
# Paket abgelehnt wurde - das synopkg-JSON sagt nur "319".
show_logs() {
    echo "--- Log-Auszüge des DSM ---"
    found=0
    for f in /var/log/synopkg.log /var/log/packages/ComfoBoxGateway.log \
             /var/log/messages /var/log/synolog/synopkg.log; do
        if [ -f "$f" ]; then
            found=1
            echo ""
            echo ">> $f"
            grep -i -E "privilege|comfobox|319|prepare_install" "$f" 2>/dev/null \
                | tail -25 || echo "   (keine passenden Zeilen)"
        fi
    done
    if [ "$found" = "0" ]; then
        echo "(keine Log-Dateien gefunden)"
    fi
    echo ""
    # Das Log, in das synopkg selbst schreibt:
    if [ -f /var/log/synopkg.log ]; then
        echo ">> /var/log/synopkg.log (letzte 15 Zeilen)"
        tail -15 /var/log/synopkg.log 2>/dev/null || true
        echo ""
    fi
}

# ---------------------------------------------------------------------------
# Syslog des Resource-Workers
#
# **Hier steht der Grund fuer "Acquire docker-project … (fail)".**
# Der Worker (docker_project_generator_worker) schreibt seine Meldungen nach
# /var/log/messages - nicht in synopkg.log. Dort steht z.B.:
#
#   docker_project_worker/…/docker_project_generater_worker.cpp:254
#   rollback created projects: `comfobox`
#
# Der vorhandene Log-Auszug filtert auf "comfobox" - Fehlermeldungen ohne
# den Projektnamen (z.B. "compose file not found", "create project failed")
# gingen dabei verloren. Diese Funktion zeigt den ganzen Kanal.
# ---------------------------------------------------------------------------
show_worker_log() {
    echo ">> /var/log/messages - Meldungen des Resource-Workers"
    shown=0
    for f in /var/log/messages /var/log/synolog/synopkg.log; do
        [ -f "$f" ] || continue
        LINES=$(grep -i -E "docker_project|docker-project|container_?manager|project worker" "$f" 2>/dev/null | tail -n 30)
        if [ -n "$LINES" ]; then
            shown=1
            echo "   --- $f ---"
            echo "$LINES" | sed 's/^/   /'
        fi
    done
    if [ "$shown" = "0" ]; then
        echo "   (keine Meldungen des Workers gefunden)"
        echo "   Das ist selbst ein Befund: der Worker ist gescheitert, BEVOR er"
        echo "   mit Docker gesprochen hat - also an seiner Konfiguration"
        echo "   (conf/resource), am Provider (ContainerManager) oder am Zustand"
        echo "   des Pakets ('broken')."
    fi
    echo ""
}

# ---------------------------------------------------------------------------
# Docker-Zustand: liegen Reste herum?
#
# Ein liegengebliebener Container mit festem Namen (container_name) oder ein
# Projekt-Datensatz des Container Managers laesst den naechsten Versuch
# **sofort** scheitern - ohne jede weitere Logzeile. Deshalb wird das hier
# ausdruecklich geprueft.
# ---------------------------------------------------------------------------
show_container_state() {
    if ! command -v docker >/dev/null 2>&1; then
        echo ">> docker nicht im PATH - Zustand nicht pruefbar"
        echo ""
        return 0
    fi

    echo ">> Container (Name, Status, Projekt):"
    docker ps -a --filter "name=comfobox" \
        --format '   {{.Names}}  {{.Status}}  {{.Label "com.docker.compose.project"}}' 2>&1 \
        | head -n 10
    echo ""

    echo ">> Netzwerke:"
    docker network ls --filter "name=comfobox" --format '   {{.Name}}  {{.Driver}}' 2>&1 | head -n 10
    echo ""

    echo ">> Volumes:"
    docker volume ls --filter "name=comfobox" --format '   {{.Name}}  {{.Driver}}' 2>&1 | head -n 10
    echo ""

    # **Die Labels eines Volumes sind der entscheidende Hinweis.**
    # Compose legt an jedem selbst erzeugten Volume das Label
    # "com.docker.compose.project" ab. Gehoert das Volume zu einem anderen
    # Projekt als dem, das der Worker gerade anlegt, meldet `docker compose`
    # nur eine Warnung - der Container Manager verwaltet seine Projekte aber
    # selbst und bricht dann mit "fail compose up" (2201) ab.
    echo ">> Volume-Details (Projekt-Label):"
    for v in $(docker volume ls --filter "name=comfobox" --format '{{.Name}}' 2>/dev/null); do
        docker volume inspect "$v" 2>/dev/null \
            | sed -n 's/.*"com.docker.compose.project": "\([^"]*\)".*/   Label com.docker.compose.project = \1/p' \
            | head -n 1
        docker volume inspect "$v" 2>/dev/null \
            | sed -n 's|.*"Mountpoint": "\([^"]*\)".*|   \1|p' | head -n 1
        echo "   ($v)"
    done
    echo ""

    # Projekte des Container Managers. Der Worker legt dort seinen
    # Projekt-Datensatz ab ("/var/packages/ContainerManager/etc/projects/
    # <uuid>.lock"). Bleibt ein Datensatz eines gerollten Projekts liegen,
    # kann derselbe Name nicht neu angelegt werden.
    echo ">> Projekte des Container Managers:"
    CMP="/var/packages/ContainerManager/etc/projects"
    if [ -d "$CMP" ]; then
        ls -la "$CMP" 2>/dev/null | head -n 20 | sed 's/^/   /'
        for f in "$CMP"/*; do
            [ -f "$f" ] || continue
            case "$f" in
                *.json|*.conf|*.lock) ;;
                *) continue ;;
            esac
            echo "   --- $f ---"
            grep -o '"name"[^,]*' "$f" 2>/dev/null | head -n 5 | sed 's/^/      /'
        done
    else
        echo "   (Verzeichnis $CMP fehlt)"
    fi
    echo ""

    # Der Container Manager (Provider des Workers) muss laufen - ohne ihn
    # kann der Worker kein Projekt anlegen.
    if command -v synopkg >/dev/null 2>&1; then
        echo ">> Container Manager (Provider des Workers):"
        synopkg status ContainerManager 2>&1 | head -n 3 | sed 's/^/   /'
        echo ""
    fi

    # Container Manager legt Projekte in einem Ordner auf einem Volume ab.
    # Fehlt er, scheitert das Anlegen eines Projekts.
    echo ">> Projektordner des Container Managers (/volume*/docker):"
    found=0
    for d in /volume*/docker; do
        [ -d "$d" ] || continue
        found=1
        ls -ldn "$d" 2>/dev/null | sed 's/^/   /'
    done
    [ "$found" = "1" ] || echo "   (keiner gefunden - /volume1/docker fehlt)"
    echo ""
}

# ---------------------------------------------------------------------------
# Vergleichspakete: nutzt ein anderes Paket auf dieser NAS denselben Worker?
#
# Wenn ja, ist dessen conf/resource die **Ground Truth** - dann laesst sich
# Zeichen fuer Zeichen vergleichen. Deshalb wird sie hier mit ausgegeben.
# ---------------------------------------------------------------------------
show_reference_projects() {
    echo ">> Andere Pakete mit dem Worker 'docker-project':"
    found=0
    for f in /var/packages/*/conf/resource; do
        [ -f "$f" ] || continue
        grep -q '"docker-project"' "$f" 2>/dev/null || continue
        found=1
        echo ""
        echo "   $f"
        sed 's/^/      /' "$f" 2>/dev/null
        PKG=$(echo "$f" | sed 's|^/var/packages/||; s|/conf/resource$||')
        echo "      Zielverzeichnis:"
        ls -la "/var/packages/$PKG/target" 2>/dev/null | head -n 12 | sed 's/^/      /'
    done
    if [ "$found" = "0" ]; then
        echo "   (keins - es gibt auf dieser NAS kein Vergleichspaket)"
    fi
    echo ""
}

# ---------------------------------------------------------------------------
# Logs des Container Managers
#
# Der Resource-Worker steckt IM Container Manager
# (`worker/libdockerprojectgeneratorworker.so`). Seine ausfuehrliche Ausgabe -
# also die eigentliche Compose-Fehlermeldung - landet deshalb in dessen eigenem
# Log. In /var/log/messages steht nur die Kurzform:
#
#   project build fail: [`2201`] `fail compose up '<uuid>'`
#
# Genau diese Zeile beweist: der Worker scheitert **an compose up** - nicht an
# seiner Konfiguration. Der Grund steht nur hier.
# ---------------------------------------------------------------------------
show_cm_logs() {
    echo ">> Logs des Container Managers (dort steht der Compose-Fehler)"
    found=0
    for f in /var/log/packages/ContainerManager.log \
             /var/log/packages/Docker.log \
             /var/log/docker.log \
             /var/log/container_manager.log \
             /var/log/synolog/synolog.log \
             /var/log/synolog/synopkg.log
    do
        [ -f "$f" ] || continue
        LINES=$(grep -i -E "compose|comfobox|project (build|create)|2201|error|fail" \
            "$f" 2>/dev/null | tail -n 25)
        [ -n "$LINES" ] || continue
        found=1
        echo "   --- $f ---"
        echo "$LINES" | sed 's/^/   /'
    done
    if [ "$found" = "0" ]; then
        echo "   (keine passenden Log-Dateien gefunden)"
        echo "   Vorhandene Logs in /var/log/packages:"
        ls /var/log/packages/ 2>/dev/null | head -n 20 | sed 's/^/      /'
    fi
    echo ""
}

# ---------------------------------------------------------------------------
# Fehlertexte aus dem Worker-Binary
#
# Das Binary ist nicht quelloffen, enthaelt aber seine Meldungstexte. Ein Blick
# hinein zeigt, **welche** Faelle der Worker ueberhaupt unterscheidet - und
# damit, was "2201" bedeuten kann.
# ---------------------------------------------------------------------------
show_worker_binary_hints() {
    LIB="/var/packages/ContainerManager/target/worker/libdockerprojectgeneratorworker.so"
    echo ">> Meldungstexte des Workers ($LIB)"
    if [ ! -f "$LIB" ]; then
        echo "   (Binary nicht gefunden)"
        echo ""
        return 0
    fi
    # `strings` gibt es auf dem DSM nicht immer - deshalb rohe Byte-Folgen
    # ueber `tr` in Zeilen zerlegen.
    tr -c '[:print:]' '\n' < "$LIB" 2>/dev/null \
        | grep -i -E "compose|project|docker" \
        | grep -v -E '^[[:space:]]*$' \
        | sort -u | head -n 30 | sed 's/^/   /'
    echo ""
}

# ---------------------------------------------------------------------------
# Wo legt der Container Manager seine Projekte an, und womit ruft der Worker
# Compose auf?
# ---------------------------------------------------------------------------
docker_share_dir() {
    for d in /volume1/docker /volume2/docker /volume3/docker /volume4/docker \
             /volume5/docker /volume6/docker /volumeUSB1/usbshare/docker; do
        if [ -d "$d" ]; then
            printf '%s\n' "$d"
            return 0
        fi
    done
    return 1
}

cm_compose_bin() {
    for c in /var/packages/ContainerManager/target/usr/bin/docker-compose \
             /usr/local/bin/docker-compose; do
        if [ -x "$c" ]; then
            printf '%s\n' "$c"
            return 0
        fi
    done
    if command -v docker-compose >/dev/null 2>&1; then
        command -v docker-compose
        return 0
    fi
    return 1
}

# ---------------------------------------------------------------------------
# Projektname aus dem Paket lesen
#
# Der Name muss exakt dem aus `conf/resource` entsprechen - sonst prueft die
# Kollisionskontrolle an einem anderen Projekt vorbei.
# ---------------------------------------------------------------------------
spk_project_name() {
    _tmp=$(mktemp -d 2>/dev/null) || _tmp=/tmp/cbproj.$$
    if tar xf "$SPK" -C "$_tmp" conf/resource 2>/dev/null; then
        _p=$(sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' \
            "$_tmp/conf/resource" 2>/dev/null | head -n 1)
        rm -rf "$_tmp"
        if [ -n "$_p" ]; then
            printf '%s\n' "$_p"
            return 0
        fi
    else
        rm -rf "$_tmp"
    fi
    printf '%s\n' "comfobox-gateway"
}

# ---------------------------------------------------------------------------
# Kollisionen finden, BEVOR installiert wird
#
# **Warum das die wichtigste Vorabpruefung ist:**
# Ein einziges fremdes Objekt laesst `compose up` **sofort** scheitern - in
# unter einer Sekunde, ohne Image-Pull und ohne `container create`:
#
#   * ein Container mit demselben `container_name`
#   * ein Volume 'comfobox-data', das das Projekt-Label eines ANDEREN
#     Projekts traegt (z.B. aus einem GUI-Test)
#   * ein belegter Host-Port 8081
#   * ein Projekt-Datensatz des Container Managers mit demselben Namen
#
# Genau so sah der Fehler aus: `Acquire docker-project ... (fail)` nach 1 s,
# `project build fail: [2201] fail compose up '<uuid>'`, danach kein Container
# und kein Volume. Deshalb wird **nach** dem Aufraeumen geprueft - so sieht
# man sofort, ob das Aufraeumen wirklich gereicht hat.
#
# Rueckgabe: 0 = frei, 1 = Kollision vorhanden
# ---------------------------------------------------------------------------
precheck_conflicts() {
    PROJ=$(spk_project_name)
    HITS=0

    echo ">> Kollisionen pruefen (Projekt '$PROJ')"

    # 1) Container mit festem Namen
    if docker ps -a --filter "name=^/${PROJ}$" --format '{{.Names}}' 2>/dev/null \
        | grep -q .; then
        HITS=$((HITS + 1))
        echo "   KOLLISION: Container '$PROJ' existiert"
        docker ps -a --filter "name=^/${PROJ}$" \
            --format '      project={{.Label "com.docker.compose.project"}}  status={{.Status}}' \
            2>/dev/null | head -n 3 | sed 's/^/   /'
        echo "      -> docker rm -f $PROJ"
    fi

    # 2) Volume mit fremdem Projekt-Label
    if docker volume inspect "$VOLUME_NAME" >/dev/null 2>&1; then
        VL=$(docker volume inspect "$VOLUME_NAME" \
            --format '{{index .Labels "com.docker.compose.project"}}' 2>/dev/null)
        case "$VL" in
            ""|"<no value>"|"$PROJ") : ;;
            *)
                HITS=$((HITS + 1))
                echo "   KOLLISION: Volume '$VOLUME_NAME' gehoert Projekt '$VL'"
                echo "      -> sudo docker volume rm $VOLUME_NAME   (oder --install-fresh)"
                ;;
        esac
    fi

    # 3) Host-Port 8081
    PORT_HIT=$(docker ps --format '{{.Names}}  {{.Ports}}' 2>/dev/null \
        | grep -E ':8081->|0\.0\.0\.0:8081' | head -n 1)
    if [ -n "$PORT_HIT" ]; then
        HITS=$((HITS + 1))
        echo "   KOLLISION: Port 8081 belegt durch: $PORT_HIT"
    fi

    # 4) Projekt-Datensaetze des Container Managers
    CMP="/var/packages/ContainerManager/etc/projects"
    if [ -d "$CMP" ]; then
        for f in "$CMP"/*.config.json; do
            [ -f "$f" ] || continue
            N=$(sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' \
                "$f" 2>/dev/null | head -n 1)
            [ "$N" = "$PROJ" ] || continue
            HITS=$((HITS + 1))
            echo "   KOLLISION: CM-Projekt '$N' existiert ($(basename "$f"))"
            echo "      -> im Container Manager unter 'Projekt' entfernen"
        done
    fi

    if [ "$HITS" = "0" ]; then
        echo "   frei - nichts im Weg"
        echo ""
        return 0
    fi
    echo ""
    echo "   $HITS Kollision(en): eine Installation wuerde wieder mit 2201"
    echo "   (fail compose up) scheitern."
    echo ""
    return 1
}

# ---------------------------------------------------------------------------
# Ground Truth: Wie sieht ein FUNKTIONIERENDES Projekt aus?
#
# Der Test im Container Manager (Projekt "test" aus einer hochgeladenen
# docker-compose.yaml) war erfolgreich. Damit liegt ein Referenzfall vor:
# der Worker kann es - nur bei unserem Paket nicht.
#
# Diese Funktion beschreibt das Referenzprojekt vollstaendig:
#   * was im Docker-Share liegt (Dateiname, Inhalt, Rechte)
#   * welchen Datensatz der Container Manager angelegt hat
#   * wie Container, Netzwerk, Volume und Labels heissen
#
# Der Vergleich mit unserem Paket zeigt dann den Unterschied.
# ---------------------------------------------------------------------------
inspect_project() {
    WANT=${1:-}
    SHARE=$(docker_share_dir 2>/dev/null)

    echo ">> Docker-Share: ${SHARE:-nicht gefunden}"
    if [ -n "$SHARE" ]; then
        echo ""
        echo "   Inhalt:"
        ls -la "$SHARE" 2>/dev/null | sed 's/^/      /'
        echo ""
        for d in "$SHARE"/*/; do
            [ -d "$d" ] || continue
            echo "   --- $d ---"
            ls -la "$d" 2>/dev/null | tail -n +2 | sed 's/^/      /'
            for f in "$d"*.yaml "$d"*.yml; do
                [ -f "$f" ] || continue
                echo "      >>> Inhalt von $(basename "$f"):"
                grep -vE '^[[:space:]]*#' "$f" 2>/dev/null \
                    | grep -vE '^[[:space:]]*$' | head -n 45 | sed 's/^/         /'
            done
            echo ""
        done
    fi

    echo ">> Projekt-Datensaetze des Container Managers:"
    CMP="/var/packages/ContainerManager/etc/projects"
    if [ -d "$CMP" ]; then
        ls -la "$CMP" 2>/dev/null | sed 's/^/   /'
        for f in "$CMP"/*; do
            [ -f "$f" ] || continue
            echo "   --- $f ---"
            head -c 2000 "$f" 2>/dev/null | sed 's/^/      /'
            echo ""
        done
    else
        echo "   ($CMP fehlt)"
    fi
    echo ""

    echo ">> Docker-Projekte (Netzwerke mit Label com.docker.compose.project):"
    for n in $(docker network ls --format '{{.Name}}' 2>/dev/null); do
        P=$(docker network inspect "$n" --format \
            '{{index .Labels "com.docker.compose.project"}}' 2>/dev/null)
        [ -n "$P" ] && [ "$P" != "<no value>" ] && echo "   $n  ->  Projekt '$P'"
    done
    echo ""

    echo ">> Laufende/beendete Container mit Compose-Labels:"
    docker ps -a --format \
        '{{.Names}}  {{.Status}}  project={{.Label "com.docker.compose.project"}}  dir={{.Label "com.docker.compose.project.working_dir"}}  files={{.Label "com.docker.compose.project.config_files"}}' \
        2>&1 | head -n 15 | sed 's/^/   /'
    echo ""

    echo ">> Volumes mit Compose-Labels:"
    for v in $(docker volume ls --format '{{.Name}}' 2>/dev/null); do
        P=$(docker volume inspect "$v" --format \
            '{{index .Labels "com.docker.compose.project"}}' 2>/dev/null)
        [ -n "$P" ] && [ "$P" != "<no value>" ] && echo "   $v  ->  Projekt '$P'"
    done
    echo ""

    echo ">> Schreibt der Worker ins Paketverzeichnis? (Rechte!)"
    PKGDEST=/var/packages/ComfoBoxGateway/target
    if [ -d "$PKGDEST" ]; then
        ls -ldn "$PKGDEST" "$PKGDEST/compose" 2>/dev/null | sed 's/^/   /'
        echo "   Dateien in $PKGDEST/compose:"
        ls -lan "$PKGDEST/compose" 2>/dev/null | tail -n +2 | sed 's/^/      /'
        echo ""

        # **Die Datei, die der Worker SELBST erzeugt.**
        # Im Binary stehen die Namen `docker-compose.yaml`,
        # `docker-compose.yml` und **`docker-compose.admin.yaml`**. Letztere
        # legt der Worker beim Anlegen des Projekts an - sie gehoert nicht
        # ins Paket. Ihr Inhalt zeigt, was der Worker vorhat (und woran er
        # scheitert).
        for f in "$PKGDEST/compose/docker-compose.admin.yaml" \
                 "$PKGDEST/compose/docker-compose.yml"; do
            [ -f "$f" ] || continue
            echo "   >>> Inhalt von $(basename "$f") (vom Worker erzeugt):"
            cat "$f" 2>/dev/null | sed 's/^/      /'
            echo ""
        done
    else
        echo "   ($PKGDEST fehlt - Paket nicht installiert)"
    fi
    echo ""

    echo ">> Projekt-Logs des Container Managers (action.log):"
    if [ -d "$CMP" ]; then
        for f in "$CMP"/*.action.log; do
            [ -f "$f" ] || continue
            NAME=$(sed -n 's/.*"name":"\([^"]*\)".*/\1/p' "${f%.action.log}.config.json" 2>/dev/null)
            echo "   --- $(basename "$f")  (Projekt: ${NAME:-?}) ---"
            # Fehlerzeilen zuerst - die sind der Grund.
            if grep -qiE 'error|fail|cannot|denied|no such' "$f" 2>/dev/null; then
                grep -inE 'error|fail|cannot|denied|no such' "$f" 2>/dev/null \
                    | head -n 15 | cut -c1-180 | sed 's/^/      /'
            else
                echo "      (keine Fehlerzeilen)"
                tail -n 8 "$f" 2>/dev/null | cut -c1-180 | sed 's/^/      /'
            fi
            echo ""
        done
    fi
    echo ""

    if [ -n "$WANT" ] && [ -n "$SHARE" ]; then
        echo ">> Vergleich mit dem Referenzprojekt '$WANT':"
        REF="$SHARE/$WANT"
        if [ -d "$REF" ]; then
            echo "   INHALT von $REF:"
            ls -la "$REF" 2>/dev/null | sed 's/^/      /'
            echo ""
            echo "   Unterschiede zu unserem Projekt 'comfobox-gateway':"
            OUR="$SHARE/comfobox-gateway"
            if [ -d "$OUR" ]; then
                diff -r "$REF" "$OUR" 2>&1 | head -n 30 | sed 's/^/      /'
            else
                echo "      $OUR existiert NICHT (der Worker hat es nie angelegt)"
            fi
        else
            echo "   $REF existiert nicht"
        fi
        echo ""
    fi
}

# ---------------------------------------------------------------------------
# Probe: unter WELCHER Kennung laeuft der Worker, und was darf er?
#
# **Der Befund vom 08.11. (Revision 0013):** Der Worker legt neben unserer
# Datei eine eigene `docker-compose.admin.yaml` an - und darin stehen zwei
# Eintraege, die NICHT aus unserem Paket stammen:
#
#     user: 286035:286035          <- die UID/GID des Paket-Benutzers
#     cap_add:
#       - NET_BIND_SERVICE
#
# Die Datei gehoert **286035** (Modus 0600). Der Worker arbeitet also unter
# der Kennung des Pakets, nicht als root. Genau daraus folgen die beiden
# moeglichen Ursachen fuer `2201`:
#
#   1. Der Paket-Benutzer darf nicht mit dem Docker-Daemon sprechen
#      (/var/run/docker.sock gehoert root:root oder root:docker).
#   2. Der Paket-Benutzer darf nicht in den Docker-Share schreiben
#      (/volume3/docker gehoert root:root 0755) - dort legt der Worker das
#      Projektverzeichnis an.
#
# Beides scheitert **sofort**, ohne Image-Pull, ohne `container create` -
# genau wie im Mitschnitt vom 21:39 (kein einziges docker-Event).
#
# Diese Probe aendert nichts an der Installation. Sie probiert die Kette
# der Reihe nach aus und zeigt bei jedem Schritt die **vollstaendige**
# Fehlermeldung:
#
#   a) Kennung, Gruppen, Socket-Rechte
#   b) `docker ps` als Paket-Benutzer
#   c) Schreibtest im Docker-Share als Paket-Benutzer
#   d) die echte Worker-Datei (admin.yaml) per Compose - als root und als
#      Paket-Benutzer, jeweils im Share
#   e) dieselbe Datei mit `user: 1001:1001` - zeigt, ob die erzwungene
#      Paket-UID der naechste Fehler waere
#
# Danach bleiben keine Container zurueck.
# ---------------------------------------------------------------------------
pkguser_probe() {
    PKGUSER=ComfoBoxGateway
    SHARE=$(docker_share_dir 2>/dev/null)
    COMPOSE_BIN=$(cm_compose_bin 2>/dev/null)
    PKGDEST=/var/packages/ComfoBoxGateway/target

    echo ">> Paket-Benutzer"
    id "$PKGUSER" 2>&1 | sed 's/^/   /'
    echo "   (die UID hier muss der UID in docker-compose.admin.yaml entsprechen)"
    echo ""

    echo ">> Docker-Socket"
    for s in /var/run/docker.sock /run/docker.sock; do
        [ -e "$s" ] || continue
        ls -ln "$s" 2>/dev/null | sed 's/^/   /'
        echo "   Gruppen-Zugriff: $(stat -c '%A %U:%G' "$s" 2>/dev/null)"
    done
    echo ""

    echo ">> Darf der Paket-Benutzer mit Docker sprechen?"
    OUT=$(su -s /bin/sh -c 'docker ps -q >/dev/null 2>&1 && echo DOCKER-OK || echo DOCKER-FAIL' "$PKGUSER" 2>&1)
    echo "   $OUT"
    case "$OUT" in
        *DOCKER-FAIL*)
            echo "   -> Der Paket-Benutzer kommt NICHT an den Daemon. Das ist eine"
            echo "      der beiden Ursachen fuer 'fail compose up' (2201)."
            ;;
    esac
    echo ""

    if [ -z "$SHARE" ]; then
        echo ">> Kein Docker-Share gefunden - Probe endet hier."
        return 1
    fi

    echo ">> Rechte des Docker-Share"
    ls -ldn "$SHARE" 2>/dev/null | sed 's/^/   /'
    if command -v synoacltool >/dev/null 2>&1; then
        echo "   ACL:"
        synoacltool -get "$SHARE" 2>&1 | head -n 20 | sed 's/^/      /'
    fi
    echo ""

    echo ">> Darf der Paket-Benutzer in den Docker-Share schreiben?"
    OUT=$(su -s /bin/sh -c "touch '$SHARE/.cb-probe' && echo WRITE-OK || echo WRITE-FAIL" "$PKGUSER" 2>&1)
    echo "   $OUT"
    rm -f "$SHARE/.cb-probe" 2>/dev/null || true
    case "$OUT" in
        *WRITE-FAIL*)
            echo "   -> Ohne Schreibrecht kann der Worker kein Projektverzeichnis"
            echo "      anlegen. Das ist die zweite Ursache fuer 2201."
            ;;
    esac
    echo ""

    # **Die seriellen Geraete.** Der Container laeuft (durch die vom Worker
    # erzeugte `docker-compose.admin.yaml`) unter der Paket-Kennung, nicht
    # unter uid 1001 aus dem Image. Ob /dev/ttyUSB* zugaenglich ist, haengt
    # deshalb allein an den Rechten der Geraetedatei auf dem NAS.
    echo ">> Serielle Geraete (Rechte entscheiden ueber den RS485-Zugriff)"
    found=0
    for d in /dev/ttyUSB* /dev/ttyACM*; do
        [ -e "$d" ] || continue
        found=1
        ls -l "$d" 2>/dev/null | sed 's/^/   /'
        echo "      Gruppe (Name -> GID): $(stat -c '%G -> %g' "$d" 2>/dev/null)"
    done
    [ "$found" = "1" ] || echo "   (kein Adapter gesteckt - /dev/ttyUSB* fehlt)"
    echo "   Der Container laeuft unter der Paket-Kennung (siehe oben), nicht als"
    echo "   uid 1001. Lesen/Schreiben geht nur, wenn die Gruppe der Geraetedatei"
    echo "   passt ODER die Datei fuer alle offen ist."
    echo ""

    # Die Datei, die der Worker selbst erzeugt - sie ist die Wahrheit.
    SRC=""
    for c in /tmp/cb-compose-after-install/docker-compose.admin.yaml \
             "$PKGDEST/compose/docker-compose.admin.yaml" \
             "$PKGDEST/compose/docker-compose.yaml"; do
        [ -f "$c" ] || continue
        SRC="$c"
        break
    done
    if [ -z "$SRC" ]; then
        echo ">> Keine Compose-Datei gefunden - bitte zuerst --install-trace laufen lassen."
        return 1
    fi
    echo ">> Testdatei: $SRC"

    DIR="$SHARE/cb-probe"
    run_up() {
        WHO=$1
        FILE=$2
        rm -rf "$DIR"
        mkdir -p "$DIR"
        cp "$FILE" "$DIR/docker-compose.yaml"
        chmod -R 0777 "$DIR" 2>/dev/null || true
        echo ""
        echo "   --- compose up als '$WHO' ($(basename "$FILE")) ---"
        if [ "$WHO" = "root" ]; then
            ( cd "$DIR" && "$COMPOSE_BIN" -p cb-probe up -d ) 2>&1 \
                | tail -n 25 | sed 's/^/      /'
        else
            su -s /bin/sh -c "cd $DIR && $COMPOSE_BIN -p cb-probe up -d" "$WHO" 2>&1 \
                | tail -n 25 | sed 's/^/      /'
        fi
        sleep 5
        echo "      Zustand: $(docker inspect comfobox-gateway --format '{{.State.Status}} exit={{.State.ExitCode}}' 2>/dev/null || echo 'kein Container')"
        echo "      Log:"
        docker logs comfobox-gateway 2>&1 | tail -n 10 | sed 's/^/         /'
        ( cd "$DIR" && "$COMPOSE_BIN" -p cb-probe down --remove-orphans ) >/dev/null 2>&1 || true
        docker rm -f comfobox-gateway >/dev/null 2>&1 || true
    }

    echo ""
    echo ">> d) Die echte Worker-Datei - als root"
    run_up root "$SRC"

    echo ""
    echo ">> d) Die echte Worker-Datei - als Paket-Benutzer"
    run_up "$PKGUSER" "$SRC"

    echo ""
    echo ">> e) Dieselbe Datei mit user 1001:1001 (wie im Image)"
    ALT="/tmp/cb-probe-uid1001.yaml"
    sed 's/^[[:space:]]*user:.*/    user: "1001:1001"/' "$SRC" > "$ALT" 2>/dev/null
    if grep -q '1001:1001' "$ALT" 2>/dev/null; then
        run_up root "$ALT"
    else
        echo "   (uebersprungen - die Datei enthaelt keine 'user:'-Zeile)"
    fi

    rm -rf "$DIR" "$ALT" 2>/dev/null || true
    echo ""
    echo ">> Aufraeumen fertig. Volume 'comfobox-data' bleibt erhalten."
    echo ""
}

# ---------------------------------------------------------------------------
# A/B-Test: welcher Eintrag der Compose-Datei laesst `compose up` scheitern?
#
# Ausgangslage: Die ausgelieferte Datei laesst sich **manuell** starten (der
# Container wird "healthy"), im Worker scheitert sie aber mit 2201. Also wird
# hier so nah wie moeglich am Worker getestet:
#
#   * mit dem Compose-Binary des Container Managers (nicht mit dem Plugin)
#   * **im Docker-Share** (dort legt der Worker das Projekt an) - nicht in /tmp
#   * mit einem eigenen Projektnamen je Variante
#
# Danach werden einzelne Eintraege weggelassen - der erste Lauf, der
# funktioniert, nennt den schuldigen Eintrag.
#
# Das Volume 'comfobox-data' wird **nicht** geloescht (kein `down -v`).
# ---------------------------------------------------------------------------
compose_matrix() {
    SHARE=$(docker_share_dir)
    if [ -z "$SHARE" ]; then
        echo "FEHLER: kein Docker-Share (/volume*/docker) gefunden."
        return 1
    fi
    COMPOSE_BIN=$(cm_compose_bin)
    if [ -z "$COMPOSE_BIN" ]; then
        echo "FEHLER: docker-compose des Container Managers nicht gefunden."
        return 1
    fi

    WORK=$(mktemp -d 2>/dev/null) || WORK=/tmp/cbmatrix.$$
    mkdir -p "$WORK"
    if ! tar xf "$SPK" -C "$WORK" package.tgz 2>/dev/null \
       || ! tar xzf "$WORK/package.tgz" -C "$WORK" compose/docker-compose.yaml 2>/dev/null; then
        echo "FEHLER: compose/docker-compose.yaml laesst sich nicht aus dem SPK lesen"
        rm -rf "$WORK"
        return 1
    fi
    SRC="$WORK/compose/docker-compose.yaml"

    echo ">> Docker-Share  : $SHARE"
    echo ">> Compose-Binary: $COMPOSE_BIN"
    "$COMPOSE_BIN" version 2>&1 | head -n 2 | sed 's/^/   /'
    echo ""

    IMG=$(sed -n 's/^[[:space:]]*image:[[:space:]]*//p' "$SRC" | head -n 1)
    echo ">> Image: $IMG"
    if docker image inspect "$IMG" >/dev/null 2>&1; then
        echo "   -> lokal vorhanden (kein Pull noetig)"
    else
        echo "   -> NICHT lokal vorhanden (muesste gezogen werden)"
    fi
    echo ""

    for V in 1 2 3 4 5; do
        DIR="$SHARE/cbm-test-$V"
        rm -rf "$DIR"
        mkdir -p "$DIR"
        case "$V" in
            1)
                cp "$SRC" "$DIR/docker-compose.yaml"
                DESC="ausgeliefert (unveraendert)"
                ;;
            2)
                grep -v '^[[:space:]]*-[[:space:]]*"/dev:/dev"' "$SRC" > "$DIR/docker-compose.yaml"
                DESC="ohne '/dev:/dev' (Serial-Passthrough)"
                ;;
            3)
                awk '
                    /^[[:space:]]*group_add:[[:space:]]*$/ { skip = 1; next }
                    skip == 1 { skip = 0; if ($0 ~ /^[[:space:]]*-/) next }
                    { print }
                ' "$SRC" > "$DIR/docker-compose.yaml"
                DESC="ohne 'group_add'"
                ;;
            4)
                awk '
                    /^[[:space:]]*security_opt:[[:space:]]*$/ { skip = 1; next }
                    skip == 1 { skip = 0; if ($0 ~ /^[[:space:]]*-/) next }
                    { print }
                ' "$SRC" > "$DIR/docker-compose.yaml"
                DESC="ohne 'security_opt'"
                ;;
            5)
                printf 'services:\n  gateway:\n    image: %s\n    container_name: cbm-test-5\n' \
                    "$IMG" > "$DIR/docker-compose.yaml"
                DESC="minimal (nur image + container_name)"
                ;;
        esac

        # Eigener Containername je Variante - schliesst Namenskollisionen aus.
        sed -i "s|container_name: comfobox-gateway|container_name: cbm-test-$V|" \
            "$DIR/docker-compose.yaml" 2>/dev/null || true

        echo "=== Variante $V: $DESC ==="
        if ! docker compose -f "$DIR/docker-compose.yaml" config >/dev/null 2>&1; then
            echo "   (Hinweis: 'docker compose config' meldet einen Syntaxfehler)"
        fi
        ( cd "$DIR" && "$COMPOSE_BIN" -p "cbm-test-$V" up -d ) > "$DIR/up.log" 2>&1
        RC=$?
        if [ "$RC" = "0" ]; then
            echo "   OK   compose up: exit=0"
        else
            echo "   FEHLER compose up: exit=$RC"
        fi
        tail -n 15 "$DIR/up.log" | sed 's/^/   /'
        echo ""

        ( cd "$DIR" && "$COMPOSE_BIN" -p "cbm-test-$V" down --remove-orphans ) \
            >/dev/null 2>&1 || true
        docker rm -f "cbm-test-$V" >/dev/null 2>&1 || true
        docker network rm "cbm-test-${V}_default" >/dev/null 2>&1 || true
        rm -rf "$DIR"
    done

    echo ">> Aufgeraeumt. Das Volume 'comfobox-data' wurde NICHT angetastet."
    echo ""
    rm -rf "$WORK"
}

# ---------------------------------------------------------------------------
# conf/resource aus dem SPK - und passt der "path" zum Inhalt?
#
# Der Worker liest die Compose-Datei unter <ziel>/<path>/docker-compose.yaml.
# Stimmen conf/resource und package.tgz nicht zusammen, meldet er nur
# "(fail)". Der Check läuft hier **vor** der Installation.
# ---------------------------------------------------------------------------
show_resource() {
    TMP=$(mktemp -d 2>/dev/null) || TMP=/tmp/diag-res.$$
    mkdir -p "$TMP"

    if tar xf "$SPK" -C "$TMP" conf/resource 2>/dev/null; then
        sed 's/^/   /' "$TMP/conf/resource"
        echo ""
        PROJ_NAME=$(sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' \
            "$TMP/conf/resource" 2>/dev/null | head -n 1)
        PROJ_PATH=$(sed -n 's/.*"path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' \
            "$TMP/conf/resource" 2>/dev/null | head -n 1)
        echo ">> Projektname: ${PROJ_NAME:-(nicht gefunden)}"
        echo ">> Pfad        : ${PROJ_PATH:-(nicht gefunden)}"
    else
        echo "   >> conf/resource FEHLT im SPK - ohne Worker startet nichts."
        PROJ_PATH=""
    fi

    # Passt der Pfad zum Inhalt von package.tgz? Der Worker akzeptiert nur
    # drei Dateinamen - `compose.yaml` ist NICHT dabei.
    if [ -n "${PROJ_PATH:-}" ]; then
        tar xf "$SPK" -C "$TMP" package.tgz 2>/dev/null || true
        COMPOSE_FOUND=""
        if [ -f "$TMP/package.tgz" ]; then
            for name in docker-compose.yaml docker-compose.yml docker-compose.admin.yaml; do
                if tar tzf "$TMP/package.tgz" 2>/dev/null \
                   | sed 's|/$||' | grep -qx "${PROJ_PATH}/${name}"; then
                    COMPOSE_FOUND="$name"
                    break
                fi
            done
        fi

        if [ -n "$COMPOSE_FOUND" ]; then
            echo ">> OK: '$PROJ_PATH/$COMPOSE_FOUND' liegt genau dort, wo der Worker sucht."
        else
            echo ">> FEHLER: keine Datei mit einem Namen, den der Worker kennt!"
            echo "   Gesucht unter <ziel>/$PROJ_PATH/:"
            echo "     docker-compose.yaml | docker-compose.yml | docker-compose.admin.yaml"
            echo "   ('compose.yaml' erkennt nur Docker Compose selbst - nicht der Worker.)"
            echo "   Inhalt von package.tgz:"
            tar tzf "$TMP/package.tgz" 2>/dev/null | sed 's/^/      /' | head -n 20
        fi
    fi
    echo ""
    rm -rf "$TMP"
}

# Die package.tgz-Prüfsumme aus der INFO lesen (DSM prüft sie).
show_info() {
    TMP=$(mktemp -d 2>/dev/null) || TMP=/tmp/diag.$$
    mkdir -p "$TMP"
    if tar xf "$SPK" -C "$TMP" INFO 2>/dev/null; then
        grep -E '^(package|version|arch|os_min_ver|support_conf_folder|startable|ctl_stop|install_type|checksum)=' "$TMP/INFO"
    else
        echo ">> (nicht auspackbar)"
    fi
    rm -rf "$TMP"
}

# ---------------------------------------------------------------------------
# 1. Kopfdaten
# ---------------------------------------------------------------------------
echo "=============================================================="
echo " ComfoBox Gateway - Paketdiagnose auf der NAS"
echo "=============================================================="
echo ""

echo "--- 1. DSM-Version ---"
show_dsm_version
echo ""

echo "--- 2. Die Datei ---"
ls -l "$SPK"
echo ""
if command -v sha256sum >/dev/null 2>&1; then
    echo "SHA256: $(sha256sum "$SPK" | cut -d' ' -f1)"
else
    echo "SHA256: (sha256sum nicht vorhanden)"
fi
echo "MD5:    $(md5sum "$SPK" | cut -d' ' -f1)"
echo ""

echo "--- 3. Inhalt des SPK ---"
tar tf "$SPK" 2>/dev/null
echo ""

# Die Struktur von package.tgz ist entscheidend: das DSM entpackt die Datei
# DIREKT in das Zielverzeichnis. Deshalb muessen die Dateien auf der
# obersten Ebene liegen (bin/, compose/, image) - nicht unter "target/".
echo "--- 3a. Struktur von package.tgz ---"
TGZ_DIR=$(mktemp -d 2>/dev/null) || TGZ_DIR=/tmp/diag-tgz.$$
mkdir -p "$TGZ_DIR"
if tar xf "$SPK" -C "$TGZ_DIR" package.tgz 2>/dev/null \
   && tar tzf "$TGZ_DIR/package.tgz" 2>/dev/null; then
    if tar tzf "$TGZ_DIR/package.tgz" 2>/dev/null | grep -q '^target/'; then
        echo ">> WARNUNG: package.tgz enthaelt eine Ebene 'target/'!"
        echo "   Die Dateien landen dann eine Stufe zu tief (<ziel>/target/...)"
        echo "   und der DSM-Worker 'docker-project' findet die Compose-Datei nicht."
    else
        echo ">> OK: package.tgz hat die Dateien auf der obersten Ebene."
    fi
else
    echo ">> package.tgz nicht lesbar"
fi
rm -rf "$TGZ_DIR"
echo ""

TMP=$(mktemp -d 2>/dev/null) || TMP=/tmp/diag.$$
mkdir -p "$TMP"
if tar xf "$SPK" -C "$TMP" conf/privilege 2>/dev/null; then
    # CR-Prüfung POSIX-sicher (kein $'\r' - das ist eine Bash-Erweiterung
    # und BusyBox-ash kann sie nicht).
    if tr -d '\r' < "$TMP/conf/privilege" | cmp -s - "$TMP/conf/privilege"; then
        echo ">> conf/privilege: nur LF-Zeilenenden"
    else
        echo ">> conf/privilege enthaelt CR-Zeichen (CRLF)"
    fi
    if grep -q '"groupname"' "$TMP/conf/privilege"; then
        echo ">> conf/privilege enthaelt 'groupname'"
    fi
    if grep -q '"prestart"' "$TMP/conf/privilege" || grep -q '"prestop"' "$TMP/conf/privilege"; then
        echo ">> conf/privilege enthaelt 'prestart'/'prestop'"
    fi
    if grep -q '"ctrl-script"' "$TMP/conf/privilege"; then
        echo ">> conf/privilege enthaelt 'ctrl-script'"
    else
        echo ">> conf/privilege enthaelt KEIN 'ctrl-script'"
    fi
    echo ""
    echo "--- 4. conf/privilege ---"
    cat "$TMP/conf/privilege"
    echo ""
    if grep -A3 '"defaults"' "$TMP/conf/privilege" 2>/dev/null \
       | grep -q '"run-as"[[:space:]]*:[[:space:]]*"package"'; then
        echo ">> defaults.run-as = package  (korrekt)"
    else
        echo ">> WARNUNG: defaults.run-as ist nicht 'package'!"
    fi
else
    echo "--- 4. conf/privilege ---"
    echo ">> conf/privilege FEHLT - das Paket gilt als Root-Paket."
fi
rm -rf "$TMP"
echo ""

# ---------------------------------------------------------------------------
# 4b. Der Resource-Worker (conf/resource)
#
# Ohne diesen Abschnitt fehlte bei der Fehlersuche die haeufigste Ursache
# fuer "Acquire docker-project … (fail)": ein "path", der nicht zum Inhalt
# von package.tgz passt.
# ---------------------------------------------------------------------------
echo "--- 4b. conf/resource (Resource-Worker) ---"
show_resource

echo "--- 5. INFO (Auszug) ---"
show_info
echo ""

echo "--- 6. Bestehende Installation ---"
if command -v synopkg >/dev/null 2>&1; then
    synopkg status ComfoBoxGateway 2>&1 || echo "(nicht installiert)"
else
    echo "(synopkg nicht gefunden)"
fi
echo ""

# **Ein fehlgeschlagenes Update hinterlaesst das Paket im Status "broken"**
# (150). DSM 7.4 versucht dann beim naechsten Mal ein UPDATE statt einer
# Neuinstallation - und scheitert an "package does not have preupgrade /
# postupgrade script" (Fehler 261), obwohl das Paket in Ordnung ist.
# Deshalb wird vor dem Installationsversuch ein "broken"-Paket entfernt.
remove_broken_install() {
    command -v synopkg >/dev/null 2>&1 || return 0
    STATUS=$(synopkg status ComfoBoxGateway 2>/dev/null \
        | sed -n 's/.*"status":"\([a-z_]*\)".*/\1/p' | head -n 1)
    [ "$STATUS" = "broken" ] || return 0

    echo ">> Das Paket steht im Status 'broken' (eine Installation ist zuvor"
    echo "   fehlgeschlagen). DSM 7.4 wuerde jetzt ein UPDATE versuchen und"
    echo "   daran scheitern. Das alte Paket wird deshalb entfernt."
    synopkg uninstall ComfoBoxGateway >/dev/null 2>&1 || true
    sleep 2
    synopkg status ComfoBoxGateway 2>&1 || true
    echo ""
}

# ---------------------------------------------------------------------------
# Alles wegraeumen, was ein fehlgeschlagener Versuch hinterlaesst
#
# Warum das noetig ist:
#   Ein Paket, das nach einer fehlgeschlagenen Installation im Status
#   "broken" (150) steht, wird vom DSM beim naechsten Mal wie ein **Update**
#   behandelt - es laeuft also ein anderer Pfad als bei einer
#   Erstinstallation. Zusaetzlich bleiben Verzeichnisse und Docker-Objekte
#   liegen. Beides kann den Resource-Worker sofort scheitern lassen:
#
#     Acquire docker-project for ComfoBoxGateway when 0x0001 (fail)
#
#   ... und zwar ohne jede weitere Logzeile.
#
# Was **nicht** geloescht wird:
#   * das Docker-Volume "comfobox-data" - darin liegt der 24-h-Schreibzaehler
#     (EEPROM-Schutz). Es wird nur gemeldet.
#   * /var/packages/ComfoBoxGateway/var - wird nach /tmp gesichert und nach
#     einer erfolgreichen Installation zurueckgespielt (ports.json,
#     Sicherung des Schreibzaehlers).
# ---------------------------------------------------------------------------
KEEP_DIR="/tmp/comfobox-pkgvar-keep"

reset_all() {
    echo ">> Paket stoppen"
    synopkg stop ComfoBoxGateway >/dev/null 2>&1 || true

    echo ">> Paket deinstallieren (auch im Status 'broken')"
    remove_broken_install
    synopkg uninstall ComfoBoxGateway >/dev/null 2>&1 || true
    sleep 2

    # Alles, was unter var/ liegt, sichern: dort stehen die Portliste und
    # die Sicherung des 24-h-Schreibzaehlers.
    rm -rf "$KEEP_DIR"
    if [ -d "/var/packages/ComfoBoxGateway/var" ]; then
        mkdir -p "$KEEP_DIR"
        cp -a /var/packages/ComfoBoxGateway/var "$KEEP_DIR/var" 2>/dev/null \
            && echo "   var/ gesichert nach $KEEP_DIR/var"
    fi

    echo ">> Verzeichnisse entfernen"
    for d in /var/packages/ComfoBoxGateway \
             /usr/local/packages/@appstore/ComfoBoxGateway; do
        [ -d "$d" ] || continue
        if rm -rf "$d" 2>/dev/null; then
            echo "   entfernt: $d"
        else
            echo "   WARNUNG: $d liess sich nicht entfernen"
        fi
    done

    echo ">> Docker-Reste entfernen (Container, Netzwerke)"
    if command -v docker >/dev/null 2>&1; then
        for c in $(docker ps -a --format '{{.Names}}' 2>/dev/null | grep -i '^comfobox'); do
            if docker rm -f "$c" >/dev/null 2>&1; then
                echo "   Container entfernt: $c"
            else
                echo "   WARNUNG: Container $c liess sich nicht entfernen"
            fi
        done
        for n in $(docker network ls --format '{{.Name}}' 2>/dev/null | grep -i '^comfobox'); do
            if docker network rm "$n" >/dev/null 2>&1; then
                echo "   Netzwerk entfernt: $n"
            else
                echo "   Netzwerk $n ist noch in Benutzung (bleibt)"
            fi
        done
        echo "   Volume 'comfobox-data' bleibt erhalten (Schreibzaehler)."
    else
        echo "   (docker nicht im PATH - Docker-Reste bitte im Container Manager pruefen)"
    fi
    echo ""

    echo ">> Zustand nach dem Aufraeumen"
    synopkg status ComfoBoxGateway 2>&1 | head -n 3 | sed 's/^/   /'
    echo ""
}

# ---------------------------------------------------------------------------
# Das Daten-Volume ersetzen
#
# **Warum das noetig sein kann:**
# Compose legt an jedem selbst erzeugten Volume ein Label
# `com.docker.compose.project=<projektname>` ab. Ueberlebt das Volume einen
# Projektwechsel oder ein gerolltes Projekt, gehoert es weiter dem **alten**
# Namen. `docker compose` meldet dann nur
#
#   WARN volume "comfobox-data" already exists but was created for
#        project "comfobox" (expected "comfobox-gateway")
#
# und macht weiter - der Container Manager verwaltet seine Projekte jedoch
# selbst (`Docker::Project::WorkerProject`,
# `/var/packages/ContainerManager/etc/projects/<uuid>.lock`) und bricht beim
# Anlegen ab:
#
#   project build fail: [`2201`] `fail compose up '<uuid>'`
#
# Der Inhalt wird vorher nach /tmp gesichert; das Volume wird danach von
# Compose frisch - mit dem richtigen Label - angelegt.
# ---------------------------------------------------------------------------
VOLUME_NAME="comfobox-data"
VOLUME_BACKUP="/tmp/comfobox-volume-backup"

reset_volume() {
    if ! command -v docker >/dev/null 2>&1; then
        echo ">> docker nicht im PATH - Volume kann nicht geprueft werden"
        echo ""
        return 0
    fi

    if ! docker volume inspect "$VOLUME_NAME" >/dev/null 2>&1; then
        echo ">> Volume '$VOLUME_NAME' existiert nicht (nichts zu tun)"
        echo ""
        return 0
    fi

    echo ">> Volume '$VOLUME_NAME' vor dem Loeschen"
    docker volume inspect "$VOLUME_NAME" 2>/dev/null \
        | grep -E '"(Name|Mountpoint)"|com\.docker\.compose\.project|com\.synology' \
        | head -n 8 | sed 's/^/   /'
    echo ""

    # Inhalt sichern - ueber das Image des Gateways (es ist lokal vorhanden
    # und bringt eine Shell mit).
    #
    # **Zwei Fallen, die hier beide zugeschlagen haben:**
    #   1. Das Image laeuft als uid 1001 (`USER comfobox`). Ein Container
    #      mit dieser Kennung kann ein root-eigenes Verzeichnis (0755)
    #      nicht beschreiben - `cp` scheitert stumm, die Sicherung ist leer
    #      und das Volume bleibt liegen. Deshalb `-u 0:0`.
    #   2. `|| true` im cp-Befehl verschluckte den Fehler. Jetzt wird das
    #      Ergebnis geprueft, nicht die Ausgabe.
    IMG=""
    if [ -f "/var/packages/ComfoBoxGateway/target/image" ]; then
        IMG=$(head -n 1 "/var/packages/ComfoBoxGateway/target/image" 2>/dev/null)
    fi
    [ -n "$IMG" ] || IMG="ghcr.io/rf77/comfobox-gateway:1.0.2"

    rm -rf "$VOLUME_BACKUP"
    mkdir -p "$VOLUME_BACKUP"
    chmod 0777 "$VOLUME_BACKUP" 2>/dev/null || true
    echo ">> Inhalt sichern nach $VOLUME_BACKUP (Image: $IMG, als root im Container)"
    SAVE_LOG=$(docker run --rm -u 0:0 --entrypoint sh \
        -v "${VOLUME_NAME}:/data:ro" -v "${VOLUME_BACKUP}:/backup" \
        "$IMG" -c 'cp -a /data/. /backup/' 2>&1)
    SAVE_RC=$?
    SAVED_FILES=$(find "$VOLUME_BACKUP" -type f 2>/dev/null | wc -l | tr -d ' ')
    if [ "$SAVE_RC" = "0" ] && [ "$SAVED_FILES" != "0" ]; then
        echo "   OK - $SAVED_FILES Datei(en) gesichert"
    elif [ "$SAVE_RC" = "0" ]; then
        echo "   OK - das Volume war leer (nichts zu sichern)"
    else
        echo "   WARNUNG: Sicherung fehlgeschlagen (exit=$SAVE_RC) - Volume bleibt"
        printf '%s\n' "$SAVE_LOG" | head -n 8 | sed 's/^/      /'
        echo "   Bewusst ohne Sicherung loeschen:"
        echo "     sudo docker volume rm $VOLUME_NAME"
        echo ""
        return 1
    fi

    if docker volume rm "$VOLUME_NAME" >/dev/null 2>&1; then
        echo "   Volume entfernt (Compose legt es mit neuem Label an)"
    else
        echo "   WARNUNG: Volume liess sich nicht entfernen (noch in Benutzung?)"
    fi

    # **Verifizieren, nicht hoffen.** Genau hier lag der Fehler: das Volume
    # galt als "gesichert", wurde aber nie geloescht - und der Worker
    # scheiterte danach am alten Projekt-Label.
    if docker volume inspect "$VOLUME_NAME" >/dev/null 2>&1; then
        echo ">> FEHLER: Volume '$VOLUME_NAME' existiert weiterhin!"
        echo "   Pruefen: docker ps -a | grep $VOLUME_NAME"
        echo ""
        return 1
    fi
    echo ">> Volume '$VOLUME_NAME' ist weg (verifiziert)"
    echo ""

    # Projekt-Reste des Container Managers: ein liegengebliebener Datensatz
    # verhindert das erneute Anlegen desselben Projekts.
    CMP="/var/packages/ContainerManager/etc/projects"
    if [ -d "$CMP" ]; then
        echo ">> Projekt-Datensaetze des Container Managers pruefen"
        found=0
        for f in "$CMP"/*; do
            [ -e "$f" ] || continue
            case "$(basename "$f")" in
                *comfobox*) found=1 ;;
            esac
        done
        if [ "$found" = "1" ]; then
            echo "   Reste mit 'comfobox' im Namen werden entfernt:"
            for f in "$CMP"/*comfobox*; do
                [ -e "$f" ] || continue
                if rm -rf "$f" 2>/dev/null; then
                    echo "   entfernt: $f"
                else
                    echo "   WARNUNG: $f liess sich nicht entfernen"
                fi
            done
        else
            echo "   keine Reste gefunden"
        fi
        echo ""
    fi
}

# Nach einer erfolgreichen Installation: die gesicherten var-Dateien
# zurueckspielen (Portliste, Sicherung des Schreibzaehlers).
restore_kept_var() {
    [ -d "$KEEP_DIR/var" ] || return 0
    [ -d "/var/packages/ComfoBoxGateway" ] || return 0
    mkdir -p "/var/packages/ComfoBoxGateway/var"
    cp -a "$KEEP_DIR/var/." "/var/packages/ComfoBoxGateway/var/" 2>/dev/null || true
    echo ">> var/ aus $KEEP_DIR zurueckgespielt"
    rm -rf "$KEEP_DIR"
}

# ---------------------------------------------------------------------------
# Installation mit Mitschnitt
#
# Warum: Der Worker meldet nur `fail compose up '<uuid>'` und rollt danach
# zurueck - Container und Netzwerk sind weg, bevor man sie ansehen kann.
# `docker events` zeichnet dagegen auf, was **waehrend** der Installation
# passiert:
#
#   container create  → der Worker hat einen Container erzeugt
#   container start   → er ist gestartet
#   container die     → er ist gestorben (mit Exit-Code)
#
# Bleibt `create` aus, scheitert Compose **vor** dem Containerstart - dann
# liegt es an Volume, Netzwerk, Image oder an der Compose-Datei, die der
# Worker tatsaechlich benutzt.
#
# Zusaetzlich wird das Projektverzeichnis im Docker-Share gezeigt: Was der
# Worker dort ablegt, ist die Datei, mit der er wirklich arbeitet.
# ---------------------------------------------------------------------------
show_share_state() {
    SHARE=$(docker_share_dir 2>/dev/null)
    if [ -z "$SHARE" ]; then
        echo "   (kein Docker-Share gefunden)"
        return 0
    fi
    echo ">> Inhalt von $SHARE:"
    ls -la "$SHARE" 2>/dev/null | head -n 20 | sed 's/^/   /'
    echo ""
    for d in "$SHARE"/*comfobox*; do
        [ -d "$d" ] || continue
        echo ">> Projektverzeichnis $d (was der Worker dort abgelegt hat):"
        ls -la "$d" 2>/dev/null | head -n 20 | sed 's/^/   /'
        for f in "$d"/*.yaml "$d"/*.yml; do
            [ -f "$f" ] || continue
            echo "   --- $(basename "$f") ---"
            grep -v '^[[:space:]]*#' "$f" 2>/dev/null \
                | grep -v '^[[:space:]]*$' | head -n 40 | sed 's/^/      /'
        done
        echo ""
    done
}

# ---------------------------------------------------------------------------
# Container-Wache
#
# Der Worker rollt bei einem Fehler sofort zurueck - Container, Netzwerk und
# Volume sind weg, bevor man sie ansehen kann. `docker events` allein zeigt
# nur, DASS ein Container starb (siehe Mitschnitt: create → start → die in
# unter einer Sekunde), aber nicht warum.
#
# Diese Wache haengt am Event-Strom und greift im Moment von `create` zu:
# sie kopiert `docker inspect` (Image, Befehl, Labels, Mounts) und
# `docker logs` (die Ausgabe des Prozesses) heraus, solange der Container
# noch existiert.
# ---------------------------------------------------------------------------
CONTAINER_WATCH=""

start_container_watch() {
    WATCH_DIR=$1
    CONTAINER_WATCH="$WATCH_DIR/containers.log"
    : > "$CONTAINER_WATCH"

    (
        docker events \
            --filter 'type=container' \
            --filter 'event=create' --filter 'event=start' \
            --filter 'event=die' --filter 'event=destroy' \
            --format '{{.Action}} {{.Actor.ID}} exit={{.Actor.Attributes.exitCode}}' 2>/dev/null \
        | while read -r ACT ID EXIT rest; do
            # Das Verzeichnis kann waehrend des Laufs verschwinden (das Skript
            # raeumt am Ende auf). Ohne diese Pruefung schreibt die Schleife
            # in eine geloeschte Datei und ueberschwemmt die Ausgabe mit
            # "No such file or directory" - genau das stand im Lauf vom 08.11.
            [ -d "$WATCH_DIR" ] || break
            {
                echo "================================================================"
                echo "Event: $ACT   ($EXIT)"
                echo "----------------------------------------------------------------"
                # `docker inspect -f` statt JSON+Parsing: die Feldnamen sind
                # stabil, und die Labels verraten die **tatsaechlich benutzte**
                # Compose-Datei. Genau das ist hier die Frage.
                docker inspect -f 'Name:       {{.Name}}
Image:      {{.Config.Image}}
ImageId:    {{.Image}}
Entrypoint: {{json .Config.Entrypoint}}
Cmd:        {{json .Config.Cmd}}
User:       {{.Config.User}}
State:      {{.State.Status}}  exit={{.State.ExitCode}}  err={{.State.Error}}

Labels:
  compose.project        = {{index .Config.Labels "com.docker.compose.project"}}
  compose.service        = {{index .Config.Labels "com.docker.compose.service"}}
  compose.project.working_dir   = {{index .Config.Labels "com.docker.compose.project.working_dir"}}
  compose.project.config_files  = {{index .Config.Labels "com.docker.compose.project.config_files"}}
  compose.version        = {{index .Config.Labels "com.docker.compose.version"}}

Mounts:
{{range .Mounts}}  {{.Type}}  {{.Source}} -> {{.Destination}}
{{end}}' "$ID" 2>&1
                echo "--- docker logs ($ACT) ---"
                docker logs "$ID" 2>&1 | tail -n 40
                echo ""
            } >> "$CONTAINER_WATCH" 2>&1
        done
    ) &
    WATCH_PID=$!
}

stop_container_watch() {
    [ -n "${WATCH_PID:-}" ] || return 0
    kill "$WATCH_PID" 2>/dev/null || true
    WATCH_PID=""
    # **Wichtig:** `$!` ist nur das Subshell. Der darin gestartete Prozess
    # `docker events` ueberlebt das `kill` und schreibt beim naechsten Ereignis
    # weiter - nach dem Aufraeumen in eine geloeschte Datei:
    #
    #     dsm-diagnose.sh: line 977: /tmp/tmp.xxxx/containers.log: No such file
    #
    # Genau diese Zeilen standen im Lauf vom 08.11. Deshalb wird der Prozess
    # hier ausdruecklich beendet.
    pkill -f 'docker events --filter' 2>/dev/null || true
}

# ---------------------------------------------------------------------------
# Rauchtest der Anwendung - ohne DSM-Worker
#
# **Die Luecke im bisherigen A/B-Test:** `docker compose up -d` liefert
# Exit-Code 0, auch wenn der Container eine Sekunde spaeter stirbt. Der Test
# hat deshalb nur bewiesen, dass Compose die Datei akzeptiert - nicht, dass
# der Gateway laeuft.
#
# Dieser Test startet das Projekt mit dem Compose-Binary des Container
# Managers im Docker-Share (wie der Worker), wartet und zeigt dann:
#   * ob der Container noch laeuft,
#   * seinen Exit-Code,
#   * seine Ausgabe (`docker logs`) - dort steht der Absturzgrund.
# ---------------------------------------------------------------------------
app_smoke() {
    SHARE=$(docker_share_dir 2>/dev/null)
    COMPOSE_BIN=$(cm_compose_bin 2>/dev/null)
    if [ -z "$SHARE" ] || [ -z "$COMPOSE_BIN" ]; then
        echo "FEHLER: Docker-Share oder Compose-Binary nicht gefunden."
        return 1
    fi

    WORK=$(mktemp -d 2>/dev/null) || WORK=/tmp/cbsmoke.$$
    mkdir -p "$WORK"
    tar xf "$SPK" -C "$WORK" package.tgz 2>/dev/null \
        && tar xzf "$WORK/package.tgz" -C "$WORK" compose/docker-compose.yaml 2>/dev/null || {
            echo "FEHLER: compose/docker-compose.yaml nicht lesbar"; rm -rf "$WORK"; return 1; }

    DIR="$SHARE/cbm-smoke"
    rm -rf "$DIR"
    mkdir -p "$DIR"
    cp "$WORK/compose/docker-compose.yaml" "$DIR/docker-compose.yaml"
    sed -i "s|container_name: comfobox-gateway|container_name: cbm-smoke|" "$DIR/docker-compose.yaml" 2>/dev/null || true

    echo ">> Starte wie der Worker: $COMPOSE_BIN im Share $DIR"
    echo ""
    ( cd "$DIR" && "$COMPOSE_BIN" -p cbm-smoke up -d ) 2>&1 | sed 's/^/   /'
    echo ""

    echo ">> 15 s warten - stirbt der Gateway, passiert es jetzt"
    sleep 15

    STATE=$(docker inspect cbm-smoke --format '{{.State.Status}}' 2>/dev/null)
    EXITC=$(docker inspect cbm-smoke --format '{{.State.ExitCode}}' 2>/dev/null)
    RESTARTS=$(docker inspect cbm-smoke --format '{{.RestartCount}}' 2>/dev/null)
    HEALTH=$(docker inspect cbm-smoke --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}n/a{{end}}' 2>/dev/null)

    echo ">> Zustand nach 15 s:"
    echo "   Status=$STATE  ExitCode=$EXITC  Restarts=$RESTARTS  Health=$HEALTH"
    echo ""

    if [ "$STATE" = "running" ]; then
        echo "   -> Der Gateway LAEUFT. Die Anwendung ist in Ordnung; der Fehler"
        echo "      liegt dann ausschliesslich im Zusammenspiel mit dem Worker."
    else
        echo "   -> Der Gateway laeuft NICHT. Das ist die Ursache."
    fi
    echo ""

    echo ">> Ausgabe des Containers (docker logs):"
    docker logs cbm-smoke 2>&1 | tail -n 60 | sed 's/^/   /'
    echo ""

    echo ">> Aufraeumen"
    ( cd "$DIR" && "$COMPOSE_BIN" -p cbm-smoke down --remove-orphans ) 2>&1 \
        | tail -n 5 | sed 's/^/   /'
    docker rm -f cbm-smoke >/dev/null 2>&1 || true
    rm -rf "$DIR" "$WORK"
    echo ""
}

# ---------------------------------------------------------------------------
# Was der Worker in target/compose hinterlassen hat
#
# **Die Datei, die der WORKER selbst erzeugt.** Im Worker-Binary stehen drei
# Dateinamen: `docker-compose.yaml`, `docker-compose.yml` und
# `docker-compose.admin.yaml`. Die letzte legt der Worker beim Anlegen des
# Projekts SELBST an. Im Lauf vom 08.11. um 19:07 lag sie mit 953 Byte in
# `<target>/compose/` - waehrend unsere Datei 5364 Byte hat. Der Worker
# benutzt also moeglicherweise NICHT unsere Datei, sondern eine von ihm
# erzeugte Kurzfassung.
#
# Diese Kurzfassung ist die einzige noch fehlende Information: wenn sie
# ungueltig ist, scheitert `compose up` sofort - ohne Image-Pull, ohne
# Netzwerk, ohne `container create`. Genau das zeigt der Mitschnitt.
#
# Sie muss DIREKT nach dem Fehlschlag kopiert werden: der naechste
# `--install*`-Lauf beginnt mit `reset_all` und loescht
# `/var/packages/ComfoBoxGateway`.
# ---------------------------------------------------------------------------
show_worker_compose_leftovers() {
    PKGDEST=/var/packages/ComfoBoxGateway/target
    [ -d "$PKGDEST/compose" ] || return 0

    echo ">> Was der Worker in $PKGDEST/compose hinterlassen hat:"
    ls -lan "$PKGDEST/compose" 2>/dev/null | tail -n +2 | sed 's/^/   /'
    echo ""
    for f in "$PKGDEST"/compose/docker-compose.admin.yaml \
             "$PKGDEST"/compose/docker-compose.yml; do
        [ -f "$f" ] || continue
        echo "   >>> $(basename "$f") ($(wc -c < "$f" 2>/dev/null | tr -d ' ') Byte):"
        sed 's/^/      /' "$f" 2>/dev/null
        echo ""
    done
    rm -rf /tmp/cb-compose-after-install
    cp -a "$PKGDEST/compose" /tmp/cb-compose-after-install 2>/dev/null \
        && echo "   (Kopie bleibt unter /tmp/cb-compose-after-install)"
    echo ""
}

install_with_trace() {    echo ">> Ausgangszustand"
    echo "   Volume '$VOLUME_NAME':"
    if docker volume inspect "$VOLUME_NAME" >/dev/null 2>&1; then
        docker volume inspect "$VOLUME_NAME" 2>/dev/null \
            | sed -n 's/.*"com.docker.compose.project": "\([^"]*\)".*/      Label com.docker.compose.project = \1/p'
    else
        echo "      (existiert nicht)"
    fi
    echo ""

    TRACE_DIR=$(mktemp -d 2>/dev/null) || TRACE_DIR=/tmp/cbtrace.$$
    mkdir -p "$TRACE_DIR"

    echo ">> Container-Wache starten (greift bei 'create' zu, vor dem Rollback)"
    start_container_watch "$TRACE_DIR"
    sleep 2

    T0=$(date '+%Y-%m-%dT%H:%M:%S')
    echo ">> synopkg install (ab $T0)"
    synopkg install "$SPK" 2>&1
    echo ""

    show_worker_compose_leftovers

    # Etwas warten, damit auch 'die'/'destroy' noch erfasst werden.
    sleep 4
    stop_container_watch
    T1=$(date '+%Y-%m-%dT%H:%M:%S')

    echo ">> docker events zwischen $T0 und $T1:"
    docker events --since "$T0" --until "$T1" \
        --filter 'type=container' --filter 'type=volume' \
        --filter 'type=network' --filter 'type=image' \
        --format '   {{.Type}}  {{.Action}}  {{.Actor.ID}}' 2>&1 \
        | cut -c1-90 | head -n 60
    echo ""

    echo ">> Was die Wache mitgeschnitten hat:"
    if [ -s "$CONTAINER_WATCH" ]; then
        head -n 120 "$CONTAINER_WATCH" | sed 's/^/   /'
    else
        echo "   (nichts - es wurde kein Container erzeugt)"
    fi
    echo ""

    echo ">> Volume '$VOLUME_NAME' nach dem Lauf:"
    if docker volume inspect "$VOLUME_NAME" >/dev/null 2>&1; then
        docker volume inspect "$VOLUME_NAME" 2>/dev/null \
            | sed -n 's/.*"com.docker.compose.project": "\([^"]*\)".*/   Label com.docker.compose.project = \1/p'
        echo "   (existiert)"
    else
        echo "   (existiert nicht - der Worker hat es also nicht angelegt)"
    fi
    echo ""

    echo ">> Alle Images auf der NAS (welches der Worker benutzt):"
    docker images 2>&1 | head -n 15 | sed 's/^/   /'
    echo ""

    show_share_state

    rm -rf "$TRACE_DIR"
}

# ---------------------------------------------------------------------------
# Forensik: WAS ruft der Worker auf?
#
# Der Worker meldet nur `fail compose up '<uuid>'` und rollt zurueck. Weder
# Container noch Volume entstehen - Compose wird also gar nicht wirksam.
# Die entscheidende Frage lautet: **welchen Befehl** fuehrt der Worker aus,
# und **welche Ausgabe** bekommt er?
#
# Diese Funktion beobachtet den Prozessbaum waehrend der Installation in
# einer engen Schleife (< 50 ms Takt). Ein `docker compose ...`-Aufruf
# erscheint damit in der Liste - mit Argumenten. Zusaetzlich werden die
# Dateien gesucht, die der Worker **waehrend** des Laufs schreibt (dort
# liegt sein Projekt-Log), und die Registrierung des Docker-Share geprueft
# (`SYNOShareGet failed` im CM-Log ist ein Kandidat fuer die Ursache).
# ---------------------------------------------------------------------------
worker_forensics() {
    PSLOG=/tmp/cb-pswatch.log
    : > "$PSLOG"

    echo ">> Prozess-Beobachter starten (Takt < 50 ms)"
    (
        i=0
        while [ "$i" -lt 200 ]; do
            ps -ef 2>/dev/null | grep -E 'docker|compose' | grep -v 'grep' >> "$PSLOG"
            sleep 0.05 2>/dev/null || sleep 1
            i=$((i + 1))
        done
    ) &
    PS_PID=$!
    sleep 1

    echo ">> synopkg install (Beobachter laeuft)"
    synopkg install "$SPK" 2>&1 | tail -n 4 | sed 's/^/   /'
    echo ""

    sleep 2
    kill "$PS_PID" 2>/dev/null || true
    wait "$PS_PID" 2>/dev/null || true

    echo ">> Aufgerufene docker/compose-Prozesse (dedupliziert):"
    if [ -s "$PSLOG" ]; then
        sort -u "$PSLOG" | grep -vE 'cb-pswatch|while|sleep 0\.05' \
            | head -n 25 | cut -c1-200 | sed 's/^/   /'
    else
        echo "   (keine - der Worker hat kein docker/compose gestartet)"
        echo "   Das bedeutet: er scheitert, BEVOR er Compose aufruft."
    fi
    echo ""

    echo ">> Dateien, die der Worker angelegt/geaendert hat (letzte 3 Minuten):"
    found=0
    for d in /var/packages/ContainerManager \
             /volume3/@appconf/ContainerManager \
             /volume3/docker \
             /volume2/docker \
             /volume1/docker; do
        [ -d "$d" ] || continue
        hits=$(find "$d" -mmin -3 2>/dev/null | head -n 15)
        if [ -n "$hits" ]; then
            found=1
            echo "   --- $d ---"
            printf '%s\n' "$hits" | sed 's/^/      /'
        fi
    done
    [ "$found" = "1" ] || echo "   (keine - der Worker hat nichts geschrieben)"
    echo ""

    echo ">> Docker-Share korrekt registriert?"
    if command -v synoshare >/dev/null 2>&1; then
        echo "   --- synoshare --get docker ---"
        synoshare --get docker 2>&1 | head -n 12 | sed 's/^/      /'
        echo "   --- alle Shares (Name + Pfad) ---"
        synoshare --enum ALL 2>&1 | head -n 25 | sed 's/^/      /'
    else
        echo "   (synoshare nicht gefunden)"
    fi
    echo ""

    echo ">> Docker-Socket und Rechte:"
    ls -l /var/run/docker.sock 2>/dev/null | sed 's/^/   /'
    ls -l /run/docker.sock 2>/dev/null | sed 's/^/   /'
    echo "   Paketbenutzer:"
    id ComfoBoxGateway 2>&1 | head -n 2 | sed 's/^/      /'
    echo ""

    echo ">> Container-Manager-Log (ungefiltert, letzte 30 Zeilen):"
    for f in /var/log/packages/ContainerManager.log /var/log/docker.log; do
        [ -f "$f" ] || continue
        echo "   --- $f ---"
        tail -n 30 "$f" 2>/dev/null | sed 's/^/      /'
    done
    echo ""

    rm -f "$PSLOG"
}

# Die Compose-Datei mit Docker pruefen - so sieht man einen Syntaxfehler,
# bevor das DSM den Worker aufruft.
validate_compose() {
    if ! command -v docker >/dev/null 2>&1; then
        echo "(docker nicht gefunden - Compose-Datei wird nicht geprueft)"
        return 0
    fi
    WORK2=$(mktemp -d 2>/dev/null) || WORK2=/tmp/diag-cmp.$$
    mkdir -p "$WORK2"
    if tar xf "$SPK" -C "$WORK2" package.tgz 2>/dev/null \
       && tar xzf "$WORK2/package.tgz" -C "$WORK2" compose/docker-compose.yaml 2>/dev/null; then
        CF="$WORK2/compose/docker-compose.yaml"
        echo ">> Compose-Datei:"
        sed 's/^/   /' "$CF"
        echo ""
        echo ">> docker compose config (Syntaxpruefung):"
        if docker compose -f "$CF" config >/dev/null 2>&1; then
            echo "   OK - die Compose-Datei ist gueltig."
        else
            docker compose -f "$CF" config 2>&1 | head -n 20 | sed 's/^/   /'
            echo "   WARNUNG: Die Compose-Datei hat einen Fehler!"
        fi
        echo ""

        # **Simulation des Worker-Roundtrips - der eigentliche Test.**
        #
        # Der Worker liest die Datei ein, ergaenzt `user:` und `cap_add:` und
        # schreibt sie neu. Dabei gehen die Anfuehrungszeichen verloren. Genau
        # daran ist Revision 0013 gescheitert:
        #
        #     group_add:
        #       - "20"     ->  - 20     (YAML-Zahl!)
        #
        #    1 error(s) decoding:
        #    * 'group_add[0]' expected type 'string', got unconvertible
        #      type 'int', value: '20'
        #
        # Diese Pruefung entfernt die Anfuehrungszeichen genauso und laesst
        # Compose die Datei erneut lesen. Was hier scheitert, scheitert auch
        # bei der Installation.
        echo ">> Simulation des Worker-Roundtrips (ohne Anfuehrungszeichen):"
        SIM="$WORK2/worker-sim.yaml"
        sed -E 's/^([[:space:]]*-[[:space:]]*)"([^"]*)"([[:space:]]*)$/\1\2\3/' "$CF" > "$SIM"
        if docker compose -f "$SIM" config >/dev/null 2>&1; then
            echo "   OK - auch ohne Anfuehrungszeichen gueltig."
        else
            docker compose -f "$SIM" config 2>&1 | head -n 20 | sed 's/^/   /'
            echo "   WARNUNG: Der Worker-Roundtrip macht die Datei ungueltig!"
            echo "            Numerische Werte in Anfuehrungszeichen (z. B. group_add)"
            echo "            verlieren ihre Quotes und werden zu YAML-Zahlen."
        fi
    fi
    rm -rf "$WORK2"
    echo ""
}

# ---------------------------------------------------------------------------
# Worker-Emulation
#
# Der DSM-Worker "docker-project" macht genau drei Dinge:
#   1. das Projekt anlegen (Verzeichnis + Netzwerk)
#   2. `docker compose up`   <-- HIER scheitert die Installation
#   3. bei Fehler: zurueckrollen ("rollback created projects")
#
# Das DSM meldet nur "fail compose up '<uuid>'" - die eigentliche
# Fehlermeldung von Docker wird **nicht** weitergegeben. Dieser Abschnitt
# macht exakt Schritt 2 nach und zeigt die volle Ausgabe.
#
# Das Paket wird dabei NICHT installiert. Am Ende wird das Projekt wieder
# entfernt (`compose down`), damit keine Reste bleiben.
# ---------------------------------------------------------------------------
worker_test() {
    echo "--- Worker-Emulation: docker compose up ---"
    echo ""

    if ! command -v docker >/dev/null 2>&1; then
        echo "FEHLER: docker ist nicht im PATH."
        echo "       Auf dem DSM liegt der Docker-Binary unter"
        echo "       /var/packages/ContainerManager/target/usr/bin/docker"
        echo "       (Ist der Container Manager nicht installiert, bitte ihn"
        echo "        ueber das Paketzentrum nachinstallieren.)"
        return 1
    fi
    echo ">> docker: $(command -v docker)"

    # Das Paket entpacken - genau wie das DSM es tut:
    #   SPK entpacken -> package.tgz -> Zielverzeichnis
    WORK3=$(mktemp -d 2>/dev/null) || WORK3=/tmp/diag-worker.$$
    mkdir -p "$WORK3/target"
    if ! tar xf "$SPK" -C "$WORK3" package.tgz 2>/dev/null; then
        echo "FEHLER: package.tgz nicht lesbar"
        rm -rf "$WORK3"
        return 1
    fi
    if ! tar xzf "$WORK3/package.tgz" -C "$WORK3/target" 2>/dev/null; then
        echo "FEHLER: package.tgz nicht entpackbar"
        rm -rf "$WORK3"
        return 1
    fi

    CF_DIR="$WORK3/target/compose"
    if [ ! -f "$CF_DIR/docker-compose.yaml" ]; then
        echo "FEHLER: compose/docker-compose.yaml fehlt in package.tgz"
        rm -rf "$WORK3"
        return 1
    fi

    # Der Projektname steht in conf/resource. Er wird hier gelesen, damit
    # die Emulation exakt denselben Projektnamen benutzt wie der Worker -
    # sonst entstehen beim Test Reste unter einem anderen Namen.
    PROJECT=""
    if tar xf "$SPK" -C "$WORK3" conf/resource 2>/dev/null; then
        PROJECT=$(sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' \
            "$WORK3/conf/resource" 2>/dev/null | head -n 1)
    fi
    [ -n "$PROJECT" ] || PROJECT="comfobox"
    echo ">> Projektname (aus conf/resource): $PROJECT"

    # 1) Die Umgebung, in der der Worker laeuft
    echo ">> Docker-Versionen:"
    docker version --format '   Server: {{.Server.Version}}' 2>&1 | head -n 3 | sed 's/^/   /'
    docker compose version 2>&1 | head -n 3 | sed 's/^/   /'
    echo ""

    # 2) Das Image - isoliert pulling (zeigt Netzwerk-/Auth-Fehler)
    if [ -f "$WORK3/target/image" ]; then
        IMAGE_NAME=$(head -n 1 "$WORK3/target/image" 2>/dev/null)
    else
        IMAGE_NAME=""
    fi
    if [ -n "$IMAGE_NAME" ]; then
        echo ">> Image-Pull (isoliert): $IMAGE_NAME"
        docker pull "$IMAGE_NAME" 2>&1 | tail -n 15 | sed 's/^/   /'
        echo ""
    fi

    # 3) Die Ports pruefen - ein belegter Port laesst "compose up" scheitern
    if command -v netstat >/dev/null 2>&1; then
        echo ">> Belegte Ports (8081/8080):"
        netstat -an 2>/dev/null | grep -E ':(8081|8080) ' | sed 's/^/   /' || echo "   (keine)"
        echo ""
    fi

    # 4) DER entscheidende Schritt: compose up
    #
    # WICHTIG: `docker compose up -d` liefert **Exit-Code 0**, auch wenn der
    # Container sofort wieder abstuerzt! Der DSM-Worker wartet dagegen, bis
    # der Container laeuft - genau dieser Unterschied hat den echten Fehler
    # bisher versteckt.
    #
    # Deshalb wird hier **ohne** `--wait` gestartet und danach von Hand auf
    # den Zustand gewartet. Das funktioniert mit **jeder** Compose-Version
    # (das `--wait`-Flag gibt es erst ab v2.1) und zeigt zusaetzlich, wie
    # sich der Zustand entwickelt - man sieht eine Neustartschleife.
    echo ">> docker compose up (der Schritt, an dem die Installation scheitert):"
    ( cd "$CF_DIR" && docker compose -p "$PROJECT" up -d 2>&1 ) > "$WORK3/up.log" 2>&1
    RC=$?
    tail -n 40 "$WORK3/up.log" | sed 's/^/   /'
    echo ""
    echo "   (Exit-Code von 'compose up': $RC)"
    echo ""

    # 5) Auf den Zustand warten - so macht es der DSM-Worker.
    #
    # Bis zu 60 Sekunden warten. Zwischen den Pruefungen wird der Zustand
    # ausgegeben - so erkennt man eine Neustartschleife sofort.
    echo ">> Warte auf den Container (max. 60 s):"
    i=0
    while [ "$i" -lt 12 ]; do
        STATE=$(docker inspect comfobox-gateway --format '{{.State.Status}}' 2>/dev/null)
        HEALTH=$(docker inspect comfobox-gateway --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}n/a{{end}}' 2>/dev/null)
        RESTARTS=$(docker inspect comfobox-gateway --format '{{.RestartCount}}' 2>/dev/null)
        echo "   [$((i * 5)) s] Status=$STATE Health=$HEALTH Restarts=$RESTARTS"
        case "$STATE" in
            running)
                if [ "$HEALTH" = "healthy" ] || [ "$HEALTH" = "n/a" ]; then
                    break
                fi
                ;;
            exited|dead)
                echo "   -> der Container ist gestorben."
                break
                ;;
        esac
        i=$((i + 1))
        sleep 5
    done
    echo ""

    # 6) Was ist daraus geworden?
    echo ">> Container:"
    docker ps -a --filter "name=comfobox" 2>&1 | head -n 10 | sed 's/^/   /'
    echo ""

    echo ">> Zustand (Restart-Zaehler, Exit-Code, Health):"
    docker inspect comfobox-gateway \
        --format '   RestartCount: {{.RestartCount}}
   State:          {{.State.Status}}
   ExitCode:       {{.State.ExitCode}}
   Error:          {{.State.Error}}
   Health:         {{if .State.Health}}{{.State.Health.Status}}{{else}}(kein Healthcheck aktiv){{end}}' 2>&1 \
        | head -n 10 | sed 's/^/   /'
    echo ""

    echo ">> Container-Logs (stdout/stderr):"
    ( cd "$CF_DIR" && docker compose -p "$PROJECT" logs --tail 40 2>&1 ) | tail -n 40 | sed 's/^/   /'
    echo ""

    echo ">> Log-Dateien im Volume (falls der Container bis zum Log kam):"
    docker run --rm -v comfobox-data:/data alpine \
        sh -c 'ls -la /data 2>/dev/null; echo "---"; ls -la /data/logs 2>/dev/null; echo "---"; tail -n 20 /data/logs/*.jsonl 2>/dev/null' 2>&1 \
        | head -n 40 | sed 's/^/   /'
    echo ""

    echo ">> Rechte des Datenverzeichnisses im Volume:"
    docker run --rm -v comfobox-data:/data alpine \
        sh -c 'ls -ldn /data /data/logs 2>/dev/null' 2>&1 | head -n 5 | sed 's/^/   /'
    echo ""

    # 7) Aufraeumen - es sollen keine Reste auf der NAS bleiben.
    #    Das Volume bleibt dabei erhalten (kein -v) - es enthaelt den
    #    24-h-Schreibzaehler.
    echo ">> Aufraeumen:"
    ( cd "$CF_DIR" && docker compose -p "$PROJECT" down --remove-orphans 2>&1 ) | tail -n 5 | sed 's/^/   /'
    echo ""

    rm -rf "$WORK3"
}

# ---------------------------------------------------------------------------
# 7. Log-Auszuege und Zustand (immer - hier steht der genaue Grund)
# ---------------------------------------------------------------------------
show_logs
show_worker_log
show_cm_logs
show_container_state
show_reference_projects

# ---------------------------------------------------------------------------
# 8. Installationsversuch
# ---------------------------------------------------------------------------
if [ "$MODE" = "--reset" ]; then
    echo "--- 8. Aufraeumen (kein Installationsversuch) ---"
    reset_all
    show_worker_log
elif [ "$MODE" = "--reset-volume" ]; then
    echo "--- 8. Aufraeumen inkl. Daten-Volume ---"
    echo "Der Inhalt des Volumes wird vorher nach $VOLUME_BACKUP gesichert."
    echo ""
    reset_all
    reset_volume
    echo ">> Danach installieren mit:"
    echo "   sudo sh $0 $SPK --install"
    echo ""
elif [ "$MODE" = "--install-fresh" ]; then
    echo "--- 8. Aufraeumen inkl. Daten-Volume ---"
    reset_all
    if ! reset_volume; then
        echo ""
        echo ">>> ABBRUCH: Das Daten-Volume liess sich nicht entfernen."
        echo "    Eine Installation wuerde wieder am alten Projekt-Label scheitern."
        echo "    Loeschen Sie es bewusst und starten Sie erneut:"
        echo "      sudo docker volume rm $VOLUME_NAME"
        echo "      sudo sh $0 $SPK --install-fresh"
        echo ""
        exit 1
    fi
    echo "--- 9. Kollisionen pruefen ---"
    echo "Ein fremder Container, ein fremdes Volume-Label oder ein belegter"
    echo "Port 8081 lassen 'compose up' sofort scheitern (2201)."
    echo ""
    if ! precheck_conflicts; then
        echo ">>> ABBRUCH: Erst die Kollision beseitigen, dann erneut starten."
        exit 1
    fi
    echo "--- 10. Compose-Datei pruefen ---"
    validate_compose
    echo "--- 11. Installationsversuch ---"
    install_with_trace
    echo "--- 12. Ergebnis ---"
    STATE=$(synopkg status ComfoBoxGateway 2>&1)
    echo "$STATE" | head -n 3 | sed 's/^/   /'
    echo ""
    case "$STATE" in
        *broken*|*install_corruption*|"") : ;;
        *) restore_kept_var ;;
    esac
    echo ""
    echo "--- 12. Was der Resource-Worker gemeldet hat ---"
    show_worker_log
    show_container_state
    show_logs
elif [ "$MODE" = "--install-trace" ]; then
    echo "--- 8. Aufraeumen inkl. Daten-Volume ---"
    reset_all
    if ! reset_volume; then
        echo ""
        echo ">>> ABBRUCH: Das Daten-Volume liess sich nicht entfernen."
        echo "      sudo docker volume rm $VOLUME_NAME"
        echo ""
        exit 1
    fi
    echo "--- 9. Kollisionen pruefen ---"
    echo "Ein fremder Container, ein fremdes Volume-Label oder ein belegter"
    echo "Port 8081 lassen 'compose up' sofort scheitern (2201)."
    echo ""
    if ! precheck_conflicts; then
        echo ">>> ABBRUCH: Erst die Kollision beseitigen, dann erneut starten."
        exit 1
    fi
    echo "--- 10. Installation mit docker-events-Mitschnitt ---"
    echo "Der Mitschnitt zeigt, ob der Worker ueberhaupt einen Container erzeugt."
    echo ""
    install_with_trace
    echo "--- 11. Ergebnis ---"
    STATE=$(synopkg status ComfoBoxGateway 2>&1)
    echo "$STATE" | head -n 3 | sed 's/^/   /'
    echo ""
    echo "--- 12. Was der Resource-Worker gemeldet hat ---"
    show_worker_log
elif [ "$MODE" = "--inspect-project" ]; then
    echo "--- Ground Truth: wie sieht ein funktionierendes Projekt aus? ---"
    echo ""
    inspect_project "$ARG3"
elif [ "$MODE" = "--pkguser-probe" ]; then
    echo "--- Unter welcher Kennung arbeitet der Worker? ---"
    echo ""
    pkguser_probe
elif [ "$MODE" = "--worker-forensics" ]; then
    echo "--- 8. Aufraeumen (ohne Daten-Volume) ---"
    reset_all
    echo "--- 9. Kollisionen pruefen ---"
    if ! precheck_conflicts; then
        echo ">>> ABBRUCH: Erst die Kollision beseitigen, dann erneut starten."
        exit 1
    fi
    echo "--- 10. Forensik: welchen Compose-Aufruf macht der Worker? ---"
    echo "Es wird installiert; das Paket landet danach im Status 'broken'."
    echo ""
    worker_forensics
    echo "--- 11. Ergebnis ---"
    synopkg status ComfoBoxGateway 2>&1 | head -n 3 | sed 's/^/   /'
    echo ""
    show_worker_log
elif [ "$MODE" = "--install" ]; then
    echo "--- 8. Reste fehlgeschlagener Versuche entfernen ---"
    reset_all

    echo "--- 9. Kollisionen pruefen ---"
    if ! precheck_conflicts; then
        echo ">>> ABBRUCH: Erst die Kollision beseitigen. Traegt das Daten-Volume"
        echo "    das Label eines alten Projekts, hilft nur '--install-fresh'."
        exit 1
    fi

    echo "--- 10. Compose-Datei pruefen ---"
    validate_compose

    echo "--- 11. Installationsversuch ---"
    echo "Die Ausgabe ist JSON und enthaelt den echten Fehlercode."
    echo ""
    synopkg install "$SPK" 2>&1
    echo ""
    show_worker_compose_leftovers
    echo "--- 11. Ergebnis ---"
    STATE=$(synopkg status ComfoBoxGateway 2>&1)
    echo "$STATE" | head -n 3 | sed 's/^/   /'
    echo ""
    # Erst wenn das Paket wirklich installiert ist, die gesicherten
    # var-Dateien zurueckspielen.
    case "$STATE" in
        *broken*|*install_corruption*|"") : ;;
        *) restore_kept_var ;;
    esac
    echo ""
    echo "--- 12. Was der Resource-Worker gemeldet hat ---"
    show_worker_log
    show_container_state
    show_logs
elif [ "$MODE" = "--app-smoke" ]; then
    echo "--- 8. Rauchtest der Anwendung (ohne DSM-Worker) ---"
    echo "Es wird nichts installiert. Der Container wird nach 15 s entfernt."
    echo ""
    app_smoke
elif [ "$MODE" = "--compose-matrix" ]; then
    echo "--- 8. A/B-Test der Compose-Datei (wie der Worker) ---"
    echo "Es wird nichts installiert. Das Volume bleibt unangetastet."
    echo ""
    compose_matrix
    echo "--- 9. Fehlertexte und Logs ---"
    show_worker_binary_hints
    show_cm_logs
elif [ "$MODE" = "--worker" ]; then
    echo "--- 8. Worker-Emulation (docker compose up) ---"
    echo "Das Paket wird dabei NICHT installiert."
    echo ""
    validate_compose
    worker_test
    echo "--- 9. Log-Auszüge des DSM ---"
    show_logs
elif [ "$MODE" = "--log" ]; then
    echo "--- 8. Installationsversuch (ausgelasst) ---"
    echo "Nur die Logs wurden gelesen - es wurde nichts installiert."
    echo ""
fi

# ---------------------------------------------------------------------------
# 9. Variantentest: welche privilege-Datei akzeptiert das DSM?
# ---------------------------------------------------------------------------
#
# Der Fehler 319 ("invalid package privilege content") sagt nur, dass der
# Inhalt nicht passt - nicht welcher Teil. Dieser Abschnitt probiert die
# möglichen Formen **durch**, indem er das Paket entpackt, die
# privilege-Datei ersetzt, neu packt und `synopkg install` aufruft.
#
# Der Test läuft in "prepare_install" - ein Fehler dort ändert **nichts**
# an der NAS. Eine erfolgreiche Variante installiert das Paket.
#
# Die Varianten folgen der Ground Truth aus dem echten, funktionierenden
# Plex-DSM7-Paket auf dieser NAS:
#
#   conf/privilege:  { "defaults": {"run-as": "package"},
#                      "username": "PlexMediaServer",
#                      "groupname": "PlexMediaServer" }
#   conf/resource:   { "video-driver": {}, "port-config": "..." }
#   → **kein ctrl-script**
#
# Unser Paket hat dagegen ein ctrl-script (mit run-as: root) und **keine**
# conf/resource. Beides sind die Kandidaten für den Fehler 319.
#
# Reihenfolge: erst die kleinste Änderung, die installierbar macht – eine
# funktionierende Variante **installiert** das Paket und beendet den Test.
if [ "$MODE" = "--test" ]; then
    echo "--- 9. Variantentest (privilege-Datei x conf/resource) ---"
    echo "Jede Variante wird installiert; das DSM meldet den Fehlercode."
    echo "Der erste Treffer installiert das Paket - danach ist Schluss."
    echo "Ein Fehler in dieser Phase aendert nichts an der NAS."
    echo ""

    WORK=$(mktemp -d 2>/dev/null) || WORK=/tmp/vtest.$$
    mkdir -p "$WORK/pkg" "$WORK/orig"
    SPK_DIR=$(CDPATH= cd -- "$(dirname -- "$SPK")" && pwd) || exit 1
    SPK_NAME=$(basename -- "$SPK")

    # Das Original für die Kontrollvariante sichern
    ( cd "$WORK/orig" && tar xpf "$SPK_DIR/$SPK_NAME" conf/privilege ) 2>/dev/null
    ORIG_PRIV="$WORK/orig/conf/privilege"

    # $1 = Zieldatei, $2 = Variante
    write_priv() {
        case "$2" in
            1|8)
                # wie ausgeliefert (ctrl-script mit root)
                cp "$ORIG_PRIV" "$1"
                ;;
            2|6)
                # minimal: nur defaults
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t}\n}\n' > "$1"
                ;;
            3|5)
                # ohne ctrl-script, mit username
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-ComfoBoxGateway"\n}\n' > "$1"
                ;;
            4)
                # ohne ctrl-script, mit username und groupname (Plex-Stil)
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-ComfoBoxGateway",\n\t"groupname": "sc-ComfoBoxGateway"\n}\n' > "$1"
                ;;
            7)
                # ohne ctrl-script, username klein geschrieben
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-comfoboxgateway"\n}\n' > "$1"
                ;;
        esac
    }

    # $1 = Nummer der Variante
    variant_label() {
        case "$1" in
            1) echo "wie ausgeliefert (ctrl-script) + conf/resource" ;;
            2) echo "nur defaults + conf/resource" ;;
            3) echo "ohne ctrl-script (username) + conf/resource" ;;
            4) echo "ohne ctrl-script (username+groupname) + conf/resource" ;;
            5) echo "ohne ctrl-script (username), OHNE resource" ;;
            6) echo "nur defaults, OHNE resource" ;;
            7) echo "ohne ctrl-script (username klein) + conf/resource" ;;
            8) echo "wie ausgeliefert, OHNE resource (Kontrolle)" ;;
        esac
    }

    # $1 = Nummer, $2 = ja|nein (conf/resource dazufügen)
    variant_resource() {
        if [ "$2" = "ja" ]; then
            printf '{\n}\n' > "$WORK/pkg/conf/resource"
        else
            rm -f "$WORK/pkg/conf/resource"
        fi
    }

    # Die Varianten: Nummer | privilege-Form | conf/resource
    # 1 = ausgeliefert + resource      (kleinste Änderung)
    # 2 = nur defaults + resource
    # 3 = ohne ctrl-script + resource
    # 4 = ohne ctrl-script, mit groupname + resource
    # 5 = ohne ctrl-script, ohne resource
    # 6 = nur defaults, ohne resource
    # 7 = ohne ctrl-script, username klein + resource
    # 8 = ausgeliefert, ohne resource  (Kontrolle - muss 319 geben)
    for spec in "1:ja" "2:ja" "3:ja" "4:ja" "5:nein" "6:nein" "7:ja" "8:nein"; do
        v=${spec%%:*}
        res=${spec##*:}

        rm -rf "$WORK/pkg"
        mkdir -p "$WORK/pkg"
        ( cd "$WORK/pkg" && tar xpf "$SPK_DIR/$SPK_NAME" ) 2>/dev/null

        if [ ! -f "$WORK/pkg/conf/privilege" ]; then
            echo "  Variante $v: kann nicht entpackt werden - abgebrochen."
            break
        fi

        write_priv "$WORK/pkg/conf/privilege" "$v"
        variant_resource "$v" "$res"

        NEU="$WORK/test-$v.spk"
        ( cd "$WORK/pkg" && tar cpf "$NEU" INFO conf scripts package.tgz PACKAGE_ICON.PNG PACKAGE_ICON_256.PNG ) 2>/dev/null

        OUT=$(synopkg install "$NEU" 2>&1)
        CODE=$(echo "$OUT" | sed -n 's/.*"code":\([0-9]*\).*/\1/p' | tail -n 1)
        DESC=$(echo "$OUT" | sed -n 's/.*"description":"\([^"]*\)".*/\1/p' | tail -n 1)

        case "$CODE" in
            ""|0)
                echo "  Variante $v: ERFOLG - das Paket wurde installiert!"
                echo "  -> Form: $(variant_label "$v")"
                echo ""
                echo "  conf/privilege:"
                cat "$WORK/pkg/conf/privilege"
                echo ""
                if [ -f "$WORK/pkg/conf/resource" ]; then
                    echo "  conf/resource:"
                    cat "$WORK/pkg/conf/resource"
                    echo ""
                fi
                synopkg status ComfoBoxGateway 2>&1 || true
                echo ""
                echo "  Danach bitte prüfen, ob die Installation vollständig war:"
                echo "    sudo tail -30 /var/log/packages/ComfoBoxGateway.log"
                echo ""
                echo "  WICHTIG: Diese Installation beweist nur, welche Form das DSM"
                echo "  akzeptiert. Die Lifecycle-Skripte laufen dabei ohne root -"
                echo "  schlägt die Installation danach fehl, bitte deinstallieren:"
                echo "    sudo synopkg uninstall ComfoBoxGateway"
                echo "  und die Meldung hier im Protokoll festhalten."
                rm -rf "$WORK"
                exit 0
                ;;
            319)
                echo "  Variante $v: 319  ($(variant_label "$v"))"
                ;;
            *)
                echo "  Variante $v: Fehlercode ${CODE:-?} ${DESC}  ($(variant_label "$v"))"
                ;;
        esac
    done

    echo ""
    echo "  Keine Variante hat funktioniert. Dann ist es nicht die"
    echo "  privilege-Datei - bitte die Log-Auszüge oben auswerten."
    rm -rf "$WORK"
    echo ""
fi

echo "=============================================================="
echo " Fertig."
echo "=============================================================="
