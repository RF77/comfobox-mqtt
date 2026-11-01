#!/bin/bash
# Syntaxprüfung aller Shell-Skripte des SPK-Skeletts (Aufruf: bash tools/check-shell.sh)
set -u
cd "$(dirname "$0")/.." || exit 1

FILES="
build-spk.sh
src/target/bin/common.sh
src/target/bin/serial-scan.sh
src/target/bin/supervisor.sh
src/scripts/preinst
src/scripts/postinst
src/scripts/preuninst
src/scripts/postuninst
src/scripts/start-stop-status
tools/test-config-parse.sh
"

fail=0
for f in $FILES; do
    if bash -n "$f" 2>/tmp/comfobox-shellcheck.err; then
        echo "OK   $f"
    else
        echo "FAIL $f"
        sed 's/^/       /' /tmp/comfobox-shellcheck.err
        fail=1
    fi
done

# Der Parser der config.json ist Shell-Code - er wird gegen eine
# realistische Datei geprüft.
echo ""
if bash tools/test-config-parse.sh; then
    echo "OK   tools/test-config-parse.sh"
else
    echo "FAIL tools/test-config-parse.sh"
    fail=1
fi

exit $fail
