"""Switch-Plattform der ComfoBox-Integration (schreibbare 0/1-Werte)."""

from __future__ import annotations

from homeassistant.components.switch import SwitchEntity, SwitchEntityDescription
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
    """Setzt die Switch-Plattform ein."""
    coordinator: ComfoBoxCoordinator = entry.runtime_data
    status = await coordinator.client.get_status()
    info = device_info(entry, status)

    entities: list[ComfoBoxEntity] = []
    for item in coordinator.data.values():
        if item.is_read_only or not item.is_boolean:
            continue
        entities.append(ComfoBoxEntity(coordinator, item, info, _description(item)))

    async_add_entities(entities)


def _description(item: ComfoBoxItem) -> SwitchEntityDescription:
    """Erzeugt die Entity-Beschreibung."""
    return SwitchEntityDescription(
        key=item.id,
        name=item.display_name,
        translation_key=slugify(f"{item.group}_{item.name}"),
    )


class ComfoBoxSwitch(ComfoBoxEntity, SwitchEntity):
    """Ein schreibbarer 0/1-Wert."""

    @property
    def is_on(self) -> bool | None:
        """Ist der Wert aktiv?"""
        value = self.item.value
        if value is None:
            return None
        return bool(float(value))

    async def async_turn_on(self, **kwargs) -> None:
        """Setzt den Wert auf 1."""
        await self._async_write(1)

    async def async_turn_off(self, **kwargs) -> None:
        """Setzt den Wert auf 0."""
        await self._async_write(0)

    async def _async_write(self, value: int) -> None:
        """Schreibt den Wert. Der Schreibschutz liegt im Gateway."""
        try:
            await self.coordinator.client.write_item(self.item.id, value)
        except Exception as err:
            raise HomeAssistantError(
                f"Schreiben von {self.item.display_name} fehlgeschlagen: {err}"
            ) from err
        await self.coordinator.async_request_refresh()
