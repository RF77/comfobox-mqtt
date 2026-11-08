#!/bin/sh
# ---------------------------------------------------------------------------
# probe-worker.sh – findet heraus, **womit** der DSM-Worker "docker-project"
# Compose aufruft. Aufruf auf der NAS:
#
#   sudo sh probe-worker.sh [/pfad/zum/paket.spk]
#
# Das Skript installiert nichts, startet nichts und aendert nichts. Es sammelt
# nur Fakten (Laufzeit < 5 s).
#
# Warum es das gibt:
#   Der Worker meldet `fail compose up '<uuid>'` (2201) **ohne** Volume,
#   **ohne** Container und **ohne** Verzeichnis unter /volume3/docker. Er
#   scheitert also, bevor Docker Arbeit bekommt. Der bisherige Test mit dem
#   **Standalone**-Binary `docker-compose` lief durch - der Worker benutzt aber
#   moeglicherweise das **CLI-Plugin** `docker compose`. Fehlt das Plugin im
#   Kontext des Workers, bricht der Aufruf sofort ab.
# ---------------------------------------------------------------------------
set -u

SPK="${1:-}"
PKG=ComfoBoxGateway

echo "=============================================================="
echo " Welchen Compose-Aufruf benutzt der DSM-Worker?"
echo "=============================================================="
echo ""

# --- Docker finden (sudo hat einen minimalen PATH) -------------------------
DOCKER=""
for c in /usr/local/bin/docker \
         /var/packages/ContainerManager/target/usr/bin/docker \
         /var/packages/Docker/target/usr/bin/docker; do
    [ -x "$c" ] && { DOCKER="$c"; break; }
done
[ -n "$DOCKER" ] || DOCKER=$(command -v docker 2>/dev/null || true)

if [ -z "$DOCKER" ]; then
    echo "FEHLER: docker nicht gefunden."
    exit 1
fi
echo "1) Docker-Binary: $DOCKER"
"$DOCKER" version --format '   Server: {{.Server.Version}}' 2>&1 | head -n 2
echo ""

echo "2) Compose als CLI-PLUGIN ('docker compose') - so ruft es der Worker"
PLUGIN_OUT=$("$DOCKER" compose version 2>&1)
PLUGIN_RC=$?
printf '%s\n' "$PLUGIN_OUT" | head -n 3 | sed 's/^/   /'
if [ "$PLUGIN_RC" = "0" ]; then
    echo "   -> PLUGIN VORHANDEN"
else
    echo "   -> PLUGIN FEHLT ODER IST KAPUTT (exit=$PLUGIN_RC) - das erklaert 2201!"
fi
echo ""

echo "3) Compose als STANDALONE-Binary ('docker-compose')"
DC=""
for c in /var/packages/ContainerManager/target/usr/bin/docker-compose \
         /usr/local/bin/docker-compose; do
    [ -x "$c" ] && { DC="$c"; break; }
done
if [ -n "$DC" ]; then
    echo "   $DC"
    "$DC" version 2>&1 | head -n 2 | sed 's/^/   /'
else
    echo "   (nicht gefunden)"
fi
echo ""

echo "4) Wo sucht das Docker-CLI nach Plugins?"
for d in /root/.docker/cli-plugins \
         /usr/local/lib/docker/cli-plugins \
         /usr/lib/docker/cli-plugins \
         /usr/libexec/docker/cli-plugins \
         /var/packages/ContainerManager/target/usr/lib/docker/cli-plugins; do
    if [ -d "$d" ]; then
        echo "   $d:"
        ls -la "$d" 2>/dev/null | tail -n +2 | sed 's/^/      /'
    fi
done
echo ""

echo "5) Der echte Test: compose config ueber das Plugin"
# Die Compose-Datei aus dem installierten Paket nehmen - oder aus dem SPK.
CF=""
if [ -f "/var/packages/$PKG/target/compose/compose.yaml" ]; then
    CF="/var/packages/$PKG/target/compose/compose.yaml"
fi
if [ -z "$CF" ] && [ -n "$SPK" ] && [ -f "$SPK" ]; then
    T=$(mktemp -d 2>/dev/null) || T=/tmp/probe.$$
    mkdir -p "$T"
    if tar xf "$SPK" -C "$T" package.tgz 2>/dev/null \
       && tar xzf "$T/package.tgz" -C "$T" compose/compose.yaml 2>/dev/null; then
        CF="$T/compose/compose.yaml"
    fi
fi

if [ -z "$CF" ]; then
    echo "   (keine Compose-Datei gefunden - Paketpfad als Argument angeben)"
else
    echo "   Datei: $CF"
    echo "   --- docker compose config (Plugin) ---"
    "$DOCKER" compose -f "$CF" config >/dev/null 2>/tmp/probe-plugin.err
    RC=$?
    if [ "$RC" = "0" ]; then
        echo "      OK (exit=0)"
    else
        echo "      FEHLER (exit=$RC):"
        head -n 10 /tmp/probe-plugin.err | sed 's/^/      /'
    fi
    rm -f /tmp/probe-plugin.err

    if [ -n "$DC" ]; then
        echo "   --- $DC config (Standalone) ---"
        if "$DC" -f "$CF" config >/dev/null 2>&1; then
            echo "      OK (exit=0)"
        else
            echo "      FEHLER (exit=$?)"
        fi
    fi
fi
echo ""

echo "6) Wie ruft der Worker Compose auf? (Texte aus dem Binary)"
LIB=/var/packages/ContainerManager/target/worker/libdockerprojectgeneratorworker.so
if [ -f "$LIB" ]; then
    tr -c '[:print:]' '\n' < "$LIB" 2>/dev/null \
        | grep -i -E 'compose[-_.a-z]*\.(ya?ml)|docker-compose|cli-plugins|/usr/bin/docker|"--?[a-z-]+"' \
        | sort -u | head -n 40 | sed 's/^/   /'
    echo ""
    echo "   (Dateinamen mit 'compose' darin - der erwartete Name muss hier stehen)"
else
    echo "   $LIB nicht gefunden"
fi
echo ""

echo "7) Zustand des Container Managers"
ls -la /var/packages/ContainerManager/etc 2>/dev/null | head -n 15 | sed 's/^/   /'
echo ""
echo "   Projektordner:"
ls -la /var/packages/ContainerManager/etc/projects 2>/dev/null | head -n 10 | sed 's/^/      /'
echo ""

echo "=============================================================="
echo " Fertig - nichts wurde veraendert."
echo "=============================================================="
