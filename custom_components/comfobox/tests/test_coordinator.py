"""Tests des Coordinators der ComfoBox-Integration.

Der Coordinator liest beim Start Katalog und Status. Danach kommen die Werte
per Ereignis-Strom (SSE); gepollt wird nur noch der Gateway-Status. Bei einem
Fehler bleibt der letzte bekannte Stand stehen - die Entitäten flackern nicht.
"""

from __future__ import annotations

from unittest.mock import AsyncMock

from homeassistant.exceptions import ConfigEntryAuthFailed

from custom_components.comfobox.api import AuthenticationError, ConnectionFailed
from custom_components.comfobox.const import STATUS_KEY
from custom_components.comfobox.coordinator import ComfoBoxCoordinator

from .conftest import _items_obj, _status_obj


def _client(mock_status, mock_items) -> AsyncMock:
    """Ein Client, der Status und Items liefert."""
    client = AsyncMock()
    client.get_status = AsyncMock(return_value=_status_obj(mock_status))
    client.get_items = AsyncMock(return_value=_items_obj(mock_items))
    return client


async def test_der_normalfall_liefert_status_und_items(hass, mock_status, mock_items):
    """Status und Items werden beim Start gelesen."""
    coordinator = ComfoBoxCoordinator(hass, _client(mock_status, mock_items), 60)

    await coordinator.async_refresh()

    assert coordinator.last_update_success is True

    data = coordinator.data
    assert STATUS_KEY in data
    assert data[STATUS_KEY].device_id == "comfobox1"
    assert data[STATUS_KEY].write_count == 17

    assert "zone_setpoint_normal" in data
    assert "zone_operation_mode" in data
    assert "states_heat_pump_operating_hours" in data
    assert len(data) == 4  # drei Items + der reservierte Status


async def test_der_status_liegt_unter_dem_reservierten_schluessel(
    hass, mock_status, mock_items
):
    """Der Status darf mit keiner Item-Id kollidieren."""
    coordinator = ComfoBoxCoordinator(hass, _client(mock_status, mock_items), 60)

    await coordinator.async_refresh()

    assert coordinator.data[STATUS_KEY].version == "1.0.0"


async def test_ein_abgelehnter_token_startet_die_neuauthentifizierung(
    hass, mock_status, mock_items
):
    """Bei 401 fordert der Coordinator zur Neuauthentifizierung auf."""
    client = _client(mock_status, mock_items)
    client.get_status = AsyncMock(side_effect=AuthenticationError("Ungültiger Token"))

    coordinator = ComfoBoxCoordinator(hass, client, 60)

    await coordinator.async_refresh()

    assert coordinator.last_update_success is False
    assert isinstance(coordinator.last_exception, ConfigEntryAuthFailed)


async def test_ein_ausfall_wird_als_update_failed_gememeldet(
    hass, mock_status, mock_items
):
    """Ein nicht erreichbares Gateway ergibt UpdateFailed."""
    client = _client(mock_status, mock_items)
    client.get_items = AsyncMock(side_effect=ConnectionFailed("Zeitüberschreitung"))

    coordinator = ComfoBoxCoordinator(hass, client, 60)

    await coordinator.async_refresh()

    assert coordinator.last_update_success is False
    assert coordinator.last_exception is not None
    assert "Zeitüberschreitung" in str(coordinator.last_exception)


async def test_der_letzte_stand_bleibt_erhalten(hass, mock_status, mock_items):
    """Nach einem Ausfall bleibt der letzte bekannte Wert stehen."""
    client = _client(mock_status, mock_items)
    coordinator = ComfoBoxCoordinator(hass, client, 60)

    await coordinator.async_refresh()
    assert coordinator.last_update_success is True
    vorher = coordinator.data

    client.get_items = AsyncMock(side_effect=ConnectionFailed("Zeitüberschreitung"))
    await coordinator.async_refresh()

    assert coordinator.last_update_success is False
    assert coordinator.data == vorher


async def test_der_coordinator_plant_keine_eigenen_aktualisierungen(
    hass, mock_status, mock_items
):
    """Kein ``update_interval`` - sonst würde jeder Push die Statusabfrage verschieben.

    Der Status wird aus einer eigenen Schleife gelesen (``status_interval``).
    """
    coordinator = ComfoBoxCoordinator(hass, _client(mock_status, mock_items), 30)

    assert coordinator.update_interval is None
    assert coordinator.status_interval == 30


async def test_ein_ungueltiges_intervall_wird_begrenzt(hass, mock_status, mock_items):
    """Ein Intervall von 0 würde die Statusabfrage zur Endlosschleife machen."""
    coordinator = ComfoBoxCoordinator(hass, _client(mock_status, mock_items), 0)

    assert coordinator.status_interval == 1
