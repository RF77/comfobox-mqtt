"""Test des Reparatur-Flows (Klick auf „Reparieren“).

Der Hinweis ist der Weg für den normalen Nutzer: Home Assistant zeigt ihn unter
*Einstellungen → System → Reparaturen*, ein Klick räumt auf.
"""

from __future__ import annotations

from typing import Any

import pytest
from homeassistant import data_entry_flow
from homeassistant.helpers import entity_registry as er  # noqa: F401
from homeassistant.helpers import issue_registry as ir

from custom_components.comfobox.cleanup import async_check_legacy_entities
from custom_components.comfobox.const import DOMAIN

from .test_cleanup import _add_legacy, _setup


@pytest.fixture
def mock_items() -> list[dict[str, Any]]:
    """Derselbe Katalog wie in den Aufräum-Tests."""
    from .test_cleanup import mock_items as katalog

    return katalog.__wrapped__()


async def test_der_flow_raeumt_nach_bestaetigung_auf(hass, mock_config_entry, mock_api):
    """Bestätigen entfernt die alten Entitäten, der Hinweis verschwindet."""
    registry = await _setup(hass, mock_config_entry, mock_api)

    alt = _add_legacy(
        hass,
        mock_config_entry,
        "number.sommerknick",
        "comfobox1_config.general.sommerknick",
    )

    coordinator = mock_config_entry.runtime_data
    await async_check_legacy_entities(hass, mock_config_entry, coordinator)

    issue_id = f"legacy_entities_{mock_config_entry.entry_id}"
    assert ir.async_get(hass).async_get_issue(DOMAIN, issue_id) is not None

    # Home Assistant startet den Fix-Flow der Integration.
    # (Der Repairs-Flow-Manager ist kein Config-Flow - er wird direkt benutzt,
    # so wie es das Frontend über den Websocket-Befehl tut.)
    from homeassistant.components.repairs import repairs_flow_manager
    from homeassistant.setup import async_setup_component

    assert await async_setup_component(hass, "repairs", {})
    await hass.async_block_till_done()

    manager = repairs_flow_manager(hass)
    assert manager is not None

    flow = await manager.async_init(DOMAIN, data={"issue_id": issue_id})

    assert flow["type"] == data_entry_flow.FlowResultType.FORM
    assert flow["step_id"] == "confirm"
    assert flow["description_placeholders"]["count"] == "1"

    # Bestätigen.
    result = await manager.async_configure(flow["flow_id"], user_input={})

    assert result["type"] == data_entry_flow.FlowResultType.CREATE_ENTRY
    assert result["data"] == {"removed": 1}

    assert registry.async_get(alt) is None
    assert ir.async_get(hass).async_get_issue(DOMAIN, issue_id) is None


async def test_ohne_veraltete_entitaeten_gibt_es_keinen_flow(
    hass, mock_config_entry, mock_api
):
    """Ohne Hinweis gibt es nichts zu reparieren."""
    await _setup(hass, mock_config_entry, mock_api)

    assert ir.async_get(hass).issues == {}
