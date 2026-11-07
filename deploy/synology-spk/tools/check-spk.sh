#!/bin/bash
# ---------------------------------------------------------------------------
# check-spk.sh – prüft ein gebautes Synology-Paket auf die Anforderungen,
# die das DSM-Paketcenter stellt.
#
# Aufruf:  bash tools/check-spk.sh [pfad/zum/paket.spk]
#          (ohne Argument: das zuletzt gebaute Paket in dist/)
#
# Warum es das gibt:
#   Das DSM verweigert die Installation **schon beim Hochladen**, wenn das
#   Paket keine `conf/privilege` enthält ("kann nicht installiert werden,
#   da es mit Root-Rechten ausgeführt wird"). Das ist beim Bauen auf einem
#   Linux-Rechner nicht sichtbar - erst auf dem NAS. Dieser Test macht den
#   Fehler sichtbar, bevor das Paket das NAS erreicht.
# ---------------------------------------------------------------------------
set -u

cd "$(dirname "$0")/.." || exit 1

SPK="${1:-}"
if [ -z "$SPK" ]; then
    # Das neueste Paket in dist/ nehmen.
    SPK=$(ls -t dist/*.spk 2>/dev/null | head -n 1)
fi

if [ -z "$SPK" ] || [ ! -f "$SPK" ]; then
    echo "FEHLER: Kein SPK gefunden (${SPK:-dist/*.spk})" >&2
    echo "       Erst bauen: bash build-spk.sh" >&2
    exit 1
fi

echo "Pruefe: $SPK"
echo ""

fail=0
ok()  { echo "  OK   $1"; }
bad() { echo "  FAIL $1"; fail=1; }

# --- tar-Hilfsfunktionen -----------------------------------------------------
# **Warum so umstaendlich?** Unter Windows (Git Bash) liest GNU tar einen
# Pfad wie "C:\Users\...\paket.spk" als **Remote-Host** ("tar: host name
# can't be extracted from ...") und haengt sich auf. Der Trick: in das
# Verzeichnis des Pakets wechseln und mit dem **Basisnamen** arbeiten.
# Unter Linux ist das ebenfalls gueltig.
SPK_DIR=$(CDPATH= cd -- "$(dirname -- "$SPK")" && pwd) || exit 1
SPK_NAME=$(basename -- "$SPK")

tar_list()    { ( cd "$SPK_DIR" && tar tpf "$SPK_NAME" ); }
tar_unpack()  { ( cd "$SPK_DIR" && tar xpf "$SPK_NAME" -C "$1" "$2" 2>/dev/null ); }

# Einen Member nach stdout holen. **Uber eine Datei, nicht ueber eine Pipe:**
# ein `tar xzO` am Pipe-Ende wartet auf stdin - und bekommt nichts, wenn die
# Pipe leer ist. Es blockiert dann auf dem Terminal (Ctrl-C noetig).
tar_get_to() {
    # $1 = Member, $2 = Zieldatei
    ( cd "$SPK_DIR" && tar xOf "$SPK_NAME" "$1" ) > "$2" 2>/dev/null
}

# Inhalt auflisten
LIST=$(mktemp 2>/dev/null || mktemp -t comfobox)
WORK=$(mktemp -d 2>/dev/null || mktemp -d -t comfobox)
trap 'rm -rf "$WORK" "$LIST"' EXIT

if ! tar_list > "$LIST" 2>/dev/null; then
    echo "FEHLER: Das Paket laesst sich nicht lesen ($SPK)." >&2
    exit 1
fi

# --- 1. conf/privilege (Pflicht ab DSM 7.0) ---------------------------------
echo "conf/privilege (Pflicht ab DSM 7.0)"
if grep -qx "conf/privilege" "$LIST"; then
    ok "conf/privilege ist im Paket"
else
    bad "conf/privilege FEHLT - das DSM verweigert die Installation"
    echo "       (Das Paket gilt dann als Root-Paket.)"
    echo "       Siehe src/conf/README.md"
fi

# --- 2. conf/ im Wurzelverzeichnis, nicht im package.tgz --------------------
echo ""
echo "Ablage von conf/"
if grep -qx "package.tgz" "$LIST"; then
    if tar_get_to "package.tgz" "$WORK/package.tgz"; then
        if tar tzf "$WORK/package.tgz" 2>/dev/null | grep -q "^conf/privilege"; then
            bad "conf/privilege liegt im package.tgz - dort sieht das DSM sie nicht"
        else
            ok "conf/privilege liegt im SPK-Wurzelverzeichnis (nicht im package.tgz)"
        fi
    else
        bad "package.tgz laesst sich nicht auspacken"
    fi
else
    bad "package.tgz fehlt"
fi

# --- 3. Inhalt der privilege-Datei ------------------------------------------
echo ""
echo "Inhalt von conf/privilege"
mkdir -p "$WORK/x"
if tar_unpack "$WORK/x" "conf/privilege"; then
    PRIV="$WORK/x/conf/privilege"

    # Gueltiges JSON? Ein Interpreter wird nur genommen, wenn er auch
    # **funktioniert** - unter Windows liegt oft ein Microsoft-Store-Stub
    # namens "python3" im PATH, der beim Aufruf fehlschlaegt.
    JSON_OK=0
    for cand in python3 python; do
        if command -v "$cand" >/dev/null 2>&1 \
           && "$cand" -c 'import json' >/dev/null 2>&1; then
            if "$cand" -c 'import json, sys; json.load(open(sys.argv[1]))' "$PRIV" >/dev/null 2>&1; then
                JSON_OK=1
            fi
            break
        fi
    done
    if [ "$JSON_OK" = "0" ] && command -v node >/dev/null 2>&1; then
        WINPRIV="$PRIV"
        command -v cygpath >/dev/null 2>&1 && WINPRIV=$(cygpath -w "$PRIV")
        if node -e 'JSON.parse(require("fs").readFileSync(process.argv[1],"utf8"))' "$WINPRIV" >/dev/null 2>&1; then
            JSON_OK=1
        fi
    fi

    if [ "$JSON_OK" = "1" ]; then
        ok "gueltige JSON-Datei"
    else
        bad "keine gueltige JSON-Datei"
    fi

    # "defaults.run-as" muss "package" sein. Der Block steht direkt unter
    # "defaults" - deshalb die folgenden Zeilen mitpruefen.
    if grep -A3 '"defaults"' "$PRIV" 2>/dev/null \
       | grep -q '"run-as"[[:space:]]*:[[:space:]]*"package"'; then
        ok "defaults.run-as = package"
    else
        bad "defaults.run-as ist nicht 'package'"
    fi

    # ctrl-script: welche Identitaet?
    CTRL=$(grep -o '"action"[[:space:]]*:[[:space:]]*"[a-z]*"' "$PRIV" 2>/dev/null \
           | sed 's/.*"\([a-z]*\)"$/\1/' | sort -u | tr '\n' ' ')
    if [ -n "$CTRL" ]; then
        ok "ctrl-script: $CTRL"
    else
        bad "ctrl-script ist leer - die Lifecycle-Skripte brauchen eine Identitaet"
    fi

    # Rechte der Datei: 0644 (wie spksrc es macht)
    MODE=$(tar tvpf "$SPK" 2>/dev/null | grep 'conf/privilege$' | awk '{print $1}' | cut -c1-10)
    case "$MODE" in
        *rw-r--r--*) ok "conf/privilege hat Modus 0644" ;;
        *) bad "conf/privilege hat Modus '$MODE' (erwartet 0644)" ;;
    esac
else
    bad "conf/privilege laesst sich nicht auspacken"
fi

# --- 4. Icons ----------------------------------------------------------------
echo ""
echo "Icons"

# PNG-Groesse aus dem IHDR-Chunk (Bytes 16..23).
png_size() {
    od -An -tu1 -j16 -N8 "$1" 2>/dev/null | awk '
        { w = $1*16777216 + $2*65536 + $3*256 + $4
          h = $5*16777216 + $6*65536 + $7*256 + $8
          print w "x" h }'
}

check_icon() {
    name="$1"
    expected="$2"
    if ! tar_unpack "$WORK/x" "$name"; then
        bad "$name fehlt"
        return
    fi
    actual=$(png_size "$WORK/x/$name")
    if [ "$actual" = "$expected" ]; then
        ok "$name ist $actual"
    else
        bad "$name ist $actual, erwartet $expected"
    fi
}

# DSM 7.0 verlangt PACKAGE_ICON.PNG mit 64x64 (Breaking Change).
check_icon "PACKAGE_ICON.PNG" "64x64"
check_icon "PACKAGE_ICON_256.PNG" "256x256"

# --- 5. INFO: Pflichtfelder -------------------------------------------------
echo ""
echo "INFO"
if tar_unpack "$WORK/x" "INFO"; then
    INFO="$WORK/x/INFO"
    for field in package version os_min_ver description arch maintainer; do
        if grep -q "^${field}=\"" "$INFO"; then
            ok "INFO hat $field"
        else
            bad "INFO hat kein Feld '$field' (Pflichtfeld nach DSM-Doku)"
        fi
    done

    # os_min_ver muss >= 7.0-40000 sein.
    OSVER=$(grep '^os_min_ver="' "$INFO" | head -n 1 | cut -d'"' -f2)
    MAJOR=$(echo "$OSVER" | cut -d. -f1)
    BUILD=$(echo "$OSVER" | cut -d- -f2)
    if [ "${MAJOR:-0}" -gt 7 ] 2>/dev/null \
       || { [ "${MAJOR:-0}" -eq 7 ] 2>/dev/null && [ "${BUILD:-0}" -ge 40000 ] 2>/dev/null; }; then
        ok "os_min_ver ist $OSVER (>= 7.0-40000)"
    else
        bad "os_min_ver ist $OSVER - das DSM verlangt mindestens 7.0-40000"
    fi

    # Das Image im Paket muss zum Versions-Tag passen.
    IMAGE=""
    if tar_get_to "package.tgz" "$WORK/pkg-info.tgz"; then
        IMAGE=$(tar xzOf "$WORK/pkg-info.tgz" target/image 2>/dev/null | head -n 1)
    fi
    SPK_VERSION=$(grep '^version="' "$INFO" | head -n 1 | cut -d'"' -f2 | cut -d- -f1)
    if [ -n "$IMAGE" ]; then
        if echo "$IMAGE" | grep -q ":${SPK_VERSION}\$"; then
            ok "Image $IMAGE passt zur Version $SPK_VERSION"
        else
            bad "Image $IMAGE passt nicht zur Version $SPK_VERSION"
        fi
    else
        bad "target/image fehlt im package.tgz"
    fi

    # Das Paket liefert ein conf/-Verzeichnis -> das DSM muss davon wissen.
    if grep -q '^support_conf_folder="yes"' "$INFO"; then
        ok "support_conf_folder=yes (das DSM liest das conf/-Verzeichnis)"
    else
        bad "support_conf_folder fehlt in der INFO - das DSM ignoriert das conf/-Verzeichnis"
    fi
else
    bad "INFO laesst sich nicht auspacken"
fi

# --- 6. Pflichtbestandteile -------------------------------------------------
echo ""
echo "Bestandteile"
for part in INFO conf/privilege scripts package.tgz PACKAGE_ICON.PNG; do
    if grep -q "^${part}" "$LIST"; then
        ok "$part"
    else
        bad "$part fehlt"
    fi
done

# --- Ergebnis ----------------------------------------------------------------
echo ""
if [ "$fail" -eq 0 ]; then
    echo "OK - das Paket erfuellt die Anforderungen des DSM-Paketcenters."
    exit 0
fi

echo "FEHLER - das Paket wuerde das DSM-Paketcenter nicht akzeptieren." >&2
exit 1
