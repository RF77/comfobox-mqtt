#!/usr/bin/env python3
"""Vergleicht die MQTT-Payloads zweier Gateway-Instanzen (Aufgabe 7.2).

Der Vergleich zeigt, dass die alte und die neue Implementierung **dieselben**
Werte publizieren - die Voraussetzung für die Umschaltung auf ``ComfoBox``.

Aufruf::

    # Live gegen Live (zwei Gateways, unterschiedliches BaseTopic)
    python tools/compare-payloads/compare_payloads.py \\
        --old mqtt://broker:1883/ComfoBox/# \\
        --new mqtt://broker:1883/ComfoBox2/# \\
        --duration 300

    # Gegen eine Aufzeichnung des alten Gateways (Golden Master)
    python tools/compare-payloads/compare_payloads.py \\
        --old-file old-snapshot.json \\
        --new mqtt://broker:1883/ComfoBox2/# \\
        --duration 300

    # Aufzeichnung anlegen (am realen System, mit altem Gateway)
    python tools/compare-payloads/compare_payloads.py record \\
        --source mqtt://broker:1883/ComfoBox/# \\
        --out old-snapshot.json --duration 600

Der Exit-Code ist **ungleich 0**, wenn es Abweichungen gab.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

# Der Ordner, in dem das Paket liegt, muss im Suchpfad sein - das Skript
# wird direkt gestartet und ist kein installiertes Paket.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from cbcompare.catalog import Catalog
from cbcompare.compare import (
    DEFAULT_MAX_SKEW_CYCLES,
    DEFAULT_TOLERANCE,
    compare,
)
from cbcompare.snapshot import Snapshot
from cbcompare.sources import collect_from_uri, parse_mqtt_uri

#: Der Standardkatalog: die eingefrorene Test-Fixture des Projekts.
DEFAULT_CATALOG = "docs/topics.json"


def build_parser() -> argparse.ArgumentParser:
    """Baut den Argument-Parser."""
    parser = argparse.ArgumentParser(
        prog="compare_payloads.py",
        description="Vergleicht die MQTT-Payloads zweier Gateway-Instanzen.",
    )
    parser.add_argument(
        "command",
        nargs="?",
        default="compare",
        choices=("compare", "record"),
        help="compare (Vergleich) oder record (Aufzeichnung).",
    )

    parser.add_argument("--old", help="MQTT-URI der alten Seite, z. B. mqtt://host:1883/ComfoBox/#")
    parser.add_argument("--new", help="MQTT-URI der neuen Seite, z. B. mqtt://host:1883/ComfoBox2/#")
    parser.add_argument("--old-file", help="Aufzeichnung der alten Seite (JSON).")
    parser.add_argument("--new-file", help="Aufzeichnung der neuen Seite (JSON).")
    parser.add_argument("--source", help="MQTT-URI für den Aufzeichnungs-Modus.")
    parser.add_argument("--out", help="Zieldatei der Aufzeichnung (JSON).")
    parser.add_argument(
        "--duration",
        type=float,
        default=300.0,
        help="Dauer der Aufzeichnung in Sekunden (Standard: 300).",
    )
    parser.add_argument(
        "--tolerance",
        type=float,
        default=DEFAULT_TOLERANCE,
        help=f"Toleranz für Zahlenwerte (Standard: {DEFAULT_TOLERANCE}).",
    )
    parser.add_argument(
        "--poll-interval",
        type=float,
        default=60.0,
        help="Dauer eines Poll-Zyklus in Sekunden (Standard: 60).",
    )
    parser.add_argument(
        "--secondary-factor",
        type=float,
        default=5.0,
        help=(
            "Wie viele Poll-Zyklen ein sekundaeres Item wartet "
            "(Standard: 5 - so ist der Gateway konfiguriert)."
        ),
    )
    parser.add_argument(
        "--max-skew-cycles",
        type=float,
        default=DEFAULT_MAX_SKEW_CYCLES,
        help="Zulässige Zeitstempel-Differenz in Poll-Zyklen (Standard: 2).",
    )
    parser.add_argument("--catalog", default=DEFAULT_CATALOG, help="Pfad zu docs/topics.json.")
    parser.add_argument("--json", dest="json_out", help="Schreibt den Report als JSON in diese Datei.")
    parser.add_argument("--username", help="MQTT-Benutzername.")
    parser.add_argument("--password", help="MQTT-Passwort.")
    return parser


def main(argv: list[str] | None = None) -> int:
    """Der Einstiegspunkt."""
    args = build_parser().parse_args(argv)

    try:
        catalog = Catalog.load(args.catalog)
    except (OSError, ValueError, TypeError) as exc:
        print(f"FEHLER: Der Katalog '{args.catalog}' konnte nicht gelesen werden: {exc}", file=sys.stderr)
        return 2

    if args.command == "record":
        return run_record(args)

    return run_compare(args, catalog)


def run_record(args: argparse.Namespace) -> int:
    """Zeichnet die Payloads eines Brokers auf."""
    if not args.source or not args.out:
        print("FEHLER: 'record' braucht --source und --out.", file=sys.stderr)
        return 2

    try:
        parse_mqtt_uri(args.source)
    except ValueError as exc:
        print(f"FEHLER: {exc}", file=sys.stderr)
        return 2

    print(f"Aufzeichnung von {args.source} für {args.duration:.0f} s ...")
    snapshot = collect_from_uri(
        args.source, args.duration, username=args.username, password=args.password
    )
    snapshot.save(args.out)

    print(f"Aufzeichnung geschrieben: {args.out} ({len(snapshot.values)} Topics)")
    return 0


def run_compare(args: argparse.Namespace, catalog: Catalog) -> int:
    """Vergleicht die beiden Seiten."""
    if not (args.old or args.old_file) or not (args.new or args.new_file):
        print(
            "FEHLER: Es braucht --old/--old-file und --new/--new-file.",
            file=sys.stderr,
        )
        return 2

    try:
        old = load_side(args.old_file, args.old, args)
        new = load_side(args.new_file, args.new, args)
    except (OSError, ValueError, RuntimeError) as exc:
        print(f"FEHLER: {exc}", file=sys.stderr)
        return 2

    report = compare(
        old,
        new,
        catalog,
        tolerance=args.tolerance,
        poll_interval=args.poll_interval,
        max_skew_cycles=args.max_skew_cycles,
        secondary_factor=args.secondary_factor,
    )

    print_report(report)

    if args.json_out:
        Path(args.json_out).write_text(
            json.dumps(report.as_dict(), indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
        print(f"JSON-Report: {args.json_out}")

    return 0 if report.ok else 1


def load_side(file_path: str | None, uri: str | None, args: argparse.Namespace) -> Snapshot:
    """Lädt eine Seite: aus einer Datei oder live vom Broker."""
    if file_path:
        return Snapshot.load(file_path)

    assert uri is not None
    return collect_from_uri(uri, args.duration, username=args.username, password=args.password)


def print_report(report) -> None:
    """Gibt den Report aus."""
    print(f"Verglichene Topics : {report.compared}")

    if not report.differences:
        print("Keine Abweichungen.")
    else:
        print(f"Abweichungen       : {len(report.differences)}")
        print()
        print(f"{'Topic':<44} {'Art':<7} {'alt':<22} {'neu':<22} Grund")
        print("-" * 130)
        for difference in report.differences:
            print(
                f"{difference.topic:<44} {difference.kind:<7} "
                f"{_short(difference.old):<22} {_short(difference.new):<22} "
                f"{difference.reason}{(' - ' + difference.detail) if difference.detail else ''}"
            )

    if report.notes:
        print()
        print(f"Hinweise           : {len(report.notes)}")
        for note in report.notes[:10]:
            print(f"  - {note}")
        if len(report.notes) > 10:
            print(f"  ... und {len(report.notes) - 10} weitere.")


def _short(value: str | None, length: int = 20) -> str:
    """Kürzt einen Wert für die Ausgabe."""
    if value is None:
        return "-"
    if len(value) <= length:
        return value
    return value[: length - 1] + "…"


if __name__ == "__main__":
    sys.exit(main())
