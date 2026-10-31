# ADR-006: Item-Katalog als deklarative Domäne

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Domäne, Datenmodell |

## Kontext

Der Kern des Systems sind **127 BACnet-Objekte** mit Typ, Grenzen, Einheit und Schreibbarkeit.
Der Ist-Zustand modelliert sie über Klassen mit expression-bodied Properties:

````csharp
// src_old/ComfoBoxLib/Items/Config.cs
public AnalogValue AbsMinVorlauftemp => new AnalogValue(830, 0, 99, "°C") { IsReadOnly = !Settings.Default.ExpertMode };
````

Der Katalog wird zur Laufzeit per Reflection (Fasterflect) enumeriert
(`ItemFactory.CreateItemsFromClass`).

**Probleme:**

| Problem | Beleg |
|---|---|
| Jeder Property-Zugriff erzeugt ein **neues Objekt** | expression-bodied Properties |
| Der Typ ergibt sich implizit aus dem Property-Typ → nicht direkt testbar | — |
| `DateValue` und `CommandValue` sind **tote Zweige** (nie instanziiert / nie publiziert) | Befunde A20, A14b |
| Der Schreibschutz (`IsReadOnly = !ExpertMode`) hängt an einem **statischen Settings-Zugriff** | Item-Konstruktor → nicht testbar, nicht parametrisiert |
| Reflection + Fasterflect als Abhängigkeit | B1, B7 (Paket für .NET 10 nötig) |
| Prioritäts-Attribute werden gelesen und **verworfen** | Befund A15 |
| `ItemFactory` erzeugt `MqttItem` mit `RefreshPriority.None` für **alle** Items | Befund A15 |

## Entscheidung

**Der Item-Katalog wird zu einer deklarativen, framework-freien Datenstruktur in
`ComfoBox.Domain`.**

- `ItemDefinition` als `record` mit `required`-Eigenschaften
- Keine Reflection, kein Fasterflect, kein dynamisches Property-Zugriff
- Schreibbarkeit und Expert-Modus sind **Datenfelder**, keine Settings-Zugriffe im Konstruktor
- Enum-Werte sind Teil der Definition (`IReadOnlyDictionary<int, string>`)
- `RefreshPriority` ist ein **echtes** Feld und wird in der Polling-Logik ausgewertet
- Der Katalog ist **serialisierbar** → die UI bekommt ihn als JSON über `/api/items`
- Die 127 Definitionen werden **aus dem Legacy-Quelltext generiert**
  (`src_old/ComfoBoxLib/Items/**`) und können jederzeit neu erzeugt werden:
  `node tools/generate-topics/generate-topics.mjs --catalog src/ComfoBox.Domain/Items`
  (`--catalog-check` prüft, ob die committierten Dateien noch passen)

### Beispiel

````csharp
new ItemDefinition
{
    Id = "zone.set_point_normal",
    Group = "Zone",
    Name = "SetPointNormal",
    BacnetObjectId = 62,
    Kind = ItemValueKind.Analog,
    Min = 5f, Max = 30f, Unit = "°C",
    IsReadOnly = false,
    RequiresExpertMode = false,
    Priority = RefreshPriority.Primary,
    Description = "Raumsollwert normal 5.0..30.0",
};
````

> **Die Id-Regel:** Topic-Pfad in camelCase → snake_case, mit Punkten verbunden:
> `Zone/SetPointNormal` → `zone.set_point_normal`. Die Id ist unabhängig vom
> konfigurierbaren `BaseTopic` und dient als Schlüssel in den REST-Routen.

### Topic-Ableitung

````csharp
public string StateTopic   => $"{BaseTopic}/{Group}/{Name}";
public string? SetTopic    => IsWritable ? $"{StateTopic}/Set" : null;
public string? AsNumberTopic => Kind == ItemValueKind.Enum ? $"{StateTopic}/AsNumber" : null;
````

> Die Topic-Ableitung ist **die** zentrale Stelle, die den Kompatibilitätsvertrag
> durchsetzt. Ein Contract-Test vergleicht sie gegen `docs/topics.json`.

### Schreibbarkeit und Expert-Modus

| Regel | Umsetzung |
|---|---|
| Item ist nie schreibbar | `IsWritable = false` → kein `/Set`-Topic |
| Expert-Modus nötig | `RequiresExpertMode = true` **und** `Gateway:ExpertMode = false` → Item gilt als read-only |
| Effective-Writable | `IsWritable && (!RequiresExpertMode || ExpertMode)` |

> **Kompatibilität:** Der Ist-Zustand legt `IsReadOnly = !ExpertMode` **im Konstruktor** ab,
> d. h. das Topic-Set hängt vom Startzeitpunkt ab. Die neue Logik ist **zeitunabhängiger**:
> Ein Item mit `RequiresExpertMode` bekommt sein `/Set`-Topic nur, wenn Expert-Modus aktiv ist
> — identisch zum Ist-Zustand, aber explizit und testbar.

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **Deklarativer Katalog (Records)** ✅ | testbar, serialisierbar, keine Reflection, kein Fremdpaket | – |
| Reflection über Klassen behalten | kleinster Diff | nicht testbar, `DateValue`/`CommandValue`-Zweige bleiben, Fasterflect-Abhängigkeit, Settings-Zugriff im Konstruktor |
| **Source-Generator** (C#-Generator erzeugt den Katalog) | typsicher + deklarativ | Overkill für 127 Items; Generator-Wartezeit; komplexeres Debugging |
| JSON/YAML-Katalog als Laufzeitquelle | Katalog ohne Build editierbar | Validierung und Typsicherheit gehen verloren; Fehler erst zur Laufzeit; BACnet-IDs ohne Compiler-Schutz |
| Datenbank | dynamisch | massiver Overhead; Katalog ändert sich selten |
| Bestehende Klassen + Reflection **und** `IsReadOnly` als Parameter | minimaler Umbau | Settings-Zugriff bleibt, Testbarkeit bleibt schlecht |

## Konsequenzen

### Positiv

- **Vollständig testbar:** der Katalog ist eine reine Datenstruktur → Contract-Test ohne MQTT
- **Kein Fremdpaket** (Fasterflect entfällt) → weniger Update-Risiko
- **Kein impliziter Zustand:** `IsReadOnly` hängt nicht mehr an einem globalen Settings-Objekt
- Die UI kann den Katalog als JSON ausliefern → **eine** Quelle der Wahrheit
- `RefreshPriority` wird wirksam → weniger Buslast (127 Items alle 60 s → 40 primary, 87 secondary)
- Tote Zweige (`DateValue`, `CommandValue`) verschwinden

### Negativ / Aufwand

- **Mehr Zeilen Code** als die Properties-Variante (ein Record je Item ≈ 12 Zeilen statt 1)
  → 127 × 12 ≈ 1.500 Zeilen
- **Einmalige Generierung** nötig (Skript aus `docs/topics.json`), sonst manueller Übertragungs-
  fehler → **Contract-Test deckt das auf**
- Der Generator liest den Legacy-Quelltext – er muss dessen **gemischte
  Kodierung** beachten (Windows-1252 in den Item-Dateien, UTF-8 in den
  Enum-Dateien). Das ist in `tools/generate-topics/generate-topics.mjs`
  (`readLegacyFile`) gelöst.
- Manuelle Pflege: eine neue ComfoBox-Generation erfordert Katalog-Erweiterung
  (im Ist-Zustand genauso)
- Kein Compile-Zeit-Schutz mehr für die Item-Zuordnung → der Contract-Test ist der Ersatz

### Neutral

- Die **Reihenfolge** der Topics im Katalog ist irrelevant (die Topic-Baumstruktur entsteht
  aus `Group`/`Name`)
- Der Katalog bleibt **statisch** – keine Laufzeit-Erweiterung. Das entspricht dem Ist-Zustand
  und ist für eine Heizungssteuerung richtig: zur Laufzeit lässt sich kein beliebiges
  BACnet-Objekt anlegen.

## Verwandte Dokumente

- [datenmodell-und-api](../02-modernisierung/datenmodell-und-api.md)
- [mqtt-kompatibilitaet](../04-migration/mqtt-kompatibilitaet.md)
- [teststrategie](../04-migration/teststrategie.md)
- [Ist-Befunde A15](../01-ist-zustand/befunde.md#d--architektur-und-wartbarkeit-p2)
