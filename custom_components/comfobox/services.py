"""Services der ComfoBox-Integration."""

from __future__ import annotations

import logging

import voluptuous as vol
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant, ServiceCall
from homeassistant.exceptions import HomeAssistantError
from homeassistant.helpers import config_validation as cv

from .const import DOMAIN
from .coordinator import ComfoBoxCoordinator

_LOGGER = logging.getLogger(__name__)

ATTR_ENTITY_ID = "entity_id"
ATTR_VALUE = "value"

SERVICE_SET_VALUE = "set_value"
SERVICE_RESTART = "restart"
SERVICE_READ_ALL = "read_all"

SET_VALUE_SCHEMA = vol.Schema(
    {
        vol.Required(ATTR_ENTITY_ID): cv.entity_id,
        vol.Required(ATTR_VALUE): vol.Any(cv.positive_float, str, bool, int),
    }
)


async def async_setup_services(hass: HomeAssistant, entry: ConfigEntry) -> None:
    """Registriert die Services fuer einen Config-Eintrag."""

    async def async_set_value(call: ServiceCall) -> None:
        """Schreibt einen Wert auf ein Item."""
        coordinator: ComfoBoxCoordinator = entry.runtime_data
        entity_id = call.data[ATTR_ENTITY_ID]
        value = call.data[ATTR_VALUE]

        # Die entity_id muss auf ein ComfoBox-Item zeigen.
        item_id = entity_id.removeprefix("number.").removeprefix("select.")
        item_id = item_id.removeprefix("switch.").removeprefix("sensor.")

        try:
            await coordinator.client.write_item(item_id, value)
        except Exception as err:
            raise HomeAssistantError(f"Schreiben fehlgeschlagen: {err}") from err
        await coordinator.async_request_refresh()

    async def async_restart(call: ServiceCall) -> None:
        """Startet das Gateway neu."""
        coordinator: ComfoBoxCoordinator = entry.runtime_data
        try:
            await coordinator.client.restart()
        except Exception as err:
            raise HomeAssistantError(f"Neustart fehlgeschlagen: {err}") from err

    async def async_read_all(call: ServiceCall) -> None:
        """Liest alle Items sofort neu."""
        coordinator: ComfoBoxCoordinator = entry.runtime_data
        await coordinator.async_request_refresh()

    hass.services.async_register(
        DOMAIN, SERVICE_SET_VALUE, async_set_value, schema=SET_VALUE_SCHEMA
    )
    hass.services.async_register(DOMAIN, SERVICE_RESTART, async_restart)
    hass.services.async_register(DOMAIN, SERVICE_READ_ALL, async_read_all)
