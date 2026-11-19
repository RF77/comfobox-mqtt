"""Select-Plattform der ComfoBox-Integration (Auswahlwerte)."""

from __future__ import annotations

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
        # Der Status liegt unter dem reservierten Schluessel - er ist kein Item.
        if not isinstance(item, ComfoBoxItem):
            continue

        if item.platform != "select":
            continue

        entities.append(ComfoBoxSelect(coordinator, item, info, _description(item)))

    async_add_entities(entities)


def _description(item: ComfoBoxItem) -> SelectEntityDescription:
    """Erzeugt die Entity-Beschreibung.

    Bei einem Enum sind die Optionen die **Vertragsnamen** - sie werden nicht
    übersetzt und nicht umbenannt. Bei einem Kodewert (Cooling 0/1/3) sind es
    die Beschriftungen aus dem Katalog, sonst die Zahlen.
    """
    return SelectEntityDescription(
        key=item.id,
        name=item.display_name,
        translation_key=slugify(f"{item.group}_{item.name}"),
        options=item.options,
    )


class ComfoBoxSelect(ComfoBoxEntity, SelectEntity):
    """Ein schreibbarer Auswahlwert."""

    @property
    def current_option(self) -> str | None:
        """Die aktuelle Auswahl.

        Liefert ``None``, wenn der Gerätewert nicht in der Auswahlliste steht -
        eine Zahl, die in den Optionen fehlt, wäre in Home Assistant ein
        ungültiger Zustand.
        """
        text = self.item.state_text
        if text is None:
            return None
        return text if text in self.options else None

    async def async_select_option(self, option: str) -> None:
        """Schreibt die Auswahl.

        Ein Enum wird über seinen **Namen** geschrieben (das ist der
        MQTT-Vertrag), ein Kodewert über die **Zahl**: der Gateway kennt nur
        Zahlen, die Beschriftung ist reiner Anzeigetext.
        """
        item = self.item
        target: float | str | None = (
            option if item.role == "choice" else item.value_for_option(option)
        )

        if target is None:
            raise HomeAssistantError(
                f"'{option}' ist kein gültiger Wert für {item.display_name}."
            )

        try:
            await self.coordinator.client.write_item(item.id, target)
        except Exception as err:
            raise HomeAssistantError(
                f"Schreiben von {item.display_name} fehlgeschlagen: {err}"
            ) from err
        await self.coordinator.async_request_refresh()
