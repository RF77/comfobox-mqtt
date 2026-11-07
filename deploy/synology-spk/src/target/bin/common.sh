#!/bin/sh
# ---------------------------------------------------------------------------
# common.sh – gemeinsame Pfade und Hilfsfunktionen für die Skripte des SPK.
#
# Wird von preinst, postinst, preuninst, postuninst und start-stop-status
# eingebunden.
#
# WICHTIG (DSM 7): Die Lifecycle-Skripte laufen als Paket-Benutzer, NICHT
# als Root. Auf DSM 7 lehnt das Paketzentrum ein Paket ab, das in
# conf/privilege "run-as": "root" verlangt (Fehler 319). Privilegierte
# Aufgaben uebernimmt deshalb der DSM-Worker "docker-project" aus
# conf/resource.
# ---------------------------------------------------------------------------

# Basisverzeichnisse (vom Paketzentrum gesetzt)
: "${SYNOPKG_PKGDEST:=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)}"
: "${SYNOPKG_PKGVAR:=${SYNOPKG_PKGDEST}/var}"

PKG_NAME="ComfoBoxGateway"
PKG_DIR="${SYNOPKG_PKGDEST}"
VAR_DIR="${SYNOPKG_PKGVAR}"
LOG_DIR="${VAR_DIR}/logs"
LOG_FILE="${SYNOPKG_TEMP_LOGFILE:-/tmp/${PKG_NAME}.log}"

# Persistente Dateien
CONFIG_FILE="${VAR_DIR}/config.json"
PORTS_FILE="${VAR_DIR}/ports.json"

# Standardwerte
DEFAULT_WEB_PORT="8081"
DEFAULT_IMAGE="ghcr.io/rf77/comfobox-gateway:1.0.1"

# Das Image steht in der Datei "image" neben den Skripten - build-spk.sh
# schreibt sie beim Bauen des Pakets. So kann das Paket nie versehentlich
# ein anderes Image ziehen.
if [ -f "${PKG_DIR}/image" ]; then
    IMAGE_FROM_PACKAGE=$(head -n 1 "${PKG_DIR}/image" 2>/dev/null)
    [ -n "$IMAGE_FROM_PACKAGE" ] && DEFAULT_IMAGE="$IMAGE_FROM_PACKAGE"
fi

log()
{
    printf '%s %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" >> "$LOG_FILE" 2>/dev/null || true
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
#   { "Bacnet": { "Port": …, "Baudrate": … },
#     "Supervisor": { "PassAllSerialDevices": …, "WebPort": …, "Image": … } }
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

# ---------------------------------------------------------------------------
# Die Rechte des Datenverzeichnisses
#
# Das Verzeichnis gehoert dem Paket-Benutzer, der Container laeuft aber mit
# uid 1001 (comfobox). Beide muessen schreiben duerfen: der Assistent legt
# die config.json an, der Gateway schreibt den 24-h-Schreibzaehler.
# Ohne Root koennen wir kein chown machen - deshalb sind die Rechte so
# gesetzt, dass beide Schreibzugriff haben. Der uebergeordnete Pfad
# /var/packages ist ohnehin nur fuer System-Benutzer lesbar.
# ---------------------------------------------------------------------------
prepare_permissions()
{
    mkdir -p "$VAR_DIR" "$LOG_DIR" "${VAR_DIR}/data" 2>/dev/null || true
    chmod 0777 "$VAR_DIR" 2>/dev/null || true
    chmod 0777 "$LOG_DIR" 2>/dev/null || true
}
