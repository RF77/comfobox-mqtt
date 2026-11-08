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
#   sudo sh dsm-diagnose.sh /pfad/zum/paket.spk [--reset|--install|--worker|--test|--log]
#
#   --reset    **Raeumt alle Reste fehlgeschlagener Versuche weg**: Paket
#              stoppen und deinstallieren (auch im Status "broken"), die
#              Verzeichnisse unter /var/packages und @appstore entfernen,
#              liegengebliebene Container und Netzwerke loeschen. Das
#              Docker-Volume "comfobox-data" bleibt erhalten (dort liegt der
#              24-h-Schreibzaehler). Danach ist die NAS wieder in dem
#              Zustand, den eine Erstinstallation erwartet.
#   --install  **Empfohlen.** Macht zuerst --reset, installiert dann das
#              Paket und zeigt anschliessend, was der Resource-Worker
#              "docker-project" ins Syslog geschrieben hat. Genau dort steht
#              der Grund fuer "Acquire docker-project … (fail)".
#   --worker   **Emuliert den DSM-Worker "docker-project"** und zeigt die
#              volle Fehlermeldung von `docker compose up`. Das Paket wird
#              dabei NICHT installiert - nur die Compose-Datei wird
#              ausprobiert und danach wieder aufgeraeumt.
#   --test     Probiert mehrere Varianten der privilege-Datei durch und
#              installiert die erste, die das DSM akzeptiert. Ein Fehler in
#              prepare_install aendert nichts an der NAS.
#   --log      Zeigt nur die Log-Auszuege des DSM (nichts wird installiert)
#
# Voraussetzung: nur BusyBox-Standardwerkzeuge (sh, tar, md5sum, sha256sum).
# Ohne --reset/--install/--test/--worker wird **nichts** veraendert.
# ---------------------------------------------------------------------------
set -u

SPK="${1:-}"
MODE="${2:-}"

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
# conf/resource aus dem SPK - und passt der "path" zum Inhalt?
#
# Der Worker liest die Compose-Datei unter <ziel>/<path>/compose.yaml.
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

    # Passt der Pfad zum Inhalt von package.tgz?
    if [ -n "${PROJ_PATH:-}" ]; then
        if tar xf "$SPK" -C "$TMP" package.tgz 2>/dev/null \
           && tar tzf "$TMP/package.tgz" 2>/dev/null \
              | sed 's|/$||' | grep -q "^${PROJ_PATH}/compose\.y"; then
            echo ">> OK: '$PROJ_PATH/compose.yaml' liegt genau dort, wo der Worker sucht."
        else
            echo ">> FEHLER: '$PROJ_PATH/compose.yaml' fehlt in package.tgz!"
            echo "   Der Worker sucht die Datei unter <ziel>/$PROJ_PATH/ - er"
            echo "   meldet dann nur '(fail)'. Inhalt von package.tgz:"
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
       && tar xzf "$WORK2/package.tgz" -C "$WORK2" compose/compose.yaml 2>/dev/null; then
        echo ">> Compose-Datei:"
        sed 's/^/   /' "$WORK2/compose/compose.yaml"
        echo ""
        echo ">> docker compose config (Syntaxpruefung):"
        if docker compose -f "$WORK2/compose/compose.yaml" config >/dev/null 2>&1; then
            echo "   OK - die Compose-Datei ist gueltig."
        else
            docker compose -f "$WORK2/compose/compose.yaml" config 2>&1 \
                | head -n 20 | sed 's/^/   /'
            echo "   WARNUNG: Die Compose-Datei hat einen Fehler!"
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
    if [ ! -f "$CF_DIR/compose.yaml" ]; then
        echo "FEHLER: compose/compose.yaml fehlt in package.tgz"
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
show_container_state
show_reference_projects

# ---------------------------------------------------------------------------
# 8. Installationsversuch
# ---------------------------------------------------------------------------
if [ "$MODE" = "--reset" ]; then
    echo "--- 8. Aufraeumen (kein Installationsversuch) ---"
    reset_all
    show_worker_log
elif [ "$MODE" = "--install" ]; then
    echo "--- 8. Reste fehlgeschlagener Versuche entfernen ---"
    reset_all

    echo "--- 9. Compose-Datei pruefen ---"
    validate_compose

    echo "--- 10. Installationsversuch ---"
    echo "Die Ausgabe ist JSON und enthaelt den echten Fehlercode."
    echo ""
    synopkg install "$SPK" 2>&1
    echo ""
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
