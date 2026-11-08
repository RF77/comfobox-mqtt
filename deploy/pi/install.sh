#!/bin/sh
# ---------------------------------------------------------------------------
# ComfoBox Gateway – Installation in einem Schritt
#
#   curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/pi/install.sh | sudo sh
#
# Das Skript installiert Docker (falls nötig), legt /opt/comfobox an, startet
# den Container und richtet den Autostart ein. Es ist wiederholbar: ein
# erneuter Aufruf aktualisiert das Image und lässt die Konfiguration stehen.
#
# Optionen (statt "| sh" dann "| sudo sh -s -- <option>"):
#   --version X.Y.Z   Image-Version (Vorgabe: neuestes GitHub-Release)
#   --port N          Port der Web-Oberfläche auf dem Pi (Vorgabe: 8099)
#   --offline DATEI   Image aus einer .tar.gz laden statt aus der Registry
#   --uninstall       Container und Compose-Datei entfernen (Daten bleiben)
#   --help            Hilfe anzeigen
#
# **Die Daten bleiben bei jedem Update erhalten.** Sie liegen im
# Docker-Volume "comfobox-data" – darunter der 24-h-Schreibzähler
# (EEPROM-Schutz der ComfoBox). Niemals mit "docker compose down -v" löschen.
# ---------------------------------------------------------------------------
set -e

REPO="RF77/comfobox-mqtt"
IMAGE_NAME="ghcr.io/rf77/comfobox-gateway"

# Vorgabe-Version. Wird überschrieben, sobald GitHub erreichbar ist oder
# --version übergeben wurde. Bei einem Release mitziehen.
DEFAULT_VERSION="1.0.4"

TARGET_DIR="/opt/comfobox"
CONTAINER="comfobox-gateway"
VOLUME="comfobox-data"

WEB_PORT="8099"
VERSION=""
OFFLINE=""
UNINSTALL=0

# ---------------------------------------------------------------------------
# Hilfsfunktionen
# ---------------------------------------------------------------------------
info() { printf '  %s\n' "$*"; }
step() { printf '\n==> %s\n' "$*"; }
warn() { printf '  WARNUNG: %s\n' "$*" >&2; }
die()  { printf '\nFEHLER: %s\n' "$*" >&2; exit 1; }

usage() {
    # Der Text steht hier als Literal und wird NICHT aus $0 gelesen: bei
    # "curl … | sudo sh" ist $0 die Shell selbst ("sh") und die Datei nicht
    # lesbar - ein sed auf $0 liefe ins Leere.
    cat <<'USAGE_EOF'
ComfoBox Gateway - Installation auf einem Raspberry Pi in einem Schritt

  curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/pi/install.sh | sudo sh

Das Skript installiert Docker (falls noetig), legt /opt/comfobox an, startet
den Container und richtet den Autostart ein. Es ist wiederholbar: ein
erneuter Aufruf aktualisiert das Image und laesst die Konfiguration stehen.

Optionen (bei "| sh" so uebergeben: ... | sudo sh -s -- --port 8099):
  --version X.Y.Z   Image-Version (Vorgabe: neuestes GitHub-Release)
  --port N          Port der Web-Oberflaeche auf dem Pi (Vorgabe: 8099)
  --offline DATEI   Image aus einer .tar.gz laden statt aus der Registry
  --uninstall       Container und Compose-Datei entfernen (Daten bleiben)
  --help            Diese Hilfe anzeigen

Die Daten bleiben bei jedem Update erhalten. Sie liegen im Docker-Volume
"comfobox-data" - darunter der 24-h-Schreibzaehler (EEPROM-Schutz der
ComfoBox). Niemals mit "docker compose down -v" loeschen.
USAGE_EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --version)   [ -n "${2:-}" ] || die "--version braucht eine Angabe"; VERSION="$2"; shift 2 ;;
        --port)      [ -n "${2:-}" ] || die "--port braucht eine Zahl"; WEB_PORT="$2"; shift 2 ;;
        --offline)   [ -n "${2:-}" ] || die "--offline braucht eine Datei"; OFFLINE="$2"; shift 2 ;;
        --uninstall) UNINSTALL=1; shift ;;
        --help|-h)   usage; exit 0 ;;
        *)           die "Unbekannte Option '$1' (--help zeigt die Möglichkeiten)" ;;
    esac
done

# ---------------------------------------------------------------------------
# 1. Voraussetzungen prüfen
# ---------------------------------------------------------------------------
step "Voraussetzungen prüfen"

if [ "$(id -u)" != "0" ]; then
    die "Bitte mit Root-Rechten starten:  ... | sudo sh"
fi

ARCH=$(uname -m)
case "$ARCH" in
    aarch64|arm64)  info "Architektur: $ARCH (64-Bit ARM)" ;;
    x86_64|amd64)   info "Architektur: $ARCH (64-Bit x86)" ;;
    armv7l|armv6l)
        # Der Pi 3 ist 64-Bit-fähig, läuft aber mit dem 32-Bit-Raspberry-Pi-OS
        # als armv7l. Für dieses Image gibt es keine armhf-Variante – und
        # .NET 10 unterstützt armhf auch nicht mehr.
        die "Dieses System ist 32-Bit ($ARCH). Das Image gibt es nur für
       64-Bit (aarch64).
       Raspberry Pi 3/4/5: bitte das 64-Bit-Betriebssystem verwenden
       (Raspberry Pi OS 64-bit, Lite genügt) und danach erneut starten.
       Prüfen mit:  getconf LONG_BIT   ->  muss 64 sein"
        ;;
    *)
        warn "Unbekannte Architektur '$ARCH' – es wird trotzdem versucht."
        ;;
esac

# Freier Speicher: Image (~260 MB) + Entpacken. Weniger als 1 GB ist knapp.
FREE_MB=$(df -Pm /var/lib 2>/dev/null | awk 'NR==2 {print $4}' || echo 0)
if [ -n "$FREE_MB" ] && [ "$FREE_MB" -lt 1024 ] 2>/dev/null; then
    warn "Nur ${FREE_MB} MB frei unter /var/lib – das ist knapp (Bedarf ~1 GB)."
fi

# ---------------------------------------------------------------------------
# 2. Version bestimmen
# ---------------------------------------------------------------------------
if [ -z "$VERSION" ]; then
    # Neuestes Release über die API – ohne Auth auf 60 Abrufe/Stunde begrenzt.
    # Schlägt es fehl (offline, Rate-Limit), bleibt DEFAULT_VERSION.
    VERSION=$(curl -fsSL --max-time 10 \
        "https://api.github.com/repos/${REPO}/releases/latest" 2>/dev/null \
        | sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"v\{0,1\}\([^"]*\)".*/\1/p' \
        | head -n 1 || true)
    if [ -z "$VERSION" ]; then
        VERSION="$DEFAULT_VERSION"
        info "Version: $VERSION (Vorgabe – GitHub war nicht erreichbar)"
    else
        info "Version: $VERSION (neuestes Release)"
    fi
else
    info "Version: $VERSION (übergeben)"
fi

IMAGE="${IMAGE_NAME}:${VERSION}"

# ---------------------------------------------------------------------------
# 3. Docker installieren, falls nötig
# ---------------------------------------------------------------------------
step "Docker prüfen"

if ! command -v docker >/dev/null 2>&1; then
    info "Docker ist nicht installiert – wird jetzt installiert."
    if ! command -v curl >/dev/null 2>&1; then
        die "curl fehlt. Bitte zuerst installieren:  sudo apt-get install -y curl"
    fi
    curl -fsSL https://get.docker.com -o /tmp/get-docker.sh \
        || die "Das Docker-Installationsskript konnte nicht geladen werden."
    sh /tmp/get-docker.sh
    rm -f /tmp/get-docker.sh
else
    info "Docker ist vorhanden: $(docker --version 2>/dev/null)"
fi

# Docker muss auch nach einem Neustart laufen – sonst startet der Container nicht.
if command -v systemctl >/dev/null 2>&1; then
    systemctl enable docker >/dev/null 2>&1 || warn "Docker konnte nicht für den Autostart aktiviert werden."
    systemctl start docker  >/dev/null 2>&1 || true
fi

# Der Docker-Dienst braucht einen Moment, bis der Socket da ist.
i=0
while [ "$i" -lt 30 ]; do
    docker info >/dev/null 2>&1 && break
    i=$((i + 1))
    sleep 1
done
docker info >/dev/null 2>&1 || die "Der Docker-Dienst antwortet nicht (systemctl status docker)."

# Compose v2 ist beim Docker-Paket dabei; ältere Installationen haben es nicht.
if docker compose version >/dev/null 2>&1; then
    COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE="docker-compose"
    warn "Es wird das alte docker-compose verwendet. Besser: docker-compose-plugin installieren."
else
    info "Compose fehlt – wird aus dem Paket 'docker-compose-plugin' installiert."
    if command -v apt-get >/dev/null 2>&1; then
        apt-get update -qq && apt-get install -y docker-compose-plugin >/dev/null 2>&1 || true
    fi
    docker compose version >/dev/null 2>&1 \
        || die "Docker Compose ist nicht verfügbar. Bitte 'docker-compose-plugin' installieren."
    COMPOSE="docker compose"
fi
info "Compose: $($COMPOSE version --short 2>/dev/null || echo 'docker compose')"

# ---------------------------------------------------------------------------
# 4. Entfernen
# ---------------------------------------------------------------------------
if [ "$UNINSTALL" = "1" ]; then
    step "Gateway entfernen"
    if [ -f "$TARGET_DIR/compose.yaml" ]; then
        ( cd "$TARGET_DIR" && $COMPOSE down ) || warn "Der Container konnte nicht sauber gestoppt werden."
    fi
    rm -rf "$TARGET_DIR"
    info "Container und $TARGET_DIR entfernt."
    info "Die Daten (Volume '$VOLUME') bleiben erhalten: config.json, Schreibzähler, Logs."
    info "Endgültig löschen:  docker volume rm $VOLUME"
    exit 0
fi

# ---------------------------------------------------------------------------
# 5. Serielle Adapter anzeigen (für die spätere Auswahl im Assistenten)
# ---------------------------------------------------------------------------
step "Serielle Adapter"

# Der Container bekommt /dev durchgereicht und sucht den Port zur Laufzeit.
# Ein fehlender Adapter ist deshalb KEIN Hindernis für die Installation.
USB_LIST=$(ls -1 /dev/ttyUSB* /dev/ttyACM* 2>/dev/null || true)
if [ -n "$USB_LIST" ]; then
    for d in $USB_LIST; do
        info "gefunden: $d"
    done
else
    warn "Kein /dev/ttyUSB* oder /dev/ttyACM* gefunden."
    warn "Der Adapter kann später angesteckt werden – die Web-Oberfläche findet ihn dann selbst."
    if ls -1 /sys/bus/usb/devices/*/idVendor >/dev/null 2>&1; then
        warn "Am USB-Bus sichtbar sind:"
        for d in /sys/bus/usb/devices/*; do
            [ -f "$d/idVendor" ] || continue
            printf '           %s:%s %s\n' \
                "$(cat "$d/idVendor" 2>/dev/null)" "$(cat "$d/idProduct" 2>/dev/null)" \
                "$(cat "$d/product" 2>/dev/null)" >&2
        done
    fi
fi

# ---------------------------------------------------------------------------
# 6. Compose-Datei schreiben
# ---------------------------------------------------------------------------
step "Konfiguration anlegen"

mkdir -p "$TARGET_DIR"

# **Diese Datei wird bei jedem Lauf neu geschrieben.** Eigene Änderungen
# gehen damit verloren – sie gehören ohnehin nicht hierher, sondern in die
# config.json (die schreibt der Assistent, sie liegt im Volume).
cat > "$TARGET_DIR/compose.yaml" <<COMPOSE_EOF
# ComfoBox Gateway – erzeugt von install.sh
# Neu erzeugen:  curl -fsSL https://raw.githubusercontent.com/${REPO}/master/deploy/pi/install.sh | sudo sh
name: comfobox

services:
  gateway:
    image: ${IMAGE}
    container_name: ${CONTAINER}
    # Autostart: der Container kommt nach jedem Neustart des Pi von selbst
    # wieder hoch (Docker-Dienst ist per systemctl enable aktiviert).
    restart: unless-stopped

    ports:
      - "${WEB_PORT}:8080/tcp"

    environment:
      - COMFOBOX_Gateway__DataDirectory=/var/lib/comfobox
      - COMFOBOX_Logging__Directory=/var/lib/comfobox/logs
      # Startwert; der Assistent schreibt Port und Baudrate in die config.json
      # (die hat Vorrang).
      - COMFOBOX_BACNET__Baudrate=38400
      - ASPNETCORE_URLS=http://+:8080
      # Server-GC ist für kleine Geräte falsch: er belegt mehrere Heaps und
      # kostet auf einem Pi 3 mit 1 GB Speicher unnötig RAM.
      - DOTNET_gcServer=0

    volumes:
      # Persistente Daten: config.json, 24-h-Schreibzähler, Logs.
      - ${VOLUME}:/var/lib/comfobox
      # Alle seriellen Geräte. Der Gateway wählt den Port zur Laufzeit –
      # ein später angesteckter Adapter erscheint damit ohne Neustart.
      - /dev:/dev

    # Der Mount macht /dev nur SICHTBAR. Ob der Prozess die Geräte öffnen
    # darf, entscheidet die Device-Cgroup (Major 188 = ttyUSB, 166 = ttyACM).
    device_cgroup_rules:
      - "c 188:* rwm"
      - "c 166:* rwm"

    # /dev/ttyUSB0 gehört root:dialout mit Modus 0660. Der Container läuft
    # als uid 1001 und braucht deshalb die Gruppe dialout (GID 20 auf Debian
    # und Raspberry Pi OS).
    group_add:
      - "20"

    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8080/api/health"]
      interval: 30s
      timeout: 5s
      # Der Pi 3 braucht länger als ein Server – .NET startet dort gemächlich.
      start_period: 60s
      retries: 3

    security_opt:
      - no-new-privileges:true

    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "5"

volumes:
  ${VOLUME}:
    name: ${VOLUME}
COMPOSE_EOF

info "$TARGET_DIR/compose.yaml geschrieben (Port ${WEB_PORT})"

# ---------------------------------------------------------------------------
# 7. Image bereitstellen
# ---------------------------------------------------------------------------
step "Image bereitstellen"

if [ -n "$OFFLINE" ]; then
    [ -f "$OFFLINE" ] || die "Die Datei '$OFFLINE' gibt es nicht."
    info "Lade Image aus $OFFLINE …"
    case "$OFFLINE" in
        *.gz) gunzip -c "$OFFLINE" | docker load ;;
        *)    docker load -i "$OFFLINE" ;;
    esac
else
    info "Ziehe $IMAGE …"
    if ! docker image inspect "$IMAGE" >/dev/null 2>&1; then
        if ! docker pull "$IMAGE"; then
            die "Das Image konnte nicht geladen werden.
       Prüfen: Internet? Ist das Paket öffentlich?
       Alternativ das Image aus dem GitHub-Release laden:
         wget https://github.com/${REPO}/releases/download/v${VERSION}/comfobox-gateway-arm64.tar.gz
         sudo sh install.sh --offline comfobox-gateway-arm64.tar.gz"
        fi
    else
        info "Das Image liegt bereits lokal."
    fi
fi

# ---------------------------------------------------------------------------
# 8. Starten
# ---------------------------------------------------------------------------
step "Gateway starten"

cd "$TARGET_DIR"
$COMPOSE up -d

info "Warte auf die Web-Oberfläche (auf einem Pi 3 kann das 1–2 Minuten dauern) …"
HEALTHY=0
i=0
while [ "$i" -lt 90 ]; do
    if curl -fsS --max-time 3 "http://localhost:${WEB_PORT}/api/health" >/dev/null 2>&1; then
        HEALTHY=1
        break
    fi
    i=$((i + 1))
    sleep 2
done

# Die Adresse, unter der der Pi erreichbar ist.
PI_IP=$(hostname -I 2>/dev/null | awk '{print $1}')
[ -n "$PI_IP" ] || PI_IP="<IP-des-Pi>"

echo ""
if [ "$HEALTHY" = "1" ]; then
    echo "=============================================================="
    echo " Fertig. Das Gateway läuft."
    echo "=============================================================="
else
    echo "=============================================================="
    echo " Installiert – die Oberfläche antwortet noch nicht."
    echo "=============================================================="
    info "Log ansehen:  cd $TARGET_DIR && sudo $COMPOSE logs -f gateway"
fi

cat <<SUMMARY

  Web-Oberfläche   http://${PI_IP}:${WEB_PORT}
  Assistent        http://${PI_IP}:${WEB_PORT}   (öffnet sich beim ersten Aufruf)
  Home Assistant   http://${PI_IP}:${WEB_PORT}   ← diese Adresse in der
                   ComfoBox-Integration (HACS) eintragen

  Nächste Schritte
    1. Web-Oberfläche öffnen und den Assistenten durchlaufen
       (Port wählen, Baudrate 38400, MQTT-Broker eintragen)
    2. Konfiguration → API-Token erzeugen (wird nur EINMAL angezeigt)
    3. In Home Assistant die Integration "ComfoBox" hinzufügen und
       Adresse + Token eintragen

  Nützlich
    Log        cd ${TARGET_DIR} && sudo ${COMPOSE} logs -f gateway
    Neu start  cd ${TARGET_DIR} && sudo ${COMPOSE} restart
    Update     curl -fsSL https://raw.githubusercontent.com/${REPO}/master/deploy/pi/install.sh | sudo sh

  Die Daten bleiben bei Update und Neustart erhalten (Volume '${VOLUME}').
SUMMARY
