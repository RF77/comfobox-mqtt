"""Binary-Sensor-Plattform der ComfoBox-Integration (Sonder-Entitaeten).

Diese Entitaeten stammen nicht aus dem Item-Katalog, sondern aus dem
Gateway-Status (`/api/status`).
"""

from __future__ import annotations

from typing import Any

from homeassistant.components.binary_sensor import (
    BinarySensorDeviceClass,
    BinarySensorEntity,
    BinarySensorEntityDescription,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.device_registry import DeviceInfo
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from . import device_info
from .const import STATUS_KEY
from .coordinator import ComfoBoxCoordinator

DESCRIPTIONS: tuple[BinarySensorEntityDescription, ...] = (
    BinarySensorEntityDescription(
        key="bacnet_connected",
        name="BACnet verbunden",
        device_class=BinarySensorDeviceClass.CONNECTIVITY,
        translation_key="bacnet_connected",
    ),
    BinarySensorEntityDescription(
        key="mqtt_connected",
        name="Broker verbunden",
        device_class=BinarySensorDeviceClass.CONNECTIVITY,
        translation_key="mqtt_connected",
    ),
)


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Setzt die Binary-Sensor-Plattform ein."""
    coordinator: ComfoBoxCoordinator = entry.runtime_data
    status = await coordinator.client.get_status()
    info = device_info(entry, status)

    async_add_entities(
        ComfoBoxStatusBinarySensor(coordinator, info, description)
        for description in DESCRIPTIONS
    )


class ComfoBoxStatusBinarySensor(
    CoordinatorEntity[ComfoBoxCoordinator], BinarySensorEntity
):
    """Ein Verbindungsstatus aus dem Gateway-Status."""

    _attr_has_entity_name = True

    def __init__(
        self,
        coordinator: ComfoBoxCoordinator,
        device_info_: DeviceInfo,
        description: BinarySensorEntityDescription,
    ) -> None:
        """Initialisiert die Entitaet."""
        super().__init__(coordinator)
        status = coordinator.data.get(STATUS_KEY)
        device_id = getattr(status, "device_id", "comfobox")
        self._attr_unique_id = f"{device_id}_{description.key}"
        self._attr_device_info = device_info_
        self.entity_description = description

    @property
    def is_on(self) -> bool | None:
        """Ist die Verbindung aktiv?"""
        status = self.coordinator.data.get(STATUS_KEY)
        if status is None:
            return None
        value: Any = getattr(status, self.entity_description.key, None)
        return None if value is None else bool(value)
