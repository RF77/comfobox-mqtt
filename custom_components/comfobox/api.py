"""API-Client für das ComfoBox-Gateway.

Der Client spricht ausschließlich mit dem Gateway – nie direkt mit dem BACnet-Bus.
Grund: das Gateway ist der einzige Master am MS/TP-Bus und hält den Schreibschutz.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

import aiohttp

from .const import API_PATH_ITEMS, API_PATH_RESTART, API_PATH_STATUS


class ComfoBoxError(Exception):
    """Basisfehler der Integration."""


class AuthenticationError(ComfoBoxError):
    """Der API-Token wurde vom Gateway abgelehnt."""


class ConnectionFailed(ComfoBoxError):
    """Das Gateway ist nicht erreichbar."""


@dataclass(slots=True)
class ComfoBoxItem:
    """Ein Item aus dem Katalog des Gateways."""

    id: str
    group: str
    name: str
    description: str | None
    value_type: str
    unit: str | None
    is_read_only: bool
    min_value: float | None
    max_value: float | None
    enum_values: list[str] = field(default_factory=list)
    value: Any = None
    last_updated: str | None = None
    is_stale: bool = False

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> ComfoBoxItem:
        """Baut ein Item aus einem API-Dict."""
        return cls(
            id=data["id"],
            group=data.get("group", ""),
            name=data.get("name", data["id"]),
            description=data.get("description"),
            value_type=data.get("valueType", "float"),
            unit=data.get("unit"),
            is_read_only=bool(data.get("isReadOnly", True)),
            min_value=data.get("min"),
            max_value=data.get("max"),
            enum_values=list(data.get("enumValues") or []),
            value=data.get("value"),
            last_updated=data.get("lastUpdated"),
            is_stale=bool(data.get("isStale", False)),
        )

    @property
    def display_name(self) -> str:
        """Anzeigename: Beschreibung bevorzugt, sonst Name."""
        return self.description or self.name

    @property
    def writable(self) -> bool:
        """Ist der Wert schreibbar?"""
        return not self.is_read_only

    @property
    def is_enum(self) -> bool:
        """Ist der Wert ein Enum?"""
        return self.value_type == "enum" or bool(self.enum_values)

    @property
    def is_boolean(self) -> bool:
        """Ist der Wert ein boolescher 0/1-Wert?"""
        return (
            self.value_type == "bool"
            and self.min_value is not None
            and self.max_value is not None
            and self.min_value == 0
            and self.max_value == 1
        )


@dataclass(slots=True)
class GatewayStatus:
    """Statusinformationen des Gateways."""

    device_id: str
    version: str
    bacnet_connected: bool
    mqtt_connected: bool
    write_count: int | None
    write_limit: int | None

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> GatewayStatus:
        """Baut den Status aus einem API-Dict."""
        writes = data.get("writes") or {}
        return cls(
            device_id=data.get("deviceId", "comfobox"),
            version=data.get("version", "unknown"),
            bacnet_connected=bool((data.get("bacnet") or {}).get("connected", False)),
            mqtt_connected=bool((data.get("mqtt") or {}).get("connected", False)),
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
        await self._request(
            "PUT", f"{API_PATH_ITEMS}/{item_id}", json={"value": value}
        )

    async def restart(self) -> None:
        """Startet das Gateway neu."""
        await self._request("POST", API_PATH_RESTART)
