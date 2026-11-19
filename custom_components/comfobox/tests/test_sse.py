"""Tests des Push-Betriebs (Ereignis-Strom / Server-Sent Events).

Die Werte kommen nicht mehr im Poll-Takt vom Gateway: der Katalog wird beim
Start einmal gelesen, danach schickt der Gateway jede Änderung über
``GET /api/events``. Gepollt wird nur der Gateway-Status.
"""

from __future__ import annotations

import asyncio
from typing import Any

import aiohttp
import pytest
from aiohttp import web

from custom_components.comfobox.api import (
    ComfoBoxApiClient,
    EventsNotSupported,
)
from custom_components.comfobox.const import STATUS_KEY
from custom_components.comfobox.coordinator import ComfoBoxCoordinator

from .conftest import _items_obj, _status_obj

#: Wie lange auf ein gepushtes Ereignis gewartet wird.
TIMEOUT = 5


def _threaded_connector() -> aiohttp.TCPConnector:
    """Ein Connector ohne aiodns.

    ``aiohttp`` nutzt unter Windows standardmäßig ``aiodns``, das dort einen
    ``SelectorEventLoop`` verlangt. Die HA-Testumgebung läuft aber auf dem
    Proactor - derselbe Grund, aus dem ``tests/conftest.py`` den Resolver von
    Home Assistant ersetzt.
    """
    return aiohttp.TCPConnector(resolver=aiohttp.ThreadedResolver())


class PushClient:
    """Ein Client, dessen Ereignis-Strom der Test selbst befüllt.

    Die Warteschlange ist die Testnaht: ``None`` beendet den Strom so, wie es
    ein Gateway-Neustart täte.
    """

    def __init__(self, status: Any, items: list[Any]) -> None:
        """Initialisiert den Client."""
        self.status = status
        self.items = items
        self.events: asyncio.Queue[tuple[str, Any] | None] = asyncio.Queue()
        self.stream_calls = 0

    async def get_status(self) -> Any:
        """Der Gateway-Status."""
        return self.status

    async def get_items(self) -> list[Any]:
        """Der Item-Katalog."""
        return self.items

    async def write_item(self, item_id: str, value: Any) -> None:
        """Schreibt einen Wert (hier ohne Wirkung)."""

    async def restart(self) -> None:
        """Startet das Gateway neu (hier ohne Wirkung)."""

    async def stream_events(self):
        """Der Ereignis-Strom."""
        self.stream_calls += 1
        while True:
            entry = await self.events.get()
            if entry is None:
                return
            yield entry


async def _start(hass, client: PushClient, interval: int = 3600) -> ComfoBoxCoordinator:
    """Startet den Coordinator samt Push-Schleifen."""
    coordinator = ComfoBoxCoordinator(hass, client, interval)
    await coordinator.async_refresh()
    await coordinator.async_start_stream()
    return coordinator


async def test_der_snapshot_setzt_die_werte_ohne_polling(hass, mock_status, mock_items):
    """Der Snapshot des Stroms ersetzt die Werte aus dem Erststart."""
    client = PushClient(_status_obj(mock_status), _items_obj(mock_items))
    coordinator = await _start(hass, client)

    updated = asyncio.Event()
    coordinator.async_add_listener(updated.set)

    try:
        client.events.put_nowait(
            (
                "snapshot",
                [
                    {
                        "id": "zone_setpoint_normal",
                        "value": 22.5,
                        "timestamp": "2026-11-19T10:00:00+00:00",
                        "isStale": False,
                    },
                    {
                        "id": "zone_operation_mode",
                        "value": 1,
                        "valueName": "Reduced",
                        "timestamp": "2026-11-19T10:00:00+00:00",
                        "isStale": False,
                    },
                ],
            )
        )

        await asyncio.wait_for(updated.wait(), TIMEOUT)

        assert coordinator.sse_active is True
        assert coordinator.data["zone_setpoint_normal"].value == 22.5
        assert coordinator.data["zone_operation_mode"].value == 1
        assert coordinator.data["zone_operation_mode"].value_text == "Reduced"

        # Der Status gehört nicht zum Strom und bleibt stehen.
        assert coordinator.data[STATUS_KEY].device_id == "comfobox1"
    finally:
        await coordinator.async_stop_stream()


async def test_eine_einzelne_aenderung_wird_uebernommen(hass, mock_status, mock_items):
    """Ein ``value``-Ereignis ersetzt genau ein Item."""
    client = PushClient(_status_obj(mock_status), _items_obj(mock_items))
    coordinator = await _start(hass, client)

    updated = asyncio.Event()
    coordinator.async_add_listener(updated.set)

    try:
        client.events.put_nowait(
            (
                "value",
                {
                    "id": "states_heat_pump_operating_hours",
                    "value": 12346,
                    "timestamp": "2026-11-19T10:01:00+00:00",
                    "isStale": False,
                },
            )
        )

        await asyncio.wait_for(updated.wait(), TIMEOUT)

        assert coordinator.data["states_heat_pump_operating_hours"].value == 12346

        # Die übrigen Items behalten ihren Stand.
        assert coordinator.data["zone_setpoint_normal"].value == 21.5
    finally:
        await coordinator.async_stop_stream()


async def test_ein_veralteter_wert_wird_weitergereicht(hass, mock_status, mock_items):
    """Der Wechsel auf „veraltet“ kommt als eigenes Ereignis."""
    client = PushClient(_status_obj(mock_status), _items_obj(mock_items))
    coordinator = await _start(hass, client)

    updated = asyncio.Event()
    coordinator.async_add_listener(updated.set)

    try:
        client.events.put_nowait(
            (
                "value",
                {
                    "id": "zone_setpoint_normal",
                    "value": 21.5,
                    "timestamp": "2026-11-19T09:00:00+00:00",
                    "isStale": True,
                },
            )
        )

        await asyncio.wait_for(updated.wait(), TIMEOUT)

        assert coordinator.data["zone_setpoint_normal"].is_stale is True
    finally:
        await coordinator.async_stop_stream()


async def test_ein_unbekanntes_item_wird_uebergangen(hass, mock_status, mock_items):
    """Der Katalog ist die Wahrheit - abgeleitete Werte sind keine Entitäten."""
    client = PushClient(_status_obj(mock_status), _items_obj(mock_items))
    coordinator = await _start(hass, client)

    updated = asyncio.Event()
    coordinator.async_add_listener(updated.set)

    try:
        client.events.put_nowait(
            ("value", {"id": "derived.superheat", "value": 4.2, "isStale": False})
        )
        # Ein Ereignis zu einem bekannten Item zeigt, dass der Strom lief.
        client.events.put_nowait(
            (
                "value",
                {
                    "id": "zone_setpoint_normal",
                    "value": 21.5,
                    "timestamp": "2026-11-19T10:00:00+00:00",
                    "isStale": False,
                },
            )
        )

        await asyncio.wait_for(updated.wait(), TIMEOUT)

        assert "derived.superheat" not in coordinator.data
    finally:
        await coordinator.async_stop_stream()


async def test_der_status_wird_weiterhin_zyklisch_gelesen(
    hass, mock_status, mock_items
):
    """Health und Schreibzähler sind nicht Teil des Stroms."""
    client = PushClient(_status_obj(mock_status), _items_obj(mock_items))
    coordinator = await _start(hass, client, interval=1)

    updated = asyncio.Event()
    coordinator.async_add_listener(updated.set)

    try:
        client.status = _status_obj(
            {
                "deviceId": "comfobox1",
                "version": "1.1.0",
                "bacnet": {"connected": False},
                "mqtt": {"connected": True},
                "writes": {"count": 18, "limit": 200},
            }
        )

        await asyncio.wait_for(updated.wait(), TIMEOUT)

        assert coordinator.data[STATUS_KEY].version == "1.1.0"
        assert coordinator.data[STATUS_KEY].write_count == 18
    finally:
        await coordinator.async_stop_stream()


async def test_stop_beendet_den_strom(hass, mock_status, mock_items):
    """``async_stop_stream`` lässt keine Aufgabe zurück."""
    client = PushClient(_status_obj(mock_status), _items_obj(mock_items))
    coordinator = await _start(hass, client)

    client.events.put_nowait(("value", {"id": "zone_setpoint_normal", "value": 1}))
    await asyncio.sleep(0)
    await asyncio.sleep(0)

    await coordinator.async_stop_stream()

    assert coordinator.sse_active is False


async def test_der_client_liest_das_sse_format():
    """Der Client versteht Ereignisnamen, mehrzeilige Daten und Lebenszeichen."""
    app = web.Application()

    async def handler(request: web.Request) -> web.StreamResponse:
        response = web.StreamResponse(headers={"Content-Type": "text/event-stream"})
        await response.prepare(request)
        await response.write(b'event: snapshot\ndata: [{"id": "a", "value": 1}]\n\n')
        await response.write(b": ping\n\n")
        await response.write(b'event: value\ndata: {"id": "a",\n')
        await response.write(b'data: "value": 2}\n\n')
        await response.write_eof()
        return response

    app.router.add_get("/api/events", handler)

    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, "127.0.0.1", 0)
    await site.start()

    try:
        port = site._server.sockets[0].getsockname()[1]
        async with aiohttp.ClientSession(connector=_threaded_connector()) as session:
            client = ComfoBoxApiClient(session, f"http://127.0.0.1:{port}", "token")
            events = [event async for event in client.stream_events()]
    finally:
        await runner.cleanup()

    assert events == [
        ("snapshot", [{"id": "a", "value": 1}]),
        ("heartbeat", None),
        ("value", {"id": "a", "value": 2}),
    ]


async def test_ein_altes_gateway_meldet_sich_als_nicht_unterstuetzt():
    """Ohne ``/api/events`` fällt die Integration auf Polling zurück."""
    app = web.Application()

    async def handler(request: web.Request) -> web.Response:
        return web.Response(status=404, text="Not Found")

    app.router.add_get("/api/events", handler)

    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, "127.0.0.1", 0)
    await site.start()

    try:
        port = site._server.sockets[0].getsockname()[1]
        async with aiohttp.ClientSession(connector=_threaded_connector()) as session:
            client = ComfoBoxApiClient(session, f"http://127.0.0.1:{port}", "token")
            with pytest.raises(EventsNotSupported):
                async for _ in client.stream_events():
                    pass
    finally:
        await runner.cleanup()
