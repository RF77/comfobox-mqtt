"""Reparatur-Hinweis: alte ComfoBox-Entitäten aufräumen.

Nach einem Update können Entitäten übrig bleiben, die es nicht mehr gibt:
Der Gateway liefert weniger Werte, oder ein Wert liegt jetzt unter einer
anderen Plattform (`number` → `switch`). Home Assistant lässt solche
Entitäten als „wiederhergestellt" stehen - samt Aufzeichnungen.

Der Hinweis erscheint unter *Einstellungen → System → Reparaturen* und
verschwindet, sobald aufgeräumt wurde.
"""

from __future__ import annotations

from typing import Any

import voluptuous as vol
from homeassistant.components.repairs import RepairsFlow
from homeassistant.core import HomeAssistant
from homeassistant.data_entry_flow import FlowResult

from .cleanup import async_remove_legacy_entities, legacy_entities
from .coordinator import ComfoBoxCoordinator


class LegacyEntitiesRepairFlow(RepairsFlow):
    """Fragt nach und räumt dann auf.

    Der Ablauf folgt dem Muster von Home Assistant: ``async_step_init`` bekommt
    beim Start die **Daten des Hinweises** als Eingabe, nicht eine Bestätigung.
    Es wird deshalb immer auf den Bestätigungsschritt verwiesen - sonst würde
    ein bloßes Öffnen sofort löschen.
    """

    async def async_step_init(
        self, user_input: dict[str, Any] | None = None
    ) -> FlowResult:
        """Startet den Ablauf (ohne zu löschen)."""
        return await self.async_step_confirm()

    async def async_step_confirm(
        self, user_input: dict[str, Any] | None = None
    ) -> FlowResult:
        """Bestätigung einholen – und erst dann aufräumen."""
        entry_id = self.data.get("entry_id")
        entry = (
            self.hass.config_entries.async_get_entry(entry_id)
            if isinstance(entry_id, str)
            else None
        )
        coordinator: ComfoBoxCoordinator | None = (
            entry.runtime_data if entry is not None else None
        )

        if entry is None or coordinator is None:
            # Die Integration wurde inzwischen entfernt oder entladen - dann
            # gibt es nichts aufzuräumen.
            return self.async_abort(reason="not_loaded")

        if user_input is not None:
            removed = await async_remove_legacy_entities(self.hass, entry, coordinator)
            return self.async_create_entry(data={"removed": removed})

        stale = legacy_entities(self.hass, entry, coordinator)

        return self.async_show_form(
            step_id="confirm",
            data_schema=vol.Schema({}),
            description_placeholders={"count": str(len(stale))},
        )


async def async_create_fix_flow(
    hass: HomeAssistant,
    issue_id: str,
    data: dict[str, str | int | float | None] | None,
) -> RepairsFlow:
    """Erzeugt den Flow zu einem Reparatur-Hinweis."""
    return LegacyEntitiesRepairFlow()
