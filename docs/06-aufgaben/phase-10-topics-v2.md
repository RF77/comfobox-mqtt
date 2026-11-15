# Phase 10 – Topics V2: Diagnose, Energie und Experten-Konfiguration

> **Status:** ⬜ offen · **Aufwand:** ≈ 12,5 PT · **Grundlage:**
> [topics-v2.md](../topics-v2.md),
> [ADR-014](../05-entscheidungen/adr-014-topics-v2-namensraum-und-expertenmodus.md)
>
> **Entschieden am 2026-11-16:**
>
> | # | Entscheidung |
> |---|---|
> | 1 | Poll-Priorität 1 bleibt bei **60 s** – nicht 30 s. |
> | 2 | Die neuen **Lesetopics** werden wie in [topics-v2.md §5](../topics-v2.md#5-neue-topics-v2) vorgeschlagen umgesetzt. |
> | 3 | **Alle neuen schreibbaren Topics** tragen `RequiresExpertMode = true`. |
> | 4 | Neue schreibbare Konfigurationswerte bleiben unter `Config/` (ADR-014). |
> | 5 | **DP 3340** wird ausgewertet: ohne Wärmemengenmessung werden kWh/COP **nicht als 0** publiziert. |
>
> **V1 bleibt eingefroren.** `docs/topics.json` und `docs/topics.md` werden **nicht**
> angefasst. V2 bekommt eine eigene Fixture (`docs/topics-v2.json`).

## 1 Ziel

V2 ergänzt den bestehenden Vertrag um die Werte, die eine Wärmepumpe erst erklärbar
machen: **Warum heizt es nicht?**, **Wie effizient läuft sie?** und **Wird es schlechter?**

| Ziel | Prüfbar durch |
|---|---|
| **56** neue BACnet-Lesetopics (Diagnose, Kältekreis, Puffer, Energie, Zähler, Solar) | Katalog-Test, V2-Contract-Test |
| **21** neue schreibbare Konfigurations-Topics – **nur mit Experten-Modus** | Katalog-Test, `BacnetDeviceTests`, `MqttTopicMapTests` |
| **1** neues Command-Topic (`ResetHeatingCurveAdaption`) – nur mit Experten-Modus | Katalog-Test, MQTT-Test |
| **7** abgeleitete Topics unter `Status/Derived/*` (keine BACnet-Objekte) | `DerivedValueTests` |
| **DP 3340-Gate:** ohne Wärmemengenmessung keine 0-Werte, klare Fehlermeldung | `EnergyMeasurementTests` |
| **Priorität 1 = 60 s**, Priorität 2 = 5 min, Priorität 3 = 1 h | `PollPlanTests`, Buslast-Rechnung |
| **V1 unverändert:** 127 Items, Fixture byte-identisch | `TopicContractTests`, `--check` |
| Keine Kollision mit den Gateway-Status-Topics | Reservierte-Pfade-Test |

## 2 Entscheidungen und ihre Folgen

| Entscheidung | Folge | Wo dokumentiert |
|---|---|---|
| P1 bleibt **60 s** | 11 statt 22 Reads/min für die neuen P1-Topics; V1-Bedien-Topics bleiben reaktionsschnell | [topics-v2.md §9](../topics-v2.md#9-auswirkungen-auf-den-bus) |
| Lesetopics wie vorgeschlagen | 56 neue BACnet-Items; keine Änderung an V1 | [topics-v2.md §5](../topics-v2.md#5-neue-topics-v2) |
| Neue Schreib-Topics nur mit Experten-Modus | Ohne Experten-Modus existiert kein `/Set`-Topic; HA zeigt das Item als `sensor` | [ADR-014](../05-entscheidungen/adr-014-topics-v2-namensraum-und-expertenmodus.md) |
| `Config/` bleibt für schreibbare Werte erlaubt | Lesen/Schreiben bleibt symmetrisch (`Config/X/Y` + `/Set`) | [ADR-014](../05-entscheidungen/adr-014-topics-v2-namensraum-und-expertenmodus.md) |
| DP 3340 wird geprüft | Energie-Topics werden bei `None` nicht publiziert; `Available = 0`; Fehler + UI-Hinweis | [Verbesserungen §6.1](../07-technikerhandbuch/verbesserungen-und-neue-topics.md#61-dp-3340--warum-ohne-wärmemengenmessung-keine-kwh-und-kein-cop-entstehen) |
| V2 bekommt eine eigene Fixture | `docs/topics.json` bleibt byte-identisch; Contract-Test filtert `IsV2` | diese Datei, §5.1 |

## 3 Vorbedingungen

| Bedingung | Prüfung |
|---|---|
| Phase 9 ist abgeschlossen | [status.md](status.md) – alle 9.x `✅` |
| Sauberer Stand | `git status` ohne Änderungen |
| Grüne Basis | `dotnet test src/ComfoBox.sln -c Release` |
| Node.js 22 | `node --version` |
| .NET SDK 10.0.400 | `dotnet --version` (aus `src/`) |
| Docker **nicht** nötig | diese Phase ist reine Gateway-/Domain-Arbeit |

**Vor der ersten Änderung** die Testzahl notieren – sie ist die Vergleichsbasis:

```bash
dotnet test src/ComfoBox.sln -c Release 2>&1 | Select-String "Passed!|Fehler!|Failed!"
```

## 4 Lesereihenfolge

1. [topics-v2.md](../topics-v2.md) – §5 (die neuen Topics), §7 (Discovery), §9 (Bus), §10 (Entscheidungen)
2. [ADR-014](../05-entscheidungen/adr-014-topics-v2-namensraum-und-expertenmodus.md) – Namensraum und Experten-Modus
3. [datenpunkt-register.md](../07-technikerhandbuch/datenpunkt-register.md) – §4.8 (Energie), §6.3.14 (DP 3340), §7 (Ausgänge)
4. [verbesserungen-und-neue-topics.md](../07-technikerhandbuch/verbesserungen-und-neue-topics.md) – §3 (abgeleitete Werte), §6.1 (DP 3340)
5. [phase-9-datenpunkt-korrekturen.md](phase-9-datenpunkt-korrekturen.md) – §4/§5 (Generator, `src_old/`-Encoding)
6. [mqtt-kompatibilitaet.md](../04-migration/mqtt-kompatibilitaet.md) – §2.3 (Schreibbarkeit), §1.2 (Status-Topics)
7. [.github/copilot-instructions.md](../../.github/copilot-instructions.md) – Projektregeln

## 5 Architektur

### 5.1 Warum V2 eine zweite Fixture braucht

Der Contract-Test vergleicht heute **alle** Katalog-Items mit `docs/topics.json` und
erwartet exakt **127** Einträge. Würde man die V2-Items einfach in denselben Katalog
schreiben, wäre der V1-Vertrag gebrochen – obwohl sich kein einziges V1-Topic ändert.

Deshalb gilt:

```
Catalog.All
├── IsV2 = false  →  V1, 127 Items  →  docs/topics.json      (eingefroren)
└── IsV2 = true   →  V2, 78 Items   →  docs/topics-v2.json   (neu)
```

| Datei | Rolle |
|---|---|
| `docs/topics.json` | **V1-Vertrag**, byte-identisch. Wird **nie** von Hand geändert. |
| `docs/topics-v2.json` | **V2-Fixture** – nur die neuen Items. Wird vom Generator erzeugt. |
| `docs/topics.md` | V1-Dokumentation, eingefroren (Archivkopie in `src_old/docs/topics.md`). |

> **Regel:** Ein V2-Item ist **additiv**. Es darf kein V1-Topic ersetzen, umbenennen oder
> in seiner Bedeutung ändern. Die einzige Ausnahme bleibt die bereits umgesetzte
> Umbenennung aus Phase 9 (`E6…`/`E7…` → `E7…`/`E8…`).

### 5.2 Katalogmarkierung und Generator

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Domain/Items/ItemDefinition.cs` | `public bool IsV2 { get; init; }` (Default `false`) |
| `src/ComfoBox.Domain/Items/ItemCatalog.cs` | `V1` und `V2` als gefilterte Listen (Convenience, keine neue Quelle) |
| `src_old/ComfoBoxLib/Attributes/V2Attribute.cs` | **neu**: `[AttributeUsage(AttributeTargets.Property)] public sealed class V2Attribute : Attribute` – nur Deklaration, wird nie kompiliert |
| `src_old/ComfoBoxLib/Attributes/EnergyAttribute.cs` | **neu**: markiert Items, die ohne DP 3340 nicht publiziert werden dürfen |
| `src_old/ComfoBoxLib/Items/Status.cs` | **neu**: alle V2-Lesetopics (Gruppen `Status/…`) |
| `src_old/ComfoBoxLib/Items/Config.cs` | V2-Ergänzungen mit `[V2]` **und** `[ExpertMode]` |
| `src_old/ComfoBoxLib/Items/Enums/*.cs` | neue Enums (siehe §5.3) |
| `tools/generate-topics/generate-topics.mjs` | `[V2]` und `[Energy]` parsen, `IsV2`/`RequiresEnergyMeasurement` ausgeben, `--out-v2` und `--check-v2` ergänzen |
| `src/ComfoBox.Domain/Items/Catalog.cs` | **generiert** – nicht von Hand bearbeiten |
| `src/ComfoBox.Domain/Items/ItemEnums.cs` | **generiert** – nicht von Hand bearbeiten |

**Generator-Regeln:**

- `V2_RE = /\[V2\]/gu` analog zu `EXPERT_RE`/`VALUES_RE` aus Phase 9.
- `ENERGY_RE = /\[Energy\]/gu` für die 11 Energie-/COP-Items.
- `--out` schreibt weiterhin **nur** V1 (`IsV2 == false`) nach `docs/topics.json`.
- `--out-v2` schreibt **nur** V2 (`IsV2 == true`) nach `docs/topics-v2.json`.
- `--check` prüft V1, `--check-v2` prüft V2.
- **`CLASS_FILES` erweitern:** `['Zone', 'Tests', 'Warmwater', 'States', 'Time', 'Config', 'Status']`.
  Ohne diesen Eintrag liest der Generator die neue `Status.cs` **nicht**.
- Die **V1-Reihenfolge** bleibt unverändert, weil die V1-Fixture `IsV2` herausfiltert.
  Die Reihenfolge von `Catalog.All` ist **kein** Vertrag; die neuen `Status`-Items stehen
  am Ende.

> **Windows-1252-Falle:** `src_old/ComfoBoxLib/Items/*.cs` ist Windows-1252. Neue
> Item-Dateien mit CP1252 lesen **und** schreiben; `git diff --stat src_old/` muss auf die
> geänderten Zeilen beschränkt bleiben. Details: [Phase 9, §5](phase-9-datenpunkt-korrekturen.md).

### 5.3 Neue Enums

Quelle: `src_old/ComfoBoxLib/Items/Enums/<Name>.cs` (der Generator liest alle Dateien).

| Enum | Werte | Verwendet von |
|---|---|---|
| `ZoneSetPointTypes` | `FrostProtection` 0 · `Reduced` 1 · `Normal` 2 · `Legionella` 3 | `Status/Zone/SetPointType` |
| `ZoneOverrides` | `None` 0 · `Party` 1 · `Eco` 2 · `Holiday` 3 · `RemoteControl` 4 · `ExternalInput` 5 · `Commissioning` 6 · `DewPointGuard` 7 · `FloorDrying` 8 · `HeatPumpFault` 9 · `OptimizedHeatPumpShutdown` 10 · `SolarSetPointRise` 11 · `SetPointShiftDeltaT` 13 · `TimesOff` 14 · `OutdoorTemperature` 18 · `SecondaryCircuitVenting` 21 · `AdditionalHeaterOnly` 22 | `Status/Zone/Override` |
| `WarmWaterSetPointTypes` | `FrostProtection` 0 · `Reduced` 1 · `Normal` 2 · `Legionella` 3 | `Status/WarmWater/SetPointType` |
| `WarmWaterStates` | `Off` 0 · `WithEnergyProducer` 1 · `Electrical` 2 · `WithAdditionalHeater` 3 · `DischargeProtection` 4 · `EnergyProducerLock` 5 | `Status/WarmWater/State` |
| `WarmWaterOverrides` | `None` 0 · `Party` 1 · `Holiday` 3 · `ExternalInput` 5 · `HeatPumpFault` 9 · `Optimization` 10 · `SetPointShiftDeltaT` 13 · `TimesOff` 14 · `FlowTemperatureTooHigh` 15 · `Electrical` 17 · `OptimizationElectrical` 19 · `EnergyProducerLock` 20 · `AdditionalHeaterOnly` 22 | `Status/WarmWater/Override` |
| `EnergyMeasurementModes` | `None` 0 · `On` 1 · `OnSeparateWarmWater` 2 | `Status/Energy/MeasurementMode` |

> **Enum-Regel (Befund A6):** Ein Wert, den das Gerät liefert, aber das Enum nicht kennt,
> wird **nicht** auf `0` abgebildet. Der Publish wird verworfen und ein Fehler
> protokolliert. Die Lücken (z. B. `12`, `15`…`17` bei `ZoneOverrides`) bleiben erhalten.

### 5.4 Reservierte Topic-Pfade

Der Namensraum `Status/` ist **geteilt**: Gateway-Status und V2-Items. Diese Pfade sind
reserviert und dürfen **nie** als Item-Topic auftauchen:

```
ComfoBox/Connected
ComfoBox/Version
ComfoBox/Special/NumberOfWritesPer24h
ComfoBox/Status/Health
ComfoBox/Status/Version
ComfoBox/Status/Uptime
ComfoBox/Status/StartedAt
ComfoBox/Status/Writes
ComfoBox/Status/WritesMax
ComfoBox/Status/Bacnet
ComfoBox/Status/Mqtt
ComfoBox/Status/LastError
ComfoBox/Status/Errors
ComfoBox/Status/Restart
ComfoBox/Status/Energy/Available     ← neu in V2 (Gate, kein Katalog-Item)
ComfoBox/Command/Restart
```

**Test:** `CatalogTests.Reservierte_Gateway_Topics_Sind_Keine_Items` prüft, dass kein
`ItemDefinition.TopicPath` einem dieser Pfade entspricht (ohne `BaseTopic`). Der Test
verhindert, dass ein Item den Health- oder Fehler-Status überschreibt.

### 5.5 Abgeleitete Werte sind keine Katalog-Items

Die sieben `Status/Derived/*`-Topics haben **kein** BACnet-Objekt. Sie dürfen deshalb nicht
als `ItemDefinition` im Katalog liegen (`BacnetObjectId` ist Pflicht und muss `> 0` sein).

| Typ | Aufgabe |
|---|---|
| `DerivedValueDefinition` (Domain) | Id, TopicPath, Einheit, Formel-Beschreibung, benötigte Eingabe-Ids |
| `DerivedValueService` (Gateway) | rechnet aus dem `IValueCache` und publiziert über `IMqttPublisher` |
| `HomeAssistantDiscovery` | bekommt eine zweite Liste (`DerivedValues`) und publiziert sie als `sensor` |

**Regeln (aus [Verbesserungen §3](../07-technikerhandbuch/verbesserungen-und-neue-topics.md#3-abgeleitete-werte-der-gateway-rechnet-nicht-das-gerät)):**

- Nur rechnen, wenn **beide** Eingangswerte im gleichen Zyklus gelesen wurden – sonst
  `unknown` publizieren, **keine 0**.
- Division nie ohne Nenner-Prüfung (DP 3060 kann 0 sein).
- Ein abgeleiteter Wert ist **nie** schreibbar.
- Jede Formel steht im Topic-Dokument und als UI-Tooltip.

### 5.6 Experten-Modus

| Ort | Verhalten |
|---|---|
| `ItemDefinition` | neue schreibbare V2-Items: `IsReadOnly = false`, `RequiresExpertMode = true` |
| `MqttTopicMap` | ohne Experten-Modus **kein** `/Set`- und kein `/AsNumber/Set`-Binding |
| `MqttSubscriber` | ohne Experten-Modus wird das `/Set`-Topic gar nicht abonniert |
| `BacnetDevice.WriteAsync` | prüft `RequiresExpertMode` **bei jedem** Schreibvorgang (bereits vorhanden) |
| `HomeAssistantDiscovery` | ohne Experten-Modus `sensor` statt `number`/`select` |
| `ItemsEndpoints` / `ItemDto` | `requiresExpertMode = true`, `writable` nur mit Experten-Modus |
| UI „Werte“ | Experten-Items sind ohne Experten-Modus gesperrt und als „Experten-Modus“ markiert |
| UI „Konfiguration“ | neue Config-Topics erscheinen erst hinter dem Experten-Modus-Schalter |

> **Laufzeit-Umschaltung:** Der Experten-Modus wirkt über `GatewayRuntimeSettings` sofort
> für REST-Schreibvorgänge. Für MQTT wird er heute **beim Start** gebunden. Aufgabe 10.13
> schliesst diese Lücke: `/Set`-Topics und Discovery folgen dem Schalter zur Laufzeit.

### 5.7 Poll-Prioritäten und Buslast

| Priorität | Intervall | Bedeutung | Umsetzung |
|---|---:|---|---|
| `Once` | Start | Gerätetyp, Softwareversion | vorhanden |
| `Primary` | **60 s** | Diagnose, Bedienung | vorhanden |
| `Secondary` | 5 min (`SecondaryPollFactor = 5`) | Kältekreis, Puffer, Energie-Leistung | vorhanden |
| `Slow` | **1 h** | Zähler, Service, Solar, DP 3340 | **neu** (`RefreshPriority.Slow`) |

`BacnetPollingService` berechnet den Slow-Faktor aus dem konfigurierten Poll-Intervall:

```csharp
var slowFactor = Math.Max(1, (int)Math.Round(3600.0 / gatewayOptions.PollIntervalSeconds));
```

Damit bleibt „P3 = stündlich“ auch dann richtig, wenn der Nutzer
`Gateway:PollIntervalSeconds` ändert.

**Buslast (nur neue V2-Topics, 60 s):**

| Priorität | Topics | Reads/h |
|---|---:|---:|
| 1 | 11 | 660 |
| 2 | 24 | 288 |
| 3 | 25 | 25 |
| **Summe** | **60** | **≈ 970** |

Bei 100 ms pro Transaktion ≈ **1,6 min Buszeit pro Stunde**. Nach der Bereinigung
(56 statt 60 Topics, siehe §5.8) sinkt die Last weiter. Die V1-Last bleibt unverändert:
29 primäre + 1 Item ohne Priorität pro Zyklus, 93 sekundäre alle 5 Zyklen.

### 5.8 Bereinigung gegenüber dem Entwurf

Zwei Blöcke aus [topics-v2.md](../topics-v2.md) werden **nicht** umgesetzt, weil sie
Duplikate erzeugen würden:

| Entwurf | Warum nicht | Was stattdessen gilt |
|---|---|---|
| `Status/Counters/ServiceInterval` (DP 3365) | DP 3365 ist bereits `Config/Service/Interval` (lesbar + schreibbar) | `Config/Service/Interval` + `/Set` (Experten-Modus) |
| `Status/Outputs/Y1`, `Y2`, `R1…R10` (DP 2779…2790) | `States/Outputs/*` zeigt seit Phase 9 auf dieselben DPs | V1 `States/Outputs/*` bleibt die einzige Quelle |
| `Status/Inputs/E1…E10`, `E21…E28` | kein Duplikat, aber anlagenspezifisch und selten | **optional** (eigene Session, `enabled_default: false`) |

Damit bleiben **56** BACnet-Lesetopics für Phase 10 (ohne die optionalen E-Topics).

### 5.9 Nicht Teil dieser Phase

Das [Datenpunkt-Register](../07-technikerhandbuch/datenpunkt-register.md) enthält weitere
`➕`-Vorschläge, die **nicht** zum entschiedenen V2-Vertrag gehören. Sie sind bewusst
zurückgestellt:

| Vorschlag | Warum nicht in Phase 10 |
|---|---|
| `Status/Zone/Name` (670), `Status/Controller/Name` (1952), `Status/Controller/ManualMode` (812), `Status/Ventilation/FanMode` (3093) | Lesetopics ausserhalb von [topics-v2.md §5](../topics-v2.md#5-neue-topics-v2); eigene Entscheidung nötig |
| `Config/EnergyProducer/FixedValueControl` (921), `FixedSetPointHeating` (158), `FixedSetPointCooling` (577) | Festwertregelung ist ein eigenes Betriebsmodell – nicht Teil der V2-Liste |
| `Config/EnergyProducer/MinRunTimeStage2` (142) | sinnvoller Kandidat, aber nicht im entschiedenen Vertrag – eigene Session |
| `…/KondFrostWarnungTemp` (3471) | ergänzt die Frostschutz-Gruppe; eigene Session |
| `Status/Zone/OutdoorSensorSource` (169) | reine Diagnose, kein Bedarf in Phase 10 |
| `Status/Inputs/E1…E10`, `E21…E28` (18 Topics) | anlagenspezifisch, optional, `enabled_default: false` |
| `Config/Energy/MeasurementMode` (DP 3340 schreiben) | nur nach Verifikation am Gerät; der Gateway schreibt DP 3340 **nicht** automatisch |

> **Regel:** Diese Liste ist kein Backlog-Versprechen. Wer einen Vorschlag aufnimmt,
> ergänzt zuerst [topics-v2.md](../topics-v2.md) und dann eine eigene Aufgabe.

## 6 Aufgaben

### 10.1 – V1/V2-Trennung im Katalog und Generator

**Ziel:** V2-Items sind im Katalog markiert, ohne den V1-Vertrag zu berühren.

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Domain/Items/ItemDefinition.cs` | `IsV2` und `RequiresEnergyMeasurement` (Default `false`) |
| `src/ComfoBox.Domain/Items/ItemCatalog.cs` | `V1` / `V2` (gefilterte Listen) |
| `src_old/ComfoBoxLib/Attributes/V2Attribute.cs` | neue Attribut-Deklaration |
| `src_old/ComfoBoxLib/Attributes/EnergyAttribute.cs` | neue Attribut-Deklaration |
| `tools/generate-topics/generate-topics.mjs` | `[V2]`/`[Energy]` parsen, `IsV2`/`RequiresEnergyMeasurement` ausgeben, `--out-v2`/`--check-v2` |
| `src/ComfoBox.Domain/Items/Catalog.cs` | regenerieren |

**Schritte:**

1. Attribute in `src_old/ComfoBoxLib/Attributes/` anlegen (keine Kompilierung, nur Quelle).
2. `CLASS_FILES` im Generator um `'Status'` erweitern (am Ende).
3. Generator: `V2_RE`, `ENERGY_RE`, `v2BySegment`, `energyBySegment` – analog zu
   `expertsBySegment` aus Phase 9.
4. `emitCatalog`: nach `Priority` die Zeilen
   `IsV2 = true,` und `RequiresEnergyMeasurement = true,` ausgeben (nur wenn gesetzt).
5. `main`: `--out` filtert `!item.IsV2`; `--out-v2` filtert `item.IsV2`;
   `--check`/`--check-v2` analog.
6. `--catalog` erzeugt weiterhin **alle** Items (V1 + V2).

**Tests:**

| Datei | Test |
|---|---|
| `src/ComfoBox.Domain.Tests/CatalogTests.cs` | `Catalog.V1.Count == 127`; `Catalog.V2.Count == 78`; `Catalog.All.Count == 205` |
| `src/ComfoBox.Domain.Tests/CatalogTests.cs` | jedes V2-Item hat `IsV2 == true`; jedes V1-Item `IsV2 == false` |
| `tools/generate-topics` | `--check` und `--check-v2` Exit 0 |

**Abnahme:**

- [ ] `docs/topics.json` ist **byte-identisch** zu `HEAD`
- [ ] `docs/topics-v2.json` existiert und enthält **78** Items
- [ ] `Catalog.All` ist eindeutig (Ids und Topic-Pfade)
- [ ] `dotnet test` grün

> **78** = 56 Lesetopics + 21 Config-Topics + 1 Command-Topic. Die optionalen
> E-Topics (18) und ein optionaler DP-3340-Schreibzugriff sind **nicht** enthalten.

### 10.2 – Contract-Tests für V1 und V2

**Ziel:** Der V1-Vertrag bleibt unverändert; V2 wird separat geprüft.

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Contract.Tests/TopicContractTests.cs` | `Catalog.V1` statt `Catalog.All`; Anzahl bleibt **127** |
| `src/ComfoBox.Contract.Tests/V2TopicContractTests.cs` | **neu**: vergleicht `Catalog.V2` mit `docs/topics-v2.json` |
| `src/ComfoBox.Domain.Tests/CatalogTests.cs` | reservierte Gateway-Pfade (§5.4) |

**Tests:**

| Test | Erwartung |
|---|---|
| `Fixture_Und_Katalog_Haben_Die_Gleiche_Anzahl_Items` | V1: 127 |
| `V2_Fixture_Und_Katalog_Haben_Die_Gleiche_Anzahl_Items` | V2: 78 |
| `Jedes_V1_Item_Ist_In_Der_Fixture` | unverändert |
| `Jedes_V2_Item_Ist_In_Der_V2_Fixture` | neu |
| `Reservierte_Gateway_Topics_Sind_Keine_Items` | kein Treffer |

**Abnahme:**

- [ ] `dotnet test src/ComfoBox.Contract.Tests` grün
- [ ] `node tools/generate-topics/generate-topics.mjs --check` grün
- [ ] `node tools/generate-topics/generate-topics.mjs --check-v2` grün

### 10.3 – Diagnose Zone und Warmwasser (8 Topics)

**Ziel:** Die häufigste Support-Frage beantwortbar machen: *Warum heizt es nicht?*

| Topic | DP | Typ | P |
|---|---:|---|---:|
| `Status/Zone/FlowTemperature` | 14 | Analog °C | 1 |
| `Status/Zone/State` | 59 | Analog (Regler-intern) | 1 |
| `Status/Zone/SetPointType` | 398 | Enum `ZoneSetPointTypes` | 2 |
| `Status/Zone/Override` | 676 | Enum `ZoneOverrides` | 2 |
| `Status/WarmWater/TemperatureBottom` | 4 | Analog °C | 2 |
| `Status/WarmWater/SetPointType` | 397 | Enum `WarmWaterSetPointTypes` | 2 |
| `Status/WarmWater/State` | 2599 | Enum `WarmWaterStates` | 2 |
| `Status/WarmWater/Override` | 2598 | Enum `WarmWaterOverrides` | 2 |

**Ziel-Dateien:** `src_old/ComfoBoxLib/Items/Status.cs` (neu), Enums (§5.3),
`src/ComfoBox.Domain/Items/Catalog.cs` + `ItemEnums.cs` (generiert).

**Tests:**

- Katalog: 8 Items, alle `IsV2 = true`, alle `IsReadOnly = true`, richtige DPs.
- Enums: exakte Namen und Lücken (A6-Regel).
- MQTT: `Status/Zone/Override` publiziert den Enum-Namen, `…/AsNumber` die Zahl.

**Abnahme:**

- [ ] alle 8 Topics erscheinen in `docs/topics-v2.json`
- [ ] `Status/Zone/Override` liefert bei einem unbekannten Gerätewert **keinen** Publish
- [ ] `dotnet test` grün

### 10.4 – Kältekreis und Wärmepumpe (13 Topics)

| Topic | DP | Typ | P |
|---|---:|---|---:|
| `Status/HeatPump/EnergyProducerTemperature` | 21 | °C | 1 |
| `Status/HeatPump/ReturnTemperature` | 3468 | °C | 1 |
| `Status/HeatPump/PrimaryFlowTemperature` | 3209 | °C | 2 |
| `Status/HeatPump/EvaporatorTemperature` | 660 | °C | 2 |
| `Status/HeatPump/SuctionGasTemperature` | 641 | °C | 2 |
| `Status/HeatPump/HotGasTemperature` | 3228 | °C | 2 |
| `Status/HeatPump/CondenserTemperature` | 666 | °C | 2 |
| `Status/HeatPump/CondensationTemperature` | 3475 | °C | 3 |
| `Status/HeatPump/EvaporationTemperature` | 3598 | °C | 3 |
| `Status/HeatPump/HeatPumpStatusStage2` | 663 | Enum `HeatPumpStatusEnums` | 2 |
| `Status/HeatPump/DefrostDeltaActual` | 644 | K | 3 |
| `Status/HeatPump/DefrostDeltaReference` | 3110 | K | 3 |
| `Status/HeatPump/DefrostDeltaCorrected` | 643 | K | 3 |

**Tests:** Katalog-Metadaten, Enum-Wiederverwendung (`HeatPumpStatusEnums` existiert
bereits), Prioritäten, Einheiten.

**Abnahme:**

- [ ] 13 Items in `docs/topics-v2.json`
- [ ] `HeatPumpStatusStage2` nutzt exakt dieselben Enum-Namen wie `States/HeatPump/HeatPumpStatus`
- [ ] `dotnet test` grün

### 10.5 – Puffer und Solar (10 Topics)

| Topic | DP | Typ | P |
|---|---:|---|---:|
| `Status/Buffer/TemperatureTop` | 24 | °C | 2 |
| `Status/Buffer/TemperatureBottom` | 25 | °C | 2 |
| `Status/Buffer/SetPoint` | 281 | °C | 2 |
| `Status/Buffer/CoolingTemperatureTop` | 3925 | °C | 3 |
| `Status/Buffer/CoolingTemperatureBottom` | 3924 | °C | 3 |
| `Status/Solar/CollectorTemperature` | 28 | °C | 2 |
| `Status/Solar/CollectorHeatExchangerTemperature` | 3903 | °C | 3 |
| `Status/Solar/CollectorDeltaTemperature` | 264 | K | 2 |
| `Status/Solar/EnergyTotal` | 36 | kWh | 3 |
| `Status/Solar/EnergyToday` | 3911 | kWh | 3 |

**Abnahme:**

- [ ] 10 Items in `docs/topics-v2.json`
- [ ] `Status/Solar/*` ist auch ohne Sonnenkollektor unschädlich (Wert bleibt `unknown`)
- [ ] `dotnet test` grün

### 10.6 – DP 3340: Wärmemengenmessung erkennen und sperren

**Ziel:** Ohne Wärmemengenmessung werden kWh und COP **nicht als 0** publiziert.

**Neue Katalog-Items:**

| Topic | DP | Typ | P |
|---|---:|---|---:|
| `Status/Energy/MeasurementMode` | 3340 | Enum `EnergyMeasurementModes` | 3 |
| `Status/Energy/FlowRate` | 3341 | m³/h | 3 |
| `Status/Energy/FlowRateWarmWater` | 3348 | m³/h | 3 |

**Neue Gateway-Topics (kein Katalog-Item):**

| Topic | Payload | Retain |
|---|---|---|
| `Status/Energy/Available` | `1` / `0` | ja |

**Ziel-Dateien:**

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Domain/Items/ItemDefinition.cs` | `RequiresEnergyMeasurement` (aus 10.1) |
| `src/ComfoBox.Gateway/Mqtt/MqttTopicMap.cs` | `EnergyAvailableTopic` |
| `src/ComfoBox.Gateway/Energy/EnergyMeasurementGate.cs` | **neu**: Zustand aus dem Cache, `IsAvailable`, `UpdateAsync` |
| `src/ComfoBox.Gateway/Mqtt/MqttStatePublisher.cs` | Items mit `RequiresEnergyMeasurement` überspringen, wenn `!IsAvailable` |
| `src/ComfoBox.Gateway/Mqtt/MqttStatusPublisher.cs` | `Status/Energy/Available` publizieren (retained) |
| `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` | Gate nach jedem Zyklus aktualisieren |
| `src/ComfoBox.Gateway/Health/ErrorCollector.cs` | keine Änderung; Aufruf aus dem Gate |

**Verhalten:**

| Zustand | `Available` | Energie-Topics | Fehler |
|---|---|---|---|
| noch nie gelesen | **nicht publiziert** | nicht publiziert | – |
| `MeasurementMode = None` | `0` | nicht publiziert | `energy_measurement_disabled` (warning, **einmal pro Wechsel**) |
| `MeasurementMode = On` / `OnSeparateWarmWater` | `1` | publiziert | – |
| Lesefehler / Cache veraltet | `0` | nicht publiziert | `energy_measurement_unavailable` (warning) |

**Fehlertext (wörtlich):**

```text
Die Wärmemengenmessung ist am Gerät ausgeschaltet (DP 3340 = 0). Die kWh-Zähler und der
COP liefern deshalb keine Werte. Am Regler unter §6.3.14 „Wärmemengenmessung“ auf 1 oder 2
stellen und den Durchflussfühler prüfen (Status/Energy/FlowRate muss bei laufender Pumpe
größer 0 sein).
```

**Wichtig:**

- **Keine 0** für unbekannt. Auch nicht „einmal 0, dann unknown“.
- **Kein automatisches Schreiben** von DP 3340. Der Gateway kann nicht prüfen, ob ein
  Durchflussfühler installiert ist.
- **Beim Start** wird `Status/Energy/Available` einmal **geleert** (retained, leere
  Payload). Ein alter `1`-Wert aus der vorigen Sitzung darf die Entitäten nicht fälschlich
  als verfügbar zeigen.
- **Beim Wechsel auf `Available = 0`** werden die 11 retained Energie-Topics einmal
  geleert; beim nächsten gültigen Read werden sie neu publiziert. Sonst bleiben veraltete
  kWh-/COP-Werte im Broker stehen.
- Der optionale Schreibzugriff `Config/Energy/MeasurementMode` (DP 3340) ist **nicht**
  Teil dieser Aufgabe. Er kommt nur nach einer Verifikation am Gerät (eigene Session,
  Experten-Modus, Werte `{0, 1, 2}`).

**Tests:**

| Test | Erwartung |
|---|---|
| `EnergyMeasurementTests.ModeNone_Unterdrueckt_Alle_Energie_Topics` | kein Publish der 11 Items |
| `EnergyMeasurementTests.ModeOn_Publiziert_Energie_Topics` | Publish |
| `EnergyMeasurementTests.Unbekannter_Mode_Publiziert_Keine_0` | `Available` fehlt bzw. `0`, keine 0-Werte |
| `EnergyMeasurementTests.Fehler_Wird_Nur_Bei_Wechsel_Gemeldet` | genau ein `Status/Errors`-Eintrag |
| `EnergyMeasurementTests.Start_Leert_Availability` | kein alter `1`-Wert nach dem Start |
| `EnergyMeasurementTests.Wechsel_Auf_Unavailable_Leert_Retained_Werte` | keine veralteten kWh-/COP-Werte |
| `MqttTopicMapTests.EnergyAvailableTopic_Ist_Retained` | Topic vorhanden |

**Abnahme:**

- [ ] bei `MeasurementMode = None` erscheinen **keine** kWh-/COP-Werte
- [ ] `Status/Energy/Available` ist retained und wechselt korrekt
- [ ] nach Start oder Abschalten stehen **keine** veralteten retained Werte
- [ ] der Fehler erscheint in `Status/Errors` und in der UI
- [ ] `dotnet test` grün

### 10.7 – Energie und COP (11 Topics)

| Topic | DP | Typ | P |
|---|---:|---|---:|
| `Status/Energy/HeatingPower` | 3342 | kW | 2 |
| `Status/Energy/WarmWaterPower` | 3346 | kW | 2 |
| `Status/Energy/ThermalEnergyHeating` | 3343 | kWh | 3 |
| `Status/Energy/ThermalEnergyWarmWater` | 3347 | kWh | 3 |
| `Status/Energy/ThermalEnergyTotal` | 3349 | kWh | 3 |
| `Status/Energy/ElectricalEnergyHeating` | 3344 | kWh | 3 |
| `Status/Energy/ElectricalEnergyWarmWater` | 3354 | kWh | 3 |
| `Status/Energy/ElectricalEnergyTotal` | 3355 | kWh | 3 |
| `Status/Energy/CopHeating` | 3345 | – | 3 |
| `Status/Energy/CopWarmWater` | 3356 | – | 3 |
| `Status/Energy/CopTotal` | 3357 | – | 3 |

Alle 11 Items tragen `[Energy]` (→ `RequiresEnergyMeasurement = true`).

**Home-Assistant-Zuordnung:**

| Topics | `device_class` | `state_class` |
|---|---|---|
| `ThermalEnergy*`, `ElectricalEnergy*` | `energy` | `total_increasing` |
| `HeatingPower`, `WarmWaterPower` | `power` | `measurement` |
| `Cop*` | – | `measurement` |

**Abnahme:**

- [ ] 11 Items in `docs/topics-v2.json`
- [ ] alle 11 tragen `RequiresEnergyMeasurement = true`
- [ ] Discovery-Payloads haben die richtige `device_class`/`state_class`
- [ ] `dotnet test` grün

### 10.8 – Zähler und Wartung (11 Topics)

| Topic | DP | Typ | P |
|---|---:|---|---:|
| `Status/Counters/Stage2OperatingHours` | 31 | h | 3 |
| `Status/Counters/Stage2Cycles` | 41 | – | 3 |
| `Status/Counters/AdditionalHeaterOperatingHours` | 33 | h | 3 |
| `Status/Counters/AdditionalHeaterCycles` | 43 | – | 3 |
| `Status/Counters/HeatingOperatingHours` | 3060 | h | 3 |
| `Status/Counters/WarmWaterOperatingHours` | 3061 | h | 3 |
| `Status/Counters/ActiveCoolingOperatingHours` | 3362 | h | 3 |
| `Status/Counters/PassiveCoolingOperatingHours` | 3363 | h | 3 |
| `Status/Counters/DefrostCycles` | 3485 | – | 3 |
| `Status/Counters/DefrostOperatingHours` | 3484 | h | 3 |
| `Status/Counters/ServiceHours` | 3366 | h | 3 |

> `Status/Counters/ServiceInterval` entfällt – DP 3365 ist bereits
> `Config/Service/Interval` (§5.8).

**Abnahme:**

- [ ] 11 Items in `docs/topics-v2.json`
- [ ] alle mit `entity_registry_enabled_default: false` in der Discovery
- [ ] `dotnet test` grün

### 10.9 – Neue Konfigurations-Topics (21, nur Experten-Modus)

**Ziel:** Die zusätzlichen Einstellungen aus [topics-v2.md §5.9](../topics-v2.md#59-config--zusätzliche-einstellungen-schreibbar)
verfügbar machen – **nur** mit Experten-Modus.

| Topic (+ `/Set`) | DP | Bereich | Einheit |
|---|---:|---|---|
| `Config/EnergyProducer/SwitchingDifferenceStage2` | 141 | 2.0…20.0 | K |
| `Config/EnergyProducer/RestartDelayStage2` | 847 | 0.0…60.0 | min |
| `Config/EnergyProducer/BivalenceStage2` | 803 | −40.0…60.0 | °C |
| `Config/EnergyProducer/CondenserPumpPreRunningTime` | 3234 | 0.0…99.0 | min |
| `Config/EnergyProducer/CondenserPumpPostRunningTime` | 188 | 0.0…99.0 | min |
| `Config/EnergyProducer/FrostProtectionMode` | 498 | 0/1/2/3/10 | – |
| `Config/EnergyProducer/FrostProtectionTemperature` | 446 | 2.0…20.0 | °C |
| `Config/Warmwater/Equipment` | 117 | 0/1/2 | – |
| `Config/Warmwater/PowerSelection` | 195 | 0…4 | – |
| `Config/Warmwater/SwitchOverDelay` | 848 | 0.0…10.0 | min |
| `Config/Cooling/PassiveMode` | 579 | 0/1/2 | – |
| `Config/Cooling/Release` | 571 | 0…6 | – |
| `Config/General/CoolingEnabled` | 3118 | 0/1 | – |
| `Config/General/PassiveCoolingHydraulics` | 572 | 0/1/2/3/4/9 | – |
| `Config/HeatingSystem/MixerDirectOffset` | 3608 | 0.0…20.0 | K |
| `Config/HeatingSystem/SetPointNormalBelowOutdoor` | 862 | −30.0…10.0 | °C |
| `Config/Ventilation/FanControl` | 3092 | 0/1/2 | – |
| `Config/Ventilation/SpeedStage1` | 3224 | 0.0…100.0 | % |
| `Config/Ventilation/SpeedStage2` | 3225 | 0.0…100.0 | % |
| `Config/Ventilation/SpeedStage3` | 3226 | 0.0…100.0 | % |
| `Config/Service/Interval` | 3365 | – | h |

**Regeln:**

- Jedes Item: `[V2]` **und** `[ExpertMode]`.
- **Nicht doppelt anlegen:** `Config/EnergyProducer/RestartDelayStage1` (DP 846) entfällt –
  DP 846 ist bereits `Config/EnergyComsumer/HeatPump/RestartDelay` (V1, Phase 9).
- Auswahl-Parameter mit Lücken bekommen `[Values(…)]` (Phase-9-Mechanik):
  `FrostProtectionMode {0,1,2,3,10}`, `Equipment {0,1,2}`, `PassiveMode {0,1,2}`,
  `CoolingEnabled {0,1}`, `PassiveCoolingHydraulics {0,1,2,3,4,9}`,
  `FanControl {0,1,2}`.
- `Service/Interval` ist gleichzeitig die Lesequelle für `Status/Counters/ServiceHours`
  (Wartungsanzeige).

**Tests:**

| Test | Erwartung |
|---|---|
| `CatalogTests` | 21 Items, alle `RequiresExpertMode = true` |
| `BacnetDeviceTests` | ohne Experten-Modus → `WriteOutcome.ExpertModeRequired`, **kein** BACnet-Request |
| `MqttTopicMapTests` | ohne Experten-Modus kein `/Set`; mit Experten-Modus vorhanden |
| `HomeAssistantDiscoveryTests` | ohne Experten-Modus `sensor`; mit Experten-Modus `number`/`select` |
| `CatalogTests.Wertepruefung` | `FrostProtectionMode = 5` wird abgelehnt |

**Abnahme:**

- [ ] 21 Items in `docs/topics-v2.json`
- [ ] ohne Experten-Modus existiert **kein** `/Set`-Topic
- [ ] `dotnet test` grün

### 10.10 – Abgeleitete Werte (7 Topics)

| Topic | Formel | Einheit | P |
|---|---|---|---:|
| `Status/Derived/Superheat` | DP 641 − DP 660 | K | 2 |
| `Status/Derived/HeatingCircuitDelta` | DP 600 − DP 3468 | K | 2 |
| `Status/Derived/BufferDelta` | DP 24 − DP 25 | K | 2 |
| `Status/Derived/RuntimeRatio` | (DP 30 + DP 31) / DP 3060 × 100 | % | 3 |
| `Status/Derived/ServiceDue` | DP 3366 ≥ DP 3365 | 1/0 | 3 |
| `Status/Derived/CopInstant` | Δ(ThermalEnergyTotal) / Δ(ElectricalEnergyTotal) über 1 h | – | 3 |
| `Status/Derived/CoolingActive` | DP 662 ∈ {9, 30, 31} oder DP 3098 aktiv | 1/0 | 2 |

**Ziel-Dateien:**

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Domain/Derived/DerivedValueDefinition.cs` | **neu** |
| `src/ComfoBox.Domain/Derived/DerivedValues.cs` | **neu**: die sieben Definitionen + Formeln |
| `src/ComfoBox.Gateway/Derived/DerivedValueService.cs` | **neu**: rechnen + publizieren |
| `src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs` | Discovery für abgeleitete Werte |
| `src/ComfoBox.Gateway/GatewayServiceCollectionExtensions.cs` | Registrierung |

**Regeln:**

- Beide Eingangswerte müssen im **gleichen** Zyklus gelesen worden sein; sonst `unknown`.
- `RuntimeRatio`: Nenner `DP 3060` prüfen (0 ⇒ `unknown`).
- `CopInstant`: benötigt zwei Zeitpunkte; beim Start und nach einem Neustart `unknown`.
- `CoolingActive`: DP 662 und DP 3098 sind **keine** booleschen Werte – die Zuordnung
  steht in der Definition, nicht im Code verstreut.

**Tests:**

| Test | Erwartung |
|---|---|
| `DerivedValueTests` | jede Formel mit gültigen Werten |
| `DerivedValueTests` | fehlender Eingang ⇒ `unknown`, keine 0 |
| `DerivedValueTests` | Division durch 0 ⇒ `unknown` |
| `HomeAssistantDiscoveryTests` | sieben zusätzliche `sensor`-Entitäten |

**Abnahme:**

- [ ] sieben Topics werden publiziert
- [ ] kein abgeleiteter Wert ist schreibbar
- [ ] Formel steht in der UI als Tooltip
- [ ] `dotnet test` grün

### 10.11 – Poll-Plan P3 und Buslast

**Ziel:** Priorität 3 („stündlich“) wird wirklich stündlich gelesen – unabhängig vom
konfigurierten Poll-Intervall.

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Domain/Items/RefreshPriority.cs` | `Slow` ergänzen |
| `tools/generate-topics/generate-topics.mjs` | `[Slow]` im `PRIORITY_RE` |
| `src_old/ComfoBoxLib/Attributes/SlowAttribute.cs` | neue Attribut-Deklaration |
| `src/ComfoBox.Gateway/Bacnet/PollPlan.cs` | `ForCycle(..., slow, slowFactor)` |
| `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` | `ItemsWith(Slow)`, `slowFactor` berechnen |
| `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` | Log-Ausgabe um „langsame Items“ ergänzen |

**Tests:**

| Test | Erwartung |
|---|---|
| `PollPlanTests.Primary_Ist_In_Jedem_Zyklus` | unverändert |
| `PollPlanTests.Secondary_Ist_Alle_Fuenf_Zyklen` | unverändert |
| `PollPlanTests.Slow_Ist_Nur_Im_Ersten_Und_Dann_Jedem_Nten` | korrekt |
| `PollPlanTests.SlowFactor_Eins_Liest_Jeden_Zyklus` | Randfall |
| `CatalogTests` | mindestens ein Item mit `RefreshPriority.Slow` |

**Buslast-Doku:**

- [topics-v2.md §9](../topics-v2.md#9-auswirkungen-auf-den-bus) bleibt die Quelle.
- Die UI-Diagnoseseite zeigt „Buslast: … Reads/min“ (Aufgabe 10.14).

**Abnahme:**

- [ ] P1 = 60 s, P2 = 5 min, P3 = 1 h (bei Default-Intervall)
- [ ] bei `PollIntervalSeconds = 30` bleibt P3 bei 1 h
- [ ] `dotnet test` grün

### 10.12 – Home-Assistant-Discovery

**Ziel:** Die neuen Werte erscheinen in Home Assistant – mit den richtigen Typen,
Kategorien und Availability.

| Regel | Umsetzung |
|---|---|
| P1/P2 | `sensor` (read-only) |
| P3 | `entity_registry_enabled_default: false` |
| Zähler/Service/Fehler | `entity_category: diagnostic` |
| `Config/*` | `number`/`select` mit `entity_category: config` – **nur** mit Experten-Modus |
| Energie/COP | `availability_topic` = `Status/Energy/Available`, `payload_available = 1`, `payload_not_available = 0` |
| kWh | `device_class: energy`, `state_class: total_increasing` |
| kW | `device_class: power`, `state_class: measurement` |
| COP | `state_class: measurement` (kein `device_class`) |
| Abgeleitete Werte | `sensor`, `entity_category: diagnostic` (ausser `Status/Derived/CoolingActive` → `binary_sensor`) |

**Ziel-Dateien:**

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs` | Komponenten-Auswahl, `availability_topic` je Item, abgeleitete Werte |
| `src/ComfoBox.Gateway/Mqtt/MqttValueFormatter.cs` | Anzeigenamen für neue Gruppen |
| `src/ComfoBox.Gateway/Mqtt/DiscoveryId.cs` | keine Änderung (Slug ist generisch) |

**Tests:**

| Test | Erwartung |
|---|---|
| `HomeAssistantDiscoveryTests` | Anzahl der Entitäten V1 + V2 + Derived |
| `HomeAssistantDiscoveryTests` | P3-Entitäten sind standardmässig deaktiviert |
| `HomeAssistantDiscoveryTests` | Energie-Entitäten tragen die Energy-Availability |
| `HomeAssistantDiscoveryTests` | ohne Experten-Modus keine `number`/`select` für neue Config-Items |

**Abnahme:**

- [ ] Discovery-Payloads sind gültiges JSON
- [ ] HA legt die neuen Entitäten ohne YAML an
- [ ] `dotnet test` grün

### 10.13 – Experten-Modus zur Laufzeit für MQTT

**Problem:** `MqttTopicMap` und `HomeAssistantDiscovery` lesen den Experten-Modus heute
**beim Start** (`GatewayOptions.ExpertMode`). `GatewayRuntimeSettings` wirkt nur im
REST-Schreibpfad. Wer den Schalter in der UI umlegt, sieht unter MQTT/HA keine Änderung –
die neuen `/Set`-Topics fehlen bis zum Neustart.

**Ziel:** Ein Umschalten des Experten-Modus wirkt sofort auf
- die abonnierten `/Set`-Topics,
- die Discovery (`sensor` ↔ `number`/`select`),
- die REST-Sicht (`ItemDto.writable`).

**Ziel-Dateien:**

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Gateway/Configuration/GatewayRuntimeSettings.cs` | `event EventHandler<bool>? ExpertModeChanged` |
| `src/ComfoBox.Gateway/Mqtt/MqttTopicMap.cs` | `MqttTopicMap` für beide Zustände baubar (`WithExpertMode(bool)` oder zwei Instanzen) |
| `src/ComfoBox.Gateway/Mqtt/MqttSubscriber.cs` | bei Wechsel: alte `/Set`-Topics abbestellen, neue abonnieren |
| `src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs` | `GatewayRuntimeSettings` statt `GatewayOptions`; bei Wechsel neu publizieren |
| `src/ComfoBox.Gateway/Api/ConfigEndpoints.cs` | nach `ExpertMode`-Änderung das Ereignis auslösen |

**Fallback (wenn die Session zu klein ist):** Den Zustand dokumentieren –
„MQTT/Discovery folgen dem Experten-Modus beim Start; ein Umschalten wirkt nach einem
Neustart“ – und in der UI einen Hinweis anzeigen. **Nicht** stillschweigend abweichen.

**Tests:**

| Test | Erwartung |
|---|---|
| `MqttTopicMapTests.ExpertMode_An_Erzeugt_Set_Topics` | vorhanden |
| `MqttTopicMapTests.ExpertMode_Aus_Entfernt_Set_Topics` | nicht vorhanden |
| `MqttSubscriberTests` | Resubscribe nach Wechsel |
| `HomeAssistantDiscoveryTests` | Rediscovery nach Wechsel |

**Abnahme:**

- [ ] Schalter in der UI umlegen → neue Config-Items werden sofort schreibbar
- [ ] Schalter aus → `/Set`-Topics verschwinden, HA zeigt `sensor`
- [ ] `dotnet test` grün

### 10.14 – UI: Diagnose- und Energie-Block

**Ziel:** Die neuen Werte sind nicht nur Topics, sondern auch erklärt.

| Bereich | Änderung |
|---|---|
| Diagnose | Block **„Warum heizt es nicht?“** mit `Zone/Override`, `Zone/State`, `WarmWater/State`, `WarmWater/Override`, `HeatPump/ReturnTemperature`, `Zone/FlowTemperature` |
| Diagnose | Block **„Kältekreis“** mit `Superheat`, `HotGasTemperature`, `SuctionGasTemperature` |
| Energie | Karte **„Verbrauch und Effizienz“** mit kWh, COP und `Available`-Status |
| Energie | Banner, wenn `MeasurementMode = None`: Text aus §10.6 + Link auf die Hilfe |
| Konfiguration | neue Config-Topics erst hinter dem Experten-Modus; Warnhinweis bei `Config/Energy/*` |
| Alle | Tooltip mit der Katalog-`Description`; Deep-Link auf [datenpunkt-register.md](../07-technikerhandbuch/datenpunkt-register.md) |

**Ziel-Dateien:**

| Datei | Änderung |
|---|---|
| `src/ComfoBox.Web/src/app/features/diagnostics/*` | Blöcke + Signale |
| `src/ComfoBox.Web/src/app/features/values/*` | Filter „nur Experten-Modus“ (falls noch nicht vorhanden) |
| `src/ComfoBox.Web/src/app/core/i18n/{translations,de,en,fr}.ts` | neue Schlüssel in **allen vier** Dateien |
| `src/ComfoBox.Web/public/help/*` | nach `node tools/build-docs.mjs` neu erzeugt |

**Befehle:**

```bash
cd src/ComfoBox.Web
npm run lint
npm run build
cd ../..
node tools/build-docs.mjs
pwsh -NoProfile -File tools/check-links.ps1
```

**Abnahme:**

- [ ] der Diagnose-Block zeigt die sechs Werte
- [ ] bei `MeasurementMode = None` erscheint der Banner
- [ ] `npm run lint && npm run build` grün
- [ ] `public/help/` ist neu erzeugt und eingecheckt
- [ ] `check-links.ps1` grün

### 10.15 – Benutzerdoku

**Ziel:** Die neuen Werte und die DP-3340-Voraussetzung verständlich erklären.

| Seite | Inhalt |
|---|---|
| `docs/benutzer/12-verbrauch.md` | „Verbrauch und Effizienz“: kWh, COP, warum ohne Wärmemengenmessung nichts kommt, was am Gerät einzustellen ist |
| `docs/benutzer/13-warum-bleibt-ein-raum-kalt.md` | „Warum bleibt ein Raum kalt?“: `Override`, `State`, `SetPointType`, Frostschutz, Ferien, Zeiten AUS |
| `docs/benutzer/09-fehlersuche.md` | Eintrag „Die Energie-Werte fehlen / stehen auf 0“ → DP 3340 |
| `docs/benutzer/README.md` | Inhaltsverzeichnis ergänzen |

**Front-Matter (Pflicht):**

```yaml
---
title: Verbrauch und Effizienz
description: kWh-Zähler, COP und die Wärmemengenmessung am Gerät
order: 120
icon: pi pi-chart-line
tags:
  - energie
  - cop
---
```

```yaml
---
title: Warum bleibt ein Raum kalt?
description: Übersteuerung, Zustand und Sollwertart verstehen
order: 130
icon: pi pi-question-circle
tags:
  - diagnose
  - zone
---
```

**Regeln:** Sie-Form, Symptom → Ursache → Lösung, **keine** Implementierungsdetails,
keine Dateipfade, keine Klassennamen.

**Abnahme:**

- [ ] beide Seiten haben vollständiges Front-Matter
- [ ] `node tools/build-docs.mjs` läuft durch
- [ ] `check-links.ps1` grün
- [ ] die Seiten erscheinen in der Web-UI unter **Hilfe**

### 10.16 – Verifikation und Abschluss

**Gesamtverifikation:**

```bash
node tools/generate-topics/generate-topics.mjs --catalog src/ComfoBox.Domain/Items
node tools/generate-topics/generate-topics.mjs --out docs/topics-v2.json
node tools/generate-topics/generate-topics.mjs --catalog-check src/ComfoBox.Domain/Items
node tools/generate-topics/generate-topics.mjs --check
node tools/generate-topics/generate-topics.mjs --check-v2
dotnet build src/ComfoBox.sln -c Release
dotnet test  src/ComfoBox.sln -c Release
cd src/ComfoBox.Web && npm run lint && npm run build && cd ../..
node tools/build-docs.mjs
pwsh -NoProfile -File tools/check-links.ps1
```

**Erwartete Diffs:**

| Datei | Erwartung |
|---|---|
| `docs/topics.json` | **kein Diff** (byte-identisch) |
| `docs/topics-v2.json` | neu, 78 Items |
| `src/ComfoBox.Domain/Items/Catalog.cs` | 78 neue Items, 127 unveränderte |
| `src/ComfoBox.Domain/Items/ItemEnums.cs` | 6 neue Enums |
| `docs/topics.md` | **kein Diff** (eingefroren) |

**Abschluss:**

- [ ] [status.md](status.md): Phase 10 auf `✅`
- [ ] [protokoll.md](protokoll.md): Eintrag mit Testzahl vorher/nachher
- [ ] `CHANGELOG.md`: `Added` (V2-Topics, Derived, DP-3340-Gate) + `Changed` (Poll P3)
- [ ] Prompt für die nächste Session ausgeben ([README §3.4](README.md#34-der-nächste-prompt))

---

## 7 Testplan

| Bereich | Datei | Prüft |
|---|---|---|
| Domain | `CatalogTests.cs` | 127 V1, 78 V2, Eindeutigkeit, reservierte Pfade |
| Domain | `DerivedValueTests.cs` | 7 Formeln, `unknown` statt 0 |
| Contract | `TopicContractTests.cs` | V1-Fixture unverändert |
| Contract | `V2TopicContractTests.cs` | V2-Fixture deckungsgleich |
| Gateway | `BacnetDeviceTests.cs` | Experten-Modus, Wertebereich, kein BACnet-Request bei Ablehnung |
| Gateway | `PollPlanTests.cs` | P1/P2/P3, Slow-Faktor |
| Gateway | `MqttTopicMapTests.cs` | `/Set` nur mit Experten-Modus |
| Gateway | `MqttSubscriberTests.cs` | Resubscribe nach Moduswechsel |
| Gateway | `EnergyMeasurementTests.cs` | DP-3340-Gate, keine 0-Werte |
| Gateway | `HomeAssistantDiscoveryTests.cs` | 78 + 7 Entitäten, `enabled_default`, `availability_topic` |
| Tools | `tools/generate-topics` | `--check`, `--check-v2` |
| Web | `npm run lint && npm run build` | i18n vollständig, Templates gültig |
| Doku | `build-docs.mjs`, `check-links.ps1` | Front-Matter, Links |

## 8 Abnahmekriterien

- [ ] V1: **127** Items, Fixture **byte-identisch**
- [ ] V2: **78** Items in `docs/topics-v2.json`
- [ ] alle neuen schreibbaren Items: `RequiresExpertMode = true`
- [ ] ohne Experten-Modus: **kein** `/Set`-Topic, **keine** `number`/`select`-Discovery
- [ ] P1 = **60 s**, P2 = 5 min, P3 = 1 h
- [ ] DP 3340 = 0: **keine** kWh-/COP-Werte, `Available = 0`, Fehler + UI-Banner
- [ ] abgeleitete Werte: `unknown` statt 0, nie schreibbar
- [ ] keine Kollision mit den Gateway-Status-Topics
- [ ] `dotnet build` 0 Warnungen, `dotnet test` grün
- [ ] `npm run lint && npm run build` grün
- [ ] `build-docs.mjs` + `check-links.ps1` grün
- [ ] CHANGELOG, status.md, protokoll.md aktualisiert

## 9 Risiken und Rückbau

| Risiko | Gegenmassnahme |
|---|---|
| **V1-Fixture wird versehentlich verändert** | `git diff --exit-code docs/topics.json` in der Verifikation; Contract-Test filtert `IsV2` |
| **Topic-Kollision mit `Status/Health`/`Status/Errors`** | reservierte Pfade (§5.4) + Test |
| **Buslast steigt zu stark** | P1 = 60 s (nicht 30 s); P3 = 1 h; optionale E-Topics standardmässig aus |
| **Neue Config-Topics werden versehentlich schreibbar** | `[ExpertMode]` + Tests auf `WriteOutcome.ExpertModeRequired` ohne Experten-Modus |
| **Energie-Entitäten zeigen 0 statt „unbekannt“** | DP-3340-Gate + `EnergyMeasurementTests` |
| **`src_old/`-Encoding zerstört Umlaute** | CP1252 lesen/schreiben; `git diff --stat src_old/` prüfen |
| **Experten-Modus wirkt unter MQTT erst nach Neustart** | Aufgabe 10.13; Fallback dokumentieren, nicht stillschweigend |
| **Abgeleitete Werte rechnen mit veralteten Eingängen** | gleicher Zyklus + `IValueCache.IsStale`; sonst `unknown` |

**Rückbau:** V2 ist additiv. Ein `git revert` des Phase-10-Commits entfernt die neuen
Topics; V1 bleibt unberührt. Die einzige dauerhafte Änderung ist die neue
`RefreshPriority.Slow` – sie ist auch ohne V2 nützlich.

## 10 Prompt für die Umsetzungs-Session

```text
Setze Phase 10 aus docs/06-aufgaben/phase-10-topics-v2.md um.

Lies in dieser Reihenfolge:
1. docs/06-aufgaben/phase-10-topics-v2.md   (die Aufgaben)
2. docs/topics-v2.md §5, §7, §9, §10         (der V2-Vertrag)
3. docs/05-entscheidungen/adr-014-topics-v2-namensraum-und-expertenmodus.md
4. docs/07-technikerhandbuch/datenpunkt-register.md §4.8, §6.3.14
5. docs/07-technikerhandbuch/verbesserungen-und-neue-topics.md §3, §6.1
6. docs/06-aufgaben/phase-9-datenpunkt-korrekturen.md §4, §5
7. .github/copilot-instructions.md

Wichtig:
- V1 bleibt eingefroren: docs/topics.json und docs/topics.md NICHT anfassen.
- V2 bekommt eine eigene Fixture docs/topics-v2.json; der Generator wird um
  --out-v2 und --check-v2 erweitert.
- Jedes neue schreibbare Item traegt [V2] UND [ExpertMode].
- Die Katalogquelle ist src_old/ComfoBoxLib/Items/*.cs - Windows-1252!
  Mit CP1252 lesen UND schreiben, sonst werden Umlaute zerstoert.
- DP 3340: bei MeasurementMode = None werden kWh/COP NICHT als 0 publiziert.
  Status/Energy/Available = 0, Fehler in Status/Errors, Banner in der UI.
- P1 = 60 s (nicht 30 s), P2 = 5 min, P3 = 1 h. RefreshPriority.Slow ergaenzen.
- Reihenfolge: 10.1/10.2 (Mechanik), dann 10.3-10.10, dann 10.11-10.16.
- Am Ende: dotnet test, npm run lint/build, node tools/build-docs.mjs,
  pwsh -NoProfile -File tools/check-links.ps1 - alles gruen.
- Protokoll und status.md aktualisieren.
```
