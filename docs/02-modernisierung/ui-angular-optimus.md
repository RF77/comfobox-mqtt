# UI: Angular + Optimus UI

> Anforderung 2: *„Angular/Optimus UI für Konfiguration und Statusübersicht/Bedienung"*

## 1. Technologie-Entscheidung

| Aspekt | Wert | Begründung |
|---|---|---|
| Framework | **Angular 22** | Aktive Version (Support bis 2028-06); Optimus UI verlangt Angular 21+ |
| UI-Library | **`@openng/optimus-ui` 2.x** | MIT, 80+ accessible Components, Angular-21+-kompatibel, PrimeNG-v21-API-kompatibel, Theming über Presets (Aura/Lara/Material/Nora) |
| State-Management | **Angular Signals** + Signal-Store | Kein NgRx nötig; weniger Boilerplate, ausreichend für diese Größe |
| Daten-Transport | **REST** (Lesen/Schreiben) + **SSE** (Live) | siehe [Datenmodell & API §4](datenmodell-und-api.md#4-sse-server-sent-events-für-live-werte) |
| Build | `@angular/build` (ESBuild) | Standard-Builder ab CLI 17 |
| Hosting | **nginx** als statischer Server + Reverse-Proxy | im selben Container wie das Gateway |

### Warum Optimus UI

| Kriterium | Optimus UI |
|---|---|
| Angular-Version | 21+ → passt zu Angular 22 |
| Lizenz | MIT (kommerziell nutzbar) |
| Accessibility | „80+ accessible components" |
| Theming | design-agnostisch, Presets Aura/Lara/Material/Nora |
| Migration | API-kompatibel mit PrimeNG v21 → Migrationsschematic vorhanden |
| Wartung | aktiv (v2.0.2, 2026) |

> **Install:**
> ```bash
> ng add @openng/optimus-ui
> # wählt automatisch den Preset; Preset kann wechseln:
> ng add @openng/optimus-ui --theme Lara
> ```
> Ergebnis in `app.config.ts`:
> ```ts
> import { provideOptimus, Aura } from '@openng/optimus-ui';
>
> export const appConfig: ApplicationConfig = {
>   providers: [provideOptimus({ theme: { preset: Aura } })],
> };
> ```

## 2. Projektstruktur

````
src/ComfoBox.Web/
├── package.json
├── angular.json
├── nginx.conf
├── Dockerfile
└── src/
    ├── index.html
    ├── main.ts
    ├── styles.scss
    ├── environments/
    │   ├── environment.ts          # apiBaseUrl: '' (relativ → nginx proxy)
    │   └── environment.prod.ts
    └── app/
        ├── app.config.ts
        ├── app.routes.ts
        ├── app.ts / app.html
        ├── core/
        │   ├── api/
        │   │   ├── api-client.ts          # fetch-Wrapper + Fehlerbehandlung
        │   │   ├── items.service.ts
        │   │   ├── health.service.ts
        │   │   ├── config.service.ts
        │   │   └── events.service.ts      # SSE
        │   ├── models/                    # TS-Interfaces (ItemDto, HealthReport, …)
        │   ├── state/
        │   │   ├── items.store.ts         # SignalStore
        │   │   └── health.store.ts
        │   └── guards/
        └── features/
            ├── dashboard/                 # Statusübersicht
            ├── items/                     # Item-Browser + Bedienung
            ├── item-detail/               # Einzelnes Item, Verlauf, Schreiben
            ├── config/                    # Konfiguration
            ├── diagnostics/               # Fehler, Logs, Status
            └── about/
````

## 3. Dashboard (Statusübersicht)

### 3.1 Header

| Element | Inhalt | Datenquelle |
|---|---|---|
| **Health-Banner** | `online` (grün) / `degraded` (gelb) / `offline` (rot) | `GET /api/health` + SSE |
| **Verbindungs-Chips** | BACnet: verbunden / getrennt; MQTT: verbunden / getrennt; Broker-Name | `GET /api/status` |
| **Version + Uptime** | `1.0.0`, `2 h 14 min` | `GET /api/status` |
| **Schreibzähler** | `17 / 200` (Fortschrittsbalken) | `GET /api/status` |

**Fehlerbanner (Anforderung 5):**

````html
@if (health.state() === 'degraded') {
  <p-message severity="warn" styleClass="w-full">
    <p class="font-semibold">Verbindung eingeschränkt</p>
    <p>{{ health.summary() }}</p>
  </p-message>
}
@if (health.state() === 'offline') {
  <p-message severity="error" styleClass="w-full">
    <p class="font-semibold">Gateway nicht erreichbar</p>
    <p>Seit {{ health.unreachableSince() }} keine Verbindung. Automatischer Neustart läuft.</p>
  </p-message>
}
````

> Bei `offline` kommt der Fehler aus dem **letzten** SSE-Event bzw. aus dem lokalen Cache –
> der Server ist ja nicht erreichbar. Die UI speichert den letzten bekannten Status im
> `localStorage`, damit der Nutzer nach einem Browser-Neuladen sieht, was los war.

### 3.2 Kacheln (Optimus UI `p-card`)

| Kachel | Inhalt | Aktion |
|---|---|---|
| **Betriebsart** | `Zone/OperationMode` | Select |
| **Raumsollwert** | `Zone/SetPointNormal` (°C) | Number |
| **Kühlung** | `Zone/HeatingCoolingMode` | Select |
| **Warmwasser** | `Warmwater/…` | Select |
| **Wärmepumpe** | `States/HeatPump/HeatPumpStatus` | – (read-only) |
| **Lüftung** | `Zone/…` | – |

> **Rule:** Auf dem Dashboard stehen nur **Frequently-Used**-Items. Der vollständige Katalog
> steht unter „Geräte".

### 3.3 Schnellaktionen

| Aktion | Verhalten |
|---|---|
| **Gateway neu starten** | `POST /api/service/restart` → Confirm-Dialog → Statusanzeige „wird neu gestartet …" |
| **BACnet neu verbinden** | `POST /api/service/reconnect/bacnet` |
| **MQTT neu verbinden** | `POST /api/service/reconnect/mqtt` |
| **Konfiguration öffnen** | Navigation zu `/config` |

> **Bestätigung beim Neustart:** `p-confirmdialog` mit dem Hinweis, dass alle Werte für
> ~10 s nicht verfügbar sind.

## 4. Item-Browser (vollständiger Katalog)

| Bereich | Inhalt |
|---|---|
| Gruppiert nach | `Zone`, `States`, `Warmwater`, `Config`, `Tests` |
| Suche | Volltext über Name, Beschreibung, Topic |
| Filter | „nur schreibbar", „nur Experten-Modus", „veraltet (stale)" |
| Ansicht | Tabelle (`p-table`) oder Karten (`p-card`) |
| Spalten | Name, Wert, Einheit, Bereich (Min/Max), Topic, Schreibbarkeit, Alter |

### Schreiben

| Werttyp | Optimus-UI-Komponente | Verhalten |
|---|---|---|
| Analog | `p-inputnumber` | Min/Max aus dem Item; `p-button` „Speichern" |
| Enum | `p-select` | Optionen aus `EnumValues`; Wert ist der **Name** |
| Read-only | – | Wert-Anzeige + Copy-Topic-Button |

**Fehlerbehandlung beim Schreiben:**

````ts
async save(item: ItemDto, value: unknown) {
  this.saving.set(item.id, true);
  try {
    await this.items.write(item.id, value);
    this.toast.add({ severity: 'success', summary: 'Gespeichert', detail: item.displayName });
  } catch (e) {
    const err = toApiError(e);
    this.toast.add({ severity: 'error', summary: err.error, detail: err.message });
    // Wert im Formular zurücksetzen auf den Serverwert
    this.items.reload(item.id);
  } finally {
    this.saving.delete(item.id);
  }
}
````

> **Wichtig:** Bei Fehlern wird der **Serverwert** wiederhergestellt – die UI zeigt nie einen
> optimistischen Wert, den das Gerät nicht angenommen hat. Das ist der Unterschied zu einem
> Optimistic-Update und für eine Heizungssteuerung die richtige Wahl.

## 5. Konfigurationsseite

| Bereich | Felder | Persistenz |
|---|---|---|
| **Allgemein** | `BaseTopic`, `PollIntervalSeconds`, `UseMqttRetain` | `PUT /api/config` |
| **Schreibschutz** | `MaxNumberOfWritesPer24h`, `ExpertMode` | `PUT /api/config` |
| **MQTT** | Broker-Liste (Host, Port, User, TLS) | nur Konfigurationsdatei → **Neustart nötig** |
| **BACnet** | Port, Baudrate, Device-ID | nur Konfigurationsdatei → **Neustart nötig** |
| **Diagnose** | Neustart, Reconnects, Log-Level | – |

- `ExpertMode` → `p-toggleswitch` **mit** `p-confirmdialog` (es ermöglicht Schreibzugriffe auf
  sensible Parameter).
- Felder, die einen Neustart erfordern, werden mit einem Hinweis-Chip markiert
  (`p-tag severity="warn"` → „Neustart erforderlich").

## 6. Diagnoseseite

| Ansicht | Inhalt |
|---|---|
| **Health-Checks** | Tabelle aller Checks mit Status, Letzter Erfolg, Nachricht |
| **Fehlerhistorie** | Letzte 50 Fehler (Zeit, Severity, Quelle, Code, Meldung), Filter |
| **Schreibvorgänge** | Zähler, Limit, Zeitfenster |
| **Verbindungen** | Broker-Status, Reconnect-Zähler, Publish-/Receive-Zähler |
| **Rohdaten** | Live-Ansicht der Topics (`ComfoBox/#`) über eine SSE-Debug-Schnittstelle |

## 7. Datenfluss & Signals

````ts
// core/state/items.store.ts
@Injectable({ providedIn: 'root' })
export class ItemsStore {
  private readonly api = inject(ItemsService);
  private readonly events = inject(EventsService);

  readonly items = signal<ItemDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    // SSE → lokaler Store
    this.events.value$.subscribe(evt => {
      if (evt.type === 'value') {
        this.items.update(list =>
          list.map(i => i.id === evt.item.id ? { ...i, value: evt.value, lastUpdated: evt.timestamp, stale: false } : i)
        );
      }
    });
  }

  async load() { … }
}
````

**Regeln:**

| Regel | Begründung |
|---|---|
| `signal`/`computed` statt RxJS-Subscriptions im Template | Angular-Idiome, zoneless-fähig |
| SSE für Live-Werte, REST nur für initiale Ladung und Aktionen | weniger HTTP-Last |
| `provideZonelessChangeDetection()` | moderne Angular-22-Empfehlung, weniger Overhead |
| Kein globales State-Framework | 127 Items + Status – Signal-Stores reichen |

## 8. Theming

| Preset | Einsatz |
|---|---|
| **Aura** | Default – modern, gut lesbar |
| Lara | klassisch, nüchtern |
| Material | Material-Design-Nutzer |
| Nora | kompakt, hohe Informationsdichte |

Dark Mode: `provideOptimus` unterstützt Dark-Mode über die Presets (`darkModeSelector`).
Die UI folgt der Systemeinstellung (`prefers-color-scheme`).

> **Festlegung für dieses Projekt: Aura als Default.** Begründung: gute Lesbarkeit der
>many numbers/temperature values auf einem Heizungs-Dashboard.

## 9. Accessibility

| Aspekt | Maßnahme |
|---|---|
| Bedienbarkeit per Tastatur | Optimus UI ist a11y-konform; `p-table` mit Keyboard-Navigation |
| Kontrast | Preset Aura erfüllt WCAG AA |
| Screenreader | `aria-label` auf allen Icon-Buttons |
| Fokus-Management | `p-dialog` mit `focusOnShow` |
| Sprache | UI-Texte **deutsch**, Code/Bezeichner englisch |

## 10. Build & Deployment

### 10.1 Build

````bash
cd src/ComfoBox.Web
npm ci
npm run build -- --configuration production
# Ergebnis: dist/ComfoBox.Web/browser
````

### 10.2 nginx.conf

````nginx
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # API → Gateway
    location /api/ {
        proxy_pass http://gateway:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SSE: kein Buffering!
    location = /api/events {
        proxy_pass http://gateway:8080;
        proxy_http_version 1.1;
        proxy_set_header Connection '';
        proxy_buffering off;
        proxy_cache off;
        chunked_transfer_encoding off;
        proxy_read_timeout 24h;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
}
````

> **Der `proxy_buffering off;` ist Pflicht** – ohne ihn wartet der Browser, bis nginx 4 KB
> gesammelt hat, und die Live-Werte kommen erst beim Beenden der Verbindung.

### 10.3 Docker-Stage

Im **selben** Image wie das Gateway, damit nur **ein** Container deployed wird:

````dockerfile
FROM node:22-alpine AS web-build
WORKDIR /app
COPY src/ComfoBox.Web/package*.json ./
RUN npm ci
COPY src/ComfoBox.Web/ ./
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS web
COPY --from=web-build /app/dist/ComfoBox.Web/browser /usr/share/nginx/html
COPY src/ComfoBox.Web/nginx.conf /etc/nginx/conf.d/default.conf
````

---

> **Hinweis:** nginx und der .NET-Host in **einem** Container ist unüblich (Zwei-Prozess-
> Supervision). **Empfehlung:** zwei Container im selben Compose-Projekt –
> `comfobox-gateway` (Port 8080) und `comfobox-web` (Port 80, nginx). nginx routet intern auf
> `gateway:8080`. Das ist sauberer, robuster und erlaubt getrennte Neustarts.
> → Siehe [containerisierung](../03-deployment/containerisierung.md) und
> [ADR-004](../05-entscheidungen/adr-004-containerisierung.md).

## 11. Ablösung des WPF-DemoClients

| Funktion | DemoClient (WPF) | Neue UI |
|---|---|---|
| Port/Baudrate wählen | ✓ | ✓ (Konfigurationsseite) |
| „Read All" | ✓ | ✓ (Dashboard-„Aktualisieren") |
| Freier BACnet-Objekt-Leser | ✓ | **entfällt** – nur Expertenfunktion; ersetzt durch den Item-Browser |
| TreeView der Item-Gruppen | ✓ | ✓ (Item-Browser) |
| **Schreiben** | ✓ (jede Änderung schreibt sofort) | ✓ (explizit mit Bestätigung) |
| **Status/Health** | ✗ | ✓ |
| **Konfiguration** | ✗ | ✓ |
| **Läuft im Browser / auf dem Pi** | ✗ | ✓ |

> Der „freie BACnet-Objekt-Leser" (freie Objekt-ID eingeben) ist im Ist-Zustand für das
> **Reverse-Engineering** neuer ComfoBox-Objekte nützlich. Er wird **nicht** in die
> produktive UI übernommen, sondern bleibt als separates Werkzeug unter
> `tools/bacnet-probe` (Konsolen-Tool) verfügbar.

## 12. Definition of Done

- [ ] `ng add @openng/optimus-ui` erfolgreich, `provideOptimus({ theme: { preset: Aura } })` gesetzt
- [ ] Dashboard zeigt Health, Verbindungen, Version, Uptime, Schreibzähler
- [ ] Fehlerbanner bei `degraded` / `offline` **sichtbar und aussagekräftig**
- [ ] Item-Browser zeigt alle 127 Items mit aktuellem Wert, gruppiert, mit Suche
- [ ] Analog- und Enum-Werte sind **schreibbar** mit Validierung (Min/Max, Options)
- [ ] Schreibfehler werden angezeigt und der Serverwert wiederhergestellt
- [ ] Konfigurationsseite ändert `ExpertMode`, Poll-Intervall, Schreiblimit
- [ ] Neustart-Button mit Bestätigung
- [ ] Live-Werte via SSE (ohne Polling-Schleife im Browser)
- [ ] Responsive von 360 px (Handy) bis 1920 px
- [ ] Lighthouse-Score ≥ 90 (Performance/Accessibility)
- [ ] Kein Zone.js-Overhead (`provideZonelessChangeDetection`)
