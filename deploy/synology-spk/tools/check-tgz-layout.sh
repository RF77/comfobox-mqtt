#!/bin/bash
# Zeigt die Struktur von package.tgz im gebauten SPK.
set -u
SPK="${1:-dist/ComfoBoxGateway_noarch-dsm7_1.0.1-0006.spk}"
SPK_DIR=$(CDPATH= cd -- "$(dirname -- "$SPK")" && pwd) || exit 1
SPK_NAME=$(basename -- "$SPK")

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
    echo "        und der Worker findet <ziel>/compose/compose.yaml nicht."
else
    echo "OK: package.tgz enthaelt die Dateien direkt auf der obersten Ebene."
fi

rm -rf "$TMP"
