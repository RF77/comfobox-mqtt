"""Die Vergleichsregeln.

Die Regeln stammen aus ``docs/06-aufgaben/phase-7.md`` (Aufgabe 7.2):

| Vergleich    | Regel                                             |
|--------------|---------------------------------------------------|
| Zahlen       | Betrag + Toleranz (0.01)                         |
| Enums        | **exakter** Name (keine Toleranz!)               |
| Verfügbarkeit| Zeitstempel-Differenz < 2 Poll-Zyklen            |
"""

from __future__ import annotations

from dataclasses import dataclass, field

from .catalog import KIND_ENUM, KIND_NUMBER, Catalog, relative_path
from .snapshot import Snapshot

#: Die Toleranz für Zahlenwerte.
DEFAULT_TOLERANCE = 0.01

#: Wie viele Poll-Zyklen dürfen zwischen zwei Veröffentlichungen liegen.
DEFAULT_MAX_SKEW_CYCLES = 2.0

#: Gründe für eine Abweichung.
REASON_VALUE = "Wert"
REASON_MISSING_OLD = "nur auf der neuen Seite"
REASON_MISSING_NEW = "nur auf der alten Seite"
REASON_NOT_A_NUMBER = "kein Zahlenwert"
REASON_STALE = "Verfügbarkeit"


@dataclass(frozen=True)
class Difference:
    """Eine Abweichung zwischen den beiden Seiten."""

    #: Der Topic-Pfad ohne ``BaseTopic``.
    topic: str

    #: Die Vergleichsregel (``enum``, ``number``, ``text``).
    kind: str

    #: Der Wert der alten Seite (oder ``None``).
    old: str | None

    #: Der Wert der neuen Seite (or ``None``).
    new: str | None

    #: Der Grund der Abweichung.
    reason: str

    #: Erläuterung (z. B. die Differenz bei Zahlen).
    detail: str = ""


@dataclass
class ComparisonReport:
    """Das Ergebnis des Vergleichs."""

    #: Wie viele Topics verglichen wurden.
    compared: int = 0

    #: Die Abweichungen.
    differences: list[Difference] = field(default_factory=list)

    #: Hinweise, die **keine** Abweichung sind.
    notes: list[str] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        """Gibt es Abweichungen?"""
        return not self.differences

    def as_dict(self) -> dict:
        """Der Report als JSON-taugliche Struktur."""
        return {
            "compared": self.compared,
            "differences": [
                {
                    "topic": d.topic,
                    "kind": d.kind,
                    "old": d.old,
                    "new": d.new,
                    "reason": d.reason,
                    "detail": d.detail,
                }
                for d in self.differences
            ],
            "notes": list(self.notes),
        }


def compare(
    old: Snapshot,
    new: Snapshot,
    catalog: Catalog,
    tolerance: float = DEFAULT_TOLERANCE,
    poll_interval: float = 60.0,
    max_skew_cycles: float = DEFAULT_MAX_SKEW_CYCLES,
    secondary_factor: float = 5.0,
) -> ComparisonReport:
    """Vergleicht zwei Snapshots.

    :param old: die Aufzeichnung der alten Seite
    :param new: die Aufzeichnung der neuen Seite
    :param catalog: die Items, die verglichen werden
    :param tolerance: die Toleranz für Zahlenwerte
    :param poll_interval: die Dauer eines Poll-Zyklus (Sekunden)
    :param max_skew_cycles: zulässige Zeitstempel-Differenz in Zyklen
    :param secondary_factor: wie viele Zyklen ein sekundäres Item wartet
    """
    report = ComparisonReport()

    old_values = _by_relative_path(old)
    new_values = _by_relative_path(new)

    # Sekundäre Items werden nur jeden ``SecondaryPollFactor``-Zyklus
    # gelesen. Das ist die **maßgebliche** Veröffentlichungsdauer - die
    # beiden Gateways lesen sie nicht im Gleichtakt.
    effective_interval = poll_interval * max(secondary_factor, 1.0)
    max_skew = effective_interval * max_skew_cycles

    for spec in catalog.items:
        old_value = old_values.get(spec.topic_path)
        new_value = new_values.get(spec.topic_path)

        if old_value is None and new_value is None:
            continue

        if old_value is None:
            report.differences.append(
                Difference(
                    topic=spec.topic_path,
                    kind=spec.kind,
                    old=None,
                    new=new_value.payload if new_value else None,
                    reason=REASON_MISSING_OLD,
                )
            )
            continue

        if new_value is None:
            report.differences.append(
                Difference(
                    topic=spec.topic_path,
                    kind=spec.kind,
                    old=old_value.payload,
                    new=None,
                    reason=REASON_MISSING_NEW,
                )
            )
            continue

        report.compared += 1

        difference = _compare_value(spec.topic_path, spec.kind, old_value.payload, new_value.payload, tolerance)
        if difference is not None:
            report.differences.append(difference)
            continue

        # Verfügbarkeit: die beiden Seiten dürfen nicht zu weit
        # auseinanderliegen - sonst hat eine Seite nicht mehr publiziert.
        skew = _skew(old_value, new_value)
        if skew is not None and skew > max_skew:
            report.differences.append(
                Difference(
                    topic=spec.topic_path,
                    kind=spec.kind,
                    old=old_value.payload,
                    new=new_value.payload,
                    reason=REASON_STALE,
                    detail=f"Zeitstempel-Differenz {skew:.0f} s (> {max_skew:.0f} s)",
                )
            )
        elif skew is not None and skew > effective_interval:
            # Kein Fehler - aber ein Hinweis: die Seiten publizieren nicht
            # im Gleichtakt (bei sekundaeren Items normal).
            report.notes.append(
                f"{spec.topic_path}: Zeitstempel-Differenz {skew:.0f} s "
                f"(ein Zyklus ist {effective_interval:.0f} s)."
            )

    return report


def _by_relative_path(snapshot: Snapshot) -> dict[str, object]:
    """Die Werte eines Snapshots, ohne ``BaseTopic``."""
    return {relative_path(topic): value for topic, value in snapshot.values.items()}


def _compare_value(topic: str, kind: str, old: str, new: str, tolerance: float) -> Difference | None:
    """Vergleicht zwei Payloads nach der Regel des Typs."""
    if kind == KIND_ENUM:
        # Der Name ist Teil des Vertrags: **keine** Toleranz.
        if old == new:
            return None
        return Difference(
            topic=topic,
            kind=kind,
            old=old,
            new=new,
            reason=REASON_VALUE,
            detail="Der Enum-Name weicht ab.",
        )

    if kind == KIND_NUMBER:
        old_number = _parse(old)
        new_number = _parse(new)

        if old_number is None or new_number is None:
            if old == new:
                return None
            return Difference(
                topic=topic,
                kind=kind,
                old=old,
                new=new,
                reason=REASON_NOT_A_NUMBER,
                detail="Mindestens eine Seite liefert keinen Zahlenwert.",
            )

        delta = abs(old_number - new_number)
        if delta <= tolerance:
            return None

        return Difference(
            topic=topic,
            kind=kind,
            old=old,
            new=new,
            reason=REASON_VALUE,
            detail=f"Differenz {delta:.4f} (> Toleranz {tolerance})",
        )

    if old == new:
        return None

    return Difference(
        topic=topic,
        kind=kind,
        old=old,
        new=new,
        reason=REASON_VALUE,
        detail="Die Payloads sind unterschiedlich.",
    )


def _parse(payload: str) -> float | None:
    """Liest einen Zahlenwert (kultur-invariant)."""
    try:
        return float(str(payload).strip())
    except (TypeError, ValueError):
        return None


def _skew(old_value, new_value) -> float | None:
    """Die Zeitstempel-Differenz in Sekunden (oder ``None``)."""
    old_moment = old_value.moment
    new_moment = new_value.moment
    if old_moment is None or new_moment is None:
        return None
    return abs((old_moment - new_moment).total_seconds())
