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
#   sudo sh dsm-diagnose.sh /pfad/zum/paket.spk [--install|--worker|--test|--log]
#
#   --install  Installiert das Paket und zeigt die JSON-Antwort (Fehlercode)
#   --worker   **Emuliert den DSM-Worker "docker-project"** und zeigt die
#              volle Fehlermeldung von `docker compose up`. Das ist der
#              Schritt, an dem die Installation scheitert ("fail compose up").
#              Das Paket wird dabei NICHT installiert - nur die Compose-Datei
#              wird ausprobiert und danach wieder aufgeräumt.
#   --test     Probiert mehrere Varianten der privilege-Datei durch und
#              installiert die erste, die das DSM akzeptiert. Ein Fehler in
#              prepare_install ändert nichts an der NAS.
#   --log      Zeigt nur die Log-Auszüge des DSM (nichts wird installiert)
#
# Voraussetzung: nur BusyBox-Standardwerkzeuge (sh, tar, md5sum, sha256sum).
# Ohne --install/--test/--worker wird **nichts** verändert.
# ---------------------------------------------------------------------------
set -u

SPK="${1:-}"
MODE="${2:-}"

if [ -z "$SPK" ]; then
    echo "Aufruf: sh dsm-diagnose.sh <pfad/zum/paket.spk> [--install|--worker|--test|--log]" >&2
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

# Die Struktur von package.tgz ist entscheidend: das DSM entpackt die Datei
# DIREKT in das Zielverzeichnis. Deshalb muessen die Dateien auf der
# obersten Ebene liegen (bin/, compose/, image) - nicht unter "target/".
echo "--- 3a. Struktur von package.tgz ---"
TGZ_DIR=$(mktemp -d 2>/dev/null) || TGZ_DIR=/tmp/diag-tgz.$$
mkdir -p "$TGZ_DIR"
if tar xf "$SPK" -C "$TGZ_DIR" package.tgz 2>/dev/null \
   && tar tzf "$TGZ_DIR/package.tgz" 2>/dev/null; then
    if tar tzf "$TGZ_DIR/package.tgz" 2>/dev/null | grep -q '^target/'; then
        echo ">> WARNUNG: package.tgz enthaelt eine Ebene 'target/'!"
        echo "   Die Dateien landen dann eine Stufe zu tief (<ziel>/target/...)"
        echo "   und der DSM-Worker 'docker-project' findet die Compose-Datei nicht."
    else
        echo ">> OK: package.tgz hat die Dateien auf der obersten Ebene."
    fi
else
    echo ">> package.tgz nicht lesbar"
fi
rm -rf "$TGZ_DIR"
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

# **Ein fehlgeschlagenes Update hinterlaesst das Paket im Status "broken"**
# (150). DSM 7.4 versucht dann beim naechsten Mal ein UPDATE statt einer
# Neuinstallation - und scheitert an "package does not have preupgrade /
# postupgrade script" (Fehler 261), obwohl das Paket in Ordnung ist.
# Deshalb wird vor dem Installationsversuch ein "broken"-Paket entfernt.
remove_broken_install() {
    command -v synopkg >/dev/null 2>&1 || return 0
    STATUS=$(synopkg status ComfoBoxGateway 2>/dev/null \
        | sed -n 's/.*"status":"\([a-z_]*\)".*/\1/p' | head -n 1)
    [ "$STATUS" = "broken" ] || return 0

    echo ">> Das Paket steht im Status 'broken' (eine Installation ist zuvor"
    echo "   fehlgeschlagen). DSM 7.4 wuerde jetzt ein UPDATE versuchen und"
    echo "   daran scheitern. Das alte Paket wird deshalb entfernt."
    synopkg uninstall ComfoBoxGateway >/dev/null 2>&1 || true
    sleep 2
    synopkg status ComfoBoxGateway 2>&1 || true
    echo ""
}

# Die Compose-Datei mit Docker pruefen - so sieht man einen Syntaxfehler,
# bevor das DSM den Worker aufruft.
validate_compose() {
    if ! command -v docker >/dev/null 2>&1; then
        echo "(docker nicht gefunden - Compose-Datei wird nicht geprueft)"
        return 0
    fi
    WORK2=$(mktemp -d 2>/dev/null) || WORK2=/tmp/diag-cmp.$$
    mkdir -p "$WORK2"
    if tar xf "$SPK" -C "$WORK2" package.tgz 2>/dev/null \
       && tar xzf "$WORK2/package.tgz" -C "$WORK2" compose/compose.yaml 2>/dev/null; then
        echo ">> Compose-Datei:"
        sed 's/^/   /' "$WORK2/compose/compose.yaml"
        echo ""
        echo ">> docker compose config (Syntaxpruefung):"
        if docker compose -f "$WORK2/compose/compose.yaml" config >/dev/null 2>&1; then
            echo "   OK - die Compose-Datei ist gueltig."
        else
            docker compose -f "$WORK2/compose/compose.yaml" config 2>&1 \
                | head -n 20 | sed 's/^/   /'
            echo "   WARNUNG: Die Compose-Datei hat einen Fehler!"
        fi
    fi
    rm -rf "$WORK2"
    echo ""
}

# ---------------------------------------------------------------------------
# Worker-Emulation
#
# Der DSM-Worker "docker-project" macht genau drei Dinge:
#   1. das Projekt anlegen (Verzeichnis + Netzwerk)
#   2. `docker compose up`   <-- HIER scheitert die Installation
#   3. bei Fehler: zurueckrollen ("rollback created projects")
#
# Das DSM meldet nur "fail compose up '<uuid>'" - die eigentliche
# Fehlermeldung von Docker wird **nicht** weitergegeben. Dieser Abschnitt
# macht exakt Schritt 2 nach und zeigt die volle Ausgabe.
#
# Das Paket wird dabei NICHT installiert. Am Ende wird das Projekt wieder
# entfernt (`compose down`), damit keine Reste bleiben.
# ---------------------------------------------------------------------------
worker_test() {
    echo "--- Worker-Emulation: docker compose up ---"
    echo ""

    if ! command -v docker >/dev/null 2>&1; then
        echo "FEHLER: docker ist nicht im PATH."
        echo "       Auf dem DSM liegt der Docker-Binary unter"
        echo "       /var/packages/ContainerManager/target/usr/bin/docker"
        return 1
    fi

    # Das Paket entpacken - genau wie das DSM es tut:
    #   SPK entpacken -> package.tgz -> Zielverzeichnis
    WORK3=$(mktemp -d 2>/dev/null) || WORK3=/tmp/diag-worker.$$
    mkdir -p "$WORK3/target"
    if ! tar xf "$SPK" -C "$WORK3" package.tgz 2>/dev/null; then
        echo "FEHLER: package.tgz nicht lesbar"
        rm -rf "$WORK3"
        return 1
    fi
    if ! tar xzf "$WORK3/package.tgz" -C "$WORK3/target" 2>/dev/null; then
        echo "FEHLER: package.tgz nicht entpackbar"
        rm -rf "$WORK3"
        return 1
    fi

    CF_DIR="$WORK3/target/compose"
    if [ ! -f "$CF_DIR/compose.yaml" ]; then
        echo "FEHLER: compose/compose.yaml fehlt in package.tgz"
        rm -rf "$WORK3"
        return 1
    fi

    # 1) Die Umgebung, in der der Worker laeuft
    echo ">> Docker-Versionen:"
    docker version --format '   Server: {{.Server.Version}}' 2>&1 | head -n 3 | sed 's/^/   /'
    docker compose version 2>&1 | head -n 3 | sed 's/^/   /'
    echo ""

    # 2) Das Image - isoliert pulling (zeigt Netzwerk-/Auth-Fehler)
    if [ -f "$WORK3/target/image" ]; then
        IMAGE_NAME=$(head -n 1 "$WORK3/target/image" 2>/dev/null)
    else
        IMAGE_NAME=""
    fi
    if [ -n "$IMAGE_NAME" ]; then
        echo ">> Image-Pull (isoliert): $IMAGE_NAME"
        docker pull "$IMAGE_NAME" 2>&1 | tail -n 15 | sed 's/^/   /'
        echo ""
    fi

    # 3) Die Ports pruefen - ein belegter Port laesst "compose up" scheitern
    if command -v netstat >/dev/null 2>&1; then
        echo ">> Belegte Ports (8081/8080):"
        netstat -an 2>/dev/null | grep -E ':(8081|8080) ' | sed 's/^/   /' || echo "   (keine)"
        echo ""
    fi

    # 4) DER entscheidende Schritt: compose up
    echo ">> docker compose up (der Schritt, an dem die Installation scheitert):"
    ( cd "$CF_DIR" && docker compose -p comfobox up -d 2>&1 ) | tail -n 40 | sed 's/^/   /'
    RC=${PIPESTATUS:-$?}
    echo ""
    echo "   (Exit-Code: $RC)"
    echo ""

    # 5) Was ist daraus geworden?
    echo ">> Container:"
    docker ps -a --filter "name=comfobox" 2>&1 | head -n 10 | sed 's/^/   /'
    echo ""
    echo ">> Container-Logs:"
    ( cd "$CF_DIR" && docker compose -p comfobox logs --tail 30 2>&1 ) | tail -n 30 | sed 's/^/   /'
    echo ""

    # 6) Aufraeumen - es sollen keine Reste auf der NAS bleiben
    echo ">> Aufraeumen:"
    ( cd "$CF_DIR" && docker compose -p comfobox down --remove-orphans 2>&1 ) | tail -n 5 | sed 's/^/   /'
    echo ""

    rm -rf "$WORK3"
}

# ---------------------------------------------------------------------------
# 7. Log-Auszüge (immer - hier steht der genaue Grund)
# ---------------------------------------------------------------------------
show_logs

# ---------------------------------------------------------------------------
# 8. Installationsversuch
# ---------------------------------------------------------------------------
if [ "$MODE" = "--install" ]; then
    echo "--- 8. Compose-Datei pruefen ---"
    validate_compose

    echo "--- 9. Installationsversuch ---"
    echo "Die Ausgabe ist JSON und enthaelt den echten Fehlercode."
    echo ""
    remove_broken_install
    synopkg install "$SPK" 2>&1
    echo ""
    show_logs
elif [ "$MODE" = "--worker" ]; then
    echo "--- 8. Worker-Emulation (docker compose up) ---"
    echo "Das Paket wird dabei NICHT installiert."
    echo ""
    validate_compose
    worker_test
    echo "--- 9. Log-Auszüge des DSM ---"
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
# Die Varianten folgen der Ground Truth aus dem echten, funktionierenden
# Plex-DSM7-Paket auf dieser NAS:
#
#   conf/privilege:  { "defaults": {"run-as": "package"},
#                      "username": "PlexMediaServer",
#                      "groupname": "PlexMediaServer" }
#   conf/resource:   { "video-driver": {}, "port-config": "..." }
#   → **kein ctrl-script**
#
# Unser Paket hat dagegen ein ctrl-script (mit run-as: root) und **keine**
# conf/resource. Beides sind die Kandidaten für den Fehler 319.
#
# Reihenfolge: erst die kleinste Änderung, die installierbar macht – eine
# funktionierende Variante **installiert** das Paket und beendet den Test.
if [ "$MODE" = "--test" ]; then
    echo "--- 9. Variantentest (privilege-Datei x conf/resource) ---"
    echo "Jede Variante wird installiert; das DSM meldet den Fehlercode."
    echo "Der erste Treffer installiert das Paket - danach ist Schluss."
    echo "Ein Fehler in dieser Phase aendert nichts an der NAS."
    echo ""

    WORK=$(mktemp -d 2>/dev/null) || WORK=/tmp/vtest.$$
    mkdir -p "$WORK/pkg" "$WORK/orig"
    SPK_DIR=$(CDPATH= cd -- "$(dirname -- "$SPK")" && pwd) || exit 1
    SPK_NAME=$(basename -- "$SPK")

    # Das Original für die Kontrollvariante sichern
    ( cd "$WORK/orig" && tar xpf "$SPK_DIR/$SPK_NAME" conf/privilege ) 2>/dev/null
    ORIG_PRIV="$WORK/orig/conf/privilege"

    # $1 = Zieldatei, $2 = Variante
    write_priv() {
        case "$2" in
            1|8)
                # wie ausgeliefert (ctrl-script mit root)
                cp "$ORIG_PRIV" "$1"
                ;;
            2|6)
                # minimal: nur defaults
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t}\n}\n' > "$1"
                ;;
            3|5)
                # ohne ctrl-script, mit username
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-ComfoBoxGateway"\n}\n' > "$1"
                ;;
            4)
                # ohne ctrl-script, mit username und groupname (Plex-Stil)
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-ComfoBoxGateway",\n\t"groupname": "sc-ComfoBoxGateway"\n}\n' > "$1"
                ;;
            7)
                # ohne ctrl-script, username klein geschrieben
                printf '{\n\t"defaults":{\n\t\t"run-as": "package"\n\t},\n\t"username": "sc-comfoboxgateway"\n}\n' > "$1"
                ;;
        esac
    }

    # $1 = Nummer der Variante
    variant_label() {
        case "$1" in
            1) echo "wie ausgeliefert (ctrl-script) + conf/resource" ;;
            2) echo "nur defaults + conf/resource" ;;
            3) echo "ohne ctrl-script (username) + conf/resource" ;;
            4) echo "ohne ctrl-script (username+groupname) + conf/resource" ;;
            5) echo "ohne ctrl-script (username), OHNE resource" ;;
            6) echo "nur defaults, OHNE resource" ;;
            7) echo "ohne ctrl-script (username klein) + conf/resource" ;;
            8) echo "wie ausgeliefert, OHNE resource (Kontrolle)" ;;
        esac
    }

    # $1 = Nummer, $2 = ja|nein (conf/resource dazufügen)
    variant_resource() {
        if [ "$2" = "ja" ]; then
            printf '{\n}\n' > "$WORK/pkg/conf/resource"
        else
            rm -f "$WORK/pkg/conf/resource"
        fi
    }

    # Die Varianten: Nummer | privilege-Form | conf/resource
    # 1 = ausgeliefert + resource      (kleinste Änderung)
    # 2 = nur defaults + resource
    # 3 = ohne ctrl-script + resource
    # 4 = ohne ctrl-script, mit groupname + resource
    # 5 = ohne ctrl-script, ohne resource
    # 6 = nur defaults, ohne resource
    # 7 = ohne ctrl-script, username klein + resource
    # 8 = ausgeliefert, ohne resource  (Kontrolle - muss 319 geben)
    for spec in "1:ja" "2:ja" "3:ja" "4:ja" "5:nein" "6:nein" "7:ja" "8:nein"; do
        v=${spec%%:*}
        res=${spec##*:}

        rm -rf "$WORK/pkg"
        mkdir -p "$WORK/pkg"
        ( cd "$WORK/pkg" && tar xpf "$SPK_DIR/$SPK_NAME" ) 2>/dev/null

        if [ ! -f "$WORK/pkg/conf/privilege" ]; then
            echo "  Variante $v: kann nicht entpackt werden - abgebrochen."
            break
        fi

        write_priv "$WORK/pkg/conf/privilege" "$v"
        variant_resource "$v" "$res"

        NEU="$WORK/test-$v.spk"
        ( cd "$WORK/pkg" && tar cpf "$NEU" INFO conf scripts package.tgz PACKAGE_ICON.PNG PACKAGE_ICON_256.PNG ) 2>/dev/null

        OUT=$(synopkg install "$NEU" 2>&1)
        CODE=$(echo "$OUT" | sed -n 's/.*"code":\([0-9]*\).*/\1/p' | tail -n 1)
        DESC=$(echo "$OUT" | sed -n 's/.*"description":"\([^"]*\)".*/\1/p' | tail -n 1)

        case "$CODE" in
            ""|0)
                echo "  Variante $v: ERFOLG - das Paket wurde installiert!"
                echo "  -> Form: $(variant_label "$v")"
                echo ""
                echo "  conf/privilege:"
                cat "$WORK/pkg/conf/privilege"
                echo ""
                if [ -f "$WORK/pkg/conf/resource" ]; then
                    echo "  conf/resource:"
                    cat "$WORK/pkg/conf/resource"
                    echo ""
                fi
                synopkg status ComfoBoxGateway 2>&1 || true
                echo ""
                echo "  Danach bitte prüfen, ob die Installation vollständig war:"
                echo "    sudo tail -30 /var/log/packages/ComfoBoxGateway.log"
                echo ""
                echo "  WICHTIG: Diese Installation beweist nur, welche Form das DSM"
                echo "  akzeptiert. Die Lifecycle-Skripte laufen dabei ohne root -"
                echo "  schlägt die Installation danach fehl, bitte deinstallieren:"
                echo "    sudo synopkg uninstall ComfoBoxGateway"
                echo "  und die Meldung hier im Protokoll festhalten."
                rm -rf "$WORK"
                exit 0
                ;;
            319)
                echo "  Variante $v: 319  ($(variant_label "$v"))"
                ;;
            *)
                echo "  Variante $v: Fehlercode ${CODE:-?} ${DESC}  ($(variant_label "$v"))"
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
