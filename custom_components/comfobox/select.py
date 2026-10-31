"""Select-Plattform der ComfoBox-Integration (schreibbare Enumwerte)."""

from __future__ import annotations

from typing import Any

from homeassistant.components.select import SelectEntity, SelectEntityDescription
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
    """Setzt die Select-Plattform ein."""
    coordinator: ComfoBoxCoordinator = entry.runtime_data
    status = await coordinator.client.get_status()
    info = device_info(entry, status)

    entities: list[ComfoBoxEntity] = []
    for item in coordinator.data.values():
        if item.is_read_only or not item.is_enum:
            continue
        entities.append(ComfoBoxEntity(coordinator, item, info, _description(item)))

    async_add_entities(entities)


def _description(item: ComfoBoxItem) -> SelectEntityDescription:
    """Erzeugt die Entity-Beschreibung.

    Die Enum-Namen werden **unveraendert** uebernommen - sie sind Teil des
    MQTT-Vertrags und duerfen nicht uebersetzt oder umbenannt werden.
    """
    return SelectEntityDescription(
        key=item.id,
        name=item.display_name,
        translation_key=slugify(f"{item.group}_{item.name}"),
        options=list(item.enum_values),
    )


class ComfoBoxSelect(ComfoBoxEntity, SelectEntity):
    """Ein schreibbarer Enumwert."""

    @property
    def current_option(self) -> str | None:
        """Der aktuell gesetzte Enum-Name."""
        value: Any = self.item.value
        return str(value) if value is not None else None

    async def async_select_option(self, option: str) -> None:
        """Schreibt den Enum-Wert."""
        try:
            await self.coordinator.client.write_item(self.item.id, option)
        except Exception as err:
            raise HomeAssistantError(
                f"Schreiben von {self.item.display_name} fehlgeschlagen: {err}"
            ) from err
        await self.coordinator.async_request_refresh()
