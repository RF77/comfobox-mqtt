"""Services der ComfoBox-Integration."""

from __future__ import annotations

import logging

import voluptuous as vol
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant, ServiceCall
from homeassistant.exceptions import HomeAssistantError
from homeassistant.helpers import config_validation as cv
from homeassistant.helpers import entity_registry as er

from .const import DOMAIN, STATUS_KEY
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


def _item_id_for(coordinator: ComfoBoxCoordinator, entity_id: str) -> str:
    """Ermittelt die Item-Id des Gateways zu einer Home-Assistant-Entität.

    <p>Die <code>entity_id</code> lässt sich **nicht** in die Item-Id
    umrechnen: Home Assistant bildet sie aus dem Anzeigenamen
    (``number.comfobox_elesta_raumsollwert_normal``), das Gateway kennt
    dagegen die Katalog-Id (``zone.setpoint_normal``).

    <p>Deshalb wird die <code>unique_id</code> der Entität nachgeschlagen.
    Sie hat die Form <code>{deviceId}_{itemId}</code> - und weil die
    Geräte-Id selbst Unterstriche enthalten darf, wird nicht blind an der
    ersten Stelle getrennt, sondern der **Katalog** entscheidet: es wird
    das Item gesucht, dessen <code>unique_id</code> exakt passt.
    """
    registry = er.async_get(coordinator.hass)
    entry = registry.async_get(entity_id)

    if entry is None or not entry.unique_id:
        raise HomeAssistantError(
            f"Die Entität '{entity_id}' gehört nicht zum ComfoBox-Gateway."
        )

    unique_id = entry.unique_id

    for item in coordinator.data.values():
        # Der Status unter dem reservierten Schlüssel ist kein Item.
        if not hasattr(item, "id"):
            continue

        device_id = getattr(
            coordinator.data.get(STATUS_KEY), "device_id", "comfobox"
        )

        if f"{device_id}_{item.id}" == unique_id:
            return item.id

    raise HomeAssistantError(
        f"Die Entität '{entity_id}' hat keine gültige ComfoBox-Item-Id."
    )


async def async_setup_services(hass: HomeAssistant, entry: ConfigEntry) -> None:
    """Registriert die Services fuer einen Config-Eintrag."""

    async def async_set_value(call: ServiceCall) -> None:
        """Schreibt einen Wert auf ein Item."""
        coordinator: ComfoBoxCoordinator = entry.runtime_data
        entity_id = call.data[ATTR_ENTITY_ID]
        value = call.data[ATTR_VALUE]

        item_id = _item_id_for(coordinator, entity_id)

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
