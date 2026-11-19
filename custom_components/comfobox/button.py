"""Button-Plattform der ComfoBox-Integration (Auslöser)."""

from __future__ import annotations

from homeassistant.components.button import ButtonEntity, ButtonEntityDescription
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.exceptions import HomeAssistantError
from homeassistant.helpers.entity_platform import AddEntitiesCallback

from . import device_info
from .api import ComfoBoxItem
from .coordinator import ComfoBoxCoordinator
from .entity import ComfoBoxEntity, slugify


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Setzt die Button-Plattform ein."""
    coordinator: ComfoBoxCoordinator = entry.runtime_data
    status = await coordinator.client.get_status()
    info = device_info(entry, status)

    entities: list[ComfoBoxEntity] = []
    for item in coordinator.data.values():
        # Der Status liegt unter dem reservierten Schluessel - er ist kein Item.
        if not isinstance(item, ComfoBoxItem):
            continue

        if item.platform != "button":
            continue

        entities.append(ComfoBoxButton(coordinator, item, info, _description(item)))

    async_add_entities(entities)


def _description(item: ComfoBoxItem) -> ButtonEntityDescription:
    """Erzeugt die Entity-Beschreibung.

    Ein Auslöser hat keinen Zustand: das Gerät liest ihn nie (Priorität
    „none"), er wird nur geschrieben. Deshalb ein Taster und keine Zahl.
    """
    return ButtonEntityDescription(
        key=item.id,
        name=item.display_name,
        translation_key=slugify(f"{item.group}_{item.name}"),
    )


class ComfoBoxButton(ComfoBoxEntity, ButtonEntity):
    """Ein Auslöser – drücken schreibt 1."""

    async def async_press(self) -> None:
        """Löst die Funktion am Regler aus."""
        try:
            await self.coordinator.client.write_item(self.item.id, 1)
        except Exception as err:
            raise HomeAssistantError(
                f"Auslösen von {self.item.display_name} fehlgeschlagen: {err}"
            ) from err
        await self.coordinator.async_request_refresh()
