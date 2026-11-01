"""Die Aufzeichnung der MQTT-Payloads (Snapshot).

Ein Snapshot ist die Grundlage des *Golden-Master*-Ansatzes: die Payload des
alten Gateways werden einmal aufgezeichnet und dienen danach als Referenz
für den Vergleich - auch ohne Broker und ohne Gerät.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import UTC, datetime
from pathlib import Path


@dataclass(frozen=True)
class RecordedValue:
    """Ein aufgezeichneter Wert."""

    #: Die Payload (unverändert, so wie sie publiziert wurde).
    payload: str

    #: Der Zeitpunkt der Veröffentlichung (ISO 8601, UTC).
    timestamp: str

    @property
    def moment(self) -> datetime | None:
        """Der Zeitpunkt als ``datetime`` (oder ``None``)."""
        try:
            # Python 3.11+ versteht das "Z" selbst.
            return datetime.fromisoformat(self.timestamp)
        except ValueError:
            return None


@dataclass
class Snapshot:
    """Eine Sammlung aufgezeichneter Payloads."""

    #: Wann die Aufzeichnung abgeschlossen wurde (ISO 8601, UTC).
    recorded_at: str = field(default_factory=lambda: now_iso())

    #: Woher die Daten stammen (z. B. die MQTT-URI).
    source: str = ""

    #: Das ``BaseTopic``, unter dem aufgezeichnet wurde.
    base_topic: str = ""

    #: Wie lange aufgezeichnet wurde (Sekunden).
    duration_seconds: float = 0.0

    #: Die Werte: Topic → Wert.
    values: dict[str, RecordedValue] = field(default_factory=dict)

    def save(self, path: str | Path) -> None:
        """Schreibt den Snapshot als JSON."""
        payload = {
            "recordedAt": self.recorded_at,
            "source": self.source,
            "baseTopic": self.base_topic,
            "durationSeconds": self.duration_seconds,
            "values": {
                topic: {"payload": value.payload, "timestamp": value.timestamp}
                for topic, value in sorted(self.values.items())
            },
        }
        Path(path).write_text(
            json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )

    @classmethod
    def load(cls, path: str | Path) -> Snapshot:
        """Liest einen Snapshot aus einer JSON-Datei."""
        data = json.loads(Path(path).read_text(encoding="utf-8-sig"))

        values: dict[str, RecordedValue] = {}
        for topic, entry in (data.get("values") or {}).items():
            if isinstance(entry, dict):
                values[topic] = RecordedValue(
                    payload=str(entry.get("payload", "")),
                    timestamp=str(entry.get("timestamp", "")),
                )
            else:
                values[topic] = RecordedValue(payload=str(entry), timestamp="")

        return cls(
            recorded_at=str(data.get("recordedAt") or now_iso()),
            source=str(data.get("source") or ""),
            base_topic=str(data.get("baseTopic") or ""),
            duration_seconds=float(data.get("durationSeconds") or 0.0),
            values=values,
        )


def now_iso() -> str:
    """Der aktuelle Zeitpunkt als ISO-8601-String (UTC)."""
    return datetime.now(UTC).isoformat(timespec="seconds").replace("+00:00", "Z")
