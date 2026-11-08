#!/bin/sh
# ---------------------------------------------------------------------------
# usb-serial-check.sh – Wo bleibt der USB-RS485-Adapter?
#
# Aufruf (auf der NAS, per SSH):
#     sudo sh usb-serial-check.sh [containername]
#
# Der Containername ist optional, Vorgabe: comfobox-gateway.
#
# **Das Skript aendert nichts.** Es sammelt Beweise und sagt am Ende, wer
# schuld ist: die Hardware, der Kernel, der Container oder die Anwendung.
#
# Warum es das gibt:
#   Im Assistenten steht „Kein serieller Port gefunden". Von aussen sehen
#   fuenf verschiedene Ursachen **gleich** aus:
#
#     1. Der Adapter steckt nicht, ist defekt oder sitzt am falschen Port.
#     2. Das NAS hat keinen Treiber fuer den Chip (CH340 -> ch341,
#        FTDI -> ftdi_sio, CP210x -> cp210x, PL2303 -> pl2303).
#     3. Der Port existiert auf der NAS, kommt aber nicht in den Container
#        (Mount falsch).
#     4. Der Container sieht den Port, darf ihn aber nicht oeffnen
#        (Device-Cgroup / Rechte).
#     5. Die Anwendung (.NET) findet den Port nicht, obwohl er da ist.
#
#   Jede Ursache braucht eine **andere** Gegenmassnahme. Raten kostet
#   Revisionen - Messen nicht.
#
# Ausgabe am Ende: „BEFUND" mit genau einer Ursache und dem naechsten Schritt.
# ---------------------------------------------------------------------------

NAME="${1:-comfobox-gateway}"

# Alle Zaehler, die die Bewertung am Ende braucht.
HOST_COUNT=0
CTR_COUNT=0
APP_COUNT=0
USB_KNOWN=""
USB_TOTAL=0
CTR_RUNNING=0

# Ergebnisse der Einzelpruefungen. Leer heisst "nicht geprueft".
SERIAL_DRV=""      # module | builtin | none   (USB-Seriell-Treiber im Kernel)
USB_LINK=0         # 1 = Enumeration war fehlerhaft (Kabel/Hub/Strom)
USB_HUB_DEPTH=0    # groesste Zahl Zwischen-Hubs vor einem Seriell-Adapter
CGROUP_TTY=""      # Anzahl erlaubter "c 188:"-Regeln im Container
CGROUP_BLOCKED=0   # 1 = Device-Cgroup verbietet ttyUSB
CTR_OPEN_FAIL=0    # 1 = open() im Container scheiterte
KERNEL=$(uname -r 2>/dev/null)

section() {
    echo ""
    echo "=============================================================="
    echo " $1"
    echo "=============================================================="
}

have() {
    command -v "$1" >/dev/null 2>&1
}

# --- 1. System -------------------------------------------------------------
section "1. System"
echo "   Kernel:  $(uname -r 2>/dev/null)"
echo "   Modell:  $(cat /proc/sys/kernel/syno_hw_version 2>/dev/null || echo unbekannt)"
if [ -f /etc.defaults/VERSION ]; then
    echo "   DSM:     $(sed -n 's/^productversion="\(.*\)"/\1/p' /etc.defaults/VERSION 2>/dev/null)"
fi
echo "   User:    $(id -un 2>/dev/null) (uid $(id -u 2>/dev/null))"
if [ "$(id -u 2>/dev/null)" != "0" ]; then
    echo ""
    echo "   HINWEIS: Ohne root fehlen Rechte und einige Angaben. Bitte mit"
    echo "            'sudo sh $0 $NAME' aufrufen."
fi

# --- 2. USB-Geraete am Bus -------------------------------------------------
#
# **Die entscheidende Frage zuerst:** Ist der Adapter ueberhaupt am USB-Bus?
# Das sagt der Kernel ueber sysfs - unabhaengig davon, ob ein Treiber ihn
# bedient. Ist hier nichts zu sehen, ist es die Hardware oder das Kabel.
section "2. USB-Geraete am Bus (sysfs)"
if [ -d /sys/bus/usb/devices ]; then
    for d in /sys/bus/usb/devices/*; do
        [ -f "$d/idVendor" ] || continue
        V=$(cat "$d/idVendor" 2>/dev/null)
        P=$(cat "$d/idProduct" 2>/dev/null)
        PROD=$(cat "$d/product" 2>/dev/null)
        MAN=$(cat "$d/manufacturer" 2>/dev/null)
        USB_TOTAL=$((USB_TOTAL + 1))

        # Bekannte Seriell-Chips. Die Zuordnung ist der ganze Zweck dieser
        # Liste: ein Treffer heisst „der Adapter ist da, nur der Kernel
        # bedient ihn noch nicht".
        KIND=""
        case "$V:$P" in
            1a86:7523|1a86:5523|1a86:55d4) KIND="CH340/CH341" ;;
            0403:*)                        KIND="FTDI" ;;
            10c4:*)                        KIND="CP210x (Silicon Labs)" ;;
            067b:2303)                     KIND="PL2303" ;;
            2341:*)                        KIND="Arduino" ;;
            1a86:*)                        KIND="WCH (unbekannt)" ;;
        esac

        printf '   %-14s %s:%s  %s %s\n' \
            "$(basename "$d")" "$V" "$P" "$MAN" "$PROD"
        if [ -n "$KIND" ]; then
            echo "                  >>> Seriell-Adapter: $KIND"
            USB_KNOWN="$USB_KNOWN $KIND"

            # Die Port-Kette verraet, hinter wie vielen Hubs der Adapter
            # sitzt. Jeder Zwischen-Hub ist eine Fehlerquelle fuer die
            # Enumeration (siehe Abschnitt 5).
            UPORT=$(cat "$d/devpath" 2>/dev/null)
            if [ -n "$UPORT" ]; then
                UDEPTH=$(printf '%s' "$UPORT" | awk -F. '{print NF-1}' 2>/dev/null)
                [ -n "$UDEPTH" ] || UDEPTH=0
                echo "                  USB-Pfad: Port $UPORT ($UDEPTH Zwischen-Hub(s))"
                [ "$UDEPTH" -gt "$USB_HUB_DEPTH" ] 2>/dev/null && USB_HUB_DEPTH=$UDEPTH
            fi
        fi

        # Welcher Treiber haengt an den Interfaces?
        #
        # Nur die Interface-Verzeichnisse tragen einen Doppelpunkt
        # ("1-3.2.1:1.0"). Frueher lief die Schleife ueber ALLE
        # Unterverzeichnisse - dadurch stand bei jedem Geraet
        # "Interface power -> Treiber: driver", also der Name des
        # Symlinks selbst. Die eigentliche Aussage war damit nicht lesbar.
        for i in "$d"/*:*.*/; do
            [ -e "$i" ] || continue
            INO=$(basename "$i")
            if [ -e "$i/driver" ]; then
                # sysfs: ".../1-3.2.1:1.0/driver" ist ein Symlink auf
                # "../../../../bus/usb/drivers/ch341". Der Name des Ziels
                # ist der Treibername - "readlink" ohne -f reicht dafuer.
                LNK=$(readlink "$i/driver" 2>/dev/null)
                [ -n "$LNK" ] || LNK=$(readlink -f "$i/driver" 2>/dev/null)
                DRV=$(basename "$LNK" 2>/dev/null)
                # Kommt der Pfad unaufgeloest zurueck, steht dort "driver" -
                # also der Name des Symlinks selbst. Das ist keine Antwort.
                [ "$DRV" = "driver" ] && DRV="(Name unbekannt)"
                echo "                  Interface $INO -> Treiber: $DRV"
            else
                # Genau der interessante Fall: das Geraet ist am Bus, aber
                # kein Treiber hat es uebernommen.
                echo "                  Interface $INO -> KEIN Treiber gebunden"
            fi
            for t in "$i"tty*; do
                [ -e "$t" ] || continue
                echo "                  tty: /dev/$(basename "$t")"
            done
        done
    done
    [ "$USB_TOTAL" = "0" ] && echo "   (keine USB-Geraete sichtbar)"
else
    echo "   /sys/bus/usb/devices fehlt - sysfs nicht verfuegbar"
fi

# --- 3. Kernel-Treiber -----------------------------------------------------
section "3. Kernel-Treiber"
MODDIR="/lib/modules/$(uname -r 2>/dev/null)/kernel/drivers/usb/serial"
echo "   Modulverzeichnis: $MODDIR"
if [ -d "$MODDIR" ]; then
    echo "   vorhandene Treiber:"
    ls "$MODDIR" 2>/dev/null | sed 's/^/      /'
else
    echo "   (Verzeichnis fehlt)"
fi
echo ""
echo "   Moduldateien der bekannten Chips (irgendwo unter /lib/modules):"
MOD_FOUND=0
for m in ch341 ftdi_sio cp210x pl2303 usbserial; do
    HIT=$(find "/lib/modules/$KERNEL" -name "$m.ko*" -print -quit 2>/dev/null)
    if [ -n "$HIT" ]; then
        echo "      $m: $HIT"
        MOD_FOUND=$((MOD_FOUND + 1))
    else
        echo "      $m: NICHT vorhanden (Modul fehlt im Kernel)"
    fi
done

# Vielleicht sind die Treiber FEST einkompiliert (dann gibt es keine
# .ko-Datei, aber der Treiber ist trotzdem da).
BUILTIN_HITS=0
for b in /lib/modules/$KERNEL/modules.builtin; do
    [ -f "$b" ] || continue
    BUILTIN_HITS=$(grep -cE 'usb/serial/(ch341|ftdi_sio|cp210x|pl2303|usbserial)\.ko' "$b" 2>/dev/null)
    [ -n "$BUILTIN_HITS" ] || BUILTIN_HITS=0
done
if [ "$BUILTIN_HITS" != "0" ]; then
    echo "      (im Kernel einkompiliert: $BUILTIN_HITS Eintrag/Eintraege in modules.builtin)"
fi

echo ""
if [ ! -d "/lib/modules/$KERNEL" ]; then
    # Ohne lesbares Modulverzeichnis ist die Frage NICHT beantwortet - und
    # eine falsche Antwort ("kein Treiber") waere schlimmer als keine.
    echo "   /lib/modules/$KERNEL ist nicht lesbar - die Frage bleibt offen."
    echo "   (Ohne root sind die Modulverzeichnisse oft nicht lesbar -"
    echo "   bitte mit sudo aufrufen.)"
elif [ "$MOD_FOUND" != "0" ]; then
    SERIAL_DRV=module
    echo "   USB-Seriell-Treiber: als Modul vorhanden"
elif [ "$BUILTIN_HITS" != "0" ]; then
    SERIAL_DRV=builtin
    echo "   USB-Seriell-Treiber: im Kernel einkompiliert"
else
    SERIAL_DRV=none
    echo "   USB-Seriell-Treiber: KEINER. Dieser Kernel kann keinen"
    echo "   USB-Seriell-Adapter bedienen - unabhaengig vom Chip."
fi
echo ""

if have lsmod; then
    echo "   geladene Module:"
    for m in ch341 ftdi_sio cp210x pl2303 usbserial; do
        if lsmod 2>/dev/null | awk '{print $1}' | grep -qx "$m"; then
            echo "      $m: GELADEN"
        else
            echo "      $m: nicht geladen"
        fi
    done
else
    echo "   (lsmod nicht verfuegbar)"
fi

# --- 4. Geraetedateien auf der NAS -----------------------------------------
section "4. Geraetedateien auf der NAS"
HOST_COUNT=$(ls -1 /dev/ttyUSB* /dev/ttyACM* 2>/dev/null | wc -l | tr -d ' ')
if [ "$HOST_COUNT" = "0" ]; then
    echo "   KEINE Geraetedatei /dev/ttyUSB* oder /dev/ttyACM*"
else
    ls -ln /dev/ttyUSB* /dev/ttyACM* 2>/dev/null | sed 's/^/   /'
    echo ""
    echo "   Lesbar/schreibbar fuer den aufrufenden Benutzer:"
    for d in /dev/ttyUSB* /dev/ttyACM*; do
        [ -e "$d" ] || continue
        if [ -r "$d" ] && [ -w "$d" ]; then
            echo "      $d: ja"
        else
            echo "      $d: NEIN"
        fi
    done
fi

echo ""
echo "   Stabile Pfade (/dev/serial/by-id):"
if ls -1 /dev/serial/by-id/* >/dev/null 2>&1; then
    ls -l /dev/serial/by-id/ 2>/dev/null | sed 's/^/      /'
else
    echo "      (keine - dann gibt es auch keine stabile Adresse)"
fi

echo ""
echo "   ports.json im PAKET-Verzeichnis (nur ein Protokoll des postinst):"
echo "   Der Gateway liest diese Datei NICHT - er liest seine eigene im"
echo "   Docker-Volume (DataDirectory) und fragt sonst das Betriebssystem."
PJ="/var/packages/ComfoBoxGateway/var/ports.json"
if [ -f "$PJ" ]; then
    sed 's/^/      /' "$PJ"
else
    echo "      (fehlt - der Gateway fragt dann das Betriebssystem)"
fi

# --- 5. Kernel-Meldungen ---------------------------------------------------
section "5. Kernel-Meldungen (USB / seriell)"
if have dmesg; then
    DMESG_ALL=$(dmesg 2>/dev/null)
    OUT=$(printf '%s\n' "$DMESG_ALL" \
        | grep -iE 'usb|tty|ch34|ftdi|cp210|pl2303|serial' | tail -n 25)
    if [ -n "$OUT" ]; then
        printf '%s\n' "$OUT" | sed 's/^/   /'
    else
        echo "   (keine passenden Meldungen)"
    fi

    # Ein Adapter kann am Bus stehen und trotzdem unzuverlaessig sein:
    #   error -71 (EPROTO), error -32 (EPIPE), -110 (Timeout)
    #   "unable to enumerate", "device not accepting address",
    #   "attempt power cycle"
    # Das ist ein Kabel-, Hub- oder Stromproblem - und es faellt im
    # Assistenten genauso aus wie ein fehlender Treiber.
    ERR_COUNT=$(printf '%s\n' "$DMESG_ALL" \
        | grep -cE 'error -(19|32|62|71|110)|unable to enumerate|not accepting address|attempt power cycle|descriptor read/64, error' 2>/dev/null)
    [ -n "$ERR_COUNT" ] || ERR_COUNT=0
    if [ "$ERR_COUNT" != "0" ]; then
        USB_LINK=1
        echo ""
        echo "   >>> $ERR_COUNT Enumeration-Fehler gefunden."
        echo "       Der Adapter wurde mehrfach abgelehnt bzw. neu verhandelt."
        echo "       Das ist ein Kabel-, Hub- oder Stromproblem - nicht der"
        echo "       Treiber. Auch mit Treiber waere die Verbindung instabil."
    fi
else
    echo "   (dmesg nicht verfuegbar)"
fi

# --- 6. Container: Konfiguration -------------------------------------------
section "6. Container '$NAME' - Konfiguration"
if ! have docker; then
    echo "   docker nicht im PATH - Abschnitte 6 bis 8 entfallen"
    echo "   (Auf DSM: /var/packages/ContainerManager/target/usr/bin/docker)"
else
    if docker inspect "$NAME" >/dev/null 2>&1; then
        STATE=$(docker inspect "$NAME" --format '{{.State.Status}}' 2>/dev/null)
        echo "   Zustand:            $STATE"
        [ "$STATE" = "running" ] && CTR_RUNNING=1
        echo "   Image:              $(docker inspect "$NAME" --format '{{.Config.Image}}' 2>/dev/null)"
        echo "   Benutzer im Container: $(docker inspect "$NAME" --format '{{.Config.User}}' 2>/dev/null)"
        echo "   Privilegiert:       $(docker inspect "$NAME" --format '{{.HostConfig.Privileged}}' 2>/dev/null)"
        echo "   Devices:            $(docker inspect "$NAME" --format '{{.HostConfig.Devices}}' 2>/dev/null)"
        # **Die Device-Cgroup entscheidet, ob ein sichtbares Geraet auch
        # geoeffnet werden darf.** Ein Bind-Mount von /dev macht die Knoten
        # sichtbar, erteilt aber KEINE Rechte.
        echo "   Device-Cgroup-Regeln: $(docker inspect "$NAME" --format '{{.HostConfig.DeviceCgroupRules}}' 2>/dev/null)"
        echo "   Mounts mit /dev:"
        docker inspect "$NAME" \
            --format '{{range .Mounts}}{{if or (eq .Destination "/dev") (eq .Source "/dev")}}      {{.Type}} {{.Source}} -> {{.Destination}} (rw={{.RW}}){{"\n"}}{{end}}{{end}}' \
            2>/dev/null

        # Was die Device-Cgroup **wirklich** erlaubt (cgroup v1).
        # Der Mount "/dev" macht die Geraeteknoten nur SICHTBAR - ob open()
        # gelingt, entscheidet diese Liste. Docker erlaubt standardmaessig
        # KEIN USB-Seriell: Major 188 (ttyUSB*) und 166 (ttyACM*) fehlen.
        if [ "$CTR_RUNNING" = "1" ]; then
            CGROUP_TTY=$(docker exec "$NAME" sh -c \
                'cat /sys/fs/cgroup/devices/devices.list 2>/dev/null | grep -c "^c 188:"' \
                2>/dev/null | tr -d ' \r')
            if [ -z "$CGROUP_TTY" ]; then
                echo "   ttyUSB in Device-Cgroup: (nicht lesbar)"
            elif [ "$CGROUP_TTY" = "0" ]; then
                CGROUP_BLOCKED=1
                echo "   ttyUSB in Device-Cgroup: NICHT erlaubt (kein 'c 188:')"
            else
                echo "   ttyUSB in Device-Cgroup: erlaubt ($CGROUP_TTY Regel(n))"
            fi
        fi
    else
        echo "   Container '$NAME' existiert nicht."
        echo "   Pruefen: docker ps -a | grep comfobox"
    fi
fi

# --- 7. Container: Sicht auf /dev ------------------------------------------
section "7. Container - Sicht auf die Geraete"
if [ "$CTR_RUNNING" = "1" ]; then
    echo "   Kennung im Container:"
    docker exec "$NAME" id 2>&1 | sed 's/^/      /'
    echo ""
    echo "   Geraetedateien im Container:"
    CTR_COUNT=$(docker exec "$NAME" sh -c \
        'ls -1 /dev/ttyUSB* /dev/ttyACM* 2>/dev/null | wc -l' 2>/dev/null | tr -d ' \r')
    [ -z "$CTR_COUNT" ] && CTR_COUNT=0
    if [ "$CTR_COUNT" = "0" ]; then
        echo "      KEINE"
    else
        docker exec "$NAME" sh -c 'ls -ln /dev/ttyUSB* /dev/ttyACM*' 2>&1 | sed 's/^/      /'
    fi
    echo ""
    echo "   Oeffnen und wieder schliessen (der eigentliche Zugriffstest):"
    OPEN_OUT=$(docker exec "$NAME" sh -c '
        found=0
        for d in /dev/ttyUSB0 /dev/ttyUSB1 /dev/ttyACM0; do
            [ -e "$d" ] || continue
            found=1
            if exec 3<>"$d" 2>/dev/null; then
                exec 3>&-
                echo "      $d: OPEN-OK"
            else
                echo "      $d: OPEN-FEHLER (Rechte oder Device-Cgroup)"
            fi
        done
        [ "$found" = "1" ] || echo "      (kein Geraet zum Testen vorhanden)"' 2>&1)
    printf '%s\n' "$OPEN_OUT" | sed 's/^/   /'
    case "$OPEN_OUT" in *OPEN-FEHLER*) CTR_OPEN_FAIL=1 ;; esac
    echo ""
    echo "   Sieht der Container /dev ueberhaupt? (erste Eintraege)"
    docker exec "$NAME" sh -c 'ls -1 /dev | head -n 20' 2>&1 | sed 's/^/      /'
else
    echo "   (uebersprungen - der Container laeuft nicht)"
fi

# --- 8. Was die Anwendung meldet -------------------------------------------
#
# **Das ist die Trennlinie zwischen „unser Fehler" und „Umgebung".**
# Der Endpunkt liefert genau das, was der Assistent anzeigt.
section "8. Was der Gateway meldet"
if [ "$CTR_RUNNING" = "1" ]; then
    echo "   GET /api/config/serial-ports:"
    APP_JSON=$(docker exec "$NAME" sh -c \
        'curl -fsS -m 5 http://localhost:8080/api/config/serial-ports' 2>/dev/null)
    if [ -z "$APP_JSON" ]; then
        echo "      (keine Antwort - laeuft der Gateway?)"
    else
        printf '%s\n' "$APP_JSON" | head -c 1500 | sed 's/^/      /'
        APP_COUNT=$(printf '%s' "$APP_JSON" | grep -o '"port"' | wc -l | tr -d ' ')
    fi
    echo ""
    echo "   Vergleich der drei Sichten:"
    echo "      NAS (/dev)            : $HOST_COUNT Geraet(e)"
    echo "      Container (/dev)      : $CTR_COUNT Geraet(e)"
    echo "      Anwendung (API)       : $APP_COUNT Port(s)"
    echo ""
    echo "   Zustand laut Gateway (Auszug):"
    docker exec "$NAME" sh -c 'curl -fsS -m 5 http://localhost:8080/api/status' 2>/dev/null \
        | head -c 800 | sed 's/^/      /'
    echo ""
else
    echo "   (uebersprungen - der Container laeuft nicht)"
fi

# --- 9. Bewertung ----------------------------------------------------------
section "9. BEFUND"
USB_LIST=$(printf '%s' "$USB_KNOWN" | sed 's/^ *//')

if [ "$HOST_COUNT" = "0" ] && [ -z "$USB_LIST" ]; then
    echo "   Auf der NAS gibt es KEINE serielle Geraetedatei und auch KEINEN"
    echo "   USB-Seriell-Chip am Bus."
    echo ""
    echo "   => Der Adapter ist nicht angeschlossen, sitzt nicht richtig, ist"
    echo "      defekt oder das Kabel ist es. Das ist NICHT der Gateway."
    echo ""
    echo "   Pruefen:"
    echo "      1. Adapter abziehen, kurz warten, neu stecken."
    echo "      2. Anderen USB-Port am NAS nehmen (die hinteren sind stabiler)."
    echo "      3. Nochmal: sudo sh $0 $NAME"
    echo "      4. Zum Gegentest den Adapter an einen PC stecken."
elif [ "$HOST_COUNT" = "0" ] && [ "$SERIAL_DRV" = "none" ]; then
    echo "   Der Adapter STECKT (gefunden:$USB_LIST), aber es gibt keine"
    echo "   Geraetedatei /dev/ttyUSB*."
    echo ""
    echo "   => Dieser Kernel hat GAR KEINEN USB-Seriell-Treiber. Das ist"
    echo "      NICHT der Gateway - und auch kein Defekt des Adapters."
    echo ""
    echo "   Abschnitt 3 findet weder ch341 noch ftdi_sio/cp210x/pl2303 -"
    echo "      und auch nicht usbserial, den gemeinsamen Unterbau. Ohne den"
    echo "      kann KEIN USB-Seriell-Chip bedient werden."
    echo "      'modprobe ch341' scheitert daher mit 'module not found'."
    echo "      Ein Adapter mit FTDI-Chip hilft NICHT: ftdi_sio fehlt genauso."
    echo ""
    echo "   Drei Wege:"
    echo ""
    echo "   A) Treibermodule fuer genau diesen Kernel nachbauen/-installieren"
    echo "      (Community, nicht von Synology unterstuetzt):"
    echo "         https://github.com/robertklep/dsm7-usb-serial-drivers"
    echo "      Passend fuer: DSM 7.4.1, Kernel $KERNEL"
    echo "      Nachteile: muss nach JEDEM DSM-Update wiederholt werden und"
    echo "      nach jedem Boot erneut geladen werden (Aufgabe im"
    echo "      DSM-Aufgabenplaner: 'bei Systemstart')."
    echo ""
    echo "   B) Den Gateway dort laufen lassen, wo der Treiber im Kernel ist:"
    echo "      Raspberry Pi (Home-Assistant-Add-on) oder Mini-PC. Der NAS"
    echo "      bleibt nur noch Broker bzw. Ziel der Oberflaeche."
    echo ""
    echo "   C) Anderer USB-Port und kuerzeres Kabel - das behebt nur die"
    echo "      Enumeration (Abschnitt 5), nicht den fehlenden Treiber."
    if [ "$USB_LINK" = "1" ]; then
        echo ""
        echo "   ZUSATZ: Die Enumeration war fehlerhaft ($USB_HUB_DEPTH"
        echo "      Zwischen-Hub(s), error -32/-71 in Abschnitt 5). Selbst mit"
        echo "      Treiber waere die Verbindung instabil. Adapter direkt an"
        echo "      eine Buchse der NAS stecken, ohne Hub und ohne Verlaengerung."
    fi
elif [ "$HOST_COUNT" = "0" ]; then
    echo "   Der Adapter STECKT (gefunden:$USB_LIST), aber es gibt keine"
    echo "   Geraetedatei /dev/ttyUSB*."
    echo ""
    if [ -z "$SERIAL_DRV" ]; then
        echo "   => Ob der Kernel den Treiber hat, ist offen (Abschnitt 3:"
        echo "      Modulverzeichnis nicht lesbar). Bitte mit sudo aufrufen."
    else
        echo "   => Der Treiber ist im Kernel vorhanden, aber nicht geladen."
        echo "      Das ist NICHT der Gateway."
    fi
    echo ""
    echo "   Abhilfe (als root, einmalig):"
    echo "      modprobe usbserial"
    echo "      modprobe ch341        # bzw. ftdi_sio / cp210x / pl2303"
    echo "      ls -l /dev/ttyUSB*"
    if [ "$USB_LINK" = "1" ]; then
        echo ""
        echo "   ZUSATZ: Die Enumeration war fehlerhaft (error -32/-71,"
        echo "      $USB_HUB_DEPTH Zwischen-Hub(s)). Adapter direkt an die NAS"
        echo "      stecken - sonst faellt der Port im Betrieb wieder weg."
    fi
elif [ "$CTR_COUNT" = "0" ] && [ "$CTR_RUNNING" = "1" ]; then
    echo "   Die NAS hat $HOST_COUNT Geraet(e), der Container sieht KEINES."
    echo ""
    echo "   => Der Container bekommt /dev nicht durchgereicht. Das ist eine"
    echo "      Einstellung des Pakets bzw. der Compose-Datei, aber NICHT der"
    echo "      Anwendung."
    echo ""
    echo "   Pruefen: docker inspect $NAME --format '{{.Mounts}}'"
    echo "   Erwartet wird ein Mount von /dev der NAS nach /dev im Container."
elif [ "$CTR_OPEN_FAIL" = "1" ] || [ "$CGROUP_BLOCKED" = "1" ]; then
    echo "   Der Container SIEHT den Port, darf ihn aber nicht OEFFNEN."
    echo ""
    echo "   => Die Device-Cgroup des Containers verbietet USB-Seriell."
    echo "      Der Mount '/dev' macht die Geraetedatei nur sichtbar; ob"
    echo "      open() gelingt, entscheidet die Cgroup. Docker erlaubt"
    echo "      standardmaessig kein Major 188 (ttyUSB) und kein 166 (ttyACM)."
    echo ""
    echo "   Abhilfe in der Compose-Datei des Pakets:"
    echo "      device_cgroup_rules:"
    echo "        - \"c 188:* rwm\""
    echo "        - \"c 166:* rwm\""
    echo ""
    echo "   (Nicht 'group_add' und nicht 'privileged' - siehe"
    echo "   deploy/synology-spk/src/target/compose/docker-compose.yaml.)"
elif [ "$APP_COUNT" = "0" ] && [ "$CTR_COUNT" != "0" ]; then
    echo "   Der Container sieht $CTR_COUNT Geraet(e), die Anwendung meldet"
    echo "   aber KEINEN Port."
    echo ""
    echo "   => Das ist ein Fehler in der Anwendung (Port-Erkennung)."
    echo "      Bitte genau diese Ausgabe an den Entwickler geben."
    echo ""
    echo "      Test ohne die Anwendung:"
    echo "         docker exec $NAME sh -c 'ls -l /dev/ttyUSB0'"
    echo "         docker exec $NAME sh -c 'exec 3<>/dev/ttyUSB0 && echo OK'"
    echo ""
    echo "      Hinweis: .NET liefert nur Geraete, die es als seriellen Port"
    echo "      erkennt. Ein OPEN-FEHLER in Abschnitt 7 (Rechte) laesst den"
    echo "      Port zwar in der Liste stehen, aber die Verbindung scheitert."
elif [ "$APP_COUNT" != "0" ]; then
    echo "   NAS, Container und Anwendung sehen denselben Port"
    echo "   ($APP_COUNT Port(s) laut API)."
    echo ""
    echo "   => Die Erkennung funktioniert. Wenn der Assistent trotzdem nichts"
    echo "      anzeigt, ist es die Oberflaeche - dann bitte die Browser-Konsole"
    echo "      und die Antwort von /api/config/serial-ports vergleichen."
    if [ "$USB_LINK" = "1" ]; then
        echo ""
        echo "   ACHTUNG: Die Enumeration war fehlerhaft (Abschnitt 5). Der"
        echo "      Port kann im Betrieb wieder verschwinden - Kabel und Hub"
        echo "      pruefen, bevor der Fehler bei der Anlage gesucht wird."
    fi
else
    echo "   Keine eindeutige Zuordnung moeglich - bitte die Abschnitte 1-8"
    echo "   oben auswerten."
fi

echo ""
echo "=============================================================="
echo " Fertig. Es wurde nichts veraendert."
echo "=============================================================="
