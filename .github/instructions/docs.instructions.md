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
├── 05-entscheidungen/           # ADRs
├── 06-aufgaben/                 # Arbeitsanweisungen je Phase (Task-Dokument)
└── benutzer/                    # Benutzerdokumentation (→ HTML für die Web-UI)
.github/
├── copilot-instructions.md      # Projektregeln für Agenten
├── instructions/*.instructions.md
├── workflows/*.yml
└── ISSUE_TEMPLATE/, pull_request_template.md
````

> **Die Dokumentation ist Teil des Projekts, kein Beiwerk.** Verhaltensänderungen ohne
> Doku-Update sind ein unvollständiger PR.

> **Zwei Doku-Welten:** `docs/02-modernisierung/` ist die **Spezifikation für
> Entwickler**. `docs/benutzer/` ist die **Anleitung für Anwender** – sie wird
> unverändert in die Web-UI, nach HACS und ins Synology-Paket ausgeliefert. Beide
> werden getrennt gepflegt; siehe §8.

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
### Zeilenverweise

| Kontext | Schreibweise |
|---|---|
| Neuzuimplementierung (`src/`) | `[ItemFactory.cs:66](../../src/ComfoBox.Domain/Items/Catalog.cs#L66)` |
| Altbestand (`src_old/`) | `[BACnetBase.cs:120](../../src_old/Bacnet/BACnetBase.cs#L120)` |

> **Regel:** Verweise auf `src_old/` **kennzeichnen** („im Altbestand"), damit der Leser
> weiß, dass der Code dort nicht mehr gepflegt wird.
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

## 7. Benutzerdokumentation (`docs/benutzer/`)

Diese Seiten gehen **unverändert** in drei Kanäle: Web-UI (`/help`), HACS und Synology
([ADR-010](../../docs/05-entscheidungen/adr-010-benutzerdokumentation.md)).

### 7.1 Front-Matter ist Pflicht

Jede Seite beginnt mit vollständigem YAML-Front-Matter – `tools/build-docs.mjs` bricht
sonst ab:

````yaml
---
title: MQTT einrichten
description: Broker konfigurieren und Topics prüfen
order: 40
icon: mqtt
tags:
  - anleitung
  - mqtt
---
````

| Feld | Pflicht | Regel |
|---|---|---|
| `title` | ja | sichtbar in der Navigation |
| `description` | ja | Teaser + Suchindex |
| `order` | ja | ganzzahlig, sortiert die Navigation |
| `icon` | ja | Name eines PrimeIcons/Optimus-Icons |
| `tags` | nein | Liste, für die Suche |

### 7.2 Inhaltliche Regeln

| Regel | Begründung |
|---|---|
| **Deutsch, Sie-Form** | Zielgruppe sind Anwender, keine Entwickler |
| **Symptom → Ursache → Lösung** | die Suchergebnisse starten beim Symptom |
| **Keine Implementierungsdetails** | kein `BACnetBase.cs`, kein Commit-Sha, kein Stacktrace |
| **Konkrete Befehle zeigen** | kopierbar, mit erwarteter Ausgabe |
| **Ein Thema pro Seite** | duplizieren statt teilen |
| Screenshots verlinken, nicht einbetten | Bilder liegen unter `docs/images/` |

> **Was nicht hineingehört:** Klassenname, Dateipfade des Quellcodes, interne
> Bezeichner, Changelog-Vokabular. Details: [ADR-010](../../docs/05-entscheidungen/adr-010-benutzerdokumentation.md).

### 7.3 Bauen und Prüfen

````bash
# HTML aus Markdown erzeugen (inkl. index.json für die Suche)
node tools/build-docs.mjs

# Alle relativen Links und Anker prüfen
pwsh -File tools/check-links.ps1
````

`tools/build-docs.mjs` braucht `marked` (devDependency von `src/ComfoBox.Web` oder
temporär via `npm i marked`). Ohne `marked` bricht der Build mit einer klaren Meldung ab –
**nicht** stillschweigend.

## 8. Aufgaben-Dokument (`docs/06-aufgaben/`)

Die Umsetzung läuft in **Phasen**, jede Phase hat eine Datei mit Aufgaben, Ziel-Dateien,
Verifikationsbefehlen und Abnahmekriterien.

| Datei | Zweck |
|---|---|
| `README.md` | Wie eine Session abläuft, globale Regeln, Vorbedingungen |
| `status.md` | Fortschrittstabelle (⬜ offen / 🟡 in Arbeit / ✅ fertig) |
| `protokoll.md` | Session-Protokoll (was wurde wann gemacht) |
| `phase-N.md` | Die Aufgaben einer Phase |

**Regeln:**

- Vor Beginn einer Aufgabe: Zeile in `status.md` auf 🟡 setzen
- Nach Abschluss: auf ✅ setzen **und** in `protokoll.md` eintragen
- Wird eine Aufgabe umgesiedelt, bleibt die **ID** stabil (`4.7`) – die Phase ändert sich
- Vorbedingungen (fehlende Werkzeuge wie Python oder Docker) gehören in die
  Phasen-Datei, damit die KI-Session sie nicht suchen muss

## 9. Checkliste vor dem PR

- [ ] Neues Verhalten in `docs/` beschrieben
- [ ] Bestehende Links noch gültig (`docs/README.md` als Index aktualisiert)
- [ ] Keine Tippfehler in Code-Beispielen (die werden übernommen!)
- [ ] Codebeispiele **kompilieren** (zumindest syntaktisch)
- [ ] ADR angelegt, falls eine Entscheidung getroffen wurde
- [ ] `CHANGELOG.md` aktualisiert
- [ ] Relative Links, keine absoluten Pfade
