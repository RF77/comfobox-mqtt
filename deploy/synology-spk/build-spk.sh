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
# Ohne Argumente kommen VERSION und REVISION aus den Dateien
# `src/VERSION` bzw. `REVISION` - so baut der lokale Aufruf und der
# CI-Workflow garantiert dasselbe Paket.
#
# Ergebnis:
#   dist/ComfoBoxGateway_noarch-dsm7_<VERSION>-<REVISION>.spk
# ---------------------------------------------------------------------------
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$SCRIPT_DIR"

read_or() {
    # $1 = Datei, $2 = Vorgabe
    if [ -f "$1" ]; then
        tr -d ' \t\r\n' < "$1"
    else
        echo "$2"
    fi
}

VERSION=${1:-$(read_or src/VERSION 1.0.0)}
REVISION=${2:-$(read_or REVISION 0001)}
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

# --- conf/privilege (Pflicht ab DSM 7.0) ------------------------------------
# **Ohne** diese Datei gilt das Paket als Root-Paket und das DSM-Paketcenter
# verweigert die Installation schon beim Hochladen:
#   "kann nicht installiert werden, da es mit Root-Rechten ausgefuehrt wird"
# Sie muss im SPK **im Wurzelverzeichnis** liegen (nicht im package.tgz).
#
# Nur die `privilege`-Datei wird kopiert - die README.md daneben ist
# Dokumentation und gehoert nicht ins Paket.
PRIVILEGE="src/conf/privilege"
if [ ! -f "$PRIVILEGE" ]; then
    echo "FEHLER: $PRIVILEGE fehlt." >&2
    echo "       Ohne conf/privilege installiert DSM 7 das Paket nicht -" >&2
    echo "       es gilt dann als Root-Paket (siehe src/conf/README.md)." >&2
    exit 1
fi
mkdir -p "$WORK/conf"
cp "$PRIVILEGE" "$WORK/conf/privilege"

# **Zeilenenden auf LF erzwingen.** Das DSM-Paketcenter lehnt eine
# `privilege`-Datei mit CR-Zeichen ab ("invalid package privilege content",
# Fehlercode 319) - der Parser verträgt das Windows-Zeilenende nicht.
# Die Datei kann CRLF im Repository haben (core.autocrlf=false auf Windows
# laesst sie unveraendert im Blob), deshalb wird hier **immer** konvertiert.
# Die Referenzpakete von SynoCommunity entstehen mit `jq` auf Linux und
# haben deshalb immer LF.
if command -v tr >/dev/null 2>&1; then
    tr -d '\r' < "$WORK/conf/privilege" > "$WORK/conf/privilege.tmp"
    mv "$WORK/conf/privilege.tmp" "$WORK/conf/privilege"
fi

# Rechte wie bei den funktionierenden Referenzpaketen (spksrc):
# Verzeichnis 0755, Datei 0644.
chmod 755 "$WORK/conf"
chmod 644 "$WORK/conf/privilege"

# Icon: das Paketcenter braucht PACKAGE_ICON.PNG (256×256).
# Erzeugt mit:  node tools/make-icon.mjs
ICON=""
for candidate in src/PACKAGE_ICON.PNG src/PACKAGE_ICON.png; do
    if [ -f "$candidate" ]; then
        ICON="$candidate"
        break
    fi
done

if [ -z "$ICON" ]; then
    echo "FEHLER: Es gibt kein Icon (src/PACKAGE_ICON.PNG)." >&2
    echo "       Erzeugen mit: node tools/make-icon.mjs" >&2
    exit 1
fi

# **DSM 7.0 verlangt PACKAGE_ICON.PNG mit 64x64** (Breaking Change: vorher
# 72x72). Die grosse Variante kommt als PACKAGE_ICON_256.PNG daneben.
ICON_64="src/PACKAGE_ICON_64.png"
if [ ! -f "$ICON_64" ]; then
    echo "FEHLER: Es gibt kein 64x64-Icon ($ICON_64)." >&2
    echo "       Erzeugen mit: node tools/make-icon.mjs --size 64 $ICON_64" >&2
    exit 1
fi

cp "$ICON_64" "$WORK/PACKAGE_ICON.PNG"
cp "$ICON" "$WORK/PACKAGE_ICON_256.PNG"

# --- package.tgz (Inhalt des Pakets) ---------------------------------------
echo "==> package.tgz erstellen"

# Die Image-Referenz steht im Paket: der Supervisor zieht genau dieses
# Image. So kann das SPK nie versehentlich eine andere Version starten.
printf 'ghcr.io/rf77/comfobox-gateway:%s\n' "$VERSION" > "$WORK/target/image"

# **Reproduzierbar bauen.** Ohne diese Optionen wandern der Zeitstempel und
# die Reihenfolge der Einträge in das Archiv - derselbe Quellstand ergibt
# dann jedes Mal eine andere MD5. Das ist mehr als Kosmetik: die `checksum`
# in der INFO und die MD5 in der `packages.json` (Paketquelle des DSM) waeren
# dann nicht mehr nachrechenbar.
#
# `SOURCE_DATE_EPOCH` ist der Zeitstempel des letzten Commits - damit sind
# auch der lokale Build und der CI-Build bitgleich. (Gleiches Prinzip wie
# in SynoCommunity/spksrc, `TAR_REPRODUCIBLE`.)
SOURCE_DATE_EPOCH=$(git log -1 --format=%ct 2>/dev/null || echo 0)
[ "$SOURCE_DATE_EPOCH" -gt 0 ] 2>/dev/null || SOURCE_DATE_EPOCH=0
TAR_REPRODUCIBLE="--sort=name --mtime=@${SOURCE_DATE_EPOCH} --owner=root --group=root --numeric-owner"

( cd "$WORK" && find target -mindepth 1 -maxdepth 1 | tar cpf - $TAR_REPRODUCIBLE --files-from=/dev/stdin | gzip -n > package.tgz )

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
# `conf` **muss** mit in die Liste - ohne conf/privilege installiert DSM 7
# das Paket nicht (es gilt dann als Root-Paket).
( cd "$WORK" && tar cpf "$SPK" $TAR_REPRODUCIBLE INFO conf scripts package.tgz PACKAGE_ICON.PNG PACKAGE_ICON_256.PNG )

# --- Fertiges Paket pruefen -------------------------------------------------
# Die inhaltliche Pruefung (conf/privilege vorhanden, Icon-Groessen, Pflicht-
# felder der INFO) macht tools/check-spk.sh - sie laeuft auch in der CI.

# --- Ergebnis ---------------------------------------------------------------
rm -rf "$WORK"
echo "==> Fertig: $SPK"
echo ""
echo "Installation auf dem NAS:"
echo "  1. Datei auf den NAS kopieren"
echo "  2. DSM: Paketcenter -> Installieren -> Manuelle Installation -> Datei wählen"
echo "  3. Nach der Installation öffnet sich automatisch http://<NAS-IP>:8081/"
