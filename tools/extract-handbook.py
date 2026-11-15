#!/usr/bin/env python3
"""Extrahiert den Text des ELESTA-Technikerhandbuchs aus der PDF.

Das Handbuch ist urheberrechtlich geschuetzt und darf nicht in dieses
oeffentliche Repository eingecheckt werden. Dieses Werkzeug erzeugt eine
Textdatei fuer die lokale Arbeit; das Zielverzeichnis ist in ``.gitignore``
eingetragen.

Aufruf::

    python -m pip install pypdf
    python tools/extract-handbook.py "H:/Dokumente/SmartHome/ComfoBox/164829_TH_ECR450_V4.0.pdf"

Ergebnis: ``docs/07-technikerhandbuch/_volltext/ecr450.txt`` – eine Seite pro
Block, eingeleitet durch ``===== SEITE n =====``.

Hinweis zur Qualitaet: die PDF-Extraktion liefert die Spalten der Parametertabellen
nicht in der richtigen Reihenfolge. Die Zuordnung *Bezeichnung -> Datenpunkt* ist
deshalb in ``docs/07-technikerhandbuch/datenpunkt-register.md`` von Hand kuratiert.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

DEFAULT_OUTPUT = Path(__file__).resolve().parent.parent / "docs" / "07-technikerhandbuch" / "_volltext" / "ecr450.txt"


def extract(pdf_path: Path) -> str:
    """Liest alle Seiten der PDF und gibt sie als Textblock zurueck."""
    try:
        from pypdf import PdfReader
    except ImportError:  # pragma: no cover - Hinweis statt Traceback
        sys.exit("pypdf fehlt. Installation: python -m pip install pypdf")

    reader = PdfReader(str(pdf_path))
    parts: list[str] = []
    for number, page in enumerate(reader.pages, start=1):
        text = page.extract_text() or ""
        parts.append(f"===== SEITE {number} =====\n{text}")
    return "\n".join(parts)


def main() -> int:
    parser = argparse.ArgumentParser(description="Technikerhandbuch als Text extrahieren.")
    parser.add_argument("pdf", type=Path, help="Pfad zur PDF-Datei")
    parser.add_argument(
        "--out",
        type=Path,
        default=DEFAULT_OUTPUT,
        help=f"Zieldatei (Default: {DEFAULT_OUTPUT})",
    )
    args = parser.parse_args()

    if not args.pdf.is_file():
        sys.exit(f"PDF nicht gefunden: {args.pdf}")

    text = extract(args.pdf)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    # LF-Zeilenenden: das Repository verlangt sie (siehe .gitattributes / Doku-Regeln).
    args.out.write_text(text, encoding="utf-8", newline="\n")

    pages = text.count("===== SEITE ")
    print(f"{pages} Seiten, {len(text)} Zeichen -> {args.out}")
    if args.out.is_relative_to(Path(__file__).resolve().parent.parent):
        print("Achtung: Ziel liegt im Repository – nur zulaessig, wenn der Pfad ignoriert wird.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
