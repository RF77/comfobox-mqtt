---
description: "Use when building or changing the Angular web UI in this repository — Angular 22, Optimus UI components, signals, SSE live updates, API client, or the npm build. Covers Optimus UI usage, state management, styling, and the REST/SSE contract with the gateway."
applyTo: ["src/ComfoBox.Web/**/*.ts", "src/ComfoBox.Web/**/*.html", "src/ComfoBox.Web/**/*.scss", "src/ComfoBox.Web/**/*.json"]
---

# Angular / Optimus UI – Regeln

## 1. Stack

| Aspekt | Wert |
|---|---|
| Framework | **Angular 22** (aktiv, Support bis 2028-06) |
| UI-Library | **`@openng/optimus-ui` 2.x** (MIT, Angular 21+, PrimeNG-v21-kompatibel) |
| State | **Signals** + Signal-Store (kein NgRx) |
| Change Detection | `provideZonelessChangeDetection()` |
| Build | `@angular/build` (ESBuild) |
| Styling | SCSS + Optimus-Theming (Preset **Aura**) |
| Node | 22 |

> **Optimus UI verlangt Angular 21+** → Angular 22 ist die Mindestversion.

## 2. Installation & Konfiguration

````bash
ng add @openng/optimus-ui
# Preset wählen: Aura (Default), Lara, Material, Nora
````

Erwartetes Ergebnis in `app.config.ts`:

````ts
import { ApplicationConfig } from '@angular/core';
import { provideOptimus, Aura } from '@openng/optimus-ui';

export const appConfig: ApplicationConfig = {
  providers: [
    provideOptimus({ theme: { preset: Aura } }),
    provideZonelessChangeDetection(),
  ],
};
````

> **API-Kompatibilität zu PrimeNG v21:** `providePrimeNG` → `provideOptimus`,
> `primeng/button` → `@openng/optimus-ui/button`,
> `@primeuix/themes/aura` → `@openng/optimus-ui-themes/aura`.
> Falls ein PrimeNG-Projekt migriert wird:
> `ng generate @openng/optimus-ui@1:migrate-from-primeng`

## 3. Komponenten

````ts
// ✅ Modul-Import pro Komponente (PrimeNG-kompatibel)
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { TableModule } from '@openng/optimus-ui/table';
import { MessageModule } from '@openng/optimus-ui/message';
import { ToastModule } from '@openng/optimus-ui/toast';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { SelectModule } from '@openng/optimus-ui/select';
import { ToggleswitchModule } from '@openng/optimus-ui/toggleswitch';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
````

| Regel | Begründung |
|---|---|
| Komponenten **standalone** importieren | kein NgModule nötig |
| `<p-button label="…" />` | PrimeNG-Syntax beibehalten |
| Icons über `<p-icon>` / `IconModule` | Optimus-Icon-Set |
| Keine direkten CSS-Overrides von PrimeNG-Klassen | bricht beim Theming-Wechsel |

## 4. State mit Signals

````ts
@Injectable({ providedIn: 'root' })
export class ItemsStore {
  private readonly api = inject(ItemsService);

  readonly items = signal<ItemDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly writableItems = computed(() => this.items().filter(i => i.writable));

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.items.set(await this.api.getAll());
    } catch (e) {
      this.error.set(toApiError(e).message);
    } finally {
      this.loading.set(false);
    }
  }
}
````

| Regel | Begründung |
|---|---|
| `signal`/`computed` statt `BehaviorSubject` | Angular-Idiome, zoneless-fähig |
| `inject()` statt Constructor-Injection | Angular-Idiome |
| `effect()` sparsam | nur für echte Seiteneffekte, nicht für Zustandsableitungen |
| Kein `subscribe()` im Template | alte Angular-Syntax |
| Store pro Feature | ein Store pro Bereich (`items`, `health`, `config`) |

## 5. Datenquelle: REST + SSE

### 5.1 API-Client

````ts
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  async get<T>(url: string): Promise<T> {
    const res = await firstValueFrom(this.http.get<T>(url));
    return res;
  }

  async put(url: string, body: unknown): Promise<void> {
    await firstValueFrom(this.http.put(url, body));
  }
}
````

> `baseUrl` ist **relativ** (`/api`) → nginx leitet an das Gateway weiter.
> Keine absolute URL im Code, keine IP-Adressen, keine Ports.

### 5.2 SSE für Live-Werte

Der Browser `EventSource` reconnectet bei Server-Absturz **nicht** zuverlässig → eigener
Reconnect mit Backoff:

````ts
@Injectable({ providedIn: 'root' })
export class EventsService {
  private source?: EventSource;
  private attempt = 0;

  connect(): void {
    this.source = new EventSource('/api/events');
    this.source.addEventListener('value', e => this.value$.next(JSON.parse((e as MessageEvent).data)));
    this.source.addEventListener('snapshot', e => this.snapshot$.next(JSON.parse((e as MessageEvent).data)));
    this.source.onerror = () => {
      this.source?.close();
      this.attempt++;
      const delay = Math.min(30_000, 2 ** this.attempt * 1000);
      setTimeout(() => this.connect(), delay);
    };
  }
}
````

### 5.3 Schreibvorgänge

**Kein optimistisches Update.** Bei Fehler wird der Serverwert wiederhergestellt:

````ts
async save(item: ItemDto, value: unknown): Promise<void> {
  try {
    await this.items.write(item.id, value);
    this.toast.add({ severity: 'success', summary: 'Gespeichert', detail: item.displayName });
  } catch (e) {
    const err = toApiError(e);
    this.toast.add({ severity: 'error', summary: err.error, detail: err.message });
    await this.items.reload(item.id);        // Serverwert wiederherstellen
  }
}
````

> **Warum:** Bei einer Heizungssteuerung darf die UI nie einen Wert anzeigen, den das Gerät
> nicht angenommen hat.

## 6. Fehlerbehandlung & Health-Anzeige

| Fehlerquelle | Anzeige |
|---|---|
| API nicht erreichbar | globales Offline-Banner + letzte bekannte Werte (aus `localStorage`) |
| `Status/Health = degraded` | Warn-Banner mit konkreter Ursache (aus `Status/Bacnet` / `Status/Mqtt`) |
| Schreibfehler 4xx | Toast mit Server-Meldung (deutsch, aus dem `message`-Feld) |
| Schreibfehler 502 (Bus) | Toast + Health-Banner |

**Pflicht:** Health-Banner auf **allen** Seiten (Root-Layout-Komponente), nicht nur im Dashboard.

## 7. Styling

````scss
// ✅ Design-Tokens von Optimus nutzen
.card {
  gap: var(--p-card-body-gap);
}

// ❌ keine festen Farben
.card { background: #1a1a1a; }   // bricht beim Theme-Wechsel
````

| Regel | Begründung |
|---|---|
| CSS-Variablen von Optimus | theme-fähig |
| Kein festes `background`/`color` | Dark/Light-Mode |
| Spacing über das Grid der UI | konsistent |
| Responsive ab 360 px | Handy-Nutzung |
| Kein `!important` | außer es gibt keine Alternative |

## 8. Accessibility

| Aspekt | Regel |
|---|---|
| Icon-Buttons | `aria-label` **immer** setzen |
| Dialoge | `focusOnShow`, Fokus-Management |
| Tabellen | semantisch (`<table>` via `p-table`), Keyboard-Navigation |
| Kontrast | Optimus-Preset erfüllt WCAG AA |
| Fehlermeldungen | `role="alert"` / `aria-live="polite"` |
| Formulare | `<label>` mit `for`, Pflichtfelder markieren |

## 9. TypScript

| Regel | Wert |
|---|---|
| `strict` | `true` |
| `noUncheckedIndexedAccess` | `true` |
| `noImplicitOverride` | `true` |
| `exactOptionalPropertyTypes` | `true` |
| `any` | verboten → `unknown` + Typwache |
| `enum` | `as const` objects bevorzugt (bessere Tree-Shaking) |
| Typen | in `core/models/` – **ein** Ort, keine Inline-Interfaces in Komponenten |

````ts
export interface ItemDto {
  id: string;
  group: string;
  name: string;
  displayName: string;
  kind: 'analog' | 'enum' | 'command';
  value: unknown;
  unit: string | null;
  min: number | null;
  max: number | null;
  writable: boolean;
  requiresExpertMode: boolean;
  stateTopic: string;
  setTopic: string | null;
  lastUpdated: string | null;
  stale: boolean;
}
````

## 10. Struktur

````
src/app/
├── core/           # api/, state/, models/, guards/  → Singleton, framework-unabhängig
└── features/       # dashboard/, items/, config/, diagnostics/  → lazy loaded
````

> **Feature-Lazy-Loading:** `loadComponent: () => import('./features/items/items.routes').then(m => m.ITEM_ROUTES)`
> → kleineres Initial-Bundle.

## 11. Kommandos

````bash
cd src/ComfoBox.Web
npm ci                # nach package.json-Änderungen
npm start             # Dev-Server auf :4200, Proxy → http://localhost:8080
npm run lint
npm run build -- --configuration production
npm test              # falls Tests vorhanden (Vitest/Karma)
````

> `proxy.conf.json` für den Dev-Server:
> ````json
> { "/api": { "target": "http://localhost:8080", "secure": false } }
> ````

## 12. Optimus-Fallstricke

> Diese Fallen sind **in diesem Projekt schon passiert** – nicht neu erfinden.

| Falle | Symptom | Regel |
|---|---|---|
| `p-tabpanel` projiziert per `ng-template` + `*ngTemplateOutlet` | ein per `@if`/`@else` **ersetzter** Zweig bleibt als sichtbarer „Waisen"-Zweig im DOM (der Leave-Pfad greift dort nicht) | Ladehinweise **per CSS-Klasse** schalten: `[class.cb-hidden]` + `display: none !important` |
| `p-message` per `@if` entfernen | die Meldung bleibt (in einem Hintergrund-Tab feuert `animationend` nie) | Schweregrad umschalten statt entfernen |
| statisches `value` auf `p-tabs` | die Reiter lassen sich nicht umschalten | `[(value)]` mit einem Signal |
| `aria-label` als Attribut auf `<p-button>` | der innere `<button>` bekommt **keinen** zugänglichen Namen | Optimus erwartet die **Eigenschaft** `ariaLabel` |
| `readonly`-Array an `[value]` von `p-table` | TS4104 | Modell-Listen `mutable` |
| `takeUntilDestroyed()` ohne Argument in `ngOnInit` | NG0203 | `DestroyRef` übergeben (`ngOnInit` ist kein Injektionskontext) |
| `page.mouse.click` im VS-Code-Browser | kein DOM-Ereignis (der Tab ist nicht sichtbar) | `element.click()` per `page.evaluate` |

## 13. Definition of Done (UI)

- [ ] `npm run lint` grün, keine `any`
- [ ] Produktions-Build erfolgreich
- [ ] Alle Seiten responsiv (360–1920 px)
- [ ] Health-/Fehlerbanner auf allen Seiten
- [ ] Schreibfehler führen zu Toast **und** Serverwert-Rücknahme
- [ ] Live-Werte per SSE (kein Polling)
- [ ] Tastaturbedienung + Screenreader-Labels
- [ ] UI-Texte deutsch
