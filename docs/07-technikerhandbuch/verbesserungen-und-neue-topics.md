# Verbesserungsvorschläge und neue Topics

Was der Gateway **zusätzlich** können sollte – mit Begründung, Aufwand und Reihenfolge.
Die verbindlichen Adressen stehen in [../topics-v2.md](../topics-v2.md); dieses Dokument
erklärt das **Warum** und ergänzt Vorschläge, die (noch) nicht im Vertrag stehen.

**Leitgedanke:** Ein Gateway, das nur Sollwerte schreibt und Istwerte anzeigt, ist eine
Fernbedienung. Ein gutes Gateway beantwortet drei Fragen:

1. **Heizt es?** (Ist/Soll, Vorlauf, Ventilstellung)
2. **Warum heizt es nicht?** (Übersteuerung, Sperre, Störung, Grenze)
3. **Wird es schlechter?** (COP, Überhitzung, Taktverhalten, Wartung)

Heute beantwortet der Gateway nur Frage 1.

---

## 1 Die fünf wichtigsten Ergänzungen

| Platz | Topic | DP | Warum |
|---:|---:|---|---|
| 1 | `Status/Zone/Override` | 676 | beantwortet „warum heizt es nicht" in **einem** Wert |
| 2 | `Status/WarmWater/State` + `Override` | 2599 / 2598 | dasselbe für Warmwasser |
| 3 | `Status/HeatPump/HotGasTemperature` + `SuctionGasTemperature` | 3228 / 641 | Kältekreis-Frühwarnung, bevor die Störung kommt |
| 4 | `Status/Energy/CopTotal` + drei kWh-Zähler | 3357 / 3343·3347·3349 | Verbrauch und Effizienz in Home Assistant |
| 5 | `Status/HeatPump/ReturnTemperature` | 3468 | ohne Rücklauf ist die Spreizung unbekannt – und die ist der beste Effizienzindikator |

Alle fünf sind **rein lesend**, kosten keinen Schreibvorgang und keine EEPROM-Zyklen.

> **Voraussetzung für Platz 4:** Die Energie- und COP-Topics liefern nur dann Werte, wenn
> am Gerät die **Wärmemengenmessung** (DP 3340) aktiviert ist. Ohne sie rechnet der Regler
> keine Wärme – die kWh-Zähler bleiben 0 und der COP ist nicht definiert. Der Gateway muss
> das erkennen und die Werte als **nicht verfügbar** markieren, nicht als 0. Details:
> [§6.1](#61-dp-3340--warum-ohne-wärmemengenmessung-keine-kwh-und-kein-cop-entstehen).

---

## 2 Fehlerspeicher

**Das Handbuch nennt keine DP-Nummer für die zehn Fehlerspeicher-Plätze.** Ein
Geräte-Fehler-Topic ist damit über den dokumentierten Weg **nicht** möglich.

Drei Wege, in absteigender Empfehlung:

| Weg | Aufwand | Bewertung |
|---|---|---|
| **A – Status-Enum auswerten** | gering | `HeatPumpStatus` (DP 662) enthält 10 Stör-/Warnzustände (11–14, 41, 46, 49, 53, 54, 61, 62). Daraus ein abgeleitetes `Status/HeatPump/IsFault` und `IsWarning` bilden. **Empfohlen.** |
| B – freie Register-/Analogmodul-Konfiguration | hoch | Der Betreiber müsste am Gerät ein Analogmodul auf den Fehlerspeicher legen. Nicht automatisch machbar, gehört in die Doku als „Profi-Weg". |
| C – Service-Schnittstelle (DP 2030…) | – | **Nein.** Das ist ein anderer Bus (RZB571/ECR-com) und kein BACnet. Ausserhalb des Scopes. |

**Zusätzlich empfohlen:** In der UI und in der Benutzerdoku klarstellen, dass
`ComfoBox/Status/Errors` **Gateway**-Fehler enthält, nicht Gerätefehler. Heute ist das
missverständlich.

---

## 3 Abgeleitete Werte (der Gateway rechnet, nicht das Gerät)

Diese Topics existieren im Handbuch nicht. Sie werden aus mehreren Datenpunkten berechnet –
das ist erlaubt und wertvoll, weil die Formel einfach und stabil ist. Sie gehören
**nicht** in den Katalog (kein BACnet-Objekt), sondern in eine eigene Abteilung
`Status/Derived/`.

| Topic | Formel | Einheit | Nutzen |
|---|---|---|---|
| `ComfoBox/Status/Derived/Superheat` | DP 641 − DP 660 (Sauggas − Verdampfer) | K | Überhitzung. Dauerhaft < 2 K ⇒ Expansionsventil zu weit offen oder Fühler defekt; > 15 K ⇒ Kältemittelmangel |
| `ComfoBox/Status/Derived/HeatingCircuitDelta` | DP 600 − DP 3468 (Vorlauf − Rücklauf) | K | Spreizung. < 3 K ⇒ Volumenstrom zu hoch (Pumpe/Effizienz), > 15 K ⇒ zu niedrig |
| `ComfoBox/Status/Derived/BufferDelta` | DP 24 − DP 25 (Puffer oben − unten) | K | Schichtung. Bei Ladung muss sie steigen, sonst ist ein Fühler oder die Pumpe defekt |
| `ComfoBox/Status/Derived/RuntimeRatio` | (DP 30 + DP 31) / DP 3060 | % | Taktverhalten. Sehr hoch ⇒ Überdimensionierung oder zu kleine Schaltdifferenz |
| `ComfoBox/Status/Derived/ServiceDue` | DP 3366 ≥ DP 3365 | 1/0 | Wartungsanzeige – dieselbe Bedingung, die am Gerät „Service notwendig" auslöst |
| `ComfoBox/Status/Derived/CopInstant` | Δ(ThermalEnergyTotal) / Δ(ElectricalEnergyTotal) über 1 h | | Momentan-COP. Aussagekräftiger als der Geräte-Durchschnitt (DP 3357) |
| `ComfoBox/Status/Derived/CoolingActive` | DP 662 ∈ {9, 30, 31} oder DP 3098 aktiv | 1/0 | einfacher Binärsensor für Home Assistant |

**Regeln für abgeleitete Topics:**

- Nur rechnen, wenn **beide** Eingangswerte im gleichen Zyklus gelesen wurden – sonst
  `unknown` publizieren, keine 0.
- Division nie ohne Nenner-Prüfung (DP 3060 kann 0 sein).
- Ein abgeleiteter Wert darf **nie** schreibbar sein.
- Jede Formel im Topic-Dokument und in der UI als Tooltip nennen – sonst ist der Wert
  nicht interpretierbar.

---

## 4 Vorschläge nach Bereich

### 4.1 Diagnose Zone und Warmwasser (Aufwand: klein, Nutzen: sehr hoch)

Siehe [../topics-v2.md §5.1/§5.2](../topics-v2.md#51-status--zone-warum-heizt-es-nicht).

**Zusätzlich denkbar:** `Status/Zone/FrostProtectionActive` – abgeleitet aus
`OperationMode` ∈ {Standby, Nur Warmwasser} **und** `OutdoorTemperatureBuilding` < DP 187.
Damit kann Home Assistant warnen, bevor ein Rohr einfriert.

### 4.2 Kältekreis (Aufwand: klein, Nutzen: hoch)

Siehe [../topics-v2.md §5.3](../topics-v2.md#53-status--kältekreis--wärmepumpe).

**Wichtig:** Diese Werte ändern sich schnell. Poll-Priorität 2 (5 min) genügt für die
Anzeige, aber nicht für die Abtau-Erkennung. Wer die Abtauung mitverfolgen will, braucht
Priorität 1 – das ist ein **bewusster** Kompromiss, kein Versehen.

### 4.3 Energie und Effizienz (Aufwand: klein, Nutzen: hoch)

Siehe [../topics-v2.md §5.5](../topics-v2.md#55-status--energie-und-cop).

**Home-Assistant-Einordnung:**

| Topic | `device_class` | `state_class` |
|---|---|---|
| `Energy/ThermalEnergy*`, `ElectricalEnergy*` | `energy` | `total_increasing` |
| `Energy/HeatingPower`, `WarmWaterPower` | `power` | `measurement` |
| `Energy/Cop*` | – | `measurement` |

Damit funktioniert das HA-Energie-Dashboard ohne YAML.

### 4.4 Zähler und Wartung (Aufwand: klein, Nutzen: mittel)

Siehe [../topics-v2.md §5.6](../topics-v2.md#56-status--zähler-und-wartung).

Diese Werte ändern sich **einmal pro Stunde**. Sie sollten mit
`entity_registry_enabled_default: false` ausgeliefert werden – wer sie will, schaltet sie ein.

### 4.5 Bus-Selbstdiagnose

*Aufwand: klein · Nutzen: mittel*

**Neu und bisher nirgends vorgeschlagen:** Der Regler kennt seine eigene
Buskonfiguration.

| DP | Parameter | Nutzen |
|---:|---|---|
| **2760** | Baudrate BACnet (1/2/4/8 = 9600/19200/38400/76800) | Der Gateway kann prüfen, ob die eingestellte Baudrate zur tatsächlichen passt |
| **2761** | Adresse BACnet (0…30) | Der Gateway kann prüfen, ob er das richtige Gerät anspricht |

**Vorschlag:** `Status/Bus/DeviceBaudrate` und `Status/Bus/DeviceAddress`, gelesen **einmal**
beim Start. Bei Abweichung zur Konfiguration → Warnung in Log, UI und
`ComfoBox/Status/Errors`.

Das würde einen ganzen Supportfall beseitigen: „Der Gateway findet die ComfoBox nicht" ist
nachweislich meist eine falsche Baudrate (Werks-Default 76800 statt 38400).

> **Einschränkung:** Diese beiden Objekte sind nur lesbar, wenn die Verbindung schon steht.
> Bei falscher Baudrate kommt gar keine Antwort. Der Nutzen ist also „Konfiguration
> bestätigen", nicht „Fehler finden".

### 4.6 Schaltuhren und Ferienprogramme (Aufwand: sehr hoch, Nutzen: fraglich)

**Nicht empfohlen.** Das Handbuch nennt für die Schaltuhr-Blöcke **keine DP-Nummern**. Die
Zeiten sind über BACnet nicht adressierbar – nur der Kanalzustand (DP 701…707, 709, 711).
Wer Schaltzeiten aus Home Assistant steuern will, soll das über `OperationMode` und
`SetPoint*` tun. Ein halbes Schaltuhr-Modell im Gateway wäre eine Falle.

### 4.7 Mehrere Zonen (Aufwand: hoch, Nutzen: fraglich)

Ebenfalls **nicht empfohlen**, solange nur eine Zone genutzt wird. Das Handbuch nennt die
Datenpunkte nur für den ersten Kreis. Zonen 2…7 sind am Gerät über die „Info"-Taste
sichtbar, aber nicht dokumentiert – jede Nummer wäre geraten.

Falls doch: erst am Gerät messen (`/api/diagnostics/bacnet/observe`), dann Katalog.
**Nie** ein Nummernschema extrapolieren (`+50 pro Zone` funktioniert bei den E-Parametern,
aber nicht überall).

---

## 5 Verbesserungen ohne neue Topics

### 5.1 Topic-Dokumentation im UI verlinken

Derzeit landet der Nutzer im UI bei einem Wert und sieht keine Erklärung. Das Handbuch
liefert zu **jedem** Parameter eine Beschreibung. Vorschlag: die `Description` aus dem
Katalog (heute nur im JSON) zusätzlich als Tooltip und als Deep-Link auf
[datenpunkt-register.md](datenpunkt-register.md) anzeigen.

**Aufwand:** klein. **Nutzen:** sehr hoch – das ist der Unterschied zwischen „Feld mit Zahl"
und „verstandene Einstellung".

### 5.2 Bereichswarnungen sichtbar machen

`BacnetDevice.WarnIfOutOfCatalogRange` schreibt **einmal pro Item** ins Log. Dieses Log
sieht im Betrieb niemand. Vorschlag: die Warnungen zusätzlich als
`Status/Errors`-Eintrag melden (Kategorie `catalog_range`) – dann erscheinen sie in der UI
und in Home Assistant.

Damit wären R1, R3 und R8 aus dem [Review](review-ist-implementierung.md) sofort aufgefallen.

### 5.3 „Gerät antwortet nicht" von „Wert ungültig" unterscheiden

Heute enden beide Wege in einer Fehlermeldung. Für den Nutzer ist der Unterschied
entscheidend: das eine ist ein Busproblem, das andere eine Fehlbedienung. Vorschlag: eigene
`error`-Codes (`bacnet_timeout`, `value_out_of_range`, `device_error`, `read_only`,
`write_limit`) – teilweise vorhanden, konsequent durchziehen und in der UI unterschiedlich
darstellen.

### 5.4 Wertevalidierung für Auswahl-Parameter

Aus [Review R9](review-ist-implementierung.md#r9): eine Bereichsprüfung akzeptiert Lücken
(`0…3` erlaubt `2`, das Gerät kennt nur `0/1/3`). Der Katalog braucht für jedes
Auswahl-Item entweder ein Enum oder eine Liste erlaubter Werte. Das ist eine
**Katalog-Erweiterung**, kein neues Topic – und verhindert eine ganze Klasse von
Fehlbedienungen.

### 5.5 Poll-Strategie dokumentieren

`PollPlan` liest primäre Items in jedem Zyklus und sekundäre alle *n* Zyklen. Mit 60 neuen
Topics steigt die Buslast. Vorschlag: die geplante Lastrechnung aus
[../topics-v2.md §9](../topics-v2.md#9-auswirkungen-auf-den-bus) in die Konfigurationsdoku
übernehmen und im UI auf der Diagnoseseite anzeigen („Buslast: 47 Reads/min, 2,3 min/h").

---

## 6 Was nicht geht – und warum das gut ist

Damit niemand Zeit in Sackgassen steckt:

| Wunsch | Warum nicht |
|---|---|
| **Fehlerspeicher auslesen** | keine DP-Nummer im Handbuch (§4.9) |
| **Schaltzeiten programmieren** | keine DP-Nummern; nur Kanalzustände (DP 701…711) |
| **Ferienprogramm setzen** | wie oben |
| **Manuell Abtauen auslösen** | keine DP-Nummer – nur als Digitalfunktion `26` an einem Ausgang |
| **Zonen 2…7 ansprechen** | nur erster Kreis dokumentiert |
| **Echte Ventilposition / hydraulischer Durchfluss** | das Gerät meldet nur die Ausgangsspannung in % (Y1/Y2); der Durchfluss der Wärmemengenmessung (DP 3341/3348) ist kein Hydraulik-Durchfluss |
| **Kältemitteldruck** | nur über Drucksensoren E151/E152 als Analogwert, nicht kalibriert in bar |
| **Wärmemenge ohne Wärmemengenmessung** | DP 3340 = 0 ⇒ der Regler rechnet keine Wärme; die kWh-Zähler bleiben 0. Der Gateway erkennt das und markiert die Werte als nicht verfügbar – siehe [§6.1](#61-dp-3340--warum-ohne-wärmemengenmessung-keine-kwh-und-kein-cop-entstehen) |
| **Firmware-Update** | nicht Teil von BACnet |
| **Gerät neustarten** | kein DP bekannt. Nur `Command/Restart` = **Gateway**-Neustart |

> Diese Liste gehört auch in die Benutzerdoku. Nichts erzeugt mehr Supportaufwand als eine
> Funktion, die alle erwarten und niemand liefern kann.

### 6.1 DP 3340 – warum ohne Wärmemengenmessung keine kWh und kein COP entstehen

**Der Datenpunkt.** Das Handbuch führt die Wärmemengenmessung in §6.3.14 (Seite 74):

| DP | Parameter | Werte |
|---:|---|---|
| **3340** | Wärmemengenmessung | `0` keine · `1` EIN · `2` EIN, WW separat |
| 3341 | Durchfluss | 0.00…99.00 m³/h |
| 3348 | Durchfluss WW | 0.00…99.00 m³/h |

Die eigentlichen Zähler stehen in §4.8: Wärmeleistung (3342/3346), Wärmemenge
(3343/3347/3349), elektrische Energie (3344/3354/3355) und COP (3345/3356/3357).

**Die Rechnung des Reglers.** Wärme ist keine Messgröße, sondern ein Integral:

```text
Wärmemenge = ∫ Volumenstrom(t) × ρ × c_p × ΔT(t) dt
```

Der Regler braucht dafür drei Dinge:

1. die **Freigabe** „Wärmemengenmessung“ (DP 3340),
2. einen **Durchflussfühler** (DP 3341 für Heizung, DP 3348 für Warmwasser),
3. die **Temperaturfühler** Vorlauf und Rücklauf (DP 600/3468 bzw. die WW-Fühler).

**Was bei DP 3340 = 0 passiert.** Der Regler rechnet nicht. Die Wärmeleistungen bleiben 0,
und die Wärmemengen-Zähler bleiben auf ihrem Startwert stehen – sie zählen nichts. Der
COP ist definiert als Wärme geteilt durch Strom; ohne Wärmezähler gibt es deshalb auch
keinen COP. Die **elektrischen** Zähler (3344/3354/3355) können je nach Konfiguration
weiterlaufen – sie zählen den Strom des Verdichters und des Zusatzheizers. Ohne Wärme
sind sie aber nur Stromzähler, keine Effizienzaussage.

**Warum der Gateway dann keine 0 publizieren darf.** `0 kWh` und `COP 0` sind **gültige
Werte**. In Home Assistant würde daraus eine flache Statistik bei 0 entstehen; sobald die
Messung später eingeschaltet wird, springt der `total_increasing`-Zähler und die
Energie-Historie ist dauerhaft verfälscht. Deshalb gilt: **unbekannt ist nicht null.**

**Was der Gateway umsetzt** (Aufgabe 10.6 in
[Phase 10](../06-aufgaben/phase-10-topics-v2.md#106--dp-3340-wärmemengenmessung-erkennen-und-sperren)):

| Schritt | Wirkung |
|---|---|
| DP 3340 lesen (P3, stündlich) | `Status/Energy/MeasurementMode` = `None` / `On` / `OnSeparateWarmWater` |
| Verfügbarkeit bilden | `Status/Energy/Available` = `1` nur bei `On`/`OnSeparateWarmWater` und frisch gelesenem Energie-Block; sonst `0` |
| Energie-Topics sperren | bei `Available = 0` werden die 11 kWh-/COP-/Leistungs-Topics **nicht** publiziert (HA-Entitäten werden über `availability_topic` „nicht verfügbar“) |
| Fehler melden | **einmal pro Zustandswechsel** ein `Status/Errors`-Eintrag `energy_measurement_disabled` mit Handlungsanweisung |
| Durchfluss anzeigen | `Status/Energy/FlowRate` und `FlowRateWarmWater` machen sichtbar, ob der Fühler liefert |

**Was der Betreiber tun muss.** Am Regler §6.3.14 „Wärmemengenmessung“ auf `1` oder `2`
stellen, den Durchflussfühler anschliessen und parametrieren, und prüfen, ob
`Status/Energy/FlowRate` bei laufender Pumpe einen Wert grösser 0 zeigt. Erst dann liefern
die kWh-Zähler und der COP Werte.

**Was der Gateway nicht kann.** Er kann die Wärmemenge **nicht** aus Laufzeiten oder
Leistungsaufnahme schätzen. Eine geschätzte Wärmemenge wäre eine falsche
Abrechnungsgrundlage und würde bei einer späteren echten Messung einen Sprung erzeugen.
Er schreibt DP 3340 auch **nicht automatisch**: ohne Durchflussfühler würde die Messung
zwar „an“ stehen, aber weiterhin 0 liefern – und der Fehler wäre schwerer zu finden als
heute.

---

## 7 Aufwand und Nutzen

| Vorschlag | Topics | Aufwand | Nutzen | Priorität |
|---|---:|---|---|---|
| Diagnose Zone/WW (`Override`, `State`, `SetPointType`) | 6 | klein | sehr hoch | **1** |
| Kältekreis-Fühler | 13 | klein | hoch | **2** |
| Energie + COP | 11 | klein | hoch | **3** |
| DP 3340 + Energie-Verfügbarkeit | 3 | klein | sehr hoch | **3** |
| Rücklauf + Puffer | 6 | klein | hoch | **4** |
| Zähler + Wartung | 11 | klein | mittel | 5 |
| Abgeleitete Werte | 7 | klein | hoch | **2** |
| Bus-Selbstdiagnose | 2 | klein | mittel | 6 |
| Ein-/Ausgänge generisch | 30 | mittel | niedrig | 7 |
| Solar | 5 | klein | niedrig | 8 |
| Fehlerspeicher über Analogmodul | 1 | hoch | niedrig | 9 |
| Schaltuhren | – | sehr hoch | fraglich | **nicht** |
| Zonen 2…7 | 30+ | hoch | fraglich | **nicht** |

Summe der empfohlenen Topics (Priorität 1–6): **≈ 59**.

---

## 8 Reihenfolge der Umsetzung

1. **Review-Fixes 1–5** ([review-ist-implementierung.md](review-ist-implementierung.md#umsetzung))
   – sie sind Voraussetzung dafür, dass die neuen Werte überhaupt stimmen.
2. **Diagnose-Topics** – sie beantworten die häufigste Frage und brauchen kein neues Denken.
3. **Abgeleitete Werte** – reine Rechnung, kein Bus.
4. **Kältekreis + Energie** – braucht Poll-Prioritäten und Discovery-Erweiterung.
5. **Zähler** – mit `enabled_default: false`.
6. **UI und Doku** – sonst sieht niemand die neuen Werte.

> **Nicht** alles gleichzeitig. Jede Stufe ist für sich nützlich und rückbaubar.
