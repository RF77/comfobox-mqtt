#!/bin/sh
# ---------------------------------------------------------------------------
# dsm-diagnose.sh – prüft ein Synology-Paket **auf der NAS** und liefert den
# echten Fehlercode des DSM.
#
# Warum es das gibt:
#   Die Fehlermeldung im Paketzentrum („kann nicht installiert werden, da es
#   mit Root-Rechten ausgeführt wird") sagt nur, dass DSM eine Bedingung
#   nicht erfüllt sieht – nicht welche. Der Weg über die Kommandozeile
#   `synopkg install` liefert dagegen ein **JSON mit Fehlercode und Text**.
#
# Aufruf (auf der NAS, per SSH):
#   sudo sh dsm-diagnose.sh /pfad/zum/paket.spk [--install|--test|--log]
#
#   --install  Installiert das Paket und zeigt die JSON-Antwort (Fehlercode)
#   --test     Probiert mehrere Varianten der privilege-Datei durch und
#              installiert die erste, die das DSM akzeptiert. Ein Fehler in
#              prepare_install ändert nichts an der NAS.
#   --log      Zeigt nur die Log-Auszüge des DSM (nichts wird installiert)
#
# Voraussetzung: nur BusyBox-Standardwerkzeuge (sh, tar, md5sum, sha256sum).
# Ohne --install/--test wird **nichts** verändert.
# ---------------------------------------------------------------------------
set -u

SPK="${1:-}"
MODE="${2:-}"

if [ -z "$SPK" ]; then
    echo "Aufruf: sh dsm-diagnose.sh <pfad/zum/paket.spk> [--install|--test|--log]" >&2
    exit 1
fi

if [ ! -f "$SPK" ]; then
    echo "FEHLER: Datei nicht gefunden: $SPK" >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# Hilfsfunktionen
# ---------------------------------------------------------------------------

# Die DSM-Version ausgeben (auf DSM gibt es /etc/VERSION).
show_dsm_version() {
    if [ -f /etc/VERSION ]; then
        cat /etc/VERSION
        echo ""
    fi
    echo "Kernel: $(uname -r 2>/dev/null)"
}

# Die Log-Auszüge des DSM. Hier steht der **genaue** Grund, warum das
# Paket abgelehnt wurde - das synopkg-JSON sagt nur "319".
show_logs() {
    echo "--- Log-Auszüge des DSM ---"
    found=0
    for f in /var/log/synopkg.log /var/log/packages/ComfoBoxGateway.log \
             /var/log/messages /var/log/synolog/synopkg.log; do
        if [ -f "$f" ]; then
            found=1
            echo ""
            echo ">> $f"
            grep -i -E "privilege|comfobox|319|prepare_install" "$f" 2>/dev/null \
                | tail -25 || echo "   (keine passenden Zeilen)"
        fi
    done
    if [ "$found" = "0" ]; then
        echo "(keine Log-Dateien gefunden)"
    fi
    echo ""
    # Das Log, in das synopkg selbst schreibt:
    if [ -f /var/log/synopkg.log ]; then
        echo ">> /var/log/synopkg.log (letzte 15 Zeilen)"
        tail -15 /var/log/synopkg.log 2>/dev/null || true
        echo ""
    fi
}

# Die package.tgz-Prüfsumme aus der INFO lesen (DSM prüft sie).
show_info() {
    TMP=$(mktemp -d 2>/dev/null) || TMP=/tmp/diag.$$
    mkdir -p "$TMP"
    if tar xf "$SPK" -C "$TMP" INFO 2>/dev/null; then
        grep -E '^(package|version|arch|os_min_ver|support_conf_folder|startable|ctl_stop|install_type|checksum)=' "$TMP/INFO"
    else
        echo ">> (nicht auspackbar)"
    fi
    rm -rf "$TMP"
}

# ---------------------------------------------------------------------------
# 1. Kopfdaten
# ---------------------------------------------------------------------------
echo "=============================================================="
echo " ComfoBox Gateway - Paketdiagnose auf der NAS"
echo "=============================================================="
echo ""

echo "--- 1. DSM-Version ---"
show_dsm_version
echo ""

echo "--- 2. Die Datei ---"
ls -l "$SPK"
echo ""
if command -v sha256sum >/dev/null 2>&1; then
    echo "SHA256: $(sha256sum "$SPK" | cut -d' ' -f1)"
else
    echo "SHA256: (sha256sum nicht vorhanden)"
fi
echo "MD5:    $(md5sum "$SPK" | cut -d' ' -f1)"
echo ""

echo "--- 3. Inhalt des SPK ---"
tar tf "$SPK" 2>/dev/null
echo ""

TMP=$(mktemp -d 2>/dev/null) || TMP=/tmp/diag.$$
mkdir -p "$TMP"
if tar xf "$SPK" -C "$TMP" conf/privilege 2>/dev/null; then
    # CR-Prüfung POSIX-sicher (kein $'\r' - das ist eine Bash-Erweiterung
    # und BusyBox-ash kann sie nicht).
    if tr -d '\r' < "$TMP/conf/privilege" | cmp -s - "$TMP/conf/privilege"; then
        echo ">> conf/privilege: nur LF-Zeilenenden"
    else
        echo ">> conf/privilege enthaelt CR-Zeichen (CRLF)"
    fi
    if grep -q '"groupname"' "$TMP/conf/privilege"; then
        echo ">> conf/privilege enthaelt 'groupname'"
    fi
    if grep -q '"prestart"' "$TMP/conf/privilege" || grep -q '"prestop"' "$TMP/conf/privilege"; then
        echo ">> conf/privilege enthaelt 'prestart'/'prestop'"
    fi
    if grep -q '"ctrl-script"' "$TMP/conf/privilege"; then
        echo ">> conf/privilege enthaelt 'ctrl-script'"
    else
        echo ">> conf/privilege enthaelt KEIN 'ctrl-script'"
    fi
    echo ""
    echo "--- 4. conf/privilege ---"
    cat "$TMP/conf/privilege"
    echo ""
    if grep -A3 '"defaults"' "$TMP/conf/privilege" 2>/dev/null \
       | grep -q '"run-as"[[:space:]]*:[[:space:]]*"package"'; then
        echo ">> defaults.run-as = package  (korrekt)"
    else
        echo ">> WARNUNG: defaults.run-as ist nicht 'package'!"
    fi
else
    echo "--- 4. conf/privilege ---"
    echo ">> conf/privilege FEHLT - das Paket gilt als Root-Paket."
fi
rm -rf "$TMP"
echo ""

echo "--- 5. INFO (Auszug) ---"
show_info
echo ""

echo "--- 6. Bestehende Installation ---"
if command -v synopkg >/dev/null 2>&1; then
    synopkg status ComfoBoxGateway 2>&1 || echo "(nicht installiert)"
else
    echo "(synopkg nicht gefunden)"
fi
echo ""

# ---------------------------------------------------------------------------
# 7. Log-Auszüge (immer - hier steht der genaue Grund)
# ---------------------------------------------------------------------------
show_logs

# ---------------------------------------------------------------------------
# 8. Installationsversuch
# ---------------------------------------------------------------------------
if [ "$MODE" = "--install" ]; then
    echo "--- 8. Installationsversuch ---"
    echo "Die Ausgabe ist JSON und enthaelt den echten Fehlercode."
    echo ""
    synopkg install "$SPK" 2>&1
    echo ""
    show_logs
elif [ "$MODE" = "--log" ]; then
    echo "--- 8. Installationsversuch (ausgelasst) ---"
    echo "Nur die Logs wurden gelesen - es wurde nichts installiert."
    echo ""
fi

# ---------------------------------------------------------------------------
# 9. Variantentest: welche privilege-Datei akzeptiert das DSM?
# ---------------------------------------------------------------------------
#
# Der Fehler 319 ("invalid package privilege content") sagt nur, dass der
# Inhalt nicht passt - nicht welcher Teil. Dieser Abschnitt probiert die
# möglichen Formen **durch**, indem er das Paket entpackt, die
# privilege-Datei ersetzt, neu packt und `synopkg install` aufruft.
#
# Der Test läuft in "prepare_install" - ein Fehler dort ändert **nichts**
# an der NAS. Eine erfolgreiche Variante installiert das Paket.
#
# Die Reihenfolge folgt der Wahrscheinlichkeit (Erkenntnis aus DSM 7.4):
#   1. ohne ctrl-script      - DSM 7 verweigert für Drittanbieter
#                              möglicherweise "run-as: root"
#   2. nur defaults          - minimal
#   3. ohne ctrl-script + groupname
#   4. wie ausgeliefert      - Kontrolle
if [ "$MODE" = "--test" ]; then
    echo "--- 9. Variantentest der privilege-Datei ---"
    echo "Jede Variante wird installiert; das DSM meldet den Fehlercode."
    echo "Der erste Treffer installiert das Paket - danach ist Schluss."
    echo "Ein Fehler in dieser Phase ändert nichts an der NAS."
    echo ""

    WORK=$(mktemp -d 2>/dev/null) || WORK=/tmp/vtest.$$
    mkdir -p "$WORK/pkg" "$WORK/orig"
    SPK_DIR=$(CDPATH= cd -- "$(dirname -- "$SPK")" && pwd) || exit 1
    SPK_NAME=$(basename -- "$SPK")

    # Das Original für die Kontrollvariante sichern
    ( cd "$WORK/orig" && tar xpf "$SPK_DIR/$SPK_NAME" conf/privilege ) 2>/dev/null
    ORIG_PRIV="$WORK/orig/conf/privilege"

    write_priv() {
        # $1 = Zieldatei, $2 = Variante
        case "$2" in
            1)  # ohne ctrl-script, mit username
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-ComfoBoxGateway"\n}\n' > "$1"
                ;;
            2)  # minimal: nur defaults
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t}\n}\n' > "$1"
                ;;
            3)  # ohne ctrl-script, mit username und groupname
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-ComfoBoxGateway",\n\t"groupname": "sc-ComfoBoxGateway"\n}\n' > "$1"
                ;;
            4)  # wie ausgeliefert (Kontrolle)
                cp "$ORIG_PRIV" "$1"
                ;;
        esac
    }

    for v in 1 2 3 4; do
        rm -rf "$WORK/pkg"
        mkdir -p "$WORK/pkg"
        ( cd "$WORK/pkg" && tar xpf "$SPK_DIR/$SPK_NAME" ) 2>/dev/null

        if [ ! -f "$WORK/pkg/conf/privilege" ]; then
            echo "  Variante $v: kann nicht entpackt werden - abgebrochen."
            break
        fi

        write_priv "$WORK/pkg/conf/privilege" "$v"

        NEU="$WORK/test-$v.spk"
        ( cd "$WORK/pkg" && tar cpf "$NEU" INFO conf scripts package.tgz PACKAGE_ICON.PNG PACKAGE_ICON_256.PNG ) 2>/dev/null

        OUT=$(synopkg install "$NEU" 2>&1)
        CODE=$(echo "$OUT" | sed -n 's/.*"code":\([0-9]*\).*/\1/p' | tail -n 1)
        DESC=$(echo "$OUT" | sed -n 's/.*"description":"\([^"]*\)".*/\1/p' | tail -n 1)

        case "$CODE" in
            ""|0)
                echo "  Variante $v: ERFOLG - das Paket wurde installiert!"
                echo "  -> Diese Form der privilege-Datei funktioniert:"
                echo ""
                cat "$WORK/pkg/conf/privilege"
                echo ""
                synopkg status ComfoBoxGateway 2>&1 || true
                echo ""
                echo "  Danach bitte prüfen, ob die Installation vollständig war:"
                echo "    sudo tail -30 /var/log/packages/ComfoBoxGateway.log"
                rm -rf "$WORK"
                exit 0
                ;;
            319)
                echo "  Variante $v: 319 invalid package privilege content"
                ;;
            *)
                echo "  Variante $v: Fehlercode ${CODE:-?} ${DESC}"
                ;;
        esac
    done

    echo ""
    echo "  Keine Variante hat funktioniert. Dann ist es nicht die"
    echo "  privilege-Datei - bitte die Log-Auszüge oben auswerten."
    rm -rf "$WORK"
    echo ""
fi

echo "=============================================================="
echo " Fertig."
echo "=============================================================="
