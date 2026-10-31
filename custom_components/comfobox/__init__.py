"""Die ComfoBox-Integration für Home Assistant.

Startet den DataUpdateCoordinator, registriert das Gerät und richtet die
Plattformen ein. Die Werte kommen ausschließlich vom ComfoBox-Gateway -
die Integration spricht nie direkt mit dem BACnet-Bus.
"""

from __future__ import annotations

import logging

from homeassistant.config_entries import ConfigEntry
from homeassistant.const import CONF_HOST, CONF_TOKEN, CONF_VERIFY_SSL, Platform
from homeassistant.core import HomeAssistant
from homeassistant.helpers.aiohttp_client import async_create_clientsession
from homeassistant.helpers.device_registry import DeviceInfo

from .api import ComfoBoxApiClient
from .const import (
    DEFAULT_SCAN_INTERVAL,
    DOMAIN,
    MANUFACTURER,
    MIN_SCAN_INTERVAL,
    MODEL,
)
from .coordinator import ComfoBoxCoordinator
from .services import async_setup_services

_LOGGER = logging.getLogger(__name__)

PLATFORMS: list[Platform] = [
    Platform.BINARY_SENSOR,
    Platform.NUMBER,
    Platform.SELECT,
    Platform.SENSOR,
    Platform.SWITCH,
]


async def async_setup_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Setzt die Integration für einen Config-Eintrag ein."""
    session = async_create_clientsession(
        hass, verify_ssl=entry.data.get(CONF_VERIFY_SSL, True)
    )
    client = ComfoBoxApiClient(session, entry.data[CONF_HOST], entry.data[CONF_TOKEN])

    interval = max(
        MIN_SCAN_INTERVAL,
        entry.options.get("scan_interval", DEFAULT_SCAN_INTERVAL),
    )

    coordinator = ComfoBoxCoordinator(hass, client, interval)
    await coordinator.async_config_entry_first_refresh()

    entry.runtime_data = coordinator
    await hass.config_entries.async_forward_entry_setups(entry, PLATFORMS)
    await async_setup_services(hass, entry)
    return True


async def async_unload_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Entlaedt die Integration."""
    unloaded = await hass.config_entries.async_unload_platforms(entry, PLATFORMS)
    if unloaded:
        entry.runtime_data = None
    return unloaded


async def async_update_listener(hass: HomeAssistant, entry: ConfigEntry) -> None:
    """Reagiert auf Options-Aenderungen (z. B. das Abfrageintervall)."""
    await hass.config_entries.async_reload(entry.entry_id)


def device_info(entry: ConfigEntry, status) -> DeviceInfo:
    """Baut die Geraeteinformationen fuer den Device-Registry."""
    return DeviceInfo(
        identifiers={(DOMAIN, status.device_id)},
        name="ComfoBox ELESTA",
        manufacturer=MANUFACTURER,
        model=MODEL,
        sw_version=status.version,
        configuration_url=entry.data[CONF_HOST],
    )
