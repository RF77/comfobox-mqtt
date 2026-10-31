"""Number-Plattform der ComfoBox-Integration (schreibbare Analogwerte)."""

from __future__ import annotations

from homeassistant.components.number import NumberEntity, NumberEntityDescription
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
    """Setzt die Number-Plattform ein."""
    coordinator: ComfoBoxCoordinator = entry.runtime_data
    status = await coordinator.client.get_status()
    info = device_info(entry, status)

    entities: list[ComfoBoxEntity] = []
    for item in coordinator.data.values():
        if item.is_read_only or item.is_enum:
            continue
        entities.append(ComfoBoxEntity(coordinator, item, info, _description(item)))

    async_add_entities(entities)


def _description(item: ComfoBoxItem) -> NumberEntityDescription:
    """Erzeugt die Entity-Beschreibung.

    Min/Max kommen **unveraendert** aus dem Katalog des Gateways. Abweichende
    Grenzen wuerden Schreibvorgaenge ausserhalb des zulaessigen Bereichs erlauben.
    """
    return NumberEntityDescription(
        key=item.id,
        name=item.display_name,
        translation_key=slugify(f"{item.group}_{item.name}"),
        native_min_value=item.min_value,
        native_max_value=item.max_value,
        native_step=0.1,
        native_unit_of_measurement=item.unit,
        mode="box",
    )


class ComfoBoxNumber(ComfoBoxEntity, NumberEntity):
    """Ein schreibbarer Analogwert."""

    async def async_set_native_value(self, value: float) -> None:
        """Schreibt den Wert. Der Schreibschutz liegt im Gateway."""
        try:
            await self.coordinator.client.write_item(self.item.id, value)
        except Exception as err:
            raise HomeAssistantError(
                f"Schreiben von {self.item.display_name} fehlgeschlagen: {err}"
            ) from err
        await self.coordinator.async_request_refresh()
