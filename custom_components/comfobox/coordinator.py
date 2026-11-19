"""DataUpdateCoordinator der ComfoBox-Integration."""

from __future__ import annotations

import asyncio
import logging
from typing import Any

from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant, callback
from homeassistant.exceptions import ConfigEntryAuthFailed
from homeassistant.helpers.update_coordinator import DataUpdateCoordinator, UpdateFailed

from .api import (
    AuthenticationError,
    ComfoBoxApiClient,
    ComfoBoxError,
    ComfoBoxItem,
    ConnectionFailed,
    EventsNotSupported,
)
from .const import (
    DOMAIN,
    SSE_FAILURES_BEFORE_UNAVAILABLE,
    SSE_RECONNECT_MAX,
    SSE_RECONNECT_MIN,
    STATUS_KEY,
)

_LOGGER = logging.getLogger(__name__)


class ComfoBoxCoordinator(DataUpdateCoordinator[dict[str, Any]]):
    """Hält den letzten Stand der Items und den Gateway-Status.

    Die Werte kommen per **Server-Sent Events** (``GET /api/events``): der
    Katalog wird beim Start **einmal** gelesen, danach schickt der Gateway nur
    noch die Änderungen. Nur der Gateway-Status (BACnet-/MQTT-Verbindung,
    Schreibzähler) wird zyklisch abgefragt - er ist nicht Teil des Streams.

    Fällt der Strom aus, wird mit Backoff neu verbunden; nach mehreren
    erfolglosen Versuchen gelten die Entitäten als nicht verfügbar. Kennt der
    Gateway den Strom gar nicht, wird auf zyklisches Abfragen zurückgefallen.
    """

    config_entry: ConfigEntry

    def __init__(
        self,
        hass: HomeAssistant,
        client: ComfoBoxApiClient,
        status_interval: int,
    ) -> None:
        """Initialisiert den Coordinator."""
        super().__init__(
            hass,
            _LOGGER,
            name=DOMAIN,
            # Kein Intervall: gepusht wird aus dem Ereignis-Strom, der Status
            # kommt aus einer eigenen Schleife (`async_start_stream`). Ein
            # `update_interval` würde von jeder Push-Nachricht neu gestartet
            # und die Statusabfrage damit dauerhaft verschieben.
            update_interval=None,
        )
        self._client = client
        self._status_interval = max(1, status_interval)
        self._items: dict[str, ComfoBoxItem] = {}
        self._tasks: list[asyncio.Task[None]] = []
        self._connect_attempts = 0
        self._failures = 0
        self._stream_active = False
        self._poll_fallback = False

    @property
    def client(self) -> ComfoBoxApiClient:
        """Der API-Client des Coordinators."""
        return self._client

    @property
    def status_interval(self) -> int:
        """Der Abstand der Statusabfrage in Sekunden."""
        return self._status_interval

    @property
    def sse_active(self) -> bool:
        """Ist der Ereignis-Strom verbunden?"""
        return self._stream_active

    async def _async_update_data(self) -> dict[str, Any]:
        """Liest Katalog und Status.

        Läuft beim Einrichten und - falls der Gateway keinen Ereignis-Strom
        kennt - im Poll-Rückfall. Bei einem Fehler bleibt der letzte bekannte
        Stand erhalten; Home Assistant meldet die Entitäten als nicht
        verfügbar.
        """
        try:
            status, items = await asyncio.gather(
                self._client.get_status(), self._client.get_items()
            )
        except AuthenticationError as err:
            raise ConfigEntryAuthFailed from err
        except ConnectionFailed as err:
            raise UpdateFailed(str(err)) from err

        self._items = {item.id: item for item in items}

        data: dict[str, Any] = dict(self._items)
        # Der Status liegt unter einem reservierten Schluessel, damit er nicht
        # mit einer Item-ID kollidiert.
        data[STATUS_KEY] = status
        return data

    async def async_start_stream(self) -> None:
        """Startet den Ereignis-Strom und die Statusabfrage."""
        self._tasks = [
            self.hass.async_create_background_task(
                self._async_stream_loop(), f"{DOMAIN}_events"
            ),
            self.hass.async_create_background_task(
                self._async_status_loop(), f"{DOMAIN}_status"
            ),
        ]

    async def async_stop_stream(self) -> None:
        """Beendet den Ereignis-Strom und die Statusabfrage."""
        tasks, self._tasks = self._tasks, []
        for task in tasks:
            task.cancel()
        if tasks:
            await asyncio.gather(*tasks, return_exceptions=True)
        self._stream_active = False

    async def _async_stream_loop(self) -> None:
        """Hält den Ereignis-Strom offen und verbindet mit Backoff neu."""
        backoff = SSE_RECONNECT_MIN

        while True:
            started = False
            try:
                await self._async_stream_once()
            except asyncio.CancelledError:
                raise
            except EventsNotSupported as err:
                _LOGGER.warning(
                    "Der Gateway kennt keinen Ereignis-Strom - die Werte werden "
                    "weiterhin zyklisch abgefragt (%s)",
                    err,
                )
                await self._async_poll_fallback()
                return
            except AuthenticationError:
                _LOGGER.warning(
                    "Der Gateway hat den API-Token für den Ereignis-Strom abgelehnt."
                )
                self._start_reauth()
                backoff = SSE_RECONNECT_MAX
            except ComfoBoxError as err:
                self._failures += 1
                _LOGGER.debug(
                    "Ereignis-Strom getrennt (Versuch %s): %s", self._failures, err
                )
                if self._failures >= SSE_FAILURES_BEFORE_UNAVAILABLE:
                    self.async_set_update_error(UpdateFailed(str(err)))
            finally:
                started = self._stream_active
                self._stream_active = False

            if started:
                # Die Verbindung hat getragen - der nächste Abbruch beginnt
                # wieder mit der kürzesten Wartezeit.
                backoff = SSE_RECONNECT_MIN

            await asyncio.sleep(backoff)
            backoff = min(backoff * 2, SSE_RECONNECT_MAX)

    async def _async_stream_once(self) -> None:
        """Liest einen Strom, bis er endet."""
        if self._connect_attempts > 0:
            # Nach einem Abbruch kann der Katalog veraltet sein (Gateway neu
            # gestartet, neues Item). Beim **ersten** Verbinden hat
            # `_async_update_data` ihn gerade gelesen.
            await self._async_reload_items()

        self._connect_attempts += 1

        async for event, payload in self._client.stream_events():
            self._stream_active = True
            self._failures = 0

            if event == "snapshot":
                self._apply_snapshot(payload)
            elif event in ("values", "value"):
                # `values` ist der Regelfall (der Gateway schickt die Werte eines
                # Auslesezyklus gesammelt); `value` bleibt für ältere Gateways.
                self._apply_values(payload)
            # `heartbeat` trägt keine Nutzlast - er hält nur die Verbindung.

    async def _async_reload_items(self) -> None:
        """Liest die Katalog-Metadaten neu (nach einem Abbruch)."""
        try:
            items = await self._client.get_items()
        except ComfoBoxError as err:
            _LOGGER.debug("Katalog konnte nicht neu geladen werden: %s", err)
            return

        self._items = {item.id: item for item in items}

    @callback
    def _apply_snapshot(self, payload: Any) -> None:
        """Übernimmt den vollständigen Stand aus dem Strom."""
        data: dict[str, Any] = {}
        if self.data and STATUS_KEY in self.data:
            data[STATUS_KEY] = self.data[STATUS_KEY]

        values = payload if isinstance(payload, list) else []
        for entry in values:
            item = self._item_from_event(entry)
            if item is not None:
                data[item.id] = item

        # Items ohne Wert (noch nie gelesen, Baugruppe fehlt) bleiben mit
        # ihrem Katalogstand stehen.
        for item_id, item in self._items.items():
            data.setdefault(item_id, item)

        self.async_set_updated_data(data)

    @callback
    def _apply_values(self, payload: Any) -> None:
        """Übernimmt einzelne Wertänderungen."""
        entries = payload if isinstance(payload, list) else [payload]

        data = dict(self.data or {})
        changed = False

        for entry in entries:
            item = self._item_from_event(entry)
            if item is None:
                continue
            data[item.id] = item
            changed = True

        if changed:
            self.async_set_updated_data(data)

    def _item_from_event(self, entry: Any) -> ComfoBoxItem | None:
        """Baut ein Item aus einem Ereignis des Streams.

        Der Katalog ist die Wahrheit: eine Id, die dort nicht steht (etwa ein
        abgeleiteter Wert), wird übergangen.
        """
        if not isinstance(entry, dict):
            return None

        item_id = entry.get("id")
        item = self._items.get(item_id) if isinstance(item_id, str) else None
        if item is None:
            return None

        return item.with_value(
            value=entry.get("value"),
            value_text=entry.get("valueName"),
            last_updated=entry.get("timestamp"),
            is_stale=bool(entry.get("isStale", False)),
        )

    async def _async_status_loop(self) -> None:
        """Fragt den Gateway-Status zyklisch ab (nicht Teil des Streams)."""
        while not self._poll_fallback:
            await asyncio.sleep(self._status_interval)

            try:
                status = await self._client.get_status()
            except AuthenticationError:
                _LOGGER.warning("Der Gateway hat den API-Token abgelehnt.")
                self._start_reauth()
                continue
            except ComfoBoxError as err:
                # Ob der Gateway erreichbar ist, entscheidet der Strom -
                # hier genügt die Diagnose.
                _LOGGER.debug("Statusabfrage fehlgeschlagen: %s", err)
                continue

            self._apply_status(status)

    @callback
    def _apply_status(self, status: Any) -> None:
        """Setzt den Status, ohne den Zustand des Stroms zu überschreiben."""
        data = dict(self.data or {})
        data[STATUS_KEY] = status

        # Bewusst nicht `async_set_updated_data`: das würde einen gemeldeten
        # Ausfall des Stroms sofort wieder als „erfolgreich“ markieren, nur
        # weil `/api/status` antwortet.
        self.data = data
        self.async_update_listeners()

    async def _async_poll_fallback(self) -> None:
        """Fragt Katalog und Status zyklisch ab (Gateway ohne Ereignis-Strom)."""
        self._poll_fallback = True

        while self._poll_fallback:
            await asyncio.sleep(self._status_interval)
            await self.async_refresh()

    @callback
    def _start_reauth(self) -> None:
        """Fordert einen neuen Token an."""
        if self.config_entry is not None:
            self.config_entry.async_start_reauth(self.hass)
