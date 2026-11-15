# ADR-014 – Topics V2: Namensraum und Experten-Modus

> **Status:** angenommen · **Datum:** 2026-11-16 · **Bereich:** Topics V2
> ([topics-v2.md](../topics-v2.md), [Phase 10](../06-aufgaben/phase-10-topics-v2.md))

## Kontext

Der V1-Topic-Vertrag ist **eingefroren**: 127 Topics, `docs/topics.json` ist die
Test-Fixture des Contract-Tests, `docs/topics.md` ist die historische Dokumentation.
V2 soll den Bestand **additiv** erweitern – vor allem um lesende Diagnose- und
Energiewerte.

Dabei entstehen drei Fragen, die der V1-Vertrag nicht beantwortet:

1. **Wo liegen die neuen Lesetopics?** Die Projektregel sagt: neue Topics nur unter
   `Status/` bzw. `Command/`. Das passt für lesende Werte.
2. **Wo liegen die neuen schreibbaren Konfigurationswerte?** Ein Paar
   `Config/X/Y` + `Config/X/Y/Set` ist symmetrisch und für Home Assistant eindeutig.
   Ein `Status/X/Y/Set` wäre semantisch falsch („Status“ ist lesend).
3. **Dürfen neue schreibbare Werte ohne Schutz entstehen?** Die neuen Werte ändern
   das Regelverhalten der Anlage (Heizkurve, Ventilation, Kältekreis). Ein
   versehentliches Schreiben aus einer Automation darf nicht möglich sein.

Zusätzlich ist der Namensraum `Status/` bereits durch **Gateway-Topics** belegt
(`Status/Health`, `Status/Errors`, `Status/Version`, …). Eine Kollision würde die
Availability oder die Fehlerhistorie überschreiben.

## Optionen

| | Option | Bewertung |
|---|---|---|
| **A** | Neue Lesetopics unter `Status/`, neue schreibbare Topics **nur** unter `Status/`/`Command/` | Erfüllt die alte Regel wörtlich, zerreisst aber das Lese-/Schreibpaar. Ein `Status/.../Set` ist semantisch falsch. |
| **B** | Neuer Top-Level-Namensraum `V2/…` | Keine Kollision, aber ein sichtbarer Bruch im Topic-Baum: Nutzer und HA-Vorlagen müssten zwei Pfade kennen. |
| **C** | Lesetopics unter `Status/`, schreibbare Config-Topics unter `Config/` – **alle neuen schreibbaren Items nur mit Experten-Modus** | Additiv, symmetrisch, testbar. Erweitert die alte Namensraum-Regel bewusst. |
| **D** | Wie C, aber **ohne** Experten-Modus | Einfacher, aber unsicher: neue Heizkurven- und Ventilationswerte wären sofort schreibbar. |

## Entscheidung

**Option C.**

### 1. Namensräume

| Namensraum | Inhalt | Status |
|---|---|---|
| `Zone/`, `Warmwater/`, `Tests/` | V1-Bedien-Topics | eingefroren |
| `States/…` | V1-Lesetopics | eingefroren |
| `Config/…` | V1-Einstellungen **und neue V2-Einstellungen** | erweitert |
| `Status/…` | **neue** V2-Lesetopics (plus Gateway-Status) | neu |
| `Command/…` | **neue** Aktionen (plus Gateway-Neustart) | neu |
| `Special/…` | V1-Bestand (`NumberOfWritesPer24h`) | eingefroren |

`Config/` wird für **neue schreibbare** Werte geöffnet, damit `Config/X/Y` und
`Config/X/Y/Set` zusammenbleiben. Das ist die einzige bewusste Weitung der Regel
„neue Topics nur unter `Status/`/`Command/`“.

### 2. Experten-Modus

**Alle neuen schreibbaren V2-Topics tragen `RequiresExpertMode = true`.**

| Zustand | `/Set`-Topic | HA-Discovery | REST |
|---|---|---|---|
| Experten-Modus **aus** | existiert **nicht** | `sensor` (nur lesen) | `writable = false` |
| Experten-Modus **an** | existiert | `number`/`select` (`entity_category: config`) | `writable = true` |

Lesen ist **immer** erlaubt. Die Regel gilt für `Config/*` **und** für neue
`Command/*`-Aktionen (z. B. `ResetHeatingCurveAdaption`). Der bestehende
`Command/Restart` (Gateway-Neustart) bleibt unverändert.

### 3. V2 ist eine zweite Fixture

`docs/topics.json` bleibt **byte-identisch** der V1-Vertrag. V2 bekommt eine eigene
Fixture `docs/topics-v2.json`. Im Katalog unterscheidet `ItemDefinition.IsV2` die
beiden Mengen; der Contract-Test prüft sie getrennt.

### 4. Reservierte Pfade

Die Gateway-Status-Topics sind **reserviert**. Kein Katalog-Item darf denselben
Pfad verwenden:

```
Status/Health · Status/Version · Status/Uptime · Status/StartedAt
Status/Writes · Status/WritesMax · Status/Bacnet · Status/Mqtt
Status/LastError · Status/Errors · Status/Restart
Status/Energy/Available (V2-Gate)
Connected · Version · Special/NumberOfWritesPer24h · Command/Restart
```

Ein Test (`CatalogTests.Reservierte_Gateway_Topics_Sind_Keine_Items`) sichert das ab.

## Folgen

**Positiv**

- V2 bleibt vollständig additiv: kein V1-Topic ändert sich.
- Lesen/Schreiben bleibt symmetrisch (`Config/X/Y` + `/Set`).
- Neue sensible Werte sind ohne Experten-Modus **nicht** schreibbar – auch nicht
  versehentlich aus Home Assistant.
- Der V1-Vertrag bleibt als eigene Fixture prüfbar.

**Negativ**

- Zwei Fixtures und zwei Generator-Läufe (`--check`, `--check-v2`).
- Ein neues schreibbares Topic ist ohne Experten-Modus **unsichtbar** – Nutzer
  müssen den Modus bewusst einschalten.
- Der Namensraum `Status/` ist geteilt; die reservierten Pfade müssen gepflegt
  werden.
- Der Experten-Modus wird für MQTT heute beim Start gebunden. Die Laufzeit-
  Umschaltung ist Aufgabe 10.13; bis dahin gilt der Startzustand.

**Neutral**

- Die Entscheidung ändert **keine** Payload-Formate und **keine** Enum-Namen.
- Die neuen Topics sind rein optional; wer sie nicht braucht, sieht keine Änderung
  an seinen V1-Automationen.

## Alternativen, die verworfen wurden

- **A (nur `Status/`/`Command/`):** hätte `Status/X/Y/Set` erzeugt. Das ist
  semantisch falsch, erschwert die HA-Discovery und macht aus einem lesenden
  Namensraum einen schreibbaren.
- **B (`V2/…`):** kein Mehrwert gegenüber `Status/`/`Config/`, aber ein Bruch im
  Topic-Baum. Nutzer und Vorlagen müssten einen dritten Pfad lernen.
- **D (ohne Experten-Modus):** Die neuen Werte ändern das Regelverhalten der
  Anlage. Der Schutz ist wichtiger als die Bequemlichkeit.
