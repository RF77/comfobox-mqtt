"""API-Client für das ComfoBox-Gateway.

Der Client spricht ausschließlich mit dem Gateway – nie direkt mit dem BACnet-Bus.
Grund: das Gateway ist der einzige Master am MS/TP-Bus und hält den Schreibschutz.
"""

from __future__ import annotations

import json
from collections.abc import AsyncIterator
from dataclasses import dataclass, field, replace
from typing import Any

import aiohttp

from .const import (
    API_PATH_EVENTS,
    API_PATH_ITEMS,
    API_PATH_RESTART,
    API_PATH_STATUS,
    SSE_READ_TIMEOUT,
)


class ComfoBoxError(Exception):
    """Basisfehler der Integration."""


class AuthenticationError(ComfoBoxError):
    """Der API-Token wurde vom Gateway abgelehnt."""


class ConnectionFailed(ComfoBoxError):
    """Das Gateway ist nicht erreichbar."""


class EventsNotSupported(ComfoBoxError):
    """Das Gateway kennt den Ereignis-Strom nicht (zu alt)."""


@dataclass(slots=True)
class ComfoBoxItem:
    """Ein Item aus dem Katalog des Gateways.

    Die Feldnamen folgen dem REST-Vertrag des Gateways
    (``src/ComfoBox.Gateway/Api/Models/ItemDto.cs``): ``kind``, ``role``,
    ``writable``, ``stale``, ``enumValues`` und ``valueLabels``.

    ``kind`` sagt, **wie** der Wert kodiert ist; ``role`` sagt, **was** er
    bedeutet. Die Rolle entscheidet über die Home-Assistant-Plattform und
    damit auch darüber, ob der Wert eine Langzeitstatistik erzeugt.
    """

    id: str
    group: str
    name: str
    display_name: str
    description: str | None
    kind: str
    role: str
    unit: str | None
    writable: bool
    min_value: float | None
    max_value: float | None
    priority: str = "secondary"
    requires_expert_mode: bool = False
    enum_values: dict[int, str] = field(default_factory=dict)
    allowed_values: list[float] = field(default_factory=list)
    value_labels: dict[int, str] = field(default_factory=dict)
    value: Any = None
    value_text: str | None = None
    last_updated: str | None = None
    is_stale: bool = False

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> ComfoBoxItem:
        """Baut ein Item aus einem API-Dict."""
        description = data.get("description")
        display_name = (data.get("displayName") or "").strip()

        if not display_name:
            # Aeltere Gateways liefern kein `displayName` - dann dient die
            # erste Zeile der Beschreibung als Name.
            first_line = (description or "").strip().splitlines()
            display_name = first_line[0] if first_line else data.get("name", data["id"])

        kind = data.get("kind") or "analog"
        min_value = data.get("min")
        max_value = data.get("max")
        enum_values = {
            int(number): name for number, name in (data.get("enumValues") or {}).items()
        }

        return cls(
            id=data["id"],
            group=data.get("group", ""),
            name=data.get("name", data["id"]),
            display_name=display_name,
            description=description,
            kind=kind,
            role=data.get("role")
            or _legacy_role(kind, min_value, max_value, data.get("writable", False)),
            unit=data.get("unit"),
            writable=bool(data.get("writable", False)),
            min_value=min_value,
            max_value=max_value,
            priority=data.get("priority") or "secondary",
            requires_expert_mode=bool(data.get("requiresExpertMode", False)),
            enum_values=enum_values,
            allowed_values=[float(v) for v in (data.get("allowedValues") or [])],
            value_labels={
                int(number): label
                for number, label in (data.get("valueLabels") or {}).items()
            },
            value=data.get("value"),
            value_text=data.get("valueText"),
            last_updated=data.get("lastUpdated"),
            is_stale=bool(data.get("stale", False)),
        )

    @property
    def is_read_only(self) -> bool:
        """Ist der Wert (in dieser Betriebsart) nicht schreibbar?"""
        return not self.writable

    @property
    def is_enum(self) -> bool:
        """Ist der Wert ein Enum?"""
        return self.kind == "enum" or bool(self.enum_values)

    @property
    def is_boolean(self) -> bool:
        """Ist der Wert ein boolescher 0/1-Wert?"""
        return self.role == "flag"

    @property
    def is_selectable(self) -> bool:
        """Hat der Wert eine feste Auswahlliste (Enum oder Kodewert)?"""
        return self.role in ("choice", "code") and bool(self.options)

    @property
    def platform(self) -> str | None:
        """Die Home-Assistant-Plattform dieses Items (oder ``None``).

        Genau **eine** Plattform je Item - sonst entstehen doppelte Entitäten
        (bisher wurde jeder 0/1-Wert als `number` *und* als `switch` angelegt).
        """
        role = self.role

        if role == "date":
            # Befund A20: der Altbestand interpretiert diese Werte nicht.
            return None
        if role == "command":
            return "button" if self.writable else None
        if role in ("choice", "code"):
            return "select" if self.writable and self.options else "sensor"
        if role == "flag":
            return "switch" if self.writable else "binary_sensor"
        if role == "setpoint":
            return "number" if self.writable else "sensor"

        # measurement, counter und info sind Sensoren.
        return "sensor"

    @property
    def enum_names(self) -> list[str]:
        """Die Enum-Namen in der Reihenfolge ihrer Zahlen.

        Die Namen sind Teil des MQTT-Vertrags und werden **unveraendert**
        uebernommen - nicht uebersetzt und nicht umbenannt.
        """
        return [self.enum_values[number] for number in sorted(self.enum_values)]

    @property
    def enum_name(self) -> str | None:
        """Der Name des aktuellen Enum-Werts (oder ``None``)."""
        if self.value_text and self.kind == "enum":
            return self.value_text
        number = self.number
        if number is None:
            return None
        return self.enum_values.get(number)

    @property
    def number(self) -> int | None:
        """Der aktuelle Wert als ganze Zahl (oder ``None``)."""
        if self.value is None:
            return None
        try:
            return int(float(self.value))
        except (TypeError, ValueError):
            return None

    @property
    def code_numbers(self) -> list[int]:
        """Die erlaubten Zahlen eines Kodewerts.

        Die Werteliste des Katalogs ist genauer als der Bereich (`Cooling`
        erlaubt 0, 1 und 3 - nicht 2). Fehlt sie, dient ein kleiner
        ganzzahliger Bereich als Liste.
        """
        if self.allowed_values:
            return [int(v) for v in self.allowed_values]
        if (
            self.min_value is not None
            and self.max_value is not None
            and self.min_value == 0
            and 1 < self.max_value <= 32
        ):
            return list(range(int(self.max_value) + 1))
        return []

    @property
    def options(self) -> list[str]:
        """Die Auswahlwerte fuer Select und Enum-Sensoren.

        Bei einem Enum sind das die **Vertragsnamen**, bei einem Kodewert die
        Beschriftung aus der Beschreibung. Fehlt eine Beschriftung oder ist sie
        mehrdeutig, bleiben es die Zahlen - Home Assistant verlangt eindeutige
        Optionen, sonst schreibt die Auswahl den falschen Wert.
        """
        if self.role == "choice":
            return self.enum_names

        labels = self._code_labels
        if labels is not None:
            return labels

        return [str(number) for number in self.code_numbers]

    @property
    def _code_labels(self) -> list[str] | None:
        """Die Beschriftungen der Kodewerte - oder ``None``.

        ``None`` heisst: keine, unvollstaendig oder mehrdeutig. Dann sind die
        Zahlen die bessere Auswahl.
        """
        if self.role != "code":
            return None

        numbers = self.code_numbers
        if not numbers:
            return None

        labels = [self.value_labels.get(number) for number in numbers]
        if any(label is None for label in labels):
            return None

        texts = [str(label) for label in labels]
        if len(set(texts)) != len(texts):
            return None

        return texts

    @property
    def state_text(self) -> str | None:
        """Der aktuelle Wert als Text - oder ``None`` fuer eine Zahl.

        ``None`` heisst: die Entitaet zeigt den Zahlenwert (Messgroesse,
        Zaehler, Reglerangabe).
        """
        if self.role == "choice":
            return self.enum_name

        if self.role != "code":
            return None

        number = self.number
        if number is None:
            return None

        labels = self._code_labels
        if labels is not None:
            for value, text in zip(self.code_numbers, labels):
                if value == number:
                    return text

        # Ein Wert ausserhalb der Liste bleibt sichtbar - auch wenn er in
        # Home Assistant nicht als Option gilt.
        return str(number)

    def label_for(self, number: int) -> str:
        """Die Beschriftung eines Kodewerts (Rueckfall: die Zahl)."""
        return self.value_labels.get(number, str(number))

    def value_for_option(self, option: str) -> float | None:
        """Der zu schreibende Wert zu einer Auswahl (oder ``None``)."""
        if self.role == "choice":
            for number, name in self.enum_values.items():
                if name == option:
                    return float(number)
            return None

        for number, text in zip(self.code_numbers, self.options):
            if text == option:
                return float(number)
        return None

    def with_value(
        self,
        value: Any,
        value_text: str | None = None,
        last_updated: str | None = None,
        is_stale: bool = False,
    ) -> ComfoBoxItem:
        """Gibt eine Kopie mit neuem Wert zurück.

        Die Metadaten (Name, Einheit, Enum-Werte, Grenzen) bleiben unverändert -
        sie kommen aus dem Katalog und ändern sich im Betrieb nicht.
        """
        return replace(
            self,
            value=value,
            value_text=value_text,
            last_updated=last_updated,
            is_stale=is_stale,
        )


@dataclass(slots=True)
class GatewayStatus:
    """Statusinformationen des Gateways."""

    device_id: str
    version: str
    bacnet_connected: bool
    mqtt_connected: bool
    mqtt_enabled: bool | None
    write_count: int | None
    write_limit: int | None

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> GatewayStatus:
        """Baut den Status aus einem API-Dict."""
        writes = data.get("writes") or {}
        mqtt = data.get("mqtt") or {}
        return cls(
            device_id=data.get("deviceId", "comfobox"),
            version=data.get("version", "unknown"),
            bacnet_connected=bool((data.get("bacnet") or {}).get("connected", False)),
            mqtt_connected=bool(mqtt.get("connected", False)),
            # Aeltere Gateways kennen das Feld nicht - `None` heisst "unbekannt".
            mqtt_enabled=mqtt.get("enabled"),
            write_count=writes.get("count"),
            write_limit=writes.get("limit"),
        )


class ComfoBoxApiClient:
    """Dünner REST-Client für das ComfoBox-Gateway."""

    def __init__(
        self,
        session: aiohttp.ClientSession,
        host: str,
        token: str,
        timeout: int = 10,
    ) -> None:
        """Initialisiert den Client."""
        self._session = session
        self._host = host.rstrip("/")
        self._token = token
        self._timeout = aiohttp.ClientTimeout(total=timeout)

    def _headers(self) -> dict[str, str]:
        """Erzeugt die Request-Header inkl. Bearer-Token."""
        return {
            "Authorization": f"Bearer {self._token}",
            "Accept": "application/json",
        }

    async def _request(self, method: str, path: str, **kwargs: Any) -> Any:
        """Führt einen Request aus und übersetzt die Fehler."""
        url = f"{self._host}{path}"
        try:
            async with self._session.request(
                method, url, headers=self._headers(), timeout=self._timeout, **kwargs
            ) as resp:
                if resp.status in (401, 403):
                    raise AuthenticationError("Ungültiger API-Token")
                if resp.status >= 400:
                    # Der Text des Gateways ist bereits menschenlesbar.
                    detail = await resp.text()
                    raise ComfoBoxError(
                        f"Gateway antwortete mit {resp.status}: {detail[:200]}"
                    )
                if resp.status == 204 or resp.content_type != "application/json":
                    return None
                return await resp.json()
        except (TimeoutError, aiohttp.ClientError) as err:
            raise ConnectionFailed(f"Gateway nicht erreichbar: {err}") from err

    async def get_status(self) -> GatewayStatus:
        """Liest den Gateway-Status."""
        data = await self._request("GET", API_PATH_STATUS)
        return GatewayStatus.from_dict(data or {})

    async def get_items(self) -> list[ComfoBoxItem]:
        """Liest den vollständigen Item-Katalog mit aktuellen Werten."""
        data = await self._request("GET", API_PATH_ITEMS)
        return [ComfoBoxItem.from_dict(entry) for entry in (data or [])]

    async def write_item(self, item_id: str, value: Any) -> None:
        """Schreibt einen Wert. Der Schreibschutz liegt im Gateway."""
        await self._request("PUT", f"{API_PATH_ITEMS}/{item_id}", json={"value": value})

    async def restart(self) -> None:
        """Startet das Gateway neu."""
        await self._request("POST", API_PATH_RESTART)

    async def stream_events(self) -> AsyncIterator[tuple[str, Any]]:
        """Liest den Ereignis-Strom des Gateways (Server-Sent Events).

        Liefert ``(event, payload)``-Paare:

        ``snapshot``
            Alle aktuell bekannten Werte - einmalig nach dem Verbinden.
        ``values``
            Die geänderten Werte eines Auslesezyklus (Array). Der Gateway
            schickt sie gesammelt, weil ein Zyklus Dutzende Werte liefert.
        ``value``
            Ein einzelner Wert - nur ältere Gateways senden so.
        ``heartbeat``
            Ein Lebenszeichen (``: ping``), Nutzlast ``None``.

        Der Aufruf kehrt erst zurück, wenn die Verbindung endet - entweder,
        weil das Gateway sie schließt, oder weil der Aufrufer abbricht.

        Raises:
            AuthenticationError: Der Token wurde abgelehnt.
            EventsNotSupported: Das Gateway kennt ``/api/events`` nicht.
            ConnectionFailed: Das Gateway ist nicht erreichbar.
        """
        url = f"{self._host}{API_PATH_EVENTS}"

        # Kein Gesamt-Timeout: der Strom ist dazu gedacht, offen zu bleiben.
        # `sock_read` greift nur, wenn **nichts** mehr kommt - der Gateway
        # schickt alle 30 s ein Lebenszeichen.
        timeout = aiohttp.ClientTimeout(
            total=None,
            connect=self._timeout.total or 10,
            sock_read=SSE_READ_TIMEOUT,
        )

        try:
            async with self._session.get(
                url, headers=self._headers(), timeout=timeout
            ) as resp:
                if resp.status in (401, 403):
                    raise AuthenticationError("Ungültiger API-Token")
                if resp.status == 404:
                    raise EventsNotSupported(
                        "Das Gateway kennt den Ereignis-Strom /api/events nicht."
                    )
                if resp.status >= 400:
                    detail = await resp.text()
                    raise ComfoBoxError(
                        f"Gateway antwortete mit {resp.status}: {detail[:200]}"
                    )

                event = "message"
                data: list[str] = []

                async for raw in resp.content:
                    line = raw.decode("utf-8", errors="replace").rstrip("\r\n")

                    if not line:
                        # Eine Leerzeile schließt das Ereignis ab.
                        if data:
                            yield event, _parse_event(event, data)
                        event, data = "message", []
                        continue

                    if line.startswith(":"):
                        # Kommentarzeile - der Gateway nutzt sie als Lebenszeichen.
                        yield "heartbeat", None
                        continue

                    field, _, value = line.partition(":")
                    value = value.removeprefix(" ")

                    if field == "event":
                        event = value
                    elif field == "data":
                        data.append(value)
        except (TimeoutError, aiohttp.ClientError) as err:
            raise ConnectionFailed(f"Gateway nicht erreichbar: {err}") from err


def _legacy_role(kind: str, min_value: Any, max_value: Any, writable: Any) -> str:
    """Leitet die Rolle fuer Gateways ohne ``role``-Feld ab.

    Nur ein Rueckfall: seit 1.3.0 liefert das Gateway die Rolle selbst.
    """
    if kind == "enum":
        return "choice"
    if kind == "date":
        return "date"
    if min_value == 0 and max_value == 1:
        return "flag"
    return "setpoint" if writable else "measurement"


def _parse_event(event: str, data: list[str]) -> Any:
    """Wandelt die Nutzlast eines Ereignisses in JSON um."""
    try:
        return json.loads("\n".join(data))
    except ValueError as err:
        raise ComfoBoxError(
            f"Ungültige Nutzlast im Ereignis '{event}' des Gateways."
        ) from err
