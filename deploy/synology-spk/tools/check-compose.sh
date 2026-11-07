#!/bin/bash
# Reproduziert den Worker-Aufruf des DSM lokal (soweit ohne Daemon moeglich).
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
echo "=== compose/compose.yaml (gepatcht) ==="
sed 's/^/   /' "$WORK/target/compose/compose.yaml"

echo ""
echo "=== docker compose config (strikt) ==="
( cd "$WORK/target/compose" && docker compose -f compose.yaml config --quiet 2>&1 | head -20 ) && echo "config: OK"

echo ""
echo "=== docker compose --dry-run up (simuliert den Worker) ==="
( cd "$WORK/target/compose" && docker compose -f compose.yaml --dry-run up -d 2>&1 | head -40 )

echo ""
echo "=== docker compose --dry-run pull ==="
( cd "$WORK/target/compose" && docker compose -f compose.yaml --dry-run pull 2>&1 | head -20 )

rm -rf "$WORK"
