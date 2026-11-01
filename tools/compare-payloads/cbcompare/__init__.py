"""Vergleich der MQTT-Payloads zweier Gateway-Instanzen.

Das Paket bildet den Kern des Werkzeugs ``tools/compare-payloads/`` aus
(Aufgabe 7.2). Es kennt bewusst **keinen** MQTT-Client - die Daten kommen
entweder live vom Broker oder aus einer Aufzeichnung (``Snapshot``). Damit
ist der Vergleich ohne Broker testbar.
"""

from .catalog import KIND_ENUM, KIND_NUMBER, KIND_TEXT, Catalog, ItemSpec
from .compare import ComparisonReport, Difference, compare
from .snapshot import RecordedValue, Snapshot

__all__ = [
    "KIND_ENUM",
    "KIND_NUMBER",
    "KIND_TEXT",
    "Catalog",
    "ComparisonReport",
    "Difference",
    "ItemSpec",
    "RecordedValue",
    "Snapshot",
    "compare",
]
