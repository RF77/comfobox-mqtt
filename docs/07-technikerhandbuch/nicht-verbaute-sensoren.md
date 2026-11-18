# Nicht verbaute Sensoren – Items ohne Messwert

**Stand:** 18.11.2026 · ComfoBox Series 5 (ELESTA ECR450, Regelsoftware 4) ·
Gateway 1.2.0 · Katalog **205 Items** (127 V1 + 78 V2)

## 1. Zweck

Der Item-Katalog beschreibt **alle** Datenpunkte, die das
[Technikerhandbuch](README.md) kennt – unabhängig davon, welche Fühler und
Baugruppen in einer konkreten Anlage verbaut sind. Ein nicht verbauter Fühler
liefert trotzdem eine Antwort: der Regler meldet `32767`.

Dieses Dokument hält fest, **welche Items an dieser Anlage keinen Messwert
liefern** und **welche Ursache** dahintersteht. Es ist die Grundlage für die
Entscheidung, ob solche Items gesperrt, ausgeblendet oder aus dem Katalog
entfernt werden.

> **Keine Echtwerte:** Dieses Dokument nennt nur Item-Ids, Datenpunkt-Nummern
> und die Art der Abweichung. Die realen Messwerte stehen in der lokalen,
> **nicht eingecheckten** Momentaufnahme (siehe [§7](#7-wo-die-echtwerte-liegen)).

## 2. Erhebung

| | |
|---|---|
| Quelle | `GET /api/items` des Gateways |
| Umfang | 205 Items |
| Aufnahmen | zwei, im Abstand von zehn Minuten |
| Ergebnis | beide Aufnahmen zeigen **dieselben** 22 Items ohne Wert |
| Gegenprobe | `GET /api/diagnostics/errors` (Fehler-Ringpuffer, 50 Einträge) |

### Vier Ursachen

| Ursache | Erkennungsmerkmal | Anzahl |
|---|---|---:|
| **Fühler liefert nichts** | Wert `32767` (= `0x7FFF`, INT16-Maximum) | 14 |
| **BACnet-Objekt existiert nicht** | Wert `null`, Log `read_failed` mit `ERROR_CODE_UNKNOWN_OBJECT` | 5 |
| **Wert nicht interpretierbar** | Wert `null`, kein Fehler (`Date`-Items, Befund A20) bzw. `no_value` | 3 |
| **Wärmemengenmessung aus** | DP 3340 = `0` („keine") | 14 |

`32767` ist kein gültiger Messwert: **kein** Katalog-Item hat einen Bereich, der
bis dorthin reicht. Der Regler benutzt das INT16-Maximum als „kein Fühler".

Die ersten drei Zeilen ergeben zusammen die **22 Items ohne Wert**. Die vierte
Zeile ist eine eigene Gruppe: die Items liefern formal `0`, sind aber ohne
Wärmemengenmessung ebenso ohne Aussage (siehe [§6](#6-wärmemengenmessung-dp-3340--0)).

Die Unterscheidung ist wichtig, weil sie zwei verschiedene Antworten verlangt:

* **Objekt vorhanden, Fühler fehlt** → `32767`. Das Objekt bleibt dauerhaft
  lesbar; der Wert ist nur ohne Aussage.
* **Objekt nicht vorhanden** → `ERROR_CODE_UNKNOWN_OBJECT`. Die Funktion ist an
  diesem Regler gar nicht eingerichtet; jeder Leseversuch erzeugt einen
  **Fehler** im Ringpuffer.

## 3. Fühler liefert nichts (14 Items)

Alle 14 Items sind **V2** – sie wurden mit
[Phase 10](../06-aufgaben/phase-10-topics-v2.md) aufgenommen. Kein einziges
V1-Topic ist betroffen.

| Item-Id | Anzeigename | Gruppe | DP | Prio | Fehlender Fühler |
|---|---|---|---:|---|---|
| `status.zone.flow_temperature` | Diagnose Zone: Vorlauftemperatur | Status/Zone | 14 | Primary | Vorlauffühler der Zone |
| `status.warm_water.temperature_bottom` | Diagnose Warmwasser: Warmwassertemperatur 2 | Status/WarmWater | 4 | Secondary | WW-Fühler unten (nur bei DP 117 = „zwei Fühler") |
| `status.buffer.temperature_top` | Pufferspeichertemperatur oben | Status/Buffer | 24 | Secondary | Pufferspeicherfühler 1 |
| `status.buffer.temperature_bottom` | Pufferspeichertemperatur unten | Status/Buffer | 25 | Secondary | Pufferspeicherfühler 2 |
| `status.heat_pump.energy_producer_temperature` | Energieerzeugertemperatur | Status/HeatPump | 21 | Primary | Fühler zu WP-Regelung |
| `status.heat_pump.return_temperature` | WP-Rücklauftemperatur | Status/HeatPump | 3468 | Primary | Rücklauffühler |
| `status.heat_pump.primary_flow_temperature` | Primärvorlauftemperatur | Status/HeatPump | 3209 | Secondary | Primärkreis-Fühler |
| `status.heat_pump.evaporator_temperature` | Verdampfertemperatur | Status/HeatPump | 660 | Secondary | Verdampferfühler |
| `status.heat_pump.suction_gas_temperature` | Sauggastemperatur | Status/HeatPump | 641 | Secondary | Sauggasfühler |
| `status.heat_pump.hot_gas_temperature` | Heissgastemperatur | Status/HeatPump | 3228 | Secondary | Heissgasfühler |
| `status.heat_pump.condenser_temperature` | Kondensatortemperatur | Status/HeatPump | 666 | Secondary | Kondensatorfühler |
| `status.heat_pump.condensation_temperature` | Kondensationstemperatur | Status/HeatPump | 3475 | Slow | Hochdrucksensor (gerechnet) |
| `status.heat_pump.evaporation_temperature` | Verdampfungstemperatur | Status/HeatPump | 3598 | Slow | Niederdrucksensor (gerechnet) |
| `status.solar.collector_temperature` | Kollektortemperatur | Status/Solar | 28 | Secondary | Kollektorfühler |

### Was der Regler selbst weiß

Das Handbuch kennt **Typ-Parameter**, mit denen der Techniker die verbaute
Hardware deklariert. Sie erklären die Werte oben – und **fehlen** im Katalog:

| DP | Parameter | Werte | Erklärt |
|---:|---|---|---|
| 117 | WW-Ausrüstung | 0 Thermostat · 1 Fühler · **2 zwei Fühler** | `TemperatureBottom` (4) |
| 805 | Pufferspeichertyp | 0 keine · 1 ein Fühler · 11 zwei Fühler | `Buffer/Temperature*` (24, 25) |
| 3918 | Kühlpufferspeichertyp | 0 keine · 1 ein Fühler · 11 zwei Fühler | `Buffer/CoolingTemperature*` (3924, 3925) |
| 807 | Sonnenkollektor Hydraulik | 0 keine Funktion · 1/2/4/21/22/24 | `Solar/*` (28, 3903, 3911) |
| 3904 | Sonnenkollektor Hydraulik 2 | 0 keine · 1 Pumpe · 2 Umlenkventil | `Solar/*` |
| 2800 | Fühler zu WP-Regelung | 0 nicht vorhanden · **1 vorhanden** | `HeatPump/*` (9 Fühler) |
| 3340 | Wärmemengenmessung | **0 keine** · 1 EIN · 2 EIN, WW separat | `Status/Energy/*` (14) |

Nur **DP 117** und **DP 3340** sind im Katalog (`Config/Warmwater/Equipment`,
`Status/Energy/MeasurementMode`). DP 117 steht an dieser Anlage auf **1**
(„ein Fühler") – der untere WW-Fühler ist damit **erwartungsgemäß** nicht
vorhanden.

## 4. BACnet-Objekt existiert nicht (5 Items)

Diese Items sind **V2**. Der Regler antwortet mit `ERROR_CODE_UNKNOWN_OBJECT`;
das Objekt ist an diesem Gerät nicht angelegt.

| Item-Id | Anzeigename | DP | Prio | Anmerkung |
|---|---|---:|---|---|
| `config.heating_system.mixer_direct_offset` | MixerDirectOffset | 3608 | Secondary | **35 von 50** Fehlereinträgen – jeder Poll-Zyklus |
| `status.buffer.cooling_temperature_top` | Kühlpufferspeichertemperatur oben | 3925 | Slow | Kühlpuffer vermutlich nicht eingerichtet (DP 3918) |
| `status.buffer.cooling_temperature_bottom` | Kühlpufferspeichertemperatur unten | 3924 | Slow | dito |
| `status.solar.collector_heat_exchanger_temperature` | Kollektorwärmetauschertemperatur | 3903 | Slow | Solar vermutlich nicht eingerichtet (DP 807) |
| `status.solar.energy_today` | Solarenergie heute | 3911 | Slow | dito |

> **Folge für die Diagnose:** `MixerDirectOffset` läuft im Sekundär-Takt in den
> Fehler und **füllt den Ringpuffer (50 Einträge) allein**. Andere Fehler werden
> dadurch verdrängt, bevor jemand sie sieht. Das ist der stärkste Grund, hier
> tätig zu werden.

## 5. Regler-Metadaten ohne Wert (3 Items)

Diese drei sind **V1** und bekannt:

| Item-Id | DP | Ursache |
|---|---:|---|
| `states.controller.controller_type` | 203 | liefert keinen numerischen Wert (`no_value`, Warnung im Log) |
| `states.controller.initial_operation_date` | 3353 | `Date`-Item: der Wert wird nicht interpretiert (Befund A20) |
| `states.controller.last_parameter_change_date` | 618 | dito |

`DateValue` ist im Projekt bewusst tot (siehe
[copilot-instructions.md §8](../../.github/copilot-instructions.md)) – hier ist
**keine** Änderung nötig.

## 6. Wärmemengenmessung (DP 3340 = 0)

Die Messung ist am Regler **nicht eingeschaltet** (Wert `0` = „keine"). Die 11
kWh-/kW-/COP-Items sind deshalb bedeutungslos; hinzu kommen die drei
Bedien-/Messwerte des Blocks:

| Item-Id | DP | `[Energy]`-gesperrt |
|---|---:|:---:|
| `status.energy.heating_power` | 3342 | ja |
| `status.energy.warm_water_power` | 3346 | ja |
| `status.energy.thermal_energy_heating` | 3343 | ja |
| `status.energy.thermal_energy_warm_water` | 3347 | ja |
| `status.energy.thermal_energy_total` | 3349 | ja |
| `status.energy.electrical_energy_heating` | 3344 | ja |
| `status.energy.electrical_energy_warm_water` | 3354 | ja |
| `status.energy.electrical_energy_total` | 3355 | ja |
| `status.energy.cop_heating` | 3345 | ja |
| `status.energy.cop_warm_water` | 3356 | ja |
| `status.energy.cop_total` | 3357 | ja |
| `status.energy.measurement_mode` | 3340 | – (Schalter) |
| `status.energy.flow_rate` | 3341 | – |
| `status.energy.flow_rate_warm_water` | 3348 | – |

**MQTT ist bereits korrekt:** der
[`EnergyMeasurementGate`](../../src/ComfoBox.Gateway/Energy/EnergyMeasurementGate.cs)
publiziert die elf gesperrten Items **nicht** und setzt
`ComfoBox/Status/Energy/Available = 0`. Die **Web-Oberfläche** zeigt sie
dagegen unverändert mit `0 kW`/`0 kWh` an – dort fehlt die Auswertung.

## 7. Wo die Echtwerte liegen

Die Momentaufnahme mit allen 205 Werten ist **privat** und deshalb nicht im
Repository:

```
privat/werte-2026-11-18.md      Bericht (nach Gruppen, mit DP und Zeitstempel)
privat/werte-2026-11-18.json    Rohdaten aus GET /api/items
privat/erzeuge-wertebericht.ps1 Erzeuger (wiederholbar)
```

Das Verzeichnis `privat/` steht in der [`.gitignore`](../../.gitignore). Neu
erzeugen:

```powershell
pwsh -File privat/erzeuge-wertebericht.ps1
```

## 8. Optionen

| # | Option | Bewertung |
|---|---|---|
| A | **Nichts tun** | `32767` und `0 kWh` bleiben sichtbar; der Fehler-Ringpuffer bleibt durch `MixerDirectOffset` blockiert. Schlechteste Variante. |
| B | **Items aus dem Katalog löschen** | Nicht empfohlen: der Katalog gilt für **alle** Anlagen – eine ComfoBox mit Solar und Pufferspeicher braucht genau diese Items. V2 ist laut [Kompatibilität](../04-migration/mqtt-kompatibilitaet.md) **additiv**; ein Entfernen ist ein Vertragsbruch und lässt Home-Assistant-Entitäten verwaisen. |
| C | **Zur Laufzeit erkennen und sperren** (Empfehlung) | Wie bei der Wärmemengenmessung: die Typ-Parameter lesen und die abhängigen Items nur publizieren/anzeigen, wenn die Baugruppe existiert. Der Katalog bleibt vollständig. |
| D | **Nur in der UI ausblenden** | Billig, aber die Topics publizieren weiter `32767` nach Home Assistant. Nur als Zwischenschritt sinnvoll. |

## 9. Empfohlene nächste Schritte

1. **`MixerDirectOffset` (DP 3608) prüfen.** Das Objekt existiert an diesem
   Regler nicht, obwohl das Handbuch es (§6.4, Seite 80) führt. Entweder ist die
   Nummer falsch oder die Funktion gibt es nur in einer anderen Ausbaustufe.
   Bis zur Klärung die Priorität auf `None` setzen – dann wird das Item nicht
   mehr gepollt und der Fehlerstrom hört auf.
2. **Typ-Parameter in den Katalog aufnehmen** (read-only, `Slow`):
   805, 3918, 807, 3904, 2800. DP 117 und 3340 sind bereits vorhanden.
3. **Ein allgemeines Verfügbarkeits-Gate** bauen – der
   [`EnergyMeasurementGate`](../../src/ComfoBox.Gateway/Energy/EnergyMeasurementGate.cs)
   ist die Vorlage: abgeleiteter Zustand, `Status/.../Available`, retained
   Clear beim Wechsel.
4. **Sentinel `32767` behandeln:** nicht als Zahl publizieren, sondern als
   „nicht verfügbar" (MQTT) bzw. `–` mit Hinweis (UI).
5. **Erst danach** über Entfernen oder Umbenennen einzelner Items entscheiden.

## 10. Offene Fragen

* Das Handbuch nennt `32767` **nicht** als Sentinel. Der Wert ist aus dem
  Verhalten abgeleitet (INT16-Maximum, kein Katalogbereich reicht dorthin) –
  für die Dokumentation wäre eine Herstellerbestätigung wünschenswert.
* Die neun Kältekreis-Fühler hängen vermutlich an der Wärmepumpe, nicht am
  Regler ([§4.4](datenpunkt-register.md)). Ob DP 2800 = 0 der einzige Schalter
  ist, lässt sich erst nach Aufnahme des Datenpunkts belegen.
* Ob `MixerDirectOffset` (3608) eine Fehlnummer ist oder eine Ausbaustufe
  voraussetzt, ist offen; das Handbuch führt die Nummer.
