#!/bin/bash
# ---------------------------------------------------------------------------
# test-config-parse.sh – prüft den JSON-Zugriff auf die config.json
#
# Der Supervisor liest die Werte für die Compose-Datei aus der config.json,
# die der Assistent im Gateway geschrieben hat. Der Parser (json_get in
# common.sh) ist Shell-Code – und damit leicht falsch. Dieser Test prüft ihn
# gegen eine realistische config.json.
#
# Aufruf:  bash tools/test-config-parse.sh
# ---------------------------------------------------------------------------
set -u

cd "$(dirname "$0")/.." || exit 1

SYNOPKG_PKGDEST="$(pwd)/src/target"
export SYNOPKG_PKGDEST
SYNOPKG_PKGVAR="$(pwd)/.test-var"
export SYNOPKG_PKGVAR

# shellcheck source=src/target/bin/common.sh
. "${SYNOPKG_PKGDEST}/bin/common.sh"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

CONFIG="$WORK/config.json"

# Die Datei, wie der Assistent sie schreibt (System.Text.Json, eingerückt).
cat > "$CONFIG" <<'EOF'
{
  "SchemaVersion": 1,
  "Api": {
    "SetupCompleted": true,
    "AdminUsername": "admin",
    "RequireAuthentication": false,
    "SuggestedUiPort": 8081
  },
  "Bacnet": {
    "Port": "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0",
    "Baudrate": 38400,
    "DeviceId": 1
  },
  "Mqtt": {
    "Brokers": [
      {
        "Host": "192.168.1.10",
        "Port": 1883,
        "Username": "comfobox",
        "Password": "geheim"
      }
    ],
    "KeepAliveSeconds": 30
  },
  "Supervisor": {
    "PassAllSerialDevices": true,
    "WebPort": 8081,
    "Image": "ghcr.io/rf77/comfobox-gateway:1.0.0"
  },
  "Gateway": {
    "BaseTopic": "ComfoBox",
    "MaxWritesPer24h": 200
  }
}
EOF

fail=0
check()
{
    description="$1"
    expected="$2"
    actual="$3"

    if [ "$expected" = "$actual" ]; then
        echo "OK   $description"
    else
        echo "FAIL $description"
        echo "       erwartet: '$expected'"
        echo "       erhalten: '$actual'"
        fail=1
    fi
}

check "Bacnet:Port" \
    "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0" \
    "$(json_get "Bacnet" "Port" "$CONFIG")"

check "Bacnet:Baudrate" \
    "38400" \
    "$(json_get "Bacnet" "Baudrate" "$CONFIG")"

check "Supervisor:PassAllSerialDevices" \
    "true" \
    "$(json_get "Supervisor" "PassAllSerialDevices" "$CONFIG")"

check "Supervisor:Image" \
    "ghcr.io/rf77/comfobox-gateway:1.0.0" \
    "$(json_get "Supervisor" "Image" "$CONFIG")"

check "Api:AdminUsername" \
    "admin" \
    "$(json_get "Api" "AdminUsername" "$CONFIG")"

check "Gateway:MaxWritesPer24h" \
    "200" \
    "$(json_get "Gateway" "MaxWritesPer24h" "$CONFIG")"

# Der Parser arbeitet abschnittsweise: "Port" wird im Abschnitt "Bacnet"
# gefunden, nicht in einem anderen Abschnitt.
check "Bacnet:Port wird nicht mit einem anderen Abschnitt verwechselt" \
    "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0" \
    "$(json_get "Bacnet" "Port" "$CONFIG")"

# Innerhalb eines Abschnitts zählt der erste Treffer - hier der Port des
# Brokers im Array.
check "Mqtt:Port (aus dem Broker-Array)" \
    "1883" \
    "$(json_get "Mqtt" "Port" "$CONFIG")"

# Ein unbekannter Abschnitt liefert nichts.
check "Unbekannter Abschnitt" \
    "" \
    "$(json_get "GibtEsNicht" "Port" "$CONFIG")"

# Eine fehlende Datei darf keinen Abbruch auslösen.
json_get "Bacnet" "Port" "$WORK/gibtsnicht.json" >/dev/null 2>&1
check "Fehlende Datei" \
    "1" \
    "$?"

exit $fail
