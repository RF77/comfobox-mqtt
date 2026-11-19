"""Aufräumen von Entitäten, die es nicht mehr gibt.

Nach einem Update der Integration ändert sich die Zuordnung Wert → Plattform:
ein 0/1-Wert war früher eine Number und ist jetzt ein Schalter, ein Kodewert war
eine Number und ist jetzt eine Auswahl, tote Datumswerte entfallen ganz.

Home Assistant lässt solche Entitäten stehen („wiederhergestellt"), samt ihrer
Aufzeichnungen. Diese Datei findet sie und räumt sie auf - ausdrücklich, denn
Löschen ist nicht umkehrbar.
"""

from __future__ import annotations

import logging
from typing import Any

from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers import entity_registry as er

from .api import ComfoBoxItem
from .const import DOMAIN, STATUS_KEY
from .coordinator import ComfoBoxCoordinator

_LOGGER = logging.getLogger(__name__)

#: Die Entitäten, die **nicht** aus dem Item-Katalog stammen.
#:
#: Sie gehören zur Integration wie jedes Item - ohne diese Liste würden sie
#: beim Aufräumen gelöscht.
STATUS_ENTITY_KEYS: tuple[str, ...] = ("bacnet_connected", "mqtt_connected")


def _device_prefix(coordinator: ComfoBoxCoordinator) -> str:
    """Das Präfix der `unique_id`s dieses Gateways."""
    status = coordinator.data.get(STATUS_KEY)
    device_id = getattr(status, "device_id", "comfobox")
    return f"{device_id}_"


def legacy_entities(
    hass: HomeAssistant,
    entry: ConfigEntry,
    coordinator: ComfoBoxCoordinator,
) -> list[er.RegistryEntry]:
    """Findet Entitäten des Eintrags, die es nicht mehr geben dürfte.

    Drei Fälle zählen als „veraltet":

    * Die Item-Id steht nicht mehr im Katalog (Wert entfallen).
    * Das Item hat keine Plattform mehr (toter Datumswert).
    * Die Entität liegt unter einer anderen Domain als heute
      (`number` → `switch`, `select`, `binary_sensor`, `button`, `sensor`).

    Args:
        hass: Die Home-Assistant-Instanz.
        entry: Der Config-Eintrag der Integration.
        coordinator: Der Coordinator mit dem aktuellen Katalog.

    Returns:
        Die veralteten Registry-Einträge (leer, wenn alles passt).
    """
    registry = er.async_get(hass)
    prefix = _device_prefix(coordinator)

    items = {
        key: value
        for key, value in coordinator.data.items()
        if isinstance(value, ComfoBoxItem)
    }

    stale: list[er.RegistryEntry] = []

    for registry_entry in er.async_entries_for_config_entry(registry, entry.entry_id):
        unique_id = registry_entry.unique_id or ""

        # Eine fremde Kennung (andere Geräte-Id) gehört nicht zu diesem
        # Katalog - sie wird nicht angefasst.
        if not unique_id.startswith(prefix):
            _LOGGER.debug(
                "Entität %s hat eine unbekannte Kennung (%s) - sie bleibt.",
                registry_entry.entity_id,
                unique_id,
            )
            continue

        key = unique_id[len(prefix) :]
        if key in STATUS_ENTITY_KEYS:
            continue

        item = items.get(key)
        if item is None:
            stale.append(registry_entry)
            continue

        platform = item.platform
        if platform is None or registry_entry.domain != platform:
            stale.append(registry_entry)

    return stale


async def async_remove_legacy_entities(
    hass: HomeAssistant,
    entry: ConfigEntry,
    coordinator: ComfoBoxCoordinator,
) -> int:
    """Löscht die veralteten Entitäten samt Aufzeichnungen.

    Args:
        hass: Die Home-Assistant-Instanz.
        entry: Der Config-Eintrag der Integration.
        coordinator: Der Coordinator mit dem aktuellen Katalog.

    Returns:
        Die Anzahl der gelöschten Entitäten.
    """
    stale = legacy_entities(hass, entry, coordinator)
    if not stale:
        return 0

    registry = er.async_get(hass)
    entity_ids = [registry_entry.entity_id for registry_entry in stale]

    removed = 0
    for registry_entry in stale:
        if registry.async_get(registry_entry.entity_id) is None:
            continue

        registry.async_remove(registry_entry.entity_id)
        removed += 1

    _LOGGER.info(
        "ComfoBox: %s veraltete Entität(en) entfernt: %s",
        removed,
        ", ".join(sorted(entity_ids)),
    )

    await _async_purge_recorder(hass, entity_ids)

    return removed


async def _async_purge_recorder(
    hass: HomeAssistant,
    entity_ids: list[str],
) -> None:
    """Löscht Historie und Langzeitstatistik der entfallenen Entitäten.

    Zwei getrennte Wege, weil Home Assistant sie trennt:

    * `recorder.purge_entities` löscht den **Zustandsverlauf** (öffentlicher
      Dienst).
    * Die **Langzeitstatistik** kennt keinen Dienst - sie wird intern über die
      Recorder-Instanz gelöscht. Bleibt sie stehen, meldet die Statistik-Seite
      dauerhaft „kein Zustand verfügbar" für jede entfallene Entität.

    Beide Schritte sind optional: ohne Recorder (`recorder:` nicht geladen)
    gibt es nichts zu löschen.
    """
    if not entity_ids:
        return

    await _async_purge_history(hass, entity_ids)
    _async_clear_statistics(hass, entity_ids)


async def _async_purge_history(
    hass: HomeAssistant,
    entity_ids: list[str],
) -> None:
    """Löscht den Zustandsverlauf über den öffentlichen Dienst."""
    if not hass.services.has_service("recorder", "purge_entities"):
        return

    try:
        await hass.services.async_call(
            "recorder",
            "purge_entities",
            {"entity_id": entity_ids, "keep_days": 0},
            blocking=True,
        )
    except Exception:
        _LOGGER.warning(
            "ComfoBox: Der Zustandsverlauf der entfernten Entitäten konnte nicht "
            "gelöscht werden.",
            exc_info=True,
        )


def _async_clear_statistics(
    hass: HomeAssistant,
    entity_ids: list[str],
) -> None:
    """Löscht die Langzeitstatistik der entfallenen Entitäten.

    Der Recorder bietet dafür keinen Dienst. Die Aufgabe wird deshalb direkt in
    seine Warteschlange gelegt; fehlt der Recorder oder ändert sich die interne
    Schnittstelle, bleibt es bei einem Hinweis - das Löschen der Entitäten ist
    wichtiger als die Statistik.
    """
    try:
        from homeassistant.components.recorder import get_instance
        from homeassistant.components.recorder.tasks import ClearStatisticsTask

        instance = get_instance(hass)
    except (ImportError, KeyError):
        _LOGGER.debug("ComfoBox: Kein Recorder - keine Statistik zu löschen.")
        return

    try:
        instance.queue_task(ClearStatisticsTask(list(entity_ids)))
    except Exception:
        _LOGGER.warning(
            "ComfoBox: Die Langzeitstatistik der entfernten Entitäten konnte "
            "nicht gelöscht werden. Bitte unter Einstellungen → Werkzeuge → "
            "Statistik von Hand löschen.",
            exc_info=True,
        )


def issue_id(entry: ConfigEntry) -> str:
    """Die Id des Reparatur-Hinweises zu diesem Eintrag."""
    return f"legacy_entities_{entry.entry_id}"


async def async_check_legacy_entities(
    hass: HomeAssistant,
    entry: ConfigEntry,
    coordinator: ComfoBoxCoordinator,
) -> int:
    """Meldet veraltete Entitäten als Reparatur-Hinweis.

    Args:
        hass: Die Home-Assistant-Instanz.
        entry: Der Config-Eintrag der Integration.
        coordinator: Der Coordinator mit dem aktuellen Katalog.

    Returns:
        Die Anzahl der gefundenen veralteten Entitäten.
    """
    # Import im Funktionskörper: die Reparatur-Verwaltung ist nicht in jeder
    # Home-Assistant-Version unter demselben Pfad verfügbar.
    from homeassistant.helpers import issue_registry as ir

    stale = legacy_entities(hass, entry, coordinator)
    issue = issue_id(entry)

    if not stale:
        ir.async_delete_issue(hass, DOMAIN, issue)
        return 0

    _LOGGER.info(
        "ComfoBox: %s Entität(en) aus einer früheren Version gefunden. "
        "Einstellungen → System → Reparaturen bietet das Aufräumen an.",
        len(stale),
    )

    ir.async_create_issue(
        hass,
        DOMAIN,
        issue,
        is_fixable=True,
        is_persistent=False,
        severity=ir.IssueSeverity.WARNING,
        translation_key="legacy_entities",
        translation_placeholders={"count": str(len(stale))},
        data={"entry_id": entry.entry_id},
    )

    return len(stale)


def describe(entities: list[er.RegistryEntry]) -> list[dict[str, Any]]:
    """Beschreibt die veralteten Entitäten für Tests und Protokolle."""
    return [
        {"entity_id": entry.entity_id, "domain": entry.domain} for entry in entities
    ]
