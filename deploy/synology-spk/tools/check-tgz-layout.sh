#!/bin/bash
# Zeigt die Struktur von package.tgz im gebauten SPK.
set -u

# Ohne Argument das **neueste** Paket in dist/ nehmen. Ein fest
# eingetragener Dateiname zeigt sonst auf ein altes Paket - und der Test
# meldet dann Fehler, die laengst behoben sind (oder umgekehrt).
SPK="${1:-}"
if [ -z "$SPK" ]; then
    SPK=$(ls -t dist/*.spk 2>/dev/null | head -n 1)
fi
if [ -z "$SPK" ] || [ ! -f "$SPK" ]; then
    echo "FEHLER: Kein SPK gefunden (${SPK:-dist/*.spk})" >&2
    echo "        Erst bauen: bash build-spk.sh" >&2
    exit 1
fi

SPK_DIR=$(CDPATH= cd -- "$(dirname -- "$SPK")" && pwd) || exit 1
SPK_NAME=$(basename -- "$SPK")

echo "Pruefe: $SPK"
echo ""

TMP=$(mktemp -d) || exit 1
( cd "$SPK_DIR" && tar xpf "$SPK_NAME" -C "$TMP" package.tgz ) 2>/dev/null || {
    echo "package.tgz nicht lesbar"; exit 1; }

echo "=== Inhalt von package.tgz ==="
tar tzf "$TMP/package.tgz"

echo ""
echo "=== package.tgz darf KEINE Ebene 'target/' enthalten ==="
if tar tzf "$TMP/package.tgz" | grep -q '^target/'; then
    echo "FEHLER: package.tgz enthaelt 'target/...' - eine Ebene zu viel!"
    echo "        Das DSM entpackt package.tgz DIREKT in das Zielverzeichnis."
    echo "        'target/bin/common.sh' landet dann in <ziel>/target/bin/common.sh"
    echo "        und der Worker findet <ziel>/compose/docker-compose.yaml nicht."
else
    echo "OK: package.tgz enthaelt die Dateien direkt auf der obersten Ebene."
fi

rm -rf "$TMP"
