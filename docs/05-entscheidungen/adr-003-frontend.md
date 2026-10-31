# ADR-003: Frontend – Angular 22 + Optimus UI

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Frontend, UI |

## Kontext

Anforderung 2: *„Angular/Optimus UI für Konfiguration und Statusübersicht/Bedienung"*.

Der Ist-Zustand hat **keine** Web-Oberfläche. Es existiert nur der WPF-`DemoClient`, der:

- direkt (ohne MQTT) mit der ComfoBox spricht,
- Port/Baudrate fest im Code verdrahtet hat (`COM4`, 76800),
- bei **jeder** Wertänderung sofort zurückschreibt,
- keine Status-/Health-Anzeige kennt,
- Windows-only ist,
- nicht auf der DS923+ laufen kann.

Bedarf: Konfiguration **und** Statusübersicht **und** Bedienung; Fehler/Verbindungsprobleme
müssen sichtbar sein (Anforderung 5); Betrieb auf der DS923+ (Anforderung 3).

Stand 2026-10-31:

| Angular | Status | Support bis |
|---|---|---|
| 20 | LTS | 2026-11-28 |
| 21 | LTS | 2027-06 |
| **22** | **Active** | **2028-06** |

`@openng/optimus-ui` 2.0.2: MIT, community-maintained, Angular-21+, 80+ accessible Components,
API-kompatibel zu PrimeNG v21, Presets Aura/Lara/Material/Nora.

## Entscheidung

**Die Web-UI wird mit Angular 22 und der Optimus-UI-Library (`@openng/optimus-ui`) umgesetzt.**

- Angular **22** (aktive Version), Standalone-Komponenten, Signals, zoneless
- Optimus UI 2.x, Preset **Aura** (Default)
- **Signal-Stores** statt NgRx – für diese Größe ausreichend
- **REST** für Lesen/Schreiben, **SSE** für Live-Werte (→ [ADR-005](adr-005-api-stil.md))
- Build über `@angular/build`; Auslieferung als statisches Bundle an nginx
- Internationalisierung der UI-Texte auf **Deutsch** (Code-Bezeichner englisch)
- Der WPF-`DemoClient` entfällt als eigenständige Anwendung; seine Kernfunktionen
  (Item-Baum, freier BACnet-Leser) werden durch den Item-Browser bzw. ein separates
  Konsolen-Werkzeug (`tools/bacnet-probe`) abgelöst

### Installation

````bash
ng new comfobox-web --style=scss --routing --style=scss
cd comfobox-web
ng add @openng/optimus-ui          # Preset Aura
````

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **Angular 22 + Optimus UI** ✅ | vom Nutzer gewünscht; moderne, gut dokumentierte UI-Library | – |
| Angular 22 + Angular Material | sehr verbreitet | Optimus UI wurde explizit gewünscht; Material bringt ein eigenes Design-System mit, das dem HA-Optik-Konzept widerspricht |
| Angular 22 + PrimeNG 21 | reif, große Community | Optimus UI **ist** die PrimeNG-kompatibler Nachfolger; zwei Libraries parallel wäre unnötig |
| **Angular 21 (LTS)** | stabil, längerer Support | Optimus UI verlangt 21+ → 22 erfüllt das mit aktivem Support bis 2028 |
| Vue / React / Svelte | gut | Angular ist explizit gefordert |
| **Blazor / Razor Pages** | C#-Stack | Angular ist gefordert; SPA-Ansatz passt besser zu Live-Werten |
| **Server-gerenderte Razor-Seiten** | einfach | kein Live-Push, höherer Server-Overhead |
| UI direkt über MQTT (mqtt.js im Browser) | kein Backend nötig | kann weder Serial-Zustand noch Neustart auslösen; Health wäre nicht vollständig |
| **Den WPF-DemoClient weiterpflegen** | wenig Aufwand | Windows-only, kein Konfigurations-/Status-UI, kein Health |

## Konsequenzen

### Positiv

- Einheitliche Technologie: C#-Backend + TypeScript-Frontend, ein Type-System, ein Team-Skillset
- Optimus UI ist **API-kompatibel zu PrimeNG v21** → bekanntes Komponenten-Ökosystem,
  80+ accessible Components, Theming über Presets
- SPA → responsive, auf dem Handy bedienbar (Heizung von unterwegs steuern)
- Live-Werte per SSE ohne Polling-Last
- ng-add/ng-update-Workflow, gute IDE-Integration (ESLint, Prettier, Angular Language Service)

### Negativ / Aufwand

- **Zweite Technologie-Stack im Repo** (Node, npm, zwei `package.json`-Ökosysteme)
- Optimus UI ist ein Community-Projekt mit 2 Mitarbeitern → **Bus-Factor-Risiko**.
  Mitigation: Version pinnen (`package-lock.json`), kein Update ohne Review;
  PrimeNG-kompatible API erleichtert einen späteren Wechsel
- Bundle-Größe: SPA lädt ~300–500 KB (gzip) – auf einem NAS im LAN irrelevant
- Zusätzliche CI-Jobs (Node-Build) → Pipeline wird etwas länger
- `ng add` setzt `provideOptimus` – ein Preset-Wechsel (`Aura` → `Lara`) ist ein Einzeiler,
  aber mit visuellen Folgen

### Neutral

- Die UI ist **nicht** Teil des MQTT-Protokolls. Sie nutzt ausschließlich die REST/SSE-API
  (→ [ADR-005](adr-005-api-stil.md)) und kann damit **nicht** die MQTT-Schnittstelle
  nachstellen. Das ist Absicht: die API ist die einzige Wahrheit.
- Ein API-Key oder Reverse-Proxy ist für den Zugriff nötig, sobald die UI nicht nur im LAN liegt
  (offene Frage Q2).

## Verwandte Dokumente

- [ui-angular-optimus](../02-modernisierung/ui-angular-optimus.md)
- [zielversionen](../02-modernisierung/zielversionen.md)
- [ADR-005](adr-005-api-stil.md)
- [ADR-004](adr-004-containerisierung.md)
