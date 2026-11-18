# Nicht verbaute Sensoren – Items ohne Messwert

**Stand:** 18.11.2026 · ComfoBox Series 5 (ELESTA ECR450, Regelsoftware 4) ·
Katalog **194 Items** (127 V1 + 67 V2)

## 1. Zweck

Der Item-Katalog beschreibt **alle** Datenpunkte, die das
[Technikerhandbuch](README.md) kennt – unabhängig davon, welche Fühler und
Baugruppen in einer konkreten Anlage verbaut sind. Ein nicht verbauter Fühler
liefert trotzdem eine Antwort: der Regler meldet `32767`.

Dieses Dokument hält fest, **welche Items an der Referenzanlage keinen Messwert
liefern**, **welche Ursache** dahintersteht und **was daraus geworden ist**.
Die Umsetzung ist in [ADR-015](../05-entscheidungen/adr-015-baugruppen-verfuegbarkeit.md)
entschieden.

> **Keine Echtwerte:** Dieses Dokument nennt nur Item-Ids, Datenpunkt-Nummern
> und die Art der Abweichung. Die realen Messwerte stehen in der lokalen,
> **nicht eingecheckten** Momentaufnahme (siehe [§6](#6-wo-die-echtwerte-liegen)).

## 2. Erhebung

| | |
|---|---|
| Quelle | `GET /api/items` des Gateways |
| Umfang | 205 Items (Stand Gateway 1.2.0) |
| Aufnahmen | zwei, im Abstand von zehn Minuten |
| Ergebnis | beide Aufnahmen zeigen **dieselben** 22 Items ohne Wert |
| Gegenprobe | `GET /api/diagnostics/errors` (Fehler-Ringpuffer, 50 Einträge) |

### Vier Ursachen

| Ursache | Erkennungsmerkmal | Anzahl | Behandlung |
|---|---|---:|---|
| **Fühler liefert nichts** | Wert `32767` (= `0x7FFF`, INT16-Maximum) | 14 | Sentinel-Prüfung + Baugruppen-Gate |
| **BACnet-Objekt existiert nicht** | `ERROR_CODE_UNKNOWN_OBJECT` | 5 | 1 entfernt, 4 über Baugruppen-Gate |
| **Wert nicht interpretierbar** | `Date`-Items (Befund A20) bzw. `no_value` | 3 | unverändert (V1) |
| **Wärmemengenmessung aus** | DP 3340 = `0` („keine") | 14 | **Block entfernt** |

`32767` ist kein gültiger Messwert: **kein** Katalog-Item hat einen Bereich, der
bis dorthin reicht. Der Regler benutzt das INT16-Maximum als „kein Fühler".

## 3. Fühler liefert nichts (14 Items)

Alle 14 Items waren **V2**. Sie bleiben im Katalog – eine Anlage mit Pufferspeicher,
Kühlung oder Solar braucht sie. Der Gateway liest sie nur, wenn der Regler die
Baugruppe als vorhanden meldet.

| Item-Id | Anzeigename | Gruppe | DP | Baugruppe |
|---|---|---|---:|---|
| `status.zone.flow_temperature` | Diagnose Zone: Vorlauftemperatur | Status/Zone | 14 | **keine** – nur Sentinel |
| `status.warm_water.temperature_bottom` | Diagnose Warmwasser: Warmwassertemperatur 2 | Status/WarmWater | 4 | DP 117 = 2 |
| `status.buffer.temperature_top` | Pufferspeichertemperatur oben | Status/Buffer | 24 | DP 805 ∈ {1, 11} |
| `status.buffer.temperature_bottom` | Pufferspeichertemperatur unten | Status/Buffer | 25 | DP 805 = 11 |
| `status.heat_pump.energy_producer_temperature` | Energieerzeugertemperatur | Status/HeatPump | 21 | DP 2800 = 1 |
| `status.heat_pump.return_temperature` | WP-Rücklauftemperatur | Status/HeatPump | 3468 | DP 2800 = 1 |
| `status.heat_pump.primary_flow_temperature` | Primärvorlauftemperatur | Status/HeatPump | 3209 | DP 2800 = 1 |
| `status.heat_pump.evaporator_temperature` | Verdampfertemperatur | Status/HeatPump | 660 | DP 2800 = 1 |
| `status.heat_pump.suction_gas_temperature` | Sauggastemperatur | Status/HeatPump | 641 | DP 2800 = 1 |
| `status.heat_pump.hot_gas_temperature` | Heissgastemperatur | Status/HeatPump | 3228 | DP 2800 = 1 |
| `status.heat_pump.condenser_temperature` | Kondensatortemperatur | Status/HeatPump | 666 | DP 2800 = 1 |
| `status.heat_pump.condensation_temperature` | Kondensationstemperatur | Status/HeatPump | 3475 | DP 2800 = 1 |
| `status.heat_pump.evaporation_temperature` | Verdampfungstemperatur | Status/HeatPump | 3598 | DP 2800 = 1 |
| `status.solar.collector_temperature` | Kollektortemperatur | Status/Solar | 28 | DP 807 ≠ 0 |

**Der Sentinel allein genügt nicht:** Er verhindert nur, dass `32767` publiziert
wird. Ein Objekt, das am Regler gar nicht existiert (nächster Abschnitt), würde
weiterhin bei jedem Poll einen Fehler erzeugen. Deshalb liest der Gateway Items
einer fehlenden Baugruppe **gar nicht** erst.

## 4. BACnet-Objekt existiert nicht (5 Items)

| Item-Id | Anzeigename | DP | Behandlung |
|---|---|---:|---|
| `config.heating_system.mixer_direct_offset` | MixerDirectOffset | 3608 | **aus dem Katalog entfernt** |
| `status.buffer.cooling_temperature_top` | Kühlpufferspeichertemperatur oben | 3925 | DP 3918 ∈ {1, 11} |
| `status.buffer.cooling_temperature_bottom` | Kühlpufferspeichertemperatur unten | 3924 | DP 3918 = 11 |
| `status.solar.collector_heat_exchanger_temperature` | Kollektorwärmetauschertemperatur | 3903 | DP 807 ≠ 0 |
| `status.solar.energy_today` | Solarenergie heute | 3911 | DP 807 ≠ 0 |

`MixerDirectOffset` ist entfernt, weil das Objekt an diesem Regler **nicht
existiert**, obwohl das Handbuch es (§6.4, Seite 80) führt. Das Item erzeugte
**35 von 50** Fehlereinträgen und verdrängte damit jede andere Meldung aus dem
Ringpuffer. Ob die Nummer falsch ist oder eine Ausbaustufe fehlt, ist offen – bis
zur Klärung ist das Item kein Teil des Vertrags.

## 5. Wärmemengenmessung – Block entfernt

Die Messung ist am Regler **nicht** eingeschaltet (DP 3340 = `0` = „keine"). Die
ComfoBox Series 5 hat **keinen** Wärmemengenzähler; der Block (Handbuch §4.8)
ist deshalb nicht Teil des Katalogs:

`HeatingPower`, `WarmWaterPower`, `ThermalEnergyHeating`, `ThermalEnergyWarmWater`,
`ThermalEnergyTotal`, `ElectricalEnergyHeating`, `ElectricalEnergyWarmWater`,
`ElectricalEnergyTotal`, `CopHeating`, `CopWarmWater`, `CopTotal`,
`MeasurementMode`, `FlowRate`, `FlowRateWarmWater` (DP 3340–3357) sowie der
abgeleitete Wert `Status/Derived/CopInstant`.

Die Items lieferten dauerhaft `0`; in Home Assistant waren sie nur irreführend.
Entscheidung und Alternativen: [ADR-015](../05-entscheidungen/adr-015-baugruppen-verfuegbarkeit.md).

## 6. Wo die Echtwerte liegen

Die Momentaufnahme mit allen Werten ist **privat** und deshalb nicht im
Repository:

```
privat/werte-<datum>.md         Bericht (nach Gruppen, mit DP und Zeitstempel)
privat/werte-<datum>.json       Rohdaten aus GET /api/items
privat/erzeuge-wertebericht.ps1 Erzeuger (wiederholbar)
```

Das Verzeichnis `privat/` steht in der [`.gitignore`](../../.gitignore). Neu
erzeugen:

```powershell
pwsh -File privat/erzeuge-wertebericht.ps1
```

Der Bericht zeigt je Item zusätzlich `lastUpdated`; ein nie gelesenes Item
erscheint mit `–`.

## 7. Was der Regler selbst weiß

Die Typ-Parameter sind seit dem 18.11.2026 im Katalog (`Config/General/*` und
`Config/Warmwater/Equipment`):

| DP | Parameter | Werte | steuert |
|---:|---|---|---|
| 117 | Warmwasserbereitung | 0 Thermostat · 1 Fühler · **2 zwei Fühler** | `TemperatureBottom` (4) |
| 805 | Pufferspeichertyp | 0 keine · 1 ein Fühler · 11 zwei Fühler | `Buffer/Temperature*` (24, 25) |
| 3918 | Kühlpufferspeichertyp | 0 keine · 1 ein Fühler · 11 zwei Fühler | `Buffer/CoolingTemperature*` (3924, 3925) |
| 807 | Sonnenkollektor Hydraulik | 0 keine Funktion · 1/2/4/21/22/24 | `Solar/*` (28, 3903, 3911) |
| 2800 | Fühler zu WP-Regelung | 0 nicht vorhanden · 1 vorhanden | die neun Kältekreis-Fühler |

`Config/Warmwater/Equipment` steht an der Referenzanlage auf **1** („ein Fühler") –
der untere WW-Fühler ist damit **erwartungsgemäß** nicht vorhanden.

## 8. Offene Fragen

- Das Handbuch nennt `32767` **nicht** als Sentinel. Der Wert ist aus dem Verhalten
  abgeleitet (INT16-Maximum, kein Katalogbereich reicht dorthin).
- Ob DP 2800 = 0 wirklich alle neun Kältekreis-Fühler abdeckt, lässt sich erst nach
  dem ersten Lauf gegen das Gerät belegen. Bis dahin sperrt der Sentinel die Werte
  zusätzlich.
- `MixerDirectOffset` (3608) ist offen: Fehlnummer im Handbuch oder andere
  Ausbaustufe? Das Handbuch führt die Nummer.
- Für `status.zone.flow_temperature` (DP 14) gibt es keinen Typ-Parameter; hier
  wirkt ausschließlich die Sentinel-Prüfung.
- `states.controller.controller_type` (DP 203) liefert keinen numerischen Wert
  (`no_value`), `initial_operation_date` (3353) und `last_parameter_change_date`
  (618) sind `Date`-Items: Befund A20, bewusst ohne Wert.
