#!/bin/sh
# ---------------------------------------------------------------------------
# serial-port-check.sh - sucht den USB-RS485-Adapter, prueft ihn und misst,
# ob die Verbindung stabil bleibt.
#
# Aufruf (auf dem Raspberry Pi oder jedem Linux-Host, per SSH):
#     sudo sh serial-port-check.sh
#     sudo sh serial-port-check.sh --port /dev/ttyUSB0 --cycles 25 --read 5
#     sh serial-port-check.sh --list            # nur suchen (ohne root)
#
# Optionen:
#     --list             nur auflisten, nichts oeffnen
#     --port DEV         nur diesen Port pruefen (z. B. /dev/ttyUSB0)
#     --baud N           Baudrate des Tests (Vorgabe 38400 = ComfoBox)
#     --cycles N         Oeffnen/Schliessen-Runden (Vorgabe 10, 0 = aus)
#     --watch N          N Sekunden auf Verschwinden des Ports achten
#     --read N           N Sekunden Busdaten lesen (nur lesen!)
#     --container NAME   Container fuer die Sichtpruefung
#                        (Vorgabe comfobox-gateway)
#     --no-container     Containerpruefung auslassen (kein Docker)
#     --free-port        Container fuer den Test stoppen und danach wieder
#                        starten (nur so laesst sich ein belegter Port testen)
#     --help             diese Hilfe
#
# Was das Skript NICHT tut:
#   * Es schreibt KEINE Daten auf den RS485-Bus (kein Probe-Frame, kein Echo).
#     Es oeffnet, setzt die Leitungsparameter, liest - mehr nicht.
#   * Es veraendert keine Datei und keine Konfiguration. Die Termios-Werte
#     gelten nur, solange der Port offen ist.
#   * Es laesst den Container laufen - ausser bei --free-port; dann wird er
#     am Ende wieder gestartet (auch bei Abbruch, per trap).
#
# Warum es das gibt:
#   Auf einer DS923+ steckte der Adapter hinter ZWEI Hubs
#   (1-3 VIA-Labs -> 1-3.2 NEC-Hub -> 1-3.2.1 CH340). Die Enumeration
#   scheiterte wiederholt mit "error -71" (EPROTO) und "error -32" (EPIPE).
#   Selbst mit Treiber waere die Verbindung instabil gewesen.
#   Genau diese Kette misst das Skript: Chip am Bus, Hub-Tiefe, Treiber,
#   Rechte, Oeffnen, 38400 8N1, N Runden, Busdaten und die dmesg-Differenz.
#   Am Ende steht EINE Ursache mit dem naechsten Schritt.
#
#   Wichtig fuer die Bewertung: Der Raspberry Pi hat einen EINGEBAUTEN Hub
#   (LAN9514/7515, an dem auch Ethernet haengt). Der Adapter erscheint dort
#   als "1-1.2" - also "1 Zwischen-Hub", obwohl er direkt an der Buchse
#   steckt. Solche internen Hubs zaehlen NICHT als Fehlerquelle (gemessen
#   2026-11-14 an einem Pi 3 Model B). Nur fremde Hubs zaehlen.
# ---------------------------------------------------------------------------
set -u

BAUD=38400
CYCLES=10
WATCH=0
READ_SECONDS=0
PORT=""
LIST_ONLY=0
CONTAINER="comfobox-gateway"
CHECK_CONTAINER=1
FREE_PORT=0

CONTAINER_STOPPED=0
DMESG_OK=0
DMESG_BASE=0

CHIP_SEEN=0
CHIP_KINDS=""
MAX_DEPTH=0
MAX_EXT_HUBS=0
USB_TOTAL=0
DRIVER_STATE="unknown"

HOST_COUNT=0
DEVS=""
PERM_ISSUE=0
HAS_BY_ID=0

CTR_RUNNING=0
CTR_CHECKED=0
CTR_COUNT=0
CGROUP_BLOCKED=0
CTR_OPEN_FAIL=0
CGROUP_RULES=""
GROUP_ADD=""
APP_API=0
APP_COUNT=0

OPEN_OK=0
OPEN_BUSY=0
OPEN_FAIL=0
STTY_FAIL=0
CYCLE_FAILS=0
VANISH_EVENTS=0
READ_DONE=0
READ_BYTES=0
READ_HEAD=""

section()
{
    printf '\n==============================================================\n'
    printf ' %s\n' "$1"
    printf '==============================================================\n'
}

have()
{
    command -v "$1" >/dev/null 2>&1
}

die()
{
    printf '\nFEHLER: %s\n' "$*" >&2
    exit 2
}

usage()
{
    cat <<'USAGE_EOF'
serial-port-check.sh - USB-RS485-Adapter finden und pruefen

  sudo sh serial-port-check.sh                 # vollstaendiger Lauf
  sudo sh serial-port-check.sh --read 5        # 5 s Busdaten mitlesen
  sudo sh serial-port-check.sh --free-port     # Container kurz stoppen
  sh serial-port-check.sh --list               # nur auflisten

Optionen:
  --list             nur auflisten, nichts oeffnen
  --port DEV         nur diesen Port pruefen (z. B. /dev/ttyUSB0)
  --baud N           Baudrate des Tests (Vorgabe 38400)
  --cycles N         Oeffnen/Schliessen-Runden (Vorgabe 10, 0 = aus)
  --watch N          N Sekunden auf Verschwinden des Ports achten
  --read N           N Sekunden Busdaten lesen (nur lesen)
  --container NAME   Container fuer die Sichtpruefung (comfobox-gateway)
  --no-container     Containerpruefung auslassen
  --free-port        Container fuer den Test stoppen, danach wieder starten
  --help             diese Hilfe

Das Skript schreibt nicht auf den Bus und aendert keine Konfiguration.
USAGE_EOF
}

cleanup()
{
    if [ "$CONTAINER_STOPPED" = "1" ]; then
        printf '\n   Container "%s" wird wieder gestartet ...\n' "$CONTAINER"
        if have docker && docker start "$CONTAINER" >/dev/null 2>&1; then
            printf '   Container laeuft wieder.\n'
        else
            printf '   WARNUNG: bitte manuell starten: docker start %s\n' "$CONTAINER" >&2
        fi
        CONTAINER_STOPPED=0
    fi
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM HUP

# ---------------------------------------------------------------------------
# Optionen
# ---------------------------------------------------------------------------
while [ $# -gt 0 ]; do
    case "$1" in
        --list)         LIST_ONLY=1; shift ;;
        --port)
            [ -n "${2:-}" ] || die "--port braucht einen Pfad"
            PORT="$2"; shift 2
            ;;
        --baud)
            [ -n "${2:-}" ] || die "--baud braucht eine Zahl"
            case "$2" in *[!0-9]*) die "--baud erwartet eine Zahl" ;; esac
            BAUD="$2"; shift 2
            ;;
        --cycles)
            [ -n "${2:-}" ] || die "--cycles braucht eine Zahl"
            case "$2" in *[!0-9]*) die "--cycles erwartet eine Zahl" ;; esac
            CYCLES="$2"; shift 2
            ;;
        --watch)
            [ -n "${2:-}" ] || die "--watch braucht eine Zahl"
            case "$2" in *[!0-9]*) die "--watch erwartet eine Zahl" ;; esac
            WATCH="$2"; shift 2
            ;;
        --read)
            [ -n "${2:-}" ] || die "--read braucht eine Zahl"
            case "$2" in *[!0-9]*) die "--read erwartet eine Zahl" ;; esac
            READ_SECONDS="$2"; shift 2
            ;;
        --container)
            [ -n "${2:-}" ] || die "--container braucht einen Namen"
            CONTAINER="$2"; shift 2
            ;;
        --no-container) CHECK_CONTAINER=0; shift ;;
        --free-port)    FREE_PORT=1; shift ;;
        --help|-h)      usage; exit 0 ;;
        *)              die "Unbekannte Option '$1' (--help zeigt die Hilfe)" ;;
    esac
done

# Baseline fuer die dmesg-Differenz: nur Meldungen zaehlen, die WAEHREND
# dieses Laufs entstehen. dmesg darf dafuer nicht geleert werden.
if dmesg >/dev/null 2>&1; then
    DMESG_OK=1
    DMESG_BASE=$(dmesg 2>/dev/null | wc -l | tr -d ' ')
fi

# ---------------------------------------------------------------------------
# 1. System
# ---------------------------------------------------------------------------
section "1. System"
printf '   Kernel:   %s\n' "$(uname -r 2>/dev/null)"
MODEL=""
if [ -r /proc/device-tree/model ]; then
    MODEL=$(tr -d '\000' < /proc/device-tree/model 2>/dev/null)
fi
[ -n "$MODEL" ] || MODEL="(nicht ermittelbar)"
printf '   Modell:   %s\n' "$MODEL"
OSNAME=$(grep -s '^PRETTY_NAME=' /etc/os-release 2>/dev/null | cut -d= -f2- | tr -d '"')
[ -n "$OSNAME" ] || OSNAME=$(uname -s 2>/dev/null)
printf '   System:   %s\n' "$OSNAME"
printf '   Arch:     %s\n' "$(uname -m 2>/dev/null)"
printf '   Aufruf:   %s (uid %s)\n' "$(id -un 2>/dev/null)" "$(id -u 2>/dev/null)"
if [ "$(id -u)" != "0" ]; then
    printf '\n   HINWEIS: Ohne root sind Rechte, dmesg und Modulangaben\n'
    printf '            unvollstaendig. Bitte: sudo sh %s\n' "$0"
fi

# ---------------------------------------------------------------------------
# 2. USB-Geraete am Bus
#
# Die entscheidende Frage zuerst: Ist der Adapter ueberhaupt am USB-Bus?
# Das sagt der Kernel ueber sysfs - unabhaengig davon, ob ein Treiber ihn
# bedient. Ist hier nichts zu sehen, ist es die Hardware oder das Kabel.
# ---------------------------------------------------------------------------

# Klassifiziert einen Hub anhand seiner Kennung.
#
# **Das ist der Unterschied zwischen "direkt angeschlossen" und "hinter zwei
# Hubs" - und auf einem Raspberry Pi der Unterschied zwischen einem
# Fehlalarm und einem echten Befund.** Der Pi 3 hat einen fest eingebauten
# Hub (LAN9514/LAN7515), an dem auch das Ethernet haengt. Er sitzt immer an
# 1-1, der Adapter erscheint deshalb als 1-1.2 - "1 Zwischen-Hub". Das ist
# aber kein externer Hub und keine Fehlerquelle.
# Auf der DS923+ war 1-3.2.1 dagegen ein NEC-Hub HINTER einem VIA-Hub -
# beide mit fremden Kennungen, also zwei echte Zwischen-Hubs.
hub_label()
{
    case "$2" in
        0424:9512) printf 'LAN9512 - im Pi eingebaut' ;;
        0424:9514) printf 'LAN9514/7515 - im Pi eingebaut' ;;
        0424:7800) printf 'LAN7800 - im Pi eingebaut' ;;
        1d6b:*)    printf 'Root-Hub des Controllers' ;;
        *)         printf '' ;;
    esac
}

# Gibt die Kette vom Adapter bis zum Root-Hub aus und setzt CHAIN_DEPTH
# (alle Zwischen-Hubs) und CHAIN_EXT_HUBS (nur die externen).
usb_chain()
{
    node=$(basename "$1")
    bus=${node%%-*}
    case "$node" in *-*) path=${node#*-} ;; *) path="" ;; esac
    prod=$(cat "$1/product" 2>/dev/null)
    out="$node $(cat "$1/idVendor" 2>/dev/null):$(cat "$1/idProduct" 2>/dev/null)"
    [ -n "$prod" ] && out="$out $prod"

    CHAIN_DEPTH=0
    CHAIN_EXT_HUBS=0
    while [ -n "$path" ]; do
        # Ein Punkt trennt die Ebenen: "1.2" haengt an Port 2 des Geraets an
        # Port 1. Ohne Punkt haengt das Geraet direkt am Root-Hub - dann gibt
        # es keinen Zwischen-Hub mehr zu zaehlen.
        case "$path" in
            *.*) parent=${path%.*} ;;
            *)   break ;;
        esac
        path=$parent
        p="/sys/bus/usb/devices/$bus-$path"
        v=$(cat "$p/idVendor" 2>/dev/null)
        pid=$(cat "$p/idProduct" 2>/dev/null)
        CHAIN_DEPTH=$((CHAIN_DEPTH + 1))
        lbl=$(hub_label "$bus-$path" "$v:$pid")
        if [ -z "$lbl" ]; then
            CHAIN_EXT_HUBS=$((CHAIN_EXT_HUBS + 1))
            lbl="EXTERNER Hub"
        fi
        out="$out -> $path $v:$pid ($lbl)"
    done

    printf '                  USB-Pfad: %s (%s Zwischen-Hub(s), davon %s extern)\n' \
        "${node#*-}" "$CHAIN_DEPTH" "$CHAIN_EXT_HUBS"
    printf '                  USB-Kette: %s -> Root-Hub (Bus %s)\n' "$out" "$bus"
}

section "2. USB-Geraete am Bus (sysfs)"
if [ -d /sys/bus/usb/devices ]; then
    for d in /sys/bus/usb/devices/*; do
        [ -f "$d/idVendor" ] || continue
        VID=$(cat "$d/idVendor" 2>/dev/null)
        PID=$(cat "$d/idProduct" 2>/dev/null)
        PROD=$(cat "$d/product" 2>/dev/null)
        MAN=$(cat "$d/manufacturer" 2>/dev/null)
        USB_TOTAL=$((USB_TOTAL + 1))

        KIND=""
        case "$VID:$PID" in
            1a86:7523)            KIND="CH340" ;;
            1a86:5523|1a86:55d4)  KIND="CH341" ;;
            1a86:*)               KIND="WCH (unbekannt)" ;;
            0403:*)               KIND="FTDI" ;;
            10c4:*)               KIND="CP210x (Silicon Labs)" ;;
            067b:2303)            KIND="PL2303" ;;
            2341:*)               KIND="Arduino" ;;
        esac

        printf '   %-14s %s:%s  %s %s\n' "$(basename "$d")" "$VID" "$PID" "$MAN" "$PROD"

        case "$KIND" in
            CH340|CH341|FTDI|CP210x*|PL2303)
                CHIP_SEEN=1
                CHIP_KINDS="$CHIP_KINDS $KIND"
                printf '                  >>> Seriell-Adapter: %s\n' "$KIND"

                # Die Zahl der Zwischen-Hubs steckt in der Port-Kette:
                # "1-3.2.1" = Bus 1, Port 3, dann 2, dann 1 -> zwei Hubs.
                # Auf einem Pi ist der erste "Hub" (1-1) der eingebaute
                # LAN95xx - usb_chain trennt ihn von echten externen Hubs.
                usb_chain "$d"
                [ "$CHAIN_DEPTH" -gt "$MAX_DEPTH" ] 2>/dev/null && MAX_DEPTH=$CHAIN_DEPTH
                [ "$CHAIN_EXT_HUBS" -gt "$MAX_EXT_HUBS" ] 2>/dev/null && MAX_EXT_HUBS=$CHAIN_EXT_HUBS

                # Nur Interface-Verzeichnisse tragen einen Doppelpunkt
                # ("1-3.2.1:1.0"). Ein ungebundenes Interface ist genau der
                # Fall "Adapter da, Treiber fehlt".
                for i in "$d"/*:*.*/; do
                    [ -e "$i" ] || continue
                    INO=$(basename "$i")
                    if [ -e "$i/driver" ]; then
                        LNK=$(readlink "$i/driver" 2>/dev/null)
                        [ -n "$LNK" ] || LNK=$(readlink -f "$i/driver" 2>/dev/null)
                        DRV=$(basename "$LNK" 2>/dev/null)
                        [ "$DRV" = "driver" ] && DRV="(Name unbekannt)"
                        printf '                  Interface %s -> Treiber: %s\n' "$INO" "$DRV"
                    else
                        printf '                  Interface %s -> KEIN Treiber gebunden\n' "$INO"
                    fi
                    for t in "$i"tty*; do
                        [ -e "$t" ] || continue
                        printf '                  tty: /dev/%s\n' "$(basename "$t")"
                    done
                done
                ;;
            "WCH (unbekannt)")
                printf '                  >>> WCH-Chip mit unbekannter Kennung (%s:%s)\n' "$VID" "$PID"
                ;;
        esac
    done
    [ "$USB_TOTAL" = "0" ] && printf '   (keine USB-Geraete sichtbar)\n'
else
    printf '   /sys/bus/usb/devices fehlt - sysfs nicht verfuegbar\n'
fi

# ---------------------------------------------------------------------------
# 3. Kernel-Treiber
# ---------------------------------------------------------------------------
section "3. Kernel-Treiber"
if [ -d /sys/bus/usb/drivers/ch341 ]; then
    DRIVER_STATE="registered"
    printf '   /sys/bus/usb/drivers/ch341: vorhanden (Treiber ist registriert)\n'
else
    printf '   /sys/bus/usb/drivers/ch341: NICHT vorhanden\n'
fi

if have lsmod; then
    for m in ch341 usbserial ftdi_sio cp210x pl2303; do
        if lsmod 2>/dev/null | awk 'NR>1 {print $1}' | grep -qx "$m"; then
            printf '      %-10s GELADEN\n' "$m"
        else
            printf '      %-10s nicht geladen\n' "$m"
        fi
    done
else
    printf '   (lsmod nicht verfuegbar)\n'
fi

KREL=$(uname -r 2>/dev/null)
if have modinfo; then
    if modinfo ch341 >/dev/null 2>&1; then
        printf '   modinfo ch341: Modul ist vorhanden\n'
        [ "$DRIVER_STATE" = "registered" ] || DRIVER_STATE="available"
    else
        printf '   modinfo ch341: Modul ist NICHT vorhanden\n'
        [ "$DRIVER_STATE" = "registered" ] || DRIVER_STATE="missing"
    fi
else
    MODFOUND=0
    for f in /lib/modules/"$KREL"/kernel/drivers/usb/serial/ch341.ko*; do
        [ -e "$f" ] && MODFOUND=1
    done
    if [ "$MODFOUND" = "1" ]; then
        printf '   ch341.ko: vorhanden unter /lib/modules/%s/kernel/drivers/usb/serial\n' "$KREL"
        [ "$DRIVER_STATE" = "registered" ] || DRIVER_STATE="available"
    elif [ -f "/lib/modules/$KREL/modules.builtin" ] \
        && grep -q 'usb/serial/ch341.ko' "/lib/modules/$KREL/modules.builtin" 2>/dev/null; then
        printf '   ch341: im Kernel einkompiliert\n'
        [ "$DRIVER_STATE" = "registered" ] || DRIVER_STATE="available"
    else
        printf '   ch341: keine Moduldatei und kein builtin-Eintrag gefunden\n'
        [ "$DRIVER_STATE" = "registered" ] || DRIVER_STATE="missing"
    fi
fi

# ---------------------------------------------------------------------------
# 4. Geraetedateien und Rechte
# ---------------------------------------------------------------------------
section "4. Geraetedateien auf dem Host"
DEVS=""
for dev in /dev/ttyUSB* /dev/ttyACM*; do
    [ -e "$dev" ] || continue
    HOST_COUNT=$((HOST_COUNT + 1))
    DEVS="$DEVS $dev"
done

if [ "$HOST_COUNT" = "0" ]; then
    printf '   KEINE Datei /dev/ttyUSB* oder /dev/ttyACM*\n'
else
    for dev in $DEVS; do
        printf '   %s\n' "$(ls -ln "$dev" 2>/dev/null)"
    done

    # Wer den Port im Container oeffnen soll, braucht die Gruppe des Nodes.
    # Auf Debian/Pi OS ist das dialout, GID 20 - dieselbe Zahl steht in der
    # Compose-Datei unter group_add.
    TUSER=""
    if [ "$(id -u)" != "0" ]; then
        TUSER=$(id -un 2>/dev/null)
    elif [ -n "${SUDO_USER:-}" ]; then
        TUSER="$SUDO_USER"
    fi

    printf '\n   Rechte:\n'
    for dev in $DEVS; do
        DUSER=$(stat -c '%U' "$dev" 2>/dev/null || printf '?')
        DGRP=$(stat -c '%G' "$dev" 2>/dev/null || printf '?')
        DMODE=$(stat -c '%a' "$dev" 2>/dev/null || printf '?')
        printf '      %s: %s:%s %s\n' "$dev" "$DUSER" "$DGRP" "$DMODE"
        if [ -n "$TUSER" ] && [ "$DGRP" = "dialout" ]; then
            if id -nG "$TUSER" 2>/dev/null | tr ' ' '\n' | grep -qx dialout; then
                printf '         Benutzer %s ist in dialout\n' "$TUSER"
            else
                printf '         Benutzer %s ist NICHT in dialout (GID 20)\n' "$TUSER"
                PERM_ISSUE=1
            fi
        fi
    done
    printf '\n   GID von dialout: %s\n' "$(getent group dialout 2>/dev/null | cut -d: -f3)"
fi

printf '\n   Stabile Pfade (/dev/serial/by-id):\n'
for byid in /dev/serial/by-id/*; do
    [ -e "$byid" ] || continue
    HAS_BY_ID=1
    printf '      %s -> %s\n' "$(basename "$byid")" "$(readlink "$byid" 2>/dev/null)"
done
[ "$HAS_BY_ID" = "1" ] || printf '      (keine - der Port hat dann keine reboot-stabile Adresse)\n'

# ---------------------------------------------------------------------------
# 5. Container: Sicht, Rechte, Anwendung
#
# Das ist die Trennlinie zwischen "Umgebung" und "Anwendung". Ein Mount von
# /dev macht die Geraetedatei nur SICHTBAR; ob open() gelingt, entscheidet
# die Device-Cgroup.
# ---------------------------------------------------------------------------
section "5. Container \"$CONTAINER\""
if [ "$CHECK_CONTAINER" = "0" ]; then
    printf '   (uebersprungen: --no-container)\n'
elif ! have docker; then
    printf '   docker nicht im PATH - Abschnitt entfaellt\n'
    printf '   (Auf dem Pi: sudo systemctl status docker)\n'
elif ! docker inspect "$CONTAINER" >/dev/null 2>&1; then
    printf '   Container existiert nicht.\n'
    printf '   Pruefen: docker ps -a | grep comfobox\n'
else
    STATE=$(docker inspect "$CONTAINER" --format '{{.State.Status}}' 2>/dev/null)
    [ "$STATE" = "running" ] && CTR_RUNNING=1
    printf '   Zustand:              %s\n' "$STATE"
    printf '   Image:                %s\n' \
        "$(docker inspect "$CONTAINER" --format '{{.Config.Image}}' 2>/dev/null)"
    printf '   Benutzer im Container: %s\n' \
        "$(docker inspect "$CONTAINER" --format '{{.Config.User}}' 2>/dev/null)"
    CGROUP_RULES=$(docker inspect "$CONTAINER" \
        --format '{{.HostConfig.DeviceCgroupRules}}' 2>/dev/null)
    GROUP_ADD=$(docker inspect "$CONTAINER" \
        --format '{{.HostConfig.GroupAdd}}' 2>/dev/null)
    printf '   Device-Cgroup-Regeln:  %s\n' "$CGROUP_RULES"
    printf '   Zusatzgruppen:         %s\n' "$GROUP_ADD"
    case "$GROUP_ADD" in
        *20*) : ;;
        *)    printf '   >>> Gruppe 20 (dialout) fehlt - der Container laeuft als uid 1001.\n' ;;
    esac

    printf '   Mounts mit /dev:\n'
    docker inspect "$CONTAINER" \
        --format '{{range .Mounts}}{{if or (eq .Destination "/dev") (eq .Source "/dev")}}      {{.Type}} {{.Source}} -> {{.Destination}} (rw={{.RW}}){{"\n"}}{{end}}{{end}}' \
        2>/dev/null

    if [ "$CTR_RUNNING" = "1" ]; then
        printf '\n   Kennung im Container:\n'
        docker exec "$CONTAINER" id 2>&1 | sed 's/^/      /'

        CTR_COUNT=$(docker exec "$CONTAINER" sh -c \
            'ls -1 /dev/ttyUSB* /dev/ttyACM* 2>/dev/null | wc -l' 2>/dev/null | tr -d ' \r')
        [ -n "$CTR_COUNT" ] || CTR_COUNT=0
        CTR_CHECKED=1
        printf '\n   Geraetedateien im Container: %s\n' "$CTR_COUNT"
        if [ "$CTR_COUNT" != "0" ]; then
            docker exec "$CONTAINER" sh -c 'ls -ln /dev/ttyUSB* /dev/ttyACM*' 2>&1 \
                | sed 's/^/      /'
        fi

        printf '\n   Oeffnen im Container:\n'
        CTR_OPEN=$(docker exec "$CONTAINER" sh -c '
            found=0
            for d in /dev/ttyUSB0 /dev/ttyUSB1 /dev/ttyUSB2 /dev/ttyACM0; do
                [ -e "$d" ] || continue
                found=1
                if ( exec 3<>"$d" ) 2>/dev/null; then
                    echo "OPEN-OK $d"
                else
                    ERR=$( ( exec 3<>"$d" ) 2>&1 )
                    case "$ERR" in
                        *busy*|*"Resource temporarily"*) echo "BUSY $d" ;;
                        *) echo "OPEN-FEHLER $d $ERR" ;;
                    esac
                fi
            done
            [ "$found" = "1" ] || echo "(kein Geraet zum Testen vorhanden)"' 2>&1)
        printf '%s\n' "$CTR_OPEN" | sed 's/^/      /'
        case "$CTR_OPEN" in *OPEN-FEHLER*) CTR_OPEN_FAIL=1 ;; esac
        case "$CTR_OPEN" in *"Permission denied"*|*"not permitted"*) CGROUP_BLOCKED=1 ;; esac

        # Device-Cgroup v1 kann man im Container direkt lesen; unter v2
        # (Bookworm) gibt es die Datei nicht - dann entscheidet der
        # Oeffnen-Test oben.
        TTY_RULE=$(docker exec "$CONTAINER" sh -c \
            '[ -f /sys/fs/cgroup/devices/devices.list ] && grep -c "^c 188:" /sys/fs/cgroup/devices/devices.list || echo ""' \
            2>/dev/null | tr -d ' \r')
        case "$TTY_RULE" in
            "")  printf '      Device-Cgroup: cgroup v2 (open-Test entscheidet)\n' ;;
            0)   CGROUP_BLOCKED=1
                 printf '      Device-Cgroup: ttyUSB NICHT erlaubt (kein "c 188:")\n' ;;
            *)   printf '      Device-Cgroup: ttyUSB erlaubt (%s Regel(n))\n' "$TTY_RULE" ;;
        esac

        printf '\n   Was der Gateway meldet:\n'
        APP_JSON=$(docker exec "$CONTAINER" sh -c \
            'curl -fsS -m 5 http://localhost:8080/api/config/serial-ports' 2>/dev/null)
        if [ -z "$APP_JSON" ]; then
            printf '      /api/config/serial-ports: keine Antwort\n'
        else
            APP_API=1
            printf '%s\n' "$APP_JSON" | head -c 1200 | sed 's/^/      /'
            APP_COUNT=$(printf '%s' "$APP_JSON" | grep -o '"port"' | wc -l | tr -d ' ')
            printf '\n      Ports laut Anwendung: %s\n' "$APP_COUNT"
        fi
        APP_STATUS=$(docker exec "$CONTAINER" sh -c \
            'curl -fsS -m 5 http://localhost:8080/api/status' 2>/dev/null)
        if [ -n "$APP_STATUS" ]; then
            printf '      /api/status (Auszug):\n'
            printf '%s' "$APP_STATUS" | head -c 700 | sed 's/^/      /'
            printf '\n'
        fi

        printf '\n   Vergleich der drei Sichten:\n'
        printf '      Host (/dev)       : %s Geraet(e)\n' "$HOST_COUNT"
        printf '      Container (/dev)  : %s Geraet(e)\n' "$CTR_COUNT"
        printf '      Anwendung (API)   : %s Port(s)\n' "$APP_COUNT"
    else
        printf '\n   (Container laeuft nicht - Sichtpruefung entfaellt)\n'
    fi
fi

# ---------------------------------------------------------------------------
# 6. Zugriffstest auf dem Host
#
# Der Gateway haelt den Port, solange er laeuft. Deshalb ist ein Oeffnen von
# aussen EBUSY - das ist kein Fehler. Nur --free-port schafft einen freien
# Port; danach wird der Container wieder gestartet.
# ---------------------------------------------------------------------------
section "6. Zugriffstest auf dem Host (nur oeffnen und lesen)"

if [ "$FREE_PORT" = "1" ]; then
    if ! have docker; then
        printf '   (kein Docker gefunden - es wird nichts gestoppt)\n'
    elif [ "$CTR_RUNNING" = "1" ]; then
        printf '   Container wird fuer den Test gestoppt ...\n'
        if docker stop "$CONTAINER" >/dev/null 2>&1; then
            CONTAINER_STOPPED=1
            CTR_RUNNING=0
            sleep 1
            printf '   (wird am Ende automatisch wieder gestartet)\n'
        else
            printf '   WARNUNG: Stoppen fehlgeschlagen - der Port bleibt belegt.\n'
        fi
    else
        printf '   (Container laeuft nicht - Port ist frei)\n'
    fi
fi

TEST_DEVS="$DEVS"
[ -n "$PORT" ] && TEST_DEVS="$PORT"

if [ "$LIST_ONLY" = "1" ]; then
    printf '   (uebersprungen: --list)\n'
elif [ -z "$TEST_DEVS" ]; then
    printf '   (kein Port zum Testen vorhanden)\n'
fi

for dev in $TEST_DEVS; do
    [ "$LIST_ONLY" = "1" ] && break
    printf '\n   %s\n' "$dev"
    if [ ! -e "$dev" ]; then
        printf '      EXISTIERT NICHT\n'
        OPEN_FAIL=1
        continue
    fi

    ERR=$( ( exec 3<>"$dev" ) 2>&1 )
    rc=$?
    if [ "$rc" != "0" ]; then
        case "$ERR" in
            *busy*|*"Resource temporarily"*)
                OPEN_BUSY=1
                printf '      OPEN: belegt (EBUSY) - ein Prozess haelt den Port\n'
                if have fuser; then
                    HOLDER=$(fuser "$dev" 2>/dev/null | tr -s ' ' ' ')
                    [ -n "$HOLDER" ] && printf '      Halter (PID):%s\n' "$HOLDER"
                fi
                if have lsof; then
                    L=$(lsof "$dev" 2>/dev/null | awk 'NR>1 {print "         "$1" (PID "$2")"}')
                    [ -n "$L" ] && printf '      Prozesse:\n%s\n' "$L"
                fi
                printf '      => Test mit --free-port (stoppt den Container und startet ihn wieder)\n'
                ;;
            *)
                OPEN_FAIL=1
                printf '      OPEN: FEHLER (%s)\n' "$(printf '%s' "$ERR" | head -n 1)"
                case "$ERR" in
                    *"Permission denied"*|*"not permitted"*) PERM_ISSUE=1 ;;
                esac
                ;;
        esac
        continue
    fi

    OPEN_OK=1
    printf '      OPEN: ok\n'

    # Die Termios-Werte gelten geraeteweit und ueberleben das Schliessen.
    # Deshalb vor dem Test sichern und am Ende zuruecksetzen - der Gateway
    # soll seinen Port so vorfinden, wie er ihn verlassen hat.
    SAVED=$(stty -F "$dev" -g 2>/dev/null)

    # 8N1, kein Handshake, kein DTR-Hangup beim Schliessen. Die Werte gelten
    # nur fuer die Dauer dieses offenen Deskriptors.
    STTY_OUT=$( ( exec 3<>"$dev" \
        && stty -F "$dev" "$BAUD" cs8 -cstopb -parenb -crtscts raw -echo -hupcl \
        && stty -F "$dev" speed ) 2>&1 )
    rc=$?
    if [ "$rc" != "0" ]; then
        STTY_FAIL=1
        printf '      Termios: FEHLER (%s)\n' "$(printf '%s' "$STTY_OUT" | head -n 1)"
    else
        case "$STTY_OUT" in
            *[!0-9]*|"")
                STTY_FAIL=1
                printf '      Termios: unerwartete Antwort (%s)\n' "$STTY_OUT"
                ;;
            *)
                printf '      Leitungsparameter: %s Baud, 8N1, kein Handshake\n' "$STTY_OUT"
                ;;
        esac
    fi

    # Busdaten lesen. Das ist der einzige Test, der die Elektrik sieht:
    # kommt kein Byte, sind A/B vertauscht, die ComfoBox ist aus oder die
    # Baudrate stimmt nicht.
    if [ "$READ_SECONDS" -gt 0 ] && [ "$STTY_FAIL" = "0" ]; then
        TICKS=$((READ_SECONDS * 10))
        [ "$TICKS" -gt 250 ] && TICKS=250
        RAW=$( (
            exec 3<>"$dev" || exit 1
            stty -F "$dev" "$BAUD" cs8 -cstopb -parenb -crtscts raw -echo -hupcl \
                min 0 time "$TICKS" || exit 2
            dd if="$dev" bs=256 count=1 2>/dev/null | od -An -tx1 -v
        ) 2>/dev/null )
        READ_DONE=1
        READ_BYTES=$(printf '%s' "$RAW" | wc -w | tr -d ' ')
        READ_HEAD=$(printf '%s' "$RAW" | tr -s ' \n' ' ' | cut -c1-64)
        printf '      Lesen (%s s): %s Byte(s)\n' "$READ_SECONDS" "$READ_BYTES"
        [ -n "$READ_HEAD" ] && printf '      erste Bytes: %s\n' "$READ_HEAD"
    fi

    # Stabilitaet: wiederholt oeffnen/schliessen. Ein Port, der dabei
    # verschwindet oder sporadisch EBUSY/EIO liefert, ist nicht zuverlaessig.
    if [ "$CYCLES" -gt 0 ] && [ "$STTY_FAIL" = "0" ]; then
        fails=0
        gone=0
        i=1
        while [ "$i" -le "$CYCLES" ]; do
            ( exec 3<>"$dev" && stty -F "$dev" "$BAUD" cs8 -cstopb -parenb \
                -crtscts raw -echo -hupcl ) 2>/dev/null
            [ "$?" = "0" ] || fails=$((fails + 1))
            [ -e "$dev" ] || gone=$((gone + 1))
            i=$((i + 1))
            sleep 0.2 2>/dev/null || sleep 1
        done
        CYCLE_FAILS=$((CYCLE_FAILS + fails))
        printf '      Stabilitaet: %s/%s Runden ok' "$((CYCLES - fails))" "$CYCLES"
        [ "$fails" != "0" ] && printf ', %s Fehler' "$fails"
        printf '\n'
        if [ "$gone" != "0" ]; then
            VANISH_EVENTS=$((VANISH_EVENTS + gone))
            printf '      Port war %s mal waehrend der Runden NICHT vorhanden\n' "$gone"
        fi
    fi

    # Laenger zusehen: der Adapter kann auch zwischen den Runden wegfallen
    # (Enumeration scheitert, Kernel verliert das Geraet).
    if [ "$WATCH" -gt 0 ]; then
        printf '      Beobachte %s s, ob der Port verschwindet ...\n' "$WATCH"
        last=1
        [ -e "$dev" ] || last=0
        seen=0
        i=0
        while [ "$i" -lt "$WATCH" ]; do
            cur=0
            [ -e "$dev" ] && cur=1
            if [ "$cur" != "$last" ]; then
                if [ "$cur" = "0" ]; then
                    printf '         +%ss: Port ist VERSCHWUNDEN\n' "$i"
                else
                    printf '         +%ss: Port ist wieder da\n' "$i"
                fi
                last=$cur
                VANISH_EVENTS=$((VANISH_EVENTS + 1))
                seen=1
            fi
            sleep 1
            i=$((i + 1))
        done
        [ "$seen" = "0" ] && printf '         Port blieb %s s durchgehend vorhanden\n' "$WATCH"
    fi

    if [ -n "$SAVED" ] && stty -F "$dev" "$SAVED" 2>/dev/null; then
        printf '      Vorherige Leitungsparameter wiederhergestellt\n'
    fi
done

# ---------------------------------------------------------------------------
# 7. Kernel-Meldungen waehrend des Tests
# ---------------------------------------------------------------------------
section "7. Kernel-Meldungen waehrend des Tests"
DMESG_ERRORS=0
if [ "$DMESG_OK" = "1" ]; then
    NEW=$(dmesg 2>/dev/null | tail -n +$((DMESG_BASE + 1)))
    RELEVANT=$(printf '%s\n' "$NEW" \
        | grep -iE 'usb|tty|ch34|ftdi|cp210|pl2303|serial' | tail -n 20)
    if [ -n "$RELEVANT" ]; then
        printf '%s\n' "$RELEVANT" | sed 's/^/   /'
    else
        printf '   (keine USB-/Seriell-Meldungen)\n'
    fi

    # error -71 (EPROTO), -32 (EPIPE), -110 (Timeout), -62: das ist die
    # Signatur eines Kabel-, Hub- oder Stromproblems - nicht des Treibers.
    DMESG_ERRORS=$(printf '%s\n' "$NEW" \
        | grep -cE 'error -(19|32|62|71|110)|unable to enumerate|not accepting address|attempt power cycle|descriptor read|over-current|reset .*USB device' \
            2>/dev/null)
    [ -n "$DMESG_ERRORS" ] || DMESG_ERRORS=0
    if [ "$DMESG_ERRORS" != "0" ]; then
        printf '\n   >>> %s Enumeration-/Verbindungsfehler waehrend des Tests.\n' "$DMESG_ERRORS"
        printf '       Das ist ein Kabel-, Hub- oder Stromproblem.\n'
    fi
else
    printf '   (dmesg nicht lesbar - ohne root oft gesperrt)\n'
fi

# ---------------------------------------------------------------------------
# 8. BEFUND
# ---------------------------------------------------------------------------
section "8. BEFUND"

if [ "$LIST_ONLY" = "1" ]; then
    printf '   Nur aufgelistet - kein Oeffnen, kein Lesen, keine Bewertung.\n'
    printf '   Fuer den vollen Test: sudo sh %s\n' "$0"
    exit 0
fi

printf '   Seriell-Chip am Bus : %s\n' "$([ "$CHIP_SEEN" = "1" ] && printf 'ja' || printf 'nein')"
printf '   Geraetedatei(en)    : %s\n' "$HOST_COUNT"
printf '   Zwischen-Hubs       : %s (davon %s extern)\n' "$MAX_DEPTH" "$MAX_EXT_HUBS"
printf '   Treiber             : %s\n' "$DRIVER_STATE"
printf '   Oeffnen             : %s\n' \
    "$([ "$OPEN_OK" = "1" ] && printf 'ok' || { [ "$OPEN_BUSY" = "1" ] && printf 'belegt' || { [ -z "$TEST_DEVS" ] && printf 'nicht getestet' || printf 'fehlgeschlagen'; }; })"
printf '   Runden-Fehler       : %s\n' "$CYCLE_FAILS"
printf '   Enumeration-Fehler  : %s\n' "$DMESG_ERRORS"
printf '\n'

if [ -n "$PORT" ] && [ ! -e "$PORT" ]; then
    printf '   Der angegebene Port %s existiert nicht.\n' "$PORT"
    printf '\n   => Falscher Pfad oder der Adapter steckt nicht. Pruefen:\n'
    printf '         ls -l /dev/ttyUSB* /dev/ttyACM* /dev/serial/by-id/\n'
elif [ "$HOST_COUNT" = "0" ] && [ "$CHIP_SEEN" = "0" ]; then
    printf '   Auf diesem Host gibt es WEDER eine Datei /dev/ttyUSB* noch einen\n'
    printf '   USB-Seriell-Chip am Bus.\n'
    printf '\n   => Der Adapter steckt nicht (richtig), ist defekt oder das Kabel\n'
    printf '      ist es. Das ist NICHT der Gateway.\n'
    printf '\n   Pruefen:\n'
    printf '      1. Adapter abziehen, kurz warten, neu stecken.\n'
    printf '      2. Andere USB-Buchse am Pi nehmen (direkt, ohne Hub).\n'
    printf '      3. Nochmal: sudo sh %s\n' "$0"
    printf '      4. Zum Gegentest den Adapter an einen PC stecken.\n'
elif [ "$HOST_COUNT" = "0" ] && [ "$DRIVER_STATE" = "missing" ]; then
    printf '   Der Adapter steckt (%s am Bus), aber es gibt keine Geraetedatei\n' "$CHIP_KINDS"
    printf '   /dev/ttyUSB*.\n'
    printf '\n   => Dieser Kernel hat keinen Treiber fuer den Chip.\n'
    printf '\n   Auf Raspberry Pi OS ist ch341 normalerweise dabei. Pruefen:\n'
    printf '      sudo modprobe ch341\n'
    printf '      ls -l /dev/ttyUSB*\n'
    printf '      dmesg | tail -n 20\n'
    printf '   Fehlt das Modul: Kernel aktualisieren und neu starten:\n'
    printf '      sudo apt update && sudo apt full-upgrade && sudo reboot\n'
elif [ "$HOST_COUNT" = "0" ]; then
    printf '   Der Adapter steckt (%s am Bus), aber es gibt keine Geraetedatei\n' "$CHIP_KINDS"
    printf '   /dev/ttyUSB*.\n'
    printf '\n   => Der Treiber ist vorhanden, hat das Geraet aber nicht uebernommen.\n'
    printf '\n   Abhilfe:\n'
    printf '      sudo modprobe ch341\n'
    printf '      Adapter abziehen und neu stecken\n'
    printf '      dmesg | tail -n 20     # sucht ch341-uart und ttyUSB\n'
elif [ "$OPEN_BUSY" = "1" ]; then
    printf '   Der Port ist belegt - der Gateway haelt ihn (das ist normal).\n'
    printf '\n   => Ohne freien Port ist kein Zugriffstest moeglich. Genau dafuer\n'
    printf '      gibt es --free-port:\n'
    printf '         sudo sh %s --free-port --read 5\n' "$0"
    printf '      Der Container wird gestoppt, getestet und danach wieder gestartet.\n'
elif [ "$OPEN_FAIL" = "1" ]; then
    printf '   Der Port existiert, kann aber nicht geoeffnet werden.\n'
    printf '\n   => Rechte oder Device-Cgroup - nicht der Adapter.\n'
    printf '\n   Auf dem Host: Benutzer in die Gruppe dialout (GID 20):\n'
    printf '      sudo usermod -aG dialout %s\n' "${SUDO_USER:-$(id -un 2>/dev/null)}"
    printf '      danach neu anmelden (oder neu starten)\n'
    printf '   Im Container muss beides stimmen:\n'
    printf '      group_add: ["20"]\n'
    printf '      device_cgroup_rules: ["c 188:* rwm", "c 166:* rwm"]\n'
    printf '   Die Compose-Datei in /opt/comfobox enthaelt beides bereits -\n'
    printf '   nach einer Aenderung: docker compose up -d\n'
elif [ "$STTY_FAIL" = "1" ]; then
    printf '   Oeffnen klappt, aber die Leitungsparameter lassen sich nicht setzen.\n'
    printf '\n   => Der Knoten verhaelt sich nicht wie ein serielles Geraet. Das ist\n'
    printf '      entweder ein falscher Pfad (kein tty) oder ein defekter Adapter.\n'
    printf '\n   Pruefen:\n'
    printf '      stty -F %s -a\n' "${TEST_DEVS%% *}"
    printf '      dmesg | tail -n 20\n'
elif [ "$VANISH_EVENTS" -gt 0 ] || [ "$CYCLE_FAILS" -gt 0 ]; then
    printf '   Der Port war waehrend des Tests nicht stabil.\n'
    printf '\n   => Das ist die Signatur von Hub, Kabel oder Stromversorgung -\n'
    printf '      genau wie auf der DS923+ (error -71/-32 hinter zwei Hubs).\n'
    printf '\n   In dieser Reihenfolge:\n'
    printf '      1. Adapter direkt an eine USB-Buchse des Pi (kein Hub).\n'
    printf '      2. Anderes, kurzes Kabel.\n'
    printf '      3. Andere Buchse; bei Pi 4/5 die USB-2-Buchsen probieren.\n'
    printf '      4. Erst danach den Adapter tauschen.\n'
elif [ "$DMESG_ERRORS" -gt 0 ]; then
    printf '   Die Enumeration war fehlerhaft (%s Meldung(en) in Abschnitt 7).\n' "$DMESG_ERRORS"
    printf '\n   => Auch mit Treiber waere die Verbindung instabil. Die Ursache ist\n'
    printf '      ein Kabel-, Hub- oder Stromproblem (error -71 = EPROTO,\n'
    printf '      -32 = EPIPE, -110 = Timeout).\n'
    printf '\n   Adapter direkt an eine Buchse des Pi stecken, ohne Hub und ohne\n'
    printf '   Verlaengerung; danach erneut messen.\n'
elif [ "$CTR_CHECKED" = "1" ] && [ "$CTR_COUNT" = "0" ] && [ "$HOST_COUNT" != "0" ]; then
    printf '   Der Host hat %s Geraet(e), der Container sieht KEINES.\n' "$HOST_COUNT"
    printf '\n   => Der Container bekommt /dev nicht durchgereicht. Das ist eine\n'
    printf '      Einstellung der Compose-Datei, nicht der Anwendung.\n'
    printf '\n   Erwartet wird in /opt/comfobox/compose.yaml:\n'
    printf '      volumes:\n'
    printf '        - /dev:/dev\n'
    printf '   Danach: docker compose up -d\n'
elif [ "$CGROUP_BLOCKED" = "1" ] || [ "$CTR_OPEN_FAIL" = "1" ]; then
    printf '   Der Container SIEHT den Port, darf ihn aber nicht OEFFNEN.\n'
    printf '\n   => Die Device-Cgroup verbietet USB-Seriell (oder die Gruppe fehlt).\n'
    printf '      Der Mount /dev macht die Geraetedatei nur sichtbar.\n'
    printf '\n   In /opt/comfobox/compose.yaml muss beides stehen:\n'
    printf '      device_cgroup_rules:\n'
    printf '        - "c 188:* rwm"     # ttyUSB*\n'
    printf '        - "c 166:* rwm"     # ttyACM*\n'
    printf '      group_add:\n'
    printf '        - "20"              # dialout\n'
    printf '   Danach: cd /opt/comfobox && sudo docker compose up -d\n'
elif [ "$APP_API" = "1" ] && [ "$APP_COUNT" = "0" ] && [ "$CTR_COUNT" != "0" ]; then
    printf '   Der Container sieht den Port, die Anwendung meldet aber keinen.\n'
    printf '\n   => Das ist ein Fehler in der Anwendung (Port-Erkennung).\n'
    printf '      Bitte diese Ausgabe an den Entwickler geben.\n'
    printf '\n   Gegentest ohne die Anwendung:\n'
    printf '      docker exec %s sh -c "exec 3</dev/ttyUSB0"\n' "$CONTAINER"
else
    printf '   Adapter und Port sind in Ordnung.\n'
    printf '\n   - Oeffnen und %s Baud 8N1 funktionieren.\n' "$BAUD"
    if [ "$CYCLES" -gt 0 ] && [ "$CYCLE_FAILS" = "0" ]; then
        printf '   - %s Oeffnen/Schliessen-Runden ohne Fehler.\n' "$CYCLES"
    fi
    [ "$VANISH_EVENTS" = "0" ] && printf '   - Keine Enumeration-Fehler waehrend des Tests.\n'
    printf '\n   Wenn die Heizung trotzdem nicht antwortet:\n'
    printf '      1. A/B vertauscht? (RS485-Leitung)\n'
    printf '      2. ComfoBox auf %s Baud umgestellt UND neu gestartet?\n' "$BAUD"
    printf '         (Werkseinstellung ist 76800)\n'
    printf '      3. Laeuft noch ein zweiter MS/TP-Master auf dem Bus?\n'
    if [ "$CTR_RUNNING" = "1" ]; then
        printf '      4. Busdaten mitlesen: sudo sh %s --free-port --read 10\n' "$0"
    else
        printf '      4. Busdaten mitlesen: sudo sh %s --read 10\n' "$0"
    fi
    if [ "$READ_DONE" = "1" ] && [ "$READ_BYTES" = "0" ]; then
        printf '\n   HINWEIS: Es kamen 0 Byte an. Bei abgeschalteter Anlage ist das\n'
        printf '            normal; sonst deutet es auf A/B oder die Baudrate.\n'
    fi
fi

# ---------------------------------------------------------------------------
# 9. Weitere Beobachtungen
# ---------------------------------------------------------------------------
if [ "$MAX_EXT_HUBS" -gt 0 ]; then
    printf '\n   HINWEIS: Der Adapter haengt hinter %s EXTERNEN Hub(s).\n' "$MAX_EXT_HUBS"
    printf '   Das war auf der DS923+ die Ursache fuer error -71/-32: ein Hub ist\n'
    printf '   eine zusaetzliche Fehlerquelle bei der Enumeration und bei der\n'
    printf '   Stromversorgung. Bitte direkt an eine USB-Buchse stecken\n'
    printf '   (kein Hub, keine Verlaengerung).\n'
    printf '   Der eingebaute Hub des Pi zaehlt hier NICHT mit.\n'
elif [ "$MAX_DEPTH" -gt 0 ]; then
    printf '\n   HINWEIS: Der Adapter haengt am EINGEBAUTEN Hub des Pi (LAN95xx) -\n'
    printf '   an dem haengen auch Ethernet und die USB-Buchsen. Das entspricht\n'
    printf '   "direkt angeschlossen" und ist keine Fehlerquelle.\n'
    printf '   Kein Handlungsbedarf.\n'
fi
if [ "$HAS_BY_ID" = "0" ] && [ "$HOST_COUNT" != "0" ]; then
    printf '\n   HINWEIS: Es gibt keinen /dev/serial/by-id-Pfad. Nach einem Neustart\n'
    printf '   kann aus ttyUSB0 ein ttyUSB1 werden. Der Gateway sucht den Port zwar\n'
    printf '   selbst, ein stabiler Pfad ist trotzdem die bessere Wahl.\n'
fi
if [ "$PERM_ISSUE" = "1" ] && [ "$OPEN_FAIL" = "0" ]; then
    printf '\n   HINWEIS: Der aufrufende Benutzer ist nicht in dialout. Der Port\n'
    printf '   liess sich trotzdem oeffnen (root bzw. Container mit group_add 20).\n'
    printf '   Fuer einen Direktstart ohne Docker: sudo usermod -aG dialout %s\n' \
        "${SUDO_USER:-$(id -un 2>/dev/null)}"
fi
if [ "$CHIP_SEEN" = "1" ] && [ "$CHIP_KINDS" != " CH340" ]; then
    printf '\n   Gefundene Chips:%s\n' "$CHIP_KINDS"
fi
