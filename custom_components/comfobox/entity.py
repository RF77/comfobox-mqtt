"""Gemeinsame Basis aller ComfoBox-Entitäten."""

from __future__ import annotations

from typing import Any

from homeassistant.const import EntityCategory
from homeassistant.helpers.device_registry import DeviceInfo
from homeassistant.helpers.entity import EntityDescription
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .api import ComfoBoxItem
from .const import STATUS_KEY
from .coordinator import ComfoBoxCoordinator

#: Gruppen, die reine Diagnose sind (Testausgänge, Zähler).
DIAGNOSTIC_GROUPS: tuple[str, ...] = ("Tests", "States/Counters", "Status/Counters")

#: Gruppen, deren Werte die Anlage verstellen – sie sind Konfiguration.
CONFIG_GROUP_PREFIX: str = "Config/"


def slugify(value: str) -> str:
    """Wandelt einen Namen in snake_case."""
    cleaned = "".join(ch if ch.isalnum() else "_" for ch in value)
    return "_".join(part for part in cleaned.split("_") if part)


def entity_category(item: ComfoBoxItem) -> EntityCategory | None:
    """Die Kategorie einer Entität (oder ``None``).

    * `Config/*` – die Anlage verstellende Parameter (Konfiguration)
    * `Tests/*`, Zähler – reine Diagnose

    Messwerte und Zustände bleiben in der normalen Liste; sonst wäre die
    Raumtemperatur unter „Diagnose" versteckt.
    """
    if item.group.startswith(CONFIG_GROUP_PREFIX):
        return EntityCategory.CONFIG

    if any(item.group.startswith(group) for group in DIAGNOSTIC_GROUPS):
        return EntityCategory.DIAGNOSTIC

    return None


def is_expert_hidden(item: ComfoBoxItem) -> bool:
    """Wird die Entität standardmäßig ausgeschaltet angelegt?

    Spiegelt die MQTT-Discovery: Testausgänge, **expertenpflichtige**
    Einstellungen und stündlich gelesene Werte erzeugen sonst Hunderte
    Entitäten und Aufzeichnungen, die niemand braucht.

    Eine gewöhnliche Einstellung wie der Warmwasser-Sollwert bleibt sichtbar -
    nur Werte, die der Regler ohnehin nur im Experten-Modus freigibt, sind
    ausgeschaltet.
    """
    if item.group.startswith("Tests"):
        return True

    if item.writable and item.requires_expert_mode:
        return True

    return item.priority == "slow"


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

        category = entity_category(item)
        if category is not None:
            self._attr_entity_category = category

        # Ein Test-Ausgang, ein Experten-Parameter oder ein stündlich
        # gelesener Wert soll die Entitätsliste nicht fluten: er wird
        # angelegt, aber standardmäßig ausgeschaltet. Der Nutzer schaltet ihn
        # bewusst ein - dann erst entstehen Aufzeichnungen.
        if is_expert_hidden(item):
            self._attr_entity_registry_enabled_default = False

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
        erlaubte Bereich und die Auswahlwerte trotzdem sichtbar sind, stehen
        sie im Dialog der Entitaet unter „Attribute". `min`/`max` und `options`
        zeigen Number und Select ohnehin schon an - hier kommen sie fuer
        Sensoren und Schalter hinzu.

        Die Rolle und das Leseverhalten stehen ebenfalls dabei: „slow" erklärt,
        warum ein Wert alt aussieht.
        """
        item = self.item
        attributes: dict[str, Any] = {"role": item.role, "read_priority": item.priority}

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

        if item.value_labels:
            attributes["value_labels"] = {
                str(number): label for number, label in item.value_labels.items()
            }

        return attributes

    @property
    def native_value(self) -> Any:
        """Der Wert aus dem Coordinator-Cache.

        Auswahlwerte tragen Text: bei einem Enum den Vertragsnamen, bei einem
        Kodewert die Beschriftung aus dem Katalog (sonst die Zahl). Messgrößen,
        Zähler und Reglerangaben bleiben Zahlen.
        """
        item = self.item

        text = item.state_text
        if text is not None:
            return text

        return item.value
