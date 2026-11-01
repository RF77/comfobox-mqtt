"""Tests des API-Clients der ComfoBox-Integration.

Der Client wird gegen einen **gemockten** HTTP-Server getestet
(``aioresponses``) - es wird **kein** echtes Gateway angesprochen.

Wichtig: der API-Token darf in **keiner** Fehlermeldung und in **keinem**
Log erscheinen.
"""

from __future__ import annotations

import aiohttp
import pytest
from aioresponses import aioresponses

from custom_components.comfobox.api import (
    AuthenticationError,
    ComfoBoxApiClient,
    ComfoBoxError,
    ConnectionFailed,
)

HOST = "http://comfobox.local:8099"
TOKEN = "cbk_0123456789abcdef_geheim"


@pytest.fixture
async def client():
    """Ein Client mit eigener Sitzung.

    Der Aufloeser wird ausdruecklich gesetzt: aiodns (der Standard unter
    Windows im HA-Stack) verlangt einen SelectorEventLoop - der
    ThreadedResolver funktioniert auf allen Plattformen.
    """
    connector = aiohttp.TCPConnector(resolver=aiohttp.ThreadedResolver())

    async with aiohttp.ClientSession(connector=connector) as session:
        yield ComfoBoxApiClient(session, HOST, TOKEN)


def _calls(mock) -> list:
    """Alle aufgezeichneten Anfragen."""
    return [call for calls in mock.requests.values() for call in calls]


async def test_get_status_liest_den_status(client):
    """Der Status wird aus der Antwort gelesen."""
    with aioresponses() as mock:
        mock.get(
            f"{HOST}/api/status",
            payload={
                "deviceId": "comfobox1",
                "version": "1.0.0",
                "bacnet": {"connected": True},
                "mqtt": {"connected": False},
                "writes": {"count": 17, "limit": 200},
            },
        )

        status = await client.get_status()

    assert status.device_id == "comfobox1"
    assert status.version == "1.0.0"
    assert status.bacnet_connected is True
    assert status.mqtt_connected is False
    assert status.write_count == 17
    assert status.write_limit == 200


async def test_get_items_liest_den_katalog(client):
    """Der Item-Katalog wird vollständig und unverändert übernommen."""
    with aioresponses() as mock:
        mock.get(
            f"{HOST}/api/items",
            payload=[
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
                    "description": None,
                    "valueType": "enum",
                    "unit": None,
                    "isReadOnly": False,
                    "enumValues": ["Normal", "Reduced"],
                    "value": "Normal",
                    "isStale": True,
                },
            ],
        )

        items = await client.get_items()

    assert len(items) == 2

    analog = items[0]
    assert analog.id == "zone_setpoint_normal"
    assert analog.group == "Zone"
    assert analog.name == "SetPointNormal"
    assert analog.display_name == "Raumsollwert Normal"
    assert analog.value_type == "float"
    assert analog.unit == "°C"
    assert analog.is_read_only is False
    assert analog.writable is True
    assert analog.min_value == 5.0
    assert analog.max_value == 35.0
    assert analog.value == 21.5
    assert analog.is_stale is False
    assert analog.is_enum is False

    enum = items[1]
    assert enum.display_name == "OperationMode"
    assert enum.enum_values == ["Normal", "Reduced"]
    assert enum.is_enum is True
    assert enum.is_stale is True


async def test_write_item_sendet_den_token_und_den_wert(client):
    """Der Schreibvorgang nutzt den Bearer-Token und den JSON-Body."""
    with aioresponses() as mock:
        mock.put(f"{HOST}/api/items/zone_setpoint_normal", status=204)

        await client.write_item("zone_setpoint_normal", 21.5)

    calls = _calls(mock)
    assert len(calls) == 1

    headers = calls[0].kwargs["headers"]
    assert headers["Authorization"] == f"Bearer {TOKEN}"
    assert calls[0].kwargs["json"] == {"value": 21.5}


async def test_restart_startet_den_gateway_neu(client):
    """Der Neustart wird als POST ausgeführt."""
    with aioresponses() as mock:
        mock.post(f"{HOST}/api/service/restart", status=202)

        await client.restart()

    assert len(_calls(mock)) == 1


@pytest.mark.parametrize("status", [401, 403])
async def test_ein_abgelehnter_token_ergibt_einen_authentifizierungsfehler(
    client, status
):
    """401 und 403 bedeuten: der Token ist falsch."""
    with aioresponses() as mock:
        mock.get(f"{HOST}/api/items", status=status)

        with pytest.raises(AuthenticationError):
            await client.get_items()


async def test_ein_serverfehler_ergibt_einen_gateway_fehler(client):
    """Ein Fehler des Gateways wird als ComfoBoxError gemeldet."""
    with aioresponses() as mock:
        mock.get(f"{HOST}/api/items", status=500, body="interner Fehler")

        with pytest.raises(ComfoBoxError) as err:
            await client.get_items()

    assert "500" in str(err.value)


async def test_ein_timeout_ergibt_einen_verbindungsfehler(client):
    """Ein Timeout wird als ConnectionFailed gemeldet."""
    with aioresponses() as mock:
        mock.get(f"{HOST}/api/items", exception=aiohttp.ServerTimeoutError())

        with pytest.raises(ConnectionFailed):
            await client.get_items()


async def test_eine_nicht_erreichbare_stelle_ergibt_einen_verbindungsfehler(client):
    """Das Gateway ist nicht erreichbar."""
    with aioresponses() as mock:
        mock.get(f"{HOST}/api/items", exception=aiohttp.ClientConnectionError())

        with pytest.raises(ConnectionFailed):
            await client.get_items()


async def test_der_token_erscheint_in_keiner_fehlermeldung(client):
    """Der Token darf niemals in einer Ausnahme stehen."""
    with aioresponses() as mock:
        mock.get(f"{HOST}/api/items", status=500, body="interner Fehler")

        with pytest.raises(ComfoBoxError) as err:
            await client.get_items()

    assert TOKEN not in str(err.value)

    with aioresponses() as mock:
        mock.get(f"{HOST}/api/items", exception=aiohttp.ClientConnectionError())

        with pytest.raises(ConnectionFailed) as err:
            await client.get_items()

    assert TOKEN not in str(err.value)
