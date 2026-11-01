"""Tests der Vergleichsregeln.

Die Regeln stammen aus ``docs/06-aufgaben/phase-7.md`` (Aufgabe 7.2):
Zahlen mit Toleranz, Enums exakt, Verfügbarkeit über die Zeitstempel.
"""

from __future__ import annotations

from cbcompare.catalog import KIND_ENUM, KIND_NUMBER, Catalog, ItemSpec
from cbcompare.compare import (
    REASON_MISSING_NEW,
    REASON_NOT_A_NUMBER,
    REASON_STALE,
    REASON_VALUE,
    compare,
)
from cbcompare.snapshot import RecordedValue, Snapshot
from conftest import TOPICS_JSON


def catalog() -> Catalog:
    """Ein kleiner Katalog für die Tests."""
    return Catalog(
        [
            ItemSpec(topic_path="Zone/OperationMode", kind=KIND_ENUM),
            ItemSpec(topic_path="Zone/Temperature", kind=KIND_NUMBER),
            ItemSpec(topic_path="Zone/Name", kind="text"),
        ]
    )


def snapshot(base_topic: str, values: dict[str, str], timestamp: str = "2026-11-01T10:00:00Z") -> Snapshot:
    """Ein Snapshot aus einfachen Werten."""
    return Snapshot(
        recorded_at=timestamp,
        source="test",
        base_topic=base_topic,
        values={
            f"{base_topic}/{path}": RecordedValue(payload=payload, timestamp=timestamp)
            for path, payload in values.items()
        },
    )


def test_gleiche_werte_erzeugen_keine_abweichung() -> None:
    old = snapshot("ComfoBox", {"Zone/OperationMode": "Comfort", "Zone/Temperature": "21.5"})
    new = snapshot("ComfoBox2", {"Zone/OperationMode": "Comfort", "Zone/Temperature": "21.5"})

    report = compare(old, new, catalog())

    assert report.ok
    assert report.compared == 2


def test_enum_wird_exakt_verglichen() -> None:
    old = snapshot("ComfoBox", {"Zone/OperationMode": "Comfort"})
    new = snapshot("ComfoBox2", {"Zone/OperationMode": "comfort"})

    report = compare(old, new, catalog())

    assert not report.ok
    difference = report.differences[0]
    assert difference.topic == "Zone/OperationMode"
    assert difference.reason == REASON_VALUE
    assert difference.old == "Comfort"
    assert difference.new == "comfort"


def test_zahlen_duerfen_innerhalb_der_toleranz_liegen() -> None:
    old = snapshot("ComfoBox", {"Zone/Temperature": "21.5"})
    new = snapshot("ComfoBox2", {"Zone/Temperature": "21.505"})

    assert compare(old, new, catalog()).ok


def test_zahlen_ausserhalb_der_toleranz_sind_eine_abweichung() -> None:
    old = snapshot("ComfoBox", {"Zone/Temperature": "21.5"})
    new = snapshot("ComfoBox2", {"Zone/Temperature": "21.6"})

    report = compare(old, new, catalog())

    assert not report.ok
    assert report.differences[0].reason == REASON_VALUE
    assert "Toleranz" in report.differences[0].detail


def test_fehlender_wert_ist_eine_abweichung() -> None:
    old = snapshot("ComfoBox", {"Zone/Temperature": "21.5"})
    new = snapshot("ComfoBox2", {})

    report = compare(old, new, catalog())

    assert not report.ok
    assert report.differences[0].reason == REASON_MISSING_NEW


def test_zu_hoher_wert_ist_eine_abweichung() -> None:
    old = snapshot("ComfoBox", {"Zone/Temperature": "21.5"})
    new = snapshot("ComfoBox2", {"Zone/Temperature": "n/a"})

    report = compare(old, new, catalog())

    assert not report.ok
    assert report.differences[0].reason == REASON_NOT_A_NUMBER


def test_unterschiedliche_basis_topics_werden_vereinheitlicht() -> None:
    old = snapshot("ComfoBox", {"Zone/Temperature": "21.5"})
    new = snapshot("ComfoBox2", {"Zone/Temperature": "21.5"})

    report = compare(old, new, catalog())

    assert report.ok
    assert report.compared == 1


def test_topics_ausserhalb_des_katalogs_werden_ignoriert() -> None:
    old = snapshot("ComfoBox", {"Status/Uptime": "100", "Zone/Temperature": "21.5"})
    new = snapshot("ComfoBox2", {"Status/Uptime": "5", "Zone/Temperature": "21.5"})

    report = compare(old, new, catalog())

    assert report.ok


def test_unterschiedliche_zeitstempel_sind_eine_verfuegbarkeitsabweichung() -> None:
    old = snapshot("ComfoBox", {"Zone/Temperature": "21.5"}, timestamp="2026-11-01T10:00:00Z")
    new = snapshot("ComfoBox2", {"Zone/Temperature": "21.5"}, timestamp="2026-11-01T10:10:00Z")

    report = compare(old, new, catalog(), poll_interval=60.0, secondary_factor=1.0)

    assert not report.ok
    assert report.differences[0].reason == REASON_STALE


def test_zeitstempel_innerhalb_von_zwei_poll_zyklen_sind_ok() -> None:
    old = snapshot("ComfoBox", {"Zone/Temperature": "21.5"}, timestamp="2026-11-01T10:00:00Z")
    new = snapshot("ComfoBox2", {"Zone/Temperature": "21.5"}, timestamp="2026-11-01T10:01:30Z")

    report = compare(old, new, catalog(), poll_interval=60.0, secondary_factor=1.0)

    assert report.ok


def test_der_sekundaerzyklus_erlaubt_eine_groessere_differenz() -> None:
    # Sekundaere Items werden nur jeden Faktor-Zyklus gelesen - die beiden
    # Gateways lesen sie nicht im Gleichtakt.
    old = snapshot("ComfoBox", {"Zone/Temperature": "21.5"}, timestamp="2026-11-01T10:00:00Z")
    new = snapshot("ComfoBox2", {"Zone/Temperature": "21.5"}, timestamp="2026-11-01T10:06:40Z")

    ohne = compare(old, new, catalog(), poll_interval=60.0, secondary_factor=1.0)
    assert not ohne.ok

    mit = compare(old, new, catalog(), poll_interval=60.0, secondary_factor=5.0)
    assert mit.ok
    assert mit.notes


def test_der_wirkliche_katalog_wird_ohne_fehler_geladen() -> None:
    items = Catalog.load(TOPICS_JSON)

    assert len(items) == 127
    assert items.get("Zone/OperationMode") is not None
    assert items.get("Zone/OperationMode").kind == KIND_ENUM
    assert items.get("Zone/SetPointNormal").kind == KIND_NUMBER
