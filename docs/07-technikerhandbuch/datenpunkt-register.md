# ECR450 – Datenpunkt-Register

Register **aller** Parameter des Reglers, gruppiert nach der Menüstruktur des Handbuchs.
Die Spalte **DP** ist die Nummer, die das Gateway als BACnet-Object-Identifier verwendet.
Die Spalte **Topic V2** nennt die zugehörige MQTT-Adresse, sofern es sie heute schon gibt
(unverändert) oder sie in [../topics-v2.md](../topics-v2.md) neu vorgeschlagen ist.

Legende: `✅` = heute implementiert · `➕` = Vorschlag V2 · `—` = nicht vorgesehen

> **Quelle:** `164829_TH_ECR450_V4.0.pdf` (ELESTA ECR450, Software-Version 4).
> Seitenangaben beziehen sich auf die Seitenzahl **im Dokument**.
> Siehe [README.md](README.md) zur Herkunft und zur Frage des Volltexts.

---

## Legende der Spalten

| Spalte | Bedeutung |
|---|---|
| **DP** | Datenpunkt-Nummer im Regler = BACnet-Object-Identifier |
| **Parameter** | Bezeichnung wie auf dem Display (Abkürzungen sind original) |
| **Bereich / Werte** | Einstellbereich, oder die erlaubten Werte bei Auswahlparametern |
| **E** | Einheit |
| **S.** | Seite im Handbuch |

---

## 1 Zone (Bedienung)

| DP | Parameter | Bereich / Werte | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 830 | Betriebsart | 1 Manuell · 2 Standby · 3 Nur Warmwasser · 4 Normal/Frost · 5 Normal/Reduziert · 6 Dauernd normal · 7 Dauernd reduziert | | 7 | ✅ `Zone/OperationMode` |
| 3078 | B'art Heiz-Kühlen | 1 Auto · 2 Nur Kühlen · 3 Nur Heizen · 5 Neutral | | 7 | ✅ `Zone/HeatingCoolingMode` |
| 674 | Party Zeit | h | h | 7 | ✅ `Zone/PartyTime` |
| 675 | Eco Zeit | h | h | 7 | ✅ `Zone/EcoTime` |
| 678 | Heizk'adaption rückstellen | Schreibbefehl (Rücksetzen) | | 7 | ➕ `Config/…/ResetHeatingCurveAdaption` |
| 75 | Eichung Raumfühler | reale Raumtemperatur | °C | 7 | ✅ `Zone/CalibrateTemperature` |
| 679 | Manuelle Adaption | Heizkennlinien-Korrektur | K | 7 | ✅ `Zone/ManualAdoption` |
| 62 | Raumsollwert normal | 5.0…30.0 | °C | 8 | ✅ `Zone/SetPointNormal` |
| 61 | Raumsollwert reduz | 5.0…30.0 | °C | 8 | ✅ `Zone/SetPointReduced` |
| 60 | Raumsollwert Frost | 5.0…30.0 | °C | 8 | ✅ `Zone/SetPointFrost` |
| 670 | Zonenname | freier Text | | 23 | ➕ `Status/Zone/Name` |

> Die drei Raumsollwerte sind **untereinander verriegelt**: „Frost" ≤ „Reduziert"/„Normal".
> „Normal" und „Reduziert" dürfen sich überschneiden.
> **Achtung:** das Gerät lehnt eine Verletzung der Verriegelung ab – der Gateway muss das
> nicht vorab prüfen, aber die Fehlermeldung sinnvoll weitergeben.

## 2 Warmwasser (Bedienung)

| DP | Parameter | Bereich / Werte | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 2590 | 1x WW-Ladung | Schreibbefehl (einmalige Ladung) | | 9 | ✅ `Warmwater/DoHeatWaterNow` |
| 72 | WW-Sollwert normal | 5…Begrenzung (DP 190) | °C | 9 | ✅ `Warmwater/SetPointNormal` |
| 71 | WW-Sollwert reduz | 5…Begrenzung | °C | 9 | ✅ `Warmwater/SetPointReduced` |
| 70 | WW-Sollwert Frost | 5…Begrenzung | °C | 9 | ✅ `Warmwater/SetPointFrost` |
| 73 | WW-Sollwert Legio | 5…Begrenzung | °C | 9 | ✅ `Warmwater/SetPointLegio` |

## 3 Freie Schaltuhren

Neun Schaltuhren (Zonen 1…7, WW, frei). Jede besteht aus Wochentag, drei Blöcken und
sechs Ferienblöcken. Im Handbuch sind **keine DP-Nummern** für die Schaltuhr-Blöcke
angegeben – sie sind über den Item-Katalog **nicht** adressierbar.

Das Gerät kennt jedoch die Kanäle als Digitalfunktionen (DP 701…707, 709, 711 in §6.5.3),
über die eine Schaltuhr auf einen Ausgang oder eine Funktion wirkt. Lesbar ist also nur der
**Zustand** des Kanals, nicht die programmierten Zeiten.

## 4 Zustände (nur lesend)

### 4.1 Zone

| DP | Parameter | Bedeutung | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 12 | Raumtemperatur | Istwert Raum | °C | 11 | ✅ `States/Zone/RoomTemperature` |
| 208 | Raumsollwert | aktuell gültiger Sollwert | °C | 11 | ✅ `States/Zone/CurrentSetPoint` |
| 14 | Vorlauftemperatur | Istwert Vorlauf | °C | 11 | ➕ `Status/Zone/FlowTemperature` |
| 209 | Vorlaufsollwert | gerechneter Sollwert | °C | 11 | ✅ `States/Zone/CalculatedFlowTemperature` |
| 10 | Aussentemperatur | aktuell | °C | 11 | ✅ `States/Zone/OutdoorTemperature` |
| 205 | Aus'temp gebäudebez | gedämpfte/gebäudebezogene Aussen-T. | °C | 11 | ✅ `States/Zone/OutdoorTemperatureBuilding` |
| 59 | Zustand Zone | Regelungs-Zustand (**Werte nicht dokumentiert**) | | 11 | ➕ `Status/Zone/State` |
| 398 | Raumsollwerttyp | verwendeter Sollwert | | 11 | ➕ `Status/Zone/SetPointType` |
| 676 | Zo'überst | Übersteuerungsquelle, 22 Werte (0 = Ohne, 1 Party, 2 Eco, 3 Ferien, 4 Fernbedienung, 5 Ext Eingang, 6 Inbetriebnahme, 7 Taupunktwächter, 8 Bodenaustrocknung, 9 WP-Störung, 10 Opti WP-Ausschalt, 11 Raumsoll'überh Sol, 13 Sollwertschiebung, 14 Zeiten AUS, 18 Aussentemperatur, 21 Entlüftung Sek'kreis, 22 Nur Zusatzheizung) | | 11 | ➕ `Status/Zone/Override` |

> **Wichtig für Home Assistant:** DP 676 und 398 erklären, *warum* ein Raum trotz Sollwert
> nicht warm wird. Ohne diese beiden Werte ist jede Diagnose im UI Blindflug.

### 4.2 Warmwasser

| DP | Parameter | Bedeutung | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 3 | Warmwassertemp | WW-Fühler oben | °C | 11 | ✅ `States/WarmWater/Temperature` |
| 4 | Warmwassertemp 2 | WW-Fühler unten | °C | 11 | ➕ `Status/WarmWater/TemperatureBottom` |
| 207 | WW-Sollwert | aktuell gültiger Sollwert | °C | 11 | ✅ `States/WarmWater/CurrentTargetTemperature` |
| 397 | WW-Sollwert (Typ) | 0 Frostschutz · 1 Reduziert · 2 Normal · 3 Legionellen | | 11 | ➕ `Status/WarmWater/SetPointType` |
| 2598 | WW-Überst | Übersteuerungsquelle (0,1,3,5,9,10,13,14,15,17,19,20,22) | | 11 | ➕ `Status/WarmWater/Override` |
| 2599 | WW-Betrieb | 0 AUS · 1 WW mit Energieerz · 2 WW-elektrisch · 3 WW mit Zusatzheiz · 4 WW-Entladeschutz · 5 EW-Sperre | | 12 | ➕ `Status/WarmWater/State` |

### 4.3 Energieerzeuger

| DP | Parameter | Bedeutung | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 21 | Energieerz'temp | Istwert der WP-Regelung | °C | 12 | ➕ `Status/HeatPump/EnergyProducerTemperature` |
| 3468 | WP-Rücklauftemp | Istwert Rücklauf | °C | 12 | ➕ `Status/HeatPump/ReturnTemperature` |
| 210 | Energieerz'sollwert | Sollwert der WP-Regelung | °C | 12 | ✅ `States/HeatPump/HeatPumpSetPoint` |
| 3098 | Kühlvorlauftemp | Istwert Kühlvorlauf | °C | 12 | ✅ `States/HeatPump/CoolingFlowTemperature` |
| 24 | Pufferspeichertemp | oben | °C | 12 | ➕ `Status/Buffer/TemperatureTop` |
| 25 | Pufferspeichertemp 2 | unten | °C | 12 | ➕ `Status/Buffer/TemperatureBottom` |
| 3924 | Kühlpuffersp'temp 1 | Kühlpuffer unten | °C | 12 | ➕ `Status/Buffer/CoolingTemperatureBottom` |
| 3925 | Kühlpuffersp'temp 2 | Kühlpuffer oben | °C | 12 | ➕ `Status/Buffer/CoolingTemperatureTop` |
| 281 | Pufferspeichersollwert | Sollwert Pufferspeicher | °C | 12 | ➕ `Status/Buffer/SetPoint` |
| 45 | WP-Leistung | aktuelle Leistung | % | 12 | ✅ `States/HeatPump/CurrentPower` |

### 4.4 Wärmepumpe / Kältekreis 1

| DP | Parameter | Bedeutung | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 662 | WP1 | Betriebszustand Stufe 1 (Werte 0…62, siehe §4.4.1) | | 12 | ✅ `States/HeatPump/HeatPumpStatus` |
| 663 | WP2 | Betriebszustand Stufe 2 | | 12 | ➕ `Status/HeatPump/HeatPumpStatusStage2` |
| 3222 | Ansauglufttemp | Ansaugluft | °C | 12 | ✅ `States/HeatPump/SuctionAirTemp` |
| 600 | WP-Vorlauftemp | Vorlauffühler | °C | 12 | ✅ `States/HeatPump/CurrentFlowTemperature` |
| 648 | WP-Vor'temp Min/Max | Min/Max-Begrenzung Vorlauffühler | °C | 12 | ✅ `States/HeatPump/MaxFlowTemperature` |
| 3209 | Prim'vorlauftemp | Vorlauf im Primärkreis | °C | 12 | ➕ `Status/HeatPump/PrimaryFlowTemperature` |
| 602 | Primärtemperatur | Temperatur im Primärkreis | °C | 12 | ✅ `States/HeatPump/PrimaryTemperature` |
| 660 | Verdampfertemp | Verdampfer | °C | 12 | ➕ `Status/HeatPump/EvaporatorTemperature` |
| 641 | Sauggastemperatur | Sauggas | °C | 12 | ➕ `Status/HeatPump/SuctionGasTemperature` |
| 3228 | Heissgastemp | Heissgas | °C | 12 | ➕ `Status/HeatPump/HotGasTemperature` |
| 666 | Kondensatortemp | Kondensator | °C | 12 | ➕ `Status/HeatPump/CondenserTemperature` |
| 644 | Akt Abtaudifferenz | aktuell gerechnete Abtaudifferenz | K | 12 | ➕ `Status/HeatPump/DefrostDeltaActual` |
| 3110 | Refer'diff gemessen | ΔT bei Referenzaufnahme | K | 12 | ➕ `Status/HeatPump/DefrostDeltaReference` |
| 643 | Refer'diff korrigiert | gültige ΔT für Überwachung | K | 12 | ➕ `Status/HeatPump/DefrostDeltaCorrected` |
| 3112 | Sich'dif Stromausfall | Sicherheits-ΔT nach Stromausfall | K | 12 | — |
| 3111 | Aussen't ref'mess | Aussen-T. bei Referenzmessung | °C | 12 | — |
| 3475 | Kondensationstemp | aus Hochdrucksensor gerechnet | °C | 12 | ➕ `Status/HeatPump/CondensationTemperature` |
| 3598 | Verdampfungstemp | aus Niederdrucksensor gerechnet | °C | 12 | ➕ `Status/HeatPump/EvaporationTemperature` |

#### 4.4.1 Betriebszustände der Wärmepumpe (DP 662/663/3208)

Werte 0…62. Die größten Gruppen:

| Werte | Bedeutung |
|---|---|
| 0…7 | AUS, Frostschutz, Vorlauf zu hoch, Sperre Bivalenz, Temp. zu tief, freie Kühlung, Verdampfertemp. zu tief, Verzögerung Stufe 2 |
| 9…19 | Passivkühlen, Heizen mit WP, Hoch-/Niederdruckstörung, Sicherheitskette, Wärmequelle, Verzögerungen nach Stromausfall, EW-Sperre, Vor-/Nachlauf Primär |
| 20…29 | Abtau-Überwachung, Abtauverzögerung, Abtauen, Abtaustillstand, Referenzmessung, Abtausperrzeit, manuell/ext. Abtauen, Abtauen mit Ventilator |
| 30…31 | Aktivkühlen (auch mit passiver WW-Ladung) |
| 40…45 | Warmwasser mit WP, Frostgefahr, Absaugung, Entlastung, MOP, Stillstand Umschaltung |
| 46…52 | Kondensatfrostschutz, Taupunktwächter, Ventilatorsperre, erfolglose Abtauung, Schwimmbad, Vorlaufzeit Kondensatorpumpe, Frostwarnung |
| 53…62 | Heissgas-Warn/Stör, Entlüftung Primärkreis, nur Zusatzheizung, Offzeit Abtauen, Verdichter-Mindestlaufzeit, kein Differenzdruck, WW nur Zusatzheizung, Hochdruckwarnung, Verdampferfühler defekt |

> Diese Liste ist im Item-Katalog als `ItemEnums.HeatPumpStatusEnums` hinterlegt und deckt
> **alle** dokumentierten Werte ab – geprüft, keine Lücke.

### 4.5 Wärmepumpe 2 (nur bei 2 Kältekreisen)

| DP | Parameter | E | S. |
|---:|---|---|---:|
| 3208 | WP2 (Betriebszustand) | | 14 |
| 601 | WP-Vorlauftemp 2 | °C | 14 |
| 649 | WP-Vor'temp 2 Min/Max | °C | 14 |
| 3211 | Prim'vorlauftemp 2 | °C | 14 |
| 646 | Primärtemperatur 2 | °C | 14 |
| 664 | Verdampfertemp 2 | °C | 14 |
| 3197 | Sauggastemperatur 2 | °C | 14 |
| 3229 | Heissgastemp 2 | °C | 14 |
| 3199 | Kondensatortemp 2 | °C | 14 |
| 3201 | Akt Abtaudifferenz 2 | K | 14 |
| 3202 | Refer'diff gemessen 2 | K | 14 |
| 3203 | Refer'diff korrigiert 2 | K | 14 |
| 3204 | Sich'dif Stromausfall 2 | K | 14 |
| 3205 | Aussen't ref'mess 2 | °C | 14 |
| 3476 | Kondensationstemp 2 | °C | 14 |
| 3599 | Verdampfungstemp 2 | °C | 14 |

### 4.6 Sonnenkollektor

| DP | Parameter | E | S. |
|---:|---|---|---:|
| 28 | Kollektortemp 1 | °C | 14 |
| 3905 | Kollektortemp 2 | °C | 14 |
| 264 | Kollektordeltatemp | K | 14 |
| 3903 | Kollektor-WT-temp | °C | 14 |
| 3909 | Kollek'vorlauftemp | °C | 14 |
| 3910 | Kollek'rücklauftemp | °C | 14 |
| 36 | Wärmemenge Koll (kumuliert) | kWh | 14 |
| 3911 | Tageswärme Kollektor | kWh | 14 |

> „Leistung Kollektor" ist in §4.6 **ohne** DP-Nummer aufgeführt – im Handbuch nicht
> adressierbar. Die aktuelle Kollektorleistung ergibt sich nur über die Wärmemengenmessung
> (§4.8/§6.3.13).

### 4.7 Zähler

| DP | Parameter | E | S. |
|---:|---|---|---:|
| 30 | Betriebsst St 1 | h | 14 |
| 40 | Einschalt St 1 | | 14 |
| 31 | Betriebsst St 2 | h | 14 |
| 41 | Einschalt St 2 | | 14 |
| 33 | Betriebsst Zus'h | h | 15 |
| 43 | Einschalt Zus'h | | 15 |
| 37 | Betreibsst WW-el | h | 15 |
| 47 | Einschalt WW-el | | 15 |
| 3060 | Betriebsst Heiz | h | 15 |
| 3061 | Betriebsst WW | h | 15 |
| 3362 | Betriebsst A'kühl | h | 15 |
| 3363 | Betriebsst P'kühl | h | 15 |
| 3364 | Betriebsst S'bad | h | 15 |
| 34 | Betriebsst Kol'pu | h | 15 |
| 3484 | Betriebsst Abtau | h | 15 |
| 3486 | Betriebsst Abtau 2 | h | 15 |
| 3485 | Einschalt Abtau | | 15 |
| 3487 | Einschalt Abtau 2 | | 15 |
| 3366 | Serviceintervall (seit letztem Service) | h | 15 |

### 4.8 Wärmemengenmessung

| DP | Parameter | E | S. |
|---:|---|---|---:|
| 3342 | Wärme'leistung (Energieerzeuger) | kW | 15 |
| 3346 | Wär'leistung WW | kW | 15 |
| 3343 | Wärmemenge Heizung | kWh | 15 |
| 3347 | Wär'menge WW | kWh | 15 |
| 3349 | Wär'menge total | kWh | 15 |
| 3344 | Elektr'energie Heizung | kWh | 15 |
| 3354 | Elek'energie WW | kWh | 15 |
| 3355 | Elek'energ total | kWh | 15 |
| 3345 | COP Heizung (Durchschnitt) | | 15 |
| 3356 | COP WW (Durchschnitt) | | 15 |
| 3357 | COP total (Durchschnitt) | | 15 |

> **Das ist der interessanteste Block für Home Assistant.** COP und kWh-Zähler sind
> read-only und ändern sich selten → niedrige Poll-Priorität, aber hoher Nutzen.

### 4.9 Fehlerspeicher

10 Speicherplätze, je mit Datum und Uhrzeit. **Das Handbuch nennt keine DP-Nummer** für
die Speicherplätze – nur die möglichen Fehler-Codes (0…965).

| Aspekt | Befund |
|---|---|
| Auslesen über BACnet | im Handbuch **nicht belegt** |
| Fehler-Codes | vollständig tabelliert (`Kein Fehler` = 0, `WW-Fühler 1 defekt` = 1, … `Fühler E155 defekt` = 965) |
| Löschen | über das Menü „Fehlerspeicher löschen…" |

⇒ Ein Fehler-Topic ist nur über einen **Zusatzweg** möglich (z. B. eine freie
Register-/Analogmodul-Konfiguration im Gerät). Siehe
[verbesserungen-und-neue-topics.md](verbesserungen-und-neue-topics.md#2-fehlerspeicher).

### 4.10–4.16 Ein-/Ausgänge

Die Zustände der Ein- und Ausgänge sind in §7 „Anlage testen" mit DP-Nummern hinterlegt
(siehe [§7](#7-anlage-testen)):

| Gruppe | DP-Bereich | S. |
|---|---|---|
| Eingänge E1…E10 | 2613…2622 | 106 |
| Eingänge E21…E28 | 2649…2656 | 106 |
| Eingänge D1…D5 | 2600…2604 | 107 |
| Eingänge D21…D28 | 2636…2640, 2989…2991 | 108 |
| Ausgänge Y1, Y2 | 2789, 2790 | 108 |
| Ausgänge R1…R10 | 2779…2788 | 108 |
| Ausgänge R21…R27 | 2791…2797 | 109 |
| Remote-IO E141…E145 / Y141…Y142 / R141…R144 | 3774…3782 | 110 |
| Zone # (ECM310A) E131…E133 / D131, D132 / R131…R133 | 672, 671, 673 / 312, 311 / 357, 358, 356 | 110 |

> **Achtung:** E1 ist im Handbuch mit **2614** angegeben – E2 ebenfalls. Aus der Folge
> E3=2615 … E10=2622 ergibt sich E1=2613, E2=2614. Die Angabe bei E1 ist ein Druckfehler.

---

## 5 Regler

### 5.1 Regler-Info

| DP | Parameter | E | S. | Topic V2 |
|---:|---|---|---:|---|
| 203 | Reglertyp | | 20 | ✅ `States/Controller/ControllerType` |
| 2044 | Erweiterungstyp | | 20 | ✅ `States/Controller/ExtensionType` |
| 2043 | Softwarevers | | 20 | ✅ `States/Controller/SoftwareVersion` |
| 3353 | Inbetriebnahme (Datum) | | 20 | ✅ `States/Controller/InitialOperationDate` |
| 211 | Betriebsst Regler | h | 20 | ✅ `States/Controller/OperatingHours` |
| 212 | Einschalt Regler | | 20 | ✅ `States/Controller/NumberOfStartUps` |
| 617 | Anzahl Par'änderungen | | 20 | ✅ `States/Controller/NumberOfParameterChanges` |
| 618 | Letzte Par'änder (Datum) | | 20 | ✅ `States/Controller/LastParameterChangeDate` |

### 5.2 Zeit, Datum

| DP | Parameter | Bereich / Werte | S. |
|---:|---|---|---:|
| 2045 | Zeit | hh:mm | 20 |
| 2046 | Datum | tt.MM | 20 |
| 2047 | Jahr | JJJJ | 20 |
| 133 | Uhrzeit Quelle | 0 interne Uhr · 1 Funkuhr/Gerätebus · 2 Gebäudeleitbus | 20 |
| 135 | Umschaltung auf Sommerzeit | | 20 |
| 136 | Umschaltung auf Winterzeit | | 20 |

### 5.3.1 Allgemeines

| DP | Parameter | Bereich / Werte | S. | Topic V2 |
|---:|---|---|---|---:|
| 1952 | Reglername | Text | 20 | ➕ `Status/Controller/Name` |
| 132 | I/O in Zustände einbl | 0 aus · 1 an | 20 | — |
| 2759 | Teststatus ausblenden | 0 angezeigt · 1 ausgeblendet | 20 | — |
| 812 | Betriebsart Manuell | 0 WP AUS, Pumpen/Zusatz EIN · 1 WP/Pumpen AUS, Zusatz EIN · 2 WP-Inbetriebnahme Heizen · 3 WP-Inbetriebnahme Kühlen · 4 Kessel als Zusatzheizung | 20 | ➕ `Status/Controller/ManualMode` |
| 883 | WP-Test | 0 keine · 1 Zeiten AUS · 2 +Simul Heizen · 3 +Simul Kühlen (endet nach 30 min) | 20 | — |
| 3365 | Serviceintervall (Sollwert) | h | 20 | ➕ `Config/Service/Interval` |
| 3464 | Datalogger Funktion | 0 keine · 1 immer · 2 Stop 1h nach Fehler · 11 Sonnenkollektor | 20 | — |
| 3465 | Datalogger Intervall | s | 20 | — |
| 3472 | Fehlerspeicher Speicherung | 0 jeden Fehler einmal · 1 jedes Mal | 21 | — |

### 5.3.2 Info-Seiten

`2029` (Grundanzeige 0…8), `2922…2929` (Typ Info-Seite 1…8), `2858…2921`
(Info 1…8, Feld 2…9). Reine Anzeige-Konfiguration – für den Gateway **ohne** Nutzen.

### 5.4 Bodenaustrocknungsprogramm

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 262 | Start | 0 Stop · 1 Start | | 23 |
| 263 | Zeit (Ablauf) | | h | 23 |
| 3425 | Starttemperatur | Vorlaufsoll-Start | °C | 24 |
| 261 | Maxtemperatur | Vorlaufsoll-Ende | °C | 24 |
| 3426 | Zeit Aufheizen | Rampe aufwärts | Tage | 24 |
| 3427 | Zeit Haltung | Verweilzeit | Tage | 24 |
| 3428 | Zeit Abheizen | Rampe abwärts | Tage | 24 |
| 3405 | Energieerzeug Bodenaustrok | 0 Wärmepumpe · 1 Nur Zusatzheizung | | 24 |

### 5.6 Betriebsart Lüfter

| DP | Parameter | Bereich | S. | Topic V2 |
|---:|---|---|---|---:|
| 3093 | Betriebsart Lüfter | 0 Aus · 1 Stufe 1 · 2 Stufe 2 · 3 Stufe 3 · 4 Auto | 24 | ➕ `Status/Ventilation/FanMode` |

---

## 6.2 Anlage konfigurieren

### 6.2.1 Allgemeines

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 185 | Sommerkick Pumpen Mischer | 0 deaktiviert · 1 täglich 16:00 | | 25 | ✅ `Config/General/Sommerknick` |
| 3398 | Entlüftung Primärkreis | Laufzeit | h | 25 | ✅ `Config/General/EntlueftungPrimaerkreis` |
| 3400 | Entlüftung Sek'kreis | Laufzeit | h | 25 | ✅ `Config/General/EntlueftungSekundaerkreis` |
| 3118 | Kühlen allgemein | 0 keine Funktion · 1 Kühlen | | 25 | ➕ `Config/General/CoolingEnabled` |
| 572 | Passivkühlen Hydraulik | 0 keine · 1 mit Primär-, ohne Zonenpumpe · 2 mit beiden · 4 ohne Primär, mit Zone · 9 ohne beide · 3 nur Lüften | | 25 | ➕ `Config/General/PassiveCoolingHydraulics` |
| 3700 | Remote-IO ECM320A | 0 keine · 1 ein ECM320A | | 25 | — |

### 6.2.2 Energieerzeuger

| DP | Parameter | Bereich | S. |
|---:|---|---|---:|
| 102 | Energieerzeuger | 0 keine · 1 einstufig · 2 zweistufig · 4 zwei unabh. WP · 6 zwei WP, 2. für WW · 71 Heizen/Kühlen mit 2 ext. Erz. · 72 Heizen/Kühlen via BACnet | 26 |
| 100 | WP1 und WP2 (Typ) | 50 Sole/Wasser · 51 Wasser/Wasser · 52 Luft/Wasser · 54 Direktverdampfer | 26 |
| 805 | Pufferspeichertyp | 0 keine · 1 ein Fühler · 11 zwei Fühler | 26 |
| 3918 | Kühlpufferspeichertyp | 0 keine · 1 ein Fühler · 11 zwei Fühler | 26 |
| 450 | Zusatzheizung | 0 keine · 1 nur Heizbetrieb · 2 nur WW · 3 Heizbetrieb oder WW | 26 |
| 807 | Sonnenkollektor Hydraulik | 0/1/2/4/21/22/24 | 26 |
| 3904 | Sonnenkollektor Hydraulik 2 | 0 keine · 1 Pumpe · 2 Umlenkventil | 26 |
| 2800 | Fühler zu WP-Regelung | 0 nicht vorhanden · 1 vorhanden | 26 |

### 6.2.3 Energieabnehmer

| DP | Parameter | Bereich | S. |
|---:|---|---|---:|
| 110 | Zone Hydraulik | 0 keine Zone · 1 direkte Zone · 2 nur Mischerkreise · 10 dir. Kreis + Mischerkreis in 1 Zone | 28 |
| 2711 | Anzahl interne Mischerkreise | max. 4 | 28 |
| 111 | Externe Mi'kreise ECM310A | max. 7 gesamt | 28 |
| 480 | Lüften-Sequenz | 0 keine Funktion · 3 Lüften unabhängig | 28 |
| 116 | Warmwasser Hydraulik | 0 keine · 1 Pumpe · 2 Umlenkventil · 3 Pumpe ab Verteiler · 5 Pumpe vor Pufferspeicher · 6 Umlenkventil vor Pufferspeicher · 9 separater Kondensator · 7 nur elektrisch | 28 |
| 118 | WW-elektrisch | 0 keine Funktion · 1 EIN | 29 |
| 760 | Schwimmbadzone | 0 keine · 1…7 Zone 1…7 | 29 |

### 6.2.4 / 6.2.5 Funktion Eingänge E und D

Alle Eingänge teilen dieselbe Auswahlliste (Auszug – die vollständige Liste steht im
Handbuch ab Seite 29):

| Gruppe | Systemweit | Energieerzeuger | Zone | Warmwasser | Fühler | Kühlen | Freigabe | EW-Sperre | Abtauen | WP-Störung | Diverses |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Werte | 1, 2 | 4, 5, 66, 9, 121, 11, 73, 12 | 15, 16, 8, 7, 6, 201, 205, 40…44, 80…89, 203, 207 | 52, 53, 54, 55, 14, 212, 3, 45 | 221…260 | 51, 50, 58…60, 62, 218, 47, 64, 65, 90, 91 | 61, 103 | 107…118 | 111, 112, 131, 132 | 151…174, 351, 352, 361, 362, 371, 372 | 210, 211, 48, 56, 57, 63, 69, 213, 92, 214…217, 99 |

| Parameter | DP |
|---|---|
| Funktion Eingang E1…E10 | 1958…1967 |
| Funktion Eingang E21…E28 | 2669…2676 |
| Funktion Eingang D1…D5 | 1953…1957 |
| Funktion Eingang D21…D25 | 2664…2668 |
| Funktion Eingang D26…D28 | 2980…2982 |

> **Für den Gateway relevant:** erst diese Konfiguration legt fest, *was* ein Eingang
> bedeutet. Die Topics `States/Inputs/E6…` sind damit installationsabhängig.

### 6.2.6 / 6.2.7 Funktion Ausgänge Y und R

Beide Ausgangsarten teilen dieselbe Auswahlliste (Auszug ab Seite 35):

| Parameter | DP |
|---|---|
| Funktion Ausgang Y1, Y2 | 1978, 1979 |
| Funktion Relaisausgang R1…R10 | 1968…1977 |
| Funktion Relaisausgang R21…R27 | 2677…2683 |

Wichtige Werte: `2/3` Verdichter 1/2 · `4/5` Primärpumpe/Ventilator 1/2 · `6/7` Abtauen/Kühlen
1/2 · `8/9` Pufferladepumpe 1/2 · `11/12` WP-Entlastung 1/2 · `13` Passivkühlen · `15` Heizen ·
`26/27` Abtauen 1/2 · `30/31` Zusatzheizung 1/2 · `40/41` Warmwasser/WW-elektrisch ·
`50/51` Kollektorpumpe 1 / Kollektortemp. zu hoch · `60…63` Lüfterstufen/Freie Kühlung ·
`1` direkte Kreispumpe · `110…142` Mischerpumpen/-ventile Zonen 1…4 · `14/16` 2-Punkt-Funktion 1/2 ·
`90` freie Schaltuhr · `92` Fehler · `161…194` freie Module · `501…642` Logikmodul-Ausgänge.

### 6.2.8 Typ Eingänge E

| Parameter | DP | Werte |
|---|---|---|
| E1…E9, E21…E28 | 2712…2728 | 0 NTC 10k · 1 PTC 1k · 2 Pt1000 · 3 Ni1000 · 11 Aktivsensor 4-20 mA · 12 Drucksensor rel. 4-20 mA · 13 Aktivsensor 0-10 V · 14 Drucksensor rel. 0-10 V · 21 GBS01 |

### 6.2.9 Zähler

| DP | Parameter | S. |
|---:|---|---:|
| 835 | Zählerfaktor E9 | 40 |
| 834 | Zählerfaktor E10 | 41 |
| 3586 | Durchflussgeber Einheit | 41 |
| 3912 | Durchflussgeber Einheit Kollektor | 41 |

### 6.2.10 Aktivsensor

Acht Funktionen mit je fünf Parametern:

| Parameter | DP-Bereich |
|---|---|
| Minimum E1…E28 | 3252…3268 |
| Maximum E1…E28 | 3269…3285 |
| Tau E1…E28 | 449, 3368…3395 |
| Konvertierung E1…E28 | 3235…3251 |
| Funktion 1…8 | 3286…3293 |
| Schwelle 1…8 | 3294…3301 |
| Schaltdiff 1…8 | 3310…3317 |
| Wirkungssinn 1…8 | 3302…3309 |
| Fehlerfunktion 1…8 | 3318…3325 |

### 6.2.11–6.2.15 Wirkungssinn, Eichung, Typen

| Parameter | DP |
|---|---|
| Wirkungssinn E1…E9, E10, E21…E28 | 2739…2747, 1987, 2748…2755 |
| Wirkungssinn D1…D5, D21…D28 | 2729…2733, 2734…2738, 2983…2985 |
| Wirkungssinn Y1, Y2 | 1999, 2000 |
| Wirkungssinn R1…R10, R21…R27 | 1989…1998, 2684…2690 |
| Eichung E1…E9, E21…E28 | 2841…2857 |
| Zone # E131…E133 (Typ) | 3541…3543 |

### 6.2.16 / 6.2.17 Ausgänge Y1 / Y2 stetig

| DP | Parameter | Bereich | E | Y2-Pendant |
|---:|---|---|---|---:|
| 144 | Y1 P-Band Xp | 2…99 | K | 3095 |
| 145 | Y1 Offset P-Band | −99…99 | K | 3099 |
| 3617 | Y1 Of P-Ban alt | −99…99 | K | 3618 |
| 3611 | Y1 Offset Ausgang | 0…100 | K | 3612 |
| 146 | Y1 I-Anteil | 0…100 | s | 3100 |
| 147 | Y1 D-Anteil | 0…100 | s | 3101 |
| 411 | Y1 Minimal | 0…100 | % | 511 |
| 412 | Y1 Maximal | 0…100 | % | 512 |
| 3592 | Y1 P-Band Xp Kühlen | 2…99 | K | 3596 |
| 3593 | Y1 Offset P-Band Kühlen | −99…99 | K | 3597 |
| 3613 | Y1 Minimal Kühlen | 0…100 | % | 3615 |
| 3614 | Y1 Maximal Kühlen | 0…100 | % | 3616 |
| 3232 | Y1 Max Mode | 0 nicht aktiv bei Übersteuerung · 1 immer aktiv | | 3233 |
| 413 | Y1 Inversmode | 0 Normal · 1 Invers | | 513 |
| **3096** | **Y1 Antriebstyp** | **0 0..10 V · 1 0..10 V stop=1 V · 11 PWM** | | 3097 |
| 410 | Y1 Periodendauer PWM | 0.0…99.0 | s | 510 |
| 3488 | Y1 Zusatzmode | 0 Normal · 1 Venti stetig nach Festwert | | 3494 |
| 3489…3492 | V1 Ventilator-Festwerte St 1/2, Flüster St 1/2 | 0…100 | % | 3495…3498 |
| 3493 | V1 Aussentemp'schwell Venti | 10…30 | °C | 3499 |
| 3591 | V1 Sollwert Kühlen | 0…99 | °C | 3595 |

> **DP 3096 hat drei Werte (0, 1, 11)** – der Katalog erlaubt heute nur 0…1. Siehe
> [review-ist-implementierung.md](review-ist-implementierung.md#r4).

### 6.2.18 Zone # (Zusatzmischerkreis-Modul ECM310A)

| DP | Parameter | Werte |
|---:|---|---|
| 910 | Funktion Eingang D132 | 0 keine · 6 Minimalsoll Vorlauf · 7 Raumsollwert normal · 8 Raumsollwert reduz · 15 Standby · 16 Nur Warmwasser · 50 Kühlungssperre · 61 Schwimmbadfreigabe · 64 Taupunktwächter · 201 Raumsoll-Korr · 205 Anwesenheit |

### 6.2.19 Remote-IO ECM320A

| Parameter | DP-Bereich |
|---|---|
| Funktion E141…E145 | 1958, 3713…3716 |
| Funktion R141…R144 | 1968…1971 (geteilt), 3719…3722 |
| Typ E141…E145 | 3723…3725, 3806, 3807 |
| Zählerfaktor E144/E145 | 3825, 3826 |
| Aktivsensor (Min/Max/Tau) | 3732…3734, 3804, 3805 / 3729…3731, 3802, 3803 |
| Konvertierung | 3796…3798, 3810, 3811 |
| Wirkungssinn | 3735…3741 · R141…R144: 3742…3745 |
| Eichung | 3746…3748, 3812, 3813 |
| Y141 stetig | 3749…3767 |
| Y142 stetig | 3849…3867 |

### 6.2.20 Regler-Interkommunikation

| DP | Parameter | E | S. |
|---:|---|---|---:|
| 3334 | BACnet-Adr Aussentemp | Adresse | 57 |
| 3463 | BACnet-Adr Pu'sp'temp | Adresse | 57 |
| 3335 | BACnet-Adr Energieerz'soll | Adresse | 57 |
| 3462 | BACnet-Adr Puffersp'soll | Adresse | 57 |
| 3931 | BACnet-Adr sync WW-Zw'lad | Adresse | 57 |
| 3932 | Anz sync WW-Zwangsladung | Anzahl | 57 |
| 4039 | Freig Reg'Interk freie Module | 0/1 | 57 |

---

## 6.3 Energieerzeuger parametrieren

### 6.3.1 Verhalten

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 880 | Vorlaufzeit Primär | 0.0…99.0 | min | 58 | ✅ `Config/EnergyProducer/PrimaryPumpPreRunningTime` |
| 881 | Nachlaufzeit Primär | 0.0…99.0 | min | 58 | ✅ `Config/EnergyProducer/PrimaryPumpPostRunningTime` |
| 3234 | Vorlaufzeit Kond'pumpe | 0.0…99.0 | min | 58 | ➕ `Config/EnergyProducer/CondenserPumpPreRunningTime` |
| 188 | Nachlaufzeit Kond'pumpe | 0.0…99.0 | min | 58 | ➕ `Config/EnergyProducer/CondenserPumpPostRunningTime` |
| 921 | Festwertregelung | 0 keine · 1 EIN | | 58 | ➕ `Config/EnergyProducer/FixedValueControl` |
| 158 | Festsollwert WP Heizen | 0…125 | °C | 58 | ➕ `Config/EnergyProducer/FixedSetPointHeating` |
| 577 | Festsollwert WP Kühlen | 0…125 | °C | 58 | ➕ `Config/EnergyProducer/FixedSetPointCooling` |
| 590 | Stillst'mode Umsch Ab/Kü | 0 · 1 · 2 · 3 · 11 | | 58 | — |
| 493 | Stillst'zeit Umsch Ab/Kü | 0…300 | s | 58 | — |
| 3583 | Verdi'min't Umsch Abt/K | 0…600 | s | 58 | — |
| 3584 | Verdi'min'dr Umsch Abt/K | 0.0…10.0 | bar | 58 | — |
| 3117 | Stillst'mod Umsch pass'kü | 0 · 1 | | 58 | — |
| 3116 | Still'zei Umsch pass'kü | 0…300 | s | 58 | — |
| 924 | Umschaltverzög WW Mode | 0 · 1 · 2 | | 58 | — |
| 848 | Umschaltverzögerung WW | 0.0…10.0 | min | 58 | ➕ `Config/Warmwater/SwitchOverDelay` |
| 176 | Opti WP-Ausschalt | 0 keine · 1 Abs. nach Sch'uhr verzögern | | 58 | — |
| 860 | Zuweisung Pu-/WP-Rückl'füh | 0 intern · 1 Gebäudeleitbus | | 58 | — |

### 6.3.2 / 6.3.3 Stufe 1 und Stufe 2

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 140 | Schaltdifferenz Stufe 1 | 2…20 | K | 60 | ✅ `Config/EnergyComsumer/HeatPump/SwitchingDifferenceStage1` |
| 143 | Min WP-Laufzeit | 0…30 | min | 60 | ✅ `Config/EnergyComsumer/HeatPump/MinHeatpumpRunningTime` |
| 846 | Wiedereinschaltverzög 1 | 0…60 | min | 60 | ✅ `Config/EnergyComsumer/HeatPump/RestartDelay` (V1) |
| **444** | **WP-Vorlauf Maximal** | **0…150** | °C | 60 | ✅ `Config/EnergyComsumer/HeatPump/FlowMaxTemperature` |
| 141 | Schaltdifferenz Stufe 2 | 2…20 | K | 61 | ➕ `Config/EnergyProducer/SwitchingDifferenceStage2` |
| 142 | Min Einschaltverz Stufe 2 | 0…60 | min | 61 | ➕ `Config/EnergyProducer/MinRunTimeStage2` |
| 847 | Wiedereinschaltverzög 2 | 0…60 | min | 61 | ➕ `Config/EnergyProducer/RestartDelayStage2` |
| 108 | Bivalenzstrategie Stufe 2 | 0 · 1 parallel · 11 alternierend · 21 alternativ | | 61 | — |
| 803 | Bival'schaltpu St 2 | −40…60 | °C | 61 | ➕ `Config/EnergyProducer/BivalenceStage2` |
| 445 | WP-Vorlauf 2 Maximal | 0…150 | °C | 61 | — |
| 3396 | Zwangsumschaltung Stufen | 0…600 | min | 61 | — |

### 6.3.4 Pufferspeicher

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 1982 | Pufferspeicher durchladen | 0…3, 11…15 | | 61 |
| 850 | Ext Pufferspeichersoll Heizen | 0…99 | °C | 61 |
| 3919 | Ext Pufferspeichersoll Kühlen | 0…99 | °C | 61 |

### 6.3.5 Begrenzungen

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 150 | Energieerzeuger Minimal | 0…99 | °C | 62 |
| 151 | Energieerzeuger Maximal | 0…125 | °C | 62 |
| 152 | Energieerz Maximal Heizen | 0…125 | °C | 62 |

### 6.3.6 WP-Störungen

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 856 | Verzögerung Hochdruck | 0…999 | s | 62 |
| 3113 | Verrieg'mode Hochdruck | 0 · 1…5 | | 62 |
| 857 | Verzögerung Niederdruck | 0…999 | s | 62 |
| 3407 | Verz Niederdruck Abtau | 0…999 | s | 62 |
| 3114 | Verrieg'mode Niederdruck | 0 · 1…5 | | 62 |
| 1983 | Mode Niederdruck | 0 Normal · 1 ohne Wirk während Abtauen | | 62 |
| 858 | Verzög Sicherheitskette | 0…999 | s | 62 |
| 925 | Verrieg'mode Sicherheitsk | 0 · 1…5 | | 62 |
| 859 | Verzög Wärmequelle | 0…999 | s | 62 |
| 3115 | Verrieg'mode Wärmequelle | 0 · 1…5 | | 62 |
| 926 | Mode Wärmequelle | 0 · 1 · 2 | | 63 |

> Die Verriegelungsmodi zählen Störungen **innerhalb 36 Stunden**. Eine Verriegelung
> verlangt einen manuellen Eingriff am Gerät – der Gateway kann sie **nicht** aufheben.

### 6.3.7 WP-Schutz

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 494 | Kondensat'frostschutz | 0 · 31 · 37 · 39 · 63 · 80 | | 63 | ✅ `Config/EnergyComsumer/Protection/KondensatorFrostschutz/KondensatFrostschutz` |
| 1984 | Kondensatorfrostsch. Mode | 0 · 1 | | 63 | — |
| 495 | Kond'frostschutztemp | −20…30 | °C | 63 | ✅ `…/KondFrostschutzTemp` |
| 496 | Kond'frostschutz Schaltdiff | 2…10 | K | 63 | ✅ `…/KondFrostschutzSchatdiff` |
| 497 | Zusatzheiz bei Kond'frostsch | 0 · 1 · 2 | | 63 | — |
| 3471 | Kond'frostwarnungstemp | −20…30 | °C | 63 | ➕ `…/KondFrostWarnungTemp` |
| 901 | WP-Entlastung | 0 · 1 · 3 · 4 · 5 | | 63 | — |
| 1985 | WP-Entlastung Ausgang | 0 · 1 | | 63 | — |
| 920 | WP-Entlastung Vorlaufzeit | 0…99 | s | 63 | — |
| 923 | WP-Entlastung Nachlaufzeit | 0…99 | s | 63 | — |
| 498 | WP-Frostschutz Mode | 0 · 1 · 2 · 3 · 10 | | 64 | ➕ `Config/EnergyProducer/FrostProtectionMode` |
| 446 | WP-Frostschutztemperatur | 2.0…20.0 | °C | 64 | ➕ `Config/EnergyProducer/FrostProtectionTemperature` |
| 3661 | WP-Frostschutztemp Heizen | −30…20 | °C | 64 | — |
| 3662 | Sperrzeit nach Abtauen | 0…30 | min | 64 | — |
| 448 | WP-Frostschutztemperatur 2 | 2.0…20.0 | °C | 64 | — |
| 499 | WP-Frostschutz Schaltdiff | 2…10 | K | 64 | — |
| 490 | MOP | 0 · 35 · 37 | | 64 | — |
| 491 | MOP-Temperatur | 0…60 | °C | 64 | — |
| 492 | MOP-Schaltdifferenz | 2…20 | K | 64 | — |
| 3479 | Heissgastemp'überwachung | 0…5 · 9 · 11…15 · 19 | | 65 | — |
| 3480 | Heissgas Warnung | 70…150 | °C | 65 | — |
| 3481 | Heissgas Störung | 70…150 | °C | 65 | — |
| 3482 | Heissgastemp Schaltdiff | 2…10 | K | 65 | — |
| 592 | Soll'sch n DeltaT | 0 · 39 | | 65 | — |
| 593 | Soll'schiebung Max'deltaT | 20…80 | K | 65 | — |
| 900 | WP-Zusatzfunktion | 0 · 1 Frostgefahr | | 65 | — |
| 1986 | WP-Zusatzfunktion Sollwert | 3…10 | K | 65 | — |
| 3934 | Überwachung Verdampferfüh | 0 · 1 | | 65 | — |
| 3935 | Überwachung Hochdr-Niederdr | 0.0…10.0 | bar | 65 | — |

### 6.3.8 / 6.3.9 Abtauen 1 und 2

| Parameter | DP (Abtau 1) | DP (Abtau 2) | Bereich |
|---|---|---|---|
| Abtaumethode | 541 | 3179 | 0 · 2 · 3 · 4 · 5 · 6 |
| Verzög Ref'messung | 440 | 3196 | 0.0…99.0 min |
| Abtauverzögerung | 3105 | 3195 | 0.0…99.0 min |
| Abtausperrzeit | 441 | 3194 | 0.0…240.0 min |
| Abtauzeit Maximal | 442 | 3192 | 0.0…99.0 min |
| Abtaustillstandzeit | 443 | 3191 | 0.0…99.0 min |
| Aussen't für Abt'still'zeit | 3104 | 3190 | −20…20 °C |
| Freigabeaussentemp Abtau | 546 | 3189 | 5…30 °C |
| Abtau Endtemperatur | 547 | 3207 | 2…40 °C |
| Abtau Schaltdifferenz | 548 | 3188 | 1.0…10.0 K |
| Abtau Venti bei Freigabe | 972 | 3186 | 0 · 3 · 4 · 6 |
| Freigabeaussentemp Venti | 973 | 3185 | 4…20 °C |
| Abtauzeit Maximal Venti | 3103 | 3193 | 0.0…99.0 min |
| Freig'verd't Venti na Abt | 3106 | 3182 | −20…20 °C |
| Min'verdampfertemp Abtau | 549 | 3184 | −50…10 °C |
| Temp'diff Grundreferenz | 970 | 3183 | 1…10 K |
| Sicherheitsabtau Interv | 3102 | 3181 | 0.0…6.0 h |
| Erfolglose Abtauung | 3107 | 3180 | 0 · 11…19 · 21…29 |
| Abtaustop durch WP AUS | 3477 | 3478 | 0 · 1 |

### 6.3.10 Abtau Zusatz

| DP | Parameter | Werte |
|---:|---|---|
| 594 | Abtaustufe | 0 aktuelle Stufen · 1 eine Stufe · 3 Stufe 1 und 2 |
| 974 | Abtau Pumpen | 0 mit Pumpen · 1 ohne Pumpen · 2 mit allen Pumpen |
| 3367 | Abtauendtemp'quelle | 0 Verdampfertemp · 1 Kondensationstemp |
| 3406 | Offzeit Abtauen | 0 · 1 |
| 3408 | EW-Sperre beim Abtau | 0 abbrechen · 1 unterbrechen |
| 3585 | Manuell Abtauen Verhalten | 0 temperaturunabhängig · 1 temperaturabhängig |

### 6.3.11 Zusatzheizung

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 459 | Zusatzheizung Position | 0 im Pufferspeicher · 1 im WP-Vorlauf | | 70 |
| 1988 | Zusatzheizung bei Störung | 0 reduziert · 1 normal | | 70 |
| 452 | Zusatzheiz Bivalenzstrateg | 0 · 1 · 21 · 31 | | 70 |
| 453 | Zusatzheiz Bival'schaltpu | −30…20 | °C | 70 |
| 3108 | Zusatzheiz Schaltdiff Bival | 1…10 | K | 70 |
| 454 | Zusatzheizung Schaltdiff | 2…20 | K | 70 |
| 455 | Zusatzheizung Verzögerung | 0…60 | min | 70 |
| 3109 | Zusatzheiz Verz bei Störung | 0…600 | min | 70 |
| 456 | Zusatzheiz 2 Bival'schaltpu | −30…20 | °C | 70 |
| 458 | Zusatzheizung 2 Verzögerung | 0…120 | min | 70 |
| 550 | Zusatzheizung und WW | 0 · 1 · 2 · 3 | | 70 |

### 6.3.12 Kühlen

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 2756 | Bivalenzstrategie Kühlen | 0 · 1 | | 71 | — |
| 476 | Bivalenzschaltpunkt Kühlen | 0…40 | °C | 71 | — |
| 579 | Passivkühlen Mode | 0 · 1 · 2 | | 71 | ➕ `Config/Cooling/PassiveMode` |
| 574 | DeltaT für Umsch Pas'kühlen | 0…20 | K | 71 | ✅ `Config/Freecooling/DeltaTempForSwitching` |
| 575 | Schaltdiff Umsch Pas'kühlen | 2…10 | K | 71 | ✅ `Config/Freecooling/DiffForSwitching` |
| 576 | Temp min bei Kühlen | 6…99 | °C | 71 | ✅ `Config/Freecooling/MinTemperature` |
| 980 | Kühlen und Pufferspeicher | 0…4 | | 71 | — |
| 981 | Aktivkühlen Hydraulik | 0 wie Heizen · 1 ohne Zonenpumpe | | 71 | — |
| 945 | Kühlen und WW | 0 · 1 · 2 | | 71 | — |
| 765 | Schwimmbad bei Passivkühlen | 0 · 1 | | 71 | — |
| 570 | WP-Begrenzung beim Kühlen | 0 · 1 | | 71 | — |

### 6.3.13 Sonnenkollektor

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 401 | Temp'differenz Pumpe EIN | 4…20 | K | 72 |
| 402 | Temp'differenz Pumpe AUS | 0…16 | K | 72 |
| 3901 | WT-Temp'diff Pumpe EIN | 2…20 | K | 72 |
| 3902 | WT-Temp'diff Pumpe AUS | 0…16 | K | 72 |
| 403 | Funkt Kollektortemp zu hoch | 0 · 1 · 2 | | 72 |
| 404 | Kollektortemperatur Maximal | 0…240 | °C | 72 |
| 3900 | Kollektortemp Maximal Ende | 0…404−10 | °C | 72 |
| 408 | Frostschutztemp Kollektor | −40…3 | °C | 72 |
| 405 | Funkt Puffersp'temp zu hoch | 0…3 | | 72 |
| 406 | Pufferspeichertemp Maximal | 0…99 | °C | 72 |
| 407 | Sch'diff Pu'speicher Maximal | 1…50 | K | 72 |
| 3445 | Solar, WW-Hochsollwert | 0…99 | °C | 72 |
| 3447 | Solar, Vorrang | 0 · 1 · 2 · 11 · 12 | | 72 |
| 3446 | Solar, Vorrangschaltdiff | 2…99 | K | 72 |
| 3448 | Solar, Timeout Teilvorrang | 0…240 | min | 72 |
| 461 | Freig Raumsoll'überhöh | 0 · 1 · 2 | | 73 |
| 460 | Schaltdiff Raumsoll'überhöh | 0…80 | K | 73 |
| 3906 | Wärmemengenmess Kollektor | 0 · 1 | | 73 |
| 3907 | Kollektorfrostschutzmittel | 0 H2O · 1 Ethylenglykol · 2 Propylenglykol | | 73 |
| 3908 | Koll'frostschutzmi'konzentraz | 10…50 | % | 73 |
| 3913 | Durchfluss Kollektor | 0.00…99.99 | m³/h | 73 |

### 6.3.14 Wärmemengenmessung

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 3340 | Wärmemengenmessung | 0 keine · 1 EIN · 2 EIN, WW separat | | 74 |
| 3341 | Durchfluss | 0.00…99.00 | m³/h | 74 |
| 3348 | Durchfluss WW | 0.00…99.00 | m³/h | 74 |

---

## 6.4 Energieabnehmer parametrieren

### 6.4.1 Zone – Verhalten

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 170 | Gebäudeträgheit | 0 ohne · 1 leicht (14 h) · 2 normal (32 h) · 3 schwer (48 h) | | 75 | ✅ `Config/EnergyComsumer/Zone/Gebäudeträgheit` |
| 112 | Mischerantrieb | 2 2-Punkt · 3 3-Punkt | | 75 | — |
| 113 | Mischerlaufzeit | 1…30 | min | 75 | ✅ `…/Mischerlaufzeit` |
| 159 | Ext Vorlaufsollwert Minimal | 0…125 | °C | 75 | — |
| 168 | Überhöh Vorlauf/Energieerz | 0…30 | K | 75 | ✅ `…/ÜberhöhVorlaufEnergieerz` |
| 186 | Nachlaufzeit Zonenpumpe | 0.0…30.0 | min | 75 | ✅ `…/NachlaufzeitZonenpumpe` |
| 462 | Raumsoll'überh Sol | 0…6 | K | 75 | ✅ `…/RaumsollüberhSol` |
| 182 | Raumfühler Fernbedienung | 0 keine · 1 EIN | | 75 | — |
| 169 | Zuweisung Aussentemp | 0 kein Aussenfühler · 1 Aussenfühler 1 · 2 Aussenfühler 2 · 3 Mittelwert · 4 aus ext. Zone · 5 Gebäudeleitbus · 6 Gerätebus | | 75 | ➕ `Status/Zone/OutdoorSensorSource` |
| 3600 | Neurobat | 0 keine · 1 Neurobat · 9 Econesta + | | 75 | — |

### 6.4.1 Heizkurve Zone

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 160 | Fixpunkt | 10…40 | °C | 76 | ✅ `Config/EnergyComsumer/Heizkurve/Fixpunkt` |
| 161 | Aussentemp im Auslegepunkt | −30…0 | °C | 76 | ✅ `…/AussentempImAuslegepunkt` |
| 162 | Vorlauftemp im Auslegepunkt | 20…99 | °C | 76 | ✅ `…/VorlauftempImAuslegepunkt` |
| 164 | Adaptierter Fixpunkt | 10…40 | °C | 76 | ✅ `…/AdaptierterFixpunkt` |
| 165 | Adapt Vorl'temp im Auslegep | 20…99 | °C | 76 | ✅ `…/AdaptVorlauftempImAuslegepunkt` |
| 167 | Heizkennlinienadaption | 0 keine · 1 manuell/auto mit Raumfühler · 2 manuell mit Korrektureingabe | | 76 | ✅ `…/Heizkennlinienadaption` |

### 6.4.1 Heizen Zone

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 180 | Tagesheizgrenze | 0 AUS · 1 EIN | | 76 | ✅ `Config/EnergyComsumer/HeizenZone/Tagesheizgrenze` |
| 3569 | Tagesheizgrenze Offset | −5.0…5.0 | K | 76 | ✅ `…/TagesheizgrenzeOffset` |
| 181 | Winterheizgrenze | 0.0…10.0 | K | 76 | ✅ `…/Winterheizgrenze` |
| 183 | Raumeinfluss | 0…150 | % | 76 | ✅ `…/Raumeinfluss` |
| 172 | Optimierung Heizschaltzeiten | 0 · 1 | | 76 | ✅ `…/OptimierungHeizschaltzeiten` |
| 173 | Max Vorhaltezeit Heizen | 0…180 | min | 76 | ✅ `…/MaxVorhaltezeitHeizen` |
| 174 | Max Vorhaltezeit Absenken | 0…120 | min | 76 | ✅ `…/MaxVorhaltezeitAbsenken` |
| 154 | Vorlauf Maximal | 0…125 | °C | 76 | ✅ `…/VorlaufMaximal` |
| 153 | Vorlauf Minimal | 0…99 | °C | 76 | ✅ `…/VorlaufMinimal` |
| 3576 | Raumsoll-Korr Zone | −5.0…5.0 | K | 76 | ✅ `…/RaumsollKorrZone` |

### 6.4.1 Kühlen Zone

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 470 | Kühlen | 0 keine Funktion · 1 Kühlen · 3 Nur Kühlen | | 76 | ✅ `Config/EnergyComsumer/CoolingZone/Cooling` |
| 473 | Sommerkühlgrenze | −10…10 | K | 77 | ✅ `…/Sommerkühlgrenze` |
| 3562 | Kühlraumsoll'abst normal | 0.0…10.0 | K | 77 | ✅ `…/KühlraumsollAbstNormal` |
| 863 | Kühlraumsoll'abst reduziert | 0.0…10.0 | K | 77 | ✅ `…/KühlraumsollAbstReduziert` |
| 864 | Kühlraumsoll'abst standby | 0.0…10.0 | K | 77 | ✅ `…/KühlraumsollAbstStandby` |
| 474 | Fixpunkt Raumsoll Küh (20 °C) | 10…30 | °C | 77 | ✅ `…/FixpunktRaumsoll` |
| 475 | Steilheit Raumsoll-Schiebung | 0.0…5.0 | K/°C | 77 | ✅ `…/SteilheitRaumsollSchiebung` |
| 477 | Min Vorl'temp Kühlen (20 °C) | 0…99 | °C | 77 | ✅ `…/MinVorlTemp20` |
| 478 | Min Vorl'temp Kühlen (40 °C) | 0…99 | °C | 77 | ✅ `…/MinVorlTemp40` |
| 479 | Abs min Vorlauftemp Kühlen | 0…99 | °C | 77 | ✅ `…/AbsMinVorlauftemp` |
| 571 | Freigabe der Kühlung | 0 automatisch · 1…3 RFB511 · 4 Bedieneinheit · 5 Eingang · 6 Gebäudeleitbus | | 77 | ➕ `Config/Cooling/Release` |
| 578 | Raumeinfluss bei Kühlen | 100…999 | % | 77 | ✅ `…/RaumeinflussBeiKühlen` |
| 3085 | Umschaltung Raumfühler | 0 keine · 1 Heizen Tr1/Kühlen Tr2 · 2 Heizen Tr2/Kühlen Tr1 | | 77 | ✅ `…/UmschaltungRaumfühler` |

> ⚠️ **DP 864 ist doppelt belegt** – „Kühlraumsoll'abst standby" (hier) und
> „Wiedereinschaltverzögerung Kompressor" (§6.3.2, Bereich 20…60 min). Siehe
> [review-ist-implementierung.md](review-ist-implementierung.md#r6).

### 6.4.2 Warmwasser

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 117 | WW-Ausrüstung | 0 Thermostat · 1 Fühler · 2 zwei Fühler (Schichtspeicher) | | 77 | ➕ `Config/Warmwater/Equipment` |
| 3466 | WW-Durchladen | 0 · 2 · 3 | | 77 | — |
| 192 | Legionellenschutzfunktion | 0 keine · 1…7 Mo…So · 8 täglich | | 77 | ✅ `Config/EnergyComsumer/Warmwater/Legionellenschutzfunktion` |
| 199 | WW-Zwangsladung | 0 keine · 1 täglich bei erster WW-Ladung | | 78 | ✅ `…/Zwangsladung` |
| 196 | WW-Vorrang | 0 keiner · 1 Teilvorrang · 2 voller Vorrang | | 78 | ✅ `…/Vorrang` |
| 193 | Energieerz'anhebung (Fühler) | 2…60 | K | 78 | ✅ `…/EnergieerzAnhebungFühler` |
| 194 | Energieerz'soll (Thermostat) | 0…99 | °C | 78 | ✅ `…/EnergieerzSollThermostat` |
| 197 | WW-Nachlaufzeit | 0.0…10.0 | min | 78 | ✅ `…/Nachlaufzeit` |
| 175 | WW-Freigabe | 0 nach Schaltuhr · 1 1 h vor Zonenbeginn · 2 dauernd | | 78 | ✅ `…/Freigabe` |
| 195 | Leistungsvorwahl bei WW | 0 bedarfsabhängig · 1 aktuelle Stufe · 2 beide · 3 Stufe 1 · 4 Stufe 2 | | 78 | ➕ `Config/Warmwater/PowerSelection` |
| 190 | WW-Sollwert Maximal | 5…99 | °C | 78 | ✅ `…/SollwertMaximal` |
| 447 | WW-Sollwert Maximal WP | 0…65 | °C | 78 | ✅ `…/SollwertMaximalWP` |
| 191 | WW-Schaltdifferenz | 1…10 | K | 78 | ✅ `…/Schaltdifferenz` |
| 157 | WW-Entladeschutz | **0 keine Funktion · 3 EIN** | | 78 | ✅ `…/Entladeschutz` |

> ⚠️ **DP 157 hat die Werte 0 und 3** (nicht 0/1). Der Katalog erlaubt heute 0…1 – deshalb
> meldet Home Assistant für dieses Item „Value outside of the range". Siehe
> [review-ist-implementierung.md](review-ist-implementierung.md#r3).

### 6.4.2 Warmwasser-elektrisch

| DP | Parameter | Werte |
|---:|---|---|
| 421 | WW-el über Eingang | 0 keine · 1 über Eingang · 2 über Eingang oder im Sommer · 3 über Eingang im Sommer |
| 422 | WW-el bei Pufferspeicher | 0 keine · 1 Pufferspeichertemp zu tief · 2 nur im Sommer |
| 423 | t'diff bei Pufferspeicher | −20…20 K |
| 424 | WW-el bei Manuell | 1…4 (WW-Sollwert Frost/Reduziert/Normal/Legio) |
| 425 | WW-el bei WP-Störung | 0 · 2 · 3 |
| 426 | WW-el nach WW-Sollwert | 0 · 2 · 3 |
| 427 | WW-el nach Bival'schaltpunkt | 0 · 1 · 2 |
| 428 | WW-el Bivalenzschaltpunkt | −20…20 °C |
| 429 | WW-el Verzögerung | 0…600 min |
| 177 | Optimierung WW-el | 0 keine · 1 mit WP erst ab WW auf reduz |

### 6.4.3 Heizen System

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 163 | Energieerz'temp im Auslegep | 20…99 | °C | 80 | ✅ `Config/HeatingSystem/EnergieerzeugerTempImAuslegepunkt` |
| 166 | Adap En'erz'temp im Auslegep | 20…99 | °C | 80 | ✅ `…/AdaptEnergieerzeugerTempImAuslegepunkt` |
| 3608 | Überhöh Mischer-Direkt'kreis | 0…20 | K | 80 | — (Objekt existiert am Regler nicht, 2026-11-18 aus dem Katalog entfernt) |
| 862 | Raumsoll norm nach Aus'temp | −30…10 | °C | 80 | ➕ `Config/HeatingSystem/SetPointNormalBelowOutdoor` |
| 187 | Anlagenfrostschutz | −15…20 | °C | 80 | ✅ `…/Anlagenfrostschutz` |

### 6.4.4 Schwimmbad

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 761 | Schwimmbad Hydraulik | 1 · 2 · 5 · 6 · 11 | | 80 |
| 763 | Schwimmbadtemp Maximal | 10…45 | °C | 81 |
| 764 | Schwimmbad Schaltdifferenz | 1…10 | K | 81 |
| 762 | Schwimmbadvorrang | 0 keine · 1 Teilvorrang | | 81 |

### 6.4.5 Lüften

| DP | Parameter | Bereich | E | S. | Topic V2 |
|---:|---|---|---|---:|---|
| 3092 | Bedienung Lüfter | 0 keine · 1 AUS/Stufe 1-3 · 2 AUS/Stufe 1-3/Auto | | 81 | ➕ `Config/Ventilation/FanControl` |
| 3224…3226 | Bedien Lüfter Drehzahl St 1…3 | 0…100 | % | 81 | ➕ `Config/Ventilation/SpeedStage1…3` |
| 471 | Schaltdiff Raumregelung | 0.4…3.0 | K | 81 | — |
| 472 | Schaltdiff Raumregelung 2 | 0.4…5.0 | K | 81 | — |
| 481 | Lüfter intermittierend | 0…60 | min/h | 81 | — |
| 482 | Offset Zuluft Sommer | −10…10 | K | 81 | — |
| 484 | t'diff freie Kühlung | 0…5 | K | 81 | — |
| 486 | Lüfterstufenschaltung | 1 parallel · 2 seriell | | 81 | — |
| 417 | Drehzahl Lüfter Man, Standby | 0…100 | % | 81 | — |

---

## 6.5 Freie Regelfunktionen

### 6.5.1 Freie Schaltuhr

| DP | Parameter | Werte |
|---:|---|---|
| 882 | Funktion der freien Schaltuhr | dieselbe Auswahlliste wie Funktion Eingang E1 |
| 3397 | Freie Schaltuhr intermittierend | 1…60 min/h |

### 6.5.2 2-Punkt-Funktion

| DP | Parameter | Bereich | E | S. |
|---:|---|---|---|---:|
| 595 | 2-Punkt-Funktion | 0 · 1 · 2 · 3 · 4 · 5 · 101 | | 84 |
| 597 | 2-Punkt Schaltpunkt | −20…150 | °C | 84 |
| 598 | 2-Punkt Schaltdifferenz | 2…20 | K | 84 |

### 6.5.3 Logikmodule 1…4

| Parameter | DP |
|---|---|
| LM1…LM4 Funktion | 3371…3374 |
| LM1…LM4 Eingang A | 3381…3384 |
| LM1…LM4 Eingang B | 3391…3394 |
| LM1…LM4 Reglerfunktion | 3587…3590 |
| LM1…LM4 Eingang A/B freier DP | 3940…3947 |
| LM1…LM4 Eingang A/B BACnet-Adr | 4040…4047 |

### 6.5.4 Konvertermodule 1…4 (KM1…KM4)

> **Das sind die „Comfofond"-Parameter des Item-Katalogs.** Die Konvertermodule bilden
> Zwei-Punkt-Funktionen auf einen analogen Messwert – bei der ComfoBox wird damit die
> Heiz-/Kühlregister-Ansteuerung der Lüftung (Comfofond-L) geführt.

| Parameter | KM1 | KM2 | KM3 | KM4 | Bereich |
|---|---|---|---|---|---|
| Funktion | 3401 | 3402 | 3403 | 3404 | 0 · 1 · 2 · 3 · 5 · 6 · 11 · 12 · 13 |
| Eingang E1 | 3411 | 3412 | 3413 | 3414 | Auswahlliste wie E1 |
| Eingang E2 | 3421 | 3422 | 3423 | 3424 | |
| Eingang A | 3415 | 3416 | 3417 | 3418 | |
| **Sollwert** | **3441** | **3442** | **3443** | **3444** | −50.0…999.0 °C |
| **Schaltdifferenz** | **3431** | **3432** | **3433** | **3434** | 2.0…20.0 K |
| Ausgang bei Error | 3451 | **3452** | 3453 | 3454 | 0 AUS · 1 EIN |
| Freier DP E1 / E2 | 3948 / 3949 | 3951 / 3952 | 3954 / 3955 | 3957 / 3958 | |
| BACnet-Adr E1 / E2 | 4048 / 4049 | 4051 / 4052 | 4054 / 4055 | 4057 / 4058 | |
| BACnet-Adr Eingang A | 4050 | 4053 | 4056 | 4059 | |

> ⚠️ **Doppelfalle:** `3452` ist **nicht** die Schaltdifferenz von KM2, sondern
> „KM2-Ausgang bei Error". Die Schaltdifferenzen sind **3431…3434**.
> Siehe [review-ist-implementierung.md](review-ist-implementierung.md#r2).

### 6.5.5 / 6.5.6 Analog- und Spezialmodule

Vier Analogmodule und vier Spezialmodule; im Rahmen dieser Auswertung nicht im Detail
erfasst, da für den Gateway nicht relevant.

---

## 7 Anlage testen

| Parameter | DP-Bereich |
|---|---|
| Ausgänge inaktiv | 2049 |
| Teststatus | 2001 |
| Ausgänge inaktiv (§6.1.4) | 2049 |
| E1…E10 aktueller Wert | 2613…2622 |
| E21…E28 aktueller Wert | 2649…2656 |
| E1…E10 Test | 2007…2016 |
| E21…E28 Test | 2696…2703 |
| D1…D5 aktueller Wert | 2600…2604 |
| D21…D28 aktueller Wert | 2636…2640, 2989…2991 |
| D1…D5 Test | 2002…2006 |
| D21…D28 Test | 2691…2695, 2986…2988 |
| Y1, Y2 aktueller Wert | 2789, 2790 |
| Y1, Y2 Test | 2027, 2028 |
| R1…R10 aktueller Wert | 2779…2788 |
| R1…R10 Test | 2017…2026 |
| R21…R27 aktueller Wert | 2791…2797 |
| R21…R27 Test | 2704…2710 |
| Zone # Modultyp | 692 (0 ohne · 1 RZM510A · 2 ECM310A) |
| Zone # Softwareversion | 688 |
| Zone # E131/E132/E133 | 672 / 671 / 673 |
| Zone # D131 / D132 | 312 / 311 |
| Zone # R131/R132/R133 | 357 / 358 / 356 |
| Remote-IO Softwareversion | 3816 |
| Remote-IO E141…E145 | 3774…3775…3776, 3814, 3815 |
| Remote-IO Y141 / Y142 | 3777 / 3778 |
| Remote-IO R141…R144 | 3779…3782 |
| Fernbedienung Typ | 689 (0 ohne · 3 RFB520A · 8 RFB510A · 21 RFB511A · 9 RFB512A · 14 RFB516A) |
| Fernbedienung Softwareversion | 691 |
| Fernbedienung Raumtemperatur | 690 |
| Fernbedienung Raumsollwertkorrektur | 217 |
| Fernbedienung Betriebsart | 216 (0 · 6 · 8 · 9 · 10 · 11) |
| Fernbedienung B'art Heiz-Kühlen | 3070 (0 · 1 · 2 · 3 · 5) |
| Raumfühler Softwareversion / Raumtemperatur | 669 / 668 |
| Neurobat Softwareversion / Fehler | 3679 / 3686 |
| Aussenfühler Softwareversion / Aussentemperatur | 3059 / 3358 |
| Funkuhr Softwareversion / Zeit | 3068 / 1944 |

> ⚠️ **R-Ausgänge haben zwei Nummernkreise.** Der Item-Katalog führt
> `States/Outputs/R4ZonePump` mit **2627** – dieser Wert liegt **nicht** im dokumentierten
> Bereich. Korrekt wäre 2782 (R4). Siehe
> [review-ist-implementierung.md](review-ist-implementierung.md#r1).

---

## 8 Suchen / 9 Datensicherung

| DP | Parameter | S. |
|---:|---|---:|
| 2039 | Suchen (Datenpunkt suchen) | 113 |
| 2040 | Installation speichern | 114 |
| 3059 | Installation wiederherstellen (**im Handbuch doppelt vergeben** – dieselbe Nummer steht auch für „Softwareversion Aussenfühler") | 114 |

---

## 10 Konfigurationsmenü (OEM)

### 10.1 Systemparameter

| DP | Parameter | Bereich | S. |
|---:|---|---|---:|
| 3119 | Sprache | 0 Deutsch · 1 English · 2 Français · 3 Italiano · 4 Español · 5 Polski | 115 |
| 3129 | Zugriffsrecht | 0 Benutzer · 17 Hauswart · 34 Service · 68 OEM · 136 Hersteller | 115 |
| 3139 | Benutzerzugr'zone | 0 alle · 1…7 | 115 |
| 3149 | Raumfühler Bedieneinheit | 0 AUS · 1…7 | 115 |
| 1951 | Grundsprache | 0…5 | 115 |
| **2760** | **Baudrate BACnet** | **1 9600 · 2 19200 · 4 38400 · 8 76800 Baud** | 115 |
| **2761** | **Adresse BACnet** | **0…30** | 115 |
| 2757 | Prot Serv RZB571 | 0 Modbus · 1 ECR-com | 115 |
| 137 | Baudrate Service | 1 · 2 · 4 | 115 |
| 138 | Adresse Service | 0…200 | 115 |
| 2030…2034 | Passwörter (Service, OEM, Hersteller, Edit-Ebene, Edit-Info) | 0…9999 | 115 |

> **DP 2760 und 2761 sind für den Gateway unmittelbar relevant:** darüber ist die
> Busgeschwindigkeit und die MS/TP-Stationsadresse des Reglers einstellbar. Beide sind
> lesbar – damit kann der Gateway seine eigene Konfiguration validieren. Siehe
> [verbesserungen-und-neue-topics.md](verbesserungen-und-neue-topics.md#45-bus-selbstdiagnose).

### 10.2 Tools

| DP | Parameter | Werte | S. |
|---:|---|---|---:|
| 3159 | Alle Daten einbl'den | 0 AUS · 1 EIN · 2 EIN+Auswahl | 116 |
| 3169 | Edit Eigensch | 0 keine · 1 Lesen/Schreiben · 2 nur Lesen · 3 Info | 116 |
| 3219 | Web-Server-Session | | 116 |

### 10.3 Verwaltung Dateien

| DP | Parameter | S. |
|---:|---|---:|
| 2835 | Export Systemparameter | 116 |
| 2836 | Import Systemparameter | 116 |
| 2035 | Export Applikation | 116 |
| 2037 | Löschen Applikation | 116 |
| 2758 | Basis-Appl ausblenden | 116 |
| 3077 | Datei-Sys reg'einh löschen | 116 |
