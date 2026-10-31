"""Gemeinsame Test-Fixtures der ComfoBox-Integration."""

from __future__ import annotations

import sys
from unittest.mock import AsyncMock, patch

import aiohttp
import pytest
import pytest_socket
from pytest_homeassistant_custom_component.common import MockConfigEntry

from custom_components.comfobox.const import DOMAIN


def _allow_socket_creation(*args: object, **kwargs: object) -> None:
    """Erlaubt die Socket-Erstellung, behaelt aber die Host-Beschraenkung.

    Home Assistants Test-Plugin ruft in ``pytest_runtest_setup``
    ``pytest_socket.disable_socket(allow_unix_socket=True)`` und sperrt damit
    jede Socket-Erstellung. Unter Linux nutzt asyncio fuer den Self-Pipe einen
    AF_UNIX-Socket (von HA erlaubt), unter **Windows** aber ein TCP-Socketpaar –
    ohne diese Freigabe scheitert bereits die Event-Loop-Erstellung mit
    ``SocketBlockedError``.

    Warum ein Patch statt eines Hooks: die Sperre muss **vor** dem Fixture-Aufbau
    aufgehoben sein. Der eingebaute ``pytest_runtest_setup`` von pytest baut die
    Fixtures auf (dort entsteht die Event-Loop) und laeuft als ``trylast``-Hook
    *nach* allen anderen. Ein ``hookwrapper`` im conftest laeuft damit ebenfalls
    zu spaet. Der Aufruf von HA erfolgt dagegen ueber das Modulattribut
    ``pytest_socket.disable_socket`` – dieses wird hier ersetzt.

    ``connect()`` bleibt auf ``127.0.0.1`` beschraenkt: die Tests erreichen das
    Internet nicht, HTTP wird ueber ``aioresponses`` gemockt.
    """
    pytest_socket.enable_socket()
    pytest_socket.socket_allow_hosts(["127.0.0.1"])


pytest_socket.disable_socket = _allow_socket_creation


@pytest.fixture(autouse=True)
def use_threaded_resolver(monkeypatch: pytest.MonkeyPatch) -> None:
    """Nutzt unter Windows den Thread-Resolver statt aiodns.

    Home Assistant setzt im TCP-Connector fest ``AsyncResolver`` (aiodns).
    aiodns verlangt unter Windows jedoch einen ``SelectorEventLoop`` – HA
    nutzt dort standardmaessig den Proactor. Der Aufruf von
    ``async_create_clientsession`` scheitert dann mit::

        RuntimeError: aiodns needs a SelectorEventLoop on Windows.

    Der ``ThreadedResolver`` loest die Namen im Thread auf und funktioniert
    auf allen Plattformen. Im **Produktivbetrieb** (Linux-Container) bleibt
    der aiodns-Resolver unveraendert – dieser Patch gilt nur den Tests.
    """
    if sys.platform != "win32":
        return

    import homeassistant.helpers.aiohttp_client as ha_client

    monkeypatch.setattr(ha_client, "AsyncResolver", aiohttp.ThreadedResolver)


@pytest.fixture(autouse=True)
def auto_enable_custom_integrations(enable_custom_integrations):
    """Aktiviert Custom-Integrationen fuer alle Tests."""
    return


@pytest.fixture
def mock_config_entry() -> MockConfigEntry:
    """Ein fertiger Config-Eintrag."""
    return MockConfigEntry(
        domain=DOMAIN,
        data={
            "host": "http://comfobox.local:8099",
            "token": "test-token",
            "verify_ssl": True,
        },
        options={"scan_interval": 10},
        title="ComfoBox (http://comfobox.local:8099)",
        unique_id="comfobox1",
    )


@pytest.fixture
def mock_status():
    """Ein simulierter Gateway-Status."""
    return {
        "deviceId": "comfobox1",
        "version": "1.0.0",
        "bacnet": {"connected": True},
        "mqtt": {"connected": False},
        "writes": {"count": 17, "limit": 200},
    }


@pytest.fixture
def mock_items():
    """Ein simulierter Item-Katalog."""
    return [
        {
            "id": "zone_setpoint_normal",
            "group": "Zone",
            "name": "SetPointNormal",
            "description": "Raumsollwert Normal",
            "valueType": "float",
            "unit": "°C",
            "isReadOnly": False,
            "min": 5.0,
            "max": 35.0,
            "value": 21.5,
            "lastUpdated": "2026-10-31T10:00:00Z",
            "isStale": False,
        },
        {
            "id": "zone_operation_mode",
            "group": "Zone",
            "name": "OperationMode",
            "description": "Betriebsart",
            "valueType": "enum",
            "unit": None,
            "isReadOnly": False,
            "enumValues": ["Normal", "Reduced", "FrostProtection"],
            "value": "Normal",
            "lastUpdated": "2026-10-31T10:00:00Z",
            "isStale": False,
        },
        {
            "id": "states_heat_pump_operating_hours",
            "group": "States",
            "name": "HeatPumpOperatingHours",
            "description": "Betriebsstunden Wärmepumpe",
            "valueType": "float",
            "unit": "h",
            "isReadOnly": True,
            "value": 12345.0,
            "lastUpdated": "2026-10-31T10:00:00Z",
            "isStale": False,
        },
    ]


@pytest.fixture
def mock_api(mock_status, mock_items):
    """Patcht den API-Client mit simulierten Antworten."""
    with patch(
        "custom_components.comfobox.api.ComfoBoxApiClient.get_status",
        AsyncMock(return_value=_status_obj(mock_status)),
    ), patch(
        "custom_components.comfobox.api.ComfoBoxApiClient.get_items",
        AsyncMock(return_value=_items_obj(mock_items)),
    ), patch(
        "custom_components.comfobox.api.ComfoBoxApiClient.write_item",
        AsyncMock(return_value=None),
    ):
        yield


def _status_obj(data):
    """Erzeugt ein GatewayStatus-Objekt aus einem Dict."""
    from custom_components.comfobox.api import GatewayStatus

    return GatewayStatus.from_dict(data)


def _items_obj(data):
    """Erzeugt Item-Objekte aus Dicts."""
    from custom_components.comfobox.api import ComfoBoxItem

    return [ComfoBoxItem.from_dict(entry) for entry in data]
