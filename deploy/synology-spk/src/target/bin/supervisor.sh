#!/bin/sh
# ---------------------------------------------------------------------------
# supervisor.sh – rendert die Compose-Datei, startet die Container und
#                 überwacht die Konfiguration.
#
# Aufruf:  supervisor.sh run | start | stop | status | render
#
# Warum ein eigener Supervisor?
#   Das Serial-Device muss per `devices:` in den Container durchgereicht
#   werden. Welches Device das ist, entscheidet der Benutzer im
#   Getting-Started-Assistent – also erst NACH der Installation. Nur ein
#   Supervisor auf dem Host kann die Compose-Datei daraufhin neu rendern und
#   den Container ersetzen, ohne das Paket neu zu installieren.
#
#   Der Container bekommt bewusst KEINEN Docker-Socket.
# ---------------------------------------------------------------------------
set -u

SELF_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck source=common.sh
. "$SELF_DIR/common.sh"

PID_FILE="${VAR_DIR}/supervisor.pid"
HASH_FILE="${VAR_DIR}/supervisor.state"
LOCK_FILE="${VAR_DIR}/supervisor.lock"

# ---------------------------------------------------------------------------
# compose.yaml rendern
#
# Die Werte kommen aus der config.json, die der Assistent im Gateway
# geschrieben hat. Deren Schema ist das der .NET-Konfiguration:
#
#   "Bacnet":     { "Port": …, "Baudrate": … }
#   "Supervisor": { "PassAllSerialDevices": …, "WebPort": …, "Image": … }
#
# json_get (in common.sh) liest daraus die Werte - abschnittsweise, damit
# "Port" nicht mit "BrokerPort" verwechselt wird.
# ---------------------------------------------------------------------------
render_compose()
{
    [ -f "$COMPOSE_TEMPLATE" ] || die "Compose-Vorlage fehlt: $COMPOSE_TEMPLATE"

    # Der Web-Port: der Admin-Link des Pakets zeigt auf 8081 (adminport in
    # der INFO). Ein Wert in der config.json überschreibt ihn.
    web_port=$(json_get "Supervisor" "WebPort" "$CONFIG_FILE" 2>/dev/null | head -n 1)
    [ -n "$web_port" ] || web_port="$DEFAULT_WEB_PORT"

    baudrate=$(json_get "Bacnet" "Baudrate" "$CONFIG_FILE" 2>/dev/null | head -n 1)
    [ -n "$baudrate" ] || baudrate="38400"

    # Serial-Port aus der Konfiguration (stabiler Pfad bevorzugt)
    serial_device=$(json_get "Bacnet" "Port" "$CONFIG_FILE" 2>/dev/null | head -n 1)
    [ -n "$serial_device" ] || serial_device=""

    # Auflösen: by-id → realer Device-Node
    resolved_serial=""
    if [ -n "$serial_device" ]; then
        if [ -e "$serial_device" ]; then
            resolved_serial=$(readlink -f "$serial_device" 2>/dev/null || echo "$serial_device")
        fi
    fi

    # Alle durchreichen? Dann alle vorhandenen seriellen Ports verwenden.
    pass_all=$(json_get "Supervisor" "PassAllSerialDevices" "$CONFIG_FILE" 2>/dev/null | head -n 1)
    [ "$pass_all" = "true" ] || pass_all="false"

    devices=""
    if [ "$pass_all" = "true" ]; then
        for tty in /dev/ttyUSB* /dev/ttyACM*; do
            [ -e "$tty" ] || continue
            devices="${devices}      - \"$tty:$tty\"\n"
        done
        [ -n "$resolved_serial" ] && devices="${devices}      - \"$resolved_serial:$resolved_serial\"\n"
    elif [ -n "$resolved_serial" ]; then
        devices="${devices}      - \"$resolved_serial:$resolved_serial\"\n"
    else
        # Noch kein Port gewählt → Setup-Modus ohne Serial-Zugriff
        log "Kein Serial-Port konfiguriert – Container startet im Setup-Modus."
    fi

    gid=$(dialout_gid)
    image=$(json_get "Supervisor" "Image" "$CONFIG_FILE" 2>/dev/null | head -n 1)
    [ -n "$image" ] || image="$DEFAULT_IMAGE"

    mkdir -p "$VAR_DIR" "$LOG_DIR"
    prepare_permissions

    # sed mit \n-Escapes – verfügbar in BusyBox ash
    sed \
        -e "s|\${WEB_PORT}|$web_port|g" \
        -e "s|\${SERIAL_DEVICES}|$devices|g" \
        -e "s|\${DIALOUT_GID}|$gid|g" \
        -e "s|\${DATA_DIR}|$VAR_DIR|g" \
        -e "s|\${LOG_DIR}|$LOG_DIR|g" \
        -e "s|\${IMAGE}|$image|g" \
        -e "s|\${BAUDRATE}|$baudrate|g" \
        "$COMPOSE_TEMPLATE" > "$COMPOSE_FILE.tmp"

    mv -f "$COMPOSE_FILE.tmp" "$COMPOSE_FILE"
    log "compose.yaml gerendert (Port=$web_port, Serial='${resolved_serial}', alle=$pass_all, gid=$gid)"
}

# ---------------------------------------------------------------------------
# Rechte des Datenverzeichnisses
#
# Der Container läuft als Benutzer "comfobox" (uid 1001). Er muss in das
# Datenverzeichnis schreiben können: der Assistent legt dort die
# config.json ab, und der 24-h-Schreibzähler liegt ebenfalls dort.
# ---------------------------------------------------------------------------
prepare_permissions()
{
    [ -d "$VAR_DIR" ] || return 0

    chown -R 1001:1001 "$VAR_DIR" 2>/dev/null || true
    chmod 700 "$VAR_DIR" 2>/dev/null || true
}

# ---------------------------------------------------------------------------
# Container starten / stoppen
# ---------------------------------------------------------------------------
compose_up()
{
    COMPOSE=$(compose_cmd) || { log "docker compose nicht gefunden"; return 1; }
    DOCKER=$(docker_bin) || { log "docker nicht gefunden"; return 1; }
    ( cd "$VAR_DIR" && eval "$COMPOSE" -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE" up -d --remove-orphans ) \
        >> "$LOG_FILE" 2>&1
    rc=$?
    [ $rc -eq 0 ] && log "Container gestartet" || log "Container-Start fehlgeschlagen (rc=$rc)"
    return $rc
}

compose_down()
{
    COMPOSE=$(compose_cmd) || return 0
    ( cd "$VAR_DIR" && eval "$COMPOSE" -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE" stop ) \
        >> "$LOG_FILE" 2>&1
    log "Container gestoppt"
}

compose_remove()
{
    COMPOSE=$(compose_cmd) || return 0
    ( cd "$VAR_DIR" && eval "$COMPOSE" -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE" down --remove-orphans ) \
        >> "$LOG_FILE" 2>&1
    log "Container entfernt"
}

container_running()
{
    DOCKER=$(docker_bin) || return 1
    "$DOCKER" ps --filter "name=^/${COMPOSE_CONTAINER}$" --filter "status=running" \
        --format '{{.Names}}' 2>/dev/null | grep -q "^${COMPOSE_CONTAINER}$"
}

# ---------------------------------------------------------------------------
# Hauptloop
# ---------------------------------------------------------------------------
run_loop()
{
    echo $$ > "$PID_FILE"
    log "Supervisor gestartet (PID $$)"

    trap 'log "Supervisor wird beendet"; rm -f "$PID_FILE"; exit 0' TERM INT

    last_hash=""
    while true; do
        # Serial-Ports aktualisieren
        "$SELF_DIR/serial-scan.sh" "$PORTS_FILE.tmp" \
            "$(json_get "Bacnet" "Port" "$CONFIG_FILE" 2>/dev/null | head -n 1)" \
            >/dev/null 2>&1 && mv -f "$PORTS_FILE.tmp" "$PORTS_FILE"

        # Änderung an config.json oder ports.json?
        new_hash=$( { md5sum "$CONFIG_FILE" 2>/dev/null; md5sum "$PORTS_FILE" 2>/dev/null; } | md5sum | cut -d' ' -f1 )

        if [ "$new_hash" != "$last_hash" ]; then
            log "Konfiguration oder Port-Scan hat sich geändert – rendere und starte neu"
            render_compose
            compose_up
            last_hash="$new_hash"
            echo "$new_hash" > "$HASH_FILE"
        fi

        sleep 3
    done
}

# ---------------------------------------------------------------------------
# Befehle
# ---------------------------------------------------------------------------
case "${1:-run}" in
    run)
        run_loop
        ;;

    start)
        mkdir -p "$VAR_DIR" "$LOG_DIR"
        if [ -f "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
            log "Supervisor läuft bereits (PID $(cat "$PID_FILE"))"
        else
            rm -f "$PID_FILE"
            "$SELF_DIR/supervisor.sh" run >> "$LOG_FILE" 2>&1 &
            echo $! > "$PID_FILE"
            log "Supervisor gestartet (PID $!)"
        fi
        ;;

    stop)
        if [ -f "$PID_FILE" ]; then
            pid=$(cat "$PID_FILE")
            kill "$pid" 2>/dev/null && log "Supervisor $pid gestoppt"
            rm -f "$PID_FILE"
        fi
        compose_down
        ;;

    status)
        if [ -f "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
            if container_running; then
                exit 0
            fi
            exit 1
        fi
        exit 3
        ;;

    render)
        render_compose
        ;;

    up)
        render_compose
        compose_up
        ;;

    down)
        compose_down
        ;;

    remove)
        compose_remove
        ;;

    *)
        echo "Aufruf: $0 {run|start|stop|status|render|up|down|remove}" >&2
        exit 1
        ;;
esac
