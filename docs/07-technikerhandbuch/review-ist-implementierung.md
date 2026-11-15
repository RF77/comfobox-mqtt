# Review: Implementierung gegen das Technikerhandbuch

Abgleich des Item-Katalogs (`src/ComfoBox.Domain/Items/Catalog.cs`, 127 Items) und des
Schreibpfads (`src/ComfoBox.Gateway/Bacnet/BacnetDevice.cs`) gegen das
ELESTA-Technikerhandbuch ECR450 V4.0.

**Methodik:** jeder `BacnetObjectId` wurde gegen die DP-Nummern im Handbuch geprüft
(Werkzeug: Volltextsuche über die extrahierte PDF, siehe [README.md](README.md#2-warum-der-volltext-nicht-im-repo-liegt)).
Bereiche und Enum-Werte wurden zeichenweise verglichen.

**Ergebnis in einem Satz:** 118 von 127 Items sind korrekt. Neun zeigen auf einen falschen
oder nicht belegten Datenpunkt, drei haben einen zu engen Wertebereich – davon **zwei
funktional blockierend**.

> **Bearbeitungsstand:** Die Befunde sind am 2026-11-15 **entschieden**. Die Umsetzung
> läuft in [Phase 9](../06-aufgaben/phase-9-datenpunkt-korrekturen.md). Dieses Dokument
> bleibt als **Nachweis** stehen – es beschreibt den Zustand vor der Korrektur.

## Entscheidungen (2026-11-15)

| # | Entscheidung | Aufgabe |
|---|---|---|
| R1 | **korrigieren** → DP 3432 | [9.2](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#92--r1-comfofondheatinghysteresis-auf-dp-3432) |
| R2 | **korrigieren** → `0/3` | [9.3](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#93--r2-entladeschutz-dp-157-auf-03) |
| R3 | **korrigieren** → DP 846; **20 min Untergrenze bleibt** (WP-spezifisch) | [9.6](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#96--r3-restartdelay-auf-dp-846) |
| R4 | **kein Befund** – 58 °C ist die Grenze der Wärmepumpe, nicht des Reglers | — |
| R5 | **weitergehend**: alle `Tests/*` werden **read-only** (nicht nur Experten-Modus) | [9.9](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#99--r5-tests-vollständig-read-only) |
| R6 | **korrigieren** auf die DPs aus §7 | [9.7](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#97--r6-statesoutputs-auf-die-dokumentierten-dps) |
| R7 | die **Namen** sind falsch → umbenennen, DPs `2619/2620` bleiben | [9.8](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#98--r7-topics-e6-und-e7-sind-falsch-benannt) |
| R8 | **`Max = 11`**; DP 3097 **nicht** nach V1 (V2-Kandidat, read-only) | [9.4](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#94--r8-primarypumpstoppedmode-dp-3096-auf-0111) |
| R9 | **Werteprüfung** `{0, 1, 3}` statt Bereich | [9.5](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#95--r9-coolingzonecooling-dp-470-nur-013) |
| R10 | **`Max = 58 °C`** (Wärmepumpen-Grenze) | [9.10](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#910--r10-ww-sollwerte-mit-obergrenze-58-c) |
| R11 | **nur Hinweis**, keine Änderung | [9.12](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#912--r11-nur-ein-hinweis-keine-änderung) |
| R12 | **Doku + UI-Hinweis**, keine Funktion | [9.13](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#913--r12-gerätefehler-sind-nicht-auslesbar-doku--ui-hinweis) |

---

## Übersicht

| # | Befund | Schwere | Auswirkung |
|---|---|---|---|
| [R1](#r1) | `Comfofond/HeatingHysteresis` → DP 3452 („KM2-Ausgang bei Error") statt **3432** | **hoch** | liest/schreibt das falsche Objekt |
| [R2](#r2) | `Entladeschutz` Bereich 0…1, Gerät kennt **0 und 3** | **hoch** | Einschalten ist **unmöglich** |
| [R3](#r3) | `RestartDelay` → DP 864 statt **846** | **hoch** | liest den Kühl-Sollwert-Offset |
| [R4](#r4) | `FlowMaxTemperature` / `SollwertMaximalWP` Max **58** statt 150/65 | **kein Befund** | 58 °C ist die Grenze der Wärmepumpe (entschieden) |
| [R5](#r5) | `Tests/OutputsInactive` ohne Experten-Modus schreibbar | **hoch** | eine Automation kann die Anlage stilllegen → wird komplett **read-only** |
| [R6](#r6) | `States/Outputs/*` mit DP 2624…2635 – im Handbuch **nicht vorhanden** | mittel | liest ins Leere |
| [R7](#r7) | `States/Inputs/E6…`/`E7…` sind um eins verschoben (2619 = E7, 2620 = E8) | mittel | falsche Beschriftung |
| [R8](#r8) | `PrimaryPump/StoppedMode` Max **1**, Gerät kennt **11** (PWM) | mittel | PWM nicht einstellbar |
| [R9](#r9) | `CoolingZone/Cooling` Katalog 0…3, Gerät kennt 0/1/**3** | niedrig | Wert 2 wird ans Gerät geschickt |
| [R10](#r10) | `Warmwater/SetPoint*` ohne Obergrenze (Gerät: 5…DP 190) | niedrig | Gerät lehnt ab statt Gateway |
| [R11](#r11) | `Config/PrimaryPump/*` sind die Parameter von **Y1**, nicht der Pumpe | niedrig | Namensirreführung |
| [R12](#r12) | Fehlerspeicher nicht abgebildet | Lücke | keine Gerätefehler in HA |

---

## R1

**`Config/Comfofond/HeatingHysteresis` zeigt auf DP 3452 – das ist ein Fehler-Flag.**

| | |
|---|---|
| Katalog | `BacnetObjectId = 3452`, Bereich 2…20 K |
| Handbuch | `3452` = **„KM2-Ausgang bei Error"**, Werte `0 AUS / 1 EIN` (§6.5.4, Seite 94) |
| Korrekt | `KM2-Schaltdifferenz` = **3432** (KM1 = 3431, KM3 = 3433, KM4 = 3434) |

**Auswirkung**

- Lesen liefert `0` oder `1` → `BacnetDevice.WarnIfOutOfCatalogRange` meldet einmalig
  „Wert außerhalb [2 … 20]".
- Schreiben von 2…20 K geht an ein Flag. Das Gerät lehnt entweder ab oder setzt einen
  undefinierten Zustand – in beiden Fällen ist die Comfofond-Heizungshysterese **nicht**
  einstellbar.

**Nebenbei:** Die bekannte Home-Assistant-Meldung „Value outside of the range: 0 not in
2…20" für dieses Item ist damit **nicht** „Comfofond nicht eingerichtet", sondern ein
falscher Datenpunkt. Auffällig: ein Wert `1` wäre der Beweis (eine Hysterese kann keine 1
sein) – bisher wurde nur `0` beobachtet.

**Fix:** `BacnetObjectId = 3432`.

---

## R2

**`Entladeschutz` – der gültige Wert 3 wird vom Gateway abgelehnt.**

| | |
|---|---|
| Katalog | `Min = 0`, `Max = 1` |
| Handbuch | `WW-Entladeschutz` = `0 Keine Funktion` · **`3 EIN`** (§6.4.2, Seite 78) |
| Katalog-Datei | [Catalog.cs](../../src/ComfoBox.Domain/Items/Catalog.cs) → `config.energy_comsumer.warmwater.entladeschutz` |

`BacnetDevice.WriteAsync` prüft `item.IsInRange((float)value)` und gibt bei Verletzung
`WriteOutcome.OutOfRange` zurück, **ohne** zu schreiben
([BacnetDevice.cs:203](../../src/ComfoBox.Gateway/Bacnet/BacnetDevice.cs#L203)).
Der Wert `3` – der einzige, der den Entladeschutz einschaltet – ist damit **nie** schreibbar.

**Empirisch bestätigt:** Das Gerät liefert `3` (die Home-Assistant-Warnung
„Wert außerhalb des Bereichs" für `Entladeschutz`). Die Firmware kennt also 3, der Katalog
nicht.

**Fix:** `Max = 3` **und** eine Werteprüfung auf `{0, 3}` – sonst würde `1` und `2`
akzeptiert, die das Gerät nicht kennt. Sauberer: ein Enum `EntladeschutzModes { KeineFunktion = 0, Ein = 3 }`
wie bei `KondensatFrostschutzEnum`.

---

## R3

**`RestartDelay` liest den Kühl-Sollwert-Offset statt der Wiedereinschaltverzögerung.**

| | |
|---|---|
| Katalog | `Config/EnergyComsumer/HeatPump/RestartDelay` → `BacnetObjectId = 864`, Bereich 20…60 min |
| Handbuch DP 864 | **„Kühlraumsoll'abst standby"**, Bereich **0.0…10.0 K** (§6.4.1, Seite 77) |
| Handbuch DP 846 | **„Wiedereinschaltverzög 1"**, Bereich 0…60 min (§6.3.2, Seite 60) |
| Handbuch DP 847 | „Wiedereinschaltverzög 2", 0…60 min (§6.3.3, Seite 61) |

Das Handbuch führt **zwei** Objekte mit der Nummer 864 (Kühl-Offset und – laut Altbestand –
RestartDelay). Die Auflösung ist eindeutig: die Wiedereinschaltverzögerung ist **846**.
Die Doppelbelegung ist damit **keine** Geräte-Eigenheit, sondern ein Fehler im Altbestand.

**Auswirkung**

- Der gelesene Wert (im Betrieb `10`) ist der Kühl-Offset, nicht die Verzögerung.
- `10` liegt außerhalb von 20…60 → Warnung bei jedem Start, und Home Assistant zeigt
  einen unplausiblen Wert.
- Ein Schreibvorgang von 20…60 min landet auf dem Kühl-Offset (Bereich 0…10) → das Gerät
  lehnt ab.

**Fix:** `BacnetObjectId = 846`. Der Bereich 20…60 muss gegen das Handbuch geprüft werden –
das Handbuch nennt **0…60**; die 20 als Untergrenze ist eine Herstellervorgabe
(Zehnder/ComfoBox), die im Handbuch nicht steht. **Beides beibehalten** (0…60 wäre
gerätekonform, 20…60 ist anlagenkonform) – aber dokumentieren.

> **Wichtig:** Dieser Befund ist der Grund für die bekannte Warnung „Objekt 864 wird von
> zwei Topics benutzt". Nach dem Fix gibt es nur noch **eine** Belegung, und
> `CatalogIntegrityTests` (das genau eine doppelte Objekt-Id erlaubt) kann verschärft werden.

---

## R4

> **Entscheidung 2026-11-15: kein Befund, keine Änderung.**
> Die 58 °C sind die **Grenze der Wärmepumpe** (ComfoBox/Zehnder), nicht des Reglers. Die
> 150 °C aus dem Handbuch gelten für den ECR450 als Universalregler und damit auch für
> Wärmepumpen, die diese Anlage nicht hat. Die Grenze bleibt, wie sie ist – sie ist
> anlagenspezifisch korrekt. Der Befund steht hier als **Nachweis**, warum 58 ≠ Handbuch
> ist.

**Zwei Obergrenzen sind kleiner als im Handbuch – bewusst.**

| Item | DP | Katalog | Handbuch |
|---|---:|---|---|
| `Config/EnergyComsumer/HeatPump/FlowMaxTemperature` | 444 | 0.0…**58.0** °C | `WP-Vorlauf Maximal` **0…150 °C** (§6.3.2, S. 60) |
| `Config/EnergyComsumer/Warmwater/SollwertMaximalWP` | 447 | 0.0…**58.0** °C | `WW-Sollwert Maximal WP` **0…65 °C** (§6.4.2, S. 78) |

**Auswirkung:** Werte oberhalb von 58 °C sind über MQTT/REST **nicht** einstellbar – die
Werte 59…150 (Vorlauf) bzw. 59…65 (WW) werden mit `OutOfRange` abgelehnt.

Für eine Fußbodenheizung sind 58 °C meist ausreichend; für Radiatoren, Estriche mit
Austrocknungsprogramm oder Altbauten nicht. Das Gerät erlaubt es – der Gateway verhindert
es ohne technischen Grund.

**Fix:** `Max = 150.0f` bzw. `Max = 65.0f`.

> **Warum 58?** Vermutlich eine Absicht aus dem Altbestand („nicht über 58 °C, sonst
> Kalk"). Diese Herstellergrenze gehört dann in die **Dokumentation** und in die UI, nicht
> in die Wertevalidierung des Gateways – sonst kann der Anlagenbetreiber sie nicht
> bewusst überschreiten.

---

## R5

**`Tests/OutputsInactive` ist ohne Experten-Modus schreibbar.**

| | |
|---|---|
| Topic | `ComfoBox/Tests/OutputsInactive/Set` → DP **2049** |
| Katalog | `RequiresExpertMode` **false**, `IsReadOnly` false |
| Handbuch | „Bei Inbetriebnahme … danach die Funktion **Ausgänge inaktiv** deaktivieren (2049)". Wert `1` = **alle Ausgänge inaktiv** (§6.1.4, S. 25 / §7, S. 106) |

**Auswirkung:** Jeder Token mit dem Umfang `Control` – also jede Automatisierung, jede
HA-Automation, jeder Skriptaufruf – kann die gesamte Wärmeerzeugung lahmlegen. Ein
einziger falscher Payload genügt, und die Anlage heizt nicht mehr. Das ist die
gefährlichste Fehlbedienung des ganzen Systems.

Dasselbe gilt für `Tests/Y1`, `Tests/Y2`, `Tests/R4` (direkte Ausgangssteuerung, ebenfalls
ohne Experten-Modus).

**Fix (drei Ebenen):**

1. `RequiresExpertMode = true` für alle `Tests/*`-Items.
2. In der UI zusätzliche Rückfrage („Dies schaltet die Anlage ab. Fortfahren?").
3. Home Assistant: `entity_category: config`, nicht in der Standardansicht.

> **Anmerkung:** Der Altbestand hatte hier ebenfalls keine Sperre – das ist ein
> **geerbter** Mangel, kein Portierungsfehler. Er ist trotzdem zu beheben: der neue
> Gateway hat einen Experten-Modus, der Altbestand hatte nur ein Konfigurationsflag.

> **Entscheidung 2026-11-15: weitergehend als oben vorgeschlagen.** Alle vier `Tests/*`
> werden **vollständig read-only** (`IsReadOnly = true`, kein `/Set`-Topic). Begründung:
> die Topics sind für die Inbetriebnahme **am Gerät** gedacht (Handbuch §7 „Anlage
> testen"); aus der Ferne fehlt jede Rückmeldung, und ein gesetzter Testwert bleibt
> stehen. Was die vier Topics tun, steht in
> [Phase 9, Aufgabe 9.9](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#99--r5-tests-vollständig-read-only).

---

## R6

**`States/Outputs/*` verwendet Objekt-Nummern, die im Handbuch nicht existieren.**

| Item | Katalog-DP | Handbuch |
|---|---:|---|
| `States/Outputs/R1FreeCooling` | 2624 | R1 = **2779** |
| `States/Outputs/R3Verdichter` | 2626 | R3 = **2781** |
| `States/Outputs/R4ZonePump` | 2627 | R4 = **2782** |
| `States/Outputs/R5WarmWater` | 2628 | R5 = **2783** |
| `States/Outputs/R7ElectricalWarmWater` | 2630 | R7 = **2785** |
| `States/Outputs/R10AdditionalHeater` | 2633 | R10 = **2788** |
| `States/Outputs/Y1PrimaryPump` | 2634 | Y1 = **2789** |
| `States/Outputs/Y2` | 2635 | Y2 = **2790** |

Der Bereich 2624…2648 ist im Handbuch **unbelegt**. Die Ausgangszustände stehen
ausschliesslich in §7 (Seite 108/109).

**Indiz, dass etwas nicht stimmt:** `ComfoBox/Tests/R4` (DP **2782**) und
`ComfoBox/States/Outputs/R4ZonePump` (DP **2627**) beschreiben dieselbe Größe mit zwei
verschiedenen Nummern. Eine davon muss falsch sein – und 2782 steht im Handbuch.

**Zusatzproblem:** `States/Outputs/Y1PrimaryPump` und `Y2` sind **schreibbar**
(`new AnalogValue(2634, "%")` ohne `IsReadOnly` im Altbestand). Auf einen Ausgangs-Istwert
zu schreiben ist sinnlos; wenn 2634 auf dem Gerät existiert und beschreibbar ist, wäre es
eine ungewollte Ausgangssteuerung.

**Vorgehen (nicht raten):**

1. `POST /api/diagnostics/bacnet/observe` starten, während der Gateway läuft.
2. Die Objekte 2624…2635 und 2779…2797 lesen lassen.
3. Übernehmen, was antwortet. Was nicht antwortet, wird in V2 auf die dokumentierte Nummer
   umgestellt (siehe [../topics-v2.md](../topics-v2.md#6-korrekturen-am-v1-katalog)).

**Bis dahin:** die beiden Items als read-only behandeln und in der UI als „unbestätigt"
kennzeichnen.

---

## R7

**Eingangs-Topics sind um einen Kanal verschoben.**

| Topic | Katalog-DP | Handbuch |
|---|---:|---|
| `States/Inputs/E6SecondRoomTemperature` | 2619 | 2619 = **E7** |
| `States/Inputs/E7HpFlowBackTemp` | 2620 | 2620 = **E8** |

Aus der Handbuchfolge `E3 = 2615 … E10 = 2622` ergibt sich `E_n = 2612 + n`:
E6 = 2618, E7 = 2619, E8 = 2620. (Die Angabe „E1 = 2614" im Handbuch ist ein Druckfehler –
E2 hat dieselbe Nummer.)

**Zwei Lesarten:**

1. Die Objekt-Ids sind um eins zu hoch → korrekt wäre 2618/2619.
2. Die Topics sind falsch benannt; der Altbestand meint E7 und E8.

**Wichtiger als die Nummer ist die Semantik:** `E6SecondRoomTemperature` behauptet, dass
Eingang E6 ein zweiter Raumfühler ist. Das ist eine **Installationsangabe**, keine
Geräte-Eigenschaft – das Handbuch legt die Funktion eines Eingangs erst in §6.2.4 fest.
Für eine andere Anlage ist das Topic schlicht falsch.

**Empfehlung:** Die beiden Topics als „Altbestand, anlagenspezifisch" dokumentieren und in
V2 durch generische `Status/Inputs/E<n>` ersetzen
(siehe [../topics-v2.md](../topics-v2.md#58-status--ein--und-ausgänge)).
> **Entscheidung 2026-11-15: Lesart 2 – die Namen sind falsch, die Datenpunkte bleiben.**
> Der am Gerät gelesene Wert passt zur jeweiligen Semantik, also zeigt `2619` tatsächlich
> den zweiten Raumfühler und `2620` den WP-Vor-/Rücklauffühler. Damit ist nur die
> **Nummer im Namen** falsch: `2619 = E7`, `2620 = E8`.
> Folge: `E6SecondRoomTemperature` → **`E7SecondRoomTemperature`**, `E7HpFlowBackTemp` →
> **`E8HpFlowBackTemp`**. Das ändert zwei Topic-Adressen und ist die **einzige**
> Vertragsänderung dieser Runde – siehe
> [Phase 9, Aufgabe 9.8](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#98--r7-topics-e6-und-e7-sind-falsch-benannt).
---

## R8

**`PrimaryPump/StoppedMode` (DP 3096) – Wert 11 fehlt.**

| | |
|---|---|
| Katalog | `Min = 0`, `Max = 1` |
| Handbuch | `Y1 Antriebstyp` = `0 0..10 V` · `1 0..10 V stop = 1 V` · **`11 PWM`** (§6.2.16, Seite 45) |

**Auswirkung:** Ein Gerät, das auf PWM konfiguriert ist, liefert `11` → Bereichswarnung bei
jedem Start. PWM lässt sich nicht einstellen.

**Fix:** `Max = 11` – oder besser ein Enum `DriveTypes { Analog0To10V = 0, Analog0To10VStop1V = 1, Pwm = 11 }`.
Dasselbe gilt für `Y2 Antriebstyp` (DP 3097), das der Katalog nicht führt.

> **Entscheidung 2026-11-15: `Max = 11` plus Werteprüfung `{0, 1, 11}`.** Kein Enum, weil
das den Payload von Zahl auf Namen umstellen würde (Vertragsbruch).
> **DP 3097 ist heute nicht im Katalog** (geprüft in `src_old/` und `Catalog.cs`) und wird
> **nicht** nach V1 aufgenommen – ein neues Topic wäre eine Vertragserweiterung. Er steht
> als **nur lesender** Kandidat in [../topics-v2.md](../topics-v2.md).

---

## R9

**`CoolingZone/Cooling` (DP 470): der Wert 2 existiert nicht.**

| | |
|---|---|
| Katalog | `Min = 0`, `Max = 3` – akzeptiert also 1 **und** 2 |
| Handbuch | `0 Keine Funktion` · `1 Kühlen` · `3 Nur Kühlen` (§6.4.1, Seite 76) |

Der Gateway lässt `2` durch; das Gerät kennt den Wert nicht. Je nach Firmware antwortet es
mit einem BACnet-Error (wird korrekt als `BacnetDeviceErrorException` gemeldet) oder
ignoriert den Schreibvorgang (dann glaubt der Nutzer, es sei gesetzt).

**Fix:** Werteprüfung auf `{0, 1, 3}` (Enum), nicht nur Bereich.

> **Allgemein:** Bei allen Auswahl-Parametern (`0/1/2/3/11`) ist eine **Bereichsprüfung
> wertlos** – sie akzeptiert Lücken. Der Katalog braucht für diese Fälle entweder ein Enum
> oder eine Liste erlaubter Werte.

> **Entscheidung 2026-11-15: Werteprüfung als Liste, kein Enum.** Ein Enum würde den
> Payload von der Zahl auf den Namen umstellen und damit den MQTT-Vertrag brechen. Deshalb
> kommt ein neues Katalog-Feld `AllowedValues` (aus `[Values(0, 1, 3)]` im Legacy-Item),
> das `Max`/`Min` **ergänzt** statt sie zu ersetzen – siehe
> [Phase 9, Aufgabe 9.1](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#91--values-erlaubte-werte-im-katalog-mechanik).

---

## R10

**`Warmwater/SetPoint*` haben keine Obergrenze.**

| | |
|---|---|
| Katalog | `Min = 5.0`, `Max = null` |
| Handbuch | `5…Begrenzung` – die Begrenzung ist **DP 190** (`WW-Sollwert Maximal`, 5…99) |

Der Gateway akzeptiert jeden Wert ≥ 5, auch 80 °C, wenn die Begrenzung auf 60 steht. Das
Gerät lehnt dann ab – der Fehler kommt also erst einen Schritt später und ist für den
Nutzer schlechter zuzuordnen.

**Fix:** `Max = 99.0f` (statische Obergrenze) und in der UI zusätzlich den **aktuellen** Wert
von DP 190 als dynamische Grenze anzeigen. Der Katalog kann die Verriegelung nicht
abbilden, weil sie von einem anderen Item abhängt.

> **Entscheidung 2026-11-15: `Max = 58 °C`.** Nicht 99 und nicht die dynamische Grenze aus
> DP 190: 58 °C ist die **Liefergrenze der Wärmepumpe** und steht schon an
> `HeatPump/FlowMaxTemperature` (444) und `Warmwater/SollwertMaximalWP` (447). Damit sind
> alle vier Temperaturgrenzen im Katalog gleich.
> **Folge, die dokumentiert werden muss:** `SetPointLegio` (DP 73) ist damit nicht mehr auf
> 60…70 °C einstellbar. Was die Wärmepumpe nicht liefert, soll der Gateway nicht anbieten –
> wer höher muss, nutzt `WW-elektrisch` (DP 118/421) am Gerät.

---

## R11

**`Config/PrimaryPump/*` sind nicht die Pumpe, sondern der stetige Ausgang Y1.**

| Topic | DP | Handbuch |
|---|---:|---|
| `PrimaryPump/Min` | 411 | `Y1 Minimal` |
| `PrimaryPump/Max` | 412 | `Y1 Maximal` |
| `PrimaryPump/MinCooling` | 3613 | `Y1 Minimal Kühlen` |
| `PrimaryPump/MaxCooling` | 3614 | `Y1 Maximal Kühlen` |
| `PrimaryPump/MaxMode` | 3232 | `Y1 Max Mode` |
| `PrimaryPump/StoppedMode` | 3096 | `Y1 Antriebstyp` |

Die Nummern sind **richtig** – nur die Benennung ist irreführend. Y1 ist bei der ComfoBox
mit der Primärpumpe belegt (deshalb der Name), das ist aber eine Konfigurationsentscheidung
des Herstellers, keine Geräte-Eigenschaft.

**Empfehlung:** so lassen (Vertrag!), aber im Topic-Dokument einen Hinweis anbringen – und
in V2 die analogen Themen als `Status/Outputs/Y1*` führen.

> **Entscheidung 2026-11-15: nur Hinweis, keine Änderung.** Kein Code, keine Umbenennung,
> kein neues Topic. Der Hinweis steht in
> [datenpunkt-register.md](datenpunkt-register.md) (§6.2.16) und in
> [../topics-v2.md](../topics-v2.md) (§4.7).

---

## R12

**Fehlerspeicher ist nicht abgebildet – und über dieses Handbuch auch nicht abbildbar.**

Das Handbuch beschreibt in §4.9 zehn Speicherplätze mit Datum und Uhrzeit und listet
**alle** Fehler-Codes (0…965), nennt aber **keine DP-Nummer** für die Speicherplätze.
Nur die Konfiguration „jeden Fehler nur einmal / jedes Mal" hat eine (DP 3472).

**Konsequenz**

- Der Gateway kann Gerätefehler **nicht** über BACnet lesen.
- `ComfoBox/Status/Errors` enthält daher ausschliesslich **Gateway**-Fehler
  (Verbindung, Timeout, Schreiblimit) – das ist korrekt, muss aber dokumentiert sein, damit
  niemand dort Gerätefehler erwartet.
- Indirekt sichtbar sind Gerätefehler über `HeatPumpStatus` (DP 662) – die Zustände
  `11…14`, `41`, `46`, `49`, `53`, `54`, `61`, `62` sind Störungen bzw. Warnungen.

**Empfehlung:** In der UI einen Hinweis anbringen („Gerätefehler stehen im Fehlerspeicher des
Reglers; dieser ist über BACnet nicht auslesbar"). Siehe
[verbesserungen-und-neue-topics.md](verbesserungen-und-neue-topics.md#2-fehlerspeicher).

> **Entscheidung 2026-11-15: wie empfohlen umsetzen** – Hinweis im Reiter „Fehlerhistorie"
> der Diagnoseseite (`diagnostics.html`, `value="errors"`) **und** in der Benutzerdoku
> ([09-fehlersuche.md](../benutzer/09-fehlersuche.md),
> [08-monitoring.md](../benutzer/08-monitoring.md)). Keine Funktion, keine neuen Topics.
> Aufgabe: [9.13](../06-aufgaben/phase-9-datenpunkt-korrekturen.md#913--r12-gerätefehler-sind-nicht-auslesbar-doku--ui-hinweis).

---

## Was korrekt ist

Damit die Liste nicht den falschen Eindruck erweckt – **der überwiegende Teil ist sauber
portiert**:

| Prüfung | Ergebnis |
|---|---|
| Objekt-Ids (127 Items) | **118 exakt** wie im Handbuch |
| `OperationModes` (1…7) | exakt, inklusive der Lücke (kein 0) |
| `HeatingCoolingModes` (1/2/3/5) | exakt |
| `KondensatFrostschutzEnum` (0/31/37/39/63/80) | exakt |
| `HeatPumpStatusEnums` (0…62) | **vollständig**, alle 54 im Handbuch genannten Werte vorhanden, keine Erfindung |
| Prioritäten (`Primary`/`Secondary`/`Once`) | im Handbuch plausibel – Zustände primär, Konfiguration sekundär |
| Kultur-Invarianz (`AnalogValue`) | korrekt; Dezimalkomma wird **abgelehnt**, nicht als Tausenderpunkt gelesen |
| Schreibpfad | validiert **vor** dem Zähler, **ohne** Clamping – ein ungültiger Wert wird abgelehnt, nie stillschweigend verbogen |
| Enum-Payload ohne 0-Fallback | Befund A6 ist behoben |
| Einheiten (`°C`, `K`, `kWh`, `%`, `h`, `min`, `m³/h`) | stimmen mit dem Handbuch überein |

---

## Nicht prüfbar

| Punkt | Grund |
|---|---|
| Objekte 2624…2635, 2619/2620 | kein Gerät angeschlossen; Handbuch schweigt |
| MS/TP-Stationsadresse | Handbuch nennt DP 2761 (0…30) – der Gateway fragt sie nicht ab |
| Zonen 2…7 | Handbuch nennt nur den ersten Kreis; weitere nur am Gerät über „Info" |
| Fehlerspeicher | siehe R12 |
| Verhalten bei Geräte-Error-PDU auf einen Schreibvorgang mit gültigem Bereich | erst am Gerät messbar |

---

## Umsetzung

Die Reihenfolge und die genauen Änderungen stehen in
[Phase 9 – Datenpunkt-Korrekturen](../06-aufgaben/phase-9-datenpunkt-korrekturen.md).
Kurzfassung:

| Schritt | Inhalt |
|---|---|
| 1 | **R2**, **R3**, **R1**, **R8**, **R9**, **R10** – Metadaten (Objekt-Ids, Grenzen, Wertelisten) |
| 2 | **R5**, **R6** – Schreibbarkeit und Ausgangs-Objekt-Ids (+ `CatalogIntegrityTests`) |
| 3 | **R7** – die zwei Topic-Namen (Vertragsänderung, CHANGELOG + Benutzerdoku) |
| 4 | **R12** – UI-Hinweis + Benutzerdoku |
| — | **R4**, **R11** – keine Änderung (entschieden) |

> Nur **R7** ändert eine Topic-Adresse. Alle übrigen Punkte sind Metadaten und damit mit der
> MQTT-Kompatibilitätszusage vereinbar
> ([../04-migration/mqtt-kompatibilitaet.md](../04-migration/mqtt-kompatibilitaet.md)).
> Der Contract-Test vergleicht `Min`/`Max`/`IsReadOnly` **feldweise** – die Fixture
> `docs/topics.json` wird deshalb über den Generator nachgezogen, nie von Hand.
