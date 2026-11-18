# ADR-015 – Baugruppen-Verfügbarkeit statt Energie- und COP-Topics

> **Status:** Accepted · **Datum:** 2026-11-18 · **Bereich:** Topics V2
> ([topics-v2.md](../topics-v2.md), [Phase 10](../06-aufgaben/phase-10-topics-v2.md))
> · **Ergänzt** [ADR-014](adr-014-topics-v2-namensraum-und-expertenmodus.md)

## Kontext

[ADR-014](adr-014-topics-v2-namensraum-und-expertenmodus.md) hat den V2-Vertrag
additiv eingeführt und dabei **Energie und COP** (DP 3340…3357, Handbuch §4.8) als
besonders nützlichen Block aufgenommen. Gegen das Verhalten der Referenzanlage
geprüft, zeigt sich:

1. **Die ComfoBox Series 5 hat keinen Wärmemengenzähler.** Die 14 Items des Blocks
   liefern dauerhaft `0` (`MeasurementMode = None`). Der Gateway hat das mit einem
   eigenen Gate abgefangen (`Status/Energy/Available`, `EnergyMeasurementGate`) –
   der Aufwand ist groß, der Nutzen null.
2. **Einzelne Fühler sind nicht verbaut.** Der Regler meldet sie mit dem Sentinel
   `32767` (INT16-Maximum). Betroffen sind 14 Items, davon **alle** neu (V2).
3. **Zwei Objekte existieren am Regler gar nicht.** `Config/HeatingSystem/MixerDirectOffset`
   (DP 3608) und die beiden Kühlpufferfühler (DP 3924/3925) antworten mit
   `ERROR_CODE_UNKNOWN_OBJECT`. Jeder Leseversuch erzeugt einen Fehler und belegt
   den Bus. `MixerDirectOffset` allein füllte 35 von 50 Plätzen des Fehler-Ringpuffers
   und verdrängte damit alle anderen Meldungen.
4. **Der Regler weiß selbst, welche Baugruppen vorhanden sind.** Das Handbuch führt
   Typ-Parameter: `Pufferspeichertyp` (DP 805), `Kühlpufferspeichertyp` (DP 3918),
   `Sonnenkollektor Hydraulik` (DP 807), `Fühler zu WP-Regelung` (DP 2800),
   `Warmwasserbereitung` (DP 117). Sie sind read-only und selten.

Damit stehen drei Fragen: Was gehört **nicht** in den Katalog? Wie wird ein Wert
gesperrt, den es an dieser Anlage nicht gibt? Und wie bleibt der Katalog für Anlagen
mit Pufferspeicher, Kühlung oder Solar vollständig?

## Optionen

| | Option | Bewertung |
|---|---|---|
| **A** | Nichts tun | `32767` und `0 kWh` bleiben in Home Assistant sichtbar; der Fehler-Ringpuffer bleibt blockiert. |
| **B** | Alle betroffenen Items aus dem Katalog entfernen | Zerstört den Katalog für andere Anlagen: eine ComfoBox mit Solar braucht `Status/Solar/*`. |
| **C** | Typ-Parameter lesen und abhängige Items zur Laufzeit sperren | Katalog bleibt vollständig, Bus und Fehlerring bleiben sauber. Braucht vier neue Items und ein Gate. |
| **D** | Nur in der UI ausblenden | Topics publizieren weiter `32767` – genau das Problem. |

## Entscheidung

**Option C, ergänzt um eine Ausnahme (Option B für den Energie-Block).**

### 1. Was aus dem Katalog **entfällt**

| Was | Warum |
|---|---|
| `Status/Energy/*` (14 Items, DP 3340–3357) | Die ComfoBox hat keinen Wärmemengenzähler – der Block ist an dieser Gerätefamilie **nie** möglich. |
| `derived.cop_instant` | Rechnete aus zwei Energie-Zählern, die es nicht gibt. |
| `Config/HeatingSystem/MixerDirectOffset` (DP 3608) | Das Objekt existiert am Regler nicht; jeder Poll erzeugte einen Fehler. |

Damit entfallen auch `RequiresEnergyMeasurement`, das `[Energy]`-Attribut, das
`EnergyMeasurementGate` und `Status/Energy/Available`. Der V2-Vertrag hat danach
**67** statt 78 Items; die V1-Fixture bleibt unverändert (127 Items).

### 2. Was **bleibt** und wie es gesperrt wird

Vier Typ-Parameter kommen als read-only V2-Items hinzu (Priorität `Slow`):

`Config/General/BufferType` (805), `Config/General/CoolingBufferType` (3918),
`Config/General/SolarHydraulics` (807), `Config/General/HeatPumpSensor` (2800).
`Config/Warmwater/Equipment` (117) gibt es bereits.

Das `HardwareAvailabilityGate` leitet daraus je Baugruppe einen Zustand ab
(`Unknown`/`Available`/`Unavailable`) und

- **liest** Items einer nicht vorhandenen Baugruppe **nicht** (kein Fehler, keine Buslast),
- **publiziert** ihre Werte nicht und leert beim Wechsel die retained Topics,
- meldet den Zustand einmalig unter `ComfoBox/Status/Hardware/<Baugruppe>` (`1`/`0`),
- meldet ihn einmal je Baugruppe in `Status/Errors`.

Zusätzlich gilt der **Sentinel**: liefert ein Temperaturfühler `32767`, wird der Wert
nicht gespeichert und nicht publiziert – einmalige Meldung je Item. Das deckt auch
Fühler ab, für die es keinen Typ-Parameter gibt (z. B. Vorlauf Zone, DP 14).

### 3. Zustand „unbekannt" ist nicht „vorhanden"

Solange ein Typ-Parameter nicht gelesen ist, gilt die Baugruppe als **nicht**
verfügbar: es wird nichts gelesen und nichts publiziert. Ein `32767` wäre schlimmer
als ein fehlender Wert. Nach dem ersten erfolgreichen Read (beim Start, danach
stündlich) wird der Zustand sofort wirksam.

## Konsequenzen

**Positiv**

- Home Assistant bekommt keine `32767`-Werte und keine erfundenen `0 kWh` mehr.
- Der Fehler-Ringpuffer (50 Plätze) bleibt für echte Fehler frei.
- Weniger Buslast: 17 Items werden an der Referenzanlage gar nicht mehr gelesen.
- Der Katalog bleibt für Anlagen mit Puffer, Kühlung und Solar vollständig.
- Je Baugruppe eine belastbare `availability_topic` in Home Assistant.

**Negativ / Risiken**

- **Vertragsänderung:** 15 V2-Topics verschwinden, 4 kommen hinzu. Bereits angelegte
  Entitäten in Home Assistant müssen einmal gelöscht werden (V2 ist erst seit 1.2.0
  veröffentlicht).
- Wer den Energie-Block vermisst, kann ihn nicht per Konfiguration einschalten – die
  Hardware fehlt. Ein anderer Regler mit Wärmemengenmessung bekäme ihn über ein neues
  ADR zurück.
- Das Gate hängt an der Reihenfolge der Reads: die Typ-Parameter werden beim Start
  **vor** dem ersten regulären Zyklus gelesen. Schlägt dieser Read fehl, bleibt die
  Baugruppe „unbekannt" und ihre Items werden nicht gelesen (sichtbar als leerer Wert
  in der UI, nicht als Fehler).
- Der Sentinel ist im Handbuch **nicht** dokumentiert; er ist aus dem Verhalten
  abgeleitet und wird nur für Einheit `°C` angewendet.

## Alternativen (verworfen)

- **Energie-Block behalten und nur sperren** (bisheriges `EnergyMeasurementGate`):
  löst kein Problem, weil die Messung an dieser Gerätefamilie nie verfügbar wird.
- **`MixerDirectOffset` auf `None` setzen statt entfernen:** das Item bliebe im
  Katalog, in Home Assistant und in der UI – ohne jeden Wert.
- **Sentinel `32767` generell als „kein Wert" behandeln:** für Zähler (Einheit `h`)
  ist `32767` ein möglicher echter Stand. Die Prüfung gilt deshalb nur für
  Temperaturfühler.
