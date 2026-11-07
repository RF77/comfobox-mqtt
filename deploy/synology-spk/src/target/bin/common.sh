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
DEFAULT_IMAGE="ghcr.io/rf77/comfobox-gateway:1.0.1"

# Das Image steht in der Datei "image" neben den Skripten - build-spk.sh
# schreibt sie beim Bauen des Pakets. So kann das Paket nie ein falsches
# Image ziehen.
if [ -f "${PKG_DIR}/image" ]; then
    IMAGE_FROM_PACKAGE=$(head -n 1 "${PKG_DIR}/image" 2>/dev/null)
    [ -n "$IMAGE_FROM_PACKAGE" ] && DEFAULT_IMAGE="$IMAGE_FROM_PACKAGE"
fi

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

# ---------------------------------------------------------------------------
# JSON-Zugriff auf die config.json
#
# Die config.json wird vom Gateway geschrieben (System.Text.Json, eingerückt)
# und folgt dem Schema der .NET-Konfiguration:
#
#   { "Bacnet": { "Port": "/dev/ttyUSB0", "Baudrate": 38400 },
#     "Supervisor": { "PassAllSerialDevices": true } }
#
# json_get liest einen Wert aus einem Abschnitt. Ein leerer Abschnitt liest
# auf der obersten Ebene.
#
#   json_get <abschnitt> <schlüssel> <datei>
#
# Es wird bewusst kein jq verwendet - auf dem DSM ist es nicht vorhanden.
# ---------------------------------------------------------------------------
json_get()
{
    _json_section="$1"
    _json_key="$2"
    _json_file="$3"

    [ -f "$_json_file" ] || return 1

    awk -v want_section="$_json_section" -v want_key="$_json_key" '
        {
            line = $0

            # Abschnittsbeginn:  "Name": {
            if (line ~ /^[ \t]*"[^"]+"[ \t]*:[ \t]*\{/) {
                name = line
                sub(/^[ \t]*"/, "", name)
                sub(/".*$/, "", name)
                current = name
                next
            }

            # Abschnittsende
            if (line ~ /^[ \t]*\}/) {
                current = ""
                next
            }

            if (current != want_section) {
                next
            }

            # Der Schlüssel muss in Anführungszeichen stehen - sonst würde
            # "Port" auch in "BrokerPort" gefunden.
            if (line !~ "\"" want_key "\"") {
                next
            }

            value = line
            sub(/^.*"[^"]*"[ \t]*:[ \t]*/, "", value)
            sub(/,?[ \t]*$/, "", value)
            gsub(/^"/, "", value)
            gsub(/"$/, "", value)
            print value
            exit
        }
    ' "$_json_file"
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
