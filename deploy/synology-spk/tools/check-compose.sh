#!/bin/bash
# Reproduziert den Worker-Aufruf des DSM lokal (soweit ohne Daemon moeglich).
#
# **Wichtig:** Der DSM-Worker "docker-project" sucht die Compose-Datei unter
# <ziel>/<path>/ mit einem **festen** Namen - nachgewiesen im Binary
# `libdockerprojectgeneratorworker.so`:
#
#     docker-compose.yaml
#     docker-compose.yml
#     docker-compose.admin.yaml
#
# `compose.yaml` ist NICHT dabei (den kennt nur Docker Compose selbst).
# Deshalb heisst die Datei im Paket `compose/docker-compose.yaml`.
set -u
cd "$(dirname "$0")/.." || exit 1

WORK=$(mktemp -d) || exit 1
SPK=$(ls -t dist/*.spk 2>/dev/null | head -n 1)
echo "SPK: $SPK"

tar xpf "$SPK" -C "$WORK" package.tgz 2>/dev/null || { echo "package.tgz nicht lesbar"; exit 1; }
mkdir -p "$WORK/target"
tar xzf "$WORK/package.tgz" -C "$WORK/target"

echo ""
echo "=== package.tgz entpackt (Struktur wie auf der NAS) ==="
find "$WORK/target" -maxdepth 2 | sed "s|$WORK/target|<target>|" | sort

echo ""
echo "=== Der Name, den der Worker sucht ==="
FOUND=""
for f in docker-compose.yaml docker-compose.yml docker-compose.admin.yaml; do
    if [ -f "$WORK/target/compose/$f" ]; then
        echo "   OK   compose/$f"
        [ -n "$FOUND" ] || FOUND="$f"
    fi
done
if [ -z "$FOUND" ]; then
    echo "   FEHLER: keine Datei mit einem Namen, den der Worker kennt!"
    echo "   Vorhanden:"
    ls -1 "$WORK/target/compose" 2>/dev/null | sed 's/^/      /'
    rm -rf "$WORK"
    exit 1
fi
if [ -f "$WORK/target/compose/compose.yaml" ]; then
    echo "   WARNUNG: compose.yaml liegt daneben - wird vom Worker ignoriert"
fi
echo ""

CF="$WORK/target/compose/$FOUND"
echo "=== compose/$FOUND (gepatcht) ==="
sed 's/^/   /' "$CF"

echo ""
echo "=== docker compose config (strikt) ==="
( cd "$WORK/target/compose" && docker compose -f "$FOUND" config --quiet 2>&1 | head -20 ) && echo "config: OK"

echo ""
echo "=== docker compose --dry-run up (simuliert den Worker) ==="
( cd "$WORK/target/compose" && docker compose -f "$FOUND" --dry-run up -d 2>&1 | head -40 )

echo ""
echo "=== docker compose --dry-run pull ==="
( cd "$WORK/target/compose" && docker compose -f "$FOUND" --dry-run pull 2>&1 | head -20 )

rm -rf "$WORK"
