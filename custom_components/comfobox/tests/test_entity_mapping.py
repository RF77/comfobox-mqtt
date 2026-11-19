"""Tests der Zuordnung von Items zu Home-Assistant-Plattformen.

Die Zuordnung ergibt sich **aus den Metadaten des Gateways** - die Integration
baut sie nicht selbst. Maßgeblich ist ``role`` (Bedeutung des Werts), nicht
``kind`` (Kodierung): eine Zahl kann ein Sollwert, ein Schalter, eine Auswahl,
ein Zähler oder eine Geräteangabe sein.

Der Katalog der Fixture deckt jede Rolle genau einmal ab - und entspricht dem
**echten** REST-Vertrag des Gateways (``ItemDto``).
"""

from __future__ import annotations

from typing import Any

import pytest
from homeassistant.helpers import entity_registry as er

from custom_components.comfobox.api import ComfoBoxApiClient
from custom_components.comfobox.const import MANUFACTURER, MODEL  # noqa: F401

#: Wann eine Entität als „standardmäßig aus" gilt.
DISABLED_BY_INTEGRATION = "integration"


def _item(
    id: str,
    group: str,
    name: str,
    kind: str,
    role: str,
    **extra: Any,
) -> dict[str, Any]:
    """Baut ein Item im Format des Gateways."""
    item: dict[str, Any] = {
        "id": id,
        "group": group,
        "name": name,
        "displayName": extra.pop("display_name", name),
        "description": extra.pop("description", None),
        "kind": kind,
        "role": role,
        "unit": extra.pop("unit", None),
        "writable": extra.pop("writable", False),
        "min": extra.pop("min", None),
        "max": extra.pop("max", None),
        "priority": extra.pop("priority", "secondary"),
        "requiresExpertMode": extra.pop("requires_expert_mode", False),
        "enumValues": extra.pop("enum_values", {}),
        "allowedValues": extra.pop("allowed_values", []),
        "valueLabels": extra.pop("value_labels", {}),
        "value": extra.pop("value", None),
        "valueText": extra.pop("value_text", None),
        "lastUpdated": "2026-11-19T10:00:00Z",
        "stale": False,
    }
    item.update(extra)
    return item


@pytest.fixture
def mock_items() -> list[dict[str, Any]]:
    """Ein Katalog mit allen Rollen des echten Gateways."""
    return [
        # Sollwert mit Einheit -> number
        _item(
            "zone.set_point_normal",
            "Zone",
            "SetPointNormal",
            "analog",
            "setpoint",
            display_name="Raumsollwert normal",
            description="Raumsollwert normal 5.0..30.0",
            unit="°C",
            writable=True,
            min=5.0,
            max=30.0,
            priority="primary",
            value=21.5,
            value_text="21,5 °C",
        ),
        # Schreibbares Enum -> select mit Vertragsnamen
        _item(
            "zone.operation_mode",
            "Zone",
            "OperationMode",
            "enum",
            "choice",
            display_name="Betriebsart",
            description="Betriebsart",
            writable=True,
            priority="primary",
            enum_values={"0": "Normal", "1": "Reduced", "2": "FrostProtection"},
            value=0,
            value_text="Normal",
        ),
        # Lesendes Enum -> Enum-Sensor
        _item(
            "states.heat_pump.heat_pump_status",
            "States/HeatPump",
            "HeatPumpStatus",
            "enum",
            "choice",
            display_name="Status Wärmepumpe",
            description="Status der Wärmepumpe",
            priority="primary",
            enum_values={"1": "Standby", "3": "Heating"},
            value=3,
            value_text="Heating",
        ),
        # Lesender Ausgang -> binary_sensor
        _item(
            "states.outputs.r4_zone_pump",
            "States/Outputs",
            "R4ZonePump",
            "analog",
            "flag",
            display_name="Zonenpumpe",
            description="Zonenpumpe R4",
            priority="primary",
            value=1,
        ),
        # Kodewert mit Beschriftung -> select, schreibt die Zahl
        _item(
            "config.ventilation.fan_control",
            "Config/Ventilation",
            "FanControl",
            "analog",
            "code",
            display_name="Lüfterregelung",
            description="Lüfterregelung: Art der Drehzahlvorgabe (0, 1 oder 2)",
            writable=True,
            min=0.0,
            max=2.0,
            allowed_values=[0.0, 1.0, 2.0],
            value_labels={"0": "Automatik", "1": "Stufe 1", "2": "Stufe 2"},
            value=1,
        ),
        # Kodewert ohne Beschriftung -> Zahlen als Optionen
        _item(
            "config.energy_comsumer.cooling_zone.cooling",
            "Config/EnergyComsumer/CoolingZone",
            "Cooling",
            "analog",
            "code",
            display_name="Kühlfunktion",
            description="Kühlfunktion: 0 = gesperrt, 1 = freigegeben",
            writable=True,
            min=0.0,
            max=3.0,
            allowed_values=[0.0, 1.0, 3.0],
            value=0,
        ),
        # Reglerangabe -> Sensor ohne Statistik
        _item(
            "status.zone.state",
            "Status/Zone",
            "State",
            "analog",
            "info",
            display_name="Zustand Zone",
            description="Zustand Zone: Regelungs-Zustand",
            priority="primary",
            value=3,
        ),
        # Zähler -> total_increasing
        _item(
            "states.counters.heat_pump_operating_cycles",
            "States/Counters",
            "HeatPumpOperatingCycles",
            "analog",
            "counter",
            display_name="Schaltspiele Wärmepumpe",
            description="Schaltspiele Wärmepumpe",
            value=1234,
        ),
        # Auslöser -> button
        _item(
            "warmwater.do_heat_water_now",
            "Warmwater",
            "DoHeatWaterNow",
            "analog",
            "command",
            display_name="Warmwasser erhitzen",
            description="Einmalige Ladung des WW-Speichers.",
            writable=True,
            priority="none",
            value=0,
        ),
        # Schreibbarer 0/1-Wert -> genau ein switch
        _item(
            "config.general.sommerknick",
            "Config/General",
            "Sommerknick",
            "analog",
            "flag",
            display_name="Sommerknick",
            description="Sommerknick: * 0: deaktiviert * 1: täglich um 16:00",
            writable=True,
            requires_expert_mode=True,
            min=0.0,
            max=1.0,
            value=0,
        ),
        # Gewöhnliche Einstellung in Config -> sichtbar
        _item(
            "config.warmwater.set_point_normal",
            "Config/Warmwater",
            "SetPointNormal",
            "analog",
            "setpoint",
            display_name="Warmwasser-Sollwert",
            description="Warmwasser-Sollwert normal",
            unit="°C",
            writable=True,
            min=40.0,
            max=58.0,
            value=52.0,
        ),
        # Stündlich gelesene Messgröße -> ausgeschaltet
        _item(
            "status.solar.energy_today",
            "Status/Solar",
            "EnergyToday",
            "analog",
            "measurement",
            display_name="Solarertrag heute",
            description="Solarertrag heute",
            unit="kWh",
            priority="slow",
            value=0.5,
        ),
        # Prozentwert, der keine Luftfeuchte ist
        _item(
            "states.heat_pump.current_power",
            "States/HeatPump",
            "CurrentPower",
            "analog",
            "measurement",
            display_name="Leistung Wärmepumpe",
            description="Aktuelle Leistung",
            unit="%",
            priority="primary",
            value=42.0,
        ),
        # Regler-Metadaten
        _item(
            "states.controller.software_version",
            "States/Controller",
            "SoftwareVersion",
            "analog",
            "info",
            display_name="Softwarestand",
            description="Softwarestand des Reglers",
            priority="once",
            value=120,
        ),
        # Toter Datumswert -> keine Entität
        _item(
            "states.controller.initial_operation_date",
            "States/Controller",
            "InitialOperationDate",
            "date",
            "date",
            display_name="Inbetriebnahme",
            priority="once",
        ),
    ]


async def _setup(hass, mock_config_entry, mock_api):
    """Richtet die Integration ein und liefert die Entitäten je Id."""
    mock_config_entry.add_to_hass(hass)

    assert await hass.config_entries.async_setup(mock_config_entry.entry_id)
    await hass.async_block_till_done()

    registry = er.async_get(hass)
    entries = er.async_entries_for_config_entry(registry, mock_config_entry.entry_id)

    return {entry.unique_id: entry for entry in entries}


async def _enable(hass, entry, config_entry) -> None:
    """Schaltet eine standardmäßig ausgeschaltete Entität ein.

    Konfigurations-Entitäten und stündlich gelesene Werte werden bewusst
    ausgeschaltet angelegt - erst nach dem Einschalten (und dem Neuladen des
    Eintrags) liefert Home Assistant einen Zustand.
    """
    er.async_get(hass).async_update_entity(entry.entity_id, disabled_by=None)
    await hass.config_entries.async_reload(config_entry.entry_id)
    await hass.async_block_till_done()


async def test_jedes_item_bekommt_genau_eine_entitaet(
    hass, mock_config_entry, mock_api
):
    """Die Regression: 0/1-Werte waren `number` **und** `switch`.

    Jedes Item darf nur eine Plattform haben - sonst entstehen zwei Entitäten
    mit demselben `unique_id`-Präfix, und jede Zustandsänderung landet doppelt
    in der Datenbank.
    """
    entries = await _setup(hass, mock_config_entry, mock_api)

    domains: dict[str, list[str]] = {}
    for unique_id, entry in entries.items():
        domains.setdefault(unique_id.removeprefix("comfobox1_"), []).append(
            entry.domain
        )

    doppelt = {item: found for item, found in domains.items() if len(found) > 1}
    assert doppelt == {}

    # 15 Items, davon eines ohne Entität (Datum) + 2 Status-Sensoren.
    assert len(entries) == 16


async def test_ein_sollwert_ist_eine_number(hass, mock_config_entry, mock_api):
    """Ein schreibbarer Wert mit Einheit ist eine Number."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_zone.set_point_normal"]
    assert entry.domain == "number"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "21.5"

    # Min/Max kommen **unverändert** aus dem Katalog des Gateways.
    assert state.attributes["min"] == 5.0
    assert state.attributes["max"] == 30.0
    assert state.attributes["unit_of_measurement"] == "°C"


async def test_ein_schreibbares_enum_ist_ein_select(hass, mock_config_entry, mock_api):
    """Ein schreibbares Enum ist ein Select mit den exakten Enum-Namen."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_zone.operation_mode"]
    assert entry.domain == "select"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "Normal"
    assert list(state.attributes["options"]) == [
        "Normal",
        "Reduced",
        "FrostProtection",
    ]


async def test_ein_lesendes_enum_ist_ein_text_sensor(hass, mock_config_entry, mock_api):
    """Ein lesendes Enum ist ein Sensor mit `device_class: enum`.

    Vorher kam ein solcher Wert als Zahl an - die Zahl war in Home Assistant
    nicht zu deuten.
    """
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_states.heat_pump.heat_pump_status"]
    assert entry.domain == "sensor"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "Heating"
    assert state.attributes["device_class"] == "enum"
    assert list(state.attributes["options"]) == ["Standby", "Heating"]
    assert "state_class" not in state.attributes


async def test_ein_ausgang_ist_ein_binary_sensor(hass, mock_config_entry, mock_api):
    """Ein lesender 0/1-Wert ist eine Meldung, keine Zahl."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_states.outputs.r4_zone_pump"]
    assert entry.domain == "binary_sensor"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "on"


async def test_ein_kodewert_zeigt_beschriftungen(hass, mock_config_entry, mock_api):
    """Ein Kodewert wird zur Auswahl mit lesbaren Namen."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_config.ventilation.fan_control"]
    assert entry.domain == "select"

    await _enable(hass, entry, mock_config_entry)

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert list(state.attributes["options"]) == ["Automatik", "Stufe 1", "Stufe 2"]
    assert state.state == "Stufe 1"


async def test_ein_kodewert_ohne_beschriftung_zeigt_zahlen(
    hass, mock_config_entry, mock_api
):
    """Ohne Beschriftung bleibt es bei den erlaubten Zahlen.

    `Cooling` erlaubt 0, 1 und 3 - die 2 fehlt, obwohl der Bereich 0…3 lautet.
    """
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_config.energy_comsumer.cooling_zone.cooling"]
    assert entry.domain == "select"

    await _enable(hass, entry, mock_config_entry)

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert list(state.attributes["options"]) == ["0", "1", "3"]
    assert state.state == "0"


async def test_ein_kodewert_schreibt_die_zahl(hass, mock_config_entry, mock_api):
    """Die Beschriftung ist Anzeige - geschrieben wird die Zahl."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_config.ventilation.fan_control"]
    write = ComfoBoxApiClient.write_item

    await _enable(hass, entry, mock_config_entry)

    await hass.services.async_call(
        "select",
        "select_option",
        {"entity_id": entry.entity_id, "option": "Stufe 2"},
        blocking=True,
    )

    write.assert_awaited_once_with("config.ventilation.fan_control", 2.0)


async def test_ein_enum_schreibt_den_namen(hass, mock_config_entry, mock_api):
    """Ein Enum wird über seinen Namen geschrieben (MQTT-Vertrag)."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_zone.operation_mode"]
    write = ComfoBoxApiClient.write_item

    await hass.services.async_call(
        "select",
        "select_option",
        {"entity_id": entry.entity_id, "option": "Reduced"},
        blocking=True,
    )

    write.assert_awaited_once_with("zone.operation_mode", "Reduced")


async def test_ein_ausloeser_ist_ein_taster(hass, mock_config_entry, mock_api):
    """Ein nie gelesener Schreibwert ist ein Button und schreibt 1."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_warmwater.do_heat_water_now"]
    assert entry.domain == "button"

    write = ComfoBoxApiClient.write_item

    await hass.services.async_call(
        "button", "press", {"entity_id": entry.entity_id}, blocking=True
    )

    write.assert_awaited_once_with("warmwater.do_heat_water_now", 1)


async def test_eine_reglerangabe_erzeugt_keine_statistik(
    hass, mock_config_entry, mock_api
):
    """Ein Softwarestand ist keine Messung."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_states.controller.software_version"]
    assert entry.domain == "sensor"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "120"
    assert "state_class" not in state.attributes


async def test_ein_zaehler_ist_total_increasing(hass, mock_config_entry, mock_api):
    """Zähler erzeugen eine Summe, keine Momentaufnahme."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_states.counters.heat_pump_operating_cycles"]
    assert entry.domain == "sensor"

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.attributes["state_class"] == "total_increasing"
    assert entry.entity_category is not None


async def test_prozent_ist_keine_luftfeuchte(hass, mock_config_entry, mock_api):
    """Prozentwerte im Katalog sind Leistungen, keine Feuchte.

    Ein „device_class: humidity" würde in Home Assistant eine
    Luftfeuchte-Kurve zeigen - und den Wert in Klima-Karten einsortieren.
    """
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_states.heat_pump.current_power"]
    state = hass.states.get(entry.entity_id)

    assert state is not None
    assert "device_class" not in state.attributes
    assert state.attributes["unit_of_measurement"] == "%"


async def test_konfiguration_und_testausgaenge_sind_ausgeschaltet(
    hass, mock_config_entry, mock_api
):
    """Ein expertenpflichtiger Parameter wird angelegt, aber nicht eingeschaltet.

    Sonst entstehen beim ersten Start Hunderte Entitäten und Aufzeichnungen,
    die niemand eingeschaltet hat.
    """
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_config.general.sommerknick"]
    assert entry.domain == "switch"
    assert entry.entity_category is not None
    assert entry.disabled_by is not None


async def test_eine_gewoehnliche_einstellung_bleibt_sichtbar(
    hass, mock_config_entry, mock_api
):
    """Der Warmwasser-Sollwert ist eine Einstellung, kein Expertenwissen."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_config.warmwater.set_point_normal"]
    assert entry.domain == "number"
    assert entry.entity_category is not None
    assert entry.disabled_by is None

    state = hass.states.get(entry.entity_id)
    assert state is not None
    assert state.state == "52.0"


async def test_stuendlich_gelesene_werte_sind_ausgeschaltet(
    hass, mock_config_entry, mock_api
):
    """Ein Wert, der nur stündlich kommt, füllt sonst nur die Datenbank."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_status.solar.energy_today"]
    assert entry.domain == "sensor"
    assert entry.disabled_by is not None


async def test_ein_datumswert_bekommt_keine_entitaet(hass, mock_config_entry, mock_api):
    """Der Altbestand interpretiert diese Werte nicht (Befund A20)."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    assert "comfobox1_states.controller.initial_operation_date" not in entries


async def test_die_beschreibung_steht_als_attribut_an_der_entitaet(
    hass, mock_config_entry, mock_api
):
    """Home Assistant kennt keinen Tooltip - die Beschreibung ist ein Attribut."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    state = hass.states.get(entries["comfobox1_zone.set_point_normal"].entity_id)

    assert state is not None
    assert state.attributes["description"] == "Raumsollwert normal 5.0..30.0"
    assert state.attributes["role"] == "setpoint"
    assert state.attributes["read_priority"] == "primary"


async def test_ein_kodewert_traegt_seine_beschriftungen_als_attribut(
    hass, mock_config_entry, mock_api
):
    """Die Wert-Beschriftungen stehen auch an der Entität."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    entry = entries["comfobox1_config.ventilation.fan_control"]
    await _enable(hass, entry, mock_config_entry)

    state = hass.states.get(entry.entity_id)

    assert state is not None
    assert state.attributes["value_labels"] == {
        "0": "Automatik",
        "1": "Stufe 1",
        "2": "Stufe 2",
    }


async def test_der_status_liefert_zwei_binary_sensoren(
    hass, mock_config_entry, mock_api
):
    """Die Sonder-Entitäten kommen aus dem Gateway-Status."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    assert entries["comfobox1_bacnet_connected"].domain == "binary_sensor"
    assert entries["comfobox1_mqtt_connected"].domain == "binary_sensor"

    bacnet = hass.states.get(entries["comfobox1_bacnet_connected"].entity_id)
    mqtt = hass.states.get(entries["comfobox1_mqtt_connected"].entity_id)

    assert bacnet is not None and bacnet.state == "on"
    assert mqtt is not None and mqtt.state == "off"


async def test_das_geraet_bleibt_ein_geraet(hass, mock_config_entry, mock_api):
    """Alle Entitäten hängen an derselben Gerätekarte."""
    entries = await _setup(hass, mock_config_entry, mock_api)

    device_ids = {
        entry.device_id for entry in entries.values() if entry.device_id is not None
    }

    assert len(device_ids) == 1
