"""Binary-Sensor-Plattform der ComfoBox-Integration.

Zwei Quellen:

* der **Gateway-Status** (`/api/status`) – BACnet- und Broker-Verbindung,
* die **0/1-Zustände** des Katalogs – Ausgänge (`R1`…`R10`, `Y1`, `Y2`),
  Testausgänge und Freigaben. Sie sind Meldungen, keine Messwerte, und
  erzeugen deshalb keine Statistik.
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
from .api import ComfoBoxItem
from .const import STATUS_KEY
from .coordinator import ComfoBoxCoordinator
from .entity import ComfoBoxEntity, slugify

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

    entities: list[Any] = [
        ComfoBoxStatusBinarySensor(coordinator, info, description)
        for description in DESCRIPTIONS
    ]

    for item in coordinator.data.values():
        # Der Status liegt unter dem reservierten Schluessel - er ist kein Item.
        if not isinstance(item, ComfoBoxItem):
            continue

        if item.platform != "binary_sensor":
            continue

        entities.append(
            ComfoBoxFlagBinarySensor(coordinator, item, info, _description(item))
        )

    async_add_entities(entities)


def _description(item: ComfoBoxItem) -> BinarySensorEntityDescription:
    """Erzeugt die Entity-Beschreibung eines 0/1-Zustands."""
    return BinarySensorEntityDescription(
        key=item.id,
        name=item.display_name,
        translation_key=slugify(f"{item.group}_{item.name}"),
        # Ein Ausgang ist ein laufender Zustand, keine Störung.
        device_class=BinarySensorDeviceClass.RUNNING,
    )


class ComfoBoxFlagBinarySensor(ComfoBoxEntity, BinarySensorEntity):
    """Ein lesender 0/1-Zustand aus dem Katalog."""

    @property
    def is_on(self) -> bool | None:
        """Ist der Zustand aktiv?"""
        value = self.item.value
        if value is None:
            return None
        return float(value) != 0.0


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

        # Ist MQTT bewusst abgeschaltet, ist "nicht verbunden" kein Fehler.
        # `None` zeigt in Home Assistant "unbekannt" statt "aus".
        if (
            self.entity_description.key == "mqtt_connected"
            and getattr(status, "mqtt_enabled", None) is False
        ):
            return None

        value: Any = getattr(status, self.entity_description.key, None)
        return None if value is None else bool(value)
