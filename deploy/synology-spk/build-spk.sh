#!/bin/sh
# ---------------------------------------------------------------------------
# ComfoBox Gateway – SPK-Skelett
#
# Erzeugt ein installierbares Synology-Paket (.spk) aus diesem Verzeichnis.
#
# Voraussetzungen: nur ein POSIX-shell + tar + gzip (kein Docker nötig).
# Auf einem Linux/Mac/Rechner oder in einem CI-Job ausführbar.
#
# Aufruf:
#   ./build-spk.sh [VERSION] [REVISION]
#
# Ergebnis:
#   dist/ComfoBoxGateway_noarch-dsm7_<VERSION>-<REVISION>.spk
# ---------------------------------------------------------------------------
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$SCRIPT_DIR"

VERSION=${1:-1.0.0}
REVISION=${2:-0001}
PACKAGE=ComfoBoxGateway
OUT="$SCRIPT_DIR/dist"
WORK="$OUT/work"

echo "==> Paket $PACKAGE $VERSION-$REVISION"

rm -rf "$WORK"
mkdir -p "$WORK/scripts" "$WORK/target"

# --- Dateien kopieren -------------------------------------------------------
cp -R src/target/. "$WORK/target/" 2>/dev/null || cp -R src/target/* "$WORK/target/"
cp src/INFO "$WORK/INFO"
cp src/scripts/* "$WORK/scripts/"
chmod 755 "$WORK"/scripts/*

# Icon optional
if [ -f src/PACKAGE_ICON.png ]; then
    cp src/PACKAGE_ICON.png "$WORK/PACKAGE_ICON.PNG"
    cp src/PACKAGE_ICON.png "$WORK/PACKAGE_ICON_256.PNG"
fi

# --- package.tgz (Inhalt des Pakets) ---------------------------------------
echo "==> package.tgz erstellen"
( cd "$WORK" && find target -mindepth 1 -maxdepth 1 | tar cpf - --files-from=/dev/stdin | gzip -n > package.tgz )

# --- Prüfsumme in INFO eintragen -------------------------------------------
CHECKSUM=$(md5sum "$WORK/package.tgz" | cut -d' ' -f1)
if grep -q '^checksum=' "$WORK/INFO"; then
    sed -i "s|^checksum=.*|checksum=\"$CHECKSUM\"|" "$WORK/INFO"
else
    printf 'checksum="%s"\n' "$CHECKSUM" >> "$WORK/INFO"
fi

# --- Version in INFO eintragen ---------------------------------------------
sed -i "s|^version=.*|version=\"$VERSION-$REVISION\"|" "$WORK/INFO"

# --- SPK packen -------------------------------------------------------------
SPK="$OUT/${PACKAGE}_noarch-dsm7_${VERSION}-${REVISION}.spk"
rm -f "$SPK"
echo "==> SPK packen"
( cd "$WORK" && tar cpf "$SPK" INFO scripts package.tgz )

# --- Ergebnis ---------------------------------------------------------------
rm -rf "$WORK"
echo "==> Fertig: $SPK"
echo ""
echo "Installation auf dem NAS:"
echo "  1. Datei auf den NAS kopieren"
echo "  2. DSM: Paketcenter -> Installieren -> Manuelle Installation -> Datei wählen"
echo "  3. Nach der Installation öffnet sich automatisch http://<NAS-IP>:8081/"
