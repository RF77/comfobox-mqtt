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

    # **KEIN ctrl-script!**
    # Auf DSM 7 lehnt das Paketzentrum eine privilege-Datei ab, die ueber
    # ctrl-script Root-Rechte verlangt ("invalid package privilege content",
    # Fehler 319). Belegt durch den Variantentest auf der DS923+:
    #   mit ctrl-script  -> 319
    #   ohne ctrl-script -> akzeptiert
    # Offizielles Muster (Synology Developer Guide, "Docker package"):
    #   { "defaults": { "run-as": "package" }, "username": "Gitlab" }
    if grep -q '"ctrl-script"' "$PRIV"; then
        bad "conf/privilege enthaelt 'ctrl-script' - DSM 7 lehnt das mit Fehler 319 ab"
        echo "       Erlaubt sind nur defaults, username und groupname."
        echo "       Privilegierte Aufgaben uebernimmt der Worker in conf/resource."
    else
        ok "kein 'ctrl-script' (Pflicht ab DSM 7)"
    fi

    # username: der Paket-Benutzer. Er muss gesetzt sein, sonst laeuft das
    # Paket unter einem Namen, den das DSM nicht kennt.
    if grep -q '"username"' "$PRIV"; then
        ok "username ist gesetzt"
    else
        bad "conf/privilege ohne 'username'"
    fi

    # Rechte der Datei: 0644 (wie spksrc es macht)
    MODE=$(tar tvpf "$SPK" 2>/dev/null | grep 'conf/privilege$' | awk '{print $1}' | cut -c1-10)
    case "$MODE" in
        *rw-r--r--*) ok "conf/privilege hat Modus 0644" ;;
        *) bad "conf/privilege hat Modus '$MODE' (erwartet 0644)" ;;
    esac

    # **Keine CR-Zeichen!** Das DSM-Paketcenter lehnt eine privilege-Datei
    # mit Windows-Zeilenenden ab ("invalid package privilege content",
    # Fehlercode 319). Der Fehler ist auf der NAS nur schwer zu finden -
    # deshalb wird er hier geprüft.
    if tr -d '\r' < "$PRIV" | cmp -s - "$PRIV"; then
        ok "conf/privilege hat nur LF-Zeilenenden"
    else
        bad "conf/privilege enthaelt CR-Zeichen (CRLF) - das DSM lehnt die Datei ab (Fehler 319)"
        echo "       Siehe deploy/synology-spk/src/conf/README.md"
    fi

    # "groupname" ist erlaubt - das Plex-Paket auf derselben NAS nutzt es
    # genauso. Nur klein geschriebene Benutzernamen hat das DSM mit
    # "failed to revise file attributes" (Fehler 313) abgelehnt.
    if grep -q '"groupname"' "$PRIV"; then
        ok "groupname ist gesetzt (wie im Plex-Paket)"
    else
        ok "kein 'groupname' (zulassen)"
    fi
else
    bad "conf/privilege laesst sich nicht auspacken"
fi

# --- 3b. conf/resource (Worker) ---------------------------------------------
echo ""
echo "conf/resource (DSM-Worker)"
if grep -qx "conf/resource" "$LIST"; then
    ok "conf/resource ist im Paket"
else
    bad "conf/resource FEHLT - ohne Worker startet der Container nicht"
fi

if tar_unpack "$WORK/x" "conf/resource"; then
    RES="$WORK/x/conf/resource"
    if tr -d '\r' < "$RES" | cmp -s - "$RES"; then
        ok "conf/resource hat nur LF-Zeilenenden"
    else
        bad "conf/resource enthaelt CR-Zeichen (CRLF)"
    fi
    if grep -q '"docker-project"' "$RES"; then
        ok "conf/resource nutzt den Worker 'docker-project'"
    else
        bad "conf/resource nutzt den Worker 'docker-project' nicht"
    fi
    if grep -q '"path"' "$RES"; then
        ok "conf/resource specifies the compose path"
    else
        bad "conf/resource ohne 'path' - der Worker findet die Compose-Datei nicht"
    fi
else
    bad "conf/resource laesst sich nicht auspacken"
fi

# --- 3c. Compose-Datei des Workers ------------------------------------------
echo ""
echo "Compose-Datei (target/compose/compose.yaml)"
if tar_get_to "package.tgz" "$WORK/package.tgz" 2>/dev/null \
   && tar tzf "$WORK/package.tgz" 2>/dev/null | grep -q "^target/compose/compose.yaml$"; then
    ok "target/compose/compose.yaml ist im package.tgz"
    mkdir -p "$WORK/c"
    if ( cd "$WORK" && tar xzf package.tgz target/compose/compose.yaml ) 2>/dev/null; then
        CF="$WORK/target/compose/compose.yaml"
        if grep -q '@[A-Z_]*@' "$CF"; then
            bad "compose.yaml enthaelt noch Platzhalter (@IMAGE@/@VAR_DIR@) - build-spk.sh hat sie nicht ersetzt"
        else
            ok "compose.yaml hat keine Platzhalter mehr"
        fi
        if grep -q 'image: ghcr.io/rf77/comfobox-gateway:' "$CF"; then
            ok "compose.yaml referenziert das Image aus GHCR"
        else
            bad "compose.yaml ohne Image-Referenz"
        fi
    fi
else
    bad "target/compose/compose.yaml fehlt im package.tgz"
fi

# --- 3d. Lifecycle-Skripte --------------------------------------------------
#
# **Regression (Revision 0005 -> 0006):**
#   Zum Zeitpunkt von "preinst" ist das Zielverzeichnis des Pakets noch
#   NICHT gefuellt - das package.tgz wird erst danach entpackt. Ein Skript,
#   das in preinst eine Datei aus target/ laed (z. B. common.sh), bricht
#   mit exit 1 ab. Das DSM meldet dann nur:
#     "Paket kann nicht installiert werden. Versuchen Sie es spaeter erneut"
#
#   Zweiter Regression: common.sh wurde ueber einen **festen** Pfad
#   geladen ($SELF_DIR/../target/bin/common.sh). DSM fuehrt die Skripte je
#   nach Phase aus verschiedenen Verzeichnissen aus - der Pfad stimmte
#   nur in einer davon.
#
# Beides wird hier geprueft, damit es nicht zurueckkommt.
echo ""
echo "Lifecycle-Skripte"

# Prueft, ob ein Skript eine Datei aus target/ (also aus dem package.tgz)
# voraussetzt. In preinst ist das verboten - in den uebrigen erlaubt.
script_needs_target()
{
    # $1 = Skriptdatei (bereits von Kommentaren befreit), $2 = verbotener Text
    grep -q "$2" "$1" 2>/dev/null
}

# Kommentarzeilen entfernen. Sonst schlagen die Checks auf die
# Dokumentation des alten Fehlers an: die Skripte erklaeren im Kommentar,
# warum sie frueher "target/bin/common.sh" geladen haben. Geprueft werden
# muss aber der **ausfuehrbare Code**, nicht der Kommentar.
# Nur Zeilen, die mit # beginnen, gelten als Kommentar.
strip_comments()
{
    # $1 = Eingabedatei, $2 = Ausgabedatei
    grep -v '^[[:space:]]*#' "$1" > "$2" 2>/dev/null || : > "$2"
}

for s in preinst postinst preuninst postuninst start-stop-status; do
    if tar_unpack "$WORK/x" "scripts/$s"; then
        S="$WORK/x/scripts/$s"
        strip_comments "$S" "$S.code"

        # LF-Zeilenenden: BusyBox-ash interpretiert ein CR am Zeilenende
        # als Teil des Befehlsnamens ("command not found").
        if tr -d '\r' < "$S" | cmp -s - "$S"; then
            ok "scripts/$s hat nur LF-Zeilenenden"
        else
            bad "scripts/$s enthaelt CR-Zeichen (CRLF)"
        fi

        # "set -u" ist auf DSM 7 gefaehrlich: in der Phase "preinst" ist
        # SYNOPKG_PKGDEST nicht garantiert gesetzt, ein "unbound variable"
        # beendet das Skript (Fehler 1: "failed to run preinst script").
        if grep -q '^set -u' "$S.code"; then
            bad "scripts/$s setzt 'set -u' - nicht gesetzte DSM-Variablen brechen das Skript ab"
        else
            ok "scripts/$s setzt kein 'set -u'"
        fi

        # Jedes Skript muss mit exit 0 enden (oder unbedingt mit 0
        # zurueckkommen) - sonst bricht die Installation ab.
        if grep -q 'exit 0' "$S.code"; then
            ok "scripts/$s endet mit exit 0"
        else
            bad "scripts/$s endet NICHT mit exit 0 - die Installation wuerde abbrechen"
        fi
    else
        bad "scripts/$s fehlt im Paket"
    fi
done

# **Die eigentliche Regression:** preinst darf KEINE Datei aus dem
# package.tgz voraussetzen - das Verzeichnis ist dort noch nicht gefuellt.
if tar_unpack "$WORK/x" "scripts/preinst"; then
    PRE="$WORK/x/scripts/preinst"
    strip_comments "$PRE" "$PRE.code"
    if script_needs_target "$PRE.code" 'target/bin'; then
        bad "scripts/preinst laed eine Datei aus target/ - dort ist zu diesem Zeitpunkt nichts entpackt"
        echo "       (Revision 0006 hat das behoben: preinst ist eigenstaendig)"
    else
        ok "scripts/preinst ist eigenstaendig (kein Zugriff auf target/)"
    fi
    if script_needs_target "$PRE.code" 'common.sh'; then
        bad "scripts/preinst laed common.sh - die Datei existiert dort noch nicht"
    else
        ok "scripts/preinst laed kein common.sh"
    fi
fi

# common.sh darf nicht auf SYNOPKG_PKGVAR setzen - die Variable gibt es
# in DSM 7 nicht. Das Datenverzeichnis liegt immer unter
# /var/packages/<paket>/var.
if tar_get_to "package.tgz" "$WORK/pkg2.tgz" \
   && tar xzOf "$WORK/pkg2.tgz" target/bin/common.sh > "$WORK/common.sh" 2>/dev/null; then
    strip_comments "$WORK/common.sh" "$WORK/common.code"
    if grep -q 'SYNOPKG_PKGVAR' "$WORK/common.code"; then
        bad "common.sh benutzt SYNOPKG_PKGVAR - diese Variable gibt es in DSM 7 nicht"
    else
        ok "common.sh benutzt kein SYNOPKG_PKGVAR"
    fi
    if grep -q '/var/packages/' "$WORK/common.code"; then
        ok "common.sh nutzt /var/packages/<paket>/var als Datenverzeichnis"
    else
        bad "common.sh nutzt kein /var/packages/<paket>/var"
    fi
    # Die common.sh muss an mehreren Orten gesucht werden, weil DSM die
    # Skripte je nach Phase aus verschiedenen Verzeichnissen ausfuehrt.
    for s in postinst start-stop-status; do
        if tar_unpack "$WORK/x" "scripts/$s"; then
            strip_comments "$WORK/x/scripts/$s" "$WORK/x/scripts/$s.code"
            if grep -q 'SYNOPKG_PKGDEST:-}/bin/common.sh' "$WORK/x/scripts/$s.code" \
               && grep -q '\.\./bin/common.sh' "$WORK/x/scripts/$s.code"; then
                ok "scripts/$s sucht common.sh an mehreren Orten"
            else
                bad "scripts/$s sucht common.sh nur an EINEM Ort - je nach Phase fehlt sie"
            fi
        fi
    done
else
    bad "target/bin/common.sh fehlt im package.tgz"
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
