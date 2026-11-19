"""Sensor-Plattform der ComfoBox-Integration."""

from __future__ import annotations

from typing import Any

from homeassistant.components.sensor import (
    SensorDeviceClass,
    SensorEntity,
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

#: Einheit -> Geraeteklasse.
#:
#: `%` und `K` fehlen bewusst: im Katalog sind Prozentwerte Pumpenleistungen,
#: Ventilatorstufen und Raumeinflüsse (keine Luftfeuchte), Kelvin-Werte immer
#: **Differenzen** (keine Temperatur). Beides als `humidity` bzw. `temperature`
#: auszugeben wäre falsch.
UNIT_TO_DEVICE_CLASS: dict[str, SensorDeviceClass] = {
    "°C": SensorDeviceClass.TEMPERATURE,
    "h": SensorDeviceClass.DURATION,
    "min": SensorDeviceClass.DURATION,
    "kWh": SensorDeviceClass.ENERGY,
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
        # Der Status liegt unter dem reservierten Schluessel - er ist kein Item.
        if not isinstance(item, ComfoBoxItem):
            continue

        # Die Plattform entscheidet - jedes Item bekommt genau eine.
        if item.platform != "sensor":
            continue

        entities.append(ComfoBoxSensor(coordinator, item, info, _description_for(item)))

    async_add_entities(entities)


class ComfoBoxSensor(ComfoBoxEntity, SensorEntity):
    """Ein lesbarer Wert.

    Den Zustand liefert `ComfoBoxEntity.native_value`: bei Auswahlwerten der
    Name bzw. die Beschriftung, sonst die Zahl.
    """


def _description_for(item: ComfoBoxItem) -> SensorEntityDescription:
    """Erzeugt die Entity-Beschreibung aus den Item-Metadaten."""
    kwargs: dict[str, Any] = {
        "key": item.id,
        "name": item.display_name,
        "translation_key": slugify(f"{item.group}_{item.name}"),
    }

    # Ein Auswahlwert ist ein Enum-Sensor: Home Assistant prüft den Zustand
    # gegen `options` und zeigt ihn als Text statt als Zahl.
    if item.is_selectable:
        kwargs["device_class"] = SensorDeviceClass.ENUM
        kwargs["options"] = item.options
        return SensorEntityDescription(**kwargs)

    device_class = UNIT_TO_DEVICE_CLASS.get(item.unit or "")
    if device_class is not None:
        kwargs["device_class"] = device_class

    if item.unit:
        kwargs["native_unit_of_measurement"] = item.unit

    state_class = _state_class(item)
    if state_class is not None:
        kwargs["state_class"] = state_class

    return SensorEntityDescription(**kwargs)


def _state_class(item: ComfoBoxItem) -> SensorStateClass | None:
    """Die Statistik-Klasse eines Sensors (oder ``None``).

    Nur **Messgrößen** und **Zähler** erzeugen eine Langzeitstatistik. Ein
    Reglertyp, ein Softwarestand oder ein Rohzustand ist keine Messung - eine
    „Messung" davon füllt nur die Datenbank und macht die Statistik-Seite
    unbrauchbar.
    """
    if item.role == "counter":
        return SensorStateClass.TOTAL_INCREASING

    if item.role != "measurement":
        return None

    # Ein Zählerstand in kWh steigt nur - auch wenn er als Messgröße vorliegt.
    if item.unit == "kWh":
        return SensorStateClass.TOTAL_INCREASING

    return SensorStateClass.MEASUREMENT
