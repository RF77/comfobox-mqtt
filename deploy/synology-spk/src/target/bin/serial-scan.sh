#!/bin/sh
# ---------------------------------------------------------------------------
# serial-scan.sh – enumeriert die seriellen Ports des NAS und schreibt ports.json
#
# Aufruf:  serial-scan.sh <ziel-pfad> [zusätzlicher-port-pfad]
#
# Ausgabe (JSON):
#   {
#     "generatedAt": "2026-10-31T08:12:44Z",
#     "ports": [
#       { "device": "/dev/ttyUSB0",
#         "stableId": "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0",
#         "description": "Prolific CH340 (1a86:7523)" }
#     ]
#   }
#
# Wird vom Supervisor regelmäßig aufgerufen. Die Datei wird in den Container
# gemountet, damit der Getting-Started-Assistent sie lesen kann.
# ---------------------------------------------------------------------------
set -u

TARGET="${1:-/dev/stdout}"
EXTRA="${2:-}"

first=1

emit()
{
    device="$1"
    stable="$2"
    description="$3"

    [ -e "$device" ] || return 0

    [ "$first" -eq 0 ] && printf ',\n' >> "$TARGET"
    first=0

    printf '    { "device": "%s", "stableId": %s, "description": "%s" }' \
        "$device" \
        "$( [ -n "$stable" ] && printf '"%s"' "$stable" || printf 'null' )" \
        "$description" >> "$TARGET"
}

describe_usb()
{
    tty="$1"
    base=$(basename "$tty")
    sysdev="/sys/class/tty/$base/device"
    vendor=$(cat "$sysdev/../idVendor" 2>/dev/null)
    product=$(cat "$sysdev/../idProduct" 2>/dev/null)
    manufacturer=$(cat "$sysdev/../manufacturer" 2>/dev/null)

    [ -z "$vendor" ] && vendor=$(cat /sys/class/tty/$base/device/../idVendor 2>/dev/null)
    [ -z "$vendor" ] && return 0

    name="$manufacturer"
    [ -z "$name" ] && name="USB"
    printf '%s %s (%s:%s)' "$name" "$base" "$vendor" "$product"
}

{
    printf '{\n  "generatedAt": "%s",\n  "ports": [' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
} > "$TARGET"

# 1) Stabile Pfade über /dev/serial/by-id (reboot-stabil)
for byid in /dev/serial/by-id/*; do
    [ -e "$byid" ] || continue
    resolved=$(readlink -f "$byid" 2>/dev/null) || continue
    case "$resolved" in
        /dev/ttyUSB*|/dev/ttyACM*)
            emit "$resolved" "$byid" "$(describe_usb "$resolved")"
            ;;
    esac
done

# 2) Alle übrigen seriellen Ports ohne stabilen Pfad
for tty in /dev/ttyUSB* /dev/ttyACM*; do
    [ -e "$tty" ] || continue
    stable=""
    for byid in /dev/serial/by-id/*; do
        [ -e "$byid" ] || continue
        resolved=$(readlink -f "$byid" 2>/dev/null) || continue
        [ "$resolved" = "$tty" ] && { stable="$byid"; break; }
    done
    [ -n "$stable" ] && continue
    emit "$tty" "" "$(describe_usb "$tty")"
done

# 3) Vom Benutzer konfigurierter Port (falls außerhalb der obigen Liste)
if [ -n "$EXTRA" ] && [ -e "$EXTRA" ]; then
    already=0
    for byid in /dev/serial/by-id/*; do
        [ -e "$byid" ] || continue
        resolved=$(readlink -f "$byid" 2>/dev/null) || continue
        [ "$resolved" = "$EXTRA" ] && already=1
    done
    [ "$already" -eq 0 ] && emit "$EXTRA" "" "Konfigurierter Port"
fi

printf '\n  ]\n}\n' >> "$TARGET"
