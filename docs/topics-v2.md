# Topics V2 – der erweiterte Topic-Vertrag

> **Status:** **entschieden am 2026-11-16** – siehe §10 und
> [ADR-014](05-entscheidungen/adr-014-topics-v2-namensraum-und-expertenmodus.md).
> **V2 ist rein additiv** – kein bestehendes Topic, kein Payload-Format und kein
> Enum-Name aus [topics.md](topics.md) wird geändert.
>
> **Grundlage:** Technikerhandbuch ELESTA ECR450 V4.0 (Software-Version 4).
> Auswertung und Datenpunkt-Register: [07-technikerhandbuch/](07-technikerhandbuch/README.md).
>
> **V1 bleibt eingefroren:** [topics.md](topics.md) und [topics.json](topics.json) sind die
> Test-Fixture des Contract-Tests. Sie werden **ausschliesslich** vom Generator
> (`tools/generate-topics/generate-topics.mjs`) geändert – nie von Hand, auch nicht für V2.

## 1 Warum ein V2-Dokument

Der heutige Topic-Baum bildet **127 von über 400** Datenpunkten des Reglers ab. Die Auswahl
stammt aus dem Legacy-Code (2016) und ist historisch gewachsen: sie enthält fast alle
*schreibbaren* Einstellungen, aber nur einen Bruchteil der *lesenden* Zustände.

Für ein Gateway, das eine Wärmepumpe in Home Assistant abbilden soll, ist das die falsche
Gewichtung:

| | V1 (heute) | V2 (entschieden) |
|---|---|---|
| lesende Topics | 46 | + 56 |
| davon Diagnose-relevant | wenige | vollständig |
| Energie-/COP-Daten | keine | vollständig |
| Zählerstände | 4 | 15 |
| Fehlerursache erkennbar | nein | ja (Übersteuerung, Zustände) |

V2 ändert **nichts** an V1 – es ergänzt.

## 2 Der Vertrag bleibt unverändert

Diese Regeln gelten für V1 **und** V2. Sie sind nicht verhandelbar.

### 2.1 Adressschema

```
{BaseTopic}/{Gruppe}/{Name}              lesend
{BaseTopic}/{Gruppe}/{Name}/Set          schreibend
{BaseTopic}/{Gruppe}/{Name}/AsNumber     Enum als Zahl (nur bei Enum-Items)
{BaseTopic}/{Gruppe}/{Name}/AsNumber/Set
```

- `BaseTopic` = `ComfoBox` (Default, konfigurierbar)
- Gruppen und Namen sind **PascalCase**, keine Umlaute, keine Leerzeichen
- Die Topic-Adresse ist **stabil** – sie ist die einzige Identität, die Nutzer kennen

### 2.2 Payload-Formate

| Wertart | Lesen | Schreiben | Beispiel |
|---|---|---|---|
| Analog | Zahl, InvariantCulture, max. 3 Nachkommastellen | Zahl | `21.5` |
| Enum | Name aus dem Enum | Name **oder** Zahl | `HeatingWithHeatPump` |
| Datum | ISO 8601 `yyyy-MM-dd` | `yyyy-MM-dd` | `2016-03-14` |
| Boolean-artig | `1` / `0` | `1` / `0` | `1` |

> **Nie** `float.Parse`/`ToString` ohne `CultureInfo.InvariantCulture`. Das war Befund A5 –
> auf deutschen Systemen sind sonst alle Schreibvorgänge kaputt.

### 2.3 Verfügbarkeit

| Topic | Payload | Retain |
|---|---|---|
| `ComfoBox/Connected` | `1` / `0` | ja |
| `ComfoBox/Version` | `Ver <SemVer>` | ja |
| `ComfoBox/Status/Health` | `online` / `degraded` / `offline` | ja (LWT) |
| `ComfoBox/Status/Errors` | JSON, letzte 50 | nein |

`Connected=1` wird bei **jedem** Connect publiziert (Befund A10).

### 2.4 Schreibschutz

Jeder Schreibvorgang läuft über `BacnetDevice` und damit über den persistenten Zähler
(`MaxNumberOfWritesPer24h`, Default 200). Es gibt **keinen** Weg daran vorbei – auch nicht
für neue V2-Topics.

Ein ungültiger Wert wird **abgelehnt**. Niemals einen Default schreiben.

### 2.5 Neue schreibbare Topics nur mit Experten-Modus

Alle in V2 **neu** hinzugefügten schreibbaren Topics tragen `RequiresExpertMode = true`.
Ohne Experten-Modus existiert ihr `/Set`-Topic **nicht**: kein Subscribe, keine
Home-Assistant-Discovery als `number`/`select`, kein REST-Schreibpfad. Das gilt auch für
die neuen Konfigurations-Topics unter `Config/`.

**Begründung:** Die neuen Werte ändern das Regelverhalten der Anlage (Heizkurve,
Ventilation, Kältekreis). Ein versehentliches Schreiben – etwa aus einer HA-Automation –
darf nicht möglich sein. **Lesen bleibt immer erlaubt.**

Die bestehenden V1-Topics behalten ihr bisheriges Verhalten; nur die bereits im
Legacy-Katalog markierten Items bleiben experten-pflichtig.

## 3 Namensräume in V2

| Namensraum | Inhalt | Status |
|---|---|---|
| `Zone/`, `Warmwater/`, `Tests/` | Bedien-Topics aus V1 | **eingefroren** |
| `States/…` | lesende Topics aus V1 | **eingefroren** |
| `Config/…` | schreibbare Einstellungen aus V1 | **eingefroren**, wird in V2 **erweitert** |
| `Status/…` | **neue** lesende Topics (V2) | neu |
| `Command/…` | **neue** Aktionen (V2) | neu |
| `Special/…` | Bestand (`NumberOfWritesPer24h`) | eingefroren |

**Die Regel „neue Topics nur unter `Status/` bzw. `Command/`" wird für schreibbare
Einstellungen bewusst geweitet:** neue *Konfigurations*-Werte bleiben unter `Config/`, damit
lesen/schreiben symmetrisch bleibt (`Config/X/Y` + `Config/X/Y/Set`). Ein neuer
Konfigurationswert unter `Status/` mit `/Set` wäre semantisch falsch und würde die
Home-Assistant-Discovery verkomplizieren. Diese Weitung ist am 2026-11-16 als
[ADR-014](05-entscheidungen/adr-014-topics-v2-namensraum-und-expertenmodus.md)
beschlossen.

> **Merksatz:** `States/` = V1-Altbestand, `Status/` = V2. Beide bedeuten „nur lesen";
> der Unterschied ist die Herkunft, nicht die Semantik.

## 4 Bestehende Topics (V1, unverändert)

**Alle 127 Topics bleiben unverändert.** Sie sind hier nur zur Vollständigkeit aufgeführt –
die verbindliche Quelle sind [topics.md](topics.md) und [topics.json](topics.json), beide
**eingefroren** (V1).

> Die Tabellen unten zeigen bereits den **Zielzustand** nach
> [Phase 9](06-aufgaben/phase-9-datenpunkt-korrekturen.md), wo er vom eingefrorenen V1
> abweicht (Objekt-Ids, Grenzen, Schreibbarkeit, zwei Namen). `topics.md`/`topics.json`
> werden dafür **nicht** angefasst – der Nachweis läuft über den Katalog und den
> Contract-Test.

Spalte **W**: `x` = schreibbar · `x (E)` = schreibbar, aber nur bei aktivem
Experten-Modus · `—` = nur lesend.
Spalte **DP** = BACnet-Object-Identifier (Datenpunkt-Nummer laut Handbuch).

> **Achtung bei `x (E)`:** Der Altbestand kennzeichnet diese Items mit
> `IsReadOnly = !Settings.Default.ExpertMode`. Weil der Experten-Modus im Legacy per Default
> **aus** war, steht in [topics.json](topics.json) `IsReadOnly: true`. Zur Laufzeit sind sie
> aber schreibbar, sobald der Experten-Modus aktiv ist – die Liste ist deshalb **nicht**
> mit der Spalte `IsReadOnly` der Fixture gleichzusetzen.

### 4.1 Bedienung – Zone

| Topic | DP | Typ | Bereich | Einheit | W |
|---|---:|---|---|---|---|
| `ComfoBox/Zone/OperationMode` | 830 | Enum `OperationModes` | 1…7 | | x |
| `ComfoBox/Zone/HeatingCoolingMode` | 3078 | Enum `HeatingCoolingModes` | 1/2/3/5 | | x |
| `ComfoBox/Zone/PartyTime` | 674 | Analog | | h | x |
| `ComfoBox/Zone/EcoTime` | 675 | Analog | | h | x |
| `ComfoBox/Zone/CalibrateTemperature` | 75 | Analog | | °C | x |
| `ComfoBox/Zone/ManualAdoption` | 679 | Analog | | K | x |
| `ComfoBox/Zone/SetPointNormal` | 62 | Analog | 5.0…30.0 | °C | x |
| `ComfoBox/Zone/SetPointReduced` | 61 | Analog | 5.0…30.0 | °C | x |
| `ComfoBox/Zone/SetPointFrost` | 60 | Analog | 5.0…30.0 | °C | x |

### 4.2 Bedienung – Warmwasser

| Topic | DP | Typ | Bereich | Einheit | W |
|---|---:|---|---|---|---|
| `ComfoBox/Warmwater/DoHeatWaterNow` | 2590 | Analog (Befehl) | | | x |
| `ComfoBox/Warmwater/SetPointNormal` | 72 | Analog | 5.0…58.0 | °C | x |
| `ComfoBox/Warmwater/SetPointReduced` | 71 | Analog | 5.0…58.0 | °C | x |
| `ComfoBox/Warmwater/SetPointFrost` | 70 | Analog | 5.0…58.0 | °C | x |
| `ComfoBox/Warmwater/SetPointLegio` | 73 | Analog | 5.0…58.0 | °C | x |

> Der Höchstwert ist die **Liefergrenze der Wärmepumpe** (58 °C), nicht DP 190.
> Seit [Phase 9](06-aufgaben/phase-9-datenpunkt-korrekturen.md) (Aufgabe 9.10, 2026-11-15)
> gilt `Max = 58`; `SetPointLegio` ist damit nicht mehr auf 60…70 °C einstellbar – bewusst.
> Wer höher muss, nutzt `WW-elektrisch` (DP 118/421) am Gerät.

### 4.3 Tests / Inbetriebnahme

| Topic | DP | Typ | Einheit | W |
|---|---:|---|---|---|
| `ComfoBox/Tests/OutputsInactive` | 2049 | Analog | | — |
| `ComfoBox/Tests/R4` | 2782 | Analog | | — |
| `ComfoBox/Tests/Y1` | 2789 | Analog | | — |
| `ComfoBox/Tests/Y2` | 2790 | Analog | | — |

> Diese Topics schalten **echte Ausgänge** und sind deshalb seit
> [Phase 9](06-aufgaben/phase-9-datenpunkt-korrekturen.md) (Aufgabe 9.9, 2026-11-15)
> **read-only**: `OutputsInactive`
> legt die gesamte Anlage still (`1` = alle Ausgänge inaktiv, Gerätefehler 99), `R4`, `Y1`
> und `Y2` setzen einen Ausgang von Hand – gedacht für den Techniker **am Gerät**
> (Handbuch §7 „Anlage testen").

### 4.4 States – Controller und Zähler

| Topic | DP | Typ | Einheit | W |
|---|---:|---|---|---|
| `ComfoBox/States/Controller/ControllerType` | 203 | Analog | | — |
| `ComfoBox/States/Controller/ExtensionType` | 2044 | Analog | | — |
| `ComfoBox/States/Controller/SoftwareVersion` | 2043 | Analog | | — |
| `ComfoBox/States/Controller/InitialOperationDate` | 3353 | Datum | | — |
| `ComfoBox/States/Controller/OperatingHours` | 211 | Analog | h | — |
| `ComfoBox/States/Controller/NumberOfStartUps` | 212 | Analog | | — |
| `ComfoBox/States/Controller/NumberOfParameterChanges` | 617 | Analog | | — |
| `ComfoBox/States/Controller/LastParameterChangeDate` | 618 | Datum | | — |
| `ComfoBox/States/Counters/HeatPumpOperatingHours` | 30 | Analog | h | — |
| `ComfoBox/States/Counters/HeatPumpOperatingCycles` | 40 | Analog | | — |
| `ComfoBox/States/Counters/ElectricalWarmWaterOperatingHours` | 37 | Analog | h | — |
| `ComfoBox/States/Counters/ElectricalWarmWaterCycles` | 47 | Analog | | — |

### 4.5 States – Wärmepumpe

| Topic | DP | Typ | Einheit | W |
|---|---:|---|---|---|
| `ComfoBox/States/HeatPump/CurrentPower` | 45 | Analog | % | — |
| `ComfoBox/States/HeatPump/HeatPumpSetPoint` | 210 | Analog | °C | — |
| `ComfoBox/States/HeatPump/CoolingFlowTemperature` | 3098 | Analog | °C | — |
| `ComfoBox/States/HeatPump/PrimaryTemperature` | 602 | Analog | °C | — |
| `ComfoBox/States/HeatPump/CurrentFlowTemperature` | 600 | Analog | °C | — |
| `ComfoBox/States/HeatPump/MaxFlowTemperature` | 648 | Analog | °C | — |
| `ComfoBox/States/HeatPump/SuctionAirTemp` | 3222 | Analog | °C | — |
| `ComfoBox/States/HeatPump/HeatPumpStatus` | 662 | Enum `HeatPumpStatusEnums` | | — |

### 4.6 States – Zone, Warmwasser, Ein-/Ausgänge

| Topic | DP | Typ | Einheit | W |
|---|---:|---|---|---|
| `ComfoBox/States/Zone/RoomTemperature` | 12 | Analog | °C | — |
| `ComfoBox/States/Zone/CurrentSetPoint` | 208 | Analog | °C | — |
| `ComfoBox/States/Zone/CalculatedFlowTemperature` | 209 | Analog | °C | — |
| `ComfoBox/States/Zone/OutdoorTemperature` | 10 | Analog | °C | — |
| `ComfoBox/States/Zone/OutdoorTemperatureBuilding` | 205 | Analog | °C | — |
| `ComfoBox/States/WarmWater/Temperature` | 3 | Analog | °C | — |
| `ComfoBox/States/WarmWater/CurrentTargetTemperature` | 207 | Analog | °C | — |
| `ComfoBox/States/Inputs/E7SecondRoomTemperature` | 2619 | Analog | °C | — |
| `ComfoBox/States/Inputs/E8HpFlowBackTemp` | 2620 | Analog | °C | — |
| `ComfoBox/States/Outputs/Y1PrimaryPump` | 2789 | Analog | % | — |
| `ComfoBox/States/Outputs/Y2` | 2790 | Analog | % | — |
| `ComfoBox/States/Outputs/R1FreeCooling` | 2779 | Analog | | — |
| `ComfoBox/States/Outputs/R3Verdichter` | 2781 | Analog | | — |
| `ComfoBox/States/Outputs/R4ZonePump` | 2782 | Analog | | — |
| `ComfoBox/States/Outputs/R5WarmWater` | 2783 | Analog | | — |
| `ComfoBox/States/Outputs/R7ElectricalWarmWater` | 2785 | Analog | | — |
| `ComfoBox/States/Outputs/R10AdditionalHeater` | 2788 | Analog | | — |

> Die Objekt-Ids in dieser Tabelle sind der **Zielzustand** nach
> [Phase 9](06-aufgaben/phase-9-datenpunkt-korrekturen.md): `2619/2620` sind laut Handbuch
> E7/E8, die Ausgangszustände stehen in §7 (2779…2790). **Seit 2026-11-15 gilt das auch im
> Katalog** (Aufgaben 9.7 und 9.8).
> Die Topics `E7…`/`E8…` sind **neue Namen**; vorher hiessen sie
> `E6SecondRoomTemperature` und `E7HpFlowBackTemp`.

### 4.7 Config – Allgemein, Primärpumpe, Freecooling

| Topic | DP | Typ | Bereich | Einheit | W |
|---|---:|---|---|---|---|
| `ComfoBox/Config/General/Sommerknick` | 185 | Analog | 0/1 | | x (E) |
| `ComfoBox/Config/General/EntlueftungPrimaerkreis` | 3398 | Analog | | h | x |
| `ComfoBox/Config/General/EntlueftungSekundaerkreis` | 3400 | Analog | | h | x |
| `ComfoBox/Config/PrimaryPump/Min` | 411 | Analog | 0.0…100.0 | % | x (E) |
| `ComfoBox/Config/PrimaryPump/Max` | 412 | Analog | 0.0…100.0 | % | x (E) |
| `ComfoBox/Config/PrimaryPump/MinCooling` | 3613 | Analog | 0.0…100.0 | % | x (E) |
| `ComfoBox/Config/PrimaryPump/MaxCooling` | 3614 | Analog | 0.0…100.0 | % | x (E) |
| `ComfoBox/Config/PrimaryPump/MaxMode` | 3232 | Analog | 0/1 | | x (E) |
| `ComfoBox/Config/PrimaryPump/StoppedMode` | 3096 | Analog | 0/1/**11** | | x (E) |
| `ComfoBox/Config/Freecooling/DeltaTempForSwitching` | 574 | Analog | 0.0…20.0 | K | x |
| `ComfoBox/Config/Freecooling/DiffForSwitching` | 575 | Analog | 2.0…10.0 | K | x |
| `ComfoBox/Config/Freecooling/MinTemperature` | 576 | Analog | 6.0…99.0 | °C | x (E) |

> `Config/PrimaryPump/*` sind **nicht** die Pumpe, sondern die Parameter des stetigen
> Ausgangs **Y1** (`Y1 Minimal/Maximal/Max Mode/Antriebstyp`). Die Benennung ist historisch.

### 4.8 Config – Comfofond, Heizsystem, Energieerzeuger

| Topic | DP | Typ | Bereich | Einheit | W |
|---|---:|---|---|---|---|
| `ComfoBox/Config/Comfofond/CoolingSetPoint` | 3441 | Analog | −50.0…999.0 | °C | x |
| `ComfoBox/Config/Comfofond/CoolingHysteresis` | 3431 | Analog | 2.0…20.0 | K | x |
| `ComfoBox/Config/Comfofond/HeatingSetPoint` | 3442 | Analog | −50.0…999.0 | °C | x |
| `ComfoBox/Config/Comfofond/HeatingHysteresis` | **3452** | Analog | 2.0…20.0 | K | x |
| `ComfoBox/Config/HeatingSystem/EnergieerzeugerTempImAuslegepunkt` | 163 | Analog | 20.0…99.0 | °C | x (E) |
| `ComfoBox/Config/HeatingSystem/AdaptEnergieerzeugerTempImAuslegepunkt` | 166 | Analog | 20.0…99.0 | °C | x (E) |
| `ComfoBox/Config/HeatingSystem/Anlagenfrostschutz` | 187 | Analog | −15.0…20.0 | °C | x |
| `ComfoBox/Config/EnergyProducer/PrimaryPumpPreRunningTime` | 880 | Analog | 0.0…99.0 | min | x (E) |
| `ComfoBox/Config/EnergyProducer/PrimaryPumpPostRunningTime` | 881 | Analog | 0.0…99.0 | min | x (E) |

> ⚠️ `HeatingHysteresis` zeigt auf DP **3452** – das ist laut Handbuch
> „KM2-Ausgang bei Error". Die Schaltdifferenz von KM2 ist **3432**.
> Siehe [review R2](07-technikerhandbuch/review-ist-implementierung.md#r2).

### 4.9 Config – Zone, Heizkurve, Heizen

| Topic | DP | Typ | Bereich | Einheit | W |
|---|---:|---|---|---|---|
| `ComfoBox/Config/EnergyComsumer/Zone/Gebäudeträgheit` | 170 | Analog | 0…3 | | x |
| `ComfoBox/Config/EnergyComsumer/Zone/Mischerlaufzeit` | 113 | Analog | 1.0…30.0 | min | x (E) |
| `ComfoBox/Config/EnergyComsumer/Zone/ÜberhöhVorlaufEnergieerz` | 168 | Analog | 0.0…30.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/Zone/NachlaufzeitZonenpumpe` | 186 | Analog | 0.0…30.0 | min | x (E) |
| `ComfoBox/Config/EnergyComsumer/Zone/RaumsollüberhSol` | 462 | Analog | 0.0…6.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/Heizkurve/Fixpunkt` | 160 | Analog | 10.0…40.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/Heizkurve/AussentempImAuslegepunkt` | 161 | Analog | −30.0…0.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/Heizkurve/VorlauftempImAuslegepunkt` | 162 | Analog | 20.0…99.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/Heizkurve/AdaptierterFixpunkt` | 164 | Analog | 10.0…40.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/Heizkurve/AdaptVorlauftempImAuslegepunkt` | 165 | Analog | 20.0…99.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/Heizkurve/Heizkennlinienadaption` | 167 | Analog | 0…2 | | x |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/Tagesheizgrenze` | 180 | Analog | 0/1 | | x |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/TagesheizgrenzeOffset` | 3569 | Analog | −5.0…5.0 | K | x |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/Winterheizgrenze` | 181 | Analog | 0.0…10.0 | K | x |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/Raumeinfluss` | 183 | Analog | 0.0…150.0 | % | x |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/OptimierungHeizschaltzeiten` | 172 | Analog | 0/1 | | x (E) |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/MaxVorhaltezeitHeizen` | 173 | Analog | 0.0…180.0 | min | x (E) |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/MaxVorhaltezeitAbsenken` | 174 | Analog | 0.0…120.0 | min | x (E) |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/VorlaufMaximal` | 154 | Analog | 0.0…125.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/VorlaufMinimal` | 153 | Analog | 0.0…99.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/HeizenZone/RaumsollKorrZone` | 3576 | Analog | −5.0…5.0 | K | x |

### 4.10 Config – Kühlen

| Topic | DP | Typ | Bereich | Einheit | W |
|---|---:|---|---|---|---|
| `ComfoBox/Config/EnergyComsumer/CoolingZone/Cooling` | 470 | Analog | 0/1/3 | | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/Sommerkühlgrenze` | 473 | Analog | −10.0…10.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/KühlraumsollAbstNormal` | 3562 | Analog | 0.0…10.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/KühlraumsollAbstReduziert` | 863 | Analog | 0.0…10.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/KühlraumsollAbstStandby` | 864 | Analog | 0.0…10.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/FixpunktRaumsoll` | 474 | Analog | 10.0…30.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/SteilheitRaumsollSchiebung` | 475 | Analog | 0.0…5.0 | K | x |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/MinVorlTemp20` | 477 | Analog | 0.0…99.0 | °C | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/MinVorlTemp40` | 478 | Analog | 0.0…99.0 | °C | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/AbsMinVorlauftemp` | 479 | Analog | 0.0…99.0 | °C | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/RaumeinflussBeiKühlen` | 578 | Analog | 100.0…999.0 | % | x (E) |
| `ComfoBox/Config/EnergyComsumer/CoolingZone/UmschaltungRaumfühler` | 3085 | Analog | 0…2 | | x |

### 4.11 Config – Warmwasser, Frostschutz, Wärmepumpe

| Topic | DP | Typ | Bereich | Einheit | W |
|---|---:|---|---|---|---|
| `ComfoBox/Config/EnergyComsumer/Warmwater/Legionellenschutzfunktion` | 192 | Analog | 0…8 | | x |
| `ComfoBox/Config/EnergyComsumer/Warmwater/Zwangsladung` | 199 | Analog | 0/1 | | x |
| `ComfoBox/Config/EnergyComsumer/Warmwater/Vorrang` | 196 | Analog | 0…2 | | x |
| `ComfoBox/Config/EnergyComsumer/Warmwater/EnergieerzAnhebungFühler` | 193 | Analog | 2.0…60.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/Warmwater/EnergieerzSollThermostat` | 194 | Analog | 0.0…99.0 | °C | x (E) |
| `ComfoBox/Config/EnergyComsumer/Warmwater/Nachlaufzeit` | 197 | Analog | 0.0…10.0 | min | x |
| `ComfoBox/Config/EnergyComsumer/Warmwater/Freigabe` | 175 | Analog | 0…2 | | x |
| `ComfoBox/Config/EnergyComsumer/Warmwater/SollwertMaximal` | 190 | Analog | 5.0…99.0 | °C | x |
| `ComfoBox/Config/EnergyComsumer/Warmwater/SollwertMaximalWP` | 447 | Analog | 0.0…58.0 | °C | x (E) |
| `ComfoBox/Config/EnergyComsumer/Warmwater/Schaltdifferenz` | 191 | Analog | 1.0…10.0 | K | x |
| `ComfoBox/Config/EnergyComsumer/Warmwater/Entladeschutz` | 157 | Analog | **0/3** | | x |
| `ComfoBox/Config/EnergyComsumer/Protection/KondensatorFrostschutz/KondensatFrostschutz` | 494 | Enum `KondensatFrostschutzEnum` | 0/31/37/39/63/80 | | x (E) |
| `ComfoBox/Config/EnergyComsumer/Protection/KondensatorFrostschutz/KondFrostschutzTemp` | 495 | Analog | −20.0…30.0 | °C | x (E) |
| `ComfoBox/Config/EnergyComsumer/Protection/KondensatorFrostschutz/KondFrostschutzSchatdiff` | 496 | Analog | 2.0…10.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/HeatPump/SwitchingDifferenceStage1` | 140 | Analog | 2.0…20.0 | K | x (E) |
| `ComfoBox/Config/EnergyComsumer/HeatPump/MinHeatpumpRunningTime` | 143 | Analog | 0.0…30.0 | min | x |
| `ComfoBox/Config/EnergyComsumer/HeatPump/RestartDelay` | 864 | Analog | 20.0…60.0 | min | x |
| `ComfoBox/Config/EnergyComsumer/HeatPump/FlowMaxTemperature` | 444 | Analog | 0.0…58.0 | °C | x |

## 5 Neue Topics (V2)

Alle folgenden Topics sind **neu**. Keines überschreibt ein V1-Topic.
`P` = Poll-Priorität (`1` = häufig, `3` = selten – siehe
[Poll-Plan](../src/ComfoBox.Gateway/Bacnet/PollPlan.cs)).

### 5.1 Status – Zone (warum heizt es nicht?)

Diese vier Topics beantworten die häufigste Support-Frage. Ohne sie ist jede Diagnose im UI
und in Home Assistant Blindflug.

| Topic | DP | Typ | Werte | P |
|---|---:|---|---|---:|
| `ComfoBox/Status/Zone/FlowTemperature` | 14 | Analog | °C | 1 |
| `ComfoBox/Status/Zone/State` | 59 | Analog | Regler-intern, Werte nicht dokumentiert | 1 |
| `ComfoBox/Status/Zone/SetPointType` | 398 | Enum `ZoneSetPointTypes` | 0 Frostschutz · 1 Reduziert · 2 Normal · 3 Legionellen | 2 |
| `ComfoBox/Status/Zone/Override` | 676 | Enum `ZoneOverrides` | 0 Ohne · 1 Party · 2 Eco · 3 Ferien · 4 Fernbedienung · 5 Externer Eingang · 6 Inbetriebnahme · 7 Taupunktwächter · 8 Bodenaustrocknung · 9 WP-Störung · 10 Optimierung WP-Ausschaltung · 11 Raumsoll-Überhöhung Solar · 13 Sollwertschiebung ΔT · 14 Zeiten AUS · 18 Aussentemperatur · 21 Entlüftung Sekundärkreis · 22 Nur Zusatzheizung | 2 |

> **`Override` ist das wichtigste neue Topic.** Es sagt in einem Wort, warum der Raum
> kalt bleibt: Ferien, Party, Eco, Taupunktwächter, WP-Störung, Zeiten AUS.

### 5.2 Status – Warmwasser

| Topic | DP | Typ | Werte | P |
|---|---:|---|---|---:|
| `ComfoBox/Status/WarmWater/TemperatureBottom` | 4 | Analog | °C | 2 |
| `ComfoBox/Status/WarmWater/SetPointType` | 397 | Enum `WarmWaterSetPointTypes` | 0 Frostschutz · 1 Reduziert · 2 Normal · 3 Legionellen | 2 |
| `ComfoBox/Status/WarmWater/State` | 2599 | Enum `WarmWaterStates` | 0 AUS · 1 WW mit Energieerzeuger · 2 WW-elektrisch · 3 WW mit Zusatzheizung · 4 WW-Entladeschutz · 5 EW-Sperre | 2 |
| `ComfoBox/Status/WarmWater/Override` | 2598 | Enum `WarmWaterOverrides` | 0 Ohne · 1 Party · 3 Ferien · 5 Externer Eingang · 9 WP-Störung · 10 Optimierung · 13 Sollwertschiebung · 14 Zeiten AUS · 15 WP-Vorlauf zu hoch · 17 WW-elektrisch · 19 Optimierung WW-el · 20 EW-Sperre · 22 Nur Zusatzheizung | 2 |

### 5.3 Status – Kältekreis / Wärmepumpe

| Topic | DP | Typ | Einheit | P |
|---|---:|---|---|---:|
| `ComfoBox/Status/HeatPump/EnergyProducerTemperature` | 21 | Analog | °C | 1 |
| `ComfoBox/Status/HeatPump/ReturnTemperature` | 3468 | Analog | °C | 1 |
| `ComfoBox/Status/HeatPump/PrimaryFlowTemperature` | 3209 | Analog | °C | 2 |
| `ComfoBox/Status/HeatPump/EvaporatorTemperature` | 660 | Analog | °C | 2 |
| `ComfoBox/Status/HeatPump/SuctionGasTemperature` | 641 | Analog | °C | 2 |
| `ComfoBox/Status/HeatPump/HotGasTemperature` | 3228 | Analog | °C | 2 |
| `ComfoBox/Status/HeatPump/CondenserTemperature` | 666 | Analog | °C | 2 |
| `ComfoBox/Status/HeatPump/CondensationTemperature` | 3475 | Analog | °C | 3 |
| `ComfoBox/Status/HeatPump/EvaporationTemperature` | 3598 | Analog | °C | 3 |
| `ComfoBox/Status/HeatPump/HeatPumpStatusStage2` | 663 | Enum `HeatPumpStatusEnums` | | 2 |
| `ComfoBox/Status/HeatPump/DefrostDeltaActual` | 644 | Analog | K | 3 |
| `ComfoBox/Status/HeatPump/DefrostDeltaReference` | 3110 | Analog | K | 3 |
| `ComfoBox/Status/HeatPump/DefrostDeltaCorrected` | 643 | Analog | K | 3 |

**Begründung:** Überhitzung und Unterkühlung sind die einzigen Frühwarnindikatoren für
Kältemittelmangel bzw. Verschmutzung. `HotGas` > 130 °C ist eine Störung; `Sauggas` nahe
`Verdampfer` zeigt ein offenes Expansionsventil. Ohne diese Werte sieht man am Gateway nur
„läuft / läuft nicht".

### 5.4 Status – Pufferspeicher

| Topic | DP | Typ | Einheit | P |
|---|---:|---|---|---:|
| `ComfoBox/Status/Buffer/TemperatureTop` | 24 | Analog | °C | 2 |
| `ComfoBox/Status/Buffer/TemperatureBottom` | 25 | Analog | °C | 2 |
| `ComfoBox/Status/Buffer/SetPoint` | 281 | Analog | °C | 2 |
| `ComfoBox/Status/Buffer/CoolingTemperatureTop` | 3925 | Analog | °C | 3 |
| `ComfoBox/Status/Buffer/CoolingTemperatureBottom` | 3924 | Analog | °C | 3 |

**Begründung:** ΔT zwischen oben und unten ist der beste Indikator für
Schichtung/Durchladung und für einen defekten Fühler.

### 5.5 Status – Baugruppen (Typ-Parameter) und ihre Verfügbarkeit

Die ComfoBox Series 5 hat **keinen Wärmemengenzähler**. Der Block „Energie und COP"
(Handbuch §4.8) ist deshalb **nicht** Teil des Katalogs: die Items liefern dauerhaft `0`
und wären in Home Assistant nur irreführend. Entschieden am 2026-11-18
([ADR-015](05-entscheidungen/adr-015-baugruppen-verfuegbarkeit.md)).

Stattdessen liest der Gateway die **Typ-Parameter**, mit denen der Regler seine verbaute
Hardware deklariert. Sie sind rein lesend, werden stündlich gelesen (P=3) und entscheiden,
welche Items überhaupt gelesen werden.

| Topic | DP | Typ | Werte | P |
|---|---:|---|---|---:|
| `ComfoBox/Config/General/BufferType` | 805 | Enum `BufferTypes` | 0 keine · 1 ein Fühler · 11 zwei Fühler | 3 |
| `ComfoBox/Config/General/CoolingBufferType` | 3918 | Enum `BufferTypes` | 0 keine · 1 ein Fühler · 11 zwei Fühler | 3 |
| `ComfoBox/Config/General/SolarHydraulics` | 807 | Enum `SolarHydraulicsTypes` | 0 keine Funktion · 1/2/4/21/22/24 | 3 |
| `ComfoBox/Config/General/HeatPumpSensor` | 2800 | Enum `PresenceTypes` | 0 nicht vorhanden · 1 vorhanden | 3 |

> **Verfügbarkeit:** Je Baugruppe publiziert der Gateway ein retained Topic unter
> `ComfoBox/Status/Hardware/…` (`1` = vorhanden, `0` = nicht vorhanden, leer = noch nicht
> gelesen). Es ist die `availability_topic` der abhängigen Entitäten.
>
> | Topic | Typ-Parameter | Abhängige Items |
> |---|---|---|
> | `ComfoBox/Status/Hardware/BufferTop` | 805 ∈ {1, 11} | `Status/Buffer/TemperatureTop` (24) |
> | `ComfoBox/Status/Hardware/BufferBottom` | 805 = 11 | `Status/Buffer/TemperatureBottom` (25) |
> | `ComfoBox/Status/Hardware/CoolingBufferTop` | 3918 ∈ {1, 11} | `Status/Buffer/CoolingTemperatureTop` (3925) |
> | `ComfoBox/Status/Hardware/CoolingBufferBottom` | 3918 = 11 | `Status/Buffer/CoolingTemperatureBottom` (3924) |
> | `ComfoBox/Status/Hardware/Solar` | 807 ≠ 0 | `Status/Solar/CollectorTemperature` (28), `…/CollectorHeatExchangerTemperature` (3903), `…/EnergyToday` (3911) |
> | `ComfoBox/Status/Hardware/HeatPumpSensors` | 2800 = 1 | die neun Kältekreis-Fühler unter `Status/HeatPump` |
> | `ComfoBox/Status/Hardware/WarmWaterBottom` | 117 = 2 | `Status/WarmWater/TemperatureBottom` (4) |
>
> **Ohne** Baugruppe wird ein Item **nicht gelesen**: ein Objekt, das es am Regler nicht
> gibt, antwortet mit `ERROR_CODE_UNKNOWN_OBJECT` – jede Abfrage erzeugt einen Fehler und
> belegt den Bus. Die retained Topics der abhängigen Items werden beim Wechsel einmal
> geleert.
>
> **Sentinel `32767`:** Meldet der Regler für einen Temperaturfühler das INT16-Maximum,
> ist kein Fühler angeschlossen. Der Wert wird **nicht** publiziert (kein Wert ist besser
> als ein falscher) und einmal je Item in `Status/Errors` gemeldet.

### 5.6 Status – Zähler und Wartung

| Topic | DP | Typ | Einheit | P |
|---|---:|---|---|---:|
| `ComfoBox/Status/Counters/Stage2OperatingHours` | 31 | Analog | h | 3 |
| `ComfoBox/Status/Counters/Stage2Cycles` | 41 | Analog | | 3 |
| `ComfoBox/Status/Counters/AdditionalHeaterOperatingHours` | 33 | Analog | h | 3 |
| `ComfoBox/Status/Counters/AdditionalHeaterCycles` | 43 | Analog | | 3 |
| `ComfoBox/Status/Counters/HeatingOperatingHours` | 3060 | Analog | h | 3 |
| `ComfoBox/Status/Counters/WarmWaterOperatingHours` | 3061 | Analog | h | 3 |
| `ComfoBox/Status/Counters/ActiveCoolingOperatingHours` | 3362 | Analog | h | 3 |
| `ComfoBox/Status/Counters/PassiveCoolingOperatingHours` | 3363 | Analog | h | 3 |
| `ComfoBox/Status/Counters/DefrostCycles` | 3485 | Analog | | 3 |
| `ComfoBox/Status/Counters/DefrostOperatingHours` | 3484 | Analog | h | 3 |
| `ComfoBox/Status/Counters/ServiceHours` | 3366 | Analog | h | 3 |

**Begründung:** `ServiceHours` gegen `Config/Service/Interval` ergibt die Wartungsanzeige
(„Service notwendig" ab Überschreitung). Die Einschaltzahlen sind für die
Verschleissbewertung relevant.

> **Nicht doppelt anlegen:** DP 3365 ist bereits als `Config/Service/Interval`
> (schreibbar, Experten-Modus) im Katalog. Ein zusätzliches
> `Status/Counters/ServiceInterval` würde denselben Datenpunkt ein zweites Mal als
> Entität publizieren. Deshalb gibt es **kein** Status-Topic; gelesen wird der Wert über
> `Config/Service/Interval`.

### 5.7 Status – Sonnenkollektor (nur falls vorhanden)

| Topic | DP | Typ | Einheit | P |
|---|---:|---|---|---:|
| `ComfoBox/Status/Solar/CollectorTemperature` | 28 | Analog | °C | 2 |
| `ComfoBox/Status/Solar/CollectorHeatExchangerTemperature` | 3903 | Analog | °C | 3 |
| `ComfoBox/Status/Solar/CollectorDeltaTemperature` | 264 | Analog | K | 2 |
| `ComfoBox/Status/Solar/EnergyTotal` | 36 | Analog | kWh | 3 |
| `ComfoBox/Status/Solar/EnergyToday` | 3911 | Analog | kWh | 3 |

### 5.8 Status – Eingänge (optional)

Die **tatsächlichen** Zustände der Eingänge (§7 des Handbuchs). Nur sinnvoll, wenn die
Beschriftung der Anlage bekannt ist – daher optional und standardmässig **aus**.

| Topic | DP | Typ | Einheit | P |
|---|---:|---|---|---:|
| `ComfoBox/Status/Inputs/E1` … `E10` | 2613…2622 | Analog | °C / % | 3 |
| `ComfoBox/Status/Inputs/E21` … `E28` | 2649…2656 | Analog | °C / % | 3 |

> **Seit Phase 9 (2026-11-15) teilweise erledigt:** `States/Outputs/R1…R10`, `Y1` und
> `Y2` zeigen im V1-Katalog bereits auf die dokumentierten DPs **2779…2790**
> ([Phase 9, Aufgabe 9.7](06-aufgaben/phase-9-datenpunkt-korrekturen.md#97--r6-statesoutputs-auf-die-dokumentierten-dps)).
> Ein zusätzliches `Status/Outputs/*` würde dieselben Datenpunkte ein zweites Mal
> publizieren und in Home Assistant doppelte Entitäten erzeugen. **Deshalb entfallen
> `Status/Outputs/Y1`, `Y2` und `R1…R10` in V2.** Es bleiben die Eingänge `E1…E10` und
> `E21…E28` – sie sind optional und standardmässig aus.
>
> Die alten DPs 2624…2635 sind im Handbuch unbelegt und werden nicht mehr verwendet.

### 5.9 Config – zusätzliche Einstellungen (schreibbar)

| Topic (+ `/Set`) | DP | Typ | Bereich | Einheit |
|---|---:|---|---|---|
| `ComfoBox/Config/EnergyProducer/SwitchingDifferenceStage2` | 141 | Analog | 2.0…20.0 | K |
| `ComfoBox/Config/EnergyProducer/RestartDelayStage2` | 847 | Analog | 0.0…60.0 | min |
| `ComfoBox/Config/EnergyProducer/BivalenceStage2` | 803 | Analog | −40.0…60.0 | °C |
| `ComfoBox/Config/EnergyProducer/CondenserPumpPreRunningTime` | 3234 | Analog | 0.0…99.0 | min |
| `ComfoBox/Config/EnergyProducer/CondenserPumpPostRunningTime` | 188 | Analog | 0.0…99.0 | min |
| `ComfoBox/Config/EnergyProducer/FrostProtectionMode` | 498 | Analog | 0/1/2/3/10 | |
| `ComfoBox/Config/EnergyProducer/FrostProtectionTemperature` | 446 | Analog | 2.0…20.0 | °C |
| `ComfoBox/Config/Warmwater/Equipment` | 117 | Analog | 0/1/2 | |
| `ComfoBox/Config/Warmwater/PowerSelection` | 195 | Analog | 0…4 | |
| `ComfoBox/Config/Warmwater/SwitchOverDelay` | 848 | Analog | 0.0…10.0 | min |
| `ComfoBox/Config/Cooling/PassiveMode` | 579 | Analog | 0/1/2 | |
| `ComfoBox/Config/Cooling/Release` | 571 | Analog | 0…6 | |
| `ComfoBox/Config/General/CoolingEnabled` | 3118 | Analog | 0/1 | |
| `ComfoBox/Config/General/PassiveCoolingHydraulics` | 572 | Analog | 0/1/2/3/4/9 | |
| `ComfoBox/Config/HeatingSystem/SetPointNormalBelowOutdoor` | 862 | Analog | −30.0…10.0 | °C |
| `ComfoBox/Config/Ventilation/FanControl` | 3092 | Analog | 0/1/2 | |
| `ComfoBox/Config/Ventilation/SpeedStage1` | 3224 | Analog | 0.0…100.0 | % |
| `ComfoBox/Config/Ventilation/SpeedStage2` | 3225 | Analog | 0.0…100.0 | % |
| `ComfoBox/Config/Ventilation/SpeedStage3` | 3226 | Analog | 0.0…100.0 | % |
| `ComfoBox/Config/Service/Interval` | 3365 | Analog | | h |

> **Nicht doppelt anlegen:** `Config/EnergyComsumer/HeatPump/RestartDelay` (V1, DP 846) ist
> bereits die Wiedereinschaltverzögerung der ersten Stufe (Phase 9, R3). Ein zusätzliches
> `Config/EnergyProducer/RestartDelayStage1` würde denselben Datenpunkt ein zweites Mal
> publizieren. Deshalb gibt es nur `RestartDelayStage2` (DP 847) als neues Topic.
>
> **Alle** Topics in dieser Tabelle sind neu und **schreibbar**. Sie tragen
> `RequiresExpertMode = true` (siehe §2.5): ohne Experten-Modus gibt es kein `/Set`-Topic,
> keine `number`/`select`-Entität in Home Assistant und keinen REST-Schreibpfad. Das ist
> verbindlich – die Werte ändern das Regelverhalten der Anlage.
>
> In Home Assistant höchstens als `number`/`select` mit `entity_category: config` und
> `entity_registry_enabled_default: false`.

### 5.10 Command – Aktionen

| Topic | DP | Payload | Wirkung |
|---|---:|---|---|
| `ComfoBox/Command/ResetHeatingCurveAdaption` | 678 | `1` | Löscht die gesammelte Heizkennlinien-Adaption |
| `ComfoBox/Command/DoHeatWaterNow` | 2590 | `1` | Erzwingt eine WW-Ladung (V1-kompatibel als `Warmwater/DoHeatWaterNow`) |
| `ComfoBox/Command/Restart` | — | `1` | Neustart des **Gateways** (nicht des Reglers) |

> **Schreiben nur mit Experten-Modus:** `Command/ResetHeatingCurveAdaption` ist neu und
> schreibbar; er trägt deshalb ebenfalls `RequiresExpertMode = true`. `Command/Restart`
> ist der bestehende Gateway-Neustart (kein Geräte-Schreibvorgang) und bleibt unverändert.
> `Warmwater/DoHeatWaterNow` ist V1 und behält sein Verhalten.
>
> **Nicht möglich:** „Manuell Abtauen" hat im Handbuch **keine** DP-Nummer. Die Funktion
> existiert nur als Digitalfunktion (Wert `26`) an einem Ausgang oder als Menüpunkt am
> Gerät. Ein Abtau-Command ist über BACnet **nicht** umsetzbar – das gehört in die Doku,
> damit es niemand versucht.

### 5.11 Enum-Namen (neu)

Neue Enums, die zu den V2-Topics gehören. Die Namen werden Teil des Vertrags.

| Enum | Werte |
|---|---|
| `ZoneSetPointTypes` | `FrostProtection` 0 · `Reduced` 1 · `Normal` 2 · `Legionella` 3 |
| `ZoneOverrides` | `None` 0 · `Party` 1 · `Eco` 2 · `Holiday` 3 · `RemoteControl` 4 · `ExternalInput` 5 · `Commissioning` 6 · `DewPointGuard` 7 · `FloorDrying` 8 · `HeatPumpFault` 9 · `OptimizedHeatPumpShutdown` 10 · `SolarSetPointRise` 11 · `SetPointShiftDeltaT` 13 · `TimesOff` 14 · `OutdoorTemperature` 18 · `SecondaryCircuitVenting` 21 · `AdditionalHeaterOnly` 22 |
| `WarmWaterSetPointTypes` | `FrostProtection` 0 · `Reduced` 1 · `Normal` 2 · `Legionella` 3 |
| `WarmWaterStates` | `Off` 0 · `WithEnergyProducer` 1 · `Electrical` 2 · `WithAdditionalHeater` 3 · `DischargeProtection` 4 · `EnergyProducerLock` 5 |
| `WarmWaterOverrides` | `None` 0 · `Party` 1 · `Holiday` 3 · `ExternalInput` 5 · `HeatPumpFault` 9 · `Optimization` 10 · `SetPointShiftDeltaT` 13 · `TimesOff` 14 · `FlowTemperatureTooHigh` 15 · `Electrical` 17 · `OptimizationElectrical` 19 · `EnergyProducerLock` 20 · `AdditionalHeaterOnly` 22 |
| `BufferTypes` | `None` 0 · `OneSensor` 1 · `TwoSensors` 11 |
| `SolarHydraulicsTypes` | `None` 0 · `PumpToBuffer` 1 · `PumpToWarmWater` 2 · `PumpToBufferWithHeatExchanger` 4 · `PumpWarmWaterThenBuffer` 21 · `PumpToWarmWaterWithHeatExchanger` 22 · `PumpWarmWaterThenBufferWithHeatExchanger` 24 |
| `PresenceTypes` | `NotInstalled` 0 · `Installed` 1 |

> **Entfallen:** `EnergyMeasurementModes` – der Energie-Block ist nicht Teil des
> Katalogs ([ADR-015](05-entscheidungen/adr-015-baugruppen-verfuegbarkeit.md)).

> **Regel:** Ein Enum-Wert, den das Gerät liefert, aber das Enum nicht kennt, wird **nicht**
> auf `0` abgebildet (Befund A6). Der Publish wird verworfen und ein Fehler protokolliert.

## 6 Korrekturen am V1-Katalog

Diese Punkte sind am **2026-11-15 entschieden** und in
[Phase 9](06-aufgaben/phase-9-datenpunkt-korrekturen.md) **umgesetzt** (2026-11-15). Sie
stehen hier, weil
sie den Bestand betreffen – nicht die V2-Topics.

| # | Item | Befund | Korrektur | Aufgabe |
|---|---|---|---|---|
| 1 | `Config/Comfofond/HeatingHysteresis` | DP 3452 = „KM2-Ausgang bei Error" | DP **3432** | 9.2 |
| 2 | `Config/EnergyComsumer/Warmwater/Entladeschutz` | Katalog 0…1, Gerät kennt 0 und **3** | Max **3** + Werte `{0, 3}` | 9.3 |
| 3 | `Config/PrimaryPump/StoppedMode` | Katalog 0…1, Gerät kennt 0/1/**11** | Max **11** + Werte `{0, 1, 11}` | 9.4 |
| 4 | `Config/EnergyComsumer/CoolingZone/Cooling` | Katalog 0…3, Gerät kennt 0/1/**3** | Werte `{0, 1, 3}` | 9.5 |
| 5 | `Config/EnergyComsumer/HeatPump/RestartDelay` | DP 864 = Kühl-Offset, nicht die Verzögerung | DP **846**; Untergrenze **20 min bleibt** | 9.6 |
| 6 | `States/Outputs/R1, R3, R4, R5, R7, R10` | DPs 2624…2633 nicht im Handbuch | auf **2779…2788** | 9.7 |
| 7 | `States/Outputs/Y1PrimaryPump`, `Y2` | DPs 2634/2635 nicht im Handbuch | auf **2789/2790**, dazu **read-only** | 9.7 |
| 8 | `Tests/OutputsInactive`, `R4`, `Y1`, `Y2` | schalten echte Ausgänge | **vollständig read-only** | 9.9 |
| 9 | `Warmwater/SetPointNormal·Reduced·Frost·Legio` | ohne Obergrenze | Max **58 °C** (WP-Grenze) | 9.10 |
| 10 | `States/Inputs/E6…`, `E7…` | Namen um eins verschoben (`2619 = E7`) | **umbenennen** in `E7…`, `E8…` | 9.8 |

**Keine Änderung** (bewusst entschieden): `FlowMaxTemperature` (444) und
`SollwertMaximalWP` (447) behalten **Max 58** – das ist die Grenze der Wärmepumpe, nicht des
Reglers. `Config/PrimaryPump/*` bleibt so benannt (R11, nur ein Hinweis).

> **Wichtig:** Punkt 1 bis 9 sind **Metadaten** (Objekt-Id, Bereich, Werteliste,
> Schreibbarkeit). Sie ändern die Topic-Adresse nicht. Nur Punkt **10** ändert zwei
> Adressen – er braucht deshalb CHANGELOG-Hinweis und HA-Migration.
> Der Contract-Test vergleicht `Min`/`Max`/`IsReadOnly` **feldweise**; die Fixture
> `docs/topics.json` wird über den Generator nachgezogen, nie von Hand.

## 7 Home-Assistant-Discovery

| Regel | Umsetzung |
|---|---|
| `unique_id` ASCII | `DiscoveryId.Slug` transliteriert (ä→ae, ö→oe, ü→ue, ß→ss) |
| Geräte-Discovery | **kein** `homeassistant/device/…` mehr – Gerätekarte entsteht aus dem `device`-Block der Entitäten |
| Einheit | aus dem Katalog, `°C`, `kWh`, `kW`, `%`, `h`, `min`, `K` |
| `state_class` | `measurement` für Temperaturen/Leistung, `total_increasing` für kWh-Zähler |
| Kategorie | `entity_category: diagnostic` für Zähler, Service, Fehler; `config` für `Config/*` |
| `entity_registry_enabled_default: false` | für alle Topics mit Poll-Priorität `3` (Zähler, Ein-/Ausgänge) |
| Neue schreibbare Topics | ohne Experten-Modus **keine** `number`/`select`-Discovery; das Item erscheint als `sensor` |
| Energie/COP | **entfällt** – die ComfoBox hat keinen Wärmemengenzähler (ADR-015) |
| Baugruppen-Fühler | `availability_topic` = `ComfoBox/Status/Hardware/…` (`1`/`0`); ohne Baugruppe wird das Item **nicht gelesen** und sein Wert nicht publiziert |

> **Warum `enabled_default: false`:** jedes aktivierte Topic erzeugt einen zusätzlichen
> Recorder-Eintrag in Home Assistant. 60 neue Sensoren, die sich einmal pro Stunde ändern,
> sind Ballast. Der Nutzer aktiviert, was er braucht.

## 8 Migrationsplan

| Schritt | Inhalt | Abhängigkeit |
|---|---|---|
| 1 | `topics-v2.md` beschliessen (inkl. ADR-014 zur Namensraum-Weitung) – **entschieden 2026-11-16** | — |
| 2 | Katalog-Metadaten korrigieren (§6 Punkt 1–6) – **Phase 9, erledigt** | Schritt 1 |
| 3 | Enum-Klassen für die neuen Status-Topics ergänzen | Schritt 2 |
| 4 | `PollPlan` um die neuen Prioritäten erweitern (P1 60 s · P2 5 min · P3 1 h) | Schritt 3 |
| 5 | Discovery um die neuen Entitäten erweitern, `enabled_default` je Priorität | Schritt 4 |
| 6 | UI: Diagnoseseite um „Zustand/Warum"-Block erweitern | Schritt 5 |
| 7 | Benutzerdoku: neue Seite „Warum bleibt ein Raum kalt" | Schritt 6 |
| 8 | Contract-Test: V1-Fixture **unverändert**, V2 als **zweite** Fixture | durchgehend |

**Nicht** Teil von V2:

- Schaltuhren und Ferienprogramme (keine DP-Nummern im Handbuch)
- Fehlerspeicher (keine DP-Nummern belegt)
- Zonen 2…7 (nur über das Gerät selbst adressierbar)
- „Manuell Abtauen" (keine DP-Nummer)
- **Energie und COP** (DP 3340–3357): die ComfoBox hat keinen Wärmemengenzähler
  ([ADR-015](05-entscheidungen/adr-015-baugruppen-verfuegbarkeit.md))
- **`Config/HeatingSystem/MixerDirectOffset`** (DP 3608): das Objekt existiert am Regler
  nicht (`ERROR_CODE_UNKNOWN_OBJECT`) und erzeugte bei jedem Poll einen Fehler

## 9 Auswirkungen auf den Bus

Der MS/TP-Bus läuft mit **38400 Baud**; ein Read-Request/Antwort-Zyklus dauert je nach
Token-Haltezeit 50…120 ms. 60 zusätzliche Topics dürfen deshalb **nicht** einfach in den
bestehenden Zyklus gelegt werden.

| Poll-Priorität | Topics | Intervall | Reads/h |
|---|---|---:|---:|
| 1 (häufig) | 11 | **60 s** | 660 |
| 2 (mittel) | 24 | 5 min | 288 |
| 3 (selten) | 25 | 1 h | 25 |
| **Summe** | **60** | | **≈ 970** |

Bei 100 ms pro Transaktion sind das ≈ 1,6 Minuten Buszeit pro Stunde – unkritisch.
**Aber:** die Priorität beeinflusst die Reaktionszeit der V1-Bedien-Topics. Der `PollPlan`
muss so erweitert werden, dass Priorität 1 weiterhin zuerst und vollständig bedient wird.

> **Warum 60 s und nicht 30 s:** `Gateway:PollIntervalSeconds` steht per Default auf 60 s.
> Ein 30-s-Intervall würde die Last der Priorität 1 verdoppeln (1320 statt 660 Reads/h) und
> die Reaktionszeit der V1-Bedien-Topics verschlechtern, ohne dass die Diagnose davon
> profitiert. **60 s sind deshalb verbindlich für Priorität 1.**
>
> Die Zahl **60** ist die Obergrenze aus dem Entwurf. Nach der Bereinigung in
> [Phase 10](06-aufgaben/phase-10-topics-v2.md) (keine doppelten Output-Topics, optionale
> E-Topics) bleiben **56** BACnet-Lesetopics; die Buslast sinkt entsprechend.

## 10 Entscheidungen

| # | Frage | Entscheidung |
|---|---|---|
| 1 | Dürfen neue **schreibbare** Topics unter `Config/` liegen (statt nur `Status/`/`Command/`)? | **Ja** – sonst wird das Lese-/Schreibpaar auseinandergerissen. **Alle** neuen schreibbaren Topics nur mit Experten-Modus. → [ADR-014](05-entscheidungen/adr-014-topics-v2-namensraum-und-expertenmodus.md) |
| 2 | Werden die Korrekturen aus §6 sofort umgesetzt oder erst mit V2? | **Sofort für Punkt 2–6** (reine Bereichsfehler, kein Risiko), Punkt 1 und 7–9 erst nach einer Messung am echten Gerät |
| 3 | Werden `States/Outputs/*` (DP 2624…2635) abgeschafft? | **Nein** – sie bleiben V1. `Status/Outputs/*` entfällt in V2 (Duplikat, siehe §5.8) |
| 4 | Sollen Zähler standardmässig aktiviert sein? | **Nein** – `enabled_default: false` |
| 5 | Fehlerspeicher über eine freie Register-/Analogmodul-Konfiguration lesbar machen? | Nur wenn der Nutzer das am Gerät einrichtet – **nicht** automatisch |
| 6 | Poll-Intervall für Priorität 1? | **60 s** (Default `Gateway:PollIntervalSeconds`) – nicht 30 s |
| 7 | Wie werden kWh/COP ohne DP 3340 behandelt? | **Gar nicht** – der Block ist nicht im Katalog. Die ComfoBox hat keinen Wärmemengenzähler, die Items lieferten dauerhaft `0`. → [ADR-015](05-entscheidungen/adr-015-baugruppen-verfuegbarkeit.md) |
| 8 | Werden `Status/Outputs/*` zusätzlich zu `States/Outputs/*` angelegt? | **Nein** – Duplikate. `States/Outputs/*` deckt 2779…2790 bereits ab |
