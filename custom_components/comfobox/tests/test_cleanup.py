"""Tests für das Aufräumen veralteter Entitäten.

Nach einem Update bleiben Entitäten übrig, die es nicht mehr gibt: der Wert ist
entfallen, oder er liegt jetzt unter einer anderen Plattform (`number` →
`switch`). Home Assistant lässt sie stehen – samt Aufzeichnungen. Die
Integration meldet sie als Reparatur-Hinweis und räumt sie auf Bestätigung auf.
"""

from __future__ import annotations

from typing import Any
from unittest.mock import AsyncMock, patch

import pytest
from homeassistant.helpers import entity_registry as er
from homeassistant.helpers import issue_registry as ir

from custom_components.comfobox.cleanup import (
    async_check_legacy_entities,
    async_remove_legacy_entities,
    legacy_entities,
)
from custom_components.comfobox.const import DOMAIN

from .test_entity_mapping import _item


@pytest.fixture
def mock_items() -> list[dict[str, Any]]:
    """Ein Katalog mit je einer Entität pro Plattform."""
    return [
        _item(
            "zone.operation_mode",
            "Zone",
            "OperationMode",
            "enum",
            "choice",
            writable=True,
            enum_values={"0": "Normal", "1": "Reduced"},
            value=0,
            value_text="Normal",
        ),
        _item(
            "config.general.sommerknick",
            "Config/General",
            "Sommerknick",
            "analog",
            "flag",
            writable=True,
            min=0.0,
            max=1.0,
            value=0,
        ),
        _item(
            "states.controller.controller_type",
            "States/Controller",
            "ControllerType",
            "analog",
            "info",
            priority="once",
            value=42,
        ),
        # Toter Datumswert: bekommt heute keine Entität mehr, früher schon.
        _item(
            "states.controller.initial_operation_date",
            "States/Controller",
            "InitialOperationDate",
            "date",
            "date",
            priority="once",
        ),
    ]


async def _setup(hass, config_entry, api) -> er.EntityRegistry:
    """Richtet die Integration ein."""
    config_entry.add_to_hass(hass)
    assert await hass.config_entries.async_setup(config_entry.entry_id)
    await hass.async_block_till_done()

    return er.async_get(hass)


def _add_legacy(hass, config_entry, entity_id: str, unique_id: str) -> str:
    """Legt einen Eintrag an, als hätte ihn eine frühere Version erzeugt."""
    registry = er.async_get(hass)

    return registry.async_get_or_create(
        domain=entity_id.split(".")[0],
        platform=DOMAIN,
        unique_id=unique_id,
        suggested_object_id=entity_id.split(".")[1],
        config_entry=config_entry,
    ).entity_id


async def test_ohne_alte_entitaeten_gibt_es_keinen_hinweis(
    hass, mock_config_entry, mock_api
):
    """Eine frische Installation erzeugt keine Reparatur."""
    await _setup(hass, mock_config_entry, mock_api)

    assert (
        ir.async_get(hass).async_get_issue(
            DOMAIN, f"legacy_entities_{mock_config_entry.entry_id}"
        )
        is None
    )


async def test_eine_entfallene_plattform_wird_erkannt(
    hass, mock_config_entry, mock_api
):
    """Der 0/1-Wert war früher eine Number - heute ist er ein Schalter."""
    registry = await _setup(hass, mock_config_entry, mock_api)

    # Die alte Number-Entität, wie sie Version 1.2 angelegt hätte.
    entity_id = _add_legacy(
        hass,
        mock_config_entry,
        "number.sommerknick",
        "comfobox1_config.general.sommerknick",
    )

    coordinator = mock_config_entry.runtime_data
    stale = legacy_entities(hass, mock_config_entry, coordinator)

    assert [entry.entity_id for entry in stale] == [entity_id]
    assert stale[0].domain == "number"

    # Der Hinweis erscheint beim nächsten Setup/Reload.
    assert await async_check_legacy_entities(hass, mock_config_entry, coordinator) == 1

    issue = ir.async_get(hass).async_get_issue(
        DOMAIN, f"legacy_entities_{mock_config_entry.entry_id}"
    )
    assert issue is not None
    assert issue.translation_key == "legacy_entities"
    assert issue.is_fixable is True

    # Und die neue Entität bleibt unberührt.
    assert (
        registry.async_get_entity_id(
            "switch", DOMAIN, "comfobox1_config.general.sommerknick"
        )
        is not None
    )


async def test_ein_entfallener_wert_wird_erkannt(hass, mock_config_entry, mock_api):
    """Ein Wert, den der Katalog nicht mehr kennt, ist veraltet."""
    await _setup(hass, mock_config_entry, mock_api)

    entity_id = _add_legacy(
        hass, mock_config_entry, "sensor.alter_wert", "comfobox1_alter.wert"
    )

    coordinator = mock_config_entry.runtime_data

    assert [
        e.entity_id for e in legacy_entities(hass, mock_config_entry, coordinator)
    ] == [entity_id]


async def test_ein_toter_datumswert_wird_erkannt(hass, mock_config_entry, mock_api):
    """Datumswerte bekommen seit dem Mapping keine Entität mehr."""
    await _setup(hass, mock_config_entry, mock_api)

    entity_id = _add_legacy(
        hass,
        mock_config_entry,
        "sensor.inbetriebnahme",
        "comfobox1_states.controller.initial_operation_date",
    )

    coordinator = mock_config_entry.runtime_data
    stale = legacy_entities(hass, mock_config_entry, coordinator)

    assert entity_id in [entry.entity_id for entry in stale]


async def test_eine_fremde_kennung_bleibt_unberuehrt(hass, mock_config_entry, mock_api):
    """Entitäten eines anderen Gateways werden nicht angefasst."""
    await _setup(hass, mock_config_entry, mock_api)

    _add_legacy(
        hass, mock_config_entry, "sensor.fremd", "anderes-gateway_zone.operation_mode"
    )

    coordinator = mock_config_entry.runtime_data

    assert legacy_entities(hass, mock_config_entry, coordinator) == []


async def test_die_status_entitaeten_bleiben_unberuehrt(
    hass, mock_config_entry, mock_api
):
    """BACnet- und Broker-Verbindung stammen nicht aus dem Katalog."""
    await _setup(hass, mock_config_entry, mock_api)

    coordinator = mock_config_entry.runtime_data

    assert legacy_entities(hass, mock_config_entry, coordinator) == []


async def test_das_aufraeumen_entfernt_nur_die_alten(hass, mock_config_entry, mock_api):
    """Aufgeräumt wird genau das, was gemeldet wurde."""
    registry = await _setup(hass, mock_config_entry, mock_api)

    alt = _add_legacy(
        hass,
        mock_config_entry,
        "number.sommerknick",
        "comfobox1_config.general.sommerknick",
    )

    coordinator = mock_config_entry.runtime_data
    removed = await async_remove_legacy_entities(hass, mock_config_entry, coordinator)

    assert removed == 1
    assert registry.async_get(alt) is None

    # Alles, was es weiterhin gibt, steht noch.
    assert (
        registry.async_get_entity_id(
            "switch", DOMAIN, "comfobox1_config.general.sommerknick"
        )
        is not None
    )
    assert (
        registry.async_get_entity_id("select", DOMAIN, "comfobox1_zone.operation_mode")
        is not None
    )


async def test_nach_dem_aufraeumen_verschwindet_der_hinweis(
    hass, mock_config_entry, mock_api
):
    """Ohne veraltete Entitäten bleibt kein Reparatur-Eintrag stehen."""
    await _setup(hass, mock_config_entry, mock_api)

    _add_legacy(
        hass,
        mock_config_entry,
        "number.sommerknick",
        "comfobox1_config.general.sommerknick",
    )

    coordinator = mock_config_entry.runtime_data
    issue = f"legacy_entities_{mock_config_entry.entry_id}"

    await async_check_legacy_entities(hass, mock_config_entry, coordinator)
    assert ir.async_get(hass).async_get_issue(DOMAIN, issue) is not None

    await async_remove_legacy_entities(hass, mock_config_entry, coordinator)

    # Der Dienst (bzw. der Abschluss des Reparatur-Flows) löscht den Hinweis.
    await async_check_legacy_entities(hass, mock_config_entry, coordinator)

    assert ir.async_get(hass).async_get_issue(DOMAIN, issue) is None


async def test_der_dienst_raeumt_auf_und_meldet_die_anzahl(
    hass, mock_config_entry, mock_api
):
    """``comfobox.cleanup_legacy_entities`` ist der Weg für Skripte.

    Eine Integration kann keine Automation anlegen - Automatisierungen sind
    Konfiguration, keine Entitäten. Ein Dienst lässt sich aber in einer
    Automation oder einem Skript aufrufen.
    """
    registry = await _setup(hass, mock_config_entry, mock_api)

    alt = _add_legacy(
        hass,
        mock_config_entry,
        "number.sommerknick",
        "comfobox1_config.general.sommerknick",
    )

    response = await hass.services.async_call(
        DOMAIN,
        "cleanup_legacy_entities",
        {},
        blocking=True,
        return_response=True,
    )

    assert response == {"removed": 1}
    assert registry.async_get(alt) is None


async def test_ohne_recorder_wird_trotzdem_aufgeraeumt(
    hass, mock_config_entry, mock_api
):
    """Das Aufräumen darf nicht daran scheitern, dass kein Recorder läuft."""
    registry = await _setup(hass, mock_config_entry, mock_api)

    alt = _add_legacy(
        hass,
        mock_config_entry,
        "number.sommerknick",
        "comfobox1_config.general.sommerknick",
    )

    coordinator = mock_config_entry.runtime_data

    # In den Tests ist kein Recorder geladen - der Dienst fehlt.
    assert not hass.services.has_service("recorder", "purge_entities")

    with patch(
        "custom_components.comfobox.cleanup._async_purge_history",
        AsyncMock(return_value=None),
    ):
        removed = await async_remove_legacy_entities(
            hass, mock_config_entry, coordinator
        )

    assert removed == 1
    assert registry.async_get(alt) is None
