#!/bin/sh
# Gemeinsame Pfade und Docker-Erkennung für alle Skripte des SPK.
# Wird von preinst, postinst, start-stop-status und dem Supervisor eingebunden.

# Basisverzeichnisse (vom Paketcenter gesetzt)
: "${SYNOPKG_PKGDEST:=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)}"
: "${SYNOPKG_PKGVAR:=${SYNOPKG_PKGDEST}/var}"

PKG_NAME="ComfoBoxGateway"
PKG_DIR="${SYNOPKG_PKGDEST}"
VAR_DIR="${SYNOPKG_PKGVAR}"
BIN_DIR="${PKG_DIR}/bin"
LOG_FILE="${SYNOPKG_TEMP_LOGFILE:-/tmp/${PKG_NAME}.log}"

# Persistente Dateien
CONFIG_FILE="${VAR_DIR}/config.json"
PORTS_FILE="${VAR_DIR}/ports.json"
COMPOSE_FILE="${VAR_DIR}/compose.yaml"
COMPOSE_TEMPLATE="${PKG_DIR}/compose.template.yaml"
WRITES_FILE="${VAR_DIR}/writes.json"
LOG_DIR="${VAR_DIR}/logs"

# Compose-Projektname (stabil – verhindert Duplikate bei Updates)
COMPOSE_PROJECT="comfobox"
COMPOSE_CONTAINER="comfobox-gateway"

# Standardwerte
DEFAULT_WEB_PORT="8081"
DEFAULT_IMAGE="ghcr.io/RF77/comfobox-gateway:1.0.0"

log()
{
    printf '%s %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" >> "$LOG_FILE" 2>/dev/null || true
    [ -n "$SYNOPKG_PKG_STATUS" ] && printf '%s\n' "$*" >> "$LOG_FILE" 2>/dev/null || true
}

die()
{
    log "FEHLER: $*"
    echo "$*" >&2
    exit 1
}

# --- Container Manager / Docker finden --------------------------------------

docker_bin()
{
    for candidate in \
        /var/packages/ContainerManager/target/usr/bin/docker \
        /var/packages/Docker/target/usr/bin/docker \
        /usr/local/bin/docker \
        /usr/bin/docker
    do
        [ -x "$candidate" ] && { echo "$candidate"; return 0; }
    done
    command -v docker 2>/dev/null && return 0
    return 1
}

# Compose-Plugin bzw. docker-compose finden.
# DSM 7.2 liefert das v2-Plugin; auf anderen Systemen gibt es docker-compose v1.
compose_cmd()
{
    DOCKER=$(docker_bin) || return 1

    for candidate in \
        /var/packages/ContainerManager/target/usr/local/libexec/docker/cli-plugins/docker-compose \
        /var/packages/ContainerManager/target/usr/bin/docker-compose \
        /var/packages/Docker/target/usr/bin/docker-compose \
        /usr/local/bin/docker-compose \
        /usr/bin/docker-compose
    do
        if [ -x "$candidate" ]; then
            echo "'$candidate'"
            return 0
        fi
    done

    # docker compose (Plugin)
    if "$DOCKER" compose version >/dev/null 2>&1; then
        echo "'$DOCKER' compose"
        return 0
    fi
    return 1
}

container_manager_running()
{
    DOCKER=$(docker_bin) || return 1
    "$DOCKER" info >/dev/null 2>&1
}

# --- Gruppen-ID für dialout ermitteln (für group_add im Compose) -------------
dialout_gid()
{
    gid=$(getent group dialout 2>/dev/null | cut -d: -f3)
    [ -z "$gid" ] && gid=$(grep '^dialout:' /etc/group 2>/dev/null | cut -d: -f3)
    [ -z "$gid" ] && gid=20
    echo "$gid"
}
