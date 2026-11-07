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

# **Zeilenenden auf LF erzwingen – für ALLE Textdateien.**
# Das DSM führt die Lifecycle-Skripte mit BusyBox-ash aus. Ein CR am Zeilenende
# macht daraus "command not found" (\r wird Teil des Befehlsnamens). Auf
# Windows entstehen CRLF-Dateien sofort (core.autocrlf=false lässt sie
# unverändert im Blob) - deshalb wird hier **immer** konvertiert, nicht nur
# für conf/privilege.
if command -v tr >/dev/null 2>&1; then
    for text_file in \
        "$WORK"/scripts/* \
        "$WORK/target/bin/"*.sh \
        "$WORK/target/compose/compose.yaml"
    do
        [ -f "$text_file" ] || continue
        tr -d '\r' < "$text_file" > "$text_file.tmp" && mv "$text_file.tmp" "$text_file"
    done
fi

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

# --- conf/resource (Pflicht, sobald das Paket Worker benutzt) ---------------
# `conf/resource` beschreibt die DSM-Worker, die das Paket anfordert. Ohne
# diese Datei startet der Container nicht - der Worker "docker-project"
# legt das Compose-Projekt an und startet/stoppt es beim Start/Stopp des
# Pakets (als Root, damit das Paket selbst keine Root-Rechte braucht).
RESOURCE="src/conf/resource"
if [ ! -f "$RESOURCE" ]; then
    echo "FEHLER: $RESOURCE fehlt." >&2
    echo "       Ohne conf/resource startet der Container nicht." >&2
    exit 1
fi
cp "$RESOURCE" "$WORK/conf/resource"

# **Zeilenenden auf LF erzwingen.** Das DSM-Paketcenter lehnt eine
# `privilege`-Datei mit CR-Zeichen ab ("invalid package privilege content",
# Fehlercode 319) - der Parser verträgt das Windows-Zeilenende nicht.
# Die Datei kann CRLF im Repository haben (core.autocrlf=false auf Windows
# laesst sie unveraendert im Blob), deshalb wird hier **immer** konvertiert.
# Die Referenzpakete von SynoCommunity entstehen mit `jq` auf Linux und
# haben deshalb immer LF.
for conf_file in "$WORK/conf/privilege" "$WORK/conf/resource"; do
    if command -v tr >/dev/null 2>&1; then
        tr -d '\r' < "$conf_file" > "$conf_file.tmp"
        mv "$conf_file.tmp" "$conf_file"
    fi
done

# Rechte wie bei den funktionierenden Referenzpaketen (spksrc):
# Verzeichnis 0755, Datei 0644.
chmod 755 "$WORK/conf"
chmod 644 "$WORK/conf/privilege" "$WORK/conf/resource"

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

# --- Compose-Datei des Docker-Project-Workers ------------------------------
# Die Datei liegt unter target/compose/compose.yaml und wird vom DSM-Worker
# "docker-project" verwaltet. Sie muss statisch sein - der Worker startet
# das Projekt beim Start des Pakets, also darf sie nicht erst zur Laufzeit
# entstehen. Die Platzhalter werden hier ersetzt:
#
#   @IMAGE@   → das Image mit dem exakten Versions-Tag
#   @VAR_DIR@ → das persistente Verzeichnis des Pakets
COMPOSE_YAML="$WORK/target/compose/compose.yaml"
if [ ! -f "$COMPOSE_YAML" ]; then
    echo "FEHLER: $COMPOSE_YAML fehlt." >&2
    echo "       Ohne Compose-Datei kann der DSM-Worker das Projekt nicht anlegen." >&2
    exit 1
fi
sed -i -e "s|@IMAGE@|ghcr.io/rf77/comfobox-gateway:${VERSION}|g" \
    -e "s|@VAR_DIR@|/var/packages/${PACKAGE}/var|g" \
    "$COMPOSE_YAML"

# Auch die Compose-Datei braucht LF (CRLF bricht den YAML-Parser von
# Docker Compose in manchen Versionen).
if command -v tr >/dev/null 2>&1; then
    tr -d '\r' < "$COMPOSE_YAML" > "$COMPOSE_YAML.tmp"
    mv "$COMPOSE_YAML.tmp" "$COMPOSE_YAML"
fi

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

# ---------------------------------------------------------------------------
# package.tgz – ACHTUNG: die Struktur ist entscheidend!
#
# Das DSM **entpackt package.tgz direkt in das Zielverzeichnis** des Pakets.
# Im DSM-Log steht das so:
#
#   mv -f /volume3/@tmp/synopkg/install.XXXX/package \
#        /usr/local/packages/@appstore/ComfoBoxGateway
#
# "package" ist der entpackte Inhalt von package.tgz – er wird zum
# Zielverzeichnis selbst. Der Inhalt von package.tgz muss deshalb die
# Dateien **direkt auf der obersten Ebene** enthalten:
#
#   bin/common.sh
#   bin/serial-scan.sh
#   compose/compose.yaml
#   image
#
# Mit einer zusätzlichen Ebene "target/" (Revision 0006) landen die Dateien
# eine Stufe zu tief:
#
#   <ziel>/target/bin/common.sh
#   <ziel>/target/compose/compose.yaml
#
# Der DSM-Worker "docker-project" sucht die Compose-Datei aber unter
# <ziel>/compose (conf/resource: "path": "compose", relativ zum Ziel) – und
# findet sie nicht. Der Worker meldet daraufhin "(fail)" und die
# Installation bricht ab:
#
#   Acquire docker-project for ComfoBoxGateway when 0x0001 (fail)
#
# (Genau das war der Fehler in Revision 0006.)
# ---------------------------------------------------------------------------
( cd "$WORK/target" \
  && find . -mindepth 1 -maxdepth 1 | sed 's|^\./||' \
  | tar cpf - $TAR_REPRODUCIBLE --files-from=/dev/stdin \
  | gzip -n > "$WORK/package.tgz" )

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
echo "Installation auf dem NAS - Variante A (empfohlen):"
echo "  Paketcenter -> Einstellungen -> Paketquellen -> Hinzufügen"
echo "    Name: ComfoBox"
echo "    URL:  https://github.com/RF77/comfobox-mqtt/releases/download/v1.0.1/packages.json"
echo "  Danach erscheint das Paket unter 'Community' und wird von dort installiert."
echo "  (Es muss KEINE Datei auf die NAS kopiert werden.)"
echo ""
echo "Installation auf dem NAS - Variante B (manuell):"
echo "  1. Datei auf den NAS kopieren"
echo "  2. DSM: Paketcenter -> Installieren -> Manuelle Installation -> Datei wählen"
echo "  3. Nach der Installation öffnet sich automatisch http://<NAS-IP>:8081/"
