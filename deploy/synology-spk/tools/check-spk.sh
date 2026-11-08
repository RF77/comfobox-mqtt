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

    # **Der Projektname muss klein geschrieben sein.** Docker Compose
    # akzeptiert als Projektnamen nur [a-z0-9_-]; ein Name mit
    # Grossbuchstaben laesst das Anlegen des Projekts scheitern - und der
    # Worker meldet dann nur "Acquire docker-project … (fail)".
    RES_PROJECT=$(sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$RES" | head -n 1)
    if [ -z "$RES_PROJECT" ]; then
        bad "conf/resource ohne Projektnamen ('name')"
    elif echo "$RES_PROJECT" | grep -q '^[a-z0-9][a-z0-9_-]*$'; then
        ok "Projektname '$RES_PROJECT' ist fuer Docker Compose gueltig"
    else
        bad "Projektname '$RES_PROJECT' ist ungueltig - nur [a-z0-9_-] erlaubt"
    fi

    # Der Pfad, unter dem der Worker die Compose-Datei sucht (relativ zum
    # Zielverzeichnis). Wird in 3c gegen den Inhalt von package.tgz geprueft.
    RES_PATH=$(sed -n 's/.*"path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$RES" | head -n 1)

    # Wir bauen kein Image im Paket - der Worker soll den Build-Schritt
    # deshalb ueberspringen ("build_params": {"build": false}).
    if grep -q '"build"[[:space:]]*:[[:space:]]*false' "$RES"; then
        ok "conf/resource schaltet den Build-Schritt ab (build_params.build = false)"
    else
        ok "kein 'build_params' (der Worker baut dann selbst - hier nicht noetig)"
    fi
else
    bad "conf/resource laesst sich nicht auspacken"
    RES_PATH=""
fi

# --- 3c. package.tgz: die Struktur ist entscheidend ------------------------
#
# Das DSM **entpackt package.tgz direkt in das Zielverzeichnis**:
#
#   mv -f …/install.XXXX/package /usr/local/packages/@appstore/ComfoBoxGateway
#
# Der Inhalt von package.tgz wird das Zielverzeichnis selbst. Deshalb
# müssen die Dateien **direkt auf der obersten Ebene** liegen
# (bin/, compose/, image) - nicht unter einer zusaetzlichen Ebene "target/".
#
# Mit der Ebene "target/" landen sie eine Stufe zu tief:
#   <ziel>/target/compose/docker-compose.yaml
# Der Worker "docker-project" sucht aber <ziel>/compose (conf/resource:
# "path": "compose", relativ zum Ziel) - findet nichts und meldet:
#   Acquire docker-project for ComfoBoxGateway (fail)
# Das war der Fehler in Revision 0006.
echo ""
echo "package.tgz (Struktur)"
if tar_get_to "package.tgz" "$WORK/package.tgz" 2>/dev/null; then
    TGZ_LIST="$WORK/tgz.list"
    tar tzf "$WORK/package.tgz" 2>/dev/null | sed 's|/$||' | sort > "$TGZ_LIST"

    if grep -q '^target/' "$TGZ_LIST"; then
        bad "package.tgz enthaelt eine Ebene 'target/' - die Dateien landen zu tief"
        echo "       Der DSM-Worker findet <ziel>/compose/compose.yaml dann nicht."
        echo "       (Siehe build-spk.sh: package.tgz ohne 'target/'-Praefix bauen)"
    else
        ok "package.tgz hat keine zusaetzliche 'target/'-Ebene"
    fi

    # Die Compose-Datei muss genau unter dem Pfad liegen, den conf/resource
    # nennt (relativ zum Zielverzeichnis). **Beide** Namen muessen vorhanden
    # sein - die Quellen widersprechen sich:
    #
    #   * Offizielle Entwicklerdoku (Docker Project Worker):
    #     `path` = "directory of compose.yaml, relative path from target".
    #   * `libdockerprojectgeneratorworker.so` enthaelt die Zeichenketten
    #     `docker-compose.yaml`, `docker-compose.yml`, `docker-compose.admin.yaml`
    #     (die Liste war beim Auslesen aber abgeschnitten).
    #
    # Deshalb liefert build-spk.sh beide Namen mit identischem Inhalt aus.
    WANT_PATH="${RES_PATH:-compose}"
    if grep -q "^${WANT_PATH}/docker-compose\.yaml$" "$TGZ_LIST"; then
        ok "${WANT_PATH}/docker-compose.yaml liegt auf der obersten Ebene"
    else
        bad "${WANT_PATH}/docker-compose.yaml fehlt - der Worker findet die Datei dann nicht"
        echo "       Erwartet: ${WANT_PATH}/docker-compose.yaml  (conf/resource: \"path\": \"${WANT_PATH}\")"
    fi

    if grep -q "^${WANT_PATH}/compose\.yaml$" "$TGZ_LIST"; then
        ok "${WANT_PATH}/compose.yaml liegt ebenfalls bei (Name aus der Doku)"
    else
        bad "${WANT_PATH}/compose.yaml fehlt (Name aus der offiziellen Doku)"
        echo "       build-spk.sh muss die Datei als Kopie mitliefern."
    fi

    if grep -q '^bin/common.sh$' "$TGZ_LIST"; then
        ok "bin/common.sh liegt auf der obersten Ebene"
    else
        bad "bin/common.sh liegt nicht auf der obersten Ebene"
    fi

    if grep -q '^image$' "$TGZ_LIST"; then
        ok "image (Image-Referenz) liegt auf der obersten Ebene"
    else
        bad "image fehlt auf der obersten Ebene"
    fi
else
    bad "package.tgz laesst sich nicht auspacken"
fi

# --- 3d. Compose-Datei des Workers ------------------------------------------
echo ""
echo "Compose-Datei (compose/docker-compose.yaml)"
if tar_get_to "package.tgz" "$WORK/package.tgz2" 2>/dev/null \
   && tar tzf "$WORK/package.tgz2" 2>/dev/null | grep -q "^compose/docker-compose.yaml$"; then
    ok "compose/docker-compose.yaml ist im package.tgz"
    mkdir -p "$WORK/c"
    if ( cd "$WORK" && tar xzf package.tgz2 compose/docker-compose.yaml ) 2>/dev/null; then
        CF="$WORK/compose/docker-compose.yaml"
        if grep -q '@[A-Z_]*@' "$CF"; then
            bad "docker-compose.yaml enthaelt noch Platzhalter (@IMAGE@/@VAR_DIR@) - build-spk.sh hat sie nicht ersetzt"
        else
            ok "docker-compose.yaml hat keine Platzhalter mehr"
        fi
        if grep -q 'image: ghcr.io/rf77/comfobox-gateway:' "$CF"; then
            ok "docker-compose.yaml referenziert das Image aus GHCR"
        else
            bad "docker-compose.yaml ohne Image-Referenz"
        fi
        # Das Feld "version" ist bei Compose v1 Pflicht. Ohne es lehnt
        # der Parser die Dateien ab (Compose v1 < 1.27).
        if grep -q '^version:' "$CF"; then
            ok "docker-compose.yaml hat das Feld 'version'"
        else
            bad "docker-compose.yaml ohne 'version' - Compose v1 lehnt die Datei ab"
        fi
        # Nur EIN Mount fuer die persistenten Daten. Zwei Mounts, deren
        # Host-Pfade ineinander liegen, sind eine Fehlerquelle.
        # Die Quelle darf ein benanntes Volume ("comfobox-data") oder ein
        # Pfad sein - mit oder ohne Anfuehrungszeichen.
        DATA_MOUNTS=$(grep -cE '^[[:space:]]*-[[:space:]]*"?[^":[:space:]]+:/var/lib/comfobox"?([[:space:]]|$)' "$CF")
        if [ "$DATA_MOUNTS" = "1" ]; then
            ok "docker-compose.yaml mountet genau ein Datenverzeichnis"
        else
            bad "docker-compose.yaml mountet $DATA_MOUNTS Datenverzeichnisse (erwartet: 1)"
            echo "       Der Log-Pfad liegt im Datenverzeichnis:"
            echo "       COMFOBOX_Logging__Directory=/var/lib/comfobox/logs"
        fi
        # Der Log-Pfad muss zum Mount passen.
        if grep -q 'COMFOBOX_Logging__Directory=/var/lib/comfobox/logs' "$CF"; then
            ok "Der Log-Pfad liegt im Datenverzeichnis (passt zum Mount)"
        else
            bad "docker-compose.yaml ohne COMFOBOX_Logging__Directory im Datenverzeichnis"
        fi
        # **KEIN Bind-Mount auf /var/packages!** Das DSM legt das Verzeichnis
        # als root:root an - der Container (uid 1001) kann dort nicht
        # schreiben, Serilog bricht den Start ab und der DSM-Worker meldet
        # nur "fail compose up". Stattdessen: ein benanntes Volume.
        if grep -qE '^[[:space:]]*-[[:space:]]*"?/var/packages' "$CF"; then
            bad "docker-compose.yaml bindet /var/packages/... ein - das Verzeichnis gehoert root:root"
            echo "       Der Container (uid 1001) kann dort nicht schreiben - der Start bricht ab."
            echo "       Richtig: das benannte Volume 'comfobox-data'."
        else
            ok "kein Bind-Mount auf /var/packages (nimmt das benannte Volume)"
        fi
        # Das benannte Volume muss fest benannt sein - das Backup-Skript
        # sucht genau diesen Namen.
        if grep -q 'name: comfobox-data' "$CF"; then
            ok "das Volume 'comfobox-data' hat einen festen Namen (Backup-Skript)"
        else
            bad "das Volume hat keinen festen Namen - das Backup-Skript findet es nicht"
        fi

        # **Beide Dateinamen muessen identisch sein.** Wenn sie auseinander-
        # laufen, startet der Worker je nach gewaehltem Namen eine andere
        # Konfiguration - ein Fehler, der sich nur auf der NAS zeigt.
        if ( cd "$WORK" && tar xzf package.tgz2 compose/compose.yaml ) 2>/dev/null; then
            if cmp -s "$CF" "$WORK/compose/compose.yaml"; then
                ok "compose.yaml und docker-compose.yaml sind byte-gleich"
            else
                bad "compose.yaml und docker-compose.yaml unterscheiden sich"
                echo "       Beide werden von build-spk.sh aus derselben Quelle erzeugt."
            fi
        else
            bad "compose/compose.yaml fehlt im package.tgz"
        fi
    fi
else
    bad "compose/docker-compose.yaml fehlt im package.tgz"
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

for s in preinst postinst preuninst postuninst preupgrade postupgrade start-stop-status; do
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
   && tar xzOf "$WORK/pkg2.tgz" bin/common.sh > "$WORK/common.sh" 2>/dev/null; then
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
    bad "bin/common.sh fehlt im package.tgz"
fi

# **preupgrade / postupgrade muessen vorhanden sein.** Fehlen sie, meldet
# das DSM beim Update den Fehler 261 ("package does not have preupgrade /
# postupgrade script"). Das trifft besonders dann zu, wenn eine
# Installation zuvor fehlgeschlagen ist: DSM 7.4 laesst das Paket dann im
# Status "broken" (150) zurueck und versucht beim naechsten Mal ein
# Update statt einer Neuinstallation.
for s in preupgrade postupgrade; do
    if tar_unpack "$WORK/x" "scripts/$s"; then
        ok "scripts/$s ist im Paket (verhindert Fehler 261 beim Update)"
    else
        bad "scripts/$s FEHLT - das Update scheitert dann mit Fehler 261"
    fi
done

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
        IMAGE=$(tar xzOf "$WORK/pkg-info.tgz" image 2>/dev/null | head -n 1)
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
