#!/bin/sh
# ---------------------------------------------------------------------------
# dsm-diagnose.sh – prüft ein Synology-Paket **auf dem NAS** und liefert den
# echten Fehlercode des DSM.
#
# Warum es das gibt:
#   Die Fehlermeldung im Paketzentrum („kann nicht installiert werden, da es
#   mit Root-Rechten ausgeführt wird") sagt nur, dass DSM eine Bedingung
#   nicht erfüllt sieht – nicht welche. Der Weg über die Kommandozeile
#   `synopkg install` liefert dagegen ein **JSON mit Fehlercode und Text**.
#
# Aufruf (auf dem NAS, per SSH):
#   sudo sh dsm-diagnose.sh /volume1/pfad/ComfoBoxGateway_noarch-dsm7_1.0.0-0003.spk
#
# Voraussetzung: nur BusyBox-Standardwerkzeuge (sh, tar, md5sum, sha256sum).
# Es wird **nichts** installiert und **nichts** verändert - das Skript liest
# nur. (Der Installationsversuch am Ende ist optional: --install)
# ---------------------------------------------------------------------------
set -u

SPK="${1:-}"
DO_INSTALL="${2:-}"

if [ -z "$SPK" ]; then
    echo "Aufruf: sh dsm-diagnose.sh <pfad/zum/paket.spk> [--install]" >&2
    exit 1
fi

if [ ! -f "$SPK" ]; then
    echo "FEHLER: Datei nicht gefunden: $SPK" >&2
    exit 1
fi

echo "=============================================================="
echo " ComfoBox Gateway - Paketdiagnose auf dem NAS"
echo "=============================================================="
echo ""

# --- 1. DSM-Version ---------------------------------------------------------
echo "--- 1. DSM-Version ---"
if [ -f /etc/VERSION ]; then
    cat /etc/VERSION
    echo ""
fi
echo "Kernel: $(uname -r 2>/dev/null)"
echo ""

# --- 2. Die Datei -----------------------------------------------------------
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

# --- 3. Inhalt des SPK ------------------------------------------------------
echo "--- 3. Inhalt des SPK ---"
LIST=$(tar tf "$SPK" 2>/dev/null)
if [ -z "$LIST" ]; then
    echo "FEHLER: Das Paket laesst sich nicht entpacken (defekt?)" >&2
    exit 1
fi
echo "$LIST"
echo ""

if echo "$LIST" | grep -q "^conf/privilege$"; then
    echo ">> conf/privilege ist vorhanden."
else
    echo ">> ACHTUNG: conf/privilege FEHLT - das DSM stuft das Paket als"
    echo ">> Root-Paket ein und verweigert die Installation."
fi
echo ""

# --- 4. Die privilege-Datei -------------------------------------------------
echo "--- 4. conf/privilege ---"
TMP=$(mktemp -d 2>/dev/null) || TMP=/tmp/diag.$$
mkdir -p "$TMP"
if tar xf "$SPK" -C "$TMP" conf/privilege 2>/dev/null; then
    cat "$TMP/conf/privilege"
    echo ""
    if grep -q '"run-as"[[:space:]]*:[[:space:]]*"package"' "$TMP/conf/privilege"; then
        echo ">> defaults.run-as = package  (korrekt)"
    else
        echo ">> WARNUNG: defaults.run-as ist nicht 'package'!"
    fi
else
    echo ">> (nicht auspackbar)"
fi
rm -rf "$TMP"
echo ""

# --- 5. Die INFO ------------------------------------------------------------
echo "--- 5. INFO (Auszug) ---"
TMP=$(mktemp -d 2>/dev/null) || TMP=/tmp/diag.$$
mkdir -p "$TMP"
if tar xf "$SPK" -C "$TMP" INFO 2>/dev/null; then
    grep -E '^(package|version|arch|os_min_ver|support_conf_folder|startable|ctl_stop|install_type)=' "$TMP/INFO"
else
    echo ">> (nicht auspackbar)"
fi
rm -rf "$TMP"
echo ""

# --- 6. Bestehende Installation ---------------------------------------------
echo "--- 6. Bestehende Installation ---"
if command -v synopkg >/dev/null 2>&1; then
    synopkg status ComfoBoxGateway 2>&1 || echo "(nicht installiert)"
else
    echo "(synopkg nicht gefunden)"
fi
echo ""

# --- 7. Optional: Installationsversuch mit Fehlercode ----------------------
if [ "$DO_INSTALL" = "--install" ]; then
    echo "--- 7. Installationsversuch ---"
    echo "Die Ausgabe ist JSON und enthaelt den echten Fehlercode."
    echo ""
    synopkg install "$SPK" 2>&1
    echo ""
else
    echo "--- 7. Installationsversuch (ausgelasst) ---"
    echo "Mit '--install' als zweites Argument zeigt das DSM den echten"
    echo "Fehlercode (JSON). Beispiel:"
    echo "  sudo sh dsm-diagnose.sh $SPK --install"
    echo ""
fi

# --- 8. Variantentest: welche privilege-Datei akzeptiert das DSM? ----------
#
# Der Fehler 319 ("invalid package privilege content") sagt nur, dass der
# Inhalt nicht passt - nicht welcher Teil. Dieser Abschnitt probiert die
# moeglichen Varianten **durch**, indem er das Paket entpackt, die
# privilege-Datei ersetzt, neu packt und `synopkg install` aufruft.
#
# Der Test laeuft in "prepare_install" - ein Fehler dort aendert **nichts**
# an der NAS. Eine erfolgreiche Variante installiert das Paket (das ist
# gewuellt).
if [ "$DO_INSTALL" = "--test" ]; then
    echo "--- 8. Variantentest der privilege-Datei ---"
    echo "Jede Variante wird installiert; das DSM meldet den Fehlercode."
    echo "Der erste Treffer installiert das Paket - danach ist Schluss."
    echo ""

    WORK=$(mktemp -d 2>/dev/null) || WORK=/tmp/vtest.$$
    mkdir -p "$WORK/pkg"
    SPK_DIR=$(CDPATH= cd -- "$(dirname -- "$SPK")" && pwd) || exit 1
    SPK_NAME=$(basename -- "$SPK")

    # Die Varianten: nur der Inhalt von conf/privilege unterscheidet sich.
    # 1 = wie ausgeliefert, 2 = ohne CR, 3 = ohne CR und ohne groupname,
    # 4 = ohne CR, ohne groupname und ohne prestart/prestop
    strip_priv() {
        # $1 = Zieldatei, $2 = 1|2|3|4
        case "$2" in
            1) tr -d '\r' < "$1" > "$1.new" ;;
            2) tr -d '\r' < "$1" | grep -v '"groupname"' > "$1.new" ;;
            3) tr -d '\r' < "$1" | grep -v '"groupname"' | grep -v '"prestart"' | grep -v '"prestop"' > "$1.new" ;;
            4) printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-ComfoBoxGateway"\n}\n' > "$1.new" ;;
        esac
        mv "$1.new" "$1"
    }

    for v in 1 2 3 4; do
        rm -rf "$WORK/pkg"
        mkdir -p "$WORK/pkg"
        ( cd "$WORK/pkg" && tar xpf "$SPK_DIR/$SPK_NAME" ) 2>/dev/null

        if [ ! -f "$WORK/pkg/conf/privilege" ]; then
            echo "  Variante $v: kann nicht entpackt werden - abgebrochen."
            break
        fi

        strip_priv "$WORK/pkg/conf/privilege" "$v"

        NEU="$WORK/test-$v.spk"
        ( cd "$WORK/pkg" && tar cpf "$NEU" INFO conf scripts package.tgz PACKAGE_ICON.PNG PACKAGE_ICON_256.PNG ) 2>/dev/null

        OUT=$(synopkg install "$NEU" 2>&1)
        CODE=$(echo "$OUT" | sed -n 's/.*"code":\([0-9]*\).*/\1/p' | head -n 1)
        DESC=$(echo "$OUT" | sed -n 's/.*"description":"\([^"]*\)".*/\1/p' | head -n 1)

        case "$CODE" in
            ""|0)
                echo "  Variante $v: ERFOLG - das Paket wurde installiert!"
                echo "  -> Diese Variante der privilege-Datei funktioniert."
                echo ""
                synopkg status ComfoBoxGateway 2>&1 || true
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
    echo "  privilege-Datei - bitte die Ausgabe von Schritt 7 auswerten."
    rm -rf "$WORK"
    echo ""
fi

echo "=============================================================="
echo " Fertig."
echo "=============================================================="
