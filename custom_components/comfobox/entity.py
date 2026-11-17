"""Gemeinsame Basis aller ComfoBox-Entitäten."""

from __future__ import annotations

from typing import Any

from homeassistant.helpers.device_registry import DeviceInfo
from homeassistant.helpers.entity import EntityDescription
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .api import ComfoBoxItem
from .const import STATUS_KEY
from .coordinator import ComfoBoxCoordinator


def slugify(value: str) -> str:
    """Wandelt einen Namen in snake_case."""
    cleaned = "".join(ch if ch.isalnum() else "_" for ch in value)
    return "_".join(part for part in cleaned.split("_") if part)


class ComfoBoxEntity(CoordinatorEntity[ComfoBoxCoordinator]):
    """Basisklasse fuer alle Entitaeten der Integration."""

    _attr_has_entity_name = True

    def __init__(
        self,
        coordinator: ComfoBoxCoordinator,
        item: ComfoBoxItem,
        device_info: DeviceInfo,
        description: EntityDescription,
    ) -> None:
        """Initialisiert die Entitaet."""
        super().__init__(coordinator)
        self._item = item
        self._attr_unique_id = f"{self._device_id}_{item.id}"
        self._attr_device_info = device_info
        self.entity_description = description

    @property
    def _device_id(self) -> str:
        """Die Geraete-ID des Gateways (aus dem Status)."""
        status = self.coordinator.data.get(STATUS_KEY)
        return getattr(status, "device_id", "comfobox")

    @property
    def item(self) -> ComfoBoxItem:
        """Der aktuelle Katalogeintrag."""
        return self.coordinator.data.get(self._item.id, self._item)

    @property
    def available(self) -> bool:
        """Ein veralteter Wert wird als nicht verfuegbar gemeldet."""
        return super().available and not self.item.is_stale

    @property
    def extra_state_attributes(self) -> dict[str, Any]:
        """Die Katalog-Metadaten des Werts.

        Home Assistant kennt keinen Tooltip. Damit die Beschreibung, der
        erlaubte Bereich und die Enum-Werte trotzdem sichtbar sind, stehen sie
        im Dialog der Entitaet unter „Attribute". `min`/`max` und `options`
        zeigen Number und Select ohnehin schon an - hier kommen sie fuer
        Sensoren und Schalter hinzu.
        """
        item = self.item
        attributes: dict[str, Any] = {}

        if item.description:
            attributes["description"] = item.description

        # Bei einem Schalter (0/1) waeren `min`/`max` nur Rauschen.
        if not item.is_boolean:
            if item.min_value is not None:
                attributes["min"] = item.min_value
            if item.max_value is not None:
                attributes["max"] = item.max_value

        if item.enum_values:
            attributes["enum_values"] = {
                str(number): name for number, name in item.enum_values.items()
            }

        return attributes

    @property
    def native_value(self) -> Any:
        """Der Wert aus dem Coordinator-Cache.

        Enum-Werte traegt das Gateway als Namen (`valueText`) - die Zahl waere
        als Zustand unbrauchbar.
        """
        item = self.item

        if item.is_enum:
            return item.enum_name

        return item.value
