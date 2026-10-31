---
description: "Use when writing or updating documentation in this repository — docs/ folder, README, ADRs, changelog, topic reference, or developer guides. Covers structure, language, and the rules for documenting behaviour changes."
applyTo: ["docs/**/*.md", "*.md", ".github/**/*.md"]
---

# Dokumentations-Regeln

## 1. Struktur

````
docs/
├── README.md                    # Index + Navigation
├── 01-ist-zustand/              # Was ist heute
├── 02-modernisierung/           # Die Spec
├── 03-deployment/               # Container, NAS, CI/CD
├── 04-migration/                # Roadmap, Kompatibilität, Tests, Risiken
└── 05-entscheidungen/           # ADRs
.github/
├── copilot-instructions.md      # Projektregeln für Agenten
├── instructions/*.instructions.md
├── workflows/*.yml
└── ISSUE_TEMPLATE/, pull_request_template.md
````

> **Die Dokumentation ist Teil des Projekts, kein Beiwerk.** Verhaltensänderungen ohne
> Doku-Update sind ein unvollständiger PR.

## 2. Sprache

| Inhalt | Sprache |
|---|---|
| Prosa, Überschriften, Tabellen | **Deutsch** |
| Code, Bezeichner, Dateinamen | Englisch |
| Commit-Messages | Englisch (Conventional Commits) |
| API-Feldnamen | Englisch (`stateTopic`, `writable`) |
| UI-Texte | **Deutsch** |

> **Ausnahme:** Enum-**Werte** und Topic-Segmente bleiben englisch – sie sind Teil des
> MQTT-Vertrags.

## 3. Form

- **Markdown** mit ATX-Überschriften (`#`, `##`, `###`)
- Eine Ebene pro Abschnitt, keine Übersprünge (`#` → `##` → `###`)
- Tabellen für Vergleiche, Listen für Sequenzen
- Codeblöcke **mit** Sprachangabe
- Dateipfade als Markdown-Links: `[docs/topics.json](../../docs/topics.json)`
- Zeilenverweise: `[ItemFactory.cs:66](../../src/ComfoBoxMqtt/Groups/ItemFactory.cs#L66)`
- Relative Links, damit die Doku auf GitHub **und** lokal funktioniert
- „Ist" und „Soll" klar trennen

## 4. Pflichtinhalte je Dokumenttyp

### ADR (`docs/05-entscheidungen/`)

| Feld | Pflicht |
|---|---|
| Status | `Proposed` / `Accepted` / `Deprecated` / `Superseded` |
| Datum | ISO-Konform |
| Kontext | Ausgangslage + wirkende Kräfte |
| Entscheidung | Was wird entschieden |
| Alternativen | Welche Optionen verworfen wurden **und warum** |
| Konsequenzen | Positiv **und** negativ |

> **Ein ADR ist unveränderlich.** Eine geänderte Entscheidung → **neues** ADR, das das alte
> ersetzt (altes auf `Superseded` setzen).

### Verhaltensänderung

Wenn sich MQTT-Topics, Payloads, die REST-API oder die Konfiguration ändern:

1. [`docs/04-migration/mqtt-kompatibilitaet.md`](../../docs/04-migration/mqtt-kompatibilitaet.md)
   prüfen – ist es eine **brechende** Änderung?
2. [`docs/03-deployment/konfiguration-und-secrets.md`](../../docs/03-deployment/konfiguration-und-secrets.md)
   → neue Settings ergänzen
3. `CHANGELOG.md` → Eintrag unter `Added` / `Changed` / `Fixed`
4. Bei strategischen Entscheidungen → neues ADR

### Bugfix

Wenn ein Befund aus [`docs/01-ist-zustand/befunde.md`](../../docs/01-ist-zustand/befunde.md)
behoben wird:

- Befund-ID im PR-Titel und in der Commit-Message nennen (`fix(bacnet): A8 …`)
- Befund in `befunde.md` als **erledigt** markieren
- Testreferenz im PR angeben (besser: Testname)

## 5. Topic-Dokumentation

`docs/topics.md` und [`docs/topics.json`](../../docs/topics.json) werden **generiert** (aus dem
Item-Katalog), nicht handgeschrieben.

> ⚠️ **Wichtig:** `docs/topics.json` ist die **Test-Fixture** des Contract-Tests.
> Sie darf nur durch den **Generator** geändert werden, nie manuell.
> `docs/Mono/MoMA Scan Results.html` ist die **BACnet-Fixture** – ebenfalls unangetastet.

## 6. Struktur der Spec (`docs/02-modernisierung/`)

Jedes Dokument beginnt mit einem kurzen Zweck-Satz und endet mit einer Checkliste oder einem
„Definition of Done". Querverweise auf die ADR-Nummern sind Pflicht.

## 7. Checkliste vor dem PR

- [ ] Neues Verhalten in `docs/` beschrieben
- [ ] Bestehende Links noch gültig (`docs/README.md` als Index aktualisiert)
- [ ] Keine Tippfehler in Code-Beispielen (die werden übernommen!)
- [ ] Codebeispiele **kompilieren** (zumindest syntaktisch)
- [ ] ADR angelegt, falls eine Entscheidung getroffen wurde
- [ ] `CHANGELOG.md` aktualisiert
- [ ] Relative Links, keine absoluten Pfade
