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
        fi

        # Welcher Treiber haengt an den Interfaces?
        for i in "$d"/*/; do
            [ -e "$i" ] || continue
            DRV=$(basename "$(readlink -f "$i/driver" 2>/dev/null)" 2>/dev/null)
            [ -n "$DRV" ] && [ "$DRV" != "." ] \
                && echo "                  Interface $(basename "$i") -> Treiber: $DRV"
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
for m in ch341 ftdi_sio cp210x pl2303 usbserial; do
    HIT=$(find "/lib/modules/$(uname -r 2>/dev/null)" -name "$m.ko*" -print -quit 2>/dev/null)
    if [ -n "$HIT" ]; then
        echo "      $m: $HIT"
    else
        echo "      $m: NICHT vorhanden (Modul fehlt im Kernel)"
    fi
done
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
echo "   ports.json im Datenverzeichnis (vom Supervisor erzeugt, optional):"
PJ="/var/packages/ComfoBoxGateway/var/ports.json"
if [ -f "$PJ" ]; then
    sed 's/^/      /' "$PJ"
else
    echo "      (fehlt - der Gateway fragt dann das Betriebssystem)"
fi

# --- 5. Kernel-Meldungen ---------------------------------------------------
section "5. Kernel-Meldungen (USB / seriell)"
if have dmesg; then
    OUT=$(dmesg 2>/dev/null | grep -iE 'usb|tty|ch34|ftdi|cp210|pl2303|serial' | tail -n 25)
    if [ -n "$OUT" ]; then
        echo "$OUT" | sed 's/^/   /'
    else
        echo "   (keine passenden Meldungen)"
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
    docker exec "$NAME" sh -c '
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
        [ "$found" = "1" ] || echo "      (kein Geraet zum Testen vorhanden)"' 2>&1 | sed 's/^/   /'
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
elif [ "$HOST_COUNT" = "0" ]; then
    echo "   Der Adapter STECKT (gefunden:$USB_LIST), aber es gibt keine"
    echo "   Geraetedatei /dev/ttyUSB*."
    echo ""
    echo "   => Dem Kernel fehlt der Treiber. Das ist NICHT der Gateway."
    echo ""
    echo "   Abhilfe (als root, einmalig):"
    echo "      modprobe usbserial"
    echo "      modprobe ch341        # bzw. ftdi_sio / cp210x / pl2303"
    echo "      ls -l /dev/ttyUSB*"
    echo ""
    echo "   Bleibt es dabei, fehlt das Modul im DSM-Kernel (siehe Abschnitt 3)."
    echo "   Dann hilft nur ein Adapter mit unterstuetztem Chip - FTDI (0403:)"
    echo "   wird von DSM am zuverlaessigsten unterstuetzt."
elif [ "$CTR_COUNT" = "0" ] && [ "$CTR_RUNNING" = "1" ]; then
    echo "   Die NAS hat $HOST_COUNT Geraet(e), der Container sieht KEINES."
    echo ""
    echo "   => Der Container bekommt /dev nicht durchgereicht. Das ist eine"
    echo "      Einstellung des Pakets bzw. der Compose-Datei, aber NICHT der"
    echo "      Anwendung."
    echo ""
    echo "   Pruefen: docker inspect $NAME --format '{{.Mounts}}'"
    echo "   Erwartet wird ein Mount von /dev der NAS nach /dev im Container."
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
else
    echo "   Keine eindeutige Zuordnung moeglich - bitte die Abschnitte 1-8"
    echo "   oben auswerten."
fi

echo ""
echo "=============================================================="
echo " Fertig. Es wurde nichts veraendert."
echo "=============================================================="
