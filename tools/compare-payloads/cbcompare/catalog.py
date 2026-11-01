"""Der Item-Katalog als Grundlage des Vergleichs.

Die Datei ``docs/topics.json`` ist die **eingefrorene Test-Fixture** des
Projekts: sie listet alle 127 MQTT-Topics des Altbestands mit ihrem
Werttyp. Genau diese Liste bestimmt, welche Topics verglichen werden -
nicht mehr und nicht weniger. Damit kann der Vergleich keine Topics
erfinden (Status-Topics, Discovery, ``/Set``) und keine auslassen.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

#: Ein benannter Wert (Enum) - der Name ist Teil des MQTT-Vertrags.
KIND_ENUM = "enum"

#: Ein Zahlenwert - wird mit einer Toleranz verglichen.
KIND_NUMBER = "number"

#: Ein Textwert - wird exakt verglichen.
KIND_TEXT = "text"


@dataclass(frozen=True)
class ItemSpec:
    """Ein Item des Katalogs."""

    #: Der Topic-Pfad ohne ``BaseTopic``, z. B. ``Zone/OperationMode``.
    topic_path: str

    #: Der Werttyp: ``enum``, ``number`` oder ``text``.
    kind: str

    #: Die Einheit (z. B. ``°C``) oder ``None``.
    unit: str | None = None

    #: Ist das Item nur lesbar?
    is_read_only: bool = False

    #: Die Untergrenze (nur bei Zahlenwerten).
    minimum: float | None = None

    #: Die Obergrenze (nur bei Zahlenwerten).
    maximum: float | None = None

    #: Der ursprüngliche Topic (mit ``BaseTopic``) - nur zur Anzeige.
    source_topic: str = ""


class Catalog:
    """Die Items, die verglichen werden."""

    def __init__(self, items: list[ItemSpec]) -> None:
        self._items = {item.topic_path: item for item in items}

    @property
    def items(self) -> list[ItemSpec]:
        """Alle Items (in Katalog-Reihenfolge)."""
        return list(self._items.values())

    def get(self, topic_path: str) -> ItemSpec | None:
        """Findet ein Item über seinen Topic-Pfad."""
        return self._items.get(topic_path)

    def __len__(self) -> int:
        return len(self._items)

    def __contains__(self, topic_path: object) -> bool:
        return topic_path in self._items

    @classmethod
    def load(cls, path: str | Path) -> Catalog:
        """Lädt den Katalog aus ``docs/topics.json``."""
        data = json.loads(Path(path).read_text(encoding="utf-8-sig"))
        if not isinstance(data, list):
            raise TypeError(f"{path}: erwartet wurde eine Liste von Items.")

        items: list[ItemSpec] = []
        for entry in data:
            if not isinstance(entry, dict):
                continue

            topic = str(entry.get("SubscribeTopic") or "")
            if not topic:
                continue

            items.append(
                ItemSpec(
                    topic_path=relative_path(topic),
                    kind=classify(str(entry.get("ValueType") or "")),
                    unit=entry.get("Unit"),
                    is_read_only=bool(entry.get("IsReadOnly")),
                    minimum=_as_float(entry.get("Min")),
                    maximum=_as_float(entry.get("Max")),
                    source_topic=topic,
                )
            )

        return cls(items)


def relative_path(topic: str) -> str:
    """Entfernt das ``BaseTopic`` (das erste Segment) vom Topic.

    ``ComfoBox/Zone/OperationMode`` wird zu ``Zone/OperationMode``,
    ``ComfoBox2/Zone/OperationMode`` genauso. So lassen sich die Topics
    zweier Gateways mit unterschiedlichem ``BaseTopic`` vergleichen.
    """
    if "/" in topic:
        return topic.split("/", 1)[1]
    return topic


def classify(value_type: str) -> str:
    """Bestimmt die Vergleichsregel aus dem Werttyp."""
    if ".Enums." in value_type:
        return KIND_ENUM
    if value_type.startswith(("float", "double", "decimal", "int", "short", "long")):
        return KIND_NUMBER
    return KIND_TEXT


def _as_float(value: Any) -> float | None:
    """Wandelt einen JSON-Wert in eine Zahl (oder ``None``)."""
    if value is None or isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    try:
        return float(str(value))
    except ValueError:
        return None
