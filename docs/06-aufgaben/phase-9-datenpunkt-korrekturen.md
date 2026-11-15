# Phase 9 – Datenpunkt-Korrekturen aus dem Technikerhandbuch

> **Status:** ✅ **umgesetzt 2026-11-15** · **Aufwand:** ≈ 4 PT · **Befunde:** R1…R12 aus
> [07-technikerhandbuch/review-ist-implementierung.md](../07-technikerhandbuch/review-ist-implementierung.md)
>
> Umsetzungsprotokoll mit Testzahlen und den aufgetretenen Fallstricken:
> [protokoll.md](protokoll.md) (Abschnitt *2026-11-15 (2)*).
> Die Abnahmekriterien, die **Hardware** brauchen (R6-Regression, `Entladeschutz = 3`,
> `StoppedMode = 11`), stehen im Abschnitt *Nächste Session* desselben Protokolls.
>
> Diese Phase setzt die am **2026-11-15** getroffenen Entscheidungen um. Sie ändert
> **ausschliesslich Metadaten und Objekt-Ids** – mit **einer** bewussten Ausnahme:
> die Umbenennung in Aufgabe **9.8** ändert zwei Topic-Adressen.

## 1 Ziel

Der Item-Katalog wurde 2016 aus dem Handbuch abgeleitet. Der Abgleich gegen das
Technikerhandbuch ECR450 V4.0 (2026-11-15) hat zwölf Abweichungen ergeben. Nach dieser
Phase gilt:

| Ziel | Prüfbar durch |
|---|---|
| Jede Objekt-Id zeigt auf den Datenpunkt, den das Handbuch nennt | `CatalogIntegrityTests` + Handbuch-Abgleich |
| Kein gültiger Gerätewert wird vom Gateway abgelehnt | `Entladeschutz = 3` ist schreibbar |
| Kein Ausgang kann versehentlich ferngesteuert werden | `Tests/*` und `States/Outputs/Y*` sind read-only |
| Auswahl-Parameter akzeptieren keine Werte, die das Gerät nicht kennt | `[Values(…)]` im Katalog |
| Fixture und Katalog sind wieder deckungsgleich | `--check`, `--catalog-check`, `check-links.ps1` |

## 2 Vorbedingungen

| Bedingung | Prüfung |
|---|---|
| Node.js 22 | `node --version` |
| .NET SDK 10.0.400 | `dotnet --version` (aus `src/`) |
| Sauberer Stand | `git status` ohne Änderungen |
| Grüne Basis | `dotnet test src/ComfoBox.sln -c Release` |

**Vor der ersten Änderung** die Testzahl notieren – sie ist die Vergleichsbasis:

```bash
dotnet test src/ComfoBox.sln -c Release 2>&1 | Select-String "Passed!|Fehler!|Failed!"
```

## 3 Lesereihenfolge

1. [review-ist-implementierung.md](../07-technikerhandbuch/review-ist-implementierung.md) – die Befunde (Nachweise)
2. [datenpunkt-register.md](../07-technikerhandbuch/datenpunkt-register.md) – die DP-Nummern
3. [topics-v2.md](../topics-v2.md) §6 – die Korrekturliste
4. [04-migration/mqtt-kompatibilitaet.md](../04-migration/mqtt-kompatibilitaet.md) – was der Vertrag erlaubt
5. Diese Datei – die Aufgaben

## 4 Der Weg einer Katalogänderung

```
src_old/ComfoBoxLib/Items/*.cs                 ← die Quelle (Windows-1252!)
        │  node tools/generate-topics/generate-topics.mjs
        ├── --catalog <dir>        → src/ComfoBox.Domain/Items/Catalog.cs + ItemEnums.cs
        └── --out docs/topics.json → die Test-Fixture des Contract-Tests

     docs/topics.md  ← eingefroren (V1). Wird NICHT erzeugt und NICHT geändert.
     src_old/docs/topics.md  ← Archivkopie dieses Stands
```

| Fakt | Konsequenz |
|---|---|
| Der **Generator liest `src_old/`** | Katalogdaten werden dort gepflegt. Das ist die **einzige** zulässige Änderung an `src_old/`: Katalogdaten, kein Code. |
| `docs/topics.json` enthält **keine** Objekt-Ids | Objekt-Id-Korrekturen (R1, R3, R6) ändern die Fixture **nicht** |
| Die Fixture enthält `Min`, `Max`, `IsReadOnly`, `PublishTopic` | Bereichs-, Werte- und Schreibbarkeits-Korrekturen ändern sie **doch** |
| `docs/topics.md` ist **eingefroren** (V1) und wird **nicht** angefasst | der aktuelle Stand steht in [topics-v2.md](../topics-v2.md); die Archivkopie in [`src_old/docs/topics.md`](../../src_old/docs/topics.md) |
| `--out` schreibt **ohne BOM**, die committete Fixture **hat** eines (`EF BB BF`) | der Lauf entfernt es. Unkritisch (beide Seiten parsen), aber es erzeugt eine Byte-Diff-Zeile. Wer es behalten will: nach dem Lauf wieder voranstellen |

### 4.1 Kommandos

```bash
# Katalog (C#) neu erzeugen – Ziel ist ein VERZEICHNIS, keine Datei
node tools/generate-topics/generate-topics.mjs --catalog src/ComfoBox.Domain/Items
node tools/generate-topics/generate-topics.mjs --catalog-check src/ComfoBox.Domain/Items

# Fixture neu erzeugen und prüfen
node tools/generate-topics/generate-topics.mjs --out docs/topics.json
node tools/generate-topics/generate-topics.mjs --check
```

> ⚠️ **Die README des Werkzeugs ist an dieser Stelle falsch:** sie nennt
> `--catalog src/ComfoBox.Domain/Items/Catalog.g.cs` (Datei), der Code erwartet ein
> Verzeichnis (`join(directory, name)`). Beim Umsetzen korrigieren (Teil von 9.1).

## 5 ⚠️ Die teuerste Falle: `src_old/` ist Windows-1252

Die Item-Dateien (`Zone`, `Tests`, `Warmwater`, `States`, `Config`) sind **Windows-1252**,
die Enum-Dateien und `Time.cs` dagegen UTF-8. Der Generator erkennt das pro Datei
(`readLegacyFile`), aber **nur wenn die Datei als Ganzes eindeutig ist**.

| Vorgehen | Ergebnis |
|---|---|
| Mit einem Editor ändern, der UTF-8 schreibt | Datei wird **ungültiges** UTF-8 → Generator fällt auf CP1252 zurück → die neu geschriebenen UTF-8-Bytes werden zu `Ã¤`/`Â°C` |
| Datei **komplett** nach UTF-8 konvertieren | funktioniert (gültiges UTF-8 → UTF-8 gelesen), erzeugt aber einen Diff über die ganze Datei |
| **Empfohlen:** mit CP1252 lesen und schreiben | Diff bleibt auf die geänderten Zeilen beschränkt |

```powershell
$p   = 'src_old/ComfoBoxLib/Items/Config.cs'
$enc = [Text.Encoding]::GetEncoding(1252)
$t   = [IO.File]::ReadAllText($p, $enc)
$t   = $t.Replace('new AnalogValue(3452, "K", 2, 20)', 'new AnalogValue(3432, "K", 2, 20)')
[IO.File]::WriteAllText($p, $t, $enc)
```

**Kontrolle nach jeder Datei:**

```powershell
git diff --stat -- src_old/            # genau so viele Zeilen, wie geändert wurden
Select-String -Path src_old/ComfoBoxLib/Items/Config.cs -Pattern 'Ã|Â'   # muss leer sein
```

---

## 6 Aufgaben

### 9.1 – `[Values(…)]`: erlaubte Werte im Katalog (Mechanik)

**Warum:** `Min`/`Max` sind **Bereiche**, keine Wertelisten. `Cooling` (DP 470) erlaubt
`0…3`, das Gerät kennt aber nur `0`, `1`, `3` – heute wird `2` an das Gerät geschickt.
Dasselbe Problem bei `Entladeschutz` (`0/3`) und `Y1 Antriebstyp` (`0/1/11`).

**Ziel-Dateien:**

| Datei | Änderung |
|---|---|
| `src_old/ComfoBoxLib/Attributes/ValuesAttribute.cs` | **neu**: `[AttributeUsage(AttributeTargets.Property)] public sealed class ValuesAttribute : Attribute { public ValuesAttribute(params int[] values) }` – nur Deklaration, wird nie kompiliert |
| `tools/generate-topics/generate-topics.mjs` | `VALUES_RE = /\[Values\(([^)]*)\)\]/gu`; in `parseClass` analog zu `expertsBySegment` eine `valuesBySegment`-Liste aufbauen, pro Property den **letzten** Treffer vor der Property übernehmen (gleiche `lastPropEnd`-Logik), Zahlen mit `Number(…)` parsen; in `emitCatalog` nach `Max` ausgeben: `AllowedValues = [0f, 3f],` |
| `tools/generate-topics/README.md` | Ziel von `--catalog` ist ein **Verzeichnis** |
| `src/ComfoBox.Domain/Items/ItemDefinition.cs` | `public IReadOnlyList<float>? AllowedValues { get; init; }` + `public bool IsValueAllowed(float value) => AllowedValues is null \|\| AllowedValues.Contains(value);` |

**Schreibpfad härten** – `src/ComfoBox.Gateway/Bacnet/BacnetDevice.cs`:

- in `WriteAsync` **nach** `IsInRange` (Zeile ~203): `if (!item.IsValueAllowed((float)value))` → Log mit Wert **und** erlaubter Liste, `return WriteOutcome.OutOfRange;`
- in `WarnIfOutOfCatalogRange` (Zeile ~441): die bestehende `IsInRange`-Prüfung um `IsValueAllowed` erweitern

**Tests:**

| Datei | Test |
|---|---|
| `src/ComfoBox.Domain.Tests/CatalogTests.cs` | `IsValueAllowed` für `Entladeschutz` (3 → true, 1 → false) und `PrimaryPump/StoppedMode` (11 → true, 5 → false) |
| `src/ComfoBox.Gateway.Tests/BacnetDeviceTests.cs` | Schreibversuch mit nicht erlaubtem Wert → `WriteOutcome.OutOfRange`, **kein** BACnet-Request |
| `src/ComfoBox.Contract.Tests` | **unverändert** – `AllowedValues` steht nicht in der Fixture |

**Abnahme:**

- [ ] `--catalog-check` grün, `--check` grün (die Fixture ändert sich durch 9.1 **nicht**)
- [ ] `AllowedValues` erscheint nur bei Items mit `[Values(…)]`

> **Wenn 9.1 zu gross wird:** R2 (9.3) und R8 (9.4) nur mit dem weiteren `Max` umsetzen und
> 9.1 als eigene Session nachziehen. Die Korrekturen bleiben dann aber unvollständig –
> im Protokoll vermerken.

### 9.2 – R1: `Comfofond/HeatingHysteresis` auf DP 3432

| | |
|---|---|
| Datei | `src_old/ComfoBoxLib/Items/Config.cs:221` |
| Alt | `public AnalogValue HeatingHysteresis => new AnalogValue(3452, "K", 2, 20);` |
| Neu | `public AnalogValue HeatingHysteresis => new AnalogValue(3432, "K", 2, 20);` |

Begründung: `3452` ist laut Handbuch „KM2-Ausgang bei Error" (0/1); die Schaltdifferenz
von KM2 ist `3432` (KM1 = 3431).

**Abnahme:**

- [ ] `Catalog.cs` enthält für `config.comfofond.heating_hysteresis` → `BacnetObjectId = 3432`
- [ ] Fixture **unverändert** (Objekt-Ids stehen nicht im JSON) → `--check` grün ohne `--out`
- [ ] `CatalogIntegrityTests` grün (3452 war nicht doppelt belegt)

### 9.3 – R2: `Entladeschutz` (DP 157) auf `0/3`

| | |
|---|---|
| Datei | `src_old/ComfoBoxLib/Items/Config.cs:760` |
| Alt | `public AnalogValue Entladeschutz => new AnalogValue(157, "", 0, 1);` |
| Neu | `[Values(0, 3)]` + `public AnalogValue Entladeschutz => new AnalogValue(157, "", 0, 3);` |

Der Wert `3` ist der **einzige**, der den Entladeschutz einschaltet – er war bisher nicht
schreibbar. `1` und `2` kennt das Gerät nicht → `[Values(0, 3)]` (aus 9.1).

**Abnahme:**

- [ ] Fixture: `ComfoBox/Config/EnergyComsumer/Warmwater/Entladeschutz` → `"Max": 3`
- [ ] `PUT /api/items/…entladeschutz` mit `3` → 204 · mit `1` → 400/422
- [ ] Fixture-Diff enthält für dieses Item nur `Max` (kein `topics.md`-Diff)

### 9.4 – R8: `PrimaryPump/StoppedMode` (DP 3096) auf `0/1/11`

| | |
|---|---|
| Datei | `src_old/ComfoBoxLib/Items/Config.cs:136` |
| Alt | `=> new AnalogValue(3096, "", 0, 1) {IsReadOnly = !Settings.Default.ExpertMode};` |
| Neu | `[Values(0, 1, 11)]` + `=> new AnalogValue(3096, "", 0, 11) {IsReadOnly = !Settings.Default.ExpertMode};` |

Handbuch §6.2.16 `Y1 Antriebstyp`: `0 0..10 V` · `1 0..10 V stop = 1 V` · `11 PWM`.
Der Experten-Modus bleibt, wie er ist.

**Abnahme:**

- [ ] Fixture: `PrimaryPump/StoppedMode` → `"Max": 11`
- [ ] `IsValueAllowed(11)` true · `IsValueAllowed(5)` false
- [ ] `description`-Attribut des Legacy-Items um den PWM-Hinweis ergänzen (die Beschreibung
      landet in Fixture und UI – deshalb Fixture mit `--out` neu erzeugen)

> **DP 3097 (`Y2 Antriebstyp`) ist heute *nicht* im Katalog** (geprüft in `src_old/` und
> `Catalog.cs`). Er wird **nicht** nach V1 aufgenommen – das wäre ein neues Topic. Er steht
> als Kandidat in [topics-v2.md](../topics-v2.md) und wäre dort **nur lesend**.

### 9.5 – R9: `CoolingZone/Cooling` (DP 470) nur `0/1/3`

| | |
|---|---|
| Datei | `src_old/ComfoBoxLib/Items/Config.cs:550` |
| Alt | `=> new AnalogValue(470, "", 0, 3) {IsReadOnly = !Settings.Default.ExpertMode}` |
| Neu | `[Values(0, 1, 3)]` + gleiche Zeile (Bereich `0…3` bleibt) |

Die Beschreibung nennt die drei Werte bereits – der Katalog hat sie bisher nicht geprüft.

**Abnahme:**

- [ ] `IsValueAllowed(2)` false, `IsValueAllowed(3)` true
- [ ] Fixture **unverändert** (`--check` grün ohne `--out`)

### 9.6 – R3: `RestartDelay` auf DP 846

| | |
|---|---|
| Datei | `src_old/ComfoBoxLib/Items/Config.cs:848` |
| Alt | `public AnalogValue RestartDelay => new AnalogValue(864, "min", 20, 60) {IsReadOnly = false};` |
| Neu | `public AnalogValue RestartDelay => new AnalogValue(846, "min", 20, 60) {IsReadOnly = false};` |

Handbuch §6.3.2: `Wiedereinschaltverzög 1` = DP **846**, Bereich 0…60 min.
Die **20 min als Untergrenze bleiben** – das ist eine Herstellervorgabe der ComfoBox
(Wärmepumpen-spezifisch) und im Handbuch nicht abgebildet.

**Damit ist Objekt 864 nicht mehr doppelt belegt.** `CatalogIntegrityTests` anpassen:

```csharp
// vorher: Assert.Equal([864], shared);
Assert.Empty(shared);   // 864 ist nach R3 nur noch KühlraumsollAbstStandby
```

Den Test `Objekt_864_Gehoert_Zu_Standby_Und_RestartDelay` ersetzen durch eine **Wache**,
die genau das Gegenteil prüft:

```csharp
[Fact]
public void Objekt_864_Ist_Nur_KuehlraumsollAbstand_Standby()
{
    var item = Assert.Single(Catalog.All.Where(i => i.BacnetObjectId == 864));
    Assert.Equal("KühlraumsollAbstStandby", item.Name);
}
```

**Abnahme:**

- [ ] `RestartDelay` liest nach dem Neustart einen Wert **innerhalb** 20…60
      (vorher `10` – das war der Kühl-Offset)
- [ ] keine „Wert außerhalb"-Warnung mehr für `RestartDelay`
- [ ] Fixture unverändert

### 9.7 – R6: `States/Outputs/*` auf die dokumentierten DPs

Handbuch §7.5/§7.6 (Seite 108/109) ist die **einzige** Fundstelle für Ausgangszustände.
Der Bereich `2624…2648` ist dort **unbelegt**.

| Datei/Zeile | Item | Alt | Neu |
|---|---|---|---:|
| `States.cs:221` | `R1FreeCooling` | 2624 | **2779** |
| `States.cs:228` | `R3Verdichter` | 2626 | **2781** |
| `States.cs:232` | `R4ZonePump` | 2627 | **2782** |
| `States.cs:236` | `R5WarmWater` | 2628 | **2783** |
| `States.cs:244` | `R7ElectricalWarmWater` | 2630 | **2785** |
| `States.cs:255` | `R10AdditionalHeater` | 2633 | **2788** |
| `States.cs:213` | `Y1PrimaryPump` | 2634 | **2789** + `IsReadOnly = true` |
| `States.cs:217` | `Y2` | 2635 | **2790** + `IsReadOnly = true` |

> **Warum `Y1PrimaryPump`/`Y2` read-only:** sie waren schreibbar. Nach der Korrektur zeigen
> sie auf den **Ausgangs-Istwert** – darauf zu schreiben ist eine ungewollte
> Ausgangssteuerung. (Die Read-only-Umstellung ist Teil von R5, siehe 9.9.)

**Folge: drei bewusste Doppelbelegungen** – `Tests/R4` ↔ `States/Outputs/R4ZonePump` (2782),
`Tests/Y1` ↔ `States/Outputs/Y1PrimaryPump` (2789), `Tests/Y2` ↔ `States/Outputs/Y2` (2790).
Das ist **korrekt**: es ist dasselbe Geräte-Objekt, einmal als Test-, einmal als
Zustands-Topic. `CatalogIntegrityTests` entsprechend umbauen:

```csharp
private static readonly int[] SharedObjects = [2782, 2789, 2790];   // Test- und Zustands-Topic
```

…mit einem Test, der die Liste **exakt** prüft, und einem zweiten, der belegt, dass jedes
dieser Objekte genau aus einem `Tests/*`- und einem `States/Outputs/*`-Item besteht.
Die Meldung von `BacnetPollingService.LogSharedBacnetObjects` listet danach drei Objekte –
das ist erwartet (Meldung im Code entsprechend ergänzen).

**Abnahme:**

- [ ] `dotnet test` grün inkl. neuer Wachen
- [ ] Fixture: `States/Outputs/Y1PrimaryPump` und `…/Y2` → `"IsReadOnly": true`, `PublishTopic: null`
- [ ] die drei Topics liefern plausible Werte (nur am Gerät prüfbar → im Protokoll vermerken)

### 9.8 – R7: Topics E6 und E7 sind falsch benannt

**Entscheidung:** die Objekt-Ids `2619`/`2620` bleiben (der aktuelle Wert ist plausibel),
die **Namen** folgen der Handbuchformel `E_n = 2612 + n`:
`2619 = E7`, `2620 = E8`.

| Datei/Zeile | Alt | Neu |
|---|---|---|
| `States.cs:197` | `public AnalogValue E6SecondRoomTemperature => new AnalogValue(2619, "°C") { IsReadOnly = true };` | Name → **`E7SecondRoomTemperature`** |
| `States.cs:202` | `public AnalogValue E7HpFlowBackTemp => new AnalogValue(2620, "°C") { IsReadOnly = true };` | Name → **`E8HpFlowBackTemp`** |

> ⚠️ **Das ändert zwei Topic-Adressen.** Es ist die **einzige** Vertragsänderung dieser
> Phase und deshalb begründungspflichtig.

**Mit umzusetzen:**

| Datei | Änderung |
|---|---|
| `docs/topics.json` | über `--out` (nie von Hand) |
| `docs/topics.md` | **nicht anfassen** – eingefroren (Aufgabe 9.14) |
| `docs/04-migration/mqtt-kompatibilitaet.md` | Ausnahme ergänzen: „zwei Topic-Namen korrigiert (R7), Objekt-Ids unverändert" mit Datum und Begründung |
| `CHANGELOG.md` | unter `Changed` mit **`⚠️ breaking`** und Migrationshinweis |
| `docs/07-technikerhandbuch/review-ist-implementierung.md` | R7 auf „behoben 2026-MM-TT" |
| Benutzerdoku | Hinweis in [09-fehlersuche.md](../benutzer/09-fehlersuche.md) oder [04-mqtt.md](../benutzer/04-mqtt.md): „zwei Topic-Namen heissen neu …" |

**Folge in Home Assistant:** die betroffenen Entitäten werden **neu** angelegt
(`unique_id` folgt dem Namen). Die alten bleiben als nicht verfügbare Leichen stehen und
müssen in HA einmal gelöscht werden. Das gehört in den CHANGELOG-Eintrag.

**Abnahme:**

- [ ] `--check` grün nach `--out`
- [ ] `check-links.ps1` grün
- [ ] CHANGELOG enthält den Breaking-Hinweis **und** die HA-Migration

### 9.9 – R5: `Tests/*` vollständig read-only

**Entscheidung (weitergehend als im Review vorgeschlagen): alle vier Items werden
read-only** – nicht nur Experten-Modus-pflichtig.

| Datei/Zeile | Item | Änderung |
|---|---|---|
| `Tests.cs:26` | `OutputsInactive` (DP 2049) | `{ IsReadOnly = true }` |
| `Tests.cs:33` | `R4` (DP 2782) | `{ IsReadOnly = true }` |
| `Tests.cs:40` | `Y1` (DP 2789) | `{ IsReadOnly = true }` |
| `Tests.cs:47` | `Y2` (DP 2790) | `{ IsReadOnly = true }` |

**Was diese vier Topics tun** (Auskunft für die Doku, siehe 9.13):

| Topic | DP | Handbuch | Wirkung |
|---|---:|---|---|
| `Tests/OutputsInactive` | 2049 | §6.1.4 (S. 25), §7 (S. 106) | `1` schaltet **alle Ausgänge inaktiv** – die Anlage ist damit stillgelegt. Muss im Normalbetrieb `0` sein; das Gerät führt den Zustand als Fehler **99 „Ausgänge inaktiv konfig"** |
| `Tests/R4` | 2782 | §7.6 (S. 108) | schaltet **Relais R4** von Hand (`0` AUS / `1` EIN). Die Regelung ist dabei ausser Kraft – reine Inbetriebnahme-Hilfe |
| `Tests/Y1` | 2789 | §7.5 (S. 108) | setzt den **analogen Ausgang Y1** von Hand in Prozent (`0…100` = 0…10 V) |
| `Tests/Y2` | 2790 | §7.5 (S. 108) | dasselbe für Y2 |

Das Handbuch nennt das Kapitel „**Anlage testen**" und sagt: „Die Ausgänge können gesetzt
und die korrekte Funktion kontrolliert werden." Der Zweck ist damit eindeutig **lokal am
Gerät**, mit dem Techniker davor. Aus der Ferne fehlt jede Rückmeldung – ein gesetzter
Testwert bleibt stehen und die Anlage heizt nicht mehr.

**Folge:** die vier Topics bleiben vorhanden (read-only), verlieren ihr `/Set`-Topic und
erscheinen in Home Assistant nicht mehr als `number`, sondern als Diagnose-Sensor. In der
Web-UI verschwindet das Eingabefeld (die Items-Seite rendert automatisch). Kein UI-Code
nötig.

**Abnahme:**

- [ ] Fixture: alle vier `"IsReadOnly": true`, `PublishTopic: null`
- [ ] `PUT /api/items/…tests.outputs_inactive` → 403/409, **kein** BACnet-Request
      (im Test mit `FakeBacnetClient` prüfen)
- [ ] HA-Discovery enthält für diese vier kein `number`-Objekt

### 9.10 – R10: WW-Sollwerte mit Obergrenze 58 °C

| Datei/Zeile | Item | Alt | Neu |
|---|---|---|---|
| `Warmwater.cs:37` | `SetPointNormal` | `new AnalogValue(72, "°C", 5)` | `new AnalogValue(72, "°C", 5, 58)` |
| `Warmwater.cs:44` | `SetPointReduced` | `new AnalogValue(71, "°C", 5)` | `new AnalogValue(71, "°C", 5, 58)` |
| `Warmwater.cs:51` | `SetPointFrost` | `new AnalogValue(70, "°C", 5)` | `new AnalogValue(70, "°C", 5, 58)` |
| `Warmwater.cs:58` | `SetPointLegio` | `new AnalogValue(73, "°C", 5)` | `new AnalogValue(73, "°C", 5, 58)` |

**58 °C ist die Grenze der Wärmepumpe**, nicht des Reglers (der erlaubt bis DP 190,
5…99 °C). Dieselbe Grenze steht schon an `HeatPump/FlowMaxTemperature` (444) und
`Warmwater/SollwertMaximalWP` (447). Damit sind alle vier Temperaturgrenzen konsistent.

> Die Legionellenschutz-Temperatur (`SetPointLegio`) liegt üblicherweise bei 60…70 °C.
> Mit `Max = 58` ist sie **nicht** mehr einstellbar – das ist bewusst: was die Wärmepumpe
> nicht liefert, soll der Gateway nicht anbieten. Wer höher muss, nutzt `WW-elektrisch`
> (DP 118/421) am Gerät. **Gehört in die Benutzerdoku** (9.13).

**Abnahme:**

- [ ] Fixture: alle vier `"Max": 58`
- [ ] Schreiben von `60` → `OutOfRange`, von `58` → erlaubt
- [ ] `topics-v2.md` §4.2 nennt die 58 °C und die Folge für `SetPointLegio`

### 9.11 – Regenerieren und Gesamtverifikation

Einmalig nach **allen** Datenkorrekturen (nicht nach jeder Zeile):

```bash
node tools/generate-topics/generate-topics.mjs --catalog src/ComfoBox.Domain/Items
node tools/generate-topics/generate-topics.mjs --out docs/topics.json
node tools/generate-topics/generate-topics.mjs --catalog-check src/ComfoBox.Domain/Items
node tools/generate-topics/generate-topics.mjs --check
dotnet build src/ComfoBox.sln -c Release
dotnet test  src/ComfoBox.sln -c Release
```

**Erwartete Diffs in `docs/topics.json`:**

| Topic | Feld | neu |
|---|---|---|
| `Config/…/Warmwater/Entladeschutz` | `Max` | `3` |
| `Config/PrimaryPump/StoppedMode` | `Max` | `11` |
| `Config/PrimaryPump/StoppedMode` | `Description` | ergänzt um PWM |
| `Tests/OutputsInactive`, `Tests/R4`, `Tests/Y1`, `Tests/Y2` | `IsReadOnly` / `PublishTopic` | `true` / `null` |
| `States/Outputs/Y1PrimaryPump`, `States/Outputs/Y2` | `IsReadOnly` / `PublishTopic` | `true` / `null` |
| `States/Inputs/E6SecondRoomTemperature` | `SubscribeTopic`/`PublishTopic` | `…/E7SecondRoomTemperature…` |
| `States/Inputs/E7HpFlowBackTemp` | `SubscribeTopic`/`PublishTopic` | `…/E8HpFlowBackTemp…` |
| `Warmwater/SetPointNormal·Reduced·Frost·Legio` | `Max` | `58` |

**Nichts** davon darf sich ändern: Item-Anzahl (127), `ValueType` aller Items ausser der
bekannten Ausnahme `DoHeatWaterNow`, alle übrigen `Min`/`Max`.

**Abnahme:**

- [ ] beide Prüfläufe Exit 0 · `dotnet build` 0 Warnungen · `dotnet test` grün
- [ ] Diff der Fixture enthält **nur** die Zeilen der Tabelle oben
- [ ] `git diff --stat src_old/` zeigt nur die geänderten Zeilen (keine Encoding-Welle)

### 9.12 – R11: nur ein Hinweis (keine Änderung)

`Config/PrimaryPump/*` (DP 411, 412, 3613, 3614, 3232, 3096) sind die Parameter des
stetigen Ausgangs **Y1**, nicht der Pumpe. Die Nummern sind richtig; Y1 ist bei der
ComfoBox mit der Primärpumpe belegt.

**Nichts im Code ändern.** Der Hinweis steht bereits in
[datenpunkt-register.md](../07-technikerhandbuch/datenpunkt-register.md) und in
[topics-v2.md](../topics-v2.md) §4.7 – Aufgabe ist nur, im
[Review](../07-technikerhandbuch/review-ist-implementierung.md) „**Hinweis, keine
Änderung**" zu vermerken.

**Abnahme:**

- [ ] Review R11 als entschieden markiert
- [ ] kein Code-Diff zu DP 411/412/3232/3096 ausser 9.4

### 9.13 – R12: Gerätefehler sind nicht auslesbar (Doku + UI-Hinweis)

Das Handbuch nennt **keine** DP-Nummer für die zehn Fehlerspeicher-Plätze (§4.9). Der
Gateway kann Gerätefehler daher **nicht** lesen; `ComfoBox/Status/Errors` enthält
ausschliesslich **Gateway**-Fehler.

| Ziel | Änderung |
|---|---|
| `docs/benutzer/09-fehlersuche.md` | Abschnitt „Gerätefehler vs. Gateway-Fehler": Gerätefehler stehen im Fehlerspeicher des Reglers am Display; der Gateway zeigt sie **nicht**. Indirekt erkennbar an `Wärmepumpen-Zustand` (Störungen 11…14, 41, 46, 49, 53, 54, 61, 62) |
| `docs/benutzer/08-monitoring.md` | derselbe Hinweis in einem Satz, mit Verweis auf die Fehlersuche |
| `src/ComfoBox.Web/src/app/features/diagnostics/diagnostics.html` | im Reiter **Fehlerhistorie** (`value="errors"`, ab Zeile 52) ein Hinweis-Block: „Diese Liste enthält Gateway-Meldungen (Verbindung, Zeitüberschreitung, Schreiblimit). Fehler der Wärmepumpe stehen im Fehlerspeicher des Reglers." |
| `src/ComfoBox.Web/src/app/core/i18n/{translations,de,en,fr}.ts` | neuer Schlüssel `diagnostics.errorsScopeHint` in **allen vier** Dateien – fehlt einer, bricht der Build ab |

**Danach:**

```bash
cd src/ComfoBox.Web && npm run lint && npm run build
node tools/build-docs.mjs          # Hilfe-Seiten neu erzeugen (eingecheckt!)
pwsh -NoProfile -File tools/check-links.ps1
```

**Abnahme:**

- [ ] Hinweis im Reiter „Fehlerhistorie" sichtbar
- [ ] `npm run build` grün (i18n vollständig in de/en/fr)
- [ ] `public/help/` neu erzeugt und mit eingecheckt
- [ ] `check-links.ps1` grün

### 9.14 – `topics.md` bleibt eingefroren (Archivkopie in `src_old`)

**Entscheidung:** `docs/topics.md` wird **nicht** angefasst – genau wie `docs/topics.json`.
Beide sind der **V1-Vertrag** und bleiben als historischer Stand stehen. Korrigierte Werte
stehen in [topics-v2.md](../topics-v2.md) und im Katalog, nicht in den V1-Dateien.

**Bereits erledigt (2026-11-15):** Der Legacy-Stand liegt als Archivkopie in
[`src_old/docs/topics.md`](../../src_old/docs/topics.md), mit Archiv-Hinweis im Kopf und
einer Erläuterung in [`src_old/docs/README.md`](../../src_old/docs/README.md).
Damit ist belegt, was das Legacy-Programm erzeugt hat, ohne die eingefrorene Datei zu
berühren.

**In dieser Aufgabe bleibt deshalb nur:**

| Schritt | Aktion |
|---|---|
| 1 | Prüfen, dass `git diff --stat docs/topics.md docs/topics.json` **leer** ist |
| 2 | In [topics-v2.md](../topics-v2.md) §6 sicherstellen, dass alle zehn Korrekturen mit Aufgabe und Zielwert stehen |
| 3 | **Optional** (eigene Session, ≈ 0,5 PT): den Generator um `--emit-markdown <datei>` erweitern, damit die Topic-Liste wieder reproduzierbar ist. Ziel wäre dann eine **neue** Datei (z. B. `docs/topics-v2.md`-Anhang oder `docs/topics.generated.md`) – **nicht** `docs/topics.md` |

**Abnahme:**

- [ ] `docs/topics.md` und `docs/topics.json` sind unverändert (Byte-Vergleich gegen `HEAD`)
- [ ] `src_old/docs/topics.md` enthält 127 Einträge (wie die Quelle)
- [ ] `check-links.ps1` grün

> **Warum keine Handkorrektur mehr:** eine gepflegte `topics.md` würde einen Vertrag
> behaupten, den die Fixture nicht kennt – der Contract-Test vergleicht gegen
> `topics.json`, nicht gegen das Markdown. Zwei Wahrheiten mit demselben Namen sind genau
> der Fehler, den dieses Projekt schon einmal hatte (Fixture 2017 vs. Katalog 2016).

### 9.15 – Abschluss

- [ ] `docs/07-technikerhandbuch/review-ist-implementierung.md`: R1, R2, R3, R5, R6, R7,
      R8, R9, R10 auf „**behoben**" mit Datum; R4, R11, R12 auf „**entschieden**"
- [ ] [status.md](status.md): Phase 9 auf ✅
- [ ] [protokoll.md](protokoll.md): Eintrag mit Testzahl vorher/nachher
- [ ] `CHANGELOG.md`: `Fixed` (R1–R3, R6–R10) + `Changed` mit `⚠️ breaking` (R7)
- [ ] Prompt für die nächste Session ausgeben (§3.4 der [README](README.md))

---

## 7 Entscheidungen auf einen Blick

| Befund | Entscheidung | Aufgabe |
|---|---|---|
| R1 Comfofond → 3432 | **korrigieren** (Code + Doku) | 9.2 |
| R2 Entladeschutz `0/3` | **korrigieren** (Max 3 + Werteprüfung) | 9.3 |
| R3 RestartDelay → 846 | **korrigieren**, **20 min Untergrenze bleibt** (WP-spezifisch) | 9.6 |
| R4 Max 58 | **keine Änderung** – 58 ist die Grenze der Wärmepumpe | 9.11 (nur Doku) |
| R5 `Tests/*` | **weitergehend als vorgeschlagen**: komplett read-only | 9.9 |
| R6 `States/Outputs/*` | **korrigieren** auf die DPs aus §7 | 9.7 |
| R7 `E6…`/`E7…` | Namen sind falsch → **umbenennen**, DPs bleiben | 9.8 |
| R8 `StoppedMode` Max 11 | **korrigieren**; DP 3097 **nicht** nach V1 (V2-Kandidat, read-only) | 9.4 |
| R9 `Cooling` `0/1/3` | **Werteprüfung** statt Bereich | 9.5 |
| R10 WW-Sollwerte | **Max 58 °C** (Wärmepumpen-Grenze) | 9.10 |
| R11 Y1-Namensirreführung | **nur Hinweis**, keine Änderung | 9.12 |
| R12 Fehlerspeicher | **Doku + UI-Hinweis**, keine Funktion | 9.13 |

## 8 Risiken

| Risiko | Gegenmassnahme |
|---|---|
| **Encoding zerstört Umlaute in `src_old/`** | §5: mit CP1252 lesen/schreiben, `git diff --stat` prüfen, `--catalog-check` |
| R6 zielt auf Objekte, die das Gerät nicht kennt | Der Wert ist im Handbuch belegt (§7). Nach dem Deploy am Gerät prüfen – bleibt ein Wert `unknown`, war die alte Nummer die richtige. Rückbau = eine Zeile |
| R7 bricht HA-Entitäten | CHANGELOG + Benutzerdoku + Migrationshinweis; nur **zwei** Topics |
| R10 macht `SetPointLegio` auf 58 unbrauchbar | bewusst; Hinweis in der Benutzerdoku, `WW-elektrisch` als Weg |
| `--out` entfernt das BOM der Fixture | unkritisch, im Commit erwähnen |
| Bereichs-/Werteprüfung bricht bestehende Automationen | nur bei Werten, die das Gerät ohnehin ablehnt; im CHANGELOG nennen |

**Rückbau:** alle Änderungen sind Metadaten. `git revert` des Commits genügt; es gibt keine
Migrations- oder Datenabhängigkeit – ausser bei R7 (zwei Topic-Namen).

## 9 Prompt für die Umsetzungs-Session

```text
Setze Phase 9 aus docs/06-aufgaben/phase-9-datenpunkt-korrekturen.md um.

Lies in dieser Reihenfolge:
1. docs/06-aufgaben/phase-9-datenpunkt-korrekturen.md   (die Aufgaben)
2. docs/07-technikerhandbuch/review-ist-implementierung.md  (die Nachweise R1-R12)
3. docs/07-technikerhandbuch/datenpunkt-register.md  (§6.4.2, §6.5.4, §7)
4. docs/topics-v2.md §6  (die Korrekturliste)
5. .github/copilot-instructions.md  (Projektregeln)

Wichtig:
- Die Katalogquelle ist src_old/ComfoBoxLib/Items/*.cs – Windows-1252! Mit CP1252
  lesen UND schreiben (§5 der Aufgabe), sonst werden Umlaute zerstört.
- Danach regenerieren: node tools/generate-topics/generate-topics.mjs --catalog
  src/ComfoBox.Domain/Items  (Verzeichnis, nicht Datei) und --out docs/topics.json
  sowie --catalog-check / --check.
- docs/topics.json nie von Hand ändern.
- R4, R11 und DP 3097 NICHT ändern (entschieden).
- Reihenfolge: 9.1 zuerst (Mechanik), dann 9.2-9.10, dann 9.11-9.15.
- Am Ende: dotnet test, npm run lint/build, node tools/build-docs.mjs,
  pwsh -NoProfile -File tools/check-links.ps1 – alles grün.
- Protokoll und status.md aktualisieren.
```
