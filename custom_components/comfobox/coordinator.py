"""DataUpdateCoordinator der ComfoBox-Integration."""

from __future__ import annotations

import asyncio
import logging
from datetime import timedelta
from typing import Any

from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.exceptions import ConfigEntryAuthFailed
from homeassistant.helpers.update_coordinator import DataUpdateCoordinator, UpdateFailed

from .api import (
    AuthenticationError,
    ComfoBoxApiClient,
    ConnectionFailed,
)
from .const import DOMAIN, STATUS_KEY

_LOGGER = logging.getLogger(__name__)


class ComfoBoxCoordinator(DataUpdateCoordinator[dict[str, Any]]):
    """Holt die Items vom Gateway und hält den letzten bekannten Stand."""

    config_entry: ConfigEntry

    def __init__(
        self,
        hass: HomeAssistant,
        client: ComfoBoxApiClient,
        interval: int,
    ) -> None:
        """Initialisiert den Coordinator."""
        super().__init__(
            hass,
            _LOGGER,
            name=DOMAIN,
            update_interval=timedelta(seconds=interval),
        )
        self._client = client

    @property
    def client(self) -> ComfoBoxApiClient:
        """Der API-Client des Coordinators."""
        return self._client

    async def _async_update_data(self) -> dict[str, Any]:
        """Fragt Status und Items ab.

        Bei Fehlern wird der letzte bekannte Stand beibehalten, damit die
        Entitaeten nicht flackern. HA markiert sie dann als `unavailable`.
        """
        try:
            status, items = await asyncio.gather(
                self._client.get_status(), self._client.get_items()
            )
        except AuthenticationError as err:
            raise ConfigEntryAuthFailed from err
        except ConnectionFailed as err:
            raise UpdateFailed(str(err)) from err

        data: dict[str, Any] = {item.id: item for item in items}
        # Der Status liegt unter einem reservierten Schluessel, damit er nicht
        # mit einer Item-ID kollidiert.
        data[STATUS_KEY] = status
        return data
