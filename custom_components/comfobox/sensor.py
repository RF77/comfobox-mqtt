"""Sensor-Plattform der ComfoBox-Integration."""

from __future__ import annotations

from typing import Any

from homeassistant.components.sensor import (
    SensorDeviceClass,
    SensorEntityDescription,
    SensorStateClass,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity_platform import AddEntitiesCallback

from . import device_info
from .api import ComfoBoxItem
from .coordinator import ComfoBoxCoordinator
from .entity import ComfoBoxEntity, slugify

#: Einheit -> Geraeteklasse. Unbekannte Einheiten bleiben ohne Klasse.
UNIT_TO_DEVICE_CLASS: dict[str, SensorDeviceClass] = {
    "°C": SensorDeviceClass.TEMPERATURE,
    "K": SensorDeviceClass.TEMPERATURE,
    "%": SensorDeviceClass.HUMIDITY,
    "h": SensorDeviceClass.DURATION,
    "min": SensorDeviceClass.DURATION,
    "W": SensorDeviceClass.POWER,
    "kW": SensorDeviceClass.POWER,
}


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Setzt die Sensor-Plattform ein."""
    coordinator: ComfoBoxCoordinator = entry.runtime_data
    status = await coordinator.client.get_status()
    info = device_info(entry, status)

    entities: list[ComfoBoxEntity] = []
    for item in coordinator.data.values():
        # Beschreibbare Items werden als number/select/switch angelegt,
        # nicht doppelt als Sensor.
        if not item.is_read_only:
            continue
        entities.append(
            ComfoBoxEntity(coordinator, item, info, _description_for(item))
        )

    async_add_entities(entities)


def _description_for(item: ComfoBoxItem) -> SensorEntityDescription:
    """Erzeugt die Entity-Beschreibung aus den Item-Metadaten."""
    device_class = UNIT_TO_DEVICE_CLASS.get(item.unit or "")

    kwargs: dict[str, Any] = {
        "key": item.id,
        "name": item.display_name,
        "translation_key": slugify(f"{item.group}_{item.name}"),
    }
    if device_class is not None:
        kwargs["device_class"] = device_class
    if item.unit is not None:
        kwargs["native_unit_of_measurement"] = item.unit
    if item.value_type == "float":
        kwargs["state_class"] = SensorStateClass.MEASUREMENT

    return SensorEntityDescription(**kwargs)
