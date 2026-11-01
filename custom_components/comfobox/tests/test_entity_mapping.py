"""Tests der Zuordnung von Items zu Home-Assistant-Plattformen.

Die Zuordnung ergibt sich **aus den Metadaten des Gateways** - die
Integration baut sie nicht selbst. Min/Max und die Enum-Namen kommen
unverändert aus dem Katalog (``/api/items``).
"""

from __future__ import annotations

import pytest
from homeassistant.helpers import device_registry as dr
from homeassistant.helpers import entity_registry as er

from custom_components.comfobox.const import MANUFACTURER, MODEL


@pytest.fixture
def mock_items():
    """Ein Katalog mit allen Item-Arten."""
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
        {
            "id": "warmwater_do_heat_water_now",
            "group": "WarmWater",
            "name": "DoHeatWaterNow",
            "description": "Warmwasser erhitzen",
            "valueType": "bool",
            "unit": None,
            "isReadOnly": False,
            "min": 0.0,
            "max": 1.0,
            "value": 0.0,
            "lastUpdated": "2026-10-31T10:00:00Z",
            "isStale": False,
        },
    ]


async def _setup(hass, mock_config_entry, mock_api):
    """Richtet die Integration ein und liefert die Entitäten."""
    mock_config_entry.add_to_hass(hass)

    assert await hass.config_entries.async_setup(mock_config_entry.entry_id)
    await hass.async_block_till_done()

    registry = er.async_get(hass)
    entries = er.async_entries_for_config_entry(registry, mock_config_entry.entry_id)

    return {entry.unique_id: entry for entry in entries}


async def test_ein_lesewert_wird_ein_sensor(hass, mock_config_entry, mock_api):
    """Ein schreibgeschützter Zahlenwert ist ein Sensor."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_states_heat_pump_operating_hours"]
    assert entry.domain == "sensor"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "12345.0"
    assert state.attributes["unit_of_measurement"] == "h"


async def test_ein_schreibbarer_zahlwert_wird_eine_number(
    hass, mock_config_entry, mock_api
):
    """Ein schreibbarer Zahlenwert ist eine Number."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_zone_setpoint_normal"]
    assert entry.domain == "number"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "21.5"

    # Min/Max kommen **unverändert** aus dem Katalog des Gateways.
    assert state.attributes["min"] == 5.0
    assert state.attributes["max"] == 35.0
    assert state.attributes["unit_of_measurement"] == "°C"


async def test_ein_schreibbares_enum_wird_ein_select(hass, mock_config_entry, mock_api):
    """Ein schreibbares Enum ist ein Select mit den exakten Enum-Namen."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_zone_operation_mode"]
    assert entry.domain == "select"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "Normal"
    assert list(state.attributes["options"]) == [
        "Normal",
        "Reduced",
        "FrostProtection",
    ]


async def test_ein_bool_wert_wird_ein_switch(hass, mock_config_entry, mock_api):
    """Ein schreibbarer 0/1-Wert ist ein Switch."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_warmwater_do_heat_water_now"]
    assert entry.domain == "switch"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "off"


async def test_der_status_liefert_zwei_binary_sensoren(
    hass, mock_config_entry, mock_api
):
    """Die Sonder-Entitäten kommen aus dem Gateway-Status."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    assert entries["comfobox1_bacnet_connected"].domain == "binary_sensor"
    assert entries["comfobox1_mqtt_connected"].domain == "binary_sensor"

    bacnet = hass.states.get(entries["comfobox1_bacnet_connected"].entity_id)
    mqtt = hass.states.get(entries["comfobox1_mqtt_connected"].entity_id)

    assert bacnet is not None and bacnet.state == "on"
    assert mqtt is not None and mqtt.state == "off"


async def test_es_gibt_genau_ein_geraet(hass, mock_config_entry, mock_api):
    """Alle Entitäten hängen an **einem** Gerät."""
    await _setup(hass, mock_config_entry, mock_api)

    registry = dr.async_get(hass)
    devices = dr.async_entries_for_config_entry(registry, mock_config_entry.entry_id)

    assert len(devices) == 1

    device = devices[0]
    assert device.manufacturer == MANUFACTURER
    assert device.model == MODEL
    assert device.sw_version == "1.0.0"
    assert device.name == "ComfoBox ELESTA"


async def test_die_unique_ids_sind_stabil_und_eindeutig(
    hass, mock_config_entry, mock_api
):
    """Die Unique-Id ist ``{deviceId}_{itemId}``."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    unique_ids = list(entries)

    assert len(unique_ids) == len(set(unique_ids))
    assert "comfobox1_zone_setpoint_normal" in unique_ids
    assert "comfobox1_zone_operation_mode" in unique_ids
    assert "comfobox1_states_heat_pump_operating_hours" in unique_ids
    assert "comfobox1_warmwater_do_heat_water_now" in unique_ids


async def test_ein_veralteter_wert_ist_nicht_verfuegbar(
    hass, mock_config_entry, mock_api
):
    """Ein veralteter Wert wird als ``unavailable`` gemeldet."""
    from unittest.mock import patch

    with patch(
        "custom_components.comfobox.api.ComfoBoxApiClient.get_items",
        _stale_items(),
    ):
        entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_states_heat_pump_operating_hours"]
    state = hass.states.get(entry.entity_id)

    assert state is not None
    assert state.state == "unavailable"


def _stale_items():
    """Ein Katalog, in dem alle Werte veraltet sind."""
    from unittest.mock import AsyncMock

    from .conftest import _items_obj

    items = _items_obj(
        [
            {
                "id": "states_heat_pump_operating_hours",
                "group": "States",
                "name": "HeatPumpOperatingHours",
                "description": "Betriebsstunden Wärmepumpe",
                "valueType": "float",
                "unit": "h",
                "isReadOnly": True,
                "value": 12345.0,
                "isStale": True,
            }
        ]
    )

    return AsyncMock(return_value=items)


async def test_ein_schreibvorgang_der_number_geht_ueber_den_gateway(
    hass, mock_config_entry, mock_api
):
    """Ein Number schreibt ueber ``PUT /api/items/{id}`` - der Schreibschutz
    liegt im Gateway, die Integration schreibt nie direkt auf den Bus."""
    from unittest.mock import AsyncMock, patch

    with patch(
        "custom_components.comfobox.api.ComfoBoxApiClient.write_item",
        AsyncMock(return_value=None),
    ) as schreiber:
        entries = await _setup(hass, mock_config_entry, mock_api)

        await hass.services.async_call(
            "number",
            "set_value",
            {
                "entity_id": entries["comfobox1_zone_setpoint_normal"].entity_id,
                "value": 22.5,
            },
            blocking=True,
        )

    schreiber.assert_awaited_once_with("zone_setpoint_normal", 22.5)


async def test_ein_schreibvorgang_des_select_behaelt_den_enum_namen(
    hass, mock_config_entry, mock_api
):
    """Der Enum-Name wird unveraendert uebergeben."""
    from unittest.mock import AsyncMock, patch

    with patch(
        "custom_components.comfobox.api.ComfoBoxApiClient.write_item",
        AsyncMock(return_value=None),
    ) as schreiber:
        entries = await _setup(hass, mock_config_entry, mock_api)

        await hass.services.async_call(
            "select",
            "select_option",
            {
                "entity_id": entries["comfobox1_zone_operation_mode"].entity_id,
                "option": "Reduced",
            },
            blocking=True,
        )

    schreiber.assert_awaited_once_with("zone_operation_mode", "Reduced")


async def test_ein_schreibvorgang_des_switch_schreibt_eine_und_null(
    hass, mock_config_entry, mock_api
):
    """Ein Schalter schreibt 1 oder 0."""
    from unittest.mock import AsyncMock, patch

    with patch(
        "custom_components.comfobox.api.ComfoBoxApiClient.write_item",
        AsyncMock(return_value=None),
    ) as schreiber:
        entries = await _setup(hass, mock_config_entry, mock_api)
        entity_id = entries["comfobox1_warmwater_do_heat_water_now"].entity_id

        await hass.services.async_call(
            "switch", "turn_on", {"entity_id": entity_id}, blocking=True
        )
        await hass.services.async_call(
            "switch", "turn_off", {"entity_id": entity_id}, blocking=True
        )

    assert schreiber.await_args_list[0].args == ("warmwater_do_heat_water_now", 1)
    assert schreiber.await_args_list[1].args == ("warmwater_do_heat_water_now", 0)


async def test_der_service_set_value_findet_die_item_id(
    hass, mock_config_entry, mock_api
):
    """Der Service ``comfobox.set_value`` schreibt auf die **Katalog-Id**.

    Die ``entity_id`` von Home Assistant lässt sich nicht in die Item-Id
    umrechnen - der Service muss das Entity-Registry befragen.
    """
    from unittest.mock import AsyncMock, patch

    with patch(
        "custom_components.comfobox.api.ComfoBoxApiClient.write_item",
        AsyncMock(return_value=None),
    ) as schreiber:
        entries = await _setup(hass, mock_config_entry, mock_api)

        await hass.services.async_call(
            "comfobox",
            "set_value",
            {
                "entity_id": entries["comfobox1_zone_setpoint_normal"].entity_id,
                "value": 22.5,
            },
            blocking=True,
        )

    schreiber.assert_awaited_once_with("zone_setpoint_normal", 22.5)


async def test_der_service_set_value_lehnt_fremde_entitaeten_ab(
    hass, mock_config_entry, mock_api
):
    """Eine Entität, die nicht zum Gateway gehört, wird abgelehnt."""
    from homeassistant.exceptions import HomeAssistantError
    from homeassistant.helpers import entity_registry as er

    await _setup(hass, mock_config_entry, mock_api)

    registry = er.async_get(hass)
    registry.async_get_or_create(
        "sensor",
        "comfobox",
        "fremde_entitaet",
        suggested_object_id="fremde_entitaet",
    )

    with pytest.raises(HomeAssistantError):
        await hass.services.async_call(
            "comfobox",
            "set_value",
            {"entity_id": "sensor.fremde_entitaet", "value": 1.0},
            blocking=True,
        )
